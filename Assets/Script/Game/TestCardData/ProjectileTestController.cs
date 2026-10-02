using UnityEngine;
using System.Collections;

/// <summary>
/// 투사체(Projectile)의 비행 궤적 및 목표 지점 폭발 연출을
/// 독립된 테스트 씬에서 키보드 단축키로 쉽게 테스트할 수 있는 컨트롤러입니다.
/// </summary>
public class ProjectileTestController : MonoBehaviour
{
    [Header("발사자 & 타겟 위치")]
    [Tooltip("투사체가 출발할 위치 (비워두면 자동 생성)")]
    public Transform shooterTransform;

    [Tooltip("투사체가 도달할 목표 위치 (비워두면 자동 생성)")]
    public Transform targetTransform;

    [Header("테스트할 투사체 프리팹")]
    [Tooltip("[1]번 키 / [Space]: 기본 투사체")]
    public GameObject normalProjectilePrefab;

    [Tooltip("[2]번 키: 6이상 강화 투사체")]
    public GameObject heavyProjectilePrefab;

    [Tooltip("[3]번 키: 주문/마법진 투사체")]
    public GameObject spellProjectilePrefab;

    [Header("설정")]
    [Tooltip("연속 발사 최소 간격(초)")]
    public float fireCooldown = 0.2f;

    private float _lastFireTime = -1f;
    private string _lastStatusMessage = "키보드를 눌러 투사체를 발사하세요.";
    private int _hitCount = 0;

    private void Start()
    {
        // 1. 발사자 큐브가 할당되지 않았다면 자동으로 생성
        if (shooterTransform == null)
        {
            GameObject shooterObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shooterObj.name = "[Test] Shooter_Cube (Blue)";
            shooterObj.transform.position = new Vector3(-4f, 0.5f, 0f);
            
            var renderer = shooterObj.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(0.2f, 0.5f, 1f); // 파란색

            shooterTransform = shooterObj.transform;
        }

        // 2. 타겟 큐브가 할당되지 않았다면 자동으로 생성
        if (targetTransform == null)
        {
            GameObject targetObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObj.name = "[Test] Target_Cube (Red)";
            targetObj.transform.position = new Vector3(4f, 0.5f, 0f);
            
            var renderer = targetObj.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(1f, 0.3f, 0.3f); // 빨간색

            targetTransform = targetObj.transform;
        }
    }

    private void Update()
    {
        // 쿨타임 체크
        if (Time.time < _lastFireTime + fireCooldown) return;

        // [Space] 또는 [1]: 기본 투사체
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            FireTest(normalProjectilePrefab, "기본 투사체 (공격력 1~5)");
        }
        // [2]: 강화 투사체
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            FireTest(heavyProjectilePrefab, "강화 투사체 (공격력 6+)");
        }
        // [3]: 주문 투사체 / 마법진
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            FireTest(spellProjectilePrefab, "주문 / 마법진 투사체");
        }
        // [R]: 발사자 <-> 타겟 위치 교체
        else if (Input.GetKeyDown(KeyCode.R))
        {
            SwapPositions();
        }
    }

    /// <summary>
    /// 지정된 투사체 프리팹을 발사자에서 타겟으로 날립니다.
    /// </summary>
    public void FireTest(GameObject prefab, string projectileName)
    {
        if (prefab == null)
        {
            _lastStatusMessage = $"⚠️ [{projectileName}] 프리팹이 인스펙터에 등록되지 않았습니다!";
            Debug.LogWarning(_lastStatusMessage);
            return;
        }

        if (shooterTransform == null || targetTransform == null)
        {
            _lastStatusMessage = "⚠️ 발사자 또는 타겟 위치가 유효하지 않습니다.";
            Debug.LogWarning(_lastStatusMessage);
            return;
        }

        _lastFireTime = Time.time;
        Vector3 startPos = shooterTransform.position;
        Vector3 targetPos = targetTransform.position;

        // 투사체 인스턴스화
        GameObject projObj = Instantiate(prefab, startPos, prefab.transform.rotation);
        ProjectileController controller = projObj.GetComponent<ProjectileController>();

        if (controller != null)
        {
            _lastStatusMessage = $"🚀 [{projectileName}] 발사 중...";

            controller.Fire(startPos, targetPos, () =>
            {
                _hitCount++;
                _lastStatusMessage = $"💥 [{projectileName}] 목표 지점 적중! (총 적중: {_hitCount}회)";
                Debug.Log(_lastStatusMessage);

                // 타겟 큐브 튕기는 연출
                StartCoroutine(TargetHitPunchRoutine(targetTransform));
            });
        }
        else
        {
            _lastStatusMessage = $"⚠️ '{prefab.name}'에 ProjectileController 컴포넌트가 없습니다!";
            Debug.LogError(_lastStatusMessage);
            Destroy(projObj);
        }
    }

    /// <summary>
    /// 발사자 큐브와 타겟 큐브의 위치를 맞바꿉니다.
    /// </summary>
    private void SwapPositions()
    {
        if (shooterTransform != null && targetTransform != null)
        {
            Vector3 tempPos = shooterTransform.position;
            shooterTransform.position = targetTransform.position;
            targetTransform.position = tempPos;

            _lastStatusMessage = "🔄 발사자와 타겟의 위치를 서로 맞바꾸었습니다.";
            Debug.Log(_lastStatusMessage);
        }
    }

    /// <summary>
    /// 피격 시 타겟 큐브가 살짝 흔들리는 시각적 피드백
    /// </summary>
    private IEnumerator TargetHitPunchRoutine(Transform target)
    {
        if (target == null) yield break;

        Vector3 originScale = target.localScale;
        target.localScale = originScale * 1.25f;

        float elapsed = 0f;
        float duration = 0.15f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(originScale * 1.25f, originScale, elapsed / duration);
            yield return null;
        }

        target.localScale = originScale;
    }

    private void OnGUI()
    {
        // 좌측 상단 디버그 안내 UI
        GUI.Box(new Rect(15, 15, 360, 185), "🎯 투사체(Projectile) 테스트 컨트롤러");

        GUI.Label(new Rect(25, 45, 340, 20), "• [1] 또는 [Space] : 기본 투사체 발사");
        GUI.Label(new Rect(25, 65, 340, 20), "• [2] : 강화 투사체(공격력 6+) 발사");
        GUI.Label(new Rect(25, 85, 340, 20), "• [3] : 주문 / 마법진 투사체 발사");
        GUI.Label(new Rect(25, 105, 340, 20), "• [R] : 발사자 <-> 타겟 위치 교체");

        GUI.Label(new Rect(25, 135, 340, 45), $"상태: {_lastStatusMessage}");
    }

    private void OnDrawGizmos()
    {
        if (shooterTransform != null && targetTransform != null)
        {
            // 발사자 (파란 원)
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(shooterTransform.position, 0.4f);

            // 타겟 (빨간 원)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetTransform.position, 0.4f);

            // 비행 궤적 가이드 라인 (노란 실선)
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(shooterTransform.position, targetTransform.position);
        }
    }
}
