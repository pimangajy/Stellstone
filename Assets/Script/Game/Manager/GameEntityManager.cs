using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using AYellowpaper.SerializedCollections;
using DG.Tweening;

/// <summary>
/// 필드 위에 나와있는 하수인이나 영웅(Entity)들을 관리하는 '현장 감독'입니다.
/// [수정됨] 몸통 박치기를 완전히 제거하고, 모든 전투를 투사체(Projectile) 기반으로 변경했습니다.
/// 공격자가 먼저 발사하고, 약간의 딜레이 후 수비자가 반격합니다.
/// </summary>
public class GameEntityManager : MonoBehaviour
{
    public static GameEntityManager Instance { get; private set; }

    [Header("테스트 설정")]
    public bool test;

    [Header("필드 슬롯 (배열로 직접 할당)")]
    [Tooltip("내 하수인들이 놓일 슬롯들 (0~6)")]
    public FieldSlot[] myFieldSlots;
    [Tooltip("상대 하수인들이 놓일 슬롯들 (0~6)")]
    public FieldSlot[] opponentFieldSlots;

    [Header("광역 이펙트 중심점 (선택 사항, 미할당 시 중앙 슬롯 자동 사용)")]
    [Tooltip("내 필드 광역 이펙트가 소환될 중심 Transform (2번 슬롯 또는 별도 중앙 오브젝트)")]
    public Transform myFieldCenter;
    [Tooltip("상대 필드 광역 이펙트가 소환될 중심 Transform (2번 슬롯 또는 별도 중앙 오브젝트)")]
    public Transform opponentFieldCenter;

    [Header("멤버 존 슬롯 (배열로 직접 할당)")]
    public FieldSlot[] myMemberSlots;
    public FieldSlot[] opponentMemberSlots;

    [Header("리더 보드")]
    public LeaderCardDisplay myLeader;
    public LeaderCardDisplay opponentLeader;

    [Header("프리팹")]
    public GameObject minionPrefab; // 하수인 모형

    /// <summary>
    /// 현재 클라이언트의 사용자 UID를 다양한 출처(GameClient, BattleManager, myLeader, PlayerPrefs)에서 안전하게 동적으로 가져옵니다.
    /// </summary>
    public string MyUid
    {
        get
        {
            if (GameClient.Instance != null && !string.IsNullOrEmpty(GameClient.Instance.UserUid))
                return GameClient.Instance.UserUid;
            if (BattleManager.Instance != null && !string.IsNullOrEmpty(BattleManager.Instance.myUid))
                return BattleManager.Instance.myUid;
            if (myLeader != null && myLeader.CurrentEntityData != null && !string.IsNullOrEmpty(myLeader.CurrentEntityData.ownerUid))
                return myLeader.CurrentEntityData.ownerUid;
            if (PlayerPrefs.HasKey("CurrentUserId"))
                return PlayerPrefs.GetString("CurrentUserId");
            return "";
        }
    }

    // 소환된 녀석들을 관리하는 명부 (ID로 찾음)
    public Dictionary<int, GameCardDisplay> _spawnedEntities = new Dictionary<int, GameCardDisplay>();

    // [액션 큐 및 사망 예정 하수인 관리]
    private readonly Queue<S_ActionResolution> _actionQueue = new Queue<S_ActionResolution>();
    private bool _isProcessingQueue = false;
    private readonly HashSet<int> _pendingDeadEntityIds = new HashSet<int>();
    private readonly HashSet<string> _playedAreaVFXKeys = new HashSet<string>();

    public bool IsPendingDead(int entityId)
    {
        return _pendingDeadEntityIds.Contains(entityId);
    }

    /// <summary>
    /// 현재 액션 큐가 실행 중이거나 대기 중인 액션 패킷이 남아있는지 여부
    /// </summary>
    public bool IsProcessingQueue => _isProcessingQueue || (_actionQueue != null && _actionQueue.Count > 0);

    /// <summary>
    /// 리더가 가장 최근에 피격당한 데미지 이벤트 로그
    /// </summary>
    public GameEvent LastLeaderDamageEvent { get; private set; }

    /// <summary>
    /// 리더를 타격한 마지막 공격 유형 키 (예: "Heavy", "Normal" 등)
    /// </summary>
    public string LastKillingAttackTypeKey { get; private set; }

    /// <summary>
    /// 리더를 타격한 마지막 투사체/스킬(CardVFXData)에 등록된 고유 패배 모션 데이터 (있을 경우)
    /// </summary>
    public LeaderDefeatData LastKillingDefeatData { get; private set; }

    [Header("기본/폴백 투사체 프리팹")]
    public GameObject defaultProjectilePrefab;

    /// <summary>
    /// EntityId를 통해 하수인 또는 리더(myLeader, opponentLeader)의 GameCardDisplay를 안전하게 조회합니다.
    /// </summary>
    public GameCardDisplay GetEntityDisplay(int entityId)
    {
        if (entityId <= 0) return null;
        if (_spawnedEntities.TryGetValue(entityId, out var display) && display != null)
        {
            return display;
        }
        if (opponentLeader != null && (entityId == opponentLeader.EntityId || (opponentLeader.CurrentEntityData != null && entityId == opponentLeader.CurrentEntityData.entityId)))
        {
            return opponentLeader;
        }
        if (myLeader != null && (entityId == myLeader.EntityId || (myLeader.CurrentEntityData != null && entityId == myLeader.CurrentEntityData.entityId)))
        {
            return myLeader;
        }
        return null;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (defaultProjectilePrefab == null)
        {
            defaultProjectilePrefab = Resources.Load<GameObject>("Prefab/TestRoom/Projectile/fireBall/Small/FireBalll")
                ?? Resources.Load<GameObject>("Prefab/TestRoom/Projectile/BlueSlash/Small/Slash_001");
#if UNITY_EDITOR
            if (defaultProjectilePrefab == null)
            {
                defaultProjectilePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/TestRoom/Projectile/fireBall/Small/FireBalll.prefab")
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/TestRoom/Projectile/BlueSlash/Small/Slash_001.prefab");
            }
#endif
        }
    }

    private void Start()
    {
        SubscribeEvents();
    }

    private void OnEnable()
    {
        test = false;
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnUpdateEntitiesEvent -= OnReceiveUpdateEntities;
            GameClient.Instance.OnUpdateEntitiesEvent += OnReceiveUpdateEntities;

            GameClient.Instance.OnUseMemberSkillSuccessEvent -= OnUseMemberSkillSuccess;
            GameClient.Instance.OnUseMemberSkillSuccessEvent += OnUseMemberSkillSuccess;

            GameClient.Instance.OnUseMemberSkillFailEvent -= OnUseMemberSkillFail;
            GameClient.Instance.OnUseMemberSkillFailEvent += OnUseMemberSkillFail;

            GameClient.Instance.OnPhaseStartEvent -= OnReceivePhaseStart;
            GameClient.Instance.OnPhaseStartEvent += OnReceivePhaseStart;
        }
    }

    private void UnsubscribeEvents()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnUpdateEntitiesEvent -= OnReceiveUpdateEntities;
            GameClient.Instance.OnUseMemberSkillSuccessEvent -= OnUseMemberSkillSuccess;
            GameClient.Instance.OnUseMemberSkillFailEvent -= OnUseMemberSkillFail;
            GameClient.Instance.OnPhaseStartEvent -= OnReceivePhaseStart;
        }
    }

    private void OnReceiveUpdateEntities(S_UpdateEntities packet)
    {
        if (packet != null && packet.updatedEntities != null)
        {
            HandleEntitiesUpdated(packet.updatedEntities);
        }
    }

    private void OnUseMemberSkillSuccess(S_UseMemberSkillSuccess packet)
    {
        if (packet == null) return;
        if (_spawnedEntities.TryGetValue(packet.memberEntityId, out var display) && display != null)
        {
            if (display.CurrentEntityData != null)
            {
                display.CurrentEntityData.hasUsedSkillThisTurn = true;
                display.CurrentEntityData.health = packet.currentHp;
            }
            display.UpdateEntityStats(display.CurrentEntityData);
        }
    }

    private void OnUseMemberSkillFail(S_UseMemberSkillFail packet)
    {
        if (packet == null) return;
        if (_spawnedEntities.TryGetValue(packet.memberEntityId, out var display) && display != null)
        {
            if (display.CurrentEntityData != null)
            {
                display.CurrentEntityData.hasUsedSkillThisTurn = false;
            }
        }
    }

    private void OnReceivePhaseStart(S_PhaseStart packet)
    {
        if (packet == null) return;
        if (packet.phase == GamePhase.STANDBY)
        {
            ResetMemberSkillUsageForNewTurn();
        }
    }

    public void ResetMemberSkillUsageForNewTurn()
    {
        foreach (var kvp in _spawnedEntities)
        {
            if (kvp.Value != null && kvp.Value.CurrentEntityData != null)
            {
                kvp.Value.CurrentEntityData.hasUsedSkillThisTurn = false;
            }
        }
    }

    public void SetReader(S_MulliganInfo info)
    {
        if (info == null) return;
        SetReader(info.myLeader, info.enemyLeader);
    }

    public void SetReader(EntityData myLeaderData, EntityData enemyLeaderData)
    {
        string currentUid = MyUid;
        if (!string.IsNullOrEmpty(currentUid) && myLeaderData != null && enemyLeaderData != null)
        {
            // 서버에서 혹시라도 호스트/게스트 고정 순서로 전송하여 myLeader와 enemyLeader가 뒤바뀐 경우 자동 스왑 보정
            if (myLeaderData.ownerUid != currentUid && enemyLeaderData.ownerUid == currentUid)
            {
                Debug.LogWarning($"[GameEntityManager] ⚠️ myLeader와 enemyLeader의 소유자가 반대로 감지되어 자동 스왑합니다. (MyUid: {currentUid}, myLeader: {myLeaderData.ownerUid}, enemyLeader: {enemyLeaderData.ownerUid})");
                var temp = myLeaderData;
                myLeaderData = enemyLeaderData;
                enemyLeaderData = temp;
            }
        }

        Debug.Log($"[GameEntityManager] 👑 SetReader: myLeader.ownerUid='{myLeaderData?.ownerUid}', opponentLeader.ownerUid='{enemyLeaderData?.ownerUid}', MyUid='{currentUid}'");

        if (myLeader != null && myLeaderData != null)
        {
            myLeader.SetLeader(myLeaderData);
            _spawnedEntities[myLeaderData.entityId] = myLeader;
        }

        if (opponentLeader != null && enemyLeaderData != null)
        {
            opponentLeader.SetLeader(enemyLeaderData);
            _spawnedEntities[enemyLeaderData.entityId] = opponentLeader;
        }
    }


    // ==================================================================
    // 1. 이벤트 처리 및 판단 (패킷 즉시 실행 - 패킷 내부는 순차 진행)
    // ==================================================================

    /// <summary>
    /// 서버로부터 수신된 액션 시퀀스 패킷을 큐에 등록하고 순차적으로 실행합니다.
    /// (이전 패킷 연출이 진행 중인 경우 완료된 후 차례대로 실행되어 역전 및 부활 버그를 방지합니다)
    /// </summary>
    public void ResolveActionSequence(S_ActionResolution info)
    {
        if (info == null) return;

        // 1. [사망 예정 타겟 사전 등록] 서버에서 이미 파괴 확정된 하수인은 즉시 다음 타겟팅에서 배제
        RegisterPendingDeadEntities(info);

        // 2. 패킷을 액션 큐에 추가
        _actionQueue.Enqueue(info);

        // 3. 큐 처리기가 실행 중이 아니라면 순차 처리 루틴 시작
        if (!_isProcessingQueue)
        {
            StartCoroutine(ProcessActionQueueRoutine());
        }
    }

    /// <summary>
    /// 서버 패킷을 사전 스캔하여 체력이 0 이하가 되거나 사망 이벤트가 있는 하수인을 즉시 사망 예정 목록에 등록합니다.
    /// </summary>
    private void RegisterPendingDeadEntities(S_ActionResolution info)
    {
        if (info == null) return;

        // finalStateUpdates 검사 (체력이 0 이하인 일반 하수인)
        if (info.finalStateUpdates != null)
        {
            foreach (var update in info.finalStateUpdates)
            {
                if (update != null && update.health <= 0 && !update.isLeader)
                {
                    MarkEntityAsPendingDead(update.entityId);
                }
            }
        }

        // eventLog 검사 (DEATH 이벤트)
        if (info.eventLog != null)
        {
            foreach (var log in info.eventLog)
            {
                if (log != null && log.eventType == GameEventType.DEATH)
                {
                    int deadId = log.targetEntityId != 0 ? log.targetEntityId : log.sourceEntityId;
                    if (deadId > 0)
                    {
                        MarkEntityAsPendingDead(deadId);
                    }
                }
            }
        }
    }

    private void MarkEntityAsPendingDead(int entityId)
    {
        if (!_pendingDeadEntityIds.Add(entityId)) return;

        if (_spawnedEntities.TryGetValue(entityId, out var cardDisplay))
        {
            cardDisplay.SetPendingDeadVisual();
        }
    }

    /// <summary>
    /// 액션 큐에 쌓인 패킷들을 순차적으로 하나씩 처리하는 코루틴
    /// </summary>
    private IEnumerator ProcessActionQueueRoutine()
    {
        _isProcessingQueue = true;

        while (_actionQueue.Count > 0)
        {
            var nextAction = _actionQueue.Dequeue();
            yield return StartCoroutine(MergedActionSequenceRoutine(nextAction.eventLog, nextAction.finalStateUpdates));
        }

        _isProcessingQueue = false;
    }

    /// <summary>
    /// 수신된 이벤트 로그들을 순차적으로 실행하고 최종 필드 상태를 갱신합니다.
    /// </summary>
    private IEnumerator MergedActionSequenceRoutine(List<GameEvent> eventLog, List<EntityData> finalStateUpdates)
    {
        // 이번 액션 시퀀스에서 공격을 수행한 공격자 엔티티 추적
        HashSet<int> attackersInThisSequence = new HashSet<int>();
        _playedAreaVFXKeys.Clear();

        // 1. 서버가 보내준 eventLog를 순차적으로 실행
        if (eventLog != null)
        {
            for (int i = 0; i < eventLog.Count; i++)
            {
                var log = eventLog[i];

            switch (log.eventType)
            {
                case GameEventType.NONE:
                    // 처리할 내용 없음
                    break;

                case GameEventType.ATTACK:
                    if (log.sourceEntityId > 0)
                    {
                        attackersInThisSequence.Add(log.sourceEntityId);
                    }
                    yield return StartCoroutine(HandleAttack(log));
                    break;

                case GameEventType.DAMAGE:
                    // 이전 이벤트가 ATTACK이었는지 확인 (일반 전투 교전은 HandleAttack에서 이미 투사체를 쏨)
                    bool isDirectCombatDamage = (i > 0 && eventLog[i - 1].eventType == GameEventType.ATTACK && 
                                                 eventLog[i - 1].sourceEntityId == log.sourceEntityId && 
                                                 eventLog[i - 1].targetEntityId == log.targetEntityId);
                    if (isDirectCombatDamage)
                    {
                        yield return StartCoroutine(HandleDamage(log, isDirectCombatDamage: true));
                    }
                    else
                    {
                        // 효과/스킬 데미지인 경우: 뒤이어 동일한 sourceEntityId, triggerType, cardId를 가진 연속된 DAMAGE 이벤트들을 묶어서 일괄 처리
                        List<GameEvent> damageBatch = new List<GameEvent> { log };
                        while (i + 1 < eventLog.Count &&
                               eventLog[i + 1].eventType == GameEventType.DAMAGE &&
                               eventLog[i + 1].sourceEntityId == log.sourceEntityId &&
                               eventLog[i + 1].triggerType == log.triggerType &&
                               eventLog[i + 1].cardId == log.cardId)
                        {
                            i++;
                            damageBatch.Add(eventLog[i]);
                        }

                        yield return StartCoroutine(HandleBatchDamage(damageBatch));
                    }
                    break;

                case GameEventType.HEAL:
                    yield return StartCoroutine(HandleHeal(log));
                    break;

                case GameEventType.BUFF:
                    yield return StartCoroutine(HandleBuff(log));
                    break;

                case GameEventType.BUFF_HAND:
                    yield return StartCoroutine(HandleBuffHand(log));
                    break;

                case GameEventType.BUFF_DECK:
                    yield return StartCoroutine(HandleBuffDeck(log));
                    break;

                case GameEventType.DEATH:
                    yield return StartCoroutine(HandleDeath(log));
                    break;

                case GameEventType.DESTROY:
                    // 연속된 DESTROY 이벤트들을 묶어서 일괄 처리
                    List<GameEvent> destroyBatch = new List<GameEvent> { log };
                    while (i + 1 < eventLog.Count &&
                           eventLog[i + 1].eventType == GameEventType.DESTROY &&
                           eventLog[i + 1].sourceEntityId == log.sourceEntityId &&
                           eventLog[i + 1].triggerType == log.triggerType &&
                           eventLog[i + 1].cardId == log.cardId)
                    {
                        i++;
                        destroyBatch.Add(eventLog[i]);
                    }
                    yield return StartCoroutine(HandleBatchDestroy(destroyBatch));
                    break;

                case GameEventType.EFFECT_TRIGGER:
                    yield return StartCoroutine(HandleEffectTrigger(log));
                    break;

                case GameEventType.SUMMON:
                case GameEventType.SUMMON_FROM_DECK:
                case GameEventType.SUMMON_FROM_HAND:
                case GameEventType.RESURRECT:
                    yield return StartCoroutine(HandleSummon(log));
                    break;

                case GameEventType.DRAW:
                    yield return StartCoroutine(HandleDraw(log));
                    break;

                case GameEventType.SEARCH_DECK:
                    yield return StartCoroutine(HandleSearchDeck(log));
                    break;

                case GameEventType.BIND:
                    yield return StartCoroutine(HandleBind(log));
                    break;

                case GameEventType.SILENCE:
                    yield return StartCoroutine(HandleSilence(log));
                    break;

                case GameEventType.FORCE_ATTACK:
                    yield return StartCoroutine(HandleForceAttack(log));
                    break;

                case GameEventType.GRANT_KEYWORD:
                    yield return StartCoroutine(HandleGrantKeyword(log));
                    break;

                case GameEventType.MANA_MOD:
                    yield return StartCoroutine(HandleManaMod(log));
                    break;

                case GameEventType.DISCARD:
                    yield return StartCoroutine(HandleDiscard(log));
                    break;

                case GameEventType.RETURN_TO_HAND:
                    yield return StartCoroutine(HandleReturnToHand(log));
                    break;

                case GameEventType.SHUFFLE_TO_DECK:
                    yield return StartCoroutine(HandleShuffleToDeck(log));
                    break;

                default:
                    Debug.LogWarning($"[MergedActionSequenceRoutine] 정의되지 않은 이벤트 타입입니다: {log.eventType}");
                    break;
            }
        }
    }

        // 2. 모든 이벤트 연출이 완벽히 종료된 후 마지막으로 필드 상태 업데이트
        if (finalStateUpdates != null && finalStateUpdates.Count > 0)
        {
            yield return YieldInstructionCache.WaitForSeconds(0.3f); // 모든 연출 종료 후 짧은 대기 뒤 스탯 최신화
            HandleEntitiesUpdated(finalStateUpdates); // 죽은 유닛 파괴 및 스탯 최신화
        }

        // 3. 공격 연출이 완전히 완료된 공격자 하수인들을 원래 자리(슬롯 바닥)로 착지 복귀
        foreach (int attackerId in attackersInThisSequence)
        {
            if (_spawnedEntities.TryGetValue(attackerId, out var attackerCard))
            {
                attackerCard.SetFloatingState(false);
            }
        }
    }

    // ==================================================================
    // 각 이벤트 타입별 대응 함수 
    // ==================================================================

    /// <summary>
    /// 광역 또는 직접 생성 이펙트(AreaFromCaster, AreaField, ImpactOnTarget)를 소환합니다.
    /// 만약 등록된 프리팹에 ProjectileController가 붙어 있는 경우, 그 안의 hitEffectPrefab(실제 폭발 파티클)을 자동으로 추출하여 소환합니다.
    /// </summary>
    private GameObject SpawnDirectVFX(GameObject prefab, Vector3 position, float destroyTime)
    {
        if (prefab == null) return null;

        GameObject actualPrefab = prefab;
        Vector3 spawnScale = prefab.transform.localScale;
        Vector3 offset = Vector3.zero;
        Quaternion rotation = prefab.transform.rotation;

        // ProjectileController가 붙어 있는지 검사 (투사체 프리팹을 광역에 잘못 지정한 경우 자동 보정)
        var projCtrl = prefab.GetComponent<ProjectileController>();
        if (projCtrl != null && projCtrl.hitEffectPrefab != null)
        {
            actualPrefab = projCtrl.hitEffectPrefab;
            offset = projCtrl.hitEffectOffset;
            rotation = Quaternion.Euler(projCtrl.hitEffectRotation);
            spawnScale = Vector3.Scale(actualPrefab.transform.localScale, projCtrl.hitEffectScale);
        }

        Vector3 finalPos = position + offset;
        GameObject vfx = Instantiate(actualPrefab, finalPos, rotation);
        vfx.transform.localScale = spawnScale;

        // 자식 ParticleSystem들의 scalingMode를 Hierarchy로 동기화하여 전체 비례 확대
        if (projCtrl != null)
        {
            float uniformScale = (projCtrl.hitEffectScale.x + projCtrl.hitEffectScale.y + projCtrl.hitEffectScale.z) / 3f;
            var allParticles = vfx.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allParticles)
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            var trails = vfx.GetComponentsInChildren<TrailRenderer>(true);
            foreach (var trail in trails)
            {
                trail.widthMultiplier *= uniformScale;
            }
            var lights = vfx.GetComponentsInChildren<Light>(true);
            foreach (var lt in lights)
            {
                lt.range *= uniformScale;
            }
        }

        if (destroyTime > 0f)
        {
            Destroy(vfx, destroyTime);
        }

        return vfx;
    }

    /// <summary>
    /// 발사 주체 엔티티가 없을 때(주문 카드 등), 시전자 리더(본인 또는 상대 리더)를 판별하여 반환합니다.
    /// </summary>
    public GameCardDisplay GetCasterLeader(int sourceEntityId = 0, EntityData entityDataFallback = null)
    {
        // 1. entityDataFallback에 소유자 UID가 명시되어 있는 경우
        if (entityDataFallback != null && !string.IsNullOrEmpty(entityDataFallback.ownerUid))
        {
            return (entityDataFallback.ownerUid == MyUid) ? myLeader : opponentLeader;
        }

        // 2. sourceEntityId가 리더의 EntityId와 일치하는 경우
        if (sourceEntityId > 0)
        {
            int myLeaderId = (myLeader != null && myLeader.CurrentEntityData != null) ? myLeader.CurrentEntityData.entityId : (myLeader != null ? myLeader.EntityId : 0);
            int oppLeaderId = (opponentLeader != null && opponentLeader.CurrentEntityData != null) ? opponentLeader.CurrentEntityData.entityId : (opponentLeader != null ? opponentLeader.EntityId : 0);

            if (myLeaderId > 0 && sourceEntityId == myLeaderId) return myLeader;
            if (oppLeaderId > 0 && sourceEntityId == oppLeaderId) return opponentLeader;
        }

        // 3. 현재 턴 소유자로 판별 (주문 카드는 현재 턴인 플레이어가 사용함)
        if (BattleManager.Instance != null)
        {
            return BattleManager.Instance.IsMyTurn ? myLeader : opponentLeader;
        }

        // 4. 폴백: 기본적으로 내 리더
        return myLeader;
    }

    /// <summary>
    /// 공격 및 모든 효과(전함, 죽메, 턴종료, 버프, 힐 등)의 투사체 발사 및 연출을 일괄 처리하는 통합 코루틴
    /// </summary>
    public IEnumerator PlayActionProjectileRoutine(
        int sourceEntityId,
        int targetEntityId,
        EffectTriggerType triggerType,
        string cardId = null,
        EntityData entityDataFallback = null,
        int explicitDamage = 0)
    {
        // 1. 발사 주체(하수인/리더) 찾기
        GameCardDisplay sourceEntity = GetEntityDisplay(sourceEntityId);

        // 2. 타겟 존재 여부 확인 (리더 폴백 포함)
        GameCardDisplay targetEntity = GetEntityDisplay(targetEntityId);
        if (targetEntity == null)
        {
            // 대상 ID 매칭 실패 시 피아 식별 기반으로 리더 타겟 폴백
            if (sourceEntity != null && sourceEntity.CurrentEntityData != null && sourceEntity.CurrentEntityData.ownerUid == MyUid)
            {
                targetEntity = opponentLeader;
            }
            else if (sourceEntity != null && sourceEntity.CurrentEntityData != null && sourceEntity.CurrentEntityData.ownerUid != MyUid)
            {
                targetEntity = myLeader;
            }
        }
        if (targetEntity == null) yield break;

        // 3. 카드 데이터 결정 (1순위: 발사자 하수인 데이터, 2순위: 전달받은 cardId, 3순위: entityData.cardId)
        CardData cardData = null;
        if (sourceEntity != null && sourceEntity._cardData != null)
        {
            cardData = sourceEntity._cardData;
        }
        else if (!string.IsNullOrEmpty(cardId))
        {
            cardData = ResourceManager.Instance.GetCardData(cardId);
        }
        else if (entityDataFallback != null && !string.IsNullOrEmpty(entityDataFallback.cardId))
        {
            cardData = ResourceManager.Instance.GetCardData(entityDataFallback.cardId);
        }

        // 4. 발사 시작 위치 결정 (발사 주체 하수인 위치 or 시전자 리더 위치)
        Vector3 startPos;
        if (sourceEntity != null)
        {
            startPos = sourceEntity.transform.position;
        }
        else
        {
            GameCardDisplay casterLeader = GetCasterLeader(sourceEntityId, entityDataFallback);
            startPos = casterLeader != null ? casterLeader.transform.position : transform.position;
            Debug.Log($"[PlayActionProjectileRoutine] 발사 주체 하수인 없음 (주문/스킬). 시전자 리더: {(casterLeader == myLeader ? "내 리더" : "상대 리더")}, 시작위치: {startPos}, cardId: {cardId}");
        }

        // 공격력/데미지 수치 계산 (명시적 전달값 > 엔티티 실시간 스탯 > 원본 카드 스탯)
        int currentAtk = explicitDamage > 0
            ? explicitDamage
            : ((sourceEntity != null && sourceEntity.CurrentEntityData != null)
                ? sourceEntity.CurrentEntityData.attack
                : (cardData != null ? cardData.attack : 0));

        // 5. 투사체 프리팹 및 사운드 결정
        GameObject prefabToUse = null;
        CardVFXData vfxData = null;
        bool isHeavyAttack = false;
        AudioClip soundToPlay = null;

        if (cardData != null)
        {
            // A. 효과/공격 트리거인 경우: triggerVFXList에서 해당 트리거 전용 CardVFXData 탐색
            if (triggerType != EffectTriggerType.NONE)
            {
                vfxData = cardData.GetTriggerVFX(triggerType);
                if (vfxData != null)
                {
                    // 일반 공격(ON_ATTACK)인 경우: 데미지(공격력) 기준치에 따른 약공/강공 분기
                    if (triggerType == EffectTriggerType.ON_ATTACK)
                    {
                        int threshold = vfxData.heavyDamageThreshold > 0 ? vfxData.heavyDamageThreshold : 6;
                        if (currentAtk >= threshold && vfxData.heavyVfxPrefab != null)
                        {
                            prefabToUse = vfxData.heavyVfxPrefab;
                            isHeavyAttack = true;
                            soundToPlay = vfxData.heavySoundEffect != null ? vfxData.heavySoundEffect : vfxData.soundEffect;
                        }
                        else if (vfxData.vfxPrefab != null)
                        {
                            prefabToUse = vfxData.vfxPrefab;
                            soundToPlay = vfxData.soundEffect;
                        }
                    }
                    else
                    {
                        // 기타 효과 트리거 (ON_PLAY, ON_DEATH 등)
                        if (vfxData.vfxPrefab != null)
                        {
                            prefabToUse = vfxData.vfxPrefab;
                            soundToPlay = vfxData.soundEffect;
                        }
                    }
                }
            }

            // B. CardVFXData가 등록되지 않았을 때의 기본/강화 투사체 선택 (기존 카드 100% 하위 호환 Fallback)
            if (prefabToUse == null)
            {
                if (currentAtk >= 6 && cardData.heavyProjectilePrefab != null)
                {
                    prefabToUse = cardData.heavyProjectilePrefab;
                    isHeavyAttack = true;
                }
                else if (cardData.projectilePrefab != null)
                {
                    prefabToUse = cardData.projectilePrefab;
                }
            }
        }

        // C. 투사체 프리팹이 등록되지 않은 경우 프로젝트 기본 투사체 프리팹으로 폴백
        if (prefabToUse == null)
        {
            prefabToUse = defaultProjectilePrefab;
        }

        // 리더를 향한 공격인 경우, 이 투사체/VFX(CardVFXData)에 지정된 전용 리더 패배 모션을 기록
        if (targetEntity == myLeader || targetEntity == opponentLeader)
        {
            if (vfxData != null && vfxData.leaderDefeatMotion != null)
            {
                LastKillingDefeatData = vfxData.leaderDefeatMotion;
            }
        }

        // 6. 트리거 반짝임 펄스 연출 및 투사체/피격 발사 (명중할 때까지 대기)
        if (sourceEntity != null && triggerType != EffectTriggerType.NONE)
        {
            sourceEntity.PlayTriggerAnimation(triggerType);
        }

        float singleDelay = vfxData != null ? vfxData.damageDelay : 0.35f;
        float singleDestroyTime = vfxData != null ? vfxData.vfxDestroyTime : 3.0f;

        // [ON_DEATH 연출 최적화] 퇴장 효과인 경우: 투사체/효과 발사와 동시에 하수인의 사망/축소 연출을 병렬로 즉시 시작!
        if (triggerType == EffectTriggerType.ON_DEATH && sourceEntity != null && sourceEntity != myLeader && sourceEntity != opponentLeader)
        {
            sourceEntity.transform.DOKill();
            sourceEntity.transform.DOScale(Vector3.zero, 0.35f);
        }

        if (vfxData != null && vfxData.playType == VFXPlayType.AreaFromCaster)
        {
            if (prefabToUse != null)
            {
                SpawnDirectVFX(prefabToUse, startPos, singleDestroyTime);
            }
            AudioClip clip = soundToPlay != null ? soundToPlay : vfxData.soundEffect;
            if (clip != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX3D(clip, startPos);
            }
            if (singleDelay > 0f)
            {
                yield return YieldInstructionCache.WaitForSeconds(singleDelay);
            }
        }
        else if (vfxData != null && vfxData.playType == VFXPlayType.ImpactOnTarget)
        {
            if (prefabToUse != null)
            {
                SpawnDirectVFX(prefabToUse, targetEntity.transform.position, singleDestroyTime);
            }
            AudioClip clip = soundToPlay != null ? soundToPlay : vfxData.soundEffect;
            if (clip != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX3D(clip, targetEntity.transform.position);
            }
            if (singleDelay > 0f)
            {
                yield return YieldInstructionCache.WaitForSeconds(singleDelay);
            }
        }
        else if (prefabToUse != null)
        {
            AudioClip clip = soundToPlay != null ? soundToPlay : (vfxData != null ? vfxData.soundEffect : null);
            if (clip != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX3D(clip, startPos);
            }

            bool isHit = false;
            FireProjectileDirect(prefabToUse, startPos, targetEntity.transform.position, () => 
            { 
                isHit = true; 
                // 💥 6데미지 이상 대형 투사체 적중 시 화면 흔들림 효과 연출
                if (isHeavyAttack)
                {
                    CameraShakeManager.Instance?.ShakeHeavy();
                }
            }, vfxData != null ? vfxData.damageDelay : (float?)null);
            yield return new WaitUntil(() => isHit);
        }
    }

    /// <summary> 공격 선언 연출을 처리합니다. </summary>
    private IEnumerator HandleAttack(GameEvent log)
    {
        // 공격 이벤트는 triggerType이 NONE이더라도 ON_ATTACK으로 처리하여 통일된 CardVFXData를 사용할 수 있도록 함
        EffectTriggerType triggerType = (log.triggerType != EffectTriggerType.NONE) ? log.triggerType : EffectTriggerType.ON_ATTACK;
        yield return StartCoroutine(PlayActionProjectileRoutine(log.sourceEntityId, log.targetEntityId, triggerType, log.cardId, log.entityData, explicitDamage: log.value));
    }

    /// <summary> 데미지 발생 연출 (UI 표시, 피격 모션 등)을 처리합니다. </summary>
    private IEnumerator HandleDamage(GameEvent log, bool isDirectCombatDamage = false)
    {
        GameCardDisplay damagedEntity = GetEntityDisplay(log.targetEntityId);

        if (damagedEntity != null)
        {
            if (damagedEntity == myLeader || damagedEntity == opponentLeader)
            {
                LastLeaderDamageEvent = log;
                LastKillingAttackTypeKey = (log.value >= 6) ? "Heavy" : "Normal";
            }

            // 일반 전투(ATTACK 직후 발생한 DAMAGE)가 아니고, 효과 데미지인 경우 투사체 발사
            if (!isDirectCombatDamage)
            {
                yield return StartCoroutine(PlayActionProjectileRoutine(log.sourceEntityId, log.targetEntityId, log.triggerType, log.cardId, log.entityData));
            }

            // 데미지 수치 UI 출력
            damagedEntity.DamageUI(log.value);

            // 리더가 피격당한 경우, 데미지 숫자가 확실히 화면에 노출되고 피격 반응을 마칠 수 있도록 대기
            if (damagedEntity == myLeader || damagedEntity == opponentLeader)
            {
                yield return YieldInstructionCache.WaitForSeconds(0.7f);
            }
        }
    }

    /// <summary>
    /// 동일한 시전자와 트리거에 의해 발생한 단일/광역 효과 데미지를 VFXPlayType에 맞춰 일괄 처리합니다.
    /// </summary>
    private IEnumerator HandleBatchDamage(List<GameEvent> damageBatch)
    {
        if (damageBatch == null || damageBatch.Count == 0) yield break;

        var firstEvent = damageBatch[0];
        int sourceEntityId = firstEvent.sourceEntityId;
        EffectTriggerType triggerType = firstEvent.triggerType;
        string cardId = firstEvent.cardId;
        EntityData entityDataFallback = firstEvent.entityData;

        // 1. 발사 주체 찾기
        GameCardDisplay sourceEntity = null;
        if (sourceEntityId > 0)
        {
            _spawnedEntities.TryGetValue(sourceEntityId, out sourceEntity);
        }

        // 2. 카드 데이터 및 VFX 데이터 조회
        CardData cardData = null;
        if (sourceEntity != null && sourceEntity._cardData != null)
        {
            cardData = sourceEntity._cardData;
        }
        else if (!string.IsNullOrEmpty(cardId))
        {
            cardData = ResourceManager.Instance.GetCardData(cardId);
        }
        else if (entityDataFallback != null && !string.IsNullOrEmpty(entityDataFallback.cardId))
        {
            cardData = ResourceManager.Instance.GetCardData(entityDataFallback.cardId);
        }

        CardVFXData vfxData = null;
        if (cardData != null && triggerType != EffectTriggerType.NONE)
        {
            vfxData = cardData.GetTriggerVFX(triggerType);
        }

        // 3. 연출 방식 및 프리팹 결정
        VFXPlayType playType = vfxData != null ? vfxData.playType : VFXPlayType.Projectile;
        GameObject prefabToUse = null;

        int currentAtk = (sourceEntity != null && sourceEntity.CurrentEntityData != null)
            ? sourceEntity.CurrentEntityData.attack
            : (cardData != null ? cardData.attack : 0);

        if (vfxData != null)
        {
            if (triggerType == EffectTriggerType.ON_ATTACK)
            {
                int threshold = vfxData.heavyDamageThreshold > 0 ? vfxData.heavyDamageThreshold : 6;
                if (currentAtk >= threshold && vfxData.heavyVfxPrefab != null)
                {
                    prefabToUse = vfxData.heavyVfxPrefab;
                }
                else
                {
                    prefabToUse = vfxData.vfxPrefab;
                }
            }
            else
            {
                prefabToUse = vfxData.vfxPrefab;
            }
        }

        if (prefabToUse == null && playType == VFXPlayType.Projectile && cardData != null)
        {
            prefabToUse = (currentAtk >= 6 && cardData.heavyProjectilePrefab != null)
                ? cardData.heavyProjectilePrefab
                : cardData.projectilePrefab;
        }

        // 4. 발사 시작 위치 (시전자 또는 시전자 리더)
        Vector3 startPos;
        if (sourceEntity != null)
        {
            startPos = sourceEntity.transform.position;
        }
        else
        {
            GameCardDisplay casterLeader = GetCasterLeader(sourceEntityId, entityDataFallback);
            startPos = casterLeader != null ? casterLeader.transform.position : transform.position;
            Debug.Log($"[HandleBatchDamage] 시전자 하수인 없음 (광역 주문 등). 시전자 리더: {(casterLeader == myLeader ? "내 리더" : "상대 리더")}, 시작위치: {startPos}");
        }

        // 5. 시전자 펄스 연출
        if (sourceEntity != null && triggerType != EffectTriggerType.NONE)
        {
            sourceEntity.PlayTriggerAnimation(triggerType);
        }

        // 6. 유효한 타겟 필터링
        List<GameEvent> validEvents = new List<GameEvent>();
        List<GameCardDisplay> validTargetDisplays = new List<GameCardDisplay>();
        foreach (var evt in damageBatch)
        {
            if (_spawnedEntities.TryGetValue(evt.targetEntityId, out var targetDisplay) && targetDisplay != null)
            {
                validEvents.Add(evt);
                validTargetDisplays.Add(targetDisplay);
            }
        }

        if (validEvents.Count == 0) yield break;

        float delay = vfxData != null ? vfxData.damageDelay : 0.35f;
        float destroyTime = vfxData != null ? vfxData.vfxDestroyTime : 3.0f;

        // 7. 연출 방식에 따른 분기 실행
        switch (playType)
        {
            case VFXPlayType.AreaFromCaster:
            {
                // [시전자 중심 광역 이펙트] (충격파, 포효, 신성폭발 등)
                // 시전자 위치에 단 1회 생성 (EFFECT_TRIGGER 등에서 이미 재생되지 않은 경우에만 생성)
                string vfxKey = $"{sourceEntityId}_{(int)triggerType}_{cardData?.cardID ?? cardId}";
                bool needSpawn = _playedAreaVFXKeys.Add(vfxKey);

                if (needSpawn)
                {
                    if (prefabToUse != null)
                    {
                        SpawnDirectVFX(prefabToUse, startPos, destroyTime);
                    }

                    if (vfxData != null && vfxData.soundEffect != null && SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySFX3D(vfxData.soundEffect, startPos);
                    }

                    if (delay > 0f)
                    {
                        yield return YieldInstructionCache.WaitForSeconds(delay);
                    }
                }

                for (int idx = 0; idx < validEvents.Count; idx++)
                {
                    validTargetDisplays[idx].DamageUI(validEvents[idx].value);
                }

                yield return YieldInstructionCache.WaitForSeconds(0.25f);
                break;
            }

            case VFXPlayType.AreaField:
            {
                // [광역 필드 이펙트] 대상 필드 중앙에 단 1회 생성 (화염폭풍, 지진 등)
                string vfxKey = $"{sourceEntityId}_{(int)triggerType}_{cardData?.cardID ?? cardId}";
                bool needSpawn = _playedAreaVFXKeys.Add(vfxKey);

                if (needSpawn)
                {
                    Vector3 centerPos = GetFieldCenterForTargets(validTargetDisplays);
                    if (prefabToUse != null)
                    {
                        SpawnDirectVFX(prefabToUse, centerPos, destroyTime);
                    }

                    if (vfxData != null && vfxData.soundEffect != null && SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySFX3D(vfxData.soundEffect, centerPos);
                    }

                    if (delay > 0f)
                    {
                        yield return YieldInstructionCache.WaitForSeconds(delay);
                    }
                }

                for (int idx = 0; idx < validEvents.Count; idx++)
                {
                    validTargetDisplays[idx].DamageUI(validEvents[idx].value);
                }

                bool hasLeaderTarget1 = validTargetDisplays.Exists(d => d == myLeader || d == opponentLeader);
                yield return YieldInstructionCache.WaitForSeconds(hasLeaderTarget1 ? 0.7f : 0.25f);
                break;
            }

            case VFXPlayType.ImpactOnTarget:
            {
                // [대상 직접 피격 이펙트] 모든 대상의 위치에 동시에 생성 (번개, 칼베기 등)
                if (prefabToUse != null)
                {
                    for (int idx = 0; idx < validTargetDisplays.Count; idx++)
                    {
                        SpawnDirectVFX(prefabToUse, validTargetDisplays[idx].transform.position, destroyTime);
                    }
                }

                if (vfxData != null && vfxData.soundEffect != null && SoundManager.Instance != null && validTargetDisplays.Count > 0)
                {
                    SoundManager.Instance.PlaySFX3D(vfxData.soundEffect, validTargetDisplays[0].transform.position);
                }

                if (delay > 0f)
                {
                    yield return YieldInstructionCache.WaitForSeconds(delay);
                }

                for (int idx = 0; idx < validEvents.Count; idx++)
                {
                    var targetDisplay = validTargetDisplays[idx];
                    if (targetDisplay == myLeader || targetDisplay == opponentLeader)
                    {
                        LastLeaderDamageEvent = validEvents[idx];
                        LastKillingAttackTypeKey = (validEvents[idx].value >= 6) ? "Heavy" : "Normal";
                        if (vfxData != null && vfxData.leaderDefeatMotion != null)
                        {
                            LastKillingDefeatData = vfxData.leaderDefeatMotion;
                        }
                    }
                    targetDisplay.DamageUI(validEvents[idx].value);
                }

                bool hasLeaderTarget2 = validTargetDisplays.Exists(d => d == myLeader || d == opponentLeader);
                yield return YieldInstructionCache.WaitForSeconds(hasLeaderTarget2 ? 0.7f : 0.25f);
                break;
            }

            case VFXPlayType.Projectile:
            default:
            {
                // [투사체 발사] CardVFXData의 projectileInterval에 따라 동시 발사(0) 또는 순차 발사(>0) 결정
                int pendingHits = validEvents.Count;
                float interval = (vfxData != null) ? vfxData.projectileInterval : 0f;

                for (int idx = 0; idx < validEvents.Count; idx++)
                {
                    var targetDisplay = validTargetDisplays[idx];
                    int damageValue = validEvents[idx].value;

                    if (targetDisplay == myLeader || targetDisplay == opponentLeader)
                    {
                        LastLeaderDamageEvent = validEvents[idx];
                        LastKillingAttackTypeKey = (damageValue >= 6) ? "Heavy" : "Normal";
                        if (vfxData != null && vfxData.leaderDefeatMotion != null)
                        {
                            LastKillingDefeatData = vfxData.leaderDefeatMotion;
                        }
                    }

                    if (prefabToUse != null)
                    {
                        FireProjectileDirect(prefabToUse, startPos, targetDisplay.transform.position, () =>
                        {
                            targetDisplay.DamageUI(damageValue);
                            pendingHits--;
                        }, vfxData != null ? vfxData.damageDelay : (float?)null);
                    }
                    else
                    {
                        targetDisplay.DamageUI(damageValue);
                        pendingHits--;
                    }

                    // 여러 발을 쏠 때, 마지막 발사가 아니라면 다음 발사 전까지 딜레이를 둡니다.
                    if (validEvents.Count > 1 && idx < validEvents.Count - 1 && interval > 0f)
                    {
                        yield return YieldInstructionCache.WaitForSeconds(interval);
                    }
                }

                yield return new WaitUntil(() => pendingHits <= 0);
                bool hasLeaderTarget3 = validTargetDisplays.Exists(d => d == myLeader || d == opponentLeader);
                yield return YieldInstructionCache.WaitForSeconds(hasLeaderTarget3 ? 0.7f : 0.2f);
                break;
            }
        }
    }

    /// <summary>
    /// 대상 목록의 위치와 소유 진영을 분석하여 적절한 필드 중앙 좌표를 반환합니다.
    /// </summary>
    private Vector3 GetFieldCenterForTargets(List<GameCardDisplay> targets)
    {
        if (targets == null || targets.Count == 0) return transform.position;

        // 타겟 중 상대방 개체가 포함되어 있는지 확인
        bool isOpponentSide = false;
        foreach (var t in targets)
        {
            if (t == null) continue;
            if (t.CurrentEntityData != null && t.CurrentEntityData.ownerUid != MyUid)
            {
                isOpponentSide = true;
                break;
            }
            if (t == opponentLeader)
            {
                isOpponentSide = true;
                break;
            }
        }

        if (isOpponentSide)
        {
            // 1순위: 인스펙터에 직접 지정된 상대 필드 중심점
            if (opponentFieldCenter != null)
            {
                return opponentFieldCenter.position;
            }
            // 2순위 (Fallback): 중앙 슬롯 (5개 중 2번 슬롯)
            if (opponentFieldSlots != null && opponentFieldSlots.Length > 0)
            {
                int mid = opponentFieldSlots.Length / 2;
                if (opponentFieldSlots[mid] != null)
                {
                    return opponentFieldSlots[mid].transform.position;
                }
            }
        }
        else
        {
            // 1순위: 인스펙터에 직접 지정된 내 필드 중심점
            if (myFieldCenter != null)
            {
                return myFieldCenter.position;
            }
            // 2순위 (Fallback): 중앙 슬롯 (5개 중 2번 슬롯)PlayActionProjectileRoutine
            if (myFieldSlots != null && myFieldSlots.Length > 0)
            {
                int mid = myFieldSlots.Length / 2;
                if (myFieldSlots[mid] != null)
                {
                    return myFieldSlots[mid].transform.position;
                }
            }
        }

        // 슬롯이 없거나 특수한 경우: 타겟들의 위치 평균값
        Vector3 avgPos = Vector3.zero;
        int count = 0;
        foreach (var t in targets)
        {
            if (t != null)
            {
                avgPos += t.transform.position;
                count++;
            }
        }
        return count > 0 ? (avgPos / count) : transform.position;
    }

    /// <summary> 체력 회복 연출을 처리합니다. </summary>
    private IEnumerator HandleHeal(GameEvent log)
    {
        GameCardDisplay healedEntity = GetEntityDisplay(log.targetEntityId);

        if (healedEntity != null)
        {
            if (log.sourceEntityId > 0 && log.sourceEntityId != log.targetEntityId)
            {
                yield return StartCoroutine(PlayActionProjectileRoutine(log.sourceEntityId, log.targetEntityId, log.triggerType, log.cardId, log.entityData));
            }

            // 힐 UI 연출 실행 (리더는 'HEAL' 텍스트 표시)
            healedEntity.HealUI(log.value);

            if (healedEntity == myLeader || healedEntity == opponentLeader)
            {
                yield return YieldInstructionCache.WaitForSeconds(0.7f);
            }
        }
    }

    /// <summary> 스탯 버프 연출을 처리합니다. </summary>
    private IEnumerator HandleBuff(GameEvent log)
    {
        if (_spawnedEntities.TryGetValue(log.targetEntityId, out var targetEntity))
        {
            if (log.sourceEntityId != log.targetEntityId)
            {
                yield return StartCoroutine(PlayActionProjectileRoutine(log.sourceEntityId, log.targetEntityId, log.triggerType, log.cardId, log.entityData));
            }

            targetEntity.PlayBuffVFX();
            targetEntity.transform.DOKill();
            targetEntity.transform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 5, 0.5f);
        }
    }

    /// <summary> 개체 사망 연출을 처리합니다. </summary>
    private IEnumerator HandleDeath(GameEvent log)
    {
        int deadId = log.sourceEntityId > 0 ? log.sourceEntityId : log.targetEntityId;
        if (_spawnedEntities.TryGetValue(deadId, out var deadEntity) && deadEntity != null)
        {
            // 이미 ON_DEATH 연출 단계에서 축소가 시작되었거나 완료된 경우 중복 트윈 방지
            if (deadEntity.transform.localScale.sqrMagnitude > 0.001f)
            {
                deadEntity.transform.DOKill();
                deadEntity.transform.DOScale(Vector3.zero, 0.25f);
            }
        }
        yield break;
    }

    /// <summary> 전투의 함성, 죽음의 메아리 등 특수 효과 발동 연출을 처리합니다. </summary>
    private IEnumerator HandleEffectTrigger(GameEvent log)
    {
        // 1. 발사 주체 하수인/리더 찾기
        GameCardDisplay triggerEntity = null;
        if (log.sourceEntityId > 0)
        {
            _spawnedEntities.TryGetValue(log.sourceEntityId, out triggerEntity);
        }

        // 2. 카드 본체 펄스 애니메이션 (아이콘 번쩍임)
        if (triggerEntity != null && log.triggerType != EffectTriggerType.NONE)
        {
            triggerEntity.PlayTriggerAnimation(log.triggerType);
        }

        // 3. 카드 데이터 및 VFX 데이터 조회
        CardData cardData = null;
        if (triggerEntity != null && triggerEntity._cardData != null)
        {
            cardData = triggerEntity._cardData;
        }
        else if (!string.IsNullOrEmpty(log.cardId))
        {
            cardData = ResourceManager.Instance.GetCardData(log.cardId);
        }

        if (cardData != null && log.triggerType != EffectTriggerType.NONE)
        {
            var vfxData = cardData.GetTriggerVFX(log.triggerType);
            if (vfxData != null)
            {
                string key = $"{log.sourceEntityId}_{(int)log.triggerType}_{cardData.cardID}";
                float delay = vfxData.damageDelay > 0f ? vfxData.damageDelay : 0.35f;
                float destroyTime = vfxData.vfxDestroyTime > 0f ? vfxData.vfxDestroyTime : 3.0f;

                // [AreaFromCaster] 시전자 중심 광역 효과: 발동 시점에 즉시 시전자 위치에 소환
                if (vfxData.playType == VFXPlayType.AreaFromCaster)
                {
                    if (_playedAreaVFXKeys.Add(key))
                    {
                        GameCardDisplay casterLeader = GetCasterLeader(log.sourceEntityId, log.entityData);
                        Vector3 startPos = triggerEntity != null ? triggerEntity.transform.position : (casterLeader != null ? casterLeader.transform.position : transform.position);
                        if (vfxData.vfxPrefab != null)
                        {
                            SpawnDirectVFX(vfxData.vfxPrefab, startPos, destroyTime);
                        }

                        if (vfxData.soundEffect != null && SoundManager.Instance != null)
                        {
                            SoundManager.Instance.PlaySFX3D(vfxData.soundEffect, startPos);
                        }

                        if (delay > 0f)
                        {
                            yield return YieldInstructionCache.WaitForSeconds(delay);
                        }
                    }
                }
                // [AreaField] 필드 광역 효과: 발동 시점에 상대 필드 중앙에 소환
                else if (vfxData.playType == VFXPlayType.AreaField)
                {
                    if (_playedAreaVFXKeys.Add(key))
                    {
                        bool isOpponentCasting = BattleManager.Instance != null && !BattleManager.Instance.IsMyTurn;
                        Transform targetCenter = isOpponentCasting ? myFieldCenter : opponentFieldCenter;
                        Vector3 centerPos = targetCenter != null ? targetCenter.position : GetFieldCenterForTargets(null);
                        if (vfxData.vfxPrefab != null)
                        {
                            SpawnDirectVFX(vfxData.vfxPrefab, centerPos, destroyTime);
                        }

                        if (vfxData.soundEffect != null && SoundManager.Instance != null)
                        {
                            SoundManager.Instance.PlaySFX3D(vfxData.soundEffect, centerPos);
                        }

                        if (delay > 0f)
                        {
                            yield return YieldInstructionCache.WaitForSeconds(delay);
                        }
                    }
                }
            }
        }

        yield break;
    }

    /// <summary>
    /// 단일 또는 여러 개의 즉시 처치(DESTROY) 이벤트를 일괄 처리합니다.
    /// </summary>
    private IEnumerator HandleBatchDestroy(List<GameEvent> destroyBatch)
    {
        if (destroyBatch == null || destroyBatch.Count == 0) yield break;

        var firstEvent = destroyBatch[0];
        int sourceEntityId = firstEvent.sourceEntityId;
        EffectTriggerType triggerType = firstEvent.triggerType;
        string cardId = firstEvent.cardId;
        EntityData entityDataFallback = firstEvent.entityData;

        // 1. 발사 주체 찾기
        GameCardDisplay sourceEntity = null;
        if (sourceEntityId > 0)
        {
            _spawnedEntities.TryGetValue(sourceEntityId, out sourceEntity);
        }

        // 2. 카드 데이터 및 VFX 데이터 조회
        CardData cardData = null;
        if (sourceEntity != null && sourceEntity._cardData != null)
        {
            cardData = sourceEntity._cardData;
        }
        else if (!string.IsNullOrEmpty(cardId))
        {
            cardData = ResourceManager.Instance.GetCardData(cardId);
        }
        else if (entityDataFallback != null && !string.IsNullOrEmpty(entityDataFallback.cardId))
        {
            cardData = ResourceManager.Instance.GetCardData(entityDataFallback.cardId);
        }

        CardVFXData vfxData = null;
        if (cardData != null && triggerType != EffectTriggerType.NONE)
        {
            vfxData = cardData.GetTriggerVFX(triggerType);
        }

        // 3. 유효한 타겟 목록 구성
        List<GameCardDisplay> validTargets = new List<GameCardDisplay>();
        foreach (var evt in destroyBatch)
        {
            if (_spawnedEntities.TryGetValue(evt.targetEntityId, out var targetDisplay) && targetDisplay != null)
            {
                validTargets.Add(targetDisplay);
            }
        }

        // 4. VFX 연출 실행 (만약 EFFECT_TRIGGER에서 이미 AreaFromCaster/AreaField가 재생되지 않았다면 여기서 재생)
        string vfxKey = $"{sourceEntityId}_{(int)triggerType}_{cardData?.cardID ?? cardId}";
        bool needSpawn = _playedAreaVFXKeys.Add(vfxKey);

        if (needSpawn && vfxData != null)
        {
            GameCardDisplay casterLeader = GetCasterLeader(sourceEntityId, entityDataFallback);
            Vector3 startPos = sourceEntity != null ? sourceEntity.transform.position : (casterLeader != null ? casterLeader.transform.position : transform.position);
            float delay = vfxData.damageDelay > 0f ? vfxData.damageDelay : 0.35f;
            float destroyTime = vfxData.vfxDestroyTime > 0f ? vfxData.vfxDestroyTime : 3.0f;

            switch (vfxData.playType)
            {
                case VFXPlayType.AreaFromCaster:
                    if (vfxData.vfxPrefab != null)
                    {
                        SpawnDirectVFX(vfxData.vfxPrefab, startPos, destroyTime);
                    }
                    if (vfxData.soundEffect != null && SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySFX3D(vfxData.soundEffect, startPos);
                    }
                    if (delay > 0f) yield return YieldInstructionCache.WaitForSeconds(delay);
                    break;

                case VFXPlayType.AreaField:
                    Vector3 centerPos = GetFieldCenterForTargets(validTargets);
                    if (vfxData.vfxPrefab != null)
                    {
                        SpawnDirectVFX(vfxData.vfxPrefab, centerPos, destroyTime);
                    }
                    if (vfxData.soundEffect != null && SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySFX3D(vfxData.soundEffect, centerPos);
                    }
                    if (delay > 0f) yield return YieldInstructionCache.WaitForSeconds(delay);
                    break;

                case VFXPlayType.ImpactOnTarget:
                    if (vfxData.vfxPrefab != null)
                    {
                        foreach (var target in validTargets)
                        {
                            SpawnDirectVFX(vfxData.vfxPrefab, target.transform.position, destroyTime);
                        }
                    }
                    if (vfxData.soundEffect != null && SoundManager.Instance != null && validTargets.Count > 0)
                    {
                        SoundManager.Instance.PlaySFX3D(vfxData.soundEffect, validTargets[0].transform.position);
                    }
                    if (delay > 0f) yield return YieldInstructionCache.WaitForSeconds(delay);
                    break;

                case VFXPlayType.Projectile:
                    int pendingHits = validTargets.Count;
                    foreach (var target in validTargets)
                    {
                        if (vfxData.vfxPrefab != null)
                        {
                            FireProjectileDirect(vfxData.vfxPrefab, startPos, target.transform.position, () =>
                            {
                                pendingHits--;
                            }, delay);
                        }
                        else
                        {
                            pendingHits--;
                        }
                    }
                    yield return new WaitUntil(() => pendingHits <= 0);
                    break;
            }
        }

        // 5. 파괴되는 대상 하수인들의 피격 및 사망 흔들림 연출
        foreach (var target in validTargets)
        {
            if (target != null)
            {
                target.transform.DOKill();
                target.transform.DOShakePosition(0.35f, 0.3f, 10, 90f);
            }
        }

        yield return YieldInstructionCache.WaitForSeconds(0.35f);
    }

    /// <summary> 손패 버리기 연출을 처리합니다. </summary>
    private IEnumerator HandleDiscard(GameEvent log)
    {
        string cardId = log.stringValue ?? log.cardId;

        // 1. 내 리더 엔티티 ID 및 상대 리더 엔티티 ID 확인
        int myLeaderId = (myLeader != null && myLeader.CurrentEntityData != null) ? myLeader.CurrentEntityData.entityId : (myLeader != null ? myLeader.EntityId : 0);
        int oppLeaderId = (opponentLeader != null && opponentLeader.CurrentEntityData != null) ? opponentLeader.CurrentEntityData.entityId : (opponentLeader != null ? opponentLeader.EntityId : 0);

        // 2. 누구의 손패가 버려지는지 피아 판정
        bool isMyDiscard = false;
        if (log.targetEntityId > 0 && myLeaderId > 0)
        {
            isMyDiscard = (log.targetEntityId == myLeaderId);
        }
        else if (log.sourceEntityId > 0 && myLeaderId > 0)
        {
            isMyDiscard = (log.sourceEntityId == myLeaderId);
        }
        else
        {
            // 폴백: 엔티티 ID로 판별할 수 없을 때, 내 손패에 해당 cardId가 실제로 있는지 검사
            if (HandCardControllManager.instance != null && HandCardControllManager.instance.handCards != null && !string.IsNullOrEmpty(cardId))
            {
                isMyDiscard = HandCardControllManager.instance.handCards.Exists(c =>
                {
                    if (c == null) return false;
                    var disp = c.GetComponent<GameCardDisplay>();
                    return disp != null && disp._cardData != null && disp._cardData.cardID == cardId;
                });
            }
        }

        // 3. 내 손패 버리기
        if (isMyDiscard)
        {
            if (HandCardControllManager.instance != null && HandCardControllManager.instance.handCards != null)
            {
                GameObject targetCard = null;
                if (!string.IsNullOrEmpty(cardId))
                {
                    targetCard = HandCardControllManager.instance.handCards.Find(c =>
                    {
                        if (c == null) return false;
                        var disp = c.GetComponent<GameCardDisplay>();
                        return disp != null && disp._cardData != null && disp._cardData.cardID == cardId;
                    });
                }

                if (targetCard == null && HandCardControllManager.instance.handCards.Count > 0)
                {
                    targetCard = HandCardControllManager.instance.handCards[0];
                }

                if (targetCard != null)
                {
                    targetCard.transform.DOKill();
                    targetCard.transform.DOScale(Vector3.zero, 0.25f);
                    yield return YieldInstructionCache.WaitForSeconds(0.25f);
                    HandCardControllManager.instance.RemoveCardFromHand(targetCard);
                }
            }
        }
        // 4. 상대방 손패 버리기
        else
        {
            if (OpponentHandVisualizer.Instance != null)
            {
                OpponentHandVisualizer.Instance.DiscardOneCard();
                yield return YieldInstructionCache.WaitForSeconds(0.2f);
            }
        }

        yield break;
    }

    /// <summary> 필드 하수인을 손패로 되돌리는(바운스) 연출을 처리합니다. </summary>
    private IEnumerator HandleReturnToHand(GameEvent log)
    {
        if (_spawnedEntities.TryGetValue(log.targetEntityId, out var target) && target != null)
        {
            target.transform.DOKill();
            target.transform.DOMove(target.transform.position + Vector3.up * 2f, 0.3f);
            target.transform.DOScale(Vector3.zero, 0.3f);
            yield return YieldInstructionCache.WaitForSeconds(0.3f);
            RemoveEntity(log.targetEntityId);
        }
    }

    /// <summary> 필드 하수인을 덱으로 섞어 넣는 연출을 처리합니다. </summary>
    private IEnumerator HandleShuffleToDeck(GameEvent log)
    {
        if (_spawnedEntities.TryGetValue(log.targetEntityId, out var target) && target != null)
        {
            target.transform.DOKill();
            target.transform.DOScale(Vector3.zero, 0.3f);
            yield return YieldInstructionCache.WaitForSeconds(0.3f);
            RemoveEntity(log.targetEntityId);
        }
    }

    /// <summary> 손패 버프 연출을 처리합니다. </summary>
    private IEnumerator HandleBuffHand(GameEvent log)
    {
        yield return YieldInstructionCache.WaitForSeconds(0.1f);
    }

    /// <summary> 덱 버프 연출을 처리합니다. </summary>
    private IEnumerator HandleBuffDeck(GameEvent log)
    {
        yield return YieldInstructionCache.WaitForSeconds(0.1f);
    }

    /// <summary> 덱 서치 연출을 처리합니다. </summary>
    private IEnumerator HandleSearchDeck(GameEvent log)
    {
        yield return YieldInstructionCache.WaitForSeconds(0.1f);
    }

    /// <summary> 속박 (빙결) 부여 연출을 처리합니다. </summary>
    private IEnumerator HandleBind(GameEvent log)
    {
        if (log.sourceEntityId != log.targetEntityId)
        {
            yield return StartCoroutine(PlayActionProjectileRoutine(log.sourceEntityId, log.targetEntityId, log.triggerType, log.cardId, log.entityData));
        }
    }

    /// <summary> 침묵 부여 연출을 처리합니다. </summary>
    private IEnumerator HandleSilence(GameEvent log)
    {
        if (log.sourceEntityId != log.targetEntityId)
        {
            yield return StartCoroutine(PlayActionProjectileRoutine(log.sourceEntityId, log.targetEntityId, log.triggerType, log.cardId, log.entityData));
        }
    }

    /// <summary> 강제 공격 연출을 처리합니다. </summary>
    private IEnumerator HandleForceAttack(GameEvent log)
    {
        // TODO: 강제 타겟팅 지정 및 공격 실행 연출 로직 작성
        yield break;
    }

    /// <summary> 키워드(도발, 속공 등) 부여 연출을 처리합니다. </summary>
    private IEnumerator HandleGrantKeyword(GameEvent log)
    {
        if (log.sourceEntityId != log.targetEntityId)
        {
            yield return StartCoroutine(PlayActionProjectileRoutine(log.sourceEntityId, log.targetEntityId, log.triggerType, log.cardId, log.entityData));
        }
    }

    /// <summary> 마나 조작(펌핑 또는 파괴) 연출을 처리합니다. </summary>
    private IEnumerator HandleManaMod(GameEvent log)
    {
        // TODO: 마나 수정이 추가되거나 깨지는 UI 이펙트 로직 작성
        yield break;
    }

    /// <summary> 하수인 소환 연출을 처리합니다. </summary>
    private IEnumerator HandleSummon(GameEvent log)
    {
        if (log.entityData != null)
        {
            // 1. 큐 매니저에게 대기 중인 UI 카드가 있다면 매칭/정리
            if (CardActionQueueManager.Instance != null)
            {
                CardActionQueueManager.Instance.ResolvePlay(log.entityData);
            }

            bool isMine = (log.entityData.ownerUid == MyUid);

            // 2. 3D 필드 소환 및 슬롯 배치, 활성화가 완전히 끝날 때까지 동기 대기
            yield return StartCoroutine(SpawnEntity(log.entityData, isMine));
        }
    }

    /// <summary> 카드 드로우 연출을 처리합니다. </summary>
    private IEnumerator HandleDraw(GameEvent log)
    {
        // TODO: 덱에서 카드가 뽑히는 DOTween 애니메이션 대기 로직 작성
        yield break;
    }

    /// <summary>
    /// 애니메이션이 끝나고 보여질 최종 필드
    /// </summary>
    public void HandleEntitiesUpdated(List<EntityData> updatedList)
    {
        if (updatedList == null) return;

        foreach (var entityData in updatedList)
        {
            // 스스로 내 것인지 판단합니다.
            bool isMine = (entityData.ownerUid == MyUid);

            // 이미 있는 녀석인가?
            if (_spawnedEntities.ContainsKey(entityData.entityId))
            {
                UpdateEntity(entityData);
            }
            else
            {
                // 없는데 살아있다면 새로 소환! (단, 이미 사망 처리되었거나 사망 예정인 엔티티는 재소환 방지)
                if (entityData.health > 0 && !_pendingDeadEntityIds.Contains(entityData.entityId))
                {
                    StartCoroutine(SpawnEntity(entityData, isMine));
                }
            }
        }
    }

    public void SpawnCard(EntityData entityData)
    {
        bool isMine = entityData.ownerUid == MyUid;
        StartCoroutine(SpawnEntity(entityData, isMine));
    }

    // ==================================================================
    // 2. 소환 및 갱신 로직
    // ==================================================================


    /// <summary>
    /// EntityData전용 소환 스크립트
    /// </summary>
    /// <param name="entityData"></param>
    /// <param name="isMine"></param>
    /// <returns></returns>
    private IEnumerator SpawnEntity(EntityData entityData, bool isMine)
    {
        if (_spawnedEntities.ContainsKey(entityData.entityId))
        {
            Debug.LogWarning($"[SpawnEntity] 이미 존재하는 EntityId입니다. 중복 소환을 방지합니다. EntityId: {entityData.entityId}");
            yield break;
        }

        CardData cardData = ResourceManager.Instance.GetCardData(entityData.cardId, $"SpawnEntity (EntityId: {entityData.entityId}, Name: '{entityData.cardName}')");
        if (cardData == null)
        {
            Debug.LogWarning($"[SpawnEntity] CardData를 찾을 수 없습니다. cardId: {entityData.cardId}, entityId: {entityData.entityId}, cardName: '{entityData.cardName}'");
            yield break;
        }

        // 1. 하수인이 놓일 '배열' 결정 (필드 vs 멤버존)
        FieldSlot[] targetSlots;
        if (entityData.isMember)
            targetSlots = isMine ? myMemberSlots : opponentMemberSlots;
        else
            targetSlots = isMine ? myFieldSlots : opponentFieldSlots;

        // 2. 서버가 지정한 position 번호의 슬롯 찾기
        FieldSlot slot = null;
        if (targetSlots != null && entityData.position < targetSlots.Length)
        {
            slot = targetSlots[entityData.position];
        }

        // 만약 슬롯을 못 찾았다면 임시로 GameEntityManager 자신의 위치를 사용
        Transform finalParent = slot != null ? slot.transform : transform;

        // 3. 생성 및 배치 (슬롯의 위치와 회전값에 맞춤)
        GameObject newObj = Instantiate(minionPrefab, finalParent.position, finalParent.rotation, finalParent);
        GameCardDisplay display = newObj.GetComponent<GameCardDisplay>();

        // [추가] 4. 슬롯 상태 점유로 변경
        if (slot != null)
        {
            slot.IsOccupied = true;
            slot.cardData = cardData; // FieldSlot 스크립트에 있는 cardData에도 저장해두면 유용합니다.
        }

        // 5. 스폰 이펙트 및 사운드 실행
        if (cardData != null && cardData.spawnEffectData != null)
        {
            newObj.SetActive(false);
            cardData.spawnEffectData.PlaySpawnVFX(finalParent);
            cardData.spawnEffectData.PlaySpawnSound(position: finalParent.position);
            yield return YieldInstructionCache.WaitForSeconds(cardData.spawnEffectData.duration);
            newObj.SetActive(true);
        }
        else
        {
            newObj.SetActive(true);
        }

        if (display != null)
        {
            display.SetupEntity(entityData, cardData);
            _spawnedEntities.Add(entityData.entityId, display);
        }
        else
        {
            Debug.Log("Card Data Null");
        }
    }

    private void UpdateEntity(EntityData entityData)
    {
        if (_spawnedEntities.TryGetValue(entityData.entityId, out GameCardDisplay display))
        {
            display.UpdateEntityStats(entityData);

            // 리더가 아닌 일반 하수인만 체력이 0 이하일 때 필드에서 제거
            if (entityData.health <= 0 && !entityData.isLeader)
            {
                RemoveEntity(entityData.entityId);
            }
        }
    }

    private void RemoveEntity(int entityId)
    {

        if (_spawnedEntities.TryGetValue(entityId, out GameCardDisplay display))
        {
            // [추가] 파괴되는 개체가 있었던 슬롯을 비워줍니다.
            EntityData data = display.CurrentEntityData;
            if (data != null)
            {
                bool isMine = (data.ownerUid == MyUid);
                FieldSlot[] targetSlots = data.isMember ?
                                          (isMine ? myMemberSlots : opponentMemberSlots) :
                                          (isMine ? myFieldSlots : opponentFieldSlots);

                if (targetSlots != null && data.position < targetSlots.Length)
                {
                    targetSlots[data.position].IsOccupied = false;
                    targetSlots[data.position].cardData = null;
                }
            }

            _spawnedEntities.Remove(entityId);
            _pendingDeadEntityIds.Remove(entityId);
            StartCoroutine(DestroyRoutine(display));
        }
    }

    private IEnumerator DestroyRoutine(GameCardDisplay display)
    {
        // 사망 연출 대기
        yield return YieldInstructionCache.WaitForSeconds(0.5f);
        Destroy(display.gameObject);
    }

    // ==================================================================
    // 3. 전투 연출 (투사체 기반 턴제 교전)
    // ==================================================================

    public void TestAttack(GameCardDisplay attacker, GameCardDisplay target)
    {
        Debug.Log("테스트 공격 시작");
        StartCoroutine(AttackRoutine(attacker, target));
    }

    public void PerformAttack(int attackerId, int targetId)
    {
        if (_spawnedEntities.TryGetValue(attackerId, out var attacker) &&
            _spawnedEntities.TryGetValue(targetId, out var target))
        {
            StartCoroutine(AttackRoutine(attacker, target));
        }
    }

    private IEnumerator AttackRoutine(GameCardDisplay attacker, GameCardDisplay target)
    {
        if (attacker == null || target == null) yield break;

        int attackerId = attacker.CurrentEntityData?.entityId ?? 0;
        int targetId = target.CurrentEntityData?.entityId ?? 0;

        // [연출 1] 공격자의 선공 투사체 발사!
        yield return StartCoroutine(PlayActionProjectileRoutine(attackerId, targetId, EffectTriggerType.NONE));

        // [연출 2] 수비자의 반격 투사체 발사!
        bool canCounterAttack = target.CurrentEntityData != null && target.CurrentEntityData.attack > 0;
        if (canCounterAttack)
        {
            yield return StartCoroutine(PlayActionProjectileRoutine(targetId, attackerId, EffectTriggerType.NONE));
        }
    }

    /// <summary>
    /// 지정된 투사체 프리팹을 시작 위치(startPos)에서 목표 위치(targetPos)로 발사합니다.
    /// </summary>
    public void FireProjectileDirect(GameObject prefabToUse, Vector3 startPos, Vector3 targetPos, Action onHitCallback, float? customDamageDelay = null)
    {
        if (prefabToUse != null)
        {
            GameObject projObj = Instantiate(prefabToUse, startPos, prefabToUse.transform.rotation);
            ProjectileController projectile = projObj.GetComponent<ProjectileController>();

            if (projectile != null)
            {
                if (customDamageDelay.HasValue)
                {
                    projectile.damageDelay = customDamageDelay.Value;
                }
                projectile.Fire(startPos, targetPos, onHitCallback);
                return;
            }
        }

        // 투사체 프리팹이 없거나 컨트롤러가 없으면 즉시 콜백 실행
        onHitCallback?.Invoke();
    }
}