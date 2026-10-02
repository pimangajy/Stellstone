using System;
using UnityEngine;

/// <summary>
/// 작업대 3D 오브젝트 스크롤러
/// - 3D 상자 및 자식 오브젝트 콜라이더 위에서 드래그/휠 감지
/// - 클릭 vs 드래그 판정 분리 (임계값 이상 움직일 때만 드래그 판정)
/// - 드래그 시작 후 영역을 벗어나도 마우스 클릭을 유지하는 동안 스크롤 유지
/// - 상자 및 자식 오브젝트 경계 기반 자동 범위 제한 (상자의 위/아래를 벗어나지 않음)
/// - 마우스 휠: 부드러운 스냅 이동 (관성 없이 목표 위치 도착 즉시 정지)
/// - 마우스 드래그: 마우스와 1:1 즉각 이동 (마우스 떼거나 멈추면 즉시 정지)
/// </summary>
public class CraftTableScroller : MonoBehaviour
{
    [Header("스크롤 대상 및 감지 영역")]
    [Tooltip("실제 위/아래로 이동시킬 3D 오브젝트 Transform (사용자 큐브/컨테이너 연결)")]
    [SerializeField] private Transform targetObject;

    [Tooltip("마우스 입력을 감지할 3D 상자(또는 영역) 콜라이더")]
    [SerializeField] private Collider dragAreaCollider;

    [Tooltip("상자 내부의 자식 콜라이더(카드팩 등)도 감지 영역에 포함할지 여부")]
    [SerializeField] private bool includeChildColliders = true;

    [Tooltip("레이캐스트 발사용 카메라 (비워둘 시 Main Camera 자동 탐색)")]
    [SerializeField] private Camera targetCamera;

    [Header("이동 축 및 감도 설정")]
    [Tooltip("이동 축 (로컬 또는 월드 기준 위/아래 방향 벡터)")]
    [SerializeField] private Vector3 moveAxis = Vector3.forward;

    [Tooltip("로컬 좌표계 이동 여부 (true: 부모 기준 localPosition 이동)")]
    [SerializeField] private bool useLocalPosition = true;

    [Tooltip("마우스 드래그 감도 (1픽셀당 이동 거리)")]
    [SerializeField] private float dragSensitivity = 0.005f;

    [Header("마우스 휠 부드러운 스냅 설정 (무관성)")]
    [Tooltip("마우스 휠 1틱당 이동할 단계 거리 (단위: m)")]
    [SerializeField] private float wheelStepDistance = 0.3f;

    [Tooltip("마우스 휠 스냅 이동 시 부드러운 이동 속도 (도착 즉시 정지)")]
    [SerializeField] private float wheelSmoothSpeed = 4.0f;

    [Header("이동 범위 자동 제한 (상자 & 자식 기준)")]
    [Tooltip("상자 위/아래 가장자리 여백 (m)")]
    [SerializeField] private float edgePadding = 0.05f;

    [Header("클릭 vs 드래그 구분")]
    [Tooltip("드래그로 인정하기 위한 최소 마우스 이동 거리 (픽셀)")]
    [SerializeField] private float dragThresholdPixels = 8f;

    [Header("구역 연동 (선택사항)")]
    [Tooltip("연동할 StorageCameraController (비워두면 항상 동작)")]
    [SerializeField] private StorageCameraController cameraController;

    [Tooltip("작업대 구역 인덱스 (기본: 0)")]
    [SerializeField] private int craftSectionIndex = 0;

    // 내부 상태 변수
    private Vector3 _initialPosition;
    private float _currentOffset = 0f;
    private float _targetOffset = 0f;
    private bool _isTrackingPress = false;
    private bool _isDragging = false;
    private Vector3 _pressStartMousePos;
    private Vector3 _lastMousePos;
    private bool _isInCraftSection = true;

    // 실시간 계산된 제한 범위
    private float _calculatedMinLimit = 0f;
    private float _calculatedMaxLimit = 0f;

    /// <summary>
    /// 카드팩 클릭 시 외부에서 구독할 수 있는 이벤트 (선택된 콜라이더 전달)
    /// </summary>
    public event Action<Collider> OnItemClicked;

    private void Awake()
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("[CraftTableScroller] ⚠️ targetCamera가 인스펙터에 할당되지 않았습니다.");
        }

        if (cameraController == null)
        {
            Debug.LogWarning("[CraftTableScroller] ⚠️ cameraController가 인스펙터에 할당되지 않았습니다.");
        }

        if (targetObject == null)
        {
            Debug.LogWarning("[CraftTableScroller] ⚠️ targetObject가 인스펙터에 할당되지 않았습니다.");
        }
        else
        {
            _initialPosition = useLocalPosition ? targetObject.localPosition : targetObject.position;
        }

        if (dragAreaCollider == null)
        {
            Debug.LogWarning("[CraftTableScroller] ⚠️ dragAreaCollider가 인스펙터에 할당되지 않았습니다.");
        }
    }

    private void OnEnable()
    {
        if (cameraController != null)
        {
            cameraController.onSectionEnter.AddListener(OnSectionEnter);
            cameraController.onReturnOverview.AddListener(OnReturnOverview);
        }
    }

    private void OnDisable()
    {
        if (cameraController != null)
        {
            cameraController.onSectionEnter.RemoveListener(OnSectionEnter);
            cameraController.onReturnOverview.RemoveListener(OnReturnOverview);
        }

        ResetDragState();
    }

    private void OnSectionEnter(int sectionIndex)
    {
        _isInCraftSection = (sectionIndex == craftSectionIndex);
        UpdateScrollLimits();
    }

    private void OnReturnOverview()
    {
        _isInCraftSection = false;
        ResetDragState();
    }

    private void Update()
    {
        if (cameraController != null && !_isInCraftSection) return;
        if (targetObject == null) return;

        UpdateScrollLimits();
        HandleDragInput();
        HandleWheelInput();
        UpdateSmoothPosition();
    }

    [Header("상호작용 잠금")]
    [Tooltip("카드팩 포커스 또는 카드 개봉 시 스크롤/클릭 입력을 잠글 수 있는 플래그")]
    public bool IsInteractive { get; set; } = true;

    /// <summary>
    /// 마우스 드래그 입력 처리 (1:1 즉각 반응, 멈추면 즉시 정지)
    /// </summary>
    private void HandleDragInput()
    {
        if (!IsInteractive)
        {
            if (_isTrackingPress || _isDragging)
            {
                ResetDragState();
            }
            return;
        }

        // 1. 마우스 좌클릭 누른 순간 (3D 감지 영역 위에서 시작했는지 검사)
        if (Input.GetMouseButtonDown(0))
        {
            if (IsCursorOverDragArea(out Collider hitCollider))
            {
                _isTrackingPress = true;
                _isDragging = false;
                _pressStartMousePos = Input.mousePosition;
                _lastMousePos = Input.mousePosition;
            }
        }

        // 2. 마우스 클릭을 유지하고 있는 동안 (영역 밖으로 나가도 드래그 유지!)
        if (_isTrackingPress && Input.GetMouseButton(0))
        {
            Vector3 currentMousePos = Input.mousePosition;

            // 임계값을 넘었는지 검사하여 드래그 상태로 진입
            if (!_isDragging)
            {
                if ((currentMousePos - _pressStartMousePos).magnitude >= dragThresholdPixels)
                {
                    _isDragging = true;
                }
            }

            // 드래그 중인 경우: 감속/지연 없이 마우스와 1:1 즉시 이동
            if (_isDragging)
            {
                float deltaY = currentMousePos.y - _lastMousePos.y;
                _targetOffset += deltaY * dragSensitivity;
                _targetOffset = Mathf.Clamp(_targetOffset, _calculatedMinLimit, _calculatedMaxLimit);
                _currentOffset = _targetOffset; // 드래그 중에는 즉시 동기화 (관성 없음)
            }

            _lastMousePos = currentMousePos;
        }

        // 3. 마우스 클릭을 놓았을 때 (드래그 종료 또는 클릭 판정)
        if (Input.GetMouseButtonUp(0) && _isTrackingPress)
        {
            // 드래그하지 않고 제자리에서 뗐다면 '단순 클릭'으로 판정
            if (!_isDragging)
            {
                if (IsCursorOverDragArea(out Collider hitCollider))
                {
                    OnItemClicked?.Invoke(hitCollider);
                    Debug.Log($"[CraftTableScroller] 🎯 단순 클릭 감지: {hitCollider.name}");
                }
            }

            ResetDragState();
        }
    }

    /// <summary>
    /// 마우스 휠 스크롤 처리 (목표 오프셋 갱신)
    /// </summary>
    private void HandleWheelInput()
    {
        if (!IsInteractive) return;
        if (_isDragging) return; // 드래그 중일 때는 휠 스크롤 무시

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            // 커서가 3D 상자 영역 위에 있을 때만 휠 스크롤 반영
            if (IsCursorOverDragArea(out _))
            {
                float direction = scroll > 0f ? 1f : -1f;
                _targetOffset += direction * wheelStepDistance;
                _targetOffset = Mathf.Clamp(_targetOffset, _calculatedMinLimit, _calculatedMaxLimit);
            }
        }
    }

    /// <summary>
    /// 휠 스크롤 시 부드럽게 목표 지점으로 이동 (관성 없이 도착 즉시 정지)
    /// </summary>
    private void UpdateSmoothPosition()
    {
        // 드래그 중이 아닐 때만 휠 스무딩 적용
        if (!_isDragging)
        {
            if (Mathf.Abs(_currentOffset - _targetOffset) > 0.0001f)
            {
                _currentOffset = Mathf.MoveTowards(_currentOffset, _targetOffset, wheelSmoothSpeed * Time.deltaTime);
            }
            else
            {
                _currentOffset = _targetOffset;
            }
        }

        Vector3 newPos = _initialPosition + moveAxis.normalized * _currentOffset;
        if (useLocalPosition)
        {
            targetObject.localPosition = newPos;
        }
        else
        {
            targetObject.position = newPos;
        }
    }

    /// <summary>
    /// 상자의 경계와 자식 오브젝트들의 위치를 분석하여 이동 한계를 자동 갱신
    /// </summary>
    public void UpdateScrollLimits()
    {
        if (dragAreaCollider == null || targetObject == null || targetObject.childCount == 0)
        {
            _calculatedMinLimit = 0f;
            _calculatedMaxLimit = 0f;
            return;
        }

        // 이동 축 월드 벡터
        Vector3 worldAxis = useLocalPosition && targetObject.parent != null
            ? targetObject.parent.TransformDirection(moveAxis.normalized)
            : moveAxis.normalized;

        // 1. 상자 콜라이더의 위/아래 경계 좌표 계산 (WorldAxis 투영)
        GetBoxExtentsAlongAxis(worldAxis, out float boxMin, out float boxMax);
        boxMin += edgePadding;
        boxMax -= edgePadding;

        // 2. targetObject 자식들의 최소/최대 좌표 계산 (offset = 0 일 때의 기준 좌표)
        float childMinAtCurrent = float.MaxValue;
        float childMaxAtCurrent = float.MinValue;
        int activeChildCount = 0;

        for (int i = 0; i < targetObject.childCount; i++)
        {
            Transform child = targetObject.GetChild(i);
            if (!child.gameObject.activeSelf) continue;

            activeChildCount++;

            // 콜라이더나 렌더러가 있으면 외곽 반경(halfSize) 포함
            float halfExtent = 0f;
            if (child.TryGetComponent<Renderer>(out var ren))
            {
                halfExtent = Vector3.Dot(ren.bounds.extents, new Vector3(Mathf.Abs(worldAxis.x), Mathf.Abs(worldAxis.y), Mathf.Abs(worldAxis.z)));
            }
            else if (child.TryGetComponent<Collider>(out var col))
            {
                halfExtent = Vector3.Dot(col.bounds.extents, new Vector3(Mathf.Abs(worldAxis.x), Mathf.Abs(worldAxis.y), Mathf.Abs(worldAxis.z)));
            }

            float proj = Vector3.Dot(child.position, worldAxis);
            childMinAtCurrent = Mathf.Min(childMinAtCurrent, proj - halfExtent);
            childMaxAtCurrent = Mathf.Max(childMaxAtCurrent, proj + halfExtent);
        }

        if (activeChildCount == 0)
        {
            _calculatedMinLimit = 0f;
            _calculatedMaxLimit = 0f;
            return;
        }

        // 현재 offset을 제거하여 offset = 0 일 때의 기준 자식 위치 산출
        float childMinAtZero = childMinAtCurrent - _currentOffset;
        float childMaxAtZero = childMaxAtCurrent - _currentOffset;

        float totalChildSpan = childMaxAtZero - childMinAtZero;
        float boxSpan = boxMax - boxMin;

        // 자식들의 전체 길이가 상자 크기보다 작으면 스크롤 불필요 (0 고정)
        if (totalChildSpan <= boxSpan)
        {
            _calculatedMinLimit = 0f;
            _calculatedMaxLimit = 0f;
            return;
        }

        // 첫 번째/마지막 자식이 상자의 맨 위/맨 아래를 벗어나지 않도록 오프셋 계산
        float limit1 = boxMax - childMaxAtZero;
        float limit2 = boxMin - childMinAtZero;

        _calculatedMinLimit = Mathf.Min(limit1, limit2);
        _calculatedMaxLimit = Mathf.Max(limit1, limit2);
    }

    /// <summary>
    /// 상자 콜라이더의 월드 공간 축 기준 최소/최대 투영 좌표 계산
    /// </summary>
    private void GetBoxExtentsAlongAxis(Vector3 axis, out float min, out float max)
    {
        if (dragAreaCollider is BoxCollider box)
        {
            Transform boxTf = box.transform;
            Vector3 c = box.center;
            Vector3 s = box.size * 0.5f;

            // 로컬 8개 꼭짓점
            Vector3[] corners = new Vector3[8]
            {
                boxTf.TransformPoint(c + new Vector3(-s.x, -s.y, -s.z)),
                boxTf.TransformPoint(c + new Vector3(-s.x, -s.y,  s.z)),
                boxTf.TransformPoint(c + new Vector3(-s.x,  s.y, -s.z)),
                boxTf.TransformPoint(c + new Vector3(-s.x,  s.y,  s.z)),
                boxTf.TransformPoint(c + new Vector3( s.x, -s.y, -s.z)),
                boxTf.TransformPoint(c + new Vector3( s.x, -s.y,  s.z)),
                boxTf.TransformPoint(c + new Vector3( s.x,  s.y, -s.z)),
                boxTf.TransformPoint(c + new Vector3( s.x,  s.y,  s.z)),
            };

            min = float.MaxValue;
            max = float.MinValue;
            foreach (var corner in corners)
            {
                float val = Vector3.Dot(corner, axis);
                min = Mathf.Min(min, val);
                max = Mathf.Max(max, val);
            }
        }
        else
        {
            // BoxCollider가 아닌 경우 일반 Bounds 사용
            Bounds b = dragAreaCollider.bounds;
            Vector3 c = b.center;
            Vector3 e = b.extents;

            Vector3[] corners = new Vector3[8]
            {
                c + new Vector3(-e.x, -e.y, -e.z),
                c + new Vector3(-e.x, -e.y,  e.z),
                c + new Vector3(-e.x,  e.y, -e.z),
                c + new Vector3(-e.x,  e.y,  e.z),
                c + new Vector3( e.x, -e.y, -e.z),
                c + new Vector3( e.x, -e.y,  e.z),
                c + new Vector3( e.x,  e.y, -e.z),
                c + new Vector3( e.x,  e.y,  e.z),
            };

            min = float.MaxValue;
            max = float.MinValue;
            foreach (var corner in corners)
            {
                float val = Vector3.Dot(corner, axis);
                min = Mathf.Min(min, val);
                max = Mathf.Max(max, val);
            }
        }
    }

    // [최적화] 마우스 커서 콜라이더 검사 시 GC Alloc 방지를 위한 정적 버퍼
    private static readonly RaycastHit[] _raycastHitsBuffer = new RaycastHit[32];

    /// <summary>
    /// 마우스 커서가 3D 카드팩 또는 상자/책상 콜라이더 위에 있는지 검사 (카드팩 최우선 감지)
    /// </summary>
    private bool IsCursorOverDragArea(out Collider hitCollider)
    {
        hitCollider = null;
        if (targetCamera == null) return false;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        int hitCount = Physics.RaycastNonAlloc(ray, _raycastHitsBuffer, 100f, ~0, QueryTriggerInteraction.Collide);

        if (hitCount > 0)
        {
            // 거리순 정렬 (가장 가까운 오브젝트부터)
            Array.Sort(_raycastHitsBuffer, 0, hitCount, System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

            // [1순위] CardPackItem이 붙어 있는 카드팩 콜라이더 최우선 검출!
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _raycastHitsBuffer[i];
                if (hit.collider != null && hit.collider.GetComponentInParent<CardPackItem>() != null)
                {
                    hitCollider = hit.collider;
                    return true;
                }
            }

            // [2순위] 지정된 상자/책상 콜라이더(dragAreaCollider) 또는 그 자식 검출 (스크롤용)
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _raycastHitsBuffer[i];
                if (dragAreaCollider != null)
                {
                    if (hit.collider == dragAreaCollider || (includeChildColliders && hit.collider.transform.IsChildOf(dragAreaCollider.transform)))
                    {
                        hitCollider = hit.collider;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 드래그 상태 리셋
    /// </summary>
    private void ResetDragState()
    {
        _isTrackingPress = false;
        _isDragging = false;
    }

    /// <summary>
    /// 인스펙터 외부 설정용
    /// </summary>
    public void SetTargetObject(Transform newTarget)
    {
        targetObject = newTarget;
        if (targetObject != null)
        {
            _initialPosition = useLocalPosition ? targetObject.localPosition : targetObject.position;
            _currentOffset = 0f;
            _targetOffset = 0f;
            UpdateScrollLimits();
        }
    }

    public void SetDragAreaCollider(Collider newCollider)
    {
        dragAreaCollider = newCollider;
        UpdateScrollLimits();
    }
}
