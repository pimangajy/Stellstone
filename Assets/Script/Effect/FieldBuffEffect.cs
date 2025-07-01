using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// --- 버프를 주는 효과 ---

[CreateAssetMenu(fileName = " Field Buff Effect", menuName = "Card Game/Effects/FieldBuff")]
public class FieldBuffEffect : CardEffect
{
    // 이제 이 효과는 value1을 공격력으로, value2를 체력으로 해석하여 사용합니다.
    public override void Execute(CardData cardData, FieldCardController target, int value1, int value2)
    {
        // FieldManager.Instance는 현재 씬에 있는 단 하나의 FieldManager를 즉시 찾아옵니다.
        if (FieldManager.Instance != null && cardData != null)
        {
            Debug.Log("아군 필드 전체에게 +" + value1 + "/+" + value2 + " 버프를 부여합니다.");
            FieldManager.Instance.BuffAllFriendlyMinions(value1, value2);
        }
    }
}
