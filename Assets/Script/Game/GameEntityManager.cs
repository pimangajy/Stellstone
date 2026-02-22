using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

/// <summary>
/// 필드 위에 나와있는 하수인이나 영웅(Entity)들을 관리하는 '현장 감독'입니다.
/// [수정됨] 몸통 박치기를 완전히 제거하고, 모든 전투를 투사체(Projectile) 기반으로 변경했습니다.
/// 공격자가 먼저 발사하고, 약간의 딜레이 후 수비자가 반격합니다.
/// </summary>
public class GameEntityManager : MonoBehaviour
{
    public static GameEntityManager Instance { get; private set; }

    [Header("필드 위치")]
    [Tooltip("내 하수인들이 놓일 자리")]
    public Transform myFieldTransform;
    [Tooltip("상대 하수인들이 놓일 자리")]
    public Transform opponentFieldTransform;

    [Header("멤버 존 위치 (추가)")]
    public Transform myMemberTransform;
    public Transform opponentMemberTransform;

    [Header("프리팹")]
    public GameObject minionPrefab; // 하수인 모형

    private string myUid;

    // 소환된 녀석들을 관리하는 명부 (ID로 찾음)
    private Dictionary<int, GameCardDisplay> _spawnedEntities = new Dictionary<int, GameCardDisplay>();

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
            GameClient.Instance.OnEntitiesUpdatedEvent += HandleEntitiesUpdated;
        }
    }

    private void OnDisable()
    {
        // 2. 이벤트 구독 해제 (메모리 누수 방지)
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnEntitiesUpdatedEvent -= HandleEntitiesUpdated;
        }
    }

    // ==================================================================
    // 1. 이벤트 처리 및 판단
    // ==================================================================

    /// <summary>
    /// [핵심] 서버에서 개체 리스트를 보내주면 루프를 돌며 하나씩 처리합니다.
    /// </summary>
    private void HandleEntitiesUpdated(List<EntityData> updatedList)
    {
        Debug.Log("필드 상태가 변화되어 소환이나 갱신을 실행");
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
                    SpawnEntity(entityData, isMine);
                }
            }
        }
    }

    // ==================================================================
    // 2. 소환 및 갱신 로직
    // ==================================================================

    private void SpawnEntity(EntityData entityData, bool isMine)
    {
        if (_spawnedEntities.ContainsKey(entityData.entityId))
        {
            Debug.Log("필드에 이미 하수인이 있음");
            return;
        }

        CardData cardData = ResourceManager.Instance.GetCardData(entityData.cardId);
        if (cardData == null) return;

        // 1. 하수인이 놓일 '부모' 구역 결정 (필드 vs 멤버존)
        Transform zoneGroup;
        if (entityData.isMember)
            zoneGroup = isMine ? myMemberTransform : opponentMemberTransform;
        else
            zoneGroup = isMine ? myFieldTransform : opponentFieldTransform;

        // 2. [핵심] 부모 구역의 자식(슬롯)들 중에서 서버가 지정한 position 번호의 슬롯을 찾음
        Transform targetSlot = null;
        if (zoneGroup != null && zoneGroup.childCount > entityData.position)
        {
            targetSlot = zoneGroup.GetChild(entityData.position);
        }

        // 만약 슬롯을 못 찾았다면 부모 위치를 기본값으로 사용
        Transform finalParent = targetSlot != null ? targetSlot : zoneGroup;

        // 3. 생성 및 배치 (슬롯의 위치와 회전값에 맞춤)
        GameObject newObj = Instantiate(minionPrefab, finalParent.position, finalParent.rotation, finalParent);
        GameCardDisplay display = newObj.GetComponent<GameCardDisplay>();

        if (display != null)
        {
            Debug.Log("필드 카드에 값 주입");
            display.SetupEntity(entityData, cardData);
            _spawnedEntities.Add(entityData.entityId, display);
        }
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
        // [연출 1] 공격자의 선공 투사체 발사!
        bool attackerHit = false;
        FireProjectile(attacker, target, () => { attackerHit = true; });

        // [연출 2] 아주 약간의 딜레이 (선공과 반격의 리듬감을 만듦)
        // 공격자의 투사체가 날아가는 도중에 수비자가 반격하게 됩니다.
        yield return new WaitForSeconds(0.2f);

        // [연출 3] 수비자의 반격 투사체 발사!
        bool targetHit = false;

        // 수비자가 공격력이 0보다 큰 하수인일 때만 반격 투사체를 날립니다.
        bool canCounterAttack = target.CurrentEntityData != null && target.CurrentEntityData.attack > 0;

        if (canCounterAttack)
        {
            FireProjectile(target, attacker, () => { targetHit = true; });
        }
        else
        {
            // 공격력이 0이거나 반격할 수 없는 대상(예: 영웅 본체 등)이면 대기하지 않음
            targetHit = true;
        }

        // [연출 4] 양쪽의 투사체가 모두 상대방에게 적중할 때까지 대기
        yield return new WaitUntil(() => attackerHit && targetHit);

        Debug.Log($"[전투 완료] {attacker.EntityId}와 {target.EntityId}의 교전이 끝났습니다.");

        // TODO: 여기서 서버가 보내준 결과(남은 체력, 파괴 여부)를 화면(UI)에 반영하거나
        // 파괴(Death) 연출을 실행하면 완벽합니다.
    }

    /// <summary>
    /// 발사자(Shooter)의 데이터를 읽어 투사체를 생성하고 목표(Target)를 향해 날립니다.
    /// </summary>
    private void FireProjectile(GameCardDisplay shooter, GameCardDisplay target, Action onHitCallback)
    {
        CardData data = shooter._cardData;

        // CardData에 투사체 프리팹이 제대로 등록되어 있는지 확인
        if (data != null && data.projectilePrefab != null)
        {
            // 1. 투사체 생성
            GameObject projObj = Instantiate(data.projectilePrefab, shooter.transform.position, Quaternion.identity);
            ProjectileController projectile = projObj.GetComponent<ProjectileController>();

            if (projectile != null)
            {
                // 2. ProjectileController를 이용해 발사 (속도와 각도 적용)
                projectile.Fire(
                    shooter.transform.position,
                    target.transform.position,
                    data.projectileSpeed,
                    data.projectileArcHeight,
                    onHitCallback
                );
                return; // 정상 발사 성공
            }
        }

        // 만약 카드의 투사체 프리팹이 빠져있거나 에러가 났을 경우 (게임 멈춤 방지용 안전장치)
        Debug.LogWarning($"[경고] {shooter.name}의 CardData에 투사체 프리팹이 없습니다! 즉시 적중 처리합니다.");
        onHitCallback?.Invoke();
    }
}