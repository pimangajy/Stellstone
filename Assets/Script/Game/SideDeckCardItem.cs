using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 사이드 패널(SidePannel) 내에 생성된 각 사이드 카드에 부착되어
/// 마우스 호버 시 카드 상세 정보 미리보기(DeckCardPreviewManager) 및 마나 하이라이트(BattleManager),
/// 카드 클릭 시 서버에 카드 획득 요청을 전달하는 컴포넌트입니다.
/// </summary>
public class SideDeckCardItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private CardData _cardData;
    private CardInfo _cardInfo;
    private Action<SideDeckCardItem> _onClickAction;
    private bool _isHovered = false;

    public CardData CardData => _cardData;
    public CardInfo CardInfo => _cardInfo;

    /// <summary>
    /// 카드 데이터 및 클릭 콜백을 초기화합니다.
    /// </summary>
    public void Init(CardData data, CardInfo info, Action<SideDeckCardItem> onClickAction)
    {
        _cardData = data;
        _cardInfo = info;
        _onClickAction = onClickAction;
    }

    /// <summary>
    /// 마우스 커서를 카드 위에 올렸을 때:
    /// 1. 기존 필드 하수인과 동일한 DeckCardPreviewManager 원본 카드 및 키워드 상세 정보 팝업 표시
    /// 2. 카드의 코스트만큼 마나 크리스탈 강조 표시 (BattleManager)
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;

        if (_cardData != null && DeckCardPreviewManager.Instance != null)
        {
            DeckCardPreviewManager.Instance.ShowPreview(_cardData, transform.position);
        }

        if (BattleManager.Instance != null)
        {
            int cost = _cardInfo != null ? _cardInfo.currentCost : (_cardData != null ? _cardData.manaCost : 0);
            BattleManager.Instance.HighlightManaCost(cost);
        }
    }

    /// <summary>
    /// 마우스 커서가 카드에서 벗어났을 때:
    /// 1. 상세 정보 미리보기 닫기
    /// 2. 마나 크리스탈 하이라이트 초기화
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHoverState();
    }

    /// <summary>
    /// 카드를 클릭했을 때 서버에 사이드 카드 획득 요청 전달
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        _onClickAction?.Invoke(this);
    }

    private void ClearHoverState()
    {
        if (_isHovered)
        {
            _isHovered = false;
            DeckCardPreviewManager.Instance?.HidePreview();
            BattleManager.Instance?.ResetHighlights();
        }
    }

    private void OnDisable()
    {
        ClearHoverState();
    }

    private void OnDestroy()
    {
        ClearHoverState();
    }
}
