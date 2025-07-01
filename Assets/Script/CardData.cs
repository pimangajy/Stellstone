using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EffectInstance
{
    public CardEffect effect; // 어떤 효과인지 (예: DamageEffect, BuffEffect)
    public int value1;       // 첫 번째 값 (예: 피해량, 공격력 버프)
    public int value2;       // 두 번째 값 (예: 체력 버프)
    // 나중에 필요하면 string, float 등 다른 타입의 값도 추가할 수 있습니다.
}

// 이 enum은 CardData 클래스 바깥이나 안, 어디에 선언해도 좋습니다.
public enum CardType
{
    하수인, // Minion
    주문,  // Spell
    장비,  // Equipment
}
// --- 하수인 전용 종족 ---
public enum MinionTribe
{
    없음, // 종족이 없는 일반 하수인
    강도단,
    아르냥
}
// --- 주문 전용 타입 ---
public enum SpellType
{
    없음, // 타입이 없는 일반 주문
    단일_대상,
    범위_광역
}
public enum EquipmentType
{
    없음, // 타입이 없는 일반 주문
    무기,
    방어구,
}

// 타겟팅 규칙
public enum TargetRule
{
    선택_불가, // 타겟을 지정할 수 없는 효과 (예: 광역 주문)
    아군_전용, // 아군 하수인만 타겟으로 지정 가능
    적군_전용, // 적군 하수인만 타겟으로 지정 가능
    모두_가능  // 아군, 적군 모두 타겟으로 지정 가능
}

// 게임에 등장할 모든 키워드를 정의하는 enum 입니다.
public enum Keyword
{
    // 하수인 공용
    도발,      // Taunt: 이 하수인을 먼저 공격해야 합니다.
    속공,      // Rush: 필드에 나온 턴에 다른 하수인을 공격할 수 있습니다.
    돌진,      // Charge: 필드에 나온 턴에 모든 것을 공격할 수 있습니다.
    흡혈, // Lifesteal: 이 하수인이 피해를 줄 때 영웅의 체력을 회복합니다.
    치명타,      // Poisonous: 이 하수인이 피해를 준 하수인은 파괴됩니다.
    은신,      // Stealth: 주문이나 효과의 대상으로 지정되지 않으며, 공격하기 전까지 무적.

    // 주문 전용
    주문증폭,  // Spell Damage: 주문의 공격력을 증가시킵니다.
    쌍둥이주문 // Twinspell: 이 주문을 사용하면, 비용은 같지만 이 키워드는 없는 복사본을 손으로 가져옵니다.
}

// [CreateAssetMenu] 속성은 유니티 에디터의 Assets/Create 메뉴에 새로운 항목을 추가해 줍니다.
[CreateAssetMenu(fileName = "New Card", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("카드 기본 정보")]
    public CardType cardType; // ★★★ 추가된 카드 타입 변수 ★★★
    public MinionTribe minionTribe;
    public SpellType spellType;
    public string cardName;
    [TextArea(3, 10)] // 인스펙터에서 여러 줄로 텍스트를 입력할 수 있게 해줍니다.
    public string description;
    public Sprite cardArtwork; // 카드 일러스트

    [Header("카드 스탯")]
    public int manaCost;
    public int attack;
    public int health;

    [Header("카드 효과")]
    // ★★★ 핵심 수정: 이제 EffectInstance의 리스트를 사용합니다. ★★★
    public List<EffectInstance> effects;

    [Header("키워드 능력")]
    [Tooltip("이 카드가 가진 모든 키워드 능력의 목록입니다.")]
    public List<Keyword> keywords;

    [Header("타겟팅 규칙")]
    [Tooltip("단일 대상 주문/효과의 타겟팅 규칙입니다.")]
    public TargetRule targetRule;


    // 나중에 여기에 카드 타입, 진영, 키워드 능력 등 더 많은 정보를 추가할 수 있습니다.
    // public enum CardType { Minion, Spell, Weapon }
    // public CardType cardType;
}
