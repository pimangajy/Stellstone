using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// 게임의 전체 규칙, 서버 동기화 데이터, 그리고 턴 종료 및 마나 UI를 통합하여 관리하는 스크립트입니다.
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    [Header("Game State (게임 상태)")]
    public string myUid;               // 내 계정의 고유 ID
    public bool isPlayerTurn = false;  // 현재 내 턴 여부
    public string currentPhase;        // 현재 페이즈
    public float remainingTime;        // 서버 동기화 기반 남은 시간
    private long _turnEndTimeTimestamp; // 서버에서 보낸 종료 시점

    [Header("Hand & Field Data (데이터)")]
    public List<CardInfo> playerHand = new List<CardInfo>();
    public List<CardInfo> enemyHand = new List<CardInfo>();
    public Dictionary<int, EntityData> entities = new Dictionary<int, EntityData>();

    [Header("Mana Data (마나 데이터)")]
    public int playerCurrentMana;      // 현재 사용 가능한 마나
    public int playerMaxMana;          // 이번 턴의 전체 마나 통
    public int enemyCurrentMana;
    public int enemyMaxMana;

    [Header("Mana UI Settings (마나 시각화)")]
    public TextMeshProUGUI manaText;      // "3 / 5" 처럼 숫자로 표시할 텍스트
    public Image[] playerManaCrystals;    // 10개의 마나 이미지 배열
    public Sprite manaOnSprite;           // 채워진 마나 이미지 (On)
    public Sprite manaOffSprite;          // 사용한 마나 이미지 (Empty Slot)
    public Sprite manaLockedSprite;       // 아직 잠긴 마나 이미지 (선택 사항, 투명하게 처리 가능)
    public Color manaHighlightColor = Color.yellow; // 카드 드래그 시 소모될 마나 강조 색상

    [Header("Turn End UI (턴 종료 UI)")]
    public Button turnButton;          // 턴 종료 버튼
    public TextMeshProUGUI statusText; // 버튼 중앙 텍스트 ("나의 턴" 등)
    public Slider timerSlider;         // 시간 게이지 슬라이더
    public Image sliderFillImage;      // 슬라이더 색상 변경을 위한 이미지
    float prevRemainingTime;           // 턴종료 타이밍을 위한 변수

    [Header("UI Settings (UI 설정)")]
    public float warningThreshold = 10f; // 경고 색상 시작 시간 (초)
    public Color myTurnColor = new Color(0.2f, 0.8f, 0.4f);   // 내 턴 색상 (초록)
    public Color enemyTurnColor = new Color(0.9f, 0.3f, 0.2f); // 상대 턴 색상 (빨강)
    public Color warningColor = new Color(1f, 0.6f, 0f);       // 경고 색상 (주황)

    // --- 시스템 이벤트 ---
    public event Action OnStateChanged;
    public event Action OnHandUpdated;
    public event Action<List<EntityData>> OnEntitiesUpdated;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // 서버 통신 이벤트 구독
        if (GameClient.Instance != null)
        {
            GameClient.Instance.ConnectToServerAsync();

            myUid = GameClient.Instance.UserUid;
            GameClient.Instance.OnPhaseStartEvent += HandlePhaseStart;
            GameClient.Instance.OnUpdateManaEvent += HandleUpdateMana;
            GameClient.Instance.OnEntitiesUpdatedEvent += HandleEntitiesUpdated;
            GameClient.Instance.OnGameReadyEvent += HandleGameReady;
        }

        OnStateChanged += UpdateManaUI;

        // 버튼 클릭 이벤트 연결
        if (turnButton != null)
            turnButton.onClick.AddListener(RequestEndTurn);

        // 슬라이더 초기 설정
        if (timerSlider != null)
        {
            timerSlider.minValue = 0;
            timerSlider.interactable = false;
        }
    }

    private void OnDisable()
    {
        // 메모리 누수 방지를 위한 이벤트 구독 해제
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPhaseStartEvent -= HandlePhaseStart;
            GameClient.Instance.OnUpdateManaEvent -= HandleUpdateMana;
            GameClient.Instance.OnEntitiesUpdatedEvent -= HandleEntitiesUpdated;
            GameClient.Instance.OnGameReadyEvent -= HandleGameReady;
        }
    }

    void Update()
    {
        // 1. 서버 동기화 기반 시간 계산
        if (_turnEndTimeTimestamp > 0)
        {
            UpdateRemainingTime();
        }

        // 2. UI 슬라이더 업데이트
        UpdateTimerUI();
    }

    // --- 서버 패킷 처리 핸들러 ---

    // 게임 시작시 시작 플레이어가 누구인지 내 손패와 상대폰패가 무엇인지 설정
    private void HandleGameReady(S_GameReady info)
    {
        isPlayerTurn = (info.firstPlayerUid == myUid);
        playerHand = info.finalHand;
        enemyHand = info.enermyfinalHand;

        OnHandUpdated?.Invoke();
        OnStateChanged?.Invoke();
    }

    // 페이즈 시작마다 실행
    private void HandlePhaseStart(S_PhaseStart info)
    {
        currentPhase = info.phase;
        // Standby -> Draw -> Main 3개의 페이즈 정보를 보내주지만 info.newTurnPlayerUid의 값은 Standby에서만 보냄
        if (!string.IsNullOrEmpty(info.newTurnPlayerUid))
        {
            isPlayerTurn = (info.newTurnPlayerUid == myUid);
        }
        _turnEndTimeTimestamp = info.turnEndTime;
        SetTimer();

        if (timerSlider != null)
        {
            timerSlider.maxValue = Mathf.Max(60f, remainingTime);
        }

        OnStateChanged?.Invoke();
        RefreshTurnUI();
        Debug.Log($"[BattleManager] 페이즈 시작 {info.newTurnPlayerUid}의 턴");
    }

    // 마나 업데이트마다 실행
    private void HandleUpdateMana(S_UpdateMana info)
    {
        if (info.ownerUid == myUid)
        {
            playerCurrentMana = info.currentMana;
            playerMaxMana = info.maxMana;
            UpdateManaUI(); // 내 마나가 바뀌었을 때만 이미지 갱신
        }
        else
        {
            enemyCurrentMana = info.currentMana;
            enemyMaxMana = info.maxMana;
        }
        OnStateChanged?.Invoke();
    }

    // 필드가 변할때마다 실행
    private void HandleEntitiesUpdated(List<EntityData> updatedEntities)
    {
        foreach (var entity in updatedEntities)
        {
            entities[entity.entityId] = entity;
        }
        OnEntitiesUpdated?.Invoke(updatedEntities);
    }

    // --- 내부 헬퍼 및 UI 로직 ---

    /// <summary>
    /// 10개의 마나 수정을 현재 마나와 최대 마나에 맞춰 시각화합니다.
    /// </summary>
    private void UpdateManaUI()
    {
        // 1. 텍스트 업데이트 (예: 3 / 5)
        if (manaText != null)
        {
            manaText.text = $"{playerCurrentMana} / {playerMaxMana}";
        }

        // 2. 이미지 배열 업데이트 (최대 10개 가정)
        if (playerManaCrystals == null || playerManaCrystals.Length == 0) return;

        for (int i = 0; i < playerManaCrystals.Length; i++)
        {
            // 인덱스는 0부터 시작하므로 i+1과 마나 값을 비교합니다.
            int slotNumber = i + 1;

            if (slotNumber <= playerCurrentMana)
            {
                // 현재 사용 가능한 마나 칸 (On)
                playerManaCrystals[i].sprite = manaOnSprite;
                playerManaCrystals[i].gameObject.SetActive(true);
                playerManaCrystals[i].color = Color.white;
            }
            else if (slotNumber <= playerMaxMana)
            {
                // 이번 턴에 이미 사용했거나 비어있는 마나 칸 (Off)
                playerManaCrystals[i].sprite = manaOffSprite;
                playerManaCrystals[i].gameObject.SetActive(true);
                playerManaCrystals[i].color = Color.white;
            }
            else
            {
                // 아직 잠겨있는 마나 칸 (Locked)
                if (manaLockedSprite != null)
                {
                    playerManaCrystals[i].sprite = manaLockedSprite;
                    playerManaCrystals[i].gameObject.SetActive(true);
                }
                else
                {
                    // 잠긴 이미지가 없으면 아예 비활성화하거나 반투명하게 처리
                    playerManaCrystals[i].gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// 카드를 드래그할 때 호출하여 소모될 예정인 마나를 시각적으로 강조합니다.
    /// </summary>
    /// <param name="cost">강조할 마나 개수</param>
    public void HighlightManaCost(int cost)
    {
        // 먼저 UI를 기본 상태로 되돌려놓고 시작합니다.
        UpdateManaUI();

        if (cost <= 0 || !isPlayerTurn) return;

        // 현재 가지고 있는 마나 중에서 뒤에서부터 cost만큼 강조합니다.
        int highlightedCount = 0;
        for (int i = playerCurrentMana - 1; i >= 0 && highlightedCount < cost; i--)
        {
            playerManaCrystals[i].color = manaHighlightColor;
            highlightedCount++;
        }

        // 만약 마나가 부족하다면 부족한 부분만큼 경고(빨간색 등)를 줄 수도 있습니다. (선택 사항)
        if (cost > playerCurrentMana)
        {
            // 예: 마나가 부족함을 알리기 위해 활성화된 모든 마나를 붉게 표시
            for (int i = 0; i < playerCurrentMana; i++)
            {
                playerManaCrystals[i].color = Color.red;
            }
        }
    }

    // 턴종료 타이밍을 맞추기 위한 변수 초기화
    public void SetTimer()
    {
        prevRemainingTime = float.MaxValue; // 초기화
    }

    // 타이머 계산
    private void UpdateRemainingTime()
    {
        long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        float diff = _turnEndTimeTimestamp - currentUnixTime;
        remainingTime = Mathf.Max(0, diff);
        // "처음 0이 되는 순간"만 감지
        if (prevRemainingTime > 0 && remainingTime <= 0)
        {
            Debug.Log("시간 초과로 턴 변경");
            RequestEndTurn();
        }

        prevRemainingTime = remainingTime;  // prevRemainingTime = 0 이 되면서 턴종료 증복 실행 방지

    }

    // 타이머 감소
    private void UpdateTimerUI()
    {
        if (timerSlider == null) return;

        timerSlider.value = remainingTime;

        if (isPlayerTurn && remainingTime <= warningThreshold)
        {
            if (sliderFillImage != null) sliderFillImage.color = warningColor;
        }
    }

    // 턴종료 버튼 설정
    private void RefreshTurnUI()
    {
        if (turnButton == null || statusText == null) return;

        if (isPlayerTurn)
        {
            statusText.text = "나의 턴";
            statusText.color = Color.white;
            turnButton.interactable = true;
            if (sliderFillImage != null) sliderFillImage.color = myTurnColor;
        }
        else
        {
            statusText.text = "상대의 턴";
            statusText.color = Color.gray;
            turnButton.interactable = false;
            if (sliderFillImage != null) sliderFillImage.color = enemyTurnColor;
        }
    }

    // 턴종료
    public void RequestEndTurn()
    {
        GameClient.Instance.RequestEndTurn();
    }
}