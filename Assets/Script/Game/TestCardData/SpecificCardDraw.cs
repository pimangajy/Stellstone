using TMPro;
using UnityEngine;

public class SpecificCardDraw : MonoBehaviour
{
    public TextMeshProUGUI nameText;

    public CardInfo cardInfo;
    public bool isOpponent; // 상대 덱 카드 여부

    public void DeckInfo(CardInfo Info, bool isOpponentCard = false)
    {
        cardInfo = Info;
        isOpponent = isOpponentCard;
        nameText.text = cardInfo.cardId.ToString();
    }

    public void SpecificCardDrawFun()
    {
        C_DebugSpecificCardDraw action = new C_DebugSpecificCardDraw
        {
            debugAction = DebugAction.SpecificCardDraw,
            targetCardId = cardInfo.cardId.ToString(),
            isOpponent = isOpponent
        };

        Debug.Log($"서버에 {(isOpponent ? "상대" : "내")} 특정카드 {cardInfo.cardId.ToString()} 드로우 요청");
        GameClient.Instance.SendDebugMessageAsync(action);
    }
}
