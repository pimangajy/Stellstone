using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// BattleManager의 서버 승인 유효 타겟 목록 캐싱, 공격/주문 타겟 검증, 하이라이트 제어 전담 partial 클래스입니다.
/// </summary>
public partial class BattleManager
{
    // 서버가 보내준 유효한 타겟 ID들을 임시 저장해 둘 캐시 리스트
    private List<int> _serverValidTargetIds = new List<int>();
    private List<GameCardDisplay> _highlightedTargets = new List<GameCardDisplay>();

    // 서버로부터 타겟 목록이 도착했을 때 호출되는 함수
    private void OnReceiveValidTargets(S_ValidTargetResponse response)
    {
        if (response.ValidTargetIds == null) return;

        _serverValidTargetIds = response.ValidTargetIds;

        foreach (int targetId in _serverValidTargetIds)
        {
            if (GameEntityManager.Instance._spawnedEntities.TryGetValue(targetId, out var targetCard))
            {
                targetCard.SetGlowState(true);
                _highlightedTargets.Add(targetCard);
            }
        }
    }

    // S_RequestTargetForPlay 패킷 전용 전투의 함성으로 타겟이 필요한 경우
    public void OnReceiveValidTargetsRequestTargetForPlay(S_RequestTargetForPlay response)
    {
        ResetHighlights();

        if (response.ValidTargetIds == null) return;

        _serverValidTargetIds = response.ValidTargetIds;

        foreach (int targetId in _serverValidTargetIds)
        {
            if (GameEntityManager.Instance._spawnedEntities.TryGetValue(targetId, out var targetCard))
            {
                targetCard.SetGlowState(true);
                _highlightedTargets.Add(targetCard);
            }
        }
    }

    /// <summary>
    /// 서버로부터 유효한 공격 수비 대상 목록이 수신되면 실행됩니다.
    /// </summary>
    private void OnReceiveValidAttackTargets(S_ValidAttackTargetsResponse response)
    {
        ResetHighlights();

        if (response.validDefenderEntityIds == null) return;

        _serverValidTargetIds = response.validDefenderEntityIds;

        foreach (int targetId in _serverValidTargetIds)
        {
            // 사망 예정인 대상은 하이라이트를 켜지 않음
            if (GameEntityManager.Instance != null && GameEntityManager.Instance.IsPendingDead(targetId))
            {
                continue;
            }

            if (GameEntityManager.Instance != null && GameEntityManager.Instance._spawnedEntities.TryGetValue(targetId, out var targetCard))
            {
                targetCard.SetGlowState(true);
                _highlightedTargets.Add(targetCard);
            }
        }
    }

    /// <summary>                                                                                                                                           
    /// 특정 엔티티 ID가 서버로부터 승인받은 유효한 타겟(공격 대상 또는 카드 효과 대상)인지 확인합니다.
    /// </summary>
    public bool IsServerValidTarget(int entityId)
    {
        if (_serverValidTargetIds == null || _serverValidTargetIds.Count == 0) return false;
        if (GameEntityManager.Instance != null && GameEntityManager.Instance.IsPendingDead(entityId)) return false;
        return _serverValidTargetIds.Contains(entityId);
    }

    /// <summary>
    /// 하이라이트 끄기 및 캐시 초기화 (마나 크리스탈 강조 초기화 포함)
    /// </summary>
    public void ResetHighlights()
    {
        foreach (var target in _highlightedTargets)
        {
            if (target != null) target.SetGlowState(false);
        }
        _highlightedTargets.Clear();
        _serverValidTargetIds.Clear();


        UpdateManaUI();
        /*
        int cardCost =  CardDragManager.instance._currentCard.GetComponent<GameCardDisplay>()._cardData.manaCost;

        if (CardDragManager.instance != null && CardDragManager.instance.IsDragging && cardCost > 0)
        {
            HighlightManaCost(cardCost);
        }
        else
        {
            UpdateManaUI();
        }
        */
    }
}
