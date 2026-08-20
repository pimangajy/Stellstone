using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst.Intrinsics;
using DG.Tweening.Core.Easing;

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
        if(cardData == null && cardData.targeting == false) return false;

        return true;
    }

    /// <summary>
    /// 2. 지정한 타겟이 카드 효과의 대상 규칙에 맞는지 검사합니다. (서버 응답 기준)
    /// </summary>
    public bool IsValidTarget(GameCardDisplay sourceCard, GameCardDisplay target)
    {
        if (target == null || sourceCard == null) return false;

        // ★ 수정: 서버가 준 타겟 목록에 있는지 확인
        return BattleManager.Instance != null && BattleManager.Instance.IsServerValidTarget(target.EntityId);
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
    /// 4. 하수인이 일반 전투(공격)를 할 때 유효한 대상인지 검사합니다. (서버 응답 기준)
    /// </summary>
    public bool IsValidAttackTarget(GameCardDisplay attacker, GameCardDisplay target)
    {
        if (target == null || attacker == null) return false;

        // ★ 수정: 서버가 준 타겟 목록에 있는지 확인
        return BattleManager.Instance != null && BattleManager.Instance.IsServerValidTarget(target.EntityId);
    }
}