using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AYellowpaper.SerializedCollections;

/// <summary>
/// 덱 편성(편집) 화면 및 미리보기에서 카드의 외형과 정보를 화면에 표시하는 컴포넌트입니다.
/// </summary>
public class DeckCardDisplay : MonoBehaviour, ICardDataHolder
{
    [Header("UI Elements")]
    public TextMeshProUGUI nameText;        // 이름
    public TextMeshProUGUI costText;        // 마나 코스트
    public TextMeshProUGUI attackText;      // 공격력
    public TextMeshProUGUI healthText;      // 체력
    public TextMeshProUGUI descriptionText; // 효과 설명
    public TextMeshProUGUI tribeText;       // 종족 명
    public TextMeshProUGUI countText;       // 카드 수량 (예: X 2)

    [Header("사용 불가 / 흑백 대체 반투명 패널")]
    [Tooltip("수량이 0이거나 미보유 카드일 때 활성화할 반투명 오버레이 패널")]
    public GameObject disabledOverlayPanel;

    [Header("이미지 UI")]
    public Image artworkImage;              // 카드 일러스트
    public Image rarityGemImage;           // 레어도 보석 (가운데 작은 보석 등)

    [Header("레어도별 카드 프레임/테두리 (Border)")]
    [Tooltip("카드 테두리/프레임 Image 컴포넌트")]
    public Image frameImage;

    [Tooltip("레어도별 프레임 스프라이트 매핑 (Common, Rare, Epic, Legendary)")]
    [SerializedDictionary("레어도", "프레임 스프라이트")]
    public SerializedDictionary<CardRarity, Sprite> rarityFrames = new SerializedDictionary<CardRarity, Sprite>();

    [Header("하수인 전용 UI 부모 오브젝트")]
    public GameObject attackObject;
    public GameObject healthObject;

    private CardData cardData;

    /// <summary>
    /// 카드 데이터를 받아 UI, 일러스트, 레어도 프레임을 갱신합니다.
    /// count가 -1이면 기본 규칙(전설: 1장, 그 외: 2장)이 적용됩니다.
    /// </summary>
    public void Setup(CardData data, int count = -1)
    {
        this.cardData = data;
        if (cardData == null) return;

        // 1. 텍스트 정보 채우기
        if (nameText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(nameText, minSize: 10f, maxSize: 22f, wordWrap: false, overflowMode: TextOverflowModes.Ellipsis);
            nameText.text = cardData.cardName;
        }
        if (costText != null) costText.text = cardData.manaCost.ToString();
        if (descriptionText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(descriptionText, minSize: 9f, maxSize: 20f, wordWrap: true, overflowMode: TextOverflowModes.Ellipsis);
            CardTextFormatter.FormatAndBind(descriptionText, cardData.description, cardData);
        }

        // 수량 텍스트 설정
        if (count < 0)
        {
            count = 2;
        }
        SetCount(count);

        // 2. 종족 정보 설정
        if (tribeText != null)
        {
            if (cardData.minionTribe != CardTribe.무소속)
            {
                tribeText.gameObject.SetActive(true);
                tribeText.text = cardData.minionTribe.ToString();
            }
            else
            {
                tribeText.gameObject.SetActive(false);
            }
        }

        // 3. 카드 타입이 '하수인'일 때만 공격력/체력 활성화
        if (cardData.cardType == CardType.하수인)
        {
            if (attackObject != null) attackObject.SetActive(true);
            if (healthObject != null) healthObject.SetActive(true);
            if (attackText != null) attackText.text = cardData.attack.ToString();
            if (healthText != null) healthText.text = cardData.health.ToString();
        }
        else
        {
            if (attackObject != null) attackObject.SetActive(false);
            if (healthObject != null) healthObject.SetActive(false);
        }

        // 4. 썸네일(일러스트) 이미지 적용
        if (artworkImage != null && cardData.thumbnail != null)
        {
            artworkImage.sprite = cardData.thumbnail;
        }

        // 5. 레어도별 프레임(테두리) 적용
        SetRarityFrame(cardData.rarity);

        // 6. 레어도별 보석 비주얼 설정 (기존 호환성 유지)
        SetRarityVisuals(cardData.rarity);
    }

    public CardData GetCardData()
    {
        return cardData;
    }

    /// <summary>
    /// 카드의 레어도에 맞춰 테두리/프레임 스프라이트를 교체합니다.
    /// </summary>
    public void SetRarityFrame(CardRarity rarity)
    {
        if (frameImage == null) return;

        if (rarityFrames != null && rarityFrames.TryGetValue(rarity, out Sprite frameSprite) && frameSprite != null)
        {
            frameImage.sprite = frameSprite;
            frameImage.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 레어도에 따라 보석의 색상을 변경합니다.
    /// </summary>
    private void SetRarityVisuals(CardRarity rarity)
    {
        if (rarityGemImage == null) return;

        switch (rarity)
        {
            case CardRarity.common:
                rarityGemImage.color = Color.white; // 일반
                break;
            case CardRarity.rare:
                rarityGemImage.color = Color.blue;  // 희귀 (파랑)
                break;
            case CardRarity.epic:
                rarityGemImage.color = new Color(0.5f, 0f, 1f); // 특급 (보라)
                break;
            case CardRarity.legendary:
                rarityGemImage.color = Color.yellow; // 전설 (황금)
                break;
            default:
                rarityGemImage.color = Color.gray;
                break;
        }
    }

    /// <summary>
    /// 카드 수량 텍스트('X 2')를 갱신합니다.
    /// 수량이 0 이하면 반투명 비활성화 패널을 켭니다.
    /// </summary>
    public void SetCount(int count)
    {
        SetRemainingState(count, count);
    }

    /// <summary>
    /// 실제 보유량(ownedCount)과 잔여 수량(remaining)을 받아 텍스트 및 반투명 패널을 갱신합니다.
    /// </summary>
    /// <param name="remaining">현재 덱에 추가 가능한 남은 장수</param>
    /// <param name="ownedCount">유저가 실제로 계정에 소유한 총 장수</param>
    public void SetRemainingState(int remaining, int ownedCount)
    {
        if (countText != null)
        {
            countText.text = $"X {Mathf.Max(0, remaining)}";
            countText.gameObject.SetActive(true);
        }

        // 유저한테 아예 없는 카드(ownedCount <= 0)이거나, 덱에 모두 넣어 더 넣을 수 없는 카드(remaining <= 0)면 패널 활성화
        bool isUnavailable = (ownedCount <= 0) || (remaining <= 0);
        if (disabledOverlayPanel != null)
        {
            disabledOverlayPanel.SetActive(isUnavailable);
        }
    }

    /// <summary>
    /// 수량 표시 및 비활성화 패널을 숨깁니다 (미리보기 UI 등).
    /// </summary>
    public void HideCount()
    {
        if (countText != null)
        {
            countText.gameObject.SetActive(false);
        }
        if (disabledOverlayPanel != null)
        {
            disabledOverlayPanel.SetActive(false);
        }
    }
}
