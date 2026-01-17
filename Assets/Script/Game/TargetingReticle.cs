using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 하수인이 대상을 공격하거나 주문을 쓸 때 나오는 '화살표(조준선)'를 그립니다.
/// 포물선(곡선) 형태로 예쁘게 날아가는 작은 물체들을 나열해서 표현합니다.
/// </summary>
public class TargetingReticle : MonoBehaviour
{
    public static TargetingReticle Instance { get; private set; }

    [Header("설정")]
    public GameObject pathObjectPrefab; // 경로를 이루는 점(작은 화살표 등)
    public int maxPoolSize = 50; // 미리 만들어둘 개수
    public float objectSpacing = 0.5f; // 점 사이 간격
    public float animationSpeed = 5.0f; // 점들이 흘러가는 속도
    public float arcHeight = 2.0f; // 포물선 높이
    public float targetHeightOffset = 0.0f; // 목표 지점 높이 보정
    public Transform arrowHead; // 맨 끝에 달릴 큰 화살촉
    public Vector3 arrowHeadRotationOffset; // 화살촉 각도 보정

    [Header("레이어")]
    public LayerMask playfieldPlaneLayer; // 바닥 인식용

    private Transform _startTransform; // 시작점 (내 하수인/카드)
    private Camera _mainCamera;
    private bool _isTargeting = false;
    private List<GameObject> _pooledPathObjects = new List<GameObject>(); // 오브젝트 풀

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        _mainCamera = Camera.main;
        CreateObjectPool(); // 풀링 초기화
        gameObject.SetActive(false);
    }

    // 미리 오브젝트들을 잔뜩 만들어둡니다. (성능 최적화)
    private void CreateObjectPool()
    {
        if (pathObjectPrefab == null) return;
        foreach (var obj in _pooledPathObjects) if (obj) Destroy(obj);
        _pooledPathObjects.Clear();

        for (int i = 0; i < maxPoolSize; i++)
        {
            GameObject obj = Instantiate(pathObjectPrefab, transform);
            obj.SetActive(false);
            _pooledPathObjects.Add(obj);
        }
    }

    // 조준 시작
    public void StartTargeting(Transform start)
    {
        _startTransform = start;
        _isTargeting = true;
        gameObject.SetActive(true);
        if (arrowHead != null) arrowHead.gameObject.SetActive(true);
    }

    // 조준 끝 (숨김)
    public void StopTargeting()
    {
        _isTargeting = false;
        _startTransform = null;

        foreach (var obj in _pooledPathObjects) obj.SetActive(false);
        if (arrowHead != null) arrowHead.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_isTargeting || _startTransform == null) return;
        UpdateArcAnimation();
    }

    // 매 프레임 포물선을 다시 그립니다.
    private void UpdateArcAnimation()
    {
        // 1. 시작점(P0)과 끝점(P2) 계산
        Vector3 p0 = _startTransform.position;
        Vector3 p2;

        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, playfieldPlaneLayer))
        {
            p2 = hit.point + Vector3.up * targetHeightOffset;
        }
        else
        {
            // 바닥이 없으면 허공으로
            p2 = ray.GetPoint(10f);
            p2.y = targetHeightOffset;
        }

        // 2. 중간점(P1) 계산 (포물선 꼭대기)
        Vector3 midPoint = (p0 + p2) / 2f;
        float directDist = Vector3.Distance(p0, p2);
        Vector3 p1 = midPoint + Vector3.up * (arcHeight + (directDist * 0.1f));

        // 3. 화살촉 배치
        if (arrowHead != null)
        {
            arrowHead.position = p2;
            // 끝부분의 방향(접선)을 구해서 회전시킴
            Vector3 preEndPos = CalculateBezierPoint(0.99f, p0, p1, p2);
            Vector3 direction = (p2 - preEndPos).normalized;
            if (direction != Vector3.zero)
            {
                arrowHead.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(arrowHeadRotationOffset);
            }
        }

        // 4. 경로를 따라 점들을 배치
        float curveLength = EstimateCurveLength(p0, p1, p2, 30);
        float currentDist = (Time.time * animationSpeed) % objectSpacing; // 흘러가는 효과

        int activeCount = 0;
        while (currentDist < curveLength)
        {
            if (activeCount >= _pooledPathObjects.Count) break;

            float t = currentDist / curveLength; // 0~1 사이 값
            Vector3 position = CalculateBezierPoint(t, p0, p1, p2);

            GameObject obj = _pooledPathObjects[activeCount];
            if (!obj.activeSelf) obj.SetActive(true);
            obj.transform.position = position;

            // 진행 방향 보기
            Vector3 nextPos = CalculateBezierPoint(Mathf.Min(t + 0.01f, 1.0f), p0, p1, p2);
            Vector3 dir = (nextPos - position).normalized;
            if (dir != Vector3.zero) obj.transform.rotation = Quaternion.LookRotation(dir);

            // 스케일 효과: 시작/끝에서 작아짐
            float scale = 1.0f;
            if (t > 0.9f) scale = (1.0f - t) * 10f;
            else if (t < 0.1f) scale = t * 10f;
            obj.transform.localScale = Vector3.one * Mathf.Clamp01(scale);

            currentDist += objectSpacing;
            activeCount++;
        }

        // 남은 오브젝트 끄기
        for (int i = activeCount; i < _pooledPathObjects.Count; i++)
        {
            if (_pooledPathObjects[i].activeSelf) _pooledPathObjects[i].SetActive(false);
        }
    }

    // 베지어 곡선 공식 (P0 -> P1 -> P2)
    private Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        return (u * u * p0) + (2 * u * t * p1) + (t * t * p2);
    }

    // 곡선 길이 대략 계산
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