using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// --- 필드 전체에 데미지를 주는 효과 ---

[CreateAssetMenu(fileName = " Field Damage Effect", menuName = "Card Game/Effects/FieldDamage")]
public class FieldDamageEffect : CardEffect
{
    public override void Execute(CardData cardData, FieldCardController target, int value1, int value2)
    {
        // FieldManager.Instance는 현재 씬에 있는 단 하나의 FieldManager를 즉시 찾아옵니다.
        if (FieldManager.Instance != null && cardData != null)
        {
            Debug.Log("범위 공격 발동");
            FieldManager.Instance.DamageAllEnemyMinions(value1, cardData);
        }
    }
}
