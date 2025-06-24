using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AimingManager : MonoBehaviour
{
    #region Singleton
    public static AimingManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            InitializePools(); // 싱글톤이 설정된 후 풀을 초기화합니다.
        }
    }
    #endregion

    [Header("커서 설정")]
    [Tooltip("생성할 커서의 프리팹입니다.")]
    public GameObject cursorPrefab;
    [Tooltip("커서가 보드 위를 떠다닐 높이입니다.")]
    public float cursorFloatHeight = 0.1f;
    [Tooltip("커서의 움직임 속도입니다.")]
    public float cursorMoveSpeed = 20f;
    [Tooltip("조준 대상이 될 수 있는 바닥의 레이어입니다.")]
    public LayerMask boardLayer;

    [Header("포물선 조준선 설정")]
    [Tooltip("조준선 입자로 사용할 프리팹입니다.")]
    public GameObject aimingDotPrefab;
    [Tooltip("조준선을 구성할 입자의 개수입니다. (풀링 개수)")]
    public int aimingDotCount = 20;
    [Tooltip("포물선 궤도의 정점 높이입니다.")]
    public float aimingCurveHeight = 2.0f;
    [Tooltip("입자가 궤도를 따라 흐르는 속도입니다.")]
    public float aimingDotSpeed = 1.5f;
    [Tooltip("입자의 진행도에 따른 투명도/크기 변화 곡선입니다.")]
    public AnimationCurve dotCurve;

    // --- 상태 및 참조 변수 ---
    private bool isAiming = false;
    private GameObject startObject; // ★★★ 조준을 시작한 게임 오브젝트
    private RectTransform startRectTransform; // UI 오브젝트인지 확인하기 위한 참조
    private GameObject cursorInstance;

    // --- 오브젝트 풀링 변수 ---
    private List<GameObject> aimingDotPool = new List<GameObject>();
    private float[] aimingDotProgress;

    /// <summary>
    /// 게임 시작 시 커서와 조준선 입자들을 미리 생성하여 풀에 보관합니다.
    /// </summary>
    private void InitializePools()
    {
        if (cursorPrefab != null)
        {
            cursorInstance = Instantiate(cursorPrefab, Vector3.zero, Quaternion.identity);
            cursorInstance.SetActive(false);
        }

        if (aimingDotPrefab != null)
        {
            aimingDotProgress = new float[aimingDotCount];
            for (int i = 0; i < aimingDotCount; i++)
            {
                GameObject dot = Instantiate(aimingDotPrefab);
                dot.SetActive(false);
                aimingDotPool.Add(dot);
                aimingDotProgress[i] = (float)i / aimingDotCount;
            }
        }
    }

    private void Update()
    {
        // 조준 중 상태일 때만 매 프레임 로직을 실행합니다.
        if (!isAiming) return;

        UpdateCursorPosition();
        UpdateAimingLine();
    }

    /// <summary>
    /// 조준을 시작합니다. 이제 GameObject를 받습니다.
    /// </summary>
    public void StartAiming(GameObject starter)
    {
        startObject = starter;
        // UI 오브젝트인지 확인하기 위해 RectTransform 컴포넌트를 가져옵니다.
        startRectTransform = starter.GetComponent<RectTransform>();

        isAiming = true;
        Cursor.visible = false;
        if (cursorInstance != null) cursorInstance.SetActive(true);
        foreach (var dot in aimingDotPool) dot.SetActive(true);
    }

    /// <summary>
    /// 조준을 중단합니다.
    /// </summary>
    public void StopAiming()
    {
        isAiming = false;
        Cursor.visible = true;
        if (cursorInstance != null) cursorInstance.SetActive(false);
        foreach (var dot in aimingDotPool) dot.SetActive(false);
    }

    private void UpdateCursorPosition()
    {
        if (cursorInstance == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f, boardLayer))
        {
            Vector3 cursorTargetPosition = hit.point + new Vector3(0, cursorFloatHeight, 0);
            cursorInstance.transform.position = Vector3.Lerp(cursorInstance.transform.position, cursorTargetPosition, Time.deltaTime * cursorMoveSpeed);
        }
    }

    private void UpdateAimingLine()
    {
        if (aimingDotPool.Count == 0 || cursorInstance == null || startObject == null) return;

        Vector3 startPoint;

        // ★★★ 핵심 수정: UI 카드인지, 3D 필드 카드인지 확인합니다. ★★★
        if (startRectTransform != null) // RectTransform이 있다면 UI 오브젝트입니다.
        {
            // UI의 스크린 좌표를 3D 월드 좌표로 변환합니다.
            Ray ray = Camera.main.ScreenPointToRay(startRectTransform.position);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f, boardLayer))
            {
                startPoint = hit.point; // 게임 보드에 닿은 지점을 시작점으로 설정
            }
            else
            {
                // 레이캐스트 실패 시 대체 위치
                startPoint = startRectTransform.position;
            }
        }
        else // 일반 3D 오브젝트입니다.
        {
            startPoint = startObject.transform.position;
        }

        Vector3 endPoint = cursorInstance.transform.position;
        Vector3 controlPoint = (startPoint + endPoint) / 2 + Vector3.up * aimingCurveHeight;

        for (int i = 0; i < aimingDotPool.Count; i++)
        {
            aimingDotProgress[i] = (aimingDotProgress[i] + Time.deltaTime * aimingDotSpeed) % 1.0f;
            Vector3 position = GetPointOnBezierCurve(startPoint, controlPoint, endPoint, aimingDotProgress[i]);
            aimingDotPool[i].transform.position = position;

            float scaleValue = dotCurve.Evaluate(aimingDotProgress[i]);
            aimingDotPool[i].transform.localScale = Vector3.one * scaleValue;
        }
    }

    private Vector3 GetPointOnBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinusT = 1f - t;
        return (oneMinusT * oneMinusT * p0) + (2f * oneMinusT * t * p1) + (t * t * p2);
    }
}
