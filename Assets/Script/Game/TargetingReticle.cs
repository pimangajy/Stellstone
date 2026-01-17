using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// (수정) LineRenderer 대신 3D 오브젝트들이 포물선을 그리며 날아가는 애니메이션을 구현합니다.
/// 2차 베지어 곡선(Quadratic Bezier Curve)을 사용합니다.
/// </summary>
public class TargetingReticle : MonoBehaviour
{
    public static TargetingReticle Instance { get; private set; }

    [Header("설정")]
    [Tooltip("조준선 경로를 따라 날아갈 작은 3D 오브젝트 프리팹")]
    public GameObject pathObjectPrefab;

    [Tooltip("생성해둘 오브젝트 최대 개수 (경로가 길어질 것을 대비해 넉넉하게 50~100개 추천)")]
    public int maxPoolSize = 50;

    [Tooltip("오브젝트 간의 간격 (이 값이 작을수록 촘촘해짐)")]
    public float objectSpacing = 0.5f;

    [Tooltip("오브젝트들이 날아가는 속도")]
    public float animationSpeed = 5.0f; // 속도 단위를 거리(m/s) 기준으로 변경하여 좀 더 빠르게 설정 필요할 수 있음

    [Tooltip("포물선의 높이")]
    public float arcHeight = 2.0f;

    [Tooltip("도착 지점을 바닥에서 얼마나 띄울지")]
    public float targetHeightOffset = 0.0f;

    [Tooltip("화살표의 촉 부분")]
    public Transform arrowHead;

    [Tooltip("화살촉의 회전 오프셋 (모델의 축이 다를 경우 보정, 예: (90, 0, 0))")]
    public Vector3 arrowHeadRotationOffset;

    [Header("레이어")]
    public LayerMask playfieldPlaneLayer;

    // 내부 변수
    private Transform _startTransform;
    private Camera _mainCamera;
    private bool _isTargeting = false;
    private List<GameObject> _pooledPathObjects = new List<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _mainCamera = Camera.main;
        CreateObjectPool();
        gameObject.SetActive(false);
    }

    private void CreateObjectPool()
    {
        if (pathObjectPrefab == null) return;

        // 기존 풀 정리 (혹시 모를 중복 방지)
        foreach (var obj in _pooledPathObjects) if (obj) Destroy(obj);
        _pooledPathObjects.Clear();

        for (int i = 0; i < maxPoolSize; i++)
        {
            GameObject obj = Instantiate(pathObjectPrefab, transform);
            obj.SetActive(false);
            _pooledPathObjects.Add(obj);
        }
    }

    public void StartTargeting(Transform start)
    {
        _startTransform = start;
        _isTargeting = true;
        gameObject.SetActive(true);
        if (arrowHead != null) arrowHead.gameObject.SetActive(true);
    }

    public void StopTargeting()
    {
        _isTargeting = false;
        _startTransform = null;

        // 모든 오브젝트 비활성화
        foreach (var obj in _pooledPathObjects)
        {
            obj.SetActive(false);
        }
        if (arrowHead != null) arrowHead.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    public GameObject GetCurrentTarget(LayerMask targetLayer)
    {
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, targetLayer))
        {
            return hit.collider.gameObject;
        }
        return null;
    }

    void Update()
    {
        if (!_isTargeting || _startTransform == null) return;

        UpdateArcAnimation();
    }

    private void UpdateArcAnimation()
    {
        // 1. P0, P2, P1 계산
        Vector3 p0 = _startTransform.position;
        Vector3 p2;

        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, playfieldPlaneLayer))
        {
            p2 = hit.point + Vector3.up * targetHeightOffset;
        }
        else
        {
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            float enter;
            if (groundPlane.Raycast(ray, out enter))
            {
                p2 = ray.GetPoint(enter) + Vector3.up * targetHeightOffset;
            }
            else
            {
                p2 = ray.GetPoint(10f);
                p2.y = targetHeightOffset;
            }
        }

        Vector3 midPoint = (p0 + p2) / 2f;
        float directDist = Vector3.Distance(p0, p2);
        Vector3 p1 = midPoint + Vector3.up * (arcHeight + (directDist * 0.1f));

        // 2. 화살촉 배치
        if (arrowHead != null)
        {
            arrowHead.position = p2;
            Vector3 preEndPos = CalculateBezierPoint(0.99f, p0, p1, p2);
            Vector3 direction = (p2 - preEndPos).normalized;
            if (direction != Vector3.zero)
            {
                // LookRotation으로 진행 방향을 먼저 바라보게 한 뒤, 오프셋만큼 추가 회전
                arrowHead.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(arrowHeadRotationOffset);
            }
        }

        // 3. 곡선 길이 근사 계산 (샘플링)
        float curveLength = EstimateCurveLength(p0, p1, p2, 30);
        if (curveLength <= 0.001f) return;

        // 4. 거리 기반으로 오브젝트 배치 (Distance Walking)
        // Time.time * speed 를 통해 전체 텍스처를 민다고 생각하면 됩니다.
        // % spacing을 통해 0 ~ spacing 사이에서 시작 위치를 반복시킵니다.
        float currentDist = (Time.time * animationSpeed) % objectSpacing;

        int activeCount = 0;

        while (currentDist < curveLength)
        {
            // 풀 크기를 초과하면 중단
            if (activeCount >= _pooledPathObjects.Count) break;

            // 현재 거리를 0~1 사이의 t 값으로 변환
            float t = currentDist / curveLength;

            Vector3 position = CalculateBezierPoint(t, p0, p1, p2);
            GameObject obj = _pooledPathObjects[activeCount];

            if (!obj.activeSelf) obj.SetActive(true);
            obj.transform.position = position;

            // 회전
            Vector3 nextPos = CalculateBezierPoint(Mathf.Min(t + 0.01f, 1.0f), p0, p1, p2);
            Vector3 dir = (nextPos - position).normalized;
            if (dir != Vector3.zero)
            {
                obj.transform.rotation = Quaternion.LookRotation(dir);
            }

            // 스케일 효과 (선택): 시작과 끝에서 부드럽게 나타나고 사라짐
            // 여기서는 끝부분(t > 0.9)에서만 작아지게 설정 예시
            float scale = 1.0f;
            if (t > 0.9f) scale = (1.0f - t) * 10f; // 0.9~1.0 구간에서 1 -> 0으로 줄어듦
            else if (t < 0.1f) scale = t * 10f;     // 0.0~0.1 구간에서 0 -> 1로 커짐

            obj.transform.localScale = Vector3.one * Mathf.Clamp01(scale);

            // 다음 오브젝트 위치로 이동
            currentDist += objectSpacing;
            activeCount++;
        }

        // 5. 쓰지 않는 나머지 오브젝트 비활성화
        for (int i = activeCount; i < _pooledPathObjects.Count; i++)
        {
            if (_pooledPathObjects[i].activeSelf)
                _pooledPathObjects[i].SetActive(false);
        }
    }

    private Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        return (uu * p0) + (2 * u * t * p1) + (tt * p2);
    }

    // 곡선의 길이를 샘플링을 통해 근사 계산
    private float EstimateCurveLength(Vector3 p0, Vector3 p1, Vector3 p2, int segments)
    {
        float length = 0f;
        Vector3 previousPos = p0;

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 currentPos = CalculateBezierPoint(t, p0, p1, p2);
            length += Vector3.Distance(previousPos, currentPos);
            previousPos = currentPos;
        }
        return length;
    }
}