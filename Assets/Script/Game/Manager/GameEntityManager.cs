using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using System.ComponentModel;
using Unity.VisualScripting;

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

    [Header("멤버 존 슬롯 (배열로 직접 할당)")]
    public FieldSlot[] myMemberSlots;
    public FieldSlot[] opponentMemberSlots;

    [Header("리더 보드")]
    public GameCardDisplay myLeader;
    public GameCardDisplay opponentLeader;

    [Header("프리팹")]
    public GameObject minionPrefab; // 하수인 모형

    private string myUid;

    // 소환된 녀석들을 관리하는 명부 (ID로 찾음)
    public Dictionary<int, GameCardDisplay> _spawnedEntities = new Dictionary<int, GameCardDisplay>();

    // [추가] 패킷을 순서대로 담아둘 큐와 실행 상태 플래그
    private Queue<S_ActionResolution> _actionQueue = new Queue<S_ActionResolution>();
    private bool _isProcessingAction = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        // 1. 서버 통신 이벤트 구독 시작
        if (GameClient.Instance != null)
        {
            myUid = GameClient.Instance.UserUid;
            test = false;
            // GameClient.Instance.OnEntitiesUpdatedEvent -= HandleEntitiesUpdated;
        }
    }

    private void OnDisable()
    {
        // 2. 이벤트 구독 해제 (메모리 누수 방지)
        if (GameClient.Instance != null)
        {
            // GameClient.Instance.OnEntitiesUpdatedEvent -= HandleEntitiesUpdated;
        }
    }

    public void SetReader(S_GameReady info)
    {
        myLeader.SetReader(info);
        opponentLeader.SetReader(info);
        _spawnedEntities.Add(info.myLeader.entityId, myLeader);
        _spawnedEntities.Add(info.enemyLeader.entityId, opponentLeader);
    }


    // ==================================================================
    // 1. 이벤트 처리 및 판단 (큐 시스템 적용)
    // ==================================================================

    // 기존 함수 수정: 패킷이 오면 바로 코루틴을 돌리지 않고 큐에 넣습니다.
    public void ResolveActionSequence(S_ActionResolution info)
    {
        _actionQueue.Enqueue(info);

        // 현재 실행 중인 연출이 없다면 큐 처리 시작
        if (!_isProcessingAction)
        {
            StartCoroutine(ProcessBufferedActionsRoutine());
        }
    }

    // [신규] 큐에 쌓인 패킷들을 모아서 하나로 병합 후 처리하는 코루틴
    private IEnumerator ProcessBufferedActionsRoutine()
    {
        _isProcessingAction = true;

        while (_actionQueue.Count > 0)
        {
            // 여러 패킷으로 쪼개져서 오는 경우를 대비해 아주 잠깐 대기하며 장바구니에 담습니다.
            yield return new WaitForSeconds(0.05f);

            List<GameEvent> mergedEventLog = new List<GameEvent>();
            List<EntityData> mergedFinalStates = new List<EntityData>();

            // 큐에 들어있는 모든 패킷을 꺼내서 하나의 리스트로 합칩니다.
            while (_actionQueue.Count > 0)
            {
                var packet = _actionQueue.Dequeue();
                if (packet.eventLog != null) mergedEventLog.AddRange(packet.eventLog);
                if (packet.finalStateUpdates != null) mergedFinalStates.AddRange(packet.finalStateUpdates);
            }

            // 모두 합쳐진 통합 데이터로 전투 연출을 실행하고 완전히 끝날 때까지 여기서 대기(병렬 실행 방지)
            yield return StartCoroutine(MergedActionSequenceRoutine(mergedEventLog, mergedFinalStates));
        }

        _isProcessingAction = false;
    }

    // [신규] 기존 ActionSequenceRoutine을 대체하는 병합 버전 코루틴
    private IEnumerator MergedActionSequenceRoutine(List<GameEvent> eventLog, List<EntityData> finalStateUpdates)
    {
        // 1. 서버가 보내준 eventLog를 순차적으로 실행
        for (int i = 0; i < eventLog.Count; i++)
        {
            var log = eventLog[i];

            Debug.Log($"[서버 이벤트 수신] EventType: {log.eventType}");

            switch (log.eventType)
            {
                case GameEventType.NONE:
                    // 처리할 내용 없음
                    break;

                case GameEventType.ATTACK:
                    yield return StartCoroutine(HandleAttack(log));
                    break;

                case GameEventType.DAMAGE:
                    yield return StartCoroutine(HandleDamage(log));
                    break;

                case GameEventType.HEAL:
                    yield return StartCoroutine(HandleHeal(log));
                    break;

                case GameEventType.BUFF:
                    yield return StartCoroutine(HandleBuff(log));
                    break;

                case GameEventType.DEATH:
                    yield return StartCoroutine(HandleDeath(log));
                    break;

                case GameEventType.EFFECT_TRIGGER:
                    yield return StartCoroutine(HandleEffectTrigger(log));
                    break;

                case GameEventType.SUMMON:
                    yield return StartCoroutine(HandleSummon(log));
                    break;

                case GameEventType.DRAW:
                    yield return StartCoroutine(HandleDraw(log));
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

                default:
                    Debug.LogWarning($"[MergedActionSequenceRoutine] 정의되지 않은 이벤트 타입입니다: {log.eventType}");
                    break;
            }
        }

        // 2. 모든 이벤트 연출이 완벽히 종료된 후 마지막으로 필드 상태 업데이트
        if (finalStateUpdates != null && finalStateUpdates.Count > 0)
        {
            yield return new WaitForSeconds(1.0f); // 모든 애니메이션이 끝나고 1초 대기
            HandleEntitiesUpdated(finalStateUpdates); // 죽은 유닛 파괴 및 스탯 최신화
        }
    }

    // ==================================================================
    // 각 이벤트 타입별 대응 함수 
    // ==================================================================

    /// <summary> 공격 선언 연출을 처리합니다. </summary>
    private IEnumerator HandleAttack(GameEvent log)
    {
        if (_spawnedEntities.TryGetValue(log.sourceEntityId, out var attacker) &&
        _spawnedEntities.TryGetValue(log.targetEntityId, out var target))
        {
            bool isHit = false;

            // 패킷에 담긴 triggerType을 그대로 전달 (일반 공격이면 NONE, 효과면 ON_PLAY 등)
            FireProjectile(attacker, target, log.triggerType, () => { isHit = true; });

            // 투사체가 명중할 때까지 대기
            yield return new WaitUntil(() => isHit);
        }
        else Debug.Log("Attack Error");
    }

    /// <summary> 데미지 발생 연출 (UI 표시, 피격 모션 등)을 처리합니다. </summary>
    private IEnumerator HandleDamage(GameEvent log)
    {
        if (_spawnedEntities.TryGetValue(log.targetEntityId, out var damagedEntity))
        {
            damagedEntity.DamageUI(log.value);
        }

        yield break;
    }

    /// <summary> 체력 회복 연출을 처리합니다. </summary>
    private IEnumerator HandleHeal(GameEvent log)
    {
        // TODO: 회복 이펙트 표시 및 스탯 갱신 연출 로직 작성
        yield break;
    }

    /// <summary> 스탯 버프 연출을 처리합니다. </summary>
    private IEnumerator HandleBuff(GameEvent log)
    {
        // TODO: 버프 이펙트 및 공/체 텍스트 초록색 변경 연출 로직 작성
        yield break;
    }

    /// <summary> 개체 사망 연출을 처리합니다. </summary>
    private IEnumerator HandleDeath(GameEvent log)
    {
        // TODO: 카드 파괴 이펙트 및 필드 이탈 대기 로직 작성
        yield break;
    }

    /// <summary> 전투의 함성, 죽음의 메아리 등 특수 효과 발동 연출을 처리합니다. </summary>
    private IEnumerator HandleEffectTrigger(GameEvent log)
    {
        // 효과를 발생시킨 개체(예: 전투의 함성을 쓴 하수인)를 찾음
        if (_spawnedEntities.TryGetValue(log.sourceEntityId, out var triggerEntity))
        {
            // 카드 디스플레이에게 연출 재생 명령을 내림
            triggerEntity.PlayTriggerAnimation(log.triggerType);

            // 연출이 끝날 때까지 대기 (이펙트 길이에 따라 유동적으로 조절 가능)
            yield return new WaitForSeconds(1.0f);
        }
        else
        {
            yield break;
        }
    }

    /// <summary> 하수인 소환 연출을 처리합니다. </summary>
    private IEnumerator HandleSummon(GameEvent log)
    {
        if (log.entityData != null)
        {
            // 상대 카드라면 보여주는 연출 내 카드라면 SpawnCard 실행
            CardActionQueueManager.Instance.ResolvePlay(log.entityData);
            HandInteractionManager.instance.AlignHand();
        }
        else Debug.Log("Summon Error");
        yield break;
    }

    /// <summary> 카드 드로우 연출을 처리합니다. </summary>
    private IEnumerator HandleDraw(GameEvent log)
    {
        // TODO: 덱에서 카드가 뽑히는 DOTween 애니메이션 대기 로직 작성
        yield break;
    }

    /// <summary> 속박 (빙결) 부여 연출을 처리합니다. </summary>
    private IEnumerator HandleBind(GameEvent log)
    {
        // TODO: 얼어붙는 이펙트 표시 로직 작성
        yield break;
    }

    /// <summary> 침묵 부여 연출을 처리합니다. </summary>
    private IEnumerator HandleSilence(GameEvent log)
    {
        // TODO: 침묵 이펙트 표시 및 버프 아이콘 제거 로직 작성
        yield break;
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
        // TODO: 키워드 획득 이펙트 표시 로직 작성
        yield break;
    }

    /// <summary> 마나 조작(펌핑 또는 파괴) 연출을 처리합니다. </summary>
    private IEnumerator HandleManaMod(GameEvent log)
    {
        // TODO: 마나 수정이 추가되거나 깨지는 UI 이펙트 로직 작성
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
            bool isMine = (entityData.ownerUid == myUid);

            // 이미 있는 녀석인가?
            if (_spawnedEntities.ContainsKey(entityData.entityId))
            {
                UpdateEntity(entityData);
            }
            else
            {
                // 없는데 살아있다면 새로 소환!
                if (entityData.health > 0)
                {
                    StartCoroutine(SpawnEntity(entityData, isMine));
                }
            }
        }
    }

    public void SpawnCard(EntityData entityData)
    {
        bool isMine = entityData.ownerUid == myUid;
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
            Debug.Log("필드에 이미 하수인이 있음");
            yield break;
        }

        CardData cardData = ResourceManager.Instance.GetCardData(entityData.cardId);
        if (cardData == null) yield break;

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

        // 5. 스폰 이팩트 실행동안 비활성화
        newObj.SetActive(false);

        if (display != null)
        {
            display.SetupEntity(entityData, cardData);
            _spawnedEntities.Add(entityData.entityId, display);
            yield return new WaitForSeconds(cardData.spawnEffectData.duration);
            newObj.SetActive(true);
        }
        else
            Debug.Log("Card Data Null");
    }

    private void UpdateEntity(EntityData entityData)
    {
        if (_spawnedEntities.TryGetValue(entityData.entityId, out GameCardDisplay display))
        {
            display.UpdateEntityStats(entityData);

            if (entityData.health <= 0)
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
                bool isMine = (data.ownerUid == myUid);
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
            StartCoroutine(DestroyRoutine(display));
        }
    }

    private IEnumerator DestroyRoutine(GameCardDisplay display)
    {
        // 사망 연출 대기
        yield return new WaitForSeconds(0.5f);
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
        // [연출 1] 공격자의 선공 투사체 발사! (일반 공격이므로 NONE 고정)
        bool attackerHit = false;
        FireProjectile(attacker, target, EffectTriggerType.NONE, () => { attackerHit = true; });

        yield return new WaitUntil(() => attackerHit);

        // [연출 3] 수비자의 반격 투사체 발사!
        bool targetHit = false;
        bool canCounterAttack = target.CurrentEntityData != null && target.CurrentEntityData.attack > 0;

        if (canCounterAttack)
        {
            // 수비자의 반격도 일반 공격이므로 NONE 고정
            FireProjectile(target, attacker, EffectTriggerType.NONE, () => { targetHit = true; });
        }
        else
        {
            targetHit = true;
        }

        yield return new WaitUntil(() => targetHit);
    }

    /// <summary>
    /// 발사자(Shooter)의 데이터를 읽어 투사체를 생성하고 목표(Target)를 향해 날립니다.
    /// </summary>
    private void FireProjectile(GameCardDisplay shooter, GameCardDisplay target, EffectTriggerType triggerType, Action onHitCallback)
    {
        CardData data = shooter._cardData;

        if (data != null)
        {
            // 1. 기본 투사체를 초기값으로 설정 (일반 공격용)
            GameObject prefabToUse = data.projectilePrefab;

            // 2. 일반 공격(NONE)이 아닐 경우, triggerVFXList에서 알맞은 이펙트 찾기
            if (triggerType != EffectTriggerType.NONE)
            {
                // 이전 대화에서 설계한 CardVFXData 리스트 활용
                CardVFXData vfxData = data.triggerVFXList.Find(x => x.triggerType == triggerType);

                // 해당 트리거에 맞는 데이터와 프리팹이 등록되어 있다면 특수 투사체로 덮어쓰기
                if (vfxData != null && vfxData.vfxPrefab != null)
                {
                    prefabToUse = vfxData.vfxPrefab;
                }
                else
                {
                    Debug.Log($"{shooter.name}의 {triggerType}에 등록된 특수 투사체가 없어 기본 투사체로 발사합니다.");
                }
            }

            if (prefabToUse != null)
            {
                // 3. 선택된 투사체 생성
                GameObject projObj = Instantiate(prefabToUse, shooter.transform.position, Quaternion.identity);
                ProjectileController projectile = projObj.GetComponent<ProjectileController>();

                if (projectile != null)
                {
                    // 4. 발사
                    projectile.Fire(
                        shooter.transform.position,
                        target.transform.position,
                        onHitCallback
                    );
                    return; // 정상 발사 성공
                }
            }
            else
                Debug.Log("투사체 프리팹이 비어있음");
        }

        // 투사체 프리팹이 아예 없거나 에러가 났을 경우 (게임 멈춤 방지용 안전장치)
        Debug.LogWarning($"[경고] {shooter.name}의 투사체 프리팹이 없거나 ProjectileController가 없습니다! 즉시 적중 처리합니다.");
        onHitCallback?.Invoke();
    }
}