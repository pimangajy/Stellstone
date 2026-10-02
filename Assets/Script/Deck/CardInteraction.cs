using UnityEngine;
using UnityEngine.EventSystems; // 마우스 클릭 이벤트를 처리하기 위해 필요합니다.

/// <summary>
/// 카드의 마우스 클릭 이벤트를 처리하는 컴포넌트입니다.
/// - 우클릭: 덱에 카드 추가 (컬렉션) 또는 덱에서 제거 (덱 리스트)
/// - 좌클릭: 컬렉션 카드의 상세 정보 및 분해 팝업(CardDetailPopup) 열기
/// </summary>
public class CardInteraction : MonoBehaviour, IPointerClickHandler
{
    // 카드가 어디에 있는지 구분하기 위한 꼬리표(Enum)
    public enum CardLocation { Collection, Deck }

    // 현재 이 카드가 어디에 속해 있는지 설정하는 변수
    public CardLocation location;

    // 카드의 정보를 가지고 있는 스크립트를 저장할 변수 (ICardDataHolder 인터페이스 사용)
    private ICardDataHolder cardDataHolder;

    void Awake()
    {
        // 내 오브젝트에 붙어있는 카드 정보 스크립트(DeckCardDisplay 등)를 가져옵니다.
        cardDataHolder = GetComponent<ICardDataHolder>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (cardDataHolder == null) return;

        CardData cardData = cardDataHolder.GetCardData();
        if (cardData == null)
        {
            Debug.LogError("카드 데이터가 없습니다.");
            return;
        }

        // 1. 마우스 오른쪽 버튼: 덱 추가 / 제거
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            switch (location)
            {
                case CardLocation.Collection:
                    // 보관함에 있는 카드를 우클릭 -> 덱에 추가
                    DeckManager.instance.AddCard(cardData);
                    break;

                case CardLocation.Deck:
                    // 덱 리스트에 있는 카드를 우클릭 -> 덱에서 제거
                    DeckManager.instance.RemoveCard(cardData);
                    break;
            }
        }
        // 2. 마우스 왼쪽 버튼: 상세 정보 및 분해 팝업 열기
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            // 컬렉션(보관함)에 있는 카드를 좌클릭했을 때 상세 팝업 열기
            if (location == CardLocation.Collection)
            {
                if (CardDetailPopup.Instance != null)
                {
                    CardDetailPopup.Instance.Open(cardData);
                }
                else
                {
                    Debug.Log($"[CardInteraction] '{cardData.cardName}' 좌클릭됨 (씬에 CardDetailPopup 매니저가 아직 배치되지 않았습니다)");
                }
            }
        }
    }
}