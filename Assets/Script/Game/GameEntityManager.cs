using UnityEngine;
using System.Collections.Generic;
using System.Collections; // Coroutine 사용

/// <summary>
/// 필드 위에 존재하는 모든 하수인과 영웅(Entity)의 생성, 관리, 연출을 담당합니다.
/// GameClient는 데이터 통신만 하고, 시각적인 처리는 이 클래스에게 위임합니다.
/// </summary>
public class GameEntityManager : MonoBehaviour
{
    public static GameEntityManager Instance { get; private set; }

    [Header("연결")]
    [Tooltip("내 필드 (하수인이 소환될 부모 Transform)")]
    public Transform myFieldTransform;
    [Tooltip("상대 필드")]
    public Transform opponentFieldTransform;

    [Header("프리팹")]
    public GameObject minionPrefab;
    // public GameObject heroPrefab; // 나중에 영웅 프리팹도 분리하면 사용

    // 필드에 소환된 모든 개체 관리 (Key: EntityId)
    private Dictionary<int, GameCardDisplay> _spawnedEntities = new Dictionary<int, GameCardDisplay>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        
    }

    // ==================================================================
    // 1. 개체 생성 및 관리
    // ==================================================================

    /// <summary>
    /// 새로운 개체를 필드에 소환합니다. (GameClient에서 호출)
    /// </summary>
    public void SpawnEntity(EntityData entityData, bool isMine)
    {
        // 1. 중복 소환 방지
        if (_spawnedEntities.ContainsKey(entityData.entityId)) return;

        // 2. 카드 데이터 로드
        CardData cardData = ResourceManager.Instance.GetCardData(entityData.cardId);
        if (cardData == null)
        {
            Debug.LogError($"[GameEntityManager] 데이터 없음: {entityData.cardId}");
            return;
        }

        // 3. 위치 결정
        Transform spawnZone = isMine ? myFieldTransform : opponentFieldTransform;

        // 4. 생성 및 초기화
        GameObject newObj = Instantiate(minionPrefab, spawnZone);
        GameCardDisplay display = newObj.GetComponent<GameCardDisplay>();

        if (display != null)
        {
            display.SetupEntity(entityData, cardData);
            _spawnedEntities.Add(entityData.entityId, display);

            // TODO: 소환 이펙트 재생 (display.PlaySpawnEffect())
        }
    }

    /// <summary>
    /// 기존 개체의 상태를 갱신합니다. (체력, 공격력 등)
    /// </summary>
    public void UpdateEntity(EntityData entityData)
    {
        if (_spawnedEntities.TryGetValue(entityData.entityId, out GameCardDisplay display))
        {
            display.UpdateEntityStats(entityData);

            // 체력이 0 이하면 사망 처리
            if (entityData.health <= 0)
            {
                RemoveEntity(entityData.entityId);
            }
        }
        else
        {
            // 없는 개체에 대한 업데이트 요청이 오면 (혹시 모르니) 생성 시도
            // (서버 순서 문제로 업데이트가 먼저 올 수도 있음)
            // SpawnEntity(entityData, ...); // isMine 정보가 없어서 지금은 패스
        }
    }

    /// <summary>
    /// 서버에서 온 데이터를 바탕으로 생성할지, 갱신할지 스스로 판단하여 처리합니다.
    /// </summary>
    public void HandleEntityUpdate(EntityData entityData, bool isMine)
    {
        if (_spawnedEntities.ContainsKey(entityData.entityId))
        {
            // 이미 있으면 상태 갱신 (또는 사망 처리)
            UpdateEntity(entityData);
        }
        else
        {
            // 없는데 체력이 0보다 크면 -> 새로 소환!
            if (entityData.health > 0)
            {
                SpawnEntity(entityData, isMine);
            }
        }
    }

    /// <summary>
    /// 개체를 파괴합니다. (사망 연출 포함)
    /// </summary>
    public void RemoveEntity(int entityId)
    {
        if (_spawnedEntities.TryGetValue(entityId, out GameCardDisplay display))
        {
            _spawnedEntities.Remove(entityId);

            // 바로 삭제하지 않고 사망 애니메이션 후 삭제하도록 코루틴 사용 가능
            StartCoroutine(DestroyRoutine(display));
        }
    }

    private IEnumerator DestroyRoutine(GameCardDisplay display)
    {
        // TODO: display.PlayDeathAnimation();
        yield return new WaitForSeconds(0.5f); // 연출 시간 대기
        Destroy(display.gameObject);
    }

    // ==================================================================
    // 2. 전투 및 연출 (GameClient가 S_UpdateEntities 외의 별도 액션 수신 시 호출)
    // ==================================================================

    /// <summary>
    /// 공격 애니메이션을 실행합니다.
    /// </summary>
    public void PerformAttack(int attackerId, int targetId)
    {
        if (_spawnedEntities.TryGetValue(attackerId, out var attacker) &&
            _spawnedEntities.TryGetValue(targetId, out var target))
        {
            // 중앙 관리자가 "누가 누구를 때려라"라고 명령만 내림
            // 실제 움직임은 하수인 오브젝트(GameCardDisplay)가 스스로 수행
            StartCoroutine(AttackRoutine(attacker, target));
        }
    }

    private IEnumerator AttackRoutine(GameCardDisplay attacker, GameCardDisplay target)
    {
        // 1. 공격 준비 (살짝 뒤로 뺐다가)
        // 2. 돌진 (대상에게 빠르게 이동)
        // 3. 타격 이펙트 (대상 위치에서)
        // 4. 복귀 (원래 자리로)

        // (간단 예시 - DOTween 사용 추천)
        Vector3 originalPos = attacker.transform.position;
        Vector3 targetPos = target.transform.position;

        // 돌진
        float moveTime = 0.2f;
        float elapsedTime = 0f;
        while (elapsedTime < moveTime)
        {
            attacker.transform.position = Vector3.Lerp(originalPos, targetPos, (elapsedTime / moveTime));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 타격! (여기서 소리나 이펙트)
        Debug.Log($" 쾅! {attacker.EntityId} -> {target.EntityId}");

        // 복귀
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