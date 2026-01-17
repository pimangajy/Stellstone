using UnityEngine;
using UnityEngine.EventSystems; // 마우스 클릭 이벤트를 처리하기 위해 필요합니다.

// IPointerClickHandler: "이 오브젝트는 클릭될 수 있어요"라고 유니티에게 약속(인터페이스)하는 것입니다.
public class CardInteraction : MonoBehaviour, IPointerClickHandler
{
    // 카드가 어디에 있는지 구분하기 위한 꼬리표(Enum)입니다.
    // Collection: 덱 짜는 화면의 카드 목록 (보관함)
    // Deck: 현재 짜고 있는 덱 리스트 (오른쪽 리스트)
    public enum CardLocation { Collection, Deck }

    // 현재 이 카드가 어디에 속해 있는지 설정하는 변수
    public CardLocation location;

    // 카드의 정보를 가지고 있는 스크립트를 저장할 변수 (인터페이스 사용)
    private ICardDataHolder cardDataHolder;

    // 게임 시작 전(초기화 단계)에 실행됩니다.
    void Awake()
    {
        // 내 몸(GameObject)에 붙어있는 "카드 정보 가지고 있는 스크립트"를 찾아서 가져옵니다.
        // (DeckCardDisplay나 DeckListItemDisplay가 여기에 해당됩니다)
        cardDataHolder = GetComponent<ICardDataHolder>();
    }

    // 약속했던 "클릭되었을 때" 실행되는 함수입니다.
    public void OnPointerClick(PointerEventData eventData)
    {
        // 마우스 오른쪽 버튼을 클릭했는지 확인합니다.
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // 카드 정보 스크립트가 없으면 중단
            if (cardDataHolder == null) return;

            // 스크립트에게서 실제 'CardData' 정보를 받아옵니다.
            CardData cardData = cardDataHolder.GetCardData();

            // 데이터가 비어있으면 에러 메시지를 띄우고 중단
            if (cardData == null)
            {
                Debug.LogError("카드 데이터가 없습니다.");
                return;
            }

            // 내가 어디에 있는 카드냐에 따라 행동이 달라집니다.
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
    }
}