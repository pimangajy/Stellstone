using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 게임의 전체적인 규칙, 플레이어 데이터, 서버와의 동기화 상태를 관리하는 중앙 제어 스크립트입니다.
/// 멀티플레이어 환경에서 서버로부터 받은 데이터를 로컬(클라이언트)에 캐싱하고 UI에 알리는 역할을 합니다.
/// </summary>
public class BattleManager : MonoBehaviour
{
    // 어디서든 접근 가능하게 하는 싱글톤 인스턴스
    public static BattleManager Instance;

    [Header("Game State (게임 상태)")]
    public string myUid;               // 내 계정의 고유 ID (Firebase UID)
    public bool isPlayerTurn = false;  // 현재가 내 턴인지 여부
    public string currentPhase;        // 현재 진행 중인 페이즈 (Draw, Main, End 등)
    public float remainingTime;        // 화면에 표시할 남은 시간 (초)
    private long _turnEndTimeTimestamp; // 서버에서 보낸 턴 종료 시점의 Unix Timestamp (동기화 기준점)

    [Header("Player Mana Data (마나 데이터)")]
    public int playerCurrentMana;      // 내 현재 마나
    public int playerMaxMana;          // 내 이번 턴 최대 마나
    public int enemyCurrentMana;       // 상대 현재 마나
    public int enemyMaxMana;           // 상대 최대 마나

    [Header("Hand & Field Data (카드 및 전장 데이터)")]
    // 내 손패 카드 리스트 (서버의 데이터와 동기화됨)
    public List<CardInfo> playerHand = new List<CardInfo>();
    // 상대방의 손패 개수 (보안상 실제 카드 정보 대신 개수만 관리)
    public int enemyHandCount = 0;
    // 필드에 존재하는 모든 개체(리더, 하수인 등)를 ID 기반으로 저장하는 사전(Dictionary)
    // entityId를 키로 사용하여 특정 하수인의 상태를 빠르게 조회할 수 있습니다.
    public Dictionary<int, EntityData> entities = new Dictionary<int, EntityData>();

    // --- UI 및 다른 시스템을 위한 이벤트 (Observer Pattern) ---
    // 값이 변경되었을 때 이 이벤트를 구독 중인 UI 스크립트들이 스스로 화면을 갱신하게 합니다.
    public event Action OnStateChanged;             // 턴, 페이즈, 마나 등 전반적인 상태 변경 시
    public event Action OnHandUpdated;              // 손패 카드 구성이 바뀌었을 때
    public event Action<List<EntityData>> OnEntitiesUpdated; // 필드의 개체 정보가 갱신되었을 때

    void Awake()
    {
        // 싱글톤 초기화: 인스턴스가 없으면 자신을 할당, 있으면 중복 파괴
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // 통신 담당인 GameClient의 이벤트들을 구독합니다.
        // 서버에서 특정 패킷이 올 때마다 아래 핸들러 함수들이 실행됩니다.
        if (GameClient.Instance != null)
        {
            myUid = GameClient.Instance.UserUid;
            GameClient.Instance.OnPhaseStartEvent += HandlePhaseStart;
            GameClient.Instance.OnUpdateManaEvent += HandleUpdateMana;
            GameClient.Instance.OnEntitiesUpdatedEvent += HandleEntitiesUpdated;
            GameClient.Instance.OnGameReadyEvent += HandleGameReady;
        }
    }

    void Update()
    {
        // 서버에서 받은 종료 시간이 설정되어 있다면, 매 프레임 남은 시간을 계산합니다.
        if (_turnEndTimeTimestamp > 0)
        {
            UpdateRemainingTime();
        }
    }

    // --- 서버 패킷 처리 핸들러 (Server -> Client) ---

    /// <summary>
    /// 게임 준비 완료 패킷 처리: 선공 여부와 초기 손패를 설정합니다.
    /// </summary>
    private void HandleGameReady(S_GameReady info)
    {
        isPlayerTurn = (info.firstPlayerUid == myUid);
        playerHand = info.finalHand;

        // 데이터가 변경되었음을 UI에 알림
        OnHandUpdated?.Invoke();
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 페이즈/턴 시작 패킷 처리: 현재 턴 주인과 종료 예정 시간을 동기화합니다.
    /// </summary>
    private void HandlePhaseStart(S_PhaseStart info)
    {
        currentPhase = info.phase;
        isPlayerTurn = (info.newTurnPlayerUid == myUid);

        // 서버의 시간 기준(Unix Timestamp)을 저장하여 클라이언트 간 시간 오차를 방지합니다.
        _turnEndTimeTimestamp = info.turnEndTime;

        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 마나 갱신 패킷 처리: 플레이어 또는 상대방의 마나 정보를 업데이트합니다.
    /// </summary>
    private void HandleUpdateMana(S_UpdateMana info)
    {
        if (info.ownerUid == myUid)
        {
            playerCurrentMana = info.currentMana;
            playerMaxMana = info.maxMana;
        }
        else
        {
            enemyCurrentMana = info.currentMana;
            enemyMaxMana = info.maxMana;
        }
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 개체 상태 갱신 패킷 처리: 필드 위 하수인들의 체력, 공격력, 위치 등을 업데이트합니다.
    /// </summary>
    private void HandleEntitiesUpdated(List<EntityData> updatedEntities)
    {
        foreach (var entity in updatedEntities)
        {
            // Dictionary의 특성을 이용해 기존 데이터를 덮어쓰거나 새로 추가합니다.
            entities[entity.entityId] = entity;
        }
        // 하수인 오브젝트들에게 변경된 데이터를 전달합니다.
        OnEntitiesUpdated?.Invoke(updatedEntities);
    }

    // --- 내부 헬퍼 함수 ---

    /// <summary>
    /// 서버 종료 타임스탬프와 현재 시스템 시간의 차이를 계산하여 남은 초를 구합니다.
    /// </summary>
    private void UpdateRemainingTime()
    {
        // 현재 UTC 시간을 Unix 초 단위로 변환
        long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // 종료 시간에서 현재 시간을 빼서 남은 시간을 구함
        float diff = _turnEndTimeTimestamp - currentUnixTime;

        // 시간이 음수가 되지 않도록 방지
        remainingTime = Mathf.Max(0, diff);
    }

    /// <summary>
    /// UI의 '턴 종료' 버튼을 눌렀을 때 실행될 요청 함수입니다.
    /// </summary>
    public void RequestEndTurn()
    {
        // 내 턴이 아니면 요청하지 않음
        if (!isPlayerTurn) return;

        // 서버에 "턴을 종료하겠다"는 의사를 패킷으로 보냅니다.
        GameClient.Instance.SendMessageAsync(new C_EndTurn { action = "END_TURN" });
    }

    private void OnDestroy()
    {
        // 오브젝트 파괴 시 이벤트 구독을 해제하여 메모리 누수를 방지합니다.
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPhaseStartEvent -= HandlePhaseStart;
            GameClient.Instance.OnUpdateManaEvent -= HandleUpdateMana;
            GameClient.Instance.OnEntitiesUpdatedEvent -= HandleEntitiesUpdated;
            GameClient.Instance.OnGameReadyEvent -= HandleGameReady;
        }
    }
}