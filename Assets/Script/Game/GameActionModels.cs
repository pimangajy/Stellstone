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
    ATTACK,              // 공격 명령
    USE_MEMBER_ABILITY,  // 멤버 특수 능력 사용
    CONCEDE,             // 항복
    MAKE_CHOICE,         // 클라이언트가 선택 결과를 보냄

    // ==========================================
    // 서버 -> 클라이언트 (S -> C) 메시지
    // ==========================================
    ACTION_RESOLUTION,         // 애니메이션 및 최종 상태 일괄 처리
    MULLIGAN_INFO,             // 멀리건 할 카드 정보
    OPPONENT_MULLIGAN_STATUS,  // 상대방 멀리건 완료 상태
    GAME_READY,                // 게임 시작
    PHASE_START,               // 페이즈 시작 (Standby, Draw, Main, End)
    UPDATE_MANA,               // 마나 갱신
    UPDATE_ENTITIES,           // 개체(필드, 체력 등) 상태 갱신
    OPPONENT_PLAY_CARD,        // 상대방이 카드를 냄
    PLAY_CARD_SUCCESS,         // 카드 사용 성공
    PLAY_CARD_FAIL,            // 카드 사용 실패
    UPDATE_HAND_CARDS,         // 손패 카드 상태(비용, 스탯 등) 갱신
    REQUEST_CHOICE,            // 서버가 클라이언트에게 선택을 요청함
    GAME_OVER,                 // 게임 종료
    ERROR                      // 서버 에러
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
    DEATH,            // 개체 사망
    EFFECT_TRIGGER,   // 특수 효과 발동 연출 (전투의 함성, 죽음의 메아리 등)
    SUMMON,           // 하수인 소환
    DRAW,              // 카드를 뽑음
    BIND,             // 속박 (빙결 대체)
    SILENCE,          // 침묵
    FORCE_ATTACK,     // 강제 공격
    GRANT_KEYWORD,    // 키워드 부여
    MANA_MOD          // 마나 조작
}

/// <summary>
/// 효과가 발동하는 시점(트리거)의 종류를 정의합니다.
/// </summary>
public enum EffectTriggerType
{
    NONE = 0,
    ON_PLAY,          // 카드를 낼 때 발동 (전투의 함성)
    ON_DEATH,          // 사망 시 발동 (죽음의 메아리)
    ON_TURN_START,     // 턴 시작 시
    ON_TURN_END,       // 턴 종료 시
    ON_ATTACK,        // 공격 시작 시
    ON_DAMAGE,        // 데미지를 입었을떄
    ON_HEAL,          // 회복했을떄
    ON_DRAW,          // 드로우 했을때
    ON_SUMMON,        // 소환할때
}

public enum GamePhase
{
    STANDBY = 0,
    DRAW = 1,
    MAIN = 2,
    END = 3
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
    public DebugAction debugAction; // 서버의 Enum 이름과 똑같은 문자열이 들어갑니다.
}

/// <summary>
/// [디버그] 특정 카드 드로우 요청 데이터
/// </summary>
public class C_DebugSpecificCardDraw : BaseDebugAction
{
    public string targetCardId;
}

// [디버그] 덱 정보 응답 (C -> S)
public class C_DebugRequestDeckInfo : BaseDebugAction
{
    // 필드 불필요 (debugAction 값만으로 충분)
}

// [디버그] 덱 정보 응답 (S -> C)
public class S_DebugResponseDeckInfo : BaseDebugAction
{
    public List<CardInfo> deckCards; // 현재 덱에 남은 카드 리스트
}

// ==================================================================
// 2. 공용 데이터 모델 (게임 상태를 표현)
// ==================================================================

/// <summary>
/// 카드를 식별하는 기본 데이터입니다.
/// </summary>
[Serializable]
public class CardInfo
{
    public string cardId;
    public string instanceId;
    public string cardName;
    public int currentCost;
    public int currentAttack;
    public int currentHealth;
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

    public int position;
    public bool isMember;
    public bool isLeader;
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
    public int value;
    public string stringValue;
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

/// <summary>
/// (C->S) 클라이언트가 서버의 선택 요구(REQUEST_CHOICE)에 응답할 때 사용합니다.
/// </summary>
public class C_MakeChoice : BaseGameAction
{
    // action = GameActionType.MAKE_CHOICE

    // 1. 토큰 소환 위치 등을 선택했을 경우의 값 (-1이면 선택안함)
    public int selectedPosition { get; set; } = -1;

    // 2. 발견(Discover) 등 특정 카드를 선택했을 경우의 값
    public string? selectedCardId { get; set; }

    // 3. 특정 하수인(타겟)을 선택했을 경우의 값 (-1이면 선택안함)
    public int selectedEntityId { get; set; } = -1;
}

public class C_Attack : BaseGameAction
{
    public int attackerEntityId;
    public int defenderEntityId;
}

public class C_UseMemberAbility : BaseGameAction
{
    public int memberEntityId;
    public string abilityId;
    public int targetEntityId;
}

public class C_Concede : BaseGameAction
{
}

// ==================================================================
// 4. 서버 -> 클라이언트 (S -> C) 메시지
// ==================================================================

public class S_ActionResolution : BaseGameAction
{
    public List<GameEvent> eventLog = new List<GameEvent>();
    public List<EntityData> finalStateUpdates;
}

public class S_MulliganInfo : BaseGameAction
{
    public List<CardInfo> cardsToMulligan;
    public long mulliganEndTime;
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
    public EntityData myLeader;
    public EntityData enemyLeader;
}

public class S_PhaseStart : BaseGameAction
{
    public string TurnPlayerUid;
    // (수정) 기존 string phase에서 enum으로 변경
    public GamePhase phase;
    public CardInfo drawnCard;
    public long turnEndTime;
}

public class S_UpdateMana : BaseGameAction
{
    public string ownerUid;
    public int currentMana;
    public int maxMana;
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
    public string? choiceType { get; set; }

    // 선택해야 하는 개수 (기본 1)
    public int count { get; set; } = 1;

    // (선택) 카드 발견 등 제한된 선택지가 있을 때 후보 목록을 보낼 수 있습니다.
    public List<CardInfo>? availableOptions { get; set; }

    // ==========================================
    // 유저 화면 UI에 띄워줄 안내 메세지
    // ==========================================
    public string? message { get; set; }

    //  이 선택을 요구하게 만든 주체(예: 방금 낸 하수인의 ID) 
    // -> 클라이언트가 이 대상을 밝게 하이라이트 표시할 수 있음
    public int sourceEntityId { get; set; }

    // (선택) 무엇을 소환/사용할 것인지 명시 (예: "token-101")
    public string? targetDataId { get; set; }
}

public class S_OpponentPlayCard : BaseGameAction
{
    public CardInfo cardPlayed;
    public int handNum;
    public int targetEntityId;
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
    public string winnerUid;
    public string reason;
}

public class S_Error : BaseGameAction
{
    public string message;
}