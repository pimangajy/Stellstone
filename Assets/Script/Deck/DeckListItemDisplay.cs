using UnityEngine;
using TMPro;

public class DeckListItemDisplay : MonoBehaviour, ICardDataHolder
{
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private TextMeshProUGUI cardCostText;
    [SerializeField] private TextMeshProUGUI cardCountText;

    private CardData cardData;

    public void Setup(CardData data, int count)
    {
        this.cardData = data;

        cardNameText.text = data.cardName;
        cardCostText.text = data.manaCost.ToString();

        if (data.rarity == Rarity.전설)
        {
            cardCountText.text = "*"; // 전설 표시 예시
        }
        else
        {
            cardCountText.text = "x" + count;
        }
    }

    public CardData GetCardData()
    {
        return cardData;
    }
}