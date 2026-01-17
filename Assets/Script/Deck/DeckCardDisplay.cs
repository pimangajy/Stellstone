using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 이 스크립트는 카드 프리팹(UI)에 붙어서, 실제 데이터를 화면에 보여주는 역할을 합니다.
public class DeckCardDisplay : MonoBehaviour, ICardDataHolder
{
    [Header("UI Elements")]
    // 유니티 에디터에서 연결할 텍스트 UI들
    public TextMeshProUGUI nameText;       // 이름
    public TextMeshProUGUI costText;       // 마나 코스트
    public TextMeshProUGUI attackText;     // 공격력
    public TextMeshProUGUI healthText;     // 체력
    public TextMeshProUGUI descriptionText; // 효과 설명
    public TextMeshProUGUI tribeText;      // 종족 값

    // 이미지 UI들
    public Image artworkImage;    // 카드 그림
    public Image rarityGemImage;  // 희귀도 보석 (가운데 작은 보석)

    // 하수인이 아니면 공격력/체력 표시를 숨기기 위해 부모 오브젝트를 연결
    public GameObject attackObject;
    public GameObject healthObject;

    // 현재 이 UI가 보여주고 있는 실제 데이터
    private CardData cardData;

    // 외부에서 데이터를 받아서 화면을 갱신하는 함수
    public void Setup(CardData data)
    {
        this.cardData = data;

        // 텍스트 내용 채우기
        nameText.text = cardData.cardName;
        costText.text = cardData.manaCost.ToString(); // 숫자는 문자열로 변환(.ToString())해야 함
        descriptionText.text = cardData.description;

        // 종족 값이 있으면 보여주고, 없으면 숨깁니다.
        if (cardData.minionTribe != MinionTribe.없음)
        {
            tribeText.gameObject.SetActive(true);
            tribeText.text = cardData.minionTribe.ToString();
        }
        else
        {
            tribeText.gameObject.SetActive(false);
        }

        // 카드 타입이 '하수인'일 때만 공격력/체력을 보여줍니다. (주문은 공격력이 없으니까요)
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

        // 썸네일(그림)이 있으면 설정
        if (cardData.thumbnail != null)
        {
            artworkImage.sprite = cardData.thumbnail;
        }

        // 희귀도(일반/파랑/보라/전설)에 따라 보석 색깔을 바꿉니다.
        SetRarityVisuals(cardData.rarity);
    }

    // 인터페이스 구현: 외부에서 "지금 무슨 카드 정보 가지고 있어?"라고 물으면 대답해줍니다.
    public CardData GetCardData()
    {
        return cardData;
    }

    // 희귀도에 따라 보석 색상을 바꿔주는 내부 함수
    private void SetRarityVisuals(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.일반:
                rarityGemImage.color = Color.white; // 흰색
                break;
            case Rarity.희귀:
                rarityGemImage.color = Color.blue; // 파란색
                break;
            case Rarity.영웅:
                rarityGemImage.color = new Color(0.5f, 0, 1); // 보라색 (RGB 혼합)
                break;
            case Rarity.전설:
                rarityGemImage.color = Color.yellow; // 노란색(황금색)
                break;
            default:
                rarityGemImage.color = Color.gray; // 기본 회색
                break;
        }
    }
}