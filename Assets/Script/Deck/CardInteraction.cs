using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // UI의 클릭 이벤트를 감지하기 위해 필요합니다.

/// <summary>
/// 카드 UI 오브젝트에 부착되어 사용자의 클릭(특히 우클릭) 상호작용을 처리합니다.
/// 이 스크립트는 자신이 어디에 위치한 카드인지(중앙 목록 or 오른쪽 덱 목록)를 알고 있어야 합니다.
/// </summary>
// [RequireComponent(typeof(DeckCardDisplay))] // 이 스크립트는 항상 DeckCardDisplay와 함께 있어야 합니다.
public class CardInteraction : MonoBehaviour, IPointerClickHandler
{
    // 이 카드가 어디에 위치하는지를 나타내는 Enum(열거형)입니다.
    public enum CardLocation
    {
        Collection, // 중앙 카드 목록 (덱에 추가될 수 있는 카드)
        Deck        // 오른쪽 덱 목록 (덱에서 제거될 수 있는 카드)
    }

    public CardLocation location; // 인스펙터 또는 코드를 통해 이 카드의 위치를 설정해줘야 합니다.

    // '카드 데이터를 제공할 수 있는 기능'을 가진 컴포넌트의 참조만 있으면 됩니다.
    private ICardDataHolder cardDataHolder;

    // 두 가지 종류의 디스플레이 스크립트에 대한 참조를 모두 가집니다.
    private DeckCardDisplay deckCardDisplay;
    private DeckListItemDisplay deckListItemDisplay;

    void Awake()
    {
        // 이 게임오브젝트에 ICardDataHolder 인터페이스를 구현한 컴포넌트가 있는지 찾습니다.
        // DeckCardDisplay든 DeckListItemDisplay든 상관없이 찾아옵니다.
        cardDataHolder = GetComponent<ICardDataHolder>();

        if (cardDataHolder == null)
        {
            Debug.LogError("CardInteraction: 이 오브젝트에 ICardDataHolder를 구현한 컴포넌트가 없습니다!", gameObject);
        }
    }

    /// <summary>
    /// 이 UI 요소가 클릭되었을 때 Unity에 의해 자동으로 호출되는 함수입니다. (IPointerClickHandler 인터페이스)
    /// </summary>
    /// <param name="eventData">클릭에 대한 정보(어떤 버튼이 눌렸는지 등)를 담고 있습니다.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 마우스 오른쪽 버튼으로 클릭했을 때만 아래 로직을 실행합니다.
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (cardDataHolder == null) return;

            CardDataFirebase cardData = cardDataHolder.GetCardData();

            // 카드 데이터를 성공적으로 가져왔는지 확인합니다.
            if (cardData == null)
            {
                Debug.LogError("CardInteraction: 카드 데이터를 가져올 수 없습니다!");
                return;
            }

            // 카드의 위치(location)에 따라 DeckManager의 다른 함수를 호출합니다.
            switch (location)
            {
                case CardLocation.Collection:
                    // 중앙 카드 목록에 있는 카드라면, 덱에 추가하는 함수를 호출합니다.
                    DeckManager.instance.AddCard(cardData);
                    break;
                case CardLocation.Deck:
                    // 오른쪽 덱 목록에 있는 카드라면, 덱에서 제거하는 함수를 호출합니다.
                    DeckManager.instance.RemoveCard(cardData);
                    break;
            }
        }
    }
}

