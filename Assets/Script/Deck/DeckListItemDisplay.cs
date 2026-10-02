using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 덱 편성 화면의 오른쪽 '현재 덱 리스트'에 들어가는 카드 줄(Item) 하나를 관리합니다.
/// 마우스를 올리면 좌측에 카드 원본 미리보기를 표시합니다.
/// </summary>
public class DeckListItemDisplay : MonoBehaviour, ICardDataHolder, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TextMeshProUGUI cardNameText;  // 카드 이름 (예: "화염구")
    [SerializeField] private TextMeshProUGUI cardCostText;  // 마나 코스트 (예: "4")
    [SerializeField] private TextMeshProUGUI cardCountText; // 장수 (예: "x2" 또는 전설 "*")

    private CardData cardData;

    public void Setup(CardData data, int count)
    {
        this.cardData = data;

        if (cardNameText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(cardNameText, minSize: 11f, maxSize: 22f, wordWrap: false, overflowMode: TextOverflowModes.Ellipsis);
            cardNameText.text = data.cardName;
        }
        if (cardCostText != null) cardCostText.text = data.manaCost.ToString();

        if (cardCountText != null)
        {
            cardCountText.text = "x" + count;
        }
    }

    public CardData GetCardData()
    {
        return cardData;
    }

    /// <summary>
    /// 마우스 커서가 덱 슬롯 위로 올라왔을 때 원본 카드 미리보기를 표시합니다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (cardData != null)
        {
            DeckCardPreviewManager.Instance?.ShowPreview(cardData, transform.position);
        }
    }

    /// <summary>
    /// 마우스 커서가 덱 슬롯을 벗어났을 때 미리보기를 숨깁니다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        DeckCardPreviewManager.Instance?.HidePreview();
    }

    private void OnDisable()
    {
        DeckCardPreviewManager.Instance?.HidePreview();
    }
}
