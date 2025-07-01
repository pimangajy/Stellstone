using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// --- 카드를 뽑는 효과 ---

[CreateAssetMenu(fileName = "New Draw Card Effect", menuName = "Card Game/Effects/Draw Card")]
public class DrawCardEffect : CardEffect
{
    [Tooltip("뽑을 카드 매수")]
    public int cardsToDraw;

    public override void Execute(CardData cardData, FieldCardController target, int value1, int value2)
    {
        cardsToDraw = value1;
        // 카드 뽑기 효과는 타겟이 필요 없습니다.
        Debug.Log("카드 " + cardsToDraw + "장을 뽑습니다.");

        // HandManager를 통해 카드를 뽑도록 요청합니다.
        if (HandManager.Instance != null)
        {
            for (int i = 0; i < cardsToDraw; i++)
            {
                // DrawRandomCard는 예시이며, 실제로는 덱에서 카드를 가져와야 합니다.
                HandManager.Instance.DrawRandomCard();
            }
        }
    }
}
