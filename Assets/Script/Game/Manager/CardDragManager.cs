using UnityEngine;
using DG.Tweening;
using System.Collections;
using Unity.VisualScripting;

/// <summary>
/// 2D UI 기반: 손패에 있는 카드를 마우스로 집어서 움직이고,
/// 필드(3D)에 내려놓거나 되돌려놓는 기능을 담당합니다.
/// </summary>
public class CardDragManager : MonoBehaviour
{
    public static CardDragManager instance;

    [Header("연결")]
    // [변경점 1] 기존 HandInteractionManager 대신 새로 만든 HandCardControllManager 연결
    public HandCardControllManager handManager;
    public Camera mainCamera;
    public GameObject previewMinion; // 추가 타겟팅 시 보여줄 임시 하수인 오브젝트
    private GameObject _previewMinion;

    [Tooltip("UI 카드가 렌더링되는 메인 캔버스 (마우스 좌표 변환용)")]
    public Canvas dragCanvas;

    [Header("드래그 설정")]
    // 3D용 dragHeight 제거
    public float dragFollowSpeed = 20f;

    [Header("UI 틸트(기울기) 효과")]
    public float tiltStrength = 0.5f; // 마우스 이동 속도에 따른 회전 강도
    public float maxTiltAngle = 20f;
    public float tiltReturnSpeed = 10f;

    [Header("영역 및 레이어")]
    public float handZoneHeightRatio = 0.35f;
    [Tooltip("3D 필드 슬롯을 감지하기 위한 레이어")]
    public LayerMask fieldSlotLayer;

    [Header("타겟팅")]
    public bool temp_CardIsTargeted = true;
    public Transform targetingSourceTransform;

    // 내부 변수들
    private GameObject _currentCard;
    private GameObject _waitingCard;
    private bool _isDragging = false;
    public LayerMask entityLayer; // Inspector에서 하수인/영웅 레이어를 할당해주세요.
    public bool IsWaitingForTarget { get; private set; } = false; // InputManager와 통신용
    private int _pendingSlotIndex = -1; // 타겟팅 확정 후 보낼 슬롯 위치 임시 저장

    // 기울기 계산을 위한 이전 프레임 마우스 위치
    private Vector2 _lastMousePosition;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPlayCardFailedEvent += OnServerFailResponse;
            GameClient.Instance.OnPlayCardSuccessEvent += OnServerSuccessResponse;
        }
    }

    private void Update()
    {
        if (handManager == null) return;

        // 1. 전투의 함성 타겟팅 대기 상태일 때의 마우스 입력 처리
        if (IsWaitingForTarget)
        {
            HandleTargetingPhase();
            return; // 타겟팅 중에는 기존 드래그 로직 무시
        }

        // 2. 일반 드래그 중일 때
        if (_isDragging && _currentCard != null)
        {
            CheckZoneAndToggleTargeting(); // 어느 필드에 놓을지 보여주는 본래 용도로 사용
            UpdateCardPositionAndTilt();
        }
    }

    // --- 서버 응답 처리 핸들러 ---
    private void OnServerSuccessResponse(string instanceId)
    {
        if (_waitingCard != null)
        {
            var display = _waitingCard.GetComponent<GameCardDisplay>();
            if (display != null && display.InstanceId == instanceId)
            {
                Debug.Log($"카드 [{instanceId}] 사용 승인됨. 손패에서 제거.");
                handManager.SetDraggedCard(null);

                // CardActionQueueManager 연출 실행
                CardActionQueueManager.Instance.PreparePlay(_waitingCard, false);
                _waitingCard = null;

                HandCardControllManager.instance.AlignHand();  // 사용 성공시 핸드 정렬
            }
        }
    }

    private void OnServerFailResponse(string reason)
    {
        if (_waitingCard != null)
        {
            Debug.LogWarning($"카드 사용 실패 ({reason}). 손패로 복귀.");
            _waitingCard.SetActive(true);
            handManager.SetDraggedCard(null);
            handManager.AlignHand();
            _waitingCard = null;
        }
    }

    // ==========================================================
    // 1. 드래그 시작
    // ==========================================================
    public void StartDrag(GameObject card)
    {
        _currentCard = card;
        _isDragging = true;
        _lastMousePosition = Input.mousePosition;

        handManager.SetDraggedCard(_currentCard);
        handManager.CreatePhantomCard(_currentCard);

        RectTransform cardRect = _currentCard.GetComponent<RectTransform>();
        cardRect.DOKill();

        // [변경점 2] Z축 이동이 아니라, UI 계층의 맨 앞으로 카드를 가져옵니다.
        cardRect.SetAsLastSibling();

        // 카드를 원래 크기로 돌리고, 각도를 똑바로 세웁니다.
        cardRect.DOScale(handManager.OriginalCardScale, 0.2f).SetEase(Ease.OutQuad);
        cardRect.DOLocalRotateQuaternion(Quaternion.identity, 0.2f);
    }

    // ==========================================================
    // 2. 카드 이동 및 기울기
    // ==========================================================
    private void UpdateCardPositionAndTilt()
    {
        RectTransform cardRect = _currentCard.GetComponent<RectTransform>();

        // 1. 위치 이동: 가상 평면(Plane)을 삭제하고 마우스 픽셀 좌표를 직접 추적합니다.
        Vector2 targetPos = Input.mousePosition;

        if (dragCanvas != null && dragCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            // Canvas가 Camera 공간일 때 부드러운 위치 변환
            RectTransformUtility.ScreenPointToWorldPointInRectangle(dragCanvas.transform as RectTransform, Input.mousePosition, dragCanvas.worldCamera, out Vector3 worldPoint);
            cardRect.position = Vector3.Lerp(cardRect.position, worldPoint, Time.deltaTime * dragFollowSpeed);
        }
        else
        {
            // Canvas가 Overlay 공간일 때
            cardRect.position = Vector3.Lerp(cardRect.position, targetPos, Time.deltaTime * dragFollowSpeed);
        }

        // 2. 이동 방향에 따른 기울기 효과
        ApplyDragTilt(cardRect);
        _lastMousePosition = Input.mousePosition;
    }

    private void ApplyDragTilt(RectTransform cardRect)
    {
        // 2D 마우스 픽셀 이동 속도를 구합니다.
        Vector2 velocity = ((Vector2)Input.mousePosition - _lastMousePosition) / Time.deltaTime;

        // UI에 맞게 X, Y축 회전 계산 (마우스가 위/아래/좌/우로 움직일 때 카드가 젖혀짐)
        float targetRotX = velocity.y * tiltStrength;
        float targetRotY = -velocity.x * tiltStrength;

        targetRotX = Mathf.Clamp(targetRotX, -maxTiltAngle, maxTiltAngle);
        targetRotY = Mathf.Clamp(targetRotY, -maxTiltAngle, maxTiltAngle);

        Quaternion targetRotation = Quaternion.Euler(targetRotX, targetRotY, 0);
        cardRect.localRotation = Quaternion.Slerp(cardRect.localRotation, targetRotation, Time.deltaTime * tiltReturnSpeed);
    }

    // ==========================================================
    // 3. 영역 판정 및 조준선 표시
    // ==========================================================
    private void CheckZoneAndToggleTargeting()
    {
        bool inHandZone = IsMouseInHandZone();

        if (inHandZone)
        {
            if (!_currentCard.activeSelf) _currentCard.SetActive(true);
            if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();
        }
        else
        {
            if (temp_CardIsTargeted)
            {
                if (_currentCard.activeSelf) _currentCard.SetActive(false);
                if (TargetingReticle.Instance != null) TargetingReticle.Instance.StartTargeting(targetingSourceTransform);
            }
            else
            {
                if (!_currentCard.activeSelf) _currentCard.SetActive(true);
                if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();
            }
        }
    }

    // ==========================================================
    // 4. 드래그 종료 (하이브리드 충돌 판정)
    // ==========================================================
    public void EndDrag()
    {
        if (_currentCard == null) return;
        if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();

        handManager.RemovePhantomCard(_currentCard);
        bool requestSent = false;

        // [핵심] 카드는 UI 캔버스에 있지만, 놓을 곳(필드)은 3D이므로 Raycast를 쏩니다.
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, fieldSlotLayer))
        {
            FieldSlot slot = hit.collider.GetComponent<FieldSlot>();

            // 테스트용 
            if (slot != null && GameEntityManager.Instance.test)
            {
                GameCardDisplay cardDisplay = _currentCard.GetComponent<GameCardDisplay>();

                // [추가된 핵심 로직] 카드가 타겟팅이 필요한지(전투의 함성) 확인
                if (CardTargetingManager.Instance != null &&
                    CardTargetingManager.Instance.RequiresTargeting(cardDisplay._cardData))
                {
                    Debug.Log($"테스트 모드 슬롯 감지됨: {slot.slotIndex} -> 타겟팅 대기 모드 진입");

                    IsWaitingForTarget = true;
                    _pendingSlotIndex = slot.slotIndex;
                    _isDragging = false; // 드래그는 끝남

                    // 1. 드래그하던 2D UI 카드는 숨깁니다.
                    _currentCard.SetActive(false);

                    // 2. 슬롯 위치에 3D 임시 하수인(미리보기)을 생성합니다.
                    if (GameEntityManager.Instance != null && GameEntityManager.Instance.minionPrefab != null)
                    {
                        Vector3 previewMinionPosition = new Vector3(0, 0.5f, 0);

                        _previewMinion = Instantiate
                            (previewMinion, slot.transform.position + previewMinionPosition, previewMinion.transform.rotation, slot.transform);

                        // 클릭 방해를 막기 위해 임시 하수인의 콜라이더를 끕니다.
                        Collider col = _previewMinion.GetComponent<Collider>();
                        if (col != null) col.enabled = false;

                        // 3. 조준선이 이 임시 하수인에서 시작하도록 설정합니다.
                        if (TargetingReticle.Instance != null)
                            TargetingReticle.Instance.StartTargeting(_previewMinion.transform);
                    }

                    return; // 아직 서버로 전송하지 않고 함수 종료
                }
                else
                {
                    // 타겟팅이 필요 없는 카드면 즉시 소환 (기존 로직)
                    Debug.Log($"테스트 모드 슬롯 감지됨: {slot.slotIndex}");
                }

                return;
            }

            if (slot != null)
            {
                GameCardDisplay cardDisplay = _currentCard.GetComponent<GameCardDisplay>();

                // [추가된 핵심 로직] 카드가 타겟팅이 필요한지(전투의 함성) 확인
                if (CardTargetingManager.Instance != null &&
                    CardTargetingManager.Instance.RequiresTargeting(cardDisplay._cardData))
                {
                    Debug.Log($"슬롯 감지됨: {slot.slotIndex} -> 타겟팅 대기 모드 진입");

                    IsWaitingForTarget = true;
                    _pendingSlotIndex = slot.slotIndex;
                    _isDragging = false; // 드래그는 끝남

                    // 카드를 회전 없이 똑바로 펴고, 카드에서부터 조준선 시작
                    _currentCard.GetComponent<RectTransform>().DOLocalRotateQuaternion(Quaternion.identity, 0.2f);
                    if (TargetingReticle.Instance != null)
                        TargetingReticle.Instance.StartTargeting(_currentCard.transform);

                    return; // 아직 서버로 전송하지 않고 함수 종료
                }
                else 
                {
                    // 타겟팅이 필요 없는 카드면 즉시 소환 (기존 로직)
                    Debug.Log($"슬롯 감지됨: {slot.slotIndex}");
                    SendPlayRequestToClient(_currentCard, slot.slotIndex);
                    requestSent = true;
                }
            }
        }

        if (requestSent)
        {
            _waitingCard = _currentCard;
            // 서버 응답 대기 중 카드의 회전을 다시 반듯하게 만듦
            _waitingCard.GetComponent<RectTransform>().DOLocalRotateQuaternion(Quaternion.identity, 0.2f);
        }
        else
        {
            // 허공에 놓음 -> 즉시 취소 후 손패로 복귀
            if (!_currentCard.activeSelf) _currentCard.SetActive(true);
            handManager.SetDraggedCard(null);
            handManager.AlignHand();
        }

        _currentCard = null;
        _isDragging = false;
    }

    //  타겟팅 클릭 대기 로직 ---
    private void HandleTargetingPhase()
    {
        // 좌클릭: 대상 확정
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            // 하수인/영웅(Entity)이 맞았는지 확인
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, entityLayer))
            {
                GameCardDisplay targetCard = hit.collider.GetComponentInParent<GameCardDisplay>();
                GameCardDisplay sourceCard = _currentCard.GetComponent<GameCardDisplay>();

                // 유효한 대상인지 검사
                if (CardTargetingManager.Instance.IsValidTarget(sourceCard, targetCard))
                {
                    // 유효한 대상이면 마침내 서버로 [카드 + 슬롯 + 타겟] 전송
                    CardTargetingManager.Instance.SendPlayTargetCardRequest(_currentCard, _pendingSlotIndex, targetCard);

                    _waitingCard = _currentCard;
                    if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();

                    ResetTargetingState();
                    return;
                }
                else
                    Debug.Log("대상이 적절하지 않습니다");
            }

            // 대상이 유효하지 않거나 허공을 클릭하면 취소
            CancelCardPlay();
            ResetTargetingState();
        }
        // 우클릭: 사용 자체를 취소
        else if (Input.GetMouseButtonDown(1))
        {
            CancelCardPlay();
            ResetTargetingState();
        }
    }

    private void CancelCardPlay()
    {
        if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();
        if (_currentCard != null && !_currentCard.activeSelf) _currentCard.SetActive(true);

        handManager.SetDraggedCard(null);
        handManager.AlignHand();
    }


    // 타겟팅종료 함수를 코루틴으로 만들어 필드 클릭시 손패가 접히는걸 방지
    private void ResetTargetingState()
    {
        StartCoroutine(ResetTargetingCoroutine());
    }

    private IEnumerator ResetTargetingCoroutine()
    {
        yield return null;

        IsWaitingForTarget = false;
        _pendingSlotIndex = -1;
        _currentCard = null;
        _isDragging = false;

        if (_previewMinion != null)
        {
            Destroy(_previewMinion);
            _previewMinion = null;
        }
    }

    private void SendPlayRequestToClient(GameObject cardObj, int slotIndex)
    {
        GameCardDisplay cardDisplay = cardObj.GetComponent<GameCardDisplay>();
        if (cardDisplay != null && GameClient.Instance != null)
        {
            GameClient.Instance.SendPlayCardRequest(cardDisplay.InstanceId, slotIndex);
        }
    }

    private bool IsMouseInHandZone()
    {
        return (Input.mousePosition.y / Screen.height) <= handZoneHeightRatio;
    }
}


/*
using UnityEngine;
using DG.Tweening; // 애니메이션 라이브러리 (DOTween)
using System.Collections;

/// <summary>
/// 손패에 있는 카드를 마우스로 집어서 움직이고, 
/// 필드에 내려놓거나(소환), 되돌려놓는 기능을 담당합니다.
/// </summary>
public class CardDragManager : MonoBehaviour
{
    public static CardDragManager instance;

    [Header("연결")]
    public HandInteractionManager handManager; // 손패 관리자
    public Camera mainCamera; // 메인 카메라

    [Header("드래그 설정")]
    public float dragHeight = 0.5f; // 드래그할 때 카드가 뜨는 높이
    public float dragFollowSpeed = 10f; // 마우스를 따라가는 속도

    [Header("틸트(기울기) 효과")]
    public float tiltStrength = 20f; // 움직일 때 얼마나 기울어질지
    public float maxTiltAngle = 20f; // 최대 기울기 각도
    public float tiltReturnSpeed = 5f; // 원래대로 돌아오는 속도

    [Header("영역 및 레이어")]
    public float handZoneHeightRatio = 0.35f; // 화면 아래쪽 35%는 '손패 영역'으로 취급
    public LayerMask cardLayer;
    public LayerMask fieldSlotLayer; // 카드를 내려놓을 수 있는 '필드 슬롯' 레이어
    public LayerMask gameBoardLayer; // 게임 보드의 레이어

    [Header("타겟팅")]
    public bool temp_CardIsTargeted = true; // (테스트용) 타겟팅 기능 켜기/끄기
    public Transform targetingSourceTransform; // 화살표가 시작될 위치

    [Header("소환 테스트")]
    public GameObject testMinionPrefab; // 소환될 하수인 프리팹

    // 내부 변수들
    private GameObject _currentCard; // 지금 잡고 있는 카드
    private GameObject _waitingCard; // 서버 응답을 기다리는 카드 (공중에 멈춰있는 카드)
    private bool _isDragging = false; // 드래그 중인가?
    private Plane _handMathPlane; // 카드 이동 계산용 가상 평면
    private Plane _playfieldMathPlane;
    private Vector3 _lastPosition; // 기울기 계산용 이전 위치

    private bool _isSpawning = false; // 소환 연출 중인가?

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        _playfieldMathPlane = new Plane(Vector3.up, Vector3.zero); // 바닥 평면 정의
        if (targetingSourceTransform == null && handManager != null)
            targetingSourceTransform = handManager.handAnchor;

        // GameClient 이벤트 구독 (성공/실패 감지용)
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPlayCardFailedEvent += OnServerFailResponse;
            // 성공 시 마나나 엔티티가 업데이트되므로 이를 성공 신호로 사용
            GameClient.Instance.OnPlayCardSuccessEvent += OnServerSuccessResponse;
        }
    }

    private void Update()
    {
        if (handManager == null) return;

        // 손패 기준 평면 정의 (카드가 이 위에서 움직임)
        _handMathPlane = new Plane(handManager.handAnchor.up, handManager.handAnchor.position);

        // [수정됨] 드래그 중이면 카드 위치를 업데이트하고 타겟팅 상태를 갱신합니다.
        if (_isDragging && _currentCard != null)
        {
            CheckZoneAndToggleTargeting(); // 기존 HandleInput에 있던 드래그 중 영역 체크를 여기로 이동
            UpdateCardPositionAndTilt();
        }
    }

    // --- 서버 응답 처리 핸들러 ---

    // 카드 사용 성공
    private void OnServerSuccessResponse(string instanceId)
    {
        if (_waitingCard != null)
        {
            var display = _waitingCard.GetComponent<GameCardDisplay>();
            // 내가 기다리던 그 카드가 맞는지 아이디로 한 번 더 확인하면 아주 안전합니다.
            if (display != null && display.InstanceId == instanceId)
            {
                Debug.Log($"카드 [{instanceId}] 사용 승인됨. 손패에서 제거.");
                handManager.SetDraggedCard(null);
                CardActionQueueManager.Instance.PreparePlay(_waitingCard, true);
                // handManager.RemoveCardFromHand(_waitingCard);
                _waitingCard = null;
            }
        }
    }

    // 실패: 서버에서 명시적으로 실패 메시지를 보냄
    private void OnServerFailResponse(string reason)
    {
        if (_waitingCard != null)
        {
            Debug.LogWarning($"카드 사용 실패 ({reason}). 손패로 복귀.");

            // 카드를 다시 보이게 하고 (혹시 꺼졌다면)
            _waitingCard.SetActive(true);

            // HandManager에게 "드래그 끝났어"라고 알림 -> 자동으로 원래 자리로 정렬됨
            handManager.SetDraggedCard(null);
            handManager.AlignHand();

            _waitingCard = null;
        }
    }

    // 드래그 시작
    public void StartDrag(GameObject card)
    {
        _currentCard = card;
        _isDragging = true;
        _lastPosition = card.transform.position;

        handManager.SetDraggedCard(_currentCard); // 손패 매니저에게 "이거 내가 가져간다"고 알림 호버상태 해제
        handManager.CreatePhantomCard(_currentCard);

        // 카드 크기를 원래대로(확대 없이) 돌리고 잡기
        _currentCard.transform.DOKill();
        _currentCard.transform.DOScale(handManager.OriginalCardScale, 0.2f).SetEase(Ease.OutQuad);
        _currentCard.transform.rotation = handManager.handAnchor.rotation;
    }

    // 카드 위치 및 기울기 업데이트
    private void UpdateCardPositionAndTilt()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        float enter;
        Vector3 targetPos = _currentCard.transform.position;

        // 마우스가 가리키는 평면상의 위치 계산
        if (IsMouseInHandZone())
        {
            if (_handMathPlane.Raycast(ray, out enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                // 손패 영역에서는 앵커 기준으로 움직임
                Vector3 localHit = handManager.handAnchor.InverseTransformPoint(hitPoint);
                Vector3 targetLocal = new Vector3(localHit.x, dragHeight, localHit.z);
                targetPos = handManager.handAnchor.TransformPoint(targetLocal);
            }
        }
        else // 필드 영역
        {
            // Physics.Raycast로 카메라에서 쏜 광선이 gameBoardLayer와 부딪히는지 검사
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, gameBoardLayer))
            {
                // hit.point는 마우스가 실제 GameBoard 표면에 닿은 정확한 3D 좌표입니다.
                // 보드의 Y 높이(hit.point.y)에 dragHeight를 더해서 띄워줍니다.
                targetPos = new Vector3(hit.point.x, hit.point.y + dragHeight, hit.point.z);
            }
            else
            {
                // (예외 처리) 마우스가 게임 보드 밖으로 나갔을 때는 
                // 기존의 가상 평면(Y=0)을 백업으로 사용해 카드가 허공으로 사라지는 걸 막습니다.
                if (_playfieldMathPlane.Raycast(ray, out enter))
                {
                    Vector3 backupHitPoint = ray.GetPoint(enter);
                    targetPos = new Vector3(backupHitPoint.x, dragHeight, backupHitPoint.z);
                }
            }
        }

        // 부드럽게 이동 (Lerp)
        _currentCard.transform.position = Vector3.Lerp(_currentCard.transform.position, targetPos, Time.deltaTime * dragFollowSpeed);

        // 이동 방향에 따라 카드 기울이기
        ApplyDragTilt();
        _lastPosition = _currentCard.transform.position;
    }

    // 카드 기울기 효과
    private void ApplyDragTilt()
    {
        Vector3 velocity = (_currentCard.transform.position - _lastPosition) / Time.deltaTime;
        Vector3 localVelocity = handManager.handAnchor.InverseTransformDirection(velocity);

        float targetRotX = localVelocity.z * tiltStrength;
        float targetRotZ = -localVelocity.x * tiltStrength;

        targetRotX = Mathf.Clamp(targetRotX, -maxTiltAngle, maxTiltAngle);
        targetRotZ = Mathf.Clamp(targetRotZ, -maxTiltAngle, maxTiltAngle);

        Quaternion targetRotation = handManager.handAnchor.rotation * Quaternion.Euler(targetRotX, 0, targetRotZ);
        _currentCard.transform.rotation = Quaternion.Slerp(_currentCard.transform.rotation, targetRotation, Time.deltaTime * tiltReturnSpeed);
    }

    // 손패 영역 안인지 밖인지 체크
    private void CheckZoneAndToggleTargeting()
    {
        bool inHandZone = IsMouseInHandZone();

        if (inHandZone)
        {
            // 손패 안: 카드를 보여줌, 타겟팅 끔
            if (!_currentCard.activeSelf) _currentCard.SetActive(true);
            if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();
        }
        else
        {
            // 필드(손패 밖):
            if (temp_CardIsTargeted) // 타겟팅이 필요한 주문이라면
            {
                // 카드는 숨기고 화살표만 보여줌
                if (_currentCard.activeSelf) _currentCard.SetActive(false);
                if (TargetingReticle.Instance != null)
                    TargetingReticle.Instance.StartTargeting(targetingSourceTransform);
            }
            else // 하수인이라면
            {
                // 카드 계속 보여줌
                if (!_currentCard.activeSelf) _currentCard.SetActive(true);
                if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();
            }
        }
    }

    // 드래그 종료 (마우스 뗌)
    public void EndDrag()
    {
        if (_currentCard == null) return;

        handManager.RemovePhantomCard(_currentCard);
        bool requestSent = false;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, fieldSlotLayer))
        {
            FieldSlot slot = hit.collider.GetComponent<FieldSlot>();
            if (slot != null)
            {
                Debug.Log($"슬롯 감지됨: {slot.slotIndex}");

                // 서버 전송
                SendPlayRequestToClient(_currentCard, slot.slotIndex);
                requestSent = true;
            }
        }

        if (requestSent)
        {
            // 요청을 보냈으면, HandManager에게 정렬을 '복구하지 말라'고 유지해야 함
            // 즉, SetDraggedCard(null)을 호출하지 않고 _waitingCard에 저장해둠
            _waitingCard = _currentCard;

            // 현재 카드는 이제 드래그 상태가 아니지만, 대기 상태가 됨
            // 위치는 현재 드랍한 위치에 그대로 고정됨 (UpdateDrag가 안도니까)
        }
        else
        {
            // 허공에 놓았음 -> 즉시 취소
            if (!_currentCard.activeSelf) _currentCard.SetActive(true);
            handManager.SetDraggedCard(null); // 드래그 해제 알림
            handManager.AlignHand();          // 즉시 정렬
        }

        _currentCard = null;
        _isDragging = false;
    }

    // 소환 연출 (카드가 하수인으로 변신!) - *현재는 사용 안 함, 서버 응답 후 별도 처리 권장*
    private IEnumerator PlaySpawnSequence(GameObject cardObj, Vector3 slotPos, FieldSlot slot)
    {
        _isSpawning = true;
        // ... (생략: 이전 코드와 동일) ...
        yield return null;
        _isSpawning = false;
    }

    private void SendPlayRequestToClient(GameObject cardObj, int slotIndex)
    {
        GameCardDisplay cardDisplay = cardObj.GetComponent<GameCardDisplay>();
        if (cardDisplay != null && GameClient.Instance != null)
        {
            // 서버 통신 매니저(GameClient)에게 일을 떠넘깁니다.
            GameClient.Instance.SendPlayCardRequest(cardDisplay.InstanceId, slotIndex);
        }
    }

    // 마우스가 화면 아래쪽에 있는지 확인
    private bool IsMouseInHandZone()
    {
        return (Input.mousePosition.y / Screen.height) <= handZoneHeightRatio;
    }
}

*/