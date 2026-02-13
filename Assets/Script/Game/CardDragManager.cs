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

        HandleInput(); // 마우스 입력 처리

        // 드래그 중이면 카드 위치를 업데이트
        if (_isDragging && _currentCard != null)
        {
            UpdateCardPositionAndTilt();
        }
    }

    // --- 서버 응답 처리 핸들러 ---

    // 성공: 마나 업데이트가 왔다는 건 카드가 정상적으로 처리되었다는 뜻
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
                handManager.RemoveCardFromHand(_waitingCard);
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

    // ----------------------------

    /// <summary>
    /// 마우스 클릭/드래그/놓기 입력을 처리합니다.
    /// </summary>
    private void HandleInput()
    {
        if (_isSpawning) return; // 소환 중이면 조작 불가

        // 1. 마우스 누름 (드래그 시작)
        if (Input.GetMouseButtonDown(0))
        {
            GameObject hoveredCard = handManager.GetHoveredCard();
            // 손패가 안정된 상태이고, 마우스 아래 카드가 있다면 -> 집기
            if (handManager.IsHandStable && hoveredCard != null)
            {
                StartDrag(hoveredCard);
            }
        }

        // 2. 마우스 누르고 있음 (드래그 중)
        if (Input.GetMouseButton(0) && _isDragging)
        {
            CheckZoneAndToggleTargeting(); // 손패 영역을 벗어났는지 확인
        }

        // 3. 마우스 뗌 (드래그 종료)
        if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            EndDrag();
        }
    }

    // 드래그 시작
    private void StartDrag(GameObject card)
    {
        _currentCard = card;
        _isDragging = true;
        _lastPosition = card.transform.position;

        handManager.SetDraggedCard(_currentCard); // 손패 매니저에게 "이거 내가 가져간다"고 알림

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
            if (_playfieldMathPlane.Raycast(ray, out enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 localHit = handManager.handAnchor.InverseTransformPoint(hitPoint);
                Vector3 targetLocal = new Vector3(localHit.x, dragHeight, localHit.z);
                targetPos = handManager.handAnchor.TransformPoint(targetLocal);
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
    void EndDrag()
    {
        if (_currentCard == null) return;

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