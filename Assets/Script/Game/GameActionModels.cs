using System;
using System.Collections.Generic;

// ==================================================================
// 0. 열거형(Enum) 정의 (서버와 완벽하게 숫자를 일치시킴)
// ==================================================================

/// <summary>
///  디버그용 액션
/// </summary>
public enum DebugAction
{
    NONE = 0,            // 기본값(안전장치)
    SpecificCardDraw,    // 특정 카드 드로우
    RequestDeckInfo,     // [신규] 클라이언트 -> 서버: 내 덱 정보 요청
    ResponseDeckInfo     // [신규] 서버 -> 클라이언트: 덱 정보 응답
}

/// <summary>
/// 카드의 출처(획득 경로)를 나타냅니다.
/// </summary>
public enum CardOrigin
{
    Deck = 0,        // 덱에서 정상 드로우/서치된 카드
    SideDeck = 1,    // 사이드덱에서 코스트를 지불하고 가져온 카드
    Graveyard = 2,   // 묘지에서 회수/재사용된 카드
    Created = 3      // 주문/효과/토큰으로 새로 생성된 카드
}

/// <summary>
/// 클라이언트와 서버가 주고받는 모든 메시지(액션)의 종류를 정의합니다.
/// </summary>
public enum GameActionType
{
    NONE = 0,

    // ==========================================
    // 클라이언트 -> 서버 (C -> S) 메시지
    // ==========================================
    MULLIGAN_DECISION,   // 멀리건 결정
    END_TURN,            // 턴 종료
    PLAY_CARD,           // 카드 사용
    SELECT_TARGET_FOR_PLAY,    // 클라이언트가 최종 선택한 타겟 전달 (또는 취소)
    VALID_TARGETS_REQUEST,// 타겟 확인
    VALID_ATTACK_TARGETS_REQUEST, // 공격가능한 대상 요청
    ATTACK,              // 공격 명령
    CONCEDE,             // 항복
    MAKE_CHOICE,         // 클라이언트가 선택 결과를 보냄
    VALID_MEMBER_SKILL_TARGETS_REQUEST, // [멤버] 스킬 조준 가능 대상 요청
    USE_MEMBER_SKILL,                   // [멤버] 스킬 사용 요청
    GET_CARD_FROM_SIDE_DECK,            // [사이드덱] 사이드덱에서 카드 가져오기 요청

    // ==========================================
    // 서버 -> 클라이언트 (S -> C) 메시지
    // ==========================================
    ACTION_RESOLUTION,         // 애니메이션 및 최종 상태 일괄 처리
    MULLIGAN_INFO,             // 멀리건 할 카드 정보
    OPPONENT_MULLIGAN_STATUS,  // 상대방 멀리건 완료 상태
    GAME_READY,                // 게임 시작
    PHASE_START,               // 페이즈 시작 (Standby, Draw, Main, End)
    DRAW_CARD,                 // 카드를 뽑음
    UPDATE_MANA,               // 마나 갱신
    UPDATE_ENTITIES,           // 개체(필드, 체력 등) 상태 갱신
    OPPONENT_PLAY_CARD,        // 상대방이 카드를 냄
    REQUEST_TARGET_FOR_PLAY,   // 서버가 클라이언트에게 "타겟 찍어줘"라고 요청
    PLAY_CARD_SUCCESS,         // 카드 사용 성공
    PLAY_CARD_FAIL,            // 카드 사용 실패
    VALID_TARGETS_RESPONSE,    // 타겟 가능한 객체 전송
    VALID_ATTACK_TARGETS_RESPONSE,  // 공격 가능한 대상 전송
    VALID_MEMBER_SKILL_TARGETS_RESPONSE, // [멤버] 스킬 조준 가능 대상 전송
    USE_MEMBER_SKILL_SUCCESS,           // [멤버] 스킬 사용 성공 브로드캐스트
    USE_MEMBER_SKILL_FAIL,              // [멤버] 스킬 사용 실패 알림
    UPDATE_HAND_CARDS,         // 손패 카드 상태(비용, 스탯 등) 갱신
    REQUEST_CHOICE,            // 서버가 클라이언트에게 선택을 요청함
    GAME_OVER,                 // 게임 종료
    ERROR,                     // 서버 에러
    GET_CARD_FROM_SIDE_DECK_SUCCESS,    // [사이드덱] 카드 가져오기 성공 브로드캐스트
    GET_CARD_FROM_SIDE_DECK_FAIL,       // [사이드덱] 카드 가져오기 실패 알림
    CARD_CREATED,                       // [신규] (S->C) 카드 생성(손패 획득) 알림
    SEND_EMOTE,                         // [신규] (C->S) 감정표현 전송 요청
    RECEIVE_EMOTE,                      // [신규] (S->C) 감정표현 수신(브로드캐스트) 알림
    NEW_LOG_EVENT                       // [신규] (S->C) 새로운 행동 기록(히스토리 타일) 알림
}

/// <summary>
/// 게임 내에서 발생하는 사건(이벤트)의 종류를 정의합니다.
/// </summary>
public enum GameEventType
{
    NONE = 0,
    ATTACK,           // 공격 선언
    DAMAGE,           // 데미지 발생
    HEAL,             // 체력 회복
    BUFF,             // 스탯 버프
    BUFF_HAND,
    BUFF_DECK,
    DEATH,            // 개체 사망
    DESTROY,          // 즉사기(처치) 발동 연출용
    EFFECT_TRIGGER,   // 특수 효과 발동 연출 (전투의 함성, 죽음의 메아리 등)
    SUMMON,           // 하수인 소환
    SUMMON_FROM_DECK,  // 덱에서 특수 소환
    SUMMON_FROM_HAND,  // 손에서 특수 소환
    RESURRECT,          // 묘지에서 부활
    DRAW,              // 카드를 뽑음
    SEARCH_DECK,       // 덱에서 서치
    BIND,             // 속박 (빙결 대체)
    SILENCE,          // 침묵
    FORCE_ATTACK,     // 강제 공격
    GRANT_KEYWORD,    // 키워드 부여
    MANA_MOD,          // 마나 조작
    DISCARD,          // 손패에서 카드를 버림
    RETURN_TO_HAND,   // 필드 하수인을 손패로 되돌림 (바운스)
    SHUFFLE_TO_DECK   // 필드 하수인을 덱으로 섞어 넣음
}

/// <summary>
/// 효과가 발동하는 시점(트리거)의 종류를 정의합니다.
/// </summary>
public enum EffectTriggerType
{
    NONE = 0,
    ON_PLAY = 1,          // 카드를 낼 때 발동 (전투의 함성)
    ON_DEATH = 2,         // 사망 시 발동 (죽음의 메아리)
    ON_SUMMON = 3,        // 소환 시 발동 (소환 감지)
    ON_TURN_START = 4,    // 턴 시작 시
    ON_TURN_END = 5,      // 턴 종료 시
    ON_ATTACK = 6,        // 공격 시작 시
    ON_DAMAGE = 7,        // 데미지를 입었을 때
    ON_HEAL = 8,          // 회복했을 때
    ON_DRAW = 9,          // 드로우 했을 때
    ON_AURA = 10,         // 오라 지속 효과
}

public enum GamePhase
{
    STANDBY = 0,
    DRAW = 1,
    MAIN = 2,
    END = 3
}

public enum TargetRule
{
    None = 0,
    Target_All = 1,                 // 모든 캐릭터(리더+멤버+하수인) 중 1개 선택
    Target_Minion = 2,              // 모든 하수인 중 1개 선택
    Target_Enemy_All = 3,           // 적 캐릭터(리더+멤버+하수인) 중 1개 선택
    Target_Enemy_Minion = 4,        // 적 하수인 중 1개 선택
    Target_Friend_All = 5,          // 아군 캐릭터(리더+멤버+하수인) 중 1개 선택
    Target_Friend_Minion = 6,       // 아군 하수인 중 1개 선택
    Target_Member = 7               // 멤버 중 1개 선택
}

/// <summary>
/// 멤버 카드의 액티브 스킬 정보 DTO
/// </summary>
[Serializable]
public class MemberSkillData
{
    public int skillId;
    public string name;
    public string description;
    public int healthCost;
    public int manaCost;
    public bool targeting;
    public bool canUse;
}

// ==================================================================
// 1. 기본 액션 클래스 (JSON 파싱용)
// ==================================================================

/// <summary>
/// 클라이언트 -> 서버 / 서버 -> 클라이언트 모든 메시지의 기반이 되는 클래스입니다.
/// </summary>
[Serializable]
public class BaseGameAction
{
    // (수정) 기존 string action 에서 enum으로 변경
    public GameActionType action;
}

// ==================================================================
// [신규] 디버그용 액션 클래스
// ==================================================================
[Serializable]
public class BaseDebugAction
{
    public DebugAction debugAction;// 서버의 Enum 이름과 똑같은 문자열이 들어갑니다.
}

/// <summary>
/// [디버그] 특정 카드 드로우 요청 데이터
/// </summary>
public class C_DebugSpecificCardDraw : BaseDebugAction
{
    public string targetCardId;
    public bool isOpponent; // 상대방 덱에서 드로우 여부
}

// [디버그] 덱 정보 요청 (C -> S)
public class C_DebugRequestDeckInfo : BaseDebugAction
{
    public bool isOpponent; // 상대방 덱 정보 요청 여부
}

// [디버그] 덱 정보 응답 (S -> C)
public class S_DebugResponseDeckInfo : BaseDebugAction
{
    public bool isOpponent; // 상대방 덱 정보 여부
    public List<CardInfo> deckCards; // 현재 덱에 남은 카드 리스트
}

// ==================================================================
// 2. 공용 데이터 모델 (게임 상태를 표현)
// ==================================================================

/// <summary>
/// 개체나 카드에 부여된 버프/디버프 및 효과 정보입니다.
/// </summary>
[Serializable]
public class EnchantmentInfo
{
    public int sourceEntityId;
    public string sourceCardId;     // 버프를 부여한 원본 카드 ID (아이콘 썸네일용)
    public string sourceCardName;   // 버프를 부여한 원본 카드 이름
    public string description;      // 버프 효과 상세 설명 (툴팁 텍스트용)
    public GameEventType effectType;
    public int attackMod;
    public int healthMod;
    public int costMod;
    public string grantedKeyword;
    public int duration;
    public int spellAmpMod;
    public int spellWeaknessMod;
    public int buffAmpAttackMod;
    public int buffAmpHealthMod;
}

/// <summary>
/// 카드를 식별하는 기본 데이터입니다.
/// </summary>
[Serializable]
public class CardInfo
{
    public string cardId;
    public string instanceId;
    public string cardName;
    public CardOrigin origin = CardOrigin.Deck; // 카드의 획득 출처 (덱, 사이드덱, 묘지, 생성)
    public int currentCost;
    public int currentAttack;
    public int currentHealth;

    // 손패 누적 스택 수치
    public int customValue;

    // 부여된 효과(버프/너프) 목록
    public List<EnchantmentInfo> enchantments = new List<EnchantmentInfo>();

    // 이 카드가 대상으로 삼을 수 있는 현재 필드의 EntityId 목록
    public List<int> validTargetIds;

    // 손패 한도(10장) 초과 등으로 인해 소각(Graveyard 직행)되었는지 여부
    public bool isBurned;

    // 🚀 [신규 추가] 실시간 증폭(Aura) 및 동적 스탯 정보
    public bool isAmplified;          // 전체 증폭 상태 여부 (true면 클라이언트에서 지속 오라/이펙트 활성화)
    public bool isSpellAmplified;     // 주문 피해 증폭 여부
    public bool isBuffAmplified;      // 주문 버프 수치 증폭 여부

    // 🚀 [신규 추가] 실시간 피해량 정보
    public int dynamicDamage;         // 증폭/버프가 적용된 최종 피해량 (예: 3 -> 4)
    public int baseDamage;            // 증폭 전 기본 피해량 (예: 3)
    public int spellAmpBonus;         // 적용된 순수 주문 증폭치 (+1, +2 등) - 주문 카드 전용
    public int minionDamageBonus;     // 적용된 하수인 효과 피해 증폭치 (+1, +2 등) - 다른 카드/오라에 의한 하수인 피해 버프

    // 🚀 [신규 추가] 실시간 주문 버프 수치 정보
    public int dynamicBuffAttack;     // 증폭이 적용된 최종 공격력 버프량 (예: 1 -> 2)
    public int dynamicBuffHealth;     // 증폭이 적용된 최종 체력 버프량 (예: 1 -> 2)
    public int baseBuffAttack;        // 증폭 전 기본 공격력 버프량
    public int baseBuffHealth;        // 증폭 전 기본 체력 버프량
    public int buffAmpAtkBonus;       // 적용된 공격력 버프 증폭치 (+1 등)
    public int buffAmpHpBonus;        // 적용된 체력 버프 증폭치 (+1 등)
}

/// <summary>
/// 필드, 손, 덱에 있는 모든 '개체'를 나타냅니다.
/// </summary>
[Serializable]
public class EntityData
{
    public int entityId;
    public string cardId;
    public string cardName;
    public string ownerUid;
    public int attack;
    public int health;
    public int maxHealth;
    public bool canAttack;
    public bool hasAttacked;

    // (수정) List<string> 에서 List<CardKeywords> enum으로 변경
    public List<CardKeywords> keywords;

    // 이 개체가 보유한 활성 효과 트리거 목록 (예: ON_TURN_END, ON_DEATH 등)
    public List<EffectTriggerType> activeTriggers;

    // 부여된 효과(버프/너프) 목록
    public List<EnchantmentInfo> enchantments = new List<EnchantmentInfo>();

    public int position;
    public bool isMember;
    public bool isLeader;
    public string skinId;

    // 멤버 카드 액티브 스킬 정보
    public List<MemberSkillData> memberSkills;
    public bool hasUsedSkillThisTurn;
}

/// <summary>
/// 게임 내에서 발생하는 하나의 '사건'을 정의합니다.
/// </summary>
[Serializable]
public class GameEvent
{
    public GameEventType eventType;
    public int sourceEntityId;
    public int targetEntityId;
    public int value;          // 주 수치 (데미지량, 힐량, 공격력 버프 등)
    public int value2;         // 부 수치 (체력 버프 등)
    public string cardId;      // 발동된 카드 원본 에셋 ID
    public string stringValue; // 범용 문자열 (키워드명 등)
    public EffectTriggerType triggerType;
    public EntityData entityData;
}


// ==================================================================
// 3. 클라이언트 -> 서버 (C -> S) 메시지
// ==================================================================

public class C_MulliganDecision : BaseGameAction
{
    public List<string> cardInstanceIdsToReplace;
}

public class C_EndTurn : BaseGameAction
{
}

public class C_PlayCard : BaseGameAction
{
    public string handCardInstanceId;
    public int targetEntityId;
    public int position;
}

// ==========================================
// (C->S) (타겟 선택 완료 또는 취소)
// ==========================================
public class C_SelectTargetForPlay : BaseGameAction
{
    // action = GameActionType.SELECT_TARGET_FOR_PLAY
    public string CardEntityId { get; set; }     // 대상을 지정한 카드의 InstanceId
    public int selectedEntityId { get; set; }     // 선택한 대상의 EntityId (취소했다면 -1 또는 0 전송)
}

/// <summary>
/// (C->S) 타겟팅이 필요한 카드사용시 타겟요청
/// </summary>
public class C_ValidTargetRequest : BaseGameAction
{
    // action = "VALID_TARGETS_RESULT"

    // 어떤 카드에 대한 타겟 결과인지 클라이언트가 매칭할 수 있도록 그대로 돌려줌
    public string CardEntityId { get; set; }
}

/// <summary>
/// (C->S) 클라이언트가 서버의 선택 요구(REQUEST_CHOICE)에 응답할 때 사용합니다.
/// </summary>
public class C_MakeChoice : BaseGameAction
{
    // action = GameActionType.MAKE_CHOICE

    // 1. 토큰 소환 위치 등을 선택했을 경우의 값 (-1이면 선택안함)
    public int selectedPosition { get; set; } = -1;

    // 2. 발견(Discover) 등 특정 카드를 선택했을 경우의 값
    public string selectedCardId { get; set; }

    // 3. 특정 하수인(타겟)을 선택했을 경우의 값 (-1이면 선택안함)
    public int selectedEntityId { get; set; } = -1;
}

public class C_Attack : BaseGameAction
{
    public int attackerEntityId;
    public int defenderEntityId;
}

/// <summary>
/// (C->S) 플레이어가 특정 하수인으로 공격을 시도하려고 드래그할 때, 공격 가능한 타겟 목록을 요청합니다.
/// </summary>
public class C_ValidAttackTargetsRequest : BaseGameAction
{

    // action = GameActionType.VALID_ATTACK_TARGETS_REQUEST;

    public int attackerEntityId { get; set; } // 공격을 시작하려는 내 하수인의 고유 ID
}

/// <summary>
/// (C->S) 필드의 멤버 카드가 특정 스킬을 사용하려 할 때 조준 가능한 타겟 목록을 요청합니다.
/// </summary>
[Serializable]
public class C_ValidMemberSkillTargetsRequest : BaseGameAction
{
    public int entityId;
    public int skillId;

    public C_ValidMemberSkillTargetsRequest()
    {
        action = GameActionType.VALID_MEMBER_SKILL_TARGETS_REQUEST;
    }
}

/// <summary>
/// (C->S) 필드의 멤버 카드 스킬을 최종 사용합니다.
/// </summary>
[Serializable]
public class C_UseMemberSkill : BaseGameAction
{
    public int entityId;
    public int skillId;
    public int targetEntityId;

    public C_UseMemberSkill()
    {
        action = GameActionType.USE_MEMBER_SKILL;
    }
}

/// <summary>
/// (C->S) 플레이어가 항복합니다.
/// </summary>
public class C_Concede : BaseGameAction
{
    // action = GameActionType.CONCEDE
}

// 타겟팅 가능한 객체를 달라고 요청
public class ValidTargetRequest
{
    // 서버가 어떤 요청인지 식별하기 위한 타입
    public string Type { get; set; } = "GetValidTargets";

    // 유저가 드래그를 시작한 카드의 고유 식별자(EntityId)
    public int CardEntityId { get; set; }
}

// ==================================================================
// 4. 서버 -> 클라이언트 (S -> C) 메시지
// ==================================================================

// 매칭시 덱 검증 패킷 
public class MatchDeckErrorResponse
{
    public string action = "MATCH_DECK_ERROR";
    public string status = "error";
    public string errorCode;            // 에러 코드 (아래 참조)
    public string message;              // 한글 상세 안내 메시지
    public int currentCount;            // 현재 메인 덱 장수
    public int requiredCount = 30;      // 필요 메인 덱 장수 (30장)
    public List<string> invalidCardIds; // 문제가 된 카드 ID 리스트 (중복, 직업 불일치 등)
}

public class S_ActionResolution : BaseGameAction
{
    public List<GameEvent> eventLog = new List<GameEvent>();
    public List<EntityData> finalStateUpdates;
}

public class S_MulliganInfo : BaseGameAction
{
    public List<CardInfo> cardsToMulligan;
    public long mulliganEndTime;
    public EntityData myLeader;           //  본인 리더 정보 (EntityData)
    public EntityData enemyLeader;        //  상대 리더 정보 (EntityData)
}

public class S_OpponentMulliganStatus : BaseGameAction
{
    public string opponentUid;
    public List<int> replacedIndices;
    public int replacedCount;
    public bool isReady;
}

public class S_GameReady : BaseGameAction
{
    public string firstPlayerUid;
    public List<CardInfo> finalHand;
    public List<CardInfo> enermyfinalHand;
    public List<CardInfo> mySideDeck; // 🌟 나의 사이드덱 카드 정보 목록
    public string opponentName; // 상대방 플레이어 닉네임
}

public class S_PhaseStart : BaseGameAction
{
    public string TurnPlayerUid;
    // (수정) 기존 string phase에서 enum으로 변경
    public GamePhase phase;
    public CardInfo drawnCard;
    public bool hasDrawn;
    public long turnEndTime;
}

public class S_UpdateMana : BaseGameAction
{
    public string ownerUid;
    public int currentMana;
    public int maxMana;
}

/// <summary>
/// (S->C) 플레이어가 카드를 드로우했음을 알립니다. (페이즈 전환 없이 순수 드로우만 처리)
/// </summary>
public class S_DrawCard : BaseGameAction
{
    // action = GameActionType.DRAW_CARD
    public string playerUid;   // 카드를 뽑은 플레이어의 UID
    public CardInfo drawnCard; // 뽑은 카드 정보 (상대방에게 보낼 때는 Fog of War를 위해 null 처리)
}

public class S_UpdateEntities : BaseGameAction
{
    public List<EntityData> updatedEntities;
}

/// <summary>
/// (S->C) 게임 진행 중(효과 발동 중) 플레이어의 개입이 필요할 때 서버가 전송합니다.
/// </summary>
public class S_RequestChoice : BaseGameAction
{
    // action = GameActionType.REQUEST_CHOICE

    // 어떤 종류의 선택을 요구하는지 명시 (예: "POSITION", "DISCOVER_CARD", "TARGET")
    public string choiceType { get; set; }

    // 선택해야 하는 개수 (기본 1)
    public int count { get; set; } = 1;

    // (선택) 카드 발견 등 제한된 선택지가 있을 때 후보 목록을 보낼 수 있습니다.
    public List<CardInfo> availableOptions { get; set; }

    // ==========================================
    // 유저 화면 UI에 띄워줄 안내 메세지
    // ==========================================
    public string message { get; set; }

    //  이 선택을 요구하게 만든 주체(예: 방금 낸 하수인의 ID) 
    // -> 클라이언트가 이 대상을 밝게 하이라이트 표시할 수 있음
    public int sourceEntityId { get; set; }

    // (선택) 무엇을 소환/사용할 것인지 명시 (예: "token-101")
    public string targetDataId { get; set; }
}

// ==========================================
//  (S->C) (타겟 지정 요청)
// ==========================================
public class S_RequestTargetForPlay : BaseGameAction
{
    // action = GameActionType.REQUEST_TARGET_FOR_PLAY
    public string CardEntityId { get; set; }     // 대상을 요구하는 카드의 InstanceId
    public int position { get; set; }             // 카드가 놓일 필드 위치
    public int targetIndex { get; set; }          // (멀티 타겟 확장용) 현재가 몇 번째 타겟인가 (0, 1, 2...)
    public List<int> ValidTargetIds { get; set; } // TargetValidator가 계산한 현재 턴의 유효한 타겟 목록 [5]
}

/// <summary>
// (S->C) 타겟 가능한 객체들을 알려줍니다.
/// </summary>
public class S_ValidTargetResponse : BaseGameAction
{
    // action = "OPPONENT_PLAY_CARD"

    // 어떤 카드에 대한 타겟 결과인지 클라이언트가 매칭할 수 있도록 그대로 돌려줌
    public string CardEntityId { get; set; }

    // TargetValidator가 계산해낸 타겟 가능한 대상들의 EntityId 목록
    public List<int> ValidTargetIds { get; set; }
}

/// <summary>
/// (S->C) 서버가 계산한 공격 가능한 타겟들의 EntityId 목록을 클라이언트에 회신합니다.
/// </summary>
public class S_ValidAttackTargetsResponse : BaseGameAction
{
    // action = GameActionType.VALID_ATTACK_TARGETS_RESPONSE;

    public int attackerEntityId { get; set; } // 대상을 조회한 공격 하수인의 고유 ID
    public List<int> validDefenderEntityIds { get; set; } = new List<int>(); // 공격 가능한 대상들의 EntityId 목록
}

/// <summary>
/// (S->C) 멤버 스킬로 조준 가능한 대상들의 EntityId 목록을 회신합니다.
/// </summary>
[Serializable]
public class S_ValidMemberSkillTargetsResponse : BaseGameAction
{
    public int entityId;
    public int skillId;
    public List<int> validTargetIds = new List<int>();

    public S_ValidMemberSkillTargetsResponse()
    {
        action = GameActionType.VALID_MEMBER_SKILL_TARGETS_RESPONSE;
    }
}

/// <summary>
/// (S->C) 멤버 스킬이 성공적으로 발동되었음을 브로드캐스트합니다.
/// </summary>
[Serializable]
public class S_UseMemberSkillSuccess : BaseGameAction
{
    public int memberEntityId;
    public int skillId;
    public int targetEntityId;
    public int currentHp;

    public S_UseMemberSkillSuccess()
    {
        action = GameActionType.USE_MEMBER_SKILL_SUCCESS;
    }
}

/// <summary>
/// (S->C) 멤버 스킬 발동이 실패했음을 알립니다.
/// </summary>
[Serializable]
public class S_UseMemberSkillFail : BaseGameAction
{
    public int memberEntityId;
    public int skillId;
    public string reason;

    public S_UseMemberSkillFail()
    {
        action = GameActionType.USE_MEMBER_SKILL_FAIL;
    }
}

public class S_OpponentPlayCard : BaseGameAction
{
    public CardInfo cardPlayed;
    public int handNum;
    public int targetEntityId;
    public int position;     // 하수인이 놓일 필드 슬롯 번호
    public int entityId;     // 서버가 생성하여 부여한 고유 엔티티 ID
}

public class S_PlayCardSuccess : BaseGameAction
{
    public string serverInstanceId;
}

public class S_PlayCardFail : BaseGameAction
{
    public string failedCardInstanceId;
    public string reason;
}

public class S_UpdateHandCards : BaseGameAction
{
    public List<CardInfo> updatedCards;
}

public class S_GameOver : BaseGameAction
{
    // 기본 게임 정보
    public string winnerUid;   // 승자 UID (무승부 시 "DRAW")
    public string reason;      // 종료 사유 (예: "LEADER_KILLED", "항복", "OPPONENT_DISCONNECTED")

    // 🎁 플레이어 맞춤 획득 보상 및 성장 정보
    public int earnedGold;     // 이번 매치로 획득한 골드 (승자: 100, 패자: 20)
    public int earnedExp;      // 이번 매치로 획득한 경험치 (승자: 100, 패자: 30)
    public int currentGold;    // 갱신된 총 보유 골드
    public int currentLevel;   // 현재 레벨
    public int currentExp;     // 현재 경험치
    public int maxExp;         // 다음 레벨업에 필요한 경험치 (currentLevel * 100)
    public bool isLevelUp;     // 이번 게임으로 레벨업했는지 여부 (true/false)
    public int scoreChange;    // 점수 변동 (+30, -15 등)
    public int currentScore;   // 갱신된 점수
}

public class S_Error : BaseGameAction
{
    public string message;
}

// ==================================================================
// 사이드덱 관련 통신 패킷 (C <-> S)
// ==================================================================

/// <summary>
/// [C->S] 사이드덱에서 원하는 카드를 손패로 가져오겠다고 요청합니다.
/// </summary>
public class C_GetCardFromSideDeck : BaseGameAction
{
    // action = "GET_CARD_FROM_SIDE_DECK"
    public string cardInstanceId; // 가져올 카드의 인스턴스 ID (또는 cardId)
}

/// <summary>
/// [S->C] 사이드덱에서 카드를 성공적으로 가져왔음을 브로드캐스트합니다.
/// </summary>
public class S_GetCardFromSideDeckSuccess : BaseGameAction
{
    // action = "GET_CARD_FROM_SIDE_DECK_SUCCESS"
    public string playerUid;              // 카드를 가져온 플레이어 UID
    public CardInfo card;                 // 가져온 카드 정보 (본인에게는 상세 정보, 상대에게는 null)
    public int consumedCost;              // 지불한 코스트(마나)
    public int remainingMana;             // 차감 후 남은 마나
    public int remainingSideDeckCount;     // 남은 사이드덱 장수
}

/// <summary>
/// [S->C] 사이드덱 카드 가져오기 요청이 규칙 위반으로 실패했음을 알립니다.
/// </summary>
public class S_GetCardFromSideDeckFail : BaseGameAction
{
    // action = "GET_CARD_FROM_SIDE_DECK_FAIL"
    public string reason; // 실패 사유 ("마나 부족", "턴 1회 제한 초과", "손패 초과" 등)
}

/// <summary>
/// (S->C) 새 카드가 생성되어 손패에 추가되었음을 알립니다. (토큰 창조, 효과 획득 등)
/// 상대방에게 전송되어 일반 드로우가 아닌 '카드 생성 애니메이션'을 실행할 수 있도록 합니다.
/// </summary>
public class S_CardCreated : BaseGameAction
{
    // action = GameActionType.CARD_CREATED
    public string playerUid;       // 카드를 생성/획득한 플레이어의 UID
    public CardInfo card;          // 생성된 카드 정보 (상대방에게는 null로 마스킹)
    public CardInfo createdCard;   // card와 동일 (클라이언트 접근 편의성)
    public int handCount;          // 카드가 추가된 후 해당 플레이어의 총 손패 장수
}

/// <summary>
/// (C->S) 플레이어가 감정표현(Emote)을 사용할 때 서버로 전송하는 패킷입니다.
/// </summary>
[Serializable]
public class C_SendEmote : BaseGameAction
{
    // action = GameActionType.SEND_EMOTE
    public string emoteId;  // 감정표현 ID (예: "GREETING", "THANKS", "SORRY", "WELL_PLAYED", "OOPS", "THINKING" 등)
    public string message;  // 말풍선에 표시될 텍스트 (예: "안녕하세요!")

    public C_SendEmote()
    {
        action = GameActionType.SEND_EMOTE;
    }

    public C_SendEmote(string emoteId, string message = "")
    {
        action = GameActionType.SEND_EMOTE;
        this.emoteId = emoteId;
        this.message = message;
    }
}

/// <summary>
/// (S->C) 방 안의 플레이어가 감정표현을 사용했을 때 서버가 양쪽(또는 상대방)에 브로드캐스트하는 패킷입니다.
/// </summary>
[Serializable]
public class S_ReceiveEmote : BaseGameAction
{
    // action = GameActionType.RECEIVE_EMOTE
    public string senderUid; // 감정표현을 보낸 플레이어의 UID
    public string emoteId;   // 감정표현 ID
    public string message;   // 말풍선에 표시될 텍스트

    public S_ReceiveEmote()
    {
        action = GameActionType.RECEIVE_EMOTE;
    }
}

// ==================================================================
// 실시간 인게임 행동 로그 및 히스토리 패킷 (S -> C)
// ==================================================================

/// <summary>
/// [S->C] 인게임에서 새로운 행동(소환, 공격, 주문 사용, 사망 등)이 발생했을 때
/// 하스스톤 스타일의 히스토리 타일 및 로그 UI 생성을 위해 실시간으로 전송되는 패킷입니다.
/// </summary>
[Serializable]
public class S_NewLogEvent : BaseGameAction
{
    // action = GameActionType.NEW_LOG_EVENT
    public string actor;          // 행동 주체 (Player UID 또는 "System")
    public string playerUid;      // 행동을 유발한 플레이어 UID (아군/적군 피아식별용)
    public string actionType;     // 행동 종류 ("SUMMON", "ATTACK", "PLAY_CARD", "DEATH", "BUFF" 등)
    public string message;        // 한 줄 요약 텍스트
    public string sourceCardId;   // 행동/버프의 원본 카드 ID (히스토리 썸네일 아이콘용)
    public string sourceCardName; // 행동/버프의 원본 카드 이름
    public int sourceEntityId;     // 주체 Entity ID
    public int targetEntityId;     // 대상 Entity ID (없으면 0)
    public string targetCardName; // 대상 카드/영웅 이름
    public int value;              // 피해량, 회복량, 공격력 등의 주 수치
    public int value2;             // 체력 버프 등의 부 수치
    public long timestamp;         // 발생 시각 (Unix 밀리초)
    public List<LogSubEvent> subEvents; // 이 행동으로 인해 파생된 세부 하위 결과 목록

    public S_NewLogEvent()
    {
        action = GameActionType.NEW_LOG_EVENT;
    }
}

/// <summary>
/// 단일 액션 번들 내부에 포함되는 개별 세부 결과 이벤트 데이터입니다.
/// </summary>
[Serializable]
public class LogSubEvent
{
    public string type;        // "DAMAGE", "HEAL", "DRAW", "SUMMON", "DEATH", "BUFF" 등
    public string targetName;  // 대상 이름 (또는 슬롯 번호)
    public int value;          // 수치 1 (피해량, 회복량, 드로우 장수 등)
    public int value2;         // 수치 2
}