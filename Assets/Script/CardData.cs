using System.Collections;
using System.Collections.Generic;
using UnityEngine;


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

    // 나중에 여기에 카드 타입, 진영, 키워드 능력 등 더 많은 정보를 추가할 수 있습니다.
    // public enum CardType { Minion, Spell, Weapon }
    // public CardType cardType;
}
