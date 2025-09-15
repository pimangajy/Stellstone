using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro를 사용하기 위해 필요
using System;

public class DeckCardDisplay : MonoBehaviour
{
    // [카드 UI 프리팹의 요소들]
    // 인스펙터 창에서 각 변수에 프리팹 안의 UI 요소들을 드래그 앤 드롭으로 연결해줘야 합니다.
    [Header("Card Data Fields")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI tribeText;

    [Header("Card Visuals")]
    public Image artworkImage;
    public Image rarityGemImage; // 희귀도 보석 이미지

    [Header("Stat Objects")]
    public GameObject attackObject;
    public GameObject healthObject;

    private CardDataFirebase cardData; // 이 UI가 표시할 카드 데이터 원본

    /// <summary>
    /// CardData를 받아와서 UI를 채우는 메인 함수
    /// </summary>
    /// <param name="data">표시할 카드 데이터</param>
    public void Setup(CardDataFirebase data)
    {
        this.cardData = data;

        // --- 기본 정보 설정 ---
        nameText.text = cardData.name;
        costText.text = cardData.cost.ToString();
        descriptionText.text = cardData.description;

        // 종족 텍스트는 값이 있을 때만 표시
        if (!string.IsNullOrEmpty(cardData.tribe))
        {
            tribeText.gameObject.SetActive(true);
            tribeText.text = cardData.tribe;
        }
        else
        {
            tribeText.gameObject.SetActive(false);
        }

        // --- 공격력과 체력 설정 (NullReferenceException 해결) ---

        // 1. 공격력 처리
        // cardData.attack이 null이 아닌지 먼저 확인합니다.
        if (cardData.attack != null)
        {
            // 값이 있을 때만 UI를 활성화하고 텍스트를 설정합니다.
            attackObject.SetActive(true);
            attackText.text = Convert.ToInt64(cardData.attack).ToString();
        }
        else
        {
            // 값이 null이면 (주문 카드) UI를 비활성화합니다.
            attackObject.SetActive(false);
        }

        // 2. 체력 처리
        // cardData.health가 null이 아닌지 먼저 확인합니다.
        if (cardData.health != null)
        {
            healthObject.SetActive(true);
            healthText.text = Convert.ToInt64(cardData.health).ToString();
        }
        else
        {
            healthObject.SetActive(false);
        }

        // 기타 시각적 요소 설정
        SetRarityVisuals(cardData.rarity);
        LoadArtwork();
    }

    private void SetRarityVisuals(string rarity)
    {
        switch (rarity.ToLower())
        {
            case "common":
                rarityGemImage.color = Color.white; // 일반: 흰색
                break;
            case "rare":
                rarityGemImage.color = Color.blue; // 희귀: 파란색
                break;
            case "epic":
                rarityGemImage.color = new Color(0.5f, 0, 1); // 영웅: 보라색
                break;
            case "legendary":
                rarityGemImage.color = Color.yellow; // 전설: 노란색
                break;
            default:
                rarityGemImage.color = Color.gray;
                break;
        }
    }
    private void LoadArtwork()
    {
        if (artworkImage == null || string.IsNullOrEmpty(cardData.imageUrl)) return;

        // 확장자를 포함한 다양한 경우를 처리하기 위해 정규화
        string imagePath = cardData.imageUrl.Replace("Assets/Resources/", "").Replace(".png", "").Replace(".jpg", "");
        Sprite artworkSprite = Resources.Load<Sprite>(imagePath);

        if (artworkSprite != null)
        {
            artworkImage.sprite = artworkSprite;
        }
        else
        {
            Debug.LogWarning($"이미지를 찾을 수 없습니다: {imagePath}");
            // 필요하다면 여기에 기본 이미지를 설정하는 코드를 추가할 수 있습니다.
            // artworkImage.sprite = defaultSprite;
        }
    }
}
