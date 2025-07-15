using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


// 타겟팅을 요청하는 행동의 종류를 정의합니다.
public enum ActionType
{
    MinionAttack,
    TargetedSpell
}
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
    /// 주어진 카드의 타겟팅 규칙에 맞는 모든 필드 카드를 하이라이트합니다.
    /// </summary>
    public void HighlightValidTargets(CardData sourceCard, ActionType actionType)
    {
        // 1. 먼저 모든 하이라이트를 끕니다.
        ClearAllHighlights();

        // 2. 유효한 타겟 목록을 가져옵니다.
        List<FieldCardController> validTargets = GetValidTargets(sourceCard, actionType);

        // 3. 찾아낸 모든 타겟에 하이라이트를 켭니다. 
        foreach (var target in validTargets)
        {
            target.SetTargetable(true);
        }
    }

    /// <summary>
    /// 유효한 타겟 목록을 계산하는 핵심 로직입니다.
    /// </summary>
    private List<FieldCardController> GetValidTargets(CardData sourceCard, ActionType actionType)
    {
        List<FieldCardController> finalTargets = new List<FieldCardController>();

        // --- 하수인 공격일 경우의 타겟팅 로직 ---
        if (actionType == ActionType.MinionAttack)
        {
            // 1. 모든 적 카드를 가져옵니다.
            List<FieldCardController> allEnemyCards = GetAllCards(true);

            // 2. 먼저 '은신'과 같이 타겟팅이 불가능한 카드들을 '제외'한 리스트를 만듭니다.
            List<FieldCardController> targetableEnemies = allEnemyCards
                .Where(card => !card.cardData.keywords.Contains(Keyword.은신))
                .ToList();

            // 3. 타겟팅 가능한 적들 중에서 '도발'을 가진 카드를 찾습니다.
            List<FieldCardController> tauntCards = targetableEnemies
                .Where(card => card.cardData.keywords.Contains(Keyword.도발))
                .ToList();

            // 4. '도발' 카드가 있다면, 그들이 최종 타겟입니다.
            if (tauntCards.Count > 0)
            {
                finalTargets = tauntCards;
            }
            // 5. '도발' 카드가 없다면, 타겟팅 가능한 모든 적이 최종 타겟입니다.
            else
            {
                finalTargets = targetableEnemies;
            }
        }
        // --- 주문 시전일 경우의 타겟팅 로직 ---
        else if (actionType == ActionType.TargetedSpell)
        {
            // 1. 주문의 TargetRule에 따라 기본 후보 목록을 정합니다.
            List<FieldCardController> potentialTargets = new List<FieldCardController>();
            TargetRule rule = sourceCard.targetRule;
            if (rule == TargetRule.아군_전용) potentialTargets = GetAllCards(false);
            else if (rule == TargetRule.적군_전용) potentialTargets = GetAllCards(true);
            else if (rule == TargetRule.모두_가능) potentialTargets = GetAllCards(null);

            // 2. 그 다음, '은신'이나 '주문 대상 지정 불가' 같은 키워드로 최종 필터링합니다.
            finalTargets = potentialTargets
                .Where(card => !(card.cardData.keywords.Contains(Keyword.은신) && card.enermy))
                // .Where(card => !card.cardData.keywords.Contains(Keyword.주문대상지정불가)) // 예시
                .ToList();
        }

        return finalTargets;
    }

    /// <summary>
    /// 필드의 모든 하이라이트를 끕니다.
    /// </summary>
    public void ClearAllHighlights()
    {
        foreach (var slot in playerSlots)
        {
            FieldCardController card = slot.GetOccupiedCard();
            if (card != null) card.SetTargetable(false);
        }
        foreach (var slot in enemySlots)
        {
            FieldCardController card = slot.GetOccupiedCard();
            if (card != null) card.SetTargetable(false);
        }
    }

    /// <summary>
    /// 필드 위의 모든 카드를 가져오는 헬퍼 함수입니다.
    /// </summary>
    /// <param name="isEnemy">true: 적 카드만, false: 아군 카드만, null: 모든 카드</param>
    private List<FieldCardController> GetAllCards(bool? isEnemy)
    {
        List<FieldCardController> allCards = new List<FieldCardController>();

        if (isEnemy == false || isEnemy == null) // 아군 또는 전체
        {
            foreach (var slot in playerSlots)
            {
                if (!slot.IsAvailable()) allCards.Add(slot.GetOccupiedCard());
            }
        }
        if (isEnemy == true || isEnemy == null) // 적군 또는 전체
        {
            foreach (var slot in enemySlots)
            {
                if (!slot.IsAvailable()) allCards.Add(slot.GetOccupiedCard());
            }
        }
        return allCards;
    }

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
