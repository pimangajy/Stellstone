using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EffectInstance
{
    // 예: ON_PLAY | DAMAGE : 3 : 0 : TARGET
    public string trigger;        // 발동 시점 (예: ON_PLAY, ON_DEATH)
    public string effectName;     // 효과 종류 (예: DAMAGE, HEAL, BUFF)
    public int value1;            // 값1 (피해량, 드로우 수 등)
    public int value2;            // 값2 (체력 버프 등)
    public string target;         // 대상 (예: TARGET_ENEMY, SELF)
    public string condition;      // 조건 (예: TRIBE)
    public string conditionValue; // 조건값 (예: 멤버)
    public int count;             // 반복 횟수

    // [수정됨] '/' 연산자(Else) 처리를 위한 재귀적 필드
    // 앞의 효과 조건이 맞지 않을 때 실행할 대체 효과
    [SerializeReference]
    public EffectInstance elseEffect;
}

public enum ClassType { 강지, 유니, 후야 }
public enum CardType { 하수인, 주문, 멤버, 무기 }
public enum MinionTribe { 없음, 강도단, 아르냥, 바쿠, 멤버 }
public enum Rarity { 일반, 희귀, 영웅, 전설 }
public enum Expansion { 기본 }
public enum TargetRule { None, Target_Enemy, Target_Friend, All }

[CreateAssetMenu(fileName = "New Card", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("1. 식별 정보")]
    public string cardID;       // CSV: CardID
    public string cardName;     // CSV: name

    [Header("2. 게임 로직 (Stats)")]
    public CardType cardType;   // CSV: type (하수인, 주문 등)
    public ClassType member;       // CSV 파일명이나 분류 (강지, 유니 등)
    public Rarity rarity;       // CSV: rarity
    public Expansion expansion; // CSV: expansion

    public int manaCost;        // CSV: cost
    public int attack;          // CSV: attack
    public int health;          // CSV: health
    public MinionTribe minionTribe; // CSV: tribe

    [TextArea(3, 10)]
    public string description;  // CSV: description

    [TextArea(3, 10)]
    public string additionalExplanation; // CSV: additional

    [Header("3. 효과 데이터")]
    // [수정됨] 파싱된 효과 리스트
    public List<EffectInstance> effects = new List<EffectInstance>();

    // 필요 시 사용하는 추가 필드들
    public TargetRule targetRule;

    [Header("4. 리소스 (Art & Sound)")]

    // 움직이는 이미지를 위해 배열로 변경
    [Tooltip("애니메이션 프레임들을 순서대로 넣으세요. 정지 화상은 1개만 넣으세요.")]
    public Sprite[] animationFrames;
    [Tooltip("카드 사용 이팩트")]
    public DissolveEffect cardDissolveEffect;
    [Tooltip("스폰 이팩트")]
    public SpawnEffectData spawnEffectData;
    [Tooltip("직업 아이콘")]
    public Sprite memberIcon;       // 직업 아이콘

    // 썸네일 (배열의 첫 번째 장을 대표 이미지로 사용)
    public Sprite thumbnail => (animationFrames != null && animationFrames.Length > 0) ? animationFrames[0] : null;

    public GameObject spawnEffect;  // 소환 이펙트 프리팹
    public AudioClip attackSound;   // 공격 사운드

    [Header("전투 연출 (원거리 공격용)")]
    [Tooltip("비워두면 몸통 박치기를 합니다. (근접 공격)")]
    public GameObject projectilePrefab; // 날아갈 투사체 프리팹 (파이어볼, 화살 등)

    [Tooltip("투사체가 날아가는 속도")]
    public float projectileSpeed = 15f;

    [Tooltip("투사체의 포물선 높이 (0이면 직선으로 날아감)")]
    public float projectileArcHeight = 1.5f;
}
