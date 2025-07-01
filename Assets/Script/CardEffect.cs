using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class CardEffect : ScriptableObject
{
    /// <summary>
    /// 이 효과를 실행합니다.
    /// </summary>
    /// <param name="caster">효과를 시전하는 주체</param>
    /// <param name="target">효과의 대상</param>
    /// <param name="value">효과에 사용될 값 (피해량, 드로우 매수 등)</param>
    public abstract void Execute(CardData cardData, FieldCardController target, int value1, int value2);
}
