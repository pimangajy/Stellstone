using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FieldManager : MonoBehaviour
{
    // --- 싱글톤 패턴 설정 ---
    public static FieldManager Instance { get; private set; }

    void Awake()
    {
        // 씬에 FieldManager가 이미 존재하면 새로 생긴 것을 파괴, 아니면 자신을 Instance로 설정
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    // -------------------------

    [Header("슬롯 관리")]
    [Tooltip("아군 필드 슬롯들을 순서대로 연결해주세요.")]
    public FieldSlot[] playerSlots;

    [Tooltip("적군 필드 슬롯들을 순서대로 연결해주세요.")]
    public FieldSlot[] enemySlots;


    /// <summary>
    /// 두 하수인 간의 전투를 지휘합니다.
    /// </summary>
    public void RequestCombat(FieldCardController attacker, FieldCardController target)
    {
        if (attacker == null || target == null) return;

        Debug.Log(attacker.cardData.cardName + "이(가) " + target.cardData.cardName + "과의 전투를 시작합니다.");

        // 전투로 인한 피해는 동시 다발적으로 일어나는 것으로 간주합니다.
        int attackerDamage = attacker.CurrentAttack;
        int targetDamage = target.CurrentAttack;

        // 1. 피격자가 공격자로부터 피해를 입습니다.
        target.TakeDamage(attackerDamage, attacker.cardData);

        // 2. 공격자가 피격자로부터 피해를 입습니다.
        attacker.TakeDamage(targetDamage, target.cardData);
    }

    /// <summary>
    /// 필드에 있는 모든 '아군' 하수인에게 스탯 버프를 부여합니다.
    /// </summary>
    public void BuffAllFriendlyMinions(int attack, int health)
    {
        Debug.Log("모든 아군 하수인에게 +" + attack + "/+" + health + " 버프를 부여합니다.");

        foreach (FieldSlot slot in playerSlots)
        {
            // 슬롯이 비어있지 않다면 (카드가 있다면)
            if (!slot.IsAvailable())
            {
                // 슬롯에 있는 카드의 컨트롤러를 가져옵니다.
                FieldCardController card = slot.GetOccupiedCard();
                if (card != null)
                {
                    // 해당 카드의 버프 함수를 호출합니다.
                    card.ApplyFieldBuff(attack, health);
                }
            }
        }
    }

    /// <summary>
    /// 필드에 있는 모든 '적군' 하수인에게 피해를 줍니다.
    /// </summary>
    public void DamageAllEnemyMinions(int damage, CardData cardData)
    {
        Debug.Log("모든 적군 하수인에게 " + damage + "의 피해를 줍니다.");

        foreach (FieldSlot slot in enemySlots)
        {
            if (!slot.IsAvailable())
            {
                Debug.Log("슬롯 있음");
                FieldCardController card = slot.GetOccupiedCard();
                if (card != null)
                {
                    Debug.Log("데미지 가함");
                    card.TakeDamage(damage, cardData);
                }
            }
        }
    }

    // 여기에 "무작위 적 하수인 하나에게...", "가장 공격력이 높은 아군 하수인에게..." 등
    // 다양한 필드 대상 효과 함수들을 계속해서 추가할 수 있습니다.
}
