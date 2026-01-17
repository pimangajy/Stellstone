using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeckCardDisplay : MonoBehaviour, ICardDataHolder
{
    [Header("UI Elements")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI tribeText;

    public Image artworkImage;
    public Image rarityGemImage;

    public GameObject attackObject;
    public GameObject healthObject;

    private CardData cardData;

    public void Setup(CardData data)
    {
        this.cardData = data;

        nameText.text = cardData.cardName;
        costText.text = cardData.manaCost.ToString();
        descriptionText.text = cardData.description;

        if (cardData.minionTribe != MinionTribe.없음)
        {
            tribeText.gameObject.SetActive(true);
            tribeText.text = cardData.minionTribe.ToString();
        }
        else
        {
            tribeText.gameObject.SetActive(false);
        }

        if (cardData.cardType == CardType.하수인)
        {
            attackObject.SetActive(true);
            healthObject.SetActive(true);
            attackText.text = cardData.attack.ToString();
            healthText.text = cardData.health.ToString();
        }
        else
        {
            attackObject.SetActive(false);
            healthObject.SetActive(false);
        }

        if (cardData.thumbnail != null)
        {
            artworkImage.sprite = cardData.thumbnail;
        }

        // [수정] 희귀도 Enum 전달
        SetRarityVisuals(cardData.rarity);
    }

    public CardData GetCardData()
    {
        return cardData;
    }

    // [수정] 매개변수 string -> Rarity 변경
    private void SetRarityVisuals(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.일반:
                rarityGemImage.color = Color.white;
                break;
            case Rarity.희귀:
                rarityGemImage.color = Color.blue;
                break;
            case Rarity.영웅:
                rarityGemImage.color = new Color(0.5f, 0, 1);
                break;
            case Rarity.전설:
                rarityGemImage.color = Color.yellow;
                break;
            default:
                rarityGemImage.color = Color.gray;
                break;
        }
    }
}