using UnityEngine;
using System.Collections.Generic;
using System.Collections;

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

    [Header("프리팹")]
    public GameObject minionPrefab; // 하수인 모형 (붕어빵 틀)

    // 소환된 녀석들을 관리하는 명부 (ID로 찾음)
    private Dictionary<int, GameCardDisplay> _spawnedEntities = new Dictionary<int, GameCardDisplay>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ==================================================================
    // 1. 소환 및 관리
    // ==================================================================

    /// <summary>
    /// [소환] 새로운 하수인을 필드에 만듭니다.
    /// </summary>
    public void SpawnEntity(EntityData entityData, bool isMine)
    {
        // 이미 있는 녀석이면 무시
        if (_spawnedEntities.ContainsKey(entityData.entityId)) return;

        // 카드 정보를 가져옵니다 (이미지 등을 알기 위해)
        CardData cardData = ResourceManager.Instance.GetCardData(entityData.cardId);
        if (cardData == null)
        {
            Debug.LogError($"[GameEntityManager] 데이터 없음: {entityData.cardId}");
            return;
        }

        // 내 편이면 내 자리, 적이면 적 자리에 소환
        Transform spawnZone = isMine ? myFieldTransform : opponentFieldTransform;

        // 오브젝트 생성
        GameObject newObj = Instantiate(minionPrefab, spawnZone);
        GameCardDisplay display = newObj.GetComponent<GameCardDisplay>();

        if (display != null)
        {
            // 정보 입력 및 명부에 등록
            display.SetupEntity(entityData, cardData);
            _spawnedEntities.Add(entityData.entityId, display);
        }
    }

    /// <summary>
    /// [갱신] 이미 나와있는 하수인의 체력/공격력을 바꿉니다.
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
    }

    /// <summary>
    /// [통합 관리] 서버 데이터를 보고 소환할지, 갱신할지 알아서 결정합니다.
    /// </summary>
    public void HandleEntityUpdate(EntityData entityData, bool isMine)
    {
        if (_spawnedEntities.ContainsKey(entityData.entityId))
        {
            // 이미 있으면 정보 갱신
            UpdateEntity(entityData);
        }
        else
        {
            // 없는데 살아있는 녀석이면 -> 새로 소환!
            if (entityData.health > 0)
            {
                SpawnEntity(entityData, isMine);
            }
        }
    }

    /// <summary>
    /// [사망] 하수인을 필드에서 제거합니다.
    /// </summary>
    public void RemoveEntity(int entityId)
    {
        if (_spawnedEntities.TryGetValue(entityId, out GameCardDisplay display))
        {
            _spawnedEntities.Remove(entityId); // 명부에서 삭제

            // 바로 지우지 않고 사망 연출을 위해 코루틴 사용
            StartCoroutine(DestroyRoutine(display));
        }
    }

    private IEnumerator DestroyRoutine(GameCardDisplay display)
    {
        // TODO: 사망 애니메이션 재생 (display.PlayDeathAnimation())
        yield return new WaitForSeconds(0.5f); // 0.5초 동안 비명 지르는 시간 등
        Destroy(display.gameObject); // 진짜 삭제
    }

    // ==================================================================
    // 2. 전투 연출
    // ==================================================================

    /// <summary>
    /// [공격] A가 B를 때리는 연출을 시킵니다.
    /// </summary>
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
        // 간단한 쿵! 박치기 연출
        Vector3 originalPos = attacker.transform.position;
        Vector3 targetPos = target.transform.position;

        // 1. 돌진
        float moveTime = 0.2f;
        float elapsedTime = 0f;
        while (elapsedTime < moveTime)
        {
            attacker.transform.position = Vector3.Lerp(originalPos, targetPos, (elapsedTime / moveTime));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 2. 타격
        Debug.Log($" 쾅! {attacker.EntityId} -> {target.EntityId}");

        // 3. 복귀
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