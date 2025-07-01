using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// --- 버프를 주는 효과 ---

[CreateAssetMenu(fileName = "New Buff Effect", menuName = "Card Game/Effects/Buff")]
public class BuffEffect : CardEffect
{
    // 이제 이 효과는 value1을 공격력으로, value2를 체력으로 해석하여 사용합니다.
    public override void Execute(CardData cardData, FieldCardController target, int value1, int value2)
    {
        if (target != null)
        {
            Debug.Log(target.cardData.cardName + "에게 +" + value1 + "/+" + value2 + " 버프를 부여합니다.");
            target.ApplyFieldBuff(value1, value2);
        }
    }
}
