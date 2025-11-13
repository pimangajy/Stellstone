using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("호버 효과 설정")]
    [Tooltip("마우스 오버 시 커질 배율입니다.")]
    public float hoverScaleMultiplier = 1.2f;
    [Tooltip("마우스 오버 시 위로 올라갈 높이입니다.")]
    public float hoverYOffset = 50f;
    [Tooltip("애니메이션의 속도입니다.")]
    public float animationDuration = 0.2f;

    private RectTransform rectTransform;
    private int originalSiblingIndex; // 원래 렌더링 순서를 기억할 변수
    private CardInHandController cardController;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        cardController = GetComponent<CardInHandController>();
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        // 드래그가 아닐 때만 실행 (클릭과 드래그 구분)
        if (eventData.dragging) return;

        // HandManager에게 핸드를 펼치라고 요청합니다.
        HandManager.Instance.ToggleHandExpansion(true);
    }

    /// <summary>
    /// 마우스 포인터가 카드 위에 올라왔을 때 자동으로 호출됩니다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if(MulliganManager.IsMulliganPhaseActive) return;

        // ★★★ 핵심 수정: 다른 카드가 드래그 중일 때는 아무것도 하지 않습니다. ★★★
        if (CardDragDrop.IsDraggingCard) return;
        // 카드의 현재 상태를 직접 확인합니다.
        if (cardController.GetState() != CardState.Idle) return;
        // 상태를 '호버 중'으로 변경합니다.
        cardController.SetState(CardState.Hovering);

        // 현재 이 카드에 실행 중인 모든 DOTween 애니메이션을 즉시 멈춥니다.
        rectTransform.DOKill();

        // 1. 원래 렌더링 순서를 기억합니다.
        originalSiblingIndex = transform.GetSiblingIndex();
        // 2. 카드를 다른 카드들보다 위에 보이도록 렌더링 순서를 맨 뒤로 보냅니다.
        transform.SetAsLastSibling();

        // 3. DOTween을 사용하여 부드러운 애니메이션을 실행합니다.
        rectTransform.DOScale(Vector3.one * hoverScaleMultiplier, animationDuration).SetEase(Ease.OutQuad);
        rectTransform.DOLocalRotate(Vector3.zero, animationDuration).SetEase(Ease.OutQuad);
        rectTransform.DOAnchorPosY(rectTransform.anchoredPosition.y + hoverYOffset, animationDuration).SetEase(Ease.OutQuad);
    }

    /// <summary>
    /// 마우스 포인터가 카드 위에서 벗어났을 때 자동으로 호출됩니다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (MulliganManager.IsMulliganPhaseActive) return;

        if (CardDragDrop.IsDraggingCard) return;
        // '호버 중' 상태가 아닐 때는 실행하지 않습니다.
        //if (cardController.GetState() != CardState.Hovering) return;

        // 현재 이 카드에 실행 중인 모든 DOTween 애니메이션을 즉시 멈춥니다.
        rectTransform.DOKill();

        // 1. 원래 렌더링 순서로 되돌립니다.
        transform.SetSiblingIndex(originalSiblingIndex);
        // 크기도 원래대로 되돌립니다.
        rectTransform.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            cardController.SetState(CardState.Idle);
        });

        // 2. DOTween을 사용하여 원래 상태로 부드럽게 되돌아가는 애니메이션을 실행합니다.
        // HandManager의 정렬 함수를 호출하여 원래 위치와 각도를 찾아가도록 합니다.
        if (HandManager.Instance != null)
        {
            HandManager.Instance.ArrangeCards();
        }

    }


}
