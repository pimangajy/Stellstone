using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using UnityEditor;

/// <summary>
/// 필드 위에 나와있는 하수인이나 영웅(Entity)들을 관리하는 '현장 감독'입니다.
/// 서버에서 "누가 소환됐다", "누가 다쳤다"고 하면 실제로 화면에 보여주는 역할을 합니다.
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
    // 3. 전투 연출
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
        Vector3 originalPos = attacker.transform.position;
        Vector3 targetPos = target.transform.position;

        float moveTime = 0.2f;
        float elapsedTime = 0f;
        while (elapsedTime < moveTime)
        {
            attacker.transform.position = Vector3.Lerp(originalPos, targetPos, (elapsedTime / moveTime));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"쾅! {attacker.EntityId} -> {target.EntityId}");

        elapsedTime = 0f;
        while (elapsedTime < moveTime)
        {
            attacker.transform.position = Vector3.Lerp(targetPos, originalPos, (elapsedTime / moveTime));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        attacker.transform.position = originalPos;
    }
}