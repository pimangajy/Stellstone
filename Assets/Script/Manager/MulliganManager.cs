using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MulliganManager : MonoBehaviour
{
    #region Singleton
    public static MulliganManager Instance { get; private set; }

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

    [Header("멀리건 설정")]
    [Tooltip("시작 핸드로 뽑을 카드의 수입니다.")]
    public int startingHandSize = 5;
    [Tooltip("카드 한 장을 뽑을 때 걸리는 시간입니다.")]
    public float drawInterval = 0.2f;

    [Header("선택 효과 설정")]
    [Tooltip("선택되었을 때 위로 올라갈 높이입니다.")]
    public float selectionYOffset = 30f;
    [Tooltip("선택되었을 때 적용될 색상 틴트입니다.")]
    public Color selectionColorTint = new Color(0.7f, 0.7f, 0.7f);
    [Tooltip("선택/해제 애니메이션의 속도입니다.")]
    public float animationDuration = 0.2f;

    // ★★★ 추가된 변수들 ★★★
    [Tooltip("멀리건 단계가 활성화되었는지 알려주는 전역 플래그입니다.")]
    public static bool IsMulliganPhaseActive { get; private set; } = true;

    // 교체하기 위해 선택된 카드들을 관리하는 리스트입니다.
    private List<CardInHandController> cardsToReturn = new List<CardInHandController>();

    /// <summary>
    /// UI 버튼에서 호출하여 멀리건 단계를 시작합니다.
    /// </summary>
    public void StartInitialDraw()
    {
        // 코루틴을 사용하여 시간차를 두고 카드를 뽑는 함수를 실행합니다.
        StartCoroutine(DrawInitialCardsRoutine());
    }

    /// <summary>
    /// 정해진 시간 간격으로 시작 카드를 뽑는 코루틴입니다.
    /// </summary>
    private IEnumerator DrawInitialCardsRoutine()
    {
        Debug.Log("초기 드로우를 시작합니다...");

        IsMulliganPhaseActive = true;

        // 시작 핸드 크기만큼 반복합니다.
        for (int i = 0; i < startingHandSize; i++)
        {
            // HandManager를 통해 무작위 카드를 한 장 뽑습니다.
            if (HandManager.Instance != null)
            {
                HandManager.Instance.DrawRandomCard();
            }

            // 정해진 시간(drawInterval)만큼 대기합니다.
            yield return new WaitForSeconds(drawInterval);
        }

        Debug.Log("초기 드로우 완료.");

        // 모든 카드를 뽑은 후, 핸드를 펼치는 함수를 호출합니다.
        if (HandManager.Instance != null)
        {
            HandManager.Instance.ToggleHandExpansion(true);
        }
    }

    /// <summary>
    /// 카드에서 직접 호출하여 교체 목록에 추가하거나 제거합니다.
    /// </summary>
    public void ToggleCardForMulligan(CardInHandController card)
    {
        if (cardsToReturn.Contains(card))
        {
            cardsToReturn.Remove(card);
            Debug.Log(card.cardData.cardName + "을(를) 교체 목록에서 제거했습니다.");
        }
        else
        {
            cardsToReturn.Add(card);
            Debug.Log(card.cardData.cardName + "을(를) 교체 목록에 추가했습니다.");
        }
    }

    /// <summary>
    /// '확인' 버튼에서 호출하여 멀리건을 최종 실행합니다.
    /// </summary>
    public void ConfirmMulligan()
    {
        if (!IsMulliganPhaseActive) return;
        StartCoroutine(PerformMulliganRoutine());
    }

    /// <summary>
    /// 선택된 카드를 덱으로 돌려보내고, 그 수만큼 새로 뽑는 코루틴입니다.
    /// </summary>
    private IEnumerator PerformMulliganRoutine()
    {
        Debug.Log(cardsToReturn.Count + "장의 카드를 교체합니다.");

        // 멀리건 단계를 종료합니다.
        IsMulliganPhaseActive = false;

        int cardCountToRedraw = cardsToReturn.Count;

        // 선택된 카드들을 핸드에서 제거하고 파괴합니다.
        foreach (var card in cardsToReturn)
        {
            if (HandManager.Instance != null)
            {
                HandManager.Instance.RemoveCardFromHand(card);
            }
            Destroy(card.gameObject);
        }
        cardsToReturn.Clear(); // 리스트 비우기

        // 잠시 대기 후, 교체한 수만큼 카드를 다시 뽑습니다.
        yield return new WaitForSeconds(0.5f);

        Debug.Log(cardCountToRedraw + "장의 카드를 새로 뽑습니다.");
        for (int i = 0; i < cardCountToRedraw; i++)
        {
            if (HandManager.Instance != null)
            {
                HandManager.Instance.DrawRandomCard();
            }
            yield return new WaitForSeconds(drawInterval);
        }

        // 모든 과정이 끝나면 핸드를 다시 펼칩니다.
        if (HandManager.Instance != null)
        {
            HandManager.Instance.ToggleHandExpansion(false);
        }
        Debug.Log("멀리건 완료!");
    }
}
