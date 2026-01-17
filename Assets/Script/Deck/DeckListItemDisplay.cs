using UnityEngine;
using TMPro;

// 이 스크립트는 오른쪽 '현재 덱 리스트'에 들어가는 얇은 줄(Item) 하나를 담당합니다.
// 텍스트 위주로 카드 이름과 비용, 장수를 보여줍니다.
public class DeckListItemDisplay : MonoBehaviour, ICardDataHolder
{
    // 유니티 에디터에서 연결할 텍스트들
    [SerializeField] private TextMeshProUGUI cardNameText;  // "화염구"
    [SerializeField] private TextMeshProUGUI cardCostText;  // "4"
    [SerializeField] private TextMeshProUGUI cardCountText; // "x2"

    // 내가 보여주고 있는 카드 데이터
    private CardData cardData;

    // 외부에서 데이터를 넣어주는 함수 (초기화)
    public void Setup(CardData data, int count)
    {
        this.cardData = data;

        cardNameText.text = data.cardName;
        cardCostText.text = data.manaCost.ToString();

        // 전설 카드는 보통 덱에 1장만 넣을 수 있어서 별표(*)로 표시하기도 합니다.
        if (data.rarity == Rarity.전설)
        {
            cardCountText.text = "*";
        }
        else
        {
            // 일반 카드는 "x2" 처럼 장수를 표시합니다.
            cardCountText.text = "x" + count;
        }
    }

    // 인터페이스 구현: 내 카드 정보를 반환
    public CardData GetCardData()
    {
        return cardData;
    }
}