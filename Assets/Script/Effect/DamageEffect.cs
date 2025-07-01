using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// --- 피해를 주는 효과 ---

[CreateAssetMenu(fileName = "New Damage Effect", menuName = "Card Game/Effects/Damage")]
public class DamageEffect : CardEffect
{
    [Tooltip("피해량")]
    public int damageAmount;

    public override void Execute(CardData cardData, FieldCardController target, int value1, int value2)
    {
        // 타겟이 유효한지 확인합니다.
        if (target != null)
        {
            damageAmount = value1;
            Debug.Log(target.cardData.cardName + "에게 " + damageAmount + "의 피해를 줍니다.");
            target.TakeDamage(damageAmount, cardData);
        }
        else
        {
            Debug.LogWarning("피해를 줄 대상이 지정되지 않았습니다.");
        }
    }
}

