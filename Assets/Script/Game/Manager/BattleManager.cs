using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 게임의 전체 규칙, 턴/마나/페이즈 상태 데이터, 서버 패킷 통신을 총괄하는 중앙 매니저입니다.
/// (UI 및 타이머 로직은 BattleManager.UI.cs, 타겟팅 검증은 BattleManager.Targeting.cs에 분리되어 있습니다)
/// </summary>
public partial class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("Game State (게임 상태)")]
    public string myUid;               // 내 계정의 고유 ID
    public bool isPlayerTurn = false;  // 현재 내 턴 여부
    public bool isMulliganPhase = true; // 현재 멀리건 단계 여부
    public GamePhase currentPhase;      // 현재 페이즈

    // --- GameStateManager 호환 프로퍼티 ---
    public bool IsMyTurn => isPlayerTurn;
    public GamePhase CurrentPhase => currentPhase;
    public int MyCurrentMana => playerCurrentMana;
    public int MyMaxMana => playerMaxMana;
    public int OppCurrentMana => enemyCurrentMana;
    public int OppMaxMana => enemyMaxMana;
    public string MyUid => myUid;

    [Header("Hand & Field Data (데이터)")]
    public List<CardInfo> playerHand = new List<CardInfo>();               // 유저 손패
    public List<CardInfo> enemyHand = new List<CardInfo>();                // 적 손패
    public Dictionary<int, EntityData> entities = new Dictionary<int, EntityData>();   // 필드위 객체

    [Header("Mana Data (마나 데이터)")]
    public int playerCurrentMana;      // 현재 사용 가능한 마나
    public int playerMaxMana;          // 이번 턴의 전체 마나 통
    public int enemyCurrentMana;
    public int enemyMaxMana;

    // --- 시스템 이벤트 ---
    public event Action OnStateChanged;
    public event Action OnHandUpdated;

    // --- GameStateManager 호환 이벤트 ---
    public event Action<bool> OnTurnChanged;
    public event Action<GamePhase> OnPhaseChanged;
    public event Action<string, int, int> OnManaChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 서버 통신 이벤트 구독
        if (GameClient.Instance != null)
        {
            GameClient gameClient = GameClient.Instance;

            gameClient.ConnectToServerAsync();

            myUid = GameClient.Instance.UserUid;
            gameClient.OnPhaseStartEvent += HandlePhaseStart;
            gameClient.OnUpdateManaEvent += HandleUpdateMana;
            gameClient.OnGameReadyEvent += HandleGameReady;
            gameClient.validTargetResponse += OnReceiveValidTargets;
            gameClient.validAttackTargetsResponse += OnReceiveValidAttackTargets;
            gameClient.OnUpdateHandCardsEvent += HandleUpdateHandCards;
        }

        OnStateChanged += UpdateManaUI;

        // UI 초기화 (BattleManager.UI.cs)
        InitUI();
    }

    private void OnDisable()
    {
        // 메모리 누수 방지를 위한 이벤트 구독 해제
        if (GameClient.Instance != null)
        {
            GameClient gameClient = GameClient.Instance;

            gameClient.OnPhaseStartEvent -= HandlePhaseStart;
            gameClient.OnUpdateManaEvent -= HandleUpdateMana;
            gameClient.OnGameReadyEvent -= HandleGameReady;
            gameClient.validTargetResponse -= OnReceiveValidTargets;
            gameClient.validAttackTargetsResponse -= OnReceiveValidAttackTargets;
            gameClient.OnUpdateHandCardsEvent -= HandleUpdateHandCards;
        }
    }

    void Update()
    {
        UpdateTimerAndUI();
    }

    // --- 서버 패킷 처리 핸들러 ---

    // 게임 시작시 시작 플레이어가 누구인지 내 손패와 상대손패가 무엇인지 설정
    private void HandleGameReady(S_GameReady info)
    {
        isMulliganPhase = false;
        bool prevTurn = isPlayerTurn;
        isPlayerTurn = (info.firstPlayerUid == myUid);
        playerHand = info.finalHand;
        enemyHand = info.enermyfinalHand;

        if (prevTurn != isPlayerTurn)
        {
            OnTurnChanged?.Invoke(isPlayerTurn);
        }

        OnHandUpdated?.Invoke();
        OnStateChanged?.Invoke();
        RefreshTurnUI();
    }

    // 페이즈 시작마다 실행
    private void HandlePhaseStart(S_PhaseStart info)
    {
        isMulliganPhase = false;
        if (GameMulliganManager.instance != null && GameMulliganManager.instance.mulliganImg != null && GameMulliganManager.instance.mulliganImg.activeSelf)
        {
            GameMulliganManager.instance.mulliganImg.SetActive(false);
            GameMulliganManager.instance.EndMulliganPhase();
        }

        if (currentPhase != info.phase)
        {
            currentPhase = info.phase;
            OnPhaseChanged?.Invoke(currentPhase);
        }

        bool prevTurn = isPlayerTurn;

        // Standby -> Draw -> Main 3개의 페이즈 정보를 보내주지만 info.newTurnPlayerUid의 값은 Standby,Draw 에서만 보냄
        if (info.TurnPlayerUid == myUid)
        {
            switch (info.phase)
            {
                case GamePhase.STANDBY:
                    isPlayerTurn = (info.TurnPlayerUid == myUid);
                    break;
                case GamePhase.DRAW:
                    if (info.hasDrawn && info.drawnCard != null)
                    {
                        if (CardDrawManager.Instance != null)
                        {
                            CardDrawManager.Instance.PerformDrawAnimation(info.drawnCard);
                        }
                    }
                    else
                    {
                        Debug.Log("[BattleManager] 🚫 드로우 불가/스킵 (드로우 봉인 또는 덱 고갈)");
                    }
                    break;
            }
        }
        else
        {
            switch (info.phase)
            {
                case GamePhase.STANDBY:
                    isPlayerTurn = false;
                    break;
                case GamePhase.DRAW:
                    if (info.hasDrawn)
                    {
                        if (OpponentHandVisualizer.Instance != null)
                        {
                            OpponentHandVisualizer.Instance.DrawCard();
                        }
                    }
                    else
                    {
                        Debug.Log("[BattleManager] 🚫 상대방 드로우 불가/스킵 (드로우 봉인 또는 덱 고갈)");
                    }
                    break;
            }
        }

        if (prevTurn != isPlayerTurn)
        {
            OnTurnChanged?.Invoke(isPlayerTurn);
        }

        _turnEndTimeTimestamp = info.turnEndTime;
        SetTimer();

        if (timerSlider != null)
        {
            timerSlider.maxValue = Mathf.Max(60f, remainingTime);
        }

        OnStateChanged?.Invoke();
        RefreshTurnUI();
    }

    // 손패 업데이트마다 실행
    private void HandleUpdateHandCards(S_UpdateHandCards info)
    {
        if (info.updatedCards == null) return;

        foreach (var updatedCard in info.updatedCards)
        {
            int idx = playerHand.FindIndex(c => c.instanceId == updatedCard.instanceId);
            if (idx != -1)
            {
                playerHand[idx] = updatedCard;
            }
        }

        OnHandUpdated?.Invoke();
        OnStateChanged?.Invoke();
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

        OnManaChanged?.Invoke(info.ownerUid, info.currentMana, info.maxMana);
        OnStateChanged?.Invoke();
    }

    // 턴종료 요청 중복 방지 플래그
    [HideInInspector] public bool isTurnEndRequested = false;

    // 턴종료 요청
    public void RequestEndTurn()
    {
        if (isTurnEndRequested) return;
        isTurnEndRequested = true;

        if (GameClient.Instance != null)
        {
            GameClient.Instance.RequestEndTurn();
        }
    }
}