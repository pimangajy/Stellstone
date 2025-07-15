using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 게임의 턴 순서와 각 단계의 진행을 관리하는 중앙 매니저입니다.
/// </summary>
public class TurnManager : MonoBehaviour
{
    #region Singleton
    public static TurnManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    #endregion

    [Header("턴 상태")]
    [SerializeField] private int turnNumber = 0; // 현재 턴 번호
    [SerializeField] private bool isPlayerTurn = true; // 현재 플레이어의 턴인지 여부

    // 각 턴 단계가 시작될 때 다른 매니저들에게 신호를 보내기 위한 UnityEvent 입니다.
    // 인스펙터에서 다른 스크립트의 함수를 연결할 수 있습니다.
    [Header("턴 이벤트")]
    public UnityEvent OnPlayerTurnStart;
    public UnityEvent OnPlayerTurnEnd;
    public UnityEvent OnEnemyTurnStart;
    public UnityEvent OnEnemyTurnEnd;

    private void Start()
    {
        HandManager.Instance.ToggleHandExpansion(true);
    }

    /// <summary>
    /// 게임이 처음 시작될 때 호출됩니다.
    /// </summary>
    public void StartGame()
    {
        turnNumber = 1;
        StartCoroutine(PlayerTurnCycle());
    }

    /// <summary>
    /// '턴 종료' UI 버튼에서 이 함수를 호출합니다.
    /// </summary>
    public void EndPlayerTurn()
    {
        // 현재 플레이어의 턴일 때만 턴을 종료할 수 있도록 합니다.
        if (isPlayerTurn)
        {
            StartCoroutine(EndPlayerTurnPhase());
        }
    }

    // ===================================================================
    // 플레이어 턴 사이클 (코루틴)
    // ===================================================================
    IEnumerator PlayerTurnCycle()
    {
        // 1. 턴 시작 단계
        OnTurnStart();
        yield return new WaitForSeconds(1f); // 턴 시작 연출 등을 위한 대기 시간

        // 2. 드로우 단계
        OnDrawPhase();
        yield return new WaitForSeconds(0.5f); // 카드 뽑는 애니메이션 대기 시간

        // 3. 메인 단계
        OnMainPhase();
        // 메인 단계에서는 플레이어가 '턴 종료' 버튼을 누를 때까지 계속 대기합니다.
    }

    // ===================================================================
    // 각 턴 단계별 실제 로직
    // ===================================================================

    /// <summary>
    /// [1] 턴 시작 단계: 마나 회복, '턴 시작 시' 효과 처리 등
    /// </summary>
    public void OnTurnStart()
    {
        Debug.Log("플레이어 턴 " + turnNumber + " 시작");
        isPlayerTurn = true;

        // 다른 매니저들에게 "플레이어 턴 시작!" 신호를 보냅니다.
        OnPlayerTurnStart?.Invoke();

        // 예시: PlayerController에게 마나를 회복하라고 명령
        // PlayerController.Instance.RestoreMana();
    }

    /// <summary>
    /// [2] 드로우 단계: 덱에서 카드를 뽑음
    /// </summary>
    private void OnDrawPhase()
    {
        Debug.Log("드로우 단계");

        // 예시: HandManager에게 카드를 한 장 뽑으라고 명령
        HandManager.Instance.DrawRandomCard();
    }

    /// <summary>
    /// [3] 메인 단계: 플레이어가 자유롭게 행동
    /// </summary>
    private void OnMainPhase()
    {
        Debug.Log("메인 단계 시작 - 플레이어 행동 대기 중...");
        // 이 단계에서는 특별한 로직 없이 플레이어의 입력을 기다립니다.
    }

    /// <summary>
    /// [4] 턴 종료 단계: '턴 종료 시' 효과 처리
    /// </summary>
    private IEnumerator EndPlayerTurnPhase()
    {
        isPlayerTurn = false;
        Debug.Log("턴 종료 단계 시작");

        // 예시: FieldManager에게 '내 턴 끝에' 발동되는 모든 효과를 처리하라고 명령
        // FieldManager.Instance.ProcessEndOfTurnEffects(true);

        yield return new WaitForSeconds(1.5f); // 효과 처리 연출을 위한 대기 시간

        // 5. 턴 완전 종료
        OnTurnEnd();

        // 적 턴 시작
        StartCoroutine(EnemyTurnCycle());
    }

    /// <summary>
    /// [5] 턴 완전 종료: 다음 턴 준비
    /// </summary>
    private void OnTurnEnd()
    {
        Debug.Log("플레이어 턴 완전 종료");
        // 다른 매니저들에게 "플레이어 턴 종료!" 신호를 보냅니다.
        OnPlayerTurnEnd?.Invoke();
    }


    // (여기에 적의 턴 사이클 코루틴을 유사하게 구현할 수 있습니다)
    IEnumerator EnemyTurnCycle()
    {
        Debug.Log("적 턴 시작");
        OnEnemyTurnStart?.Invoke();

        // AI가 행동...
        yield return new WaitForSeconds(2f); // 적이 생각하는 척하는 시간

        Debug.Log("적 턴 종료");
        OnEnemyTurnEnd?.Invoke();

        // 다음 플레이어 턴 시작
        turnNumber++;
        StartCoroutine(PlayerTurnCycle());
    }
}
