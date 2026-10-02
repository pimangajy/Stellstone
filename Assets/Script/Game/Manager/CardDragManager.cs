using UnityEngine;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 3D 월드 기반: 손패 카드를 마우스로 드래그하여 필드에 배치하거나 주문을 시전하는 기능을 담당합니다.
/// GameInputManager의 드래그 상태 머신(ReadyToDrag -> DraggingHand)과 완벽히 연동됩니다.
/// </summary>
public class CardDragManager : MonoBehaviour
{
    public static CardDragManager instance;

    [Header("1. 매니저 및 카메라 연결")]
    [Tooltip("손패 관리를 담당하는 3D 매니저")]
    public HandCardControllManager handManager;

    [Tooltip("탑뷰 메인 카메라")]
    public Camera mainCamera;

    [Tooltip("타겟팅 화살표의 발사 시작점 Transform (미지정 시 handAnchor 또는 리더 사용)")]
    public Transform targetingSourceTransform;

    [Header("2. 3D 드래그 이동 설정")]
    [Tooltip("드래그 중인 카드가 바닥/손패보다 위로 떠오르는 3D Y축 추가 높이입니다.")]
    public float dragElevationY = 0.5f;

    [Tooltip("마우스 커서의 3D 월드 좌표를 따라가는 보간 속도입니다.")]
    public float dragFollowSpeed = 25f;

    [Tooltip("드래그 시 카드 크기 배율입니다 (기본: 1.0)")]
    public float dragScaleMultiplier = 1.0f;

    [Header("3. 3D 틸트(기울기) 물리 효과")]
    [Tooltip("마우스 이동 속도에 따른 틸트 회전 강도")]
    public float tiltStrength = 0.08f;

    [Tooltip("최대 틸트 각도 (도 단위)")]
    public float maxTiltAngle = 20f;

    [Tooltip("마우스 정지 시 원래 직립 각도로 복귀하는 속도")]
    public float tiltReturnSpeed = 10f;

    [Header("4. 영역 판정 및 레이어")]
    [Tooltip("화면 하단 손패 영역 비율 (기본 0.35 = 화면 하단 35% 이하는 손패 영역)")]
    public float handZoneHeightRatio = 0.35f;

    [Tooltip("3D 필드 슬롯 감지용 레이어 (기본: Layer 6)")]
    public LayerMask fieldSlotLayer;

    [Tooltip("하수인/영웅 엔티티 감지용 레이어 (기본: Layer 12)")]
    public LayerMask entityLayer;

    [Header("5. 타겟팅 설정")]
    [Tooltip("true일 경우 카드의 타겟팅 속성과 관계없이 화면 일정 높이 이상 드래그 시 무조건 조준선(TargetingReticle)이 표시됩니다.")]
    public bool forceTargetingArrow = true;

    [Tooltip("기존 씬 호환용 임시 타겟 플래그")]
    public bool temp_CardIsTargeted = true;

    [Tooltip("조준선 활성화 시 드래그 중인 카드를 일시 숨겨 필드 시야를 확보할지 여부입니다.")]
    public bool hideCardInTargetingMode = true;

    // --- 상태 프로퍼티 ---
    public bool IsDragging => _isDragging;
    public bool IsWaitingForTarget { get; private set; } = false;
    public int LastPlayedSlotIndex { get; private set; } = -1;

    // --- 내부 변수 ---
    public GameObject _currentCard;
    private GameObject _waitingCard;
    private bool _isDragging = false;
    private bool _isTargetingMode = false;
    private bool _wasInHandZone = true;
    private Vector2 _lastMousePosition;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (handManager == null) handManager = HandCardControllManager.instance;

        // 기존 씬 인스펙터 값이 100 등 비정상적으로 큰 경우 3D 틸트에 맞게 자동 보정
        if (tiltStrength > 10f) tiltStrength = 0.08f;

        if (targetingSourceTransform == null)
        {
            if (GameEntityManager.Instance != null && GameEntityManager.Instance.myLeader != null)
            {
                targetingSourceTransform = GameEntityManager.Instance.myLeader.transform;
            }
            else
            {
                GameObject leaderObj = GameObject.Find("Reader_Frame");
                if (leaderObj != null) targetingSourceTransform = leaderObj.transform;
            }
        }

        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPlayCardFailedEvent += OnServerFailResponse;
            GameClient.Instance.OnPlayCardSuccessEvent += OnServerSuccessResponse;
        }
    }

    private void Update()
    {
        if (handManager == null) handManager = HandCardControllManager.instance;

        // 1. 타겟팅 대기 상태 처리
        if (IsWaitingForTarget)
        {
            HandleTargetingPhase();
            return;
        }

        // 2. 일반 드래그 중일 때: 영역 검사 및 3D 위치/틸트 갱신
        if (_isDragging && _currentCard != null)
        {
            CheckZoneAndToggleTargeting();
            UpdateCardPositionAndTilt();
        }
    }

    // ==========================================================
    // 1. 드래그 시작 (GameInputManager 연동)
    // ==========================================================

    /// <summary>
    /// GameInputManager에서 마우스 드래그 임계값(dragThreshold)을 넘었을 때 호출되는 드래그 시작 함수
    /// </summary>
    public void StartDrag(GameObject card)
    {
        if (card == null) return;

        _currentCard = card;
        _isDragging = true;
        _isTargetingMode = false;
        _lastMousePosition = Input.mousePosition;
        _wasInHandZone = true;

        if (handManager == null) handManager = HandCardControllManager.instance;
        if (handManager != null)
        {
            handManager.SetDraggedCard(_currentCard);
            handManager.CreatePhantomCard(_currentCard);
        }

        // 카드 애니메이션 정리 및 최상위 렌더링
        _currentCard.transform.DOKill();
        _currentCard.transform.SetAsLastSibling();

        // 3D 카메라를 향하는 기본 직립 각도 및 크기 설정
        Quaternion baseRot = (handManager != null) ? Quaternion.Euler(handManager.cardBaseRotation) : Quaternion.Euler(-90f, 0f, 0f);
        Vector3 baseScale = (handManager != null) ? (handManager.OriginalCardScale * handManager.handScaleMultiplier * dragScaleMultiplier) : _currentCard.transform.localScale;

        _currentCard.transform.localRotation = baseRot;
        _currentCard.transform.DOScale(baseScale, 0.2f).SetEase(Ease.OutQuad);

        // 드래그 시작 시 소모 마나 크리스탈 하이라이트
        if (BattleManager.Instance != null)
        {
            GameCardDisplay cardDisplay = _currentCard.GetComponent<GameCardDisplay>();
            int cost = cardDisplay != null ? (cardDisplay._cardInfo != null ? cardDisplay._cardInfo.currentCost : (cardDisplay._cardData != null ? cardDisplay._cardData.manaCost : 0)) : 0;
            BattleManager.Instance.HighlightManaCost(cost);
        }
    }

    // ==========================================================
    // 2. 3D 카드 위치 이동 및 틸트(기울기)
    // ==========================================================

    /// <summary>
    /// 3D 가상 평면 레이캐스트를 통해 마우스 커서의 3D 월드 좌표를 따라가고 3D 틸트 효과를 적용합니다.
    /// </summary>
    private void UpdateCardPositionAndTilt()
    {
        if (_currentCard == null || mainCamera == null) return;

        // 1. 3D 가상 수평 평면(Plane) 레이캐스트로 마우스 3D 월드 위치 계산
        float baseY = (handManager != null && handManager.handAnchor != null) ? handManager.handAnchor.position.y : 0f;
        Plane dragPlane = new Plane(Vector3.up, new Vector3(0f, baseY + dragElevationY, 0f));
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 targetWorldPos = ray.GetPoint(enter);
            _currentCard.transform.position = Vector3.Lerp(_currentCard.transform.position, targetWorldPos, Time.deltaTime * dragFollowSpeed);
        }

        // 2. 3D 이동 틸트(기울기) 적용
        Apply3DDragTilt();
        _lastMousePosition = Input.mousePosition;
    }

    /// <summary>
    /// 마우스 이동 속도와 방향에 따라 카드가 젖혀지는 3D 틸트 효과
    /// </summary>
    private void Apply3DDragTilt()
    {
        if (_currentCard == null) return;

        Vector2 mouseDelta = ((Vector2)Input.mousePosition - _lastMousePosition) / Time.deltaTime;

        // 속도에 따른 틸트 각도 계산 (마우스가 움직이는 방향으로 기울어짐)
        float tiltPitch = Mathf.Clamp(mouseDelta.y * tiltStrength, -maxTiltAngle, maxTiltAngle);
        float tiltRoll = Mathf.Clamp(-mouseDelta.x * tiltStrength, -maxTiltAngle, maxTiltAngle);

        Quaternion baseRot = (handManager != null) ? Quaternion.Euler(handManager.cardBaseRotation) : Quaternion.Euler(-90f, 0f, 0f);
        Quaternion targetRot = baseRot * Quaternion.Euler(tiltPitch, 0f, tiltRoll);

        _currentCard.transform.localRotation = Quaternion.Slerp(_currentCard.transform.localRotation, targetRot, Time.deltaTime * tiltReturnSpeed);
    }

    // ==========================================================
    // 3. 영역 판정 및 타겟팅/손패 접기 전환
    // ==========================================================

    /// <summary>
    /// 마우스가 손패 영역과 필드 영역 사이를 오갈 때 손패 접기/펼치기 및 조준선 전환을 처리합니다.
    /// </summary>
    private void CheckZoneAndToggleTargeting()
    {
        bool inHandZone = IsMouseInHandZone();
        if (inHandZone == _wasInHandZone) return;

        _wasInHandZone = inHandZone;

        if (inHandZone)
        {
            // [필드 -> 손패 영역 안으로 복귀할 때]
            _isTargetingMode = false;

            if (_currentCard != null && !_currentCard.activeSelf)
            {
                _currentCard.SetActive(true);
            }

            if (TargetingReticle.Instance != null)
            {
                TargetingReticle.Instance.StopTargeting();
            }

            if (handManager != null && handManager.isFolded)
            {
                handManager.SpreadHand();
            }
        }
        else
        {
            // [손패 -> 필드 영역으로 나갈 때]
            GameCardDisplay cardDisplay = _currentCard != null ? _currentCard.GetComponent<GameCardDisplay>() : null;
            bool isMinion = (cardDisplay != null && cardDisplay._cardData != null && cardDisplay._cardData.cardType == CardType.하수인);
            bool isMember = (cardDisplay != null && cardDisplay._cardData != null && cardDisplay._cardData.cardType == CardType.멤버);
            bool requiresTargeting = (cardDisplay != null && cardDisplay._cardData != null && cardDisplay._cardData.targeting);

            // 손패 접기 (필드 슬롯 및 리더 시야 확보)
            if (handManager != null && !handManager.isFolded)
            {
                handManager.FoldHand();
            }

            // 서버에 유효 타겟 목록 요청 (하수인/멤버는 소환 슬롯 배치 후 2차 타겟팅 단계에서 서버가 대상을 요청하므로 제외)
            if (!isMinion && !isMember && (requiresTargeting || forceTargetingArrow || temp_CardIsTargeted))
            {
                if (GameClient.Instance != null && cardDisplay != null && !string.IsNullOrEmpty(cardDisplay.InstanceId))
                {
                    GameClient.Instance.SendValidTargetResponse(cardDisplay.InstanceId);
                }
            }

            // 조준선 발동 여부: forceTargetingArrow가 true이면 조건 불문 무조건 조준선 활성화
            bool shouldActivateTargeting = forceTargetingArrow || temp_CardIsTargeted || requiresTargeting || isMinion || isMember;

            if (shouldActivateTargeting)
            {
                _isTargetingMode = true;

                if (hideCardInTargetingMode && _currentCard != null)
                {
                    _currentCard.SetActive(false);
                }

                if (TargetingReticle.Instance != null)
                {
                    Transform source = GetTargetingSource();
                    if (source != null)
                    {
                        TargetingReticle.Instance.StartTargeting(source);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 조준선(TargetingReticle)이 시작될 최적의 3D 월드 트랜스폼을 반환합니다 (아군 리더 우선).
    /// </summary>
    public Transform GetTargetingSource()
    {
        if (targetingSourceTransform != null) return targetingSourceTransform;

        if (GameEntityManager.Instance != null && GameEntityManager.Instance.myLeader != null)
        {
            return GameEntityManager.Instance.myLeader.transform;
        }

        GameObject leaderObj = GameObject.Find("Reader_Frame");
        if (leaderObj != null) return leaderObj.transform;

        if (handManager != null && handManager.handAnchor != null)
        {
            return handManager.handAnchor;
        }

        return transform;
    }

    // ==========================================================
    // 4. 드래그 종료 (슬롯 검사 및 카드 플레이)
    // ==========================================================

    /// <summary>
    /// GameInputManager에서 마우스 좌클릭을 놓았을 때(Mouse Up) 호출되는 드래그 종료 함수
    /// </summary>
    public void EndDrag()
    {
        if (_currentCard == null) return;

        if (TargetingReticle.Instance != null)
        {
            TargetingReticle.Instance.StopTargeting();
        }

        if (handManager != null)
        {
            handManager.RemovePhantomCard(_currentCard);
        }

        GameCardDisplay cardDisplay = _currentCard.GetComponent<GameCardDisplay>();
        bool isMinion = (cardDisplay != null && cardDisplay._cardData != null && cardDisplay._cardData.cardType == CardType.하수인);
        bool isMember = (cardDisplay != null && cardDisplay._cardData != null && cardDisplay._cardData.cardType == CardType.멤버);
        bool requiresTargeting = (cardDisplay != null && cardDisplay._cardData != null && cardDisplay._cardData.targeting);

        bool requestSent = false;

        // 1. 하수인 카드의 경우: 3D 필드 슬롯에 놓았는지 검사
        if (isMinion)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, fieldSlotLayer))
            {
                FieldSlot slot = hit.collider.GetComponent<FieldSlot>();
                bool isMySlot = GameEntityManager.Instance != null &&
                                GameEntityManager.Instance.myFieldSlots != null &&
                                System.Array.IndexOf(GameEntityManager.Instance.myFieldSlots, slot) >= 0;

                if (slot != null && !slot.IsOccupied && isMySlot)
                {
                    LastPlayedSlotIndex = slot.slotIndex;
                    SendPlayRequestToClient(_currentCard, slot.slotIndex);
                    requestSent = true;

                    // 오프라인 / 테스트 모드일 때 자체 소환 처리
                    if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
                    {
                        Debug.Log($"[CardDragManager] (오프라인 테스트) 슬롯 {slot.slotIndex}에 하수인 카드 배치 완료");
                        if (handManager != null)
                        {
                            handManager.RemoveCardFromHand(_currentCard);
                        }
                    }
                }
                else if (slot != null && !isMySlot)
                {
                    Debug.LogWarning("[CardDragManager] 🚫 상대방 필드 슬롯에는 하수인을 배치할 수 없습니다.");
                }
            }
        }
        // 2. 멤버 카드의 경우: 3D 멤버 슬롯 또는 필드에 놓았을 때 0번 슬롯으로 소환
        else if (isMember)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, fieldSlotLayer))
            {
                FieldSlot slot = hit.collider.GetComponent<FieldSlot>();
                int targetSlot = (slot != null) ? slot.slotIndex : 0;
                LastPlayedSlotIndex = targetSlot;
                SendPlayRequestToClient(_currentCard, targetSlot);
                requestSent = true;

                if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
                {
                    Debug.Log($"[CardDragManager] (오프라인 테스트) 멤버 슬롯 {targetSlot}에 멤버 카드 배치 완료");
                    if (handManager != null)
                    {
                        handManager.RemoveCardFromHand(_currentCard);
                    }
                }
            }
            else if (!IsMouseInHandZone())
            {
                // 손패 영역 밖 필드 아무 곳에나 드롭해도 멤버존(0번 슬롯)으로 소환
                LastPlayedSlotIndex = 0;
                SendPlayRequestToClient(_currentCard, 0);
                requestSent = true;

                if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
                {
                    Debug.Log($"[CardDragManager] (오프라인 테스트) 멤버 슬롯 0에 멤버 카드 배치 완료");
                    if (handManager != null)
                    {
                        handManager.RemoveCardFromHand(_currentCard);
                    }
                }
            }
        }
        // 3. 주문 카드인 경우
        else if (cardDisplay != null && cardDisplay._cardData != null)
        {
            if (requiresTargeting)
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, entityLayer))
                {
                    GameCardDisplay targetCard = hit.collider.GetComponentInParent<GameCardDisplay>();
                    if (targetCard != null && (BattleManager.Instance == null || BattleManager.Instance.IsServerValidTarget(targetCard.EntityId)))
                    {
                        if (GameClient.Instance != null)
                        {
                            GameClient.Instance.SendPlayCardRequest(cardDisplay.InstanceId, -1, targetCard.EntityId);
                        }
                        requestSent = true;
                    }
                }
            }
            else
            {
                // 비타겟팅 주문: 손패 영역 밖(필드 영역)에 놓았을 때 시전
                if (!IsMouseInHandZone())
                {
                    SendPlayRequestToClient(_currentCard, -1);
                    requestSent = true;
                }
            }
        }

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.ResetHighlights();
        }

        if (requestSent)
        {
            _waitingCard = _currentCard;
            _currentCard.SetActive(false);
            if (handManager != null)
            {
                handManager.SetDraggedCard(null);
                if (handManager.isFolded) handManager.SpreadHand();
                handManager.AlignHand();
            }
        }
        else
        {
            // 유효하지 않은 곳에 놓았으므로 드래그 취소 (손패로 복귀)
            CancelDrag();
            return;
        }

        _currentCard = null;
        _isDragging = false;
        _isTargetingMode = false;
    }

    // ==========================================================
    // 5. 드래그 취소 (우클릭 또는 손패 복귀)
    // ==========================================================

    /// <summary>
    /// 마우스 우클릭 또는 유효하지 않은 드롭 시 드래그를 취소하고 카드를 손패로 복귀시킵니다.
    /// </summary>
    public void CancelDrag()
    {
        if (!_isDragging && !IsWaitingForTarget) return;

        if (TargetingReticle.Instance != null)
        {
            TargetingReticle.Instance.StopTargeting();
        }

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.ResetHighlights();
        }

        if (_currentCard != null)
        {
            if (!_currentCard.activeSelf)
            {
                // 커서 위치에서부터 복귀 애니메이션이 시작되도록 위치 동기화
                float baseY = (handManager != null && handManager.handAnchor != null) ? handManager.handAnchor.position.y : 0f;
                Plane dragPlane = new Plane(Vector3.up, new Vector3(0f, baseY + dragElevationY, 0f));
                Ray ray = mainCamera != null ? mainCamera.ScreenPointToRay(Input.mousePosition) : new Ray();
                if (dragPlane.Raycast(ray, out float enter))
                {
                    _currentCard.transform.position = ray.GetPoint(enter);
                }
                _currentCard.SetActive(true);
            }

            if (handManager != null)
            {
                handManager.RemovePhantomCard(_currentCard);
                handManager.SetDraggedCard(null);
                if (handManager.isFolded || _isTargetingMode)
                {
                    handManager.SpreadHand();
                }
                handManager.AlignHand(); // 원래 손패 슬롯 위치로 부드럽게 복귀
            }
        }

        _currentCard = null;
        _isDragging = false;
        _wasInHandZone = true;
        _isTargetingMode = false;
        IsWaitingForTarget = false;
    }

    // ==========================================================
    // 6. 전투의 함성 타겟팅 대기 로직
    // ==========================================================
    private void HandleTargetingPhase()
    {
        // 좌클릭: 대상 확정
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, entityLayer))
            {
                // 타겟팅 완료
            }
            CancelCardPlay();
            ResetTargetingState();
        }
        // 우클릭: 사용 자체 취소
        else if (Input.GetMouseButtonDown(1))
        {
            CancelCardPlay();
            ResetTargetingState();
        }
    }

    private void CancelCardPlay()
    {
        CancelDrag();
    }

    private void ResetTargetingState()
    {
        StartCoroutine(ResetTargetingCoroutine());
    }

    private IEnumerator ResetTargetingCoroutine()
    {
        yield return null;
        IsWaitingForTarget = false;
        _currentCard = null;
        _isDragging = false;
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

    // ==========================================================
    // 서버 응답 핸들러
    // ==========================================================
    private void OnServerSuccessResponse(string instanceId)
    {
        if (_waitingCard != null)
        {
            var display = _waitingCard.GetComponent<GameCardDisplay>();
            if (display != null && display.InstanceId == instanceId)
            {
                if (handManager != null)
                {
                    handManager.SetDraggedCard(null);
                    handManager.RemoveCardFromHand(_waitingCard);
                    handManager.AlignHand();
                }
                _waitingCard = null;
            }
        }
    }

    private void OnServerFailResponse(string reason)
    {
        if (_waitingCard != null)
        {
            Debug.LogWarning($"[CardDragManager] 카드 사용 실패 ({reason}). 손패로 복귀.");
            _waitingCard.SetActive(true);
            if (handManager != null)
            {
                handManager.SetDraggedCard(null);
                handManager.AlignHand();
            }
            _waitingCard = null;
        }
    }
}
