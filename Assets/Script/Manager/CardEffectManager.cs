using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 효과의 실행을 중앙에서 관리하는 싱글톤 매니저입니다.
/// </summary>
public class CardEffectManager : MonoBehaviour
{
    #region Singleton
    public static CardEffectManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    #endregion


    /// <summary>
    /// 지정된 카드의 모든 효과를 순서대로 실행합니다.
    /// </summary>
    /// <param name="cardToPlay">사용할 카드의 데이터</param>
    /// <param name="caster">효과 시전자 (카드를 낸 아군 하수인, 없을 수도 있음)</param>
    /// <param name="target">효과 대상 (선택된 적군 하수인, 없을 수도 있음)</param>

    public void ExecuteEffects(CardData cardToPlay, FieldCardController target)
    {
        if (cardToPlay == null || cardToPlay.effects == null) return;

        Debug.Log(cardToPlay.cardName + "의 효과를 발동합니다.");

        // ★★★ 핵심 수정: EffectInstance 리스트를 순회합니다. ★★★
        foreach (var effectInstance in cardToPlay.effects)
        {
            if (effectInstance != null && effectInstance.effect != null)
            {
                // 효과를 실행할 때, 해당 인스턴스가 가진 값들을 함께 넘겨줍니다.
                effectInstance.effect.Execute(
                    cardToPlay,
                    target,
                    effectInstance.value1,
                    effectInstance.value2
                );
            }
        }
    }
}
