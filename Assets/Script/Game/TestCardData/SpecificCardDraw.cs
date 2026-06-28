using TMPro;
using UnityEngine;

public class SpecificCardDraw : MonoBehaviour
{
    public TextMeshProUGUI nameText;

    public CardInfo cardInfo;

    public void DeckInfo(CardInfo Info)
    {
        cardInfo = Info;
        nameText.text = cardInfo.cardId.ToString();
    }

    public void SpecificCardDrawFun()
    {
        C_DebugSpecificCardDraw action = new C_DebugSpecificCardDraw
        {
            debugAction = DebugAction.SpecificCardDraw,
            targetCardId = cardInfo.cardId.ToString(),
        };

        Debug.Log($"서버에 특정카드 {cardInfo.cardId.ToString()} 드로우 요청");
        GameClient.Instance.SendDebugMessageAsync(action);
    }
}
