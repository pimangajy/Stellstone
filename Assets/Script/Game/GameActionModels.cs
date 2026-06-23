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

    // C -> S (클라이언트 요청)
    MULLIGAN_DECISION = 1,
    END_TURN = 2,
    PLAY_CARD = 3,
    ATTACK = 4,
    USE_MEMBER_ABILITY = 5,
    CONCEDE = 6,

    // S -> C (서버 응답)
    ACTION_RESOLUTION = 7,
    MULLIGAN_INFO = 8,
    OPPONENT_MULLIGAN_STATUS = 9,
    GAME_READY = 10,
    PHASE_START = 11,
    UPDATE_MANA = 12,
    UPDATE_ENTITIES = 13,
    OPPONENT_PLAY_CARD = 14,
    PLAY_CARD_SUCCESS = 15,
    PLAY_CARD_FAIL = 16,
    UPDATE_HAND_CARDS = 17,
    GAME_OVER = 18,
    ERROR = 19
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
    DRAW              // 카드를 뽑음
}

public enum EffectTriggerType
{
    NONE = 0,
    ON_PLAY = 1,          // 카드를 낼 때 발동 (전투의 함성)
    ON_DEATH = 2          // 사망 시 발동 (죽음의 메아리)
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

// [디버그] 덱 정보 응답 (C -> S)
public class C_DebugRequestDeckInfo : BaseDebugAction
{
    // 필드 불필요 (debugAction 값만으로 충분)
}

// [디버그] 덱 정보 응답 (S -> C)
public class S_DebugResponseDeckInfo : BaseDebugAction
{
    public List<CardInfo>? deckCards; // 현재 덱에 남은 카드 리스트
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
// 3. 디버그용 메시지
// ==================================================================

[Serializable]
public class C_DebugSpecificCardDraw : BaseDebugAction
{
    public string targetCardId;
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