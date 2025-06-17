using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardDisplay : MonoBehaviour
{
    // 이 카드가 참조할 데이터 원본입니다.
    public CardData cardData;

    [Header("UI 연결")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI manaText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI healthText;
    public Image artworkImage;

    // 게임이 시작되거나 카드가 생성될 때 데이터를 UI에 적용합니다.
    void Start()
    {
        if (cardData != null)
        {
            ApplyCardData();
        }
    }

    // cardData의 정보를 UI 컴포넌트에 채워 넣는 함수입니다.
    public void ApplyCardData()
    {
        nameText.text = cardData.cardName;
        descriptionText.text = cardData.description;
        manaText.text = cardData.manaCost.ToString();
        attackText.text = cardData.attack.ToString();
        healthText.text = cardData.health.ToString();
        artworkImage.sprite = cardData.cardArtwork;
    }
}
