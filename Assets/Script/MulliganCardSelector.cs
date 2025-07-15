using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class MulliganCardSelector : MonoBehaviour, IPointerClickHandler
{
    public bool isSelected = false;
    private CardInHandController cardController;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;


    void Awake()
    {
        cardController = GetComponent<CardInHandController>();
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// 카드가 클릭되었을 때 호출됩니다.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 멀리건 단계가 아니거나, 드래그 중이었다면 아무것도 하지 않습니다.
        if (!MulliganManager.IsMulliganPhaseActive || eventData.dragging) return;

        // 선택 상태를 토글합니다.
        isSelected = !isSelected;

        // MulliganManager에게 선택 상태가 변경되었음을 알립니다.
        MulliganManager.Instance.ToggleCardForMulligan(cardController);

        // 시각적 효과를 적용합니다.
        AnimateSelection(isSelected);
    }

    /// <summary>
    /// 선택 상태에 따라 카드를 위로 올리거나 내리는 애니메이션을 실행합니다.
    /// </summary>
    private void AnimateSelection(bool select)
    {
        // 애니메이션 시작 전에 원래 위치를 저장해 둡니다.
        if (select)
        {
            originalPosition = rectTransform.anchoredPosition;
        }

        rectTransform.DOKill();

        // 선택되면 위로, 해제되면 원래 위치로 이동합니다.
        Vector2 targetPosition = select ? originalPosition + new Vector2(0, MulliganManager.Instance.selectionYOffset) : originalPosition;
        Color targetColor = select ? MulliganManager.Instance.selectionColorTint : Color.white;

        rectTransform.DOAnchorPos(targetPosition, MulliganManager.Instance.animationDuration).SetEase(Ease.OutQuad);
        // canvasGroup.DOColor(targetColor, animationDuration); // DOTween은 CanvasGroup의 색상도 변경할 수 있습니다.
    }
}
