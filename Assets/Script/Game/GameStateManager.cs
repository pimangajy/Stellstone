using UnityEngine;
using Firebase.Auth;
using System; // Action 사용을 위해 필요

/// <summary>
/// 클라이언트 측의 게임 상태(내 턴 여부, 마나, 페이즈 등)를 중앙에서 관리하는 스크립트입니다.
/// GameClient의 이벤트를 구독하여 데이터를 갱신하고,
/// 상태가 변하면 다시 UI나 다른 매니저들에게 이벤트를 뿌려주는 '허브' 역할을 합니다.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    // ==================================================================
    // 1. 상태 변화 알림 이벤트 (UI나 다른 매니저가 구독함)
    // ==================================================================
    // "페이즈가 바뀌었습니다" (예: "Draw", "Main", "End")
    public event Action<GamePhase> OnPhaseChanged;

    // "내 턴 상태가 바뀌었습니다" (true: 내 턴, false: 상대 턴)
    public event Action<bool> OnTurnChanged;

    // "마나 정보가 바뀌었습니다" (현재 마나, 최대 마나)
    public event Action<string, int, int> OnManaChanged;


    // ==================================================================
    // 2. 내부 데이터 및 프로퍼티
    // ==================================================================
    [Header("게임 상태 정보")]
    [SerializeField] private bool _isMyTurn = false;
    [SerializeField] private GamePhase _currentPhase = GamePhase.MAIN;

    [Header("내 마나")]
    [SerializeField] private int _myCurrentMana = 0;
    [SerializeField] private int _myMaxMana = 0;

    [Header("상대 마나")]
    [SerializeField] private int _oppCurrentMana = 0;
    [SerializeField] private int _oppMaxMana = 0;

    public string MyUid => (GameClient.Instance != null) ? GameClient.Instance.UserUid : null;
    public bool IsMyTurn => _isMyTurn;
    public GamePhase CurrentPhase => _currentPhase;
    public int MyCurrentMana => _myCurrentMana;
    public int MyMaxMana => _myMaxMana;


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    void Start()
    {
        // GameClient의 이벤트 구독
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPhaseStartEvent += HandlePhaseStart;
            GameClient.Instance.OnUpdateManaEvent += HandleUpdateMana;
            GameClient.Instance.OnGameReadyEvent += HandleGameReady;
        }
    }

    void OnDestroy()
    {
        // 구독 해제 (메모리 누수 방지)
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPhaseStartEvent -= HandlePhaseStart;
            GameClient.Instance.OnUpdateManaEvent -= HandleUpdateMana;
            GameClient.Instance.OnGameReadyEvent -= HandleGameReady;
        }
    }

    // ==================================================================
    // 3. 이벤트 핸들러 (서버 데이터를 내 상태로 반영)
    // ==================================================================

    // 게임 시작 시 처리
    private void HandleGameReady(S_GameReady info)
    {
        // 선공 여부 확인 및 설정
        CheckTurn(info.firstPlayerUid);
    }

    // 페이즈 시작 패킷 수신 시
    private void HandlePhaseStart(S_PhaseStart info)
    {
        // 1. 페이즈 값 갱신
        if (_currentPhase != info.phase)
        {
            _currentPhase = info.phase;
            Debug.Log($"[GameStateManager] 페이즈 변경: {_currentPhase}");

            // ★ 상태가 변했음을 UI 등에게 알림
            OnPhaseChanged?.Invoke(_currentPhase);
        }

        // 2. 턴 주인 정보가 같이 왔다면 갱신
        if (!string.IsNullOrEmpty(info.TurnPlayerUid))
        {
            CheckTurn(info.TurnPlayerUid);
        }
    }

    // 마나 업데이트 패킷 수신 시
    private void HandleUpdateMana(S_UpdateMana info)
    {
        if (info.ownerUid == MyUid)
        {
            // 내 마나 정보인 경우
            _myCurrentMana = info.currentMana;
            _myMaxMana = info.maxMana;
            Debug.Log($"[Mana] 내 마나 갱신: {_myCurrentMana}/{_myMaxMana}");
        }
        else
        {
            // 상대방 마나 정보인 경우
            _oppCurrentMana = info.currentMana;
            _oppMaxMana = info.maxMana;
            Debug.Log($"[Mana] 상대 마나 갱신: {_oppCurrentMana}/{_oppMaxMana}");
        }

        // UI 매니저 등이 이 이벤트를 받아서 ownerUid에 따라 다른 UI 텍스트를 고치게 됩니다.
        OnManaChanged?.Invoke(info.ownerUid, info.currentMana, info.maxMana);
    }

    // ==================================================================
    // 4. 내부 로직 헬퍼
    // ==================================================================

    private void CheckTurn(string turnPlayerUid)
    {
        bool wasMyTurn = _isMyTurn;

        if (MyUid == turnPlayerUid)
        {
            _isMyTurn = true;
            if (!wasMyTurn) Debug.Log("나의 턴입니다!");
        }
        else
        {
            _isMyTurn = false;
            if (wasMyTurn) Debug.Log("상대의 턴입니다.");
        }

        // 턴 상태가 이전과 달라졌다면 이벤트 발생
        if (wasMyTurn != _isMyTurn)
        {
            OnTurnChanged?.Invoke(_isMyTurn);
        }
    }
}