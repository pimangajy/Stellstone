using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 상대방 AI의 행동을 제어하는 클래스입니다.
/// 드로우, 카드 사용, 턴 종료의 기본적인 흐름을 처리합니다.
/// </summary>
public class AIBattleController : MonoBehaviour
{
    public static AIBattleController Instance;

    [Header("설정")]
    [Tooltip("AI의 행동 사이의 지연 시간 (초)")]
    public float actionDelay = 1.5f;

    // 현재 AI의 턴인지 확인하는 플래그 (GameStateManager 등에서 설정)
    public bool isAITurn = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// AI의 턴을 시작합니다. GameStateManager에서 호출하거나 이벤트를 통해 실행합니다.
    /// </summary>
    public void StartAITurn()
    {
        isAITurn = true;
        Debug.Log("<color=red>[AI]</color> AI의 턴이 시작되었습니다.");
        StartCoroutine(AITurnRoutine());
    }

    private IEnumerator AITurnRoutine()
    {
        // 1. 카드 드로우
        yield return new WaitForSeconds(actionDelay);
        ExecuteDraw();

        // 2. 손패 확인 및 카드 사용 로직
        yield return new WaitForSeconds(actionDelay);
        yield return StartCoroutine(PlayCardsFromHand());

        // 3. 더 이상 할 수 있는 행동이 없으면 턴 종료
        yield return new WaitForSeconds(actionDelay);
        EndAITurn();
    }

    private void ExecuteDraw()
    {
        Debug.Log("<color=red>[AI]</color> 카드를 드로우합니다.");
        // 기존 CardDrawManager를 호출 (대상은 AI/Enemy여야 함)
        // CardDrawManager.Instance.DrawCard(Owner.Enemy); // 예시 구조
    }

    private IEnumerator PlayCardsFromHand()
    {
        Debug.Log("<color=red>[AI]</color> 손패를 확인하고 카드를 사용합니다.");

        // HandInteractionManager 등을 통해 AI의 손패 리스트를 가져온다고 가정
        // List<GameCardDisplay> aiHand = HandInteractionManager.Instance.GetEnemyHand(); 

        bool canPlayMore = true;

        while (canPlayMore)
        {
            // TODO: 현재 코스트(마나)와 카드의 비용을 비교하여 사용 가능한 카드 탐색
            // GameCardDisplay targetCard = FindUsableCard(aiHand);

            /* if (targetCard != null)
            {
                Debug.Log($"<color=red>[AI]</color> 카드 {targetCard._cardData.cardName} 사용!");
                // 카드 사용 로직 실행 (필드 배치 등)
                // BattleManager.Instance.PlayCard(targetCard, Owner.Enemy);
                
                yield return new WaitForSeconds(actionDelay);
            }
            else
            {
                canPlayMore = false;
            }
            */

            // 테스트 단계이므로 한 번 확인 후 종료하도록 설정
            canPlayMore = false;
            yield return null;
        }
    }

    public void EndAITurn()
    {
        Debug.Log("행동 종료, 턴을 넘깁니다.");
        isAITurn = false;

        // TurnEndController를 통해 턴 종료 신호를 보냄
        if (GameClient.Instance != null)
        {
            GameClient.Instance.RequestEndTurn(); // 버튼 클릭 함수 재사용 혹은 전용 함수 호출
        }
    }
}