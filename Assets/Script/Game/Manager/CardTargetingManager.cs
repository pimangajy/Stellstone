using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 카드의 타겟팅 조건 판별, 유효성 검사, 서버 전송을 전담하는 매니저입니다.
/// </summary>
public class CardTargetingManager : MonoBehaviour
{
    public static CardTargetingManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 1. 해당 카드가 타겟팅(조준선)이 필요한 카드인지 판별합니다.
    /// </summary>
    public bool RequiresTargeting(CardData cardData)
    {
        if (cardData == null || cardData.effects == null) return false;

        foreach (var effect in cardData.effects)
        {
            // 발동 시점이 ON_PLAY(또는 기본값)이면서 대상 지정(TARGET)이 포함된 경우 [1, 2]
            if ((string.IsNullOrEmpty(effect.trigger) || effect.trigger == "ON_PLAY") &&
                !string.IsNullOrEmpty(effect.target) && effect.target.ToUpper().Contains("TARGET"))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 2. 지정한 타겟이 카드 효과의 대상 규칙(TargetRule)에 맞는지 유효성 검사를 합니다.
    /// </summary>
    public bool IsValidTarget(GameCardDisplay sourceCard, GameCardDisplay target)
    {
        Debug.LogWarning("효과 발동 대상 확인중..");

        if (target == null || sourceCard == null || sourceCard._cardData == null) return false;

        // [핵심 변경] 문자열이 아닌 우리가 만든 Enum 값을 직접 가져옵니다.
        TargetRule rule = sourceCard._cardData.targetRule;
        string myUid = GameClient.Instance != null ? GameClient.Instance.UserUid : "";

        // 대상의 상태 (내 것인지, 하수인인지) 파악
        bool isTargetMine = (target.CurrentEntityData != null && target.CurrentEntityData.ownerUid == myUid);
        bool isTargetMinion = target._cardData != null && target._cardData.cardType == CardType.하수인;

        // Enum 값을 기준으로 분기
        switch (rule)
        {
            case TargetRule.Target_All: // 모든 캐릭터 (제한 없음)
                return true;

            case TargetRule.Target_Minion: // 모든 하수인
                return isTargetMinion;

            case TargetRule.Target_Enemy_All: // 모든 적 캐릭터
                return !isTargetMine;

            case TargetRule.Target_Enemy_Minion: // 적 하수인
                return !isTargetMine && isTargetMinion;

            case TargetRule.Target_Enemy_Leader: // 적 영웅 (명치)
                                                 // 내 것이 아니고, 하수인도 아니어야 영웅입니다.
                return !isTargetMine && !isTargetMinion;

            case TargetRule.Target_Friend_All: // 모든 아군 캐릭터
                return isTargetMine;

            case TargetRule.Target_Friend_Minion: // 아군 하수인
                return isTargetMine && isTargetMinion;

            case TargetRule.Target_Friend_Leader: // 아군 영웅
                return isTargetMine && !isTargetMinion;

            default:
                // 타겟팅이 필요 없는 카드거나 잘못된 룰일 경우
                Debug.LogWarning($"유효하지 않거나 지정할 수 없는 타겟 룰: {rule}");
                return false;
        }
    }

    /// <summary>
    /// 3. 유효성 검사가 끝난 후 대상의 ID를 포함하여 서버로 카드 플레이를 요청합니다.
    /// </summary>
    public void SendPlayTargetCardRequest(GameObject cardObj, int slotIndex, GameCardDisplay targetEntity)
    {
        GameCardDisplay cardDisplay = cardObj.GetComponent<GameCardDisplay>();
        if (cardDisplay != null && GameClient.Instance != null)
        {
            int targetId = targetEntity != null ? targetEntity.EntityId : 0;
            // GameClient의 SendPlayCardRequest 호출 (targetEntityId 포함) [7]
            GameClient.Instance.SendPlayCardRequest(cardDisplay.InstanceId, slotIndex, targetId);
        }
    }

    /// <summary>
    /// 4. 하수인이 일반 전투(공격)를 할 때 유효한 대상인지 검사합니다. (도발, 은신 규칙 적용)
    /// </summary>
    public bool IsValidAttackTarget(GameCardDisplay attacker, GameCardDisplay target)
    {
        if (target == null || attacker == null) return false;
        if (target == attacker) return false; // 자기 자신 공격 불가

        var targetEntity = target.CurrentEntityData;
        string myUid = GameClient.Instance != null ? GameClient.Instance.UserUid : "";

        // 1. 대상이 적군인지 확인 (내 유닛은 공격 불가)
        if (targetEntity == null || targetEntity.ownerUid == myUid) return false;

        // 2. 대상이 은신(STEALTH) 상태인지 확인 -> 은신 상태면 공격 대상으로 지정 불가
        if (targetEntity.keywords != null && targetEntity.keywords.Contains(CardKeywords.Stealth))
        {
            return false;
        }

        // 3. 적 필드에 도발(TAUNT) 하수인이 존재하는지 확인
        bool enemyHasTaunt = false;
        foreach (var entity in GameEntityManager.Instance._spawnedEntities.Values)
        {
            var entityData = entity.CurrentEntityData;

            // 대상이 적군 하수인이고
            if (entityData != null && entityData.ownerUid != myUid && entity._cardData.cardType == CardType.하수인)
            {
                // 도발 키워드가 있으면서 은신 상태가 아니라면
                if (entityData.keywords != null && entityData.keywords.Contains(CardKeywords.Taunt) && !entityData.keywords.Contains(CardKeywords.Stealth))
                {
                    enemyHasTaunt = true;
                    break;
                }
            }
        }

        // 4. 필드에 도발 하수인이 있다면, 현재 조준한 대상도 반드시 도발을 가지고 있어야 함
        if (enemyHasTaunt)
        {
            if (targetEntity.keywords == null || !targetEntity.keywords.Contains(CardKeywords.Taunt))
            {
                return false; // 도발이 아닌 적은 공격 불가
            }
        }

        return true; // 위 조건을 모두 통과하면 유효한 타겟
    }
}