using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

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


public enum CardClass
{
    Gangzi,
    Yuni,
    Huya
}
public enum CardType
{
    UNKNOWN = 0,
    하수인,
    주문,
    멤버,
    READER
}

public enum CardTribe
{
    무소속,
    강도단,
    아르냥,
    바쿠,
    멤버
}

public enum CardRarity
{
    common,
    rare,
    epic,
    legendary

}
public enum Expansion { 기본 }

public enum CardKeywords
{
    Default = 0,
    Charge = 1,       // 돌진
    Rush = 2,         // 속공
    Taunt = 3,        // 도발
    DivineShield = 4, // 천보
    Poisonous = 5,    // 독성
    Stealth = 6,      // 은신
    Lifesteal = 7,    // 생흡
    Windfury = 8,      // 질풍
    Bind = 9,          // 속박
    Silence = 10,        // 침묵
    Elusive = 11,        // 주문 면역
    Bodyguard = 12       // 리더 수호 (대신 맞기)
}


[CreateAssetMenu(fileName = "New Card", menuName = "Card Game/Card Data")]
public class CardData : ScriptableObject
{
    [Header("1. 식별 정보")]
    public string cardID;       // CSV: CardID
    public string cardName;     // CSV: name
    [Tooltip("체크 시 덱 편성 및 상점 팩 뽑기에서 제외되는 토큰/생성 전용 카드입니다.")]
    public bool isToken = false;// CSV: IsToken
    public bool IsToken => isToken;

    [Tooltip("기본 지급 카드로, 분해가 불가능한 카드입니다.")]
    public bool isDefaultCard = false; // CSV: IsDefaultCard
    public bool IsDefaultCard => isDefaultCard;
    public bool DefaultCard => isDefaultCard;

    [Header("2. 게임 로직 (Stats)")]
    public CardClass cardClass;   // 직업 분류 (강지, 유니, 후야 등)
    public CardType cardType;     // 카드 종류 (하수인, 주문, 멤버, 리더 등)
    public CardRarity rarity;     // CSV: rarity
    public Expansion expansion;   // CSV: expansion

    public int manaCost;          // CSV: cost
    public int attack;            // CSV: attack
    public int health;            // CSV: health
    public CardTribe minionTribe; // CSV: tribe

    [TextArea(3, 10)]
    public string description;    // CSV: description

    [TextArea(3, 10)]
    public string additionalExplanation; // CSV: additional

    [Header("3. 효과 데이터")]
    [FormerlySerializedAs("keyward")]
    public List<CardKeywords> keywords = new List<CardKeywords>();

    // 기존 코드와의 100% 하위 호환을 위한 프로퍼티
    public List<CardKeywords> keyward => keywords;

    // 필요 시 사용하는 추가 필드들
    public bool targeting;

    [Header("멤버 스킬 데이터 (최대 4개)")]
    public List<MemberSkillData> memberSkills = new List<MemberSkillData>();

    [System.NonSerialized]
    private List<string> _cachedMemberSkillDescriptions;

    // 스킬 설명 목록만 빠르게 가져오는 헬퍼 프로퍼티 (최초 1회 캐싱하여 LINQ 람다 및 힙 할당 방지)
    public List<string> memberSkillDescriptions
    {
        get
        {
            if (_cachedMemberSkillDescriptions == null)
            {
                _cachedMemberSkillDescriptions = new List<string>();
                if (memberSkills != null)
                {
                    for (int i = 0; i < memberSkills.Count; i++)
                    {
                        if (memberSkills[i] != null && memberSkills[i].description != null)
                        {
                            _cachedMemberSkillDescriptions.Add(memberSkills[i].description);
                        }
                    }
                }
            }
            return _cachedMemberSkillDescriptions;
        }
    }

    private void OnValidate()
    {
        _cachedMemberSkillDescriptions = null;
    }

    [Header("특수 오라 스탯")]
    public int spellAmp = 0;        // 주문 공격력 / 증폭 (+1 등)
    public int spellWeakness = 0;   // 주문 약화 (피격 주문 피해 감소)
    public bool drawSeal = false;   // 드로우 봉인
    public int buffAmpAttack = 0;   // 아군 공격력 버프 주문 증폭
    public int buffAmpHealth = 0;   // 아군 체력 버프 주문 증폭

    [Header("트리거별 고유 연출 (VFX Data)")]
    public List<CardVFXData> triggerVFXList = new List<CardVFXData>();

    /// <summary>
    /// 트리거에 해당하는 VFX 데이터를 0 할당으로 빠르게 탐색합니다.
    /// </summary>
    public CardVFXData GetTriggerVFX(EffectTriggerType triggerType)
    {
        if (triggerVFXList == null) return null;
        for (int i = 0; i < triggerVFXList.Count; i++)
        {
            if (triggerVFXList[i] != null && triggerVFXList[i].triggerType == triggerType)
            {
                return triggerVFXList[i];
            }
        }
        return null;
    }

    [Header("4. 리소스 (Art & Sound)")]

    // 움직이는 이미지를 위해 배열로 변경
    [Tooltip("애니메이션 프레임들을 순서대로 넣으세요. 정지 화상은 1개만 넣으세요.")]
    public Sprite[] animationFrames;
    [Tooltip("스폰 이팩트")]
    public SpawnEffectData spawnEffectData;
    [Tooltip("직업 아이콘")]
    public Sprite memberIcon;       // 직업 아이콘

    // 썸네일 (배열의 첫 번째 장을 대표 이미지로 사용)
    public Sprite thumbnail => (animationFrames != null && animationFrames.Length > 0) ? animationFrames[0] : null;

    [Header("전투 연출")]
    [Tooltip("기본 공격 투사체 (공격력 1~5)")]
    public GameObject projectilePrefab; // 날아갈 투사체 프리팹 (파이어볼, 화살 등)
    [Tooltip("강화 공격 투사체 (공격력 6 이상, 미설정 시 기본 투사체 사용)")]
    public GameObject heavyProjectilePrefab;
}
