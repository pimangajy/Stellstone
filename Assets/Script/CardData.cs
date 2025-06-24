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
public enum CardTribe
{
    강도단, 
    아르냥,  
}
// [CreateAssetMenu] 속성은 유니티 에디터의 Assets/Create 메뉴에 새로운 항목을 추가해 줍니다.
[CreateAssetMenu(fileName = "New Card", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("카드 기본 정보")]
    public CardType cardType; // ★★★ 추가된 카드 타입 변수 ★★★
    public CardTribe cardTribe;
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
