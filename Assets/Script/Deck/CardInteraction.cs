using UnityEngine;
using UnityEngine.EventSystems;

public class CardInteraction : MonoBehaviour, IPointerClickHandler
{
    public enum CardLocation { Collection, Deck }
    public CardLocation location;

    private ICardDataHolder cardDataHolder;

    void Awake()
    {
        cardDataHolder = GetComponent<ICardDataHolder>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (cardDataHolder == null) return;

            // 이제 CardData (ScriptableObject)를 가져옵니다.
            CardData cardData = cardDataHolder.GetCardData();

            if (cardData == null)
            {
                Debug.LogError("카드 데이터가 없습니다.");
                return;
            }

            switch (location)
            {
                case CardLocation.Collection:
                    DeckManager.instance.AddCard(cardData);
                    break;
                case CardLocation.Deck:
                    DeckManager.instance.RemoveCard(cardData);
                    break;
            }
        }
    }
}