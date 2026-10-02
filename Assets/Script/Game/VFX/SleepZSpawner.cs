using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 수면(ZZZ) 연출의 스포너 및 오브젝트 풀러입니다.
/// SleepZZZ 오브젝트에 부착되어, 오브젝트가 활성화되면 Z 파티클을 주기적으로 생성하고
/// 비활성화되면 모든 파티클을 풀로 즉시 회수합니다.
/// </summary>
public class SleepZSpawner : MonoBehaviour
{
    [Header("1. 스폰 및 프리팹 설정")]
    [Tooltip("파티클이 스폰될 기준 위치 (ZSpawner 오브젝트)")]
    public Transform spawnPoint;

    [Tooltip("스폰할 3D Z 파티클 템플릿 (Z_Prefab)")]
    public GameObject zPrefab;

    [Tooltip("초기 풀 크기")]
    [SerializeField] private int initialPoolSize = 6;

    [Tooltip("파티클 생성 간격 (초)")]
    [Range(0.2f, 2.0f)]
    public float spawnInterval = 0.7f;

    [Header("2. 이동 및 방향 설정")]
    [Tooltip("파티클이 날아갈 3D 방향 벡터 (자유롭게 조절 가능)")]
    public Vector3 moveDirection = new Vector3(0.5f, 1.2f, -0.3f);

    [Tooltip("파티클이 이동할 총 거리")]
    public float moveDistance = 1.2f;

    [Tooltip("파티클 이동에 걸리는 시간 (초)")]
    public float moveDuration = 1.2f;

    [Header("3. 크기 연출 설정")]
    [Tooltip("생성 시 초기 크기")]
    public Vector3 startScale = Vector3.one * 0.15f;

    [Tooltip("날아가며 커지는 최종 크기")]
    public Vector3 endScale = Vector3.one * 0.4f;

    private readonly Queue<ZAnimationParticle> _pool = new Queue<ZAnimationParticle>();
    private readonly List<ZAnimationParticle> _activeParticles = new List<ZAnimationParticle>();
    private Coroutine _spawnCoroutine;

    private void Awake()
    {
        if (spawnPoint == null)
        {
            Transform found = transform.Find("ZSpawner");
            spawnPoint = found != null ? found : transform;
        }

        if (zPrefab == null)
        {
            Transform foundPrefab = transform.Find("Z_Prefab");
            if (foundPrefab != null) zPrefab = foundPrefab.gameObject;
        }

        InitPool();
    }

    private void InitPool()
    {
        if (zPrefab == null) return;

        // 템플릿 원본 오브젝트는 비활성화 유지
        zPrefab.SetActive(false);

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledParticle();
        }
    }

    private ZAnimationParticle CreatePooledParticle()
    {
        if (zPrefab == null) return null;

        GameObject instance = Instantiate(zPrefab, transform);
        instance.name = "Z_Particle_Pooled";
        instance.SetActive(false);

        var particle = instance.GetComponent<ZAnimationParticle>();
        if (particle == null)
        {
            particle = instance.AddComponent<ZAnimationParticle>();
        }

        _pool.Enqueue(particle);
        return particle;
    }

    private void OnEnable()
    {
        if (_spawnCoroutine != null) StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = StartCoroutine(SpawnRoutine());
    }

    private void OnDisable()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }

        RecycleAllActiveParticles();
    }

    private IEnumerator SpawnRoutine()
    {
        // 켜지자마자 첫 번째 파티클 즉시 1개 생성
        SpawnParticle();

        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            SpawnParticle();
        }
    }

    private void SpawnParticle()
    {
        if (zPrefab == null) return;

        ZAnimationParticle particle = GetParticle();
        if (particle == null) return;

        _activeParticles.Add(particle);

        Vector3 startPos = spawnPoint != null ? spawnPoint.localPosition : Vector3.zero;
        Vector3 dir = moveDirection.normalized;
        Vector3 targetPos = startPos + (dir * moveDistance);

        particle.PlayAnimation(
            startPos,
            targetPos,
            moveDuration,
            startScale,
            endScale,
            OnParticleFinished
        );
    }

    private ZAnimationParticle GetParticle()
    {
        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }

        return CreatePooledParticle();
    }

    private void OnParticleFinished(ZAnimationParticle particle)
    {
        if (particle == null) return;
        _activeParticles.Remove(particle);
        _pool.Enqueue(particle);
    }

    private void RecycleAllActiveParticles()
    {
        for (int i = _activeParticles.Count - 1; i >= 0; i--)
        {
            var p = _activeParticles[i];
            if (p != null)
            {
                p.StopAndReset();
                _pool.Enqueue(p);
            }
        }
        _activeParticles.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
        Vector3 dir = transform.TransformDirection(moveDirection.normalized);
        Vector3 destination = origin + (dir * moveDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(origin, 0.05f);
        Gizmos.DrawLine(origin, destination);
        Gizmos.DrawWireSphere(destination, 0.08f);
    }
}
