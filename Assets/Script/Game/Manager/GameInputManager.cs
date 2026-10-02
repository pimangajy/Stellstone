using UnityEngine;
using UnityEngine.EventSystems; // UI 이벤트 시스템 처리를 위해 필수
using System.Collections.Generic;
/// <summary>
/// 게임 내의 모든 마우스 입력(Hover, Click, Drag)을 중앙에서 관리하는 스크립트입니다.
/// 
/// [연동 완료]
/// - HandCardControllManager의 호버링 기능 (ProcessHover)
/// - HandCardControllManager의 멀리건 클릭 기능 (OnMulliganCardClicked)
/// </summary>
public class GameInputManager : MonoBehaviour
{
    public static GameInputManager Instance;

    [Header("레이어 설정 (우선순위)")]
    [Tooltip("손패 카드 레이어 (가장 먼저 클릭 판정)")]
    public LayerMask handCardLayer;
    [Tooltip("필드 하수인/영웅 레이어 (손패 다음으로 클릭 판정)")]
    public LayerMask minionEntityLayer;
    [Tooltip("필드 레이어 (하수인 다음으로 클릭 판정)")]
    public LayerMask fieldEntityLayer;
    [Tooltip("슬롯 레이어")]
    public LayerMask fieldSlotLayer;

    [Header("드래그 설정")]
    public float dragThreshold = 10f; // 이만큼 움직여야 드래그로 인정

    // 전투의 함성 조준 상태를 업데이트하기 위해 서버 패킷 데이터를 임시 보관할 변수
    private S_RequestTargetForPlay _pendingTargetPacket;

    // --- 상태 관리를 위한 열거형(Enum) ---
    public enum InputState
    {
        Idle,           // 아무것도 안 함 (호버링 중)
        ReadyToDrag,    // 마우스를 꾹 눌렀으나 아직 안 움직임
        DraggingHand,   // 손패 카드를 드래그 중
        DraggingField,   // 필드 하수인을 드래그 중 (공격 조준)
        WaitingForChoice // 서버로부터 선택을 기다리는 상태
    }

    [Header("현재 상태 (디버그용)")]
    public InputState currentState = InputState.Idle;

    // --- 내부 변수 ---
    private Camera _mainCamera;
    private Vector2 _mouseDownPos;

    // 현재 선택된 대상들
    [SerializeField]
    private GameCardDisplay _selectedHandCard; // 드래그하려고 잡은 손패 카드
    [SerializeField]
    private GameCardDisplay _selectedFieldEntity; // 공격하려고 잡은 필드 하수인

    // 선택 모드 관련 내부 변수 ---
    private string _currentChoiceType = "";
    private int _choiceSourceEntityId = -1;
    private int _memberSkillSourceEntityId = 0;
    private int _memberSkillPendingSkillId = 0;
    private List<int> _validMemberSkillTargets = new List<int>();

    // 필드 하수인 1초 호버 프리뷰 관련 변수
    private GameCardDisplay _hoveredFieldCard;
    private float _fieldHoverTimer = 0f;
    private bool _hasShownFieldPreview = false;
    private const float FIELD_HOVER_DELAY = 1.0f;

    // UI 레이캐스트 재사용 버퍼 (클릭 시 힙 할당 방지)
    private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>(16);
    private PointerEventData _cachedPointerData;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _mainCamera = Camera.main;
    }

    private void Start()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPlayCardFailedEvent += OnPlayCardFailed;
            GameClient.Instance.OnValidMemberSkillTargetsResponseEvent += OnValidMemberSkillTargetsResponse;
        }
    }

    private void OnDestroy()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPlayCardFailedEvent -= OnPlayCardFailed;
            GameClient.Instance.OnValidMemberSkillTargetsResponseEvent -= OnValidMemberSkillTargetsResponse;
        }
    }

    private void OnValidMemberSkillTargetsResponse(S_ValidMemberSkillTargetsResponse response)
    {
        if (currentState == InputState.WaitingForChoice && _currentChoiceType == "MEMBER_SKILL")
        {
            if (response != null && response.validTargetIds != null)
            {
                _validMemberSkillTargets = new List<int>(response.validTargetIds);
            }
        }
    }

    private void OnPlayCardFailed(string reason)
    {
        // 타겟팅/선택 대기 모드 중 실패 패킷(더 이상 유효한 대상/슬롯 없음 등)을 수신하면 즉시 타겟팅 종료
        if (currentState == InputState.WaitingForChoice)
        {
            CleanUpTargetingMode();
        }
    }

    void Update()
    {
        // 1. 현재 내 턴인지 확인합니다.
        bool isMyTurn = BattleManager.Instance == null || BattleManager.Instance.isPlayerTurn;
        bool isFold = HandCardControllManager.instance != null && HandCardControllManager.instance.isFolded;

        // 상대 턴인데 마우스를 쥐고 있거나 드래그 상태라면 강제로 취소시킵니다 (Idle 상태로 복귀).
        if (!isMyTurn && currentState != InputState.Idle && currentState != InputState.WaitingForChoice)
        {
            ResetInput();
        }

        // 2. 상태에 따른 마우스 입력 처리
        switch (currentState)
        {
            case InputState.Idle:
                // Idle 상태에서는 호버링을 해야 하므로 내 턴 여부를 전달합니다. (상대 턴에도 작동)
                HandleIdleAndHover(isMyTurn, isFold);
                break;
            case InputState.ReadyToDrag:
                if (isMyTurn) HandleReadyToDrag(); // 드래그 준비는 내 턴에만
                break;
            case InputState.DraggingHand:
                if (isMyTurn) HandleDraggingHand(); // 손패 드래그도 내 턴에만
                break;
            case InputState.DraggingField:
                if (isMyTurn) HandleDraggingField(); // 공격 조준도 내 턴에만
                break;
            case InputState.WaitingForChoice:
                HandleWaitingForChoice();
                break;
        }
    }


    // =========================================================
    // 1. 평상시 (Idle) : 호버링(Hover) 감지 및 클릭(Down) 대기
    // =========================================================
    // UI 요소 감지용 함수 (클릭 시 힙 할당 방지를 위해 내부 리스트 재사용)
    private List<RaycastResult> GetUIElementsUnderPointer()
    {
        _uiRaycastResults.Clear();
        if (EventSystem.current == null) return _uiRaycastResults;

        if (_cachedPointerData == null)
        {
            _cachedPointerData = new PointerEventData(EventSystem.current);
        }
        _cachedPointerData.position = Input.mousePosition;
        EventSystem.current.RaycastAll(_cachedPointerData, _uiRaycastResults);
        return _uiRaycastResults;
    }

    private void HandleIdleAndHover(bool isMyTurn, bool isFold)
    {
        // 멤버 스킬 선택창이 열려 있으면 3D 입력 및 호버링 중단
        if (MemberSkillSelectUI.Instance != null && MemberSkillSelectUI.Instance.IsOpen)
        {
            CancelFieldMinionHover();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            CancelFieldMinionHover();
            _mouseDownPos = Input.mousePosition;

            // =======================================================
            // 1단계: UI (2D 캔버스 - 손패 카드) 우선 판정
            // =======================================================
            List<RaycastResult> uiHits = GetUIElementsUnderPointer();
            bool isUIClicked = false;

            foreach (RaycastResult hit in uiHits)
            {
                GameCardDisplay cardDisplay = hit.gameObject.GetComponentInParent<GameCardDisplay>();
                if (cardDisplay != null)
                {
                    // 부모 또는 자식의 레이어가 handCardLayer에 포함되어 있는지 확인
                    if (((1 << cardDisplay.gameObject.layer) & handCardLayer) != 0 || ((1 << hit.gameObject.layer) & handCardLayer) != 0)
                    {
                        isUIClicked = true;
                        GameObject cardRoot = cardDisplay.gameObject;

                        // 손패가 접혀있을 때 클릭하면 펼치기 (내 턴/상대 턴 모두 허용)
                        if (HandCardControllManager.instance != null && HandCardControllManager.instance.isFolded)
                        {
                            HandCardControllManager.instance.ToggleHandFold();
                            return;
                        }

                        if (HandCardControllManager.instance != null && HandCardControllManager.instance.isMulliganPhase)
                        {
                            HandCardControllManager.instance.OnMulliganCardClicked(cardRoot);
                            return;
                        }

                        // 손패 카드 드래그는 내 턴에만 허용 (상대 턴에는 드래그 불가)
                        if (isMyTurn)
                        {
                            _selectedHandCard = cardDisplay;

                            if (_selectedHandCard != null)
                            {
                                currentState = InputState.ReadyToDrag;
                                return;
                            }
                        }
                        else
                        {
                            // 상대 턴에는 손패 카드를 클릭해도 드래그를 시작하지 않고 리턴
                            return;
                        }
                    }
                }
            }

            // =======================================================
            // 2단계: UI를 클릭하지 않았다면 3D 물리(Physics) 기반 판정
            // =======================================================
            if (!isUIClicked)
            {
                Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

                // 필드 하수인/리더 클릭 판정 [2]
                if (Physics.Raycast(ray, out RaycastHit minionHit, 100f, minionEntityLayer))
                {
                    if (EntityDetailViewer.Instance != null) EntityDetailViewer.Instance.HideDetail();

                    var clickedDisplay = minionHit.collider.GetComponentInParent<GameCardDisplay>();
                    if (clickedDisplay != null)
                    {
                        // [감정표현] 내 리더를 클릭한 경우 감정표현 메뉴 토글 (내 턴 / 상대 턴 모두 지원)
                        bool isMyLeader = (GameEntityManager.Instance != null && GameEntityManager.Instance.myLeader == clickedDisplay) ||
                                          (clickedDisplay is LeaderCardDisplay && clickedDisplay.CurrentEntityData != null && clickedDisplay.CurrentEntityData.ownerUid == GameEntityManager.Instance?.MyUid);
                        if (isMyLeader)
                        {
                            EmotionManager.Instance?.ToggleEmoteMenu();
                            ResetInput();
                            return;
                        }

                        // 리더가 아닌 다른 필드 유닛을 클릭한 경우 열려있던 감정표현 메뉴 닫기
                        EmotionManager.Instance?.HideEmoteMenu();

                        // 내 턴일 때만 아군 멤버 스킬 팝업 및 공격 조준 드래그 허용
                        if (isMyTurn)
                        {
                            _selectedFieldEntity = clickedDisplay;

                            if (_selectedFieldEntity != null)
                            {
                                // [멤버 체크] 내 필드의 멤버 카드를 클릭한 경우 스킬 선택 팝업을 엽니다.
                                if (IsFriendlyMember(_selectedFieldEntity))
                                {
                                    if (MemberSkillSelectUI.Instance != null)
                                    {
                                        MemberSkillSelectUI.Instance.Open(_selectedFieldEntity);
                                    }
                                    ResetInput();
                                    return;
                                }

                                if (GameEntityManager.Instance != null && GameEntityManager.Instance.test)
                                {
                                    currentState = InputState.ReadyToDrag;
                                    return;
                                }

                                if (EntityAttackManager.Instance != null && EntityAttackManager.Instance.IsFriendlyMinion(_selectedFieldEntity))
                                {
                                    currentState = InputState.ReadyToDrag;
                                }
                            }
                        }
                    }
                    return;
                }
                // 필드 배경 및 빈 슬롯 클릭 판정 [3]
                else if (Physics.Raycast(ray, out RaycastHit fieldHit, 100f, fieldEntityLayer | fieldSlotLayer))
                {
                    EmotionManager.Instance?.HideEmoteMenu();
                    if (EntityDetailViewer.Instance != null) EntityDetailViewer.Instance.HideDetail();

                    if (HandCardControllManager.instance != null && !HandCardControllManager.instance.isMulliganPhase && !HandCardControllManager.instance.isFolded)
                    {
                        HandCardControllManager.instance.ToggleHandFold();
                    }

                    ResetInput();
                    return;
                }
                else
                {
                    // 필드 외부 빈 공간을 클릭한 경우에도 감정표현 메뉴 닫기
                    EmotionManager.Instance?.HideEmoteMenu();
                }
            }
        }
        else // 호버링 감지
        {
            if (HandCardControllManager.instance != null && !isFold)
            {
                HandCardControllManager.instance.ProcessHover(Input.mousePosition);
            }

            // 필드 하수인 1초 호버 프리뷰 감지
            ProcessFieldMinionHover();
        }

        // 우클릭 상세정보 창 띄우기 (3D 기반 유지)
        if (Input.GetMouseButtonDown(1))
        {
            CancelFieldMinionHover();
            Debug.Log("우클릭");

            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit minionHit, 100f, minionEntityLayer))
            {
                GameCardDisplay targetCard = minionHit.collider.GetComponentInParent<GameCardDisplay>();
                if (targetCard != null && EntityDetailViewer.Instance != null)
                {
                    EntityDetailViewer.Instance.ShowDetail(targetCard);
                    Debug.Log("상세 정보");
                }
            }
        }
    }

    /// <summary>
    /// 마우스가 필드 하수인 위에 1초 이상 머물렀을 때 DeckCardPreviewManager로 원본 카드 프리뷰를 띄웁니다.
    /// </summary>
    private void ProcessFieldMinionHover()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit minionHit, 100f, minionEntityLayer))
        {
            GameCardDisplay targetCard = minionHit.collider.GetComponentInParent<GameCardDisplay>();
            if (targetCard != null && targetCard._cardData != null)
            {
                if (_hoveredFieldCard == targetCard)
                {
                    _fieldHoverTimer += Time.deltaTime;
                    if (_fieldHoverTimer >= FIELD_HOVER_DELAY && !_hasShownFieldPreview)
                    {
                        _hasShownFieldPreview = true;
                        if (DeckCardPreviewManager.Instance != null)
                        {
                            DeckCardPreviewManager.Instance.ShowPreview(targetCard._cardData, targetCard.transform.position, targetCard);
                        }
                    }
                }
                else
                {
                    CancelFieldMinionHover();
                    _hoveredFieldCard = targetCard;
                }
                return;
            }
        }

        CancelFieldMinionHover();
    }

    private void CancelFieldMinionHover()
    {
        if (_hasShownFieldPreview)
        {
            DeckCardPreviewManager.Instance?.HidePreview();
        }
        _hoveredFieldCard = null;
        _fieldHoverTimer = 0f;
        _hasShownFieldPreview = false;
    }

    // =========================================================
    // 2. 누른 상태 (ReadyToDrag) : 진짜로 드래그하는지 확인
    // =========================================================
    private void HandleReadyToDrag()
    {
        // 마우스를 떼버리면 취소 (클릭만 한 경우)
        if (Input.GetMouseButtonUp(0))
        {
            ResetInput();
            return;
        }

        // 드래그 거리 확인
        if (Vector2.Distance(_mouseDownPos, Input.mousePosition) > dragThreshold)
        {
            // 잡고 있는 대상에 따라 상태 분리
            if (_selectedHandCard != null)
            {
                currentState = InputState.DraggingHand;

                // [연동 완료] CardDragManager에게 드래그 시작 명령
                if (CardDragManager.instance != null)
                    CardDragManager.instance.StartDrag(_selectedHandCard.gameObject);
            }
            else if (_selectedFieldEntity != null)
            {
                // 실제 드래그 시도 순간 공격 가능 여부 판정
                bool canAttack = EntityAttackManager.Instance != null && EntityAttackManager.Instance.CanAttack(_selectedFieldEntity);

                if (canAttack)
                {
                    currentState = InputState.DraggingField;

                    // [연동 완료] EntityAttackManager에게 공격 조준 시작 명령
                    if (EntityAttackManager.Instance != null)
                    {
                        EntityAttackManager.Instance.StartAttackDrag(_selectedFieldEntity);
                    }
                }
                else
                {
                    // 공격 불가 상태에서 드래그 시도:
                    // 1. 로딩씬의 SoundManager를 통해 경고음 재생
                    if (SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlayUISound(UIButtonSoundType.Warning);
                    }

                    // 2. 조준선 생성하지 않고 입력 즉시 취소
                    ResetInput();
                }
            }
        }
    }

    // =========================================================
    // 3. 손패 드래그 중 (DraggingHand)
    // =========================================================
    private void HandleDraggingHand()
    {
        // 우클릭: 드래그 / 타겟팅 조준 즉시 취소
        if (Input.GetMouseButtonDown(1))
        {
            if (CardDragManager.instance != null)
                CardDragManager.instance.CancelDrag();

            ResetInput();
            return;
        }

        if (Input.GetMouseButtonUp(0))
        {
            // [연동 완료] CardDragManager에게 드래그 종료 명령
            if (CardDragManager.instance != null)
                CardDragManager.instance.EndDrag();

            ResetInput();
        }
    }

    // =========================================================
    // 4. 필드 공격 조준 중 (DraggingField)
    // =========================================================
    private void HandleDraggingField()
    {
        // 우클릭: 공격 조준 즉시 취소 (공중 부양 즉시 해제)
        if (Input.GetMouseButtonDown(1))
        {
            if (EntityAttackManager.Instance != null)
                EntityAttackManager.Instance.ResetState(false);

            ResetInput();
            return;
        }

        // [연동 완료] 조준선 갱신 및 타겟 하이라이트 (매 프레임 실행)
        if (EntityAttackManager.Instance != null)
        {
            EntityAttackManager.Instance.UpdateTargetHighlight();
        }

        if (Input.GetMouseButtonUp(0))
        {
            // [연동 완료] 공격 실행 및 상태 초기화 명령
            if (EntityAttackManager.Instance != null)
                EntityAttackManager.Instance.TryCompleteAttack();

            ResetInput();
        }
    }

    // =========================================================
    // 5. 대상 선택 대기
    // =========================================================

    /// <summary>
    /// 서버로부터 S_RequestChoice 패킷을 받았을 때 외부에서 호출합니다.
    /// </summary>
    public void StartChoiceMode(string choiceType, int sourceEntityId)
    {
        currentState = InputState.WaitingForChoice;
        _currentChoiceType = choiceType;
        _choiceSourceEntityId = sourceEntityId;

        // 조준선(화살표) 활성화
        if (TargetingReticle.Instance != null)
        {
            Transform startTransform = null;

            // 1. 방금 하수인을 내려놓은 필드 슬롯 위치 우선 확인 (하수인이 소환될 슬롯에서 화살표 출발)
            if (CardDragManager.instance != null && CardDragManager.instance.LastPlayedSlotIndex >= 0)
            {
                int slotIdx = CardDragManager.instance.LastPlayedSlotIndex;
                if (GameEntityManager.Instance != null && GameEntityManager.Instance.myFieldSlots != null && slotIdx < GameEntityManager.Instance.myFieldSlots.Length)
                {
                    startTransform = GameEntityManager.Instance.myFieldSlots[slotIdx].transform;
                }
            }

            // 2. 이미 필드에 생성된 하수인 오브젝트가 있다면 해당 위치 사용
            if (startTransform == null && GameEntityManager.Instance != null && GameEntityManager.Instance._spawnedEntities.TryGetValue(sourceEntityId, out var sourceCard))
            {
                if (sourceCard != null) startTransform = sourceCard.transform;
            }

            // 3. 없으면 내 리더 위치 사용
            if (startTransform == null && GameEntityManager.Instance != null && GameEntityManager.Instance.myLeader != null)
            {
                startTransform = GameEntityManager.Instance.myLeader.transform;
            }

            if (startTransform != null)
            {
                TargetingReticle.Instance.StartTargeting(startTransform);
            }
        }

        // 선택 모드 진입 시 손패 접기 (필드/슬롯/리더 시야 확보)
        if (HandCardControllManager.instance != null && !HandCardControllManager.instance.isFolded)
        {
            HandCardControllManager.instance.FoldHand();
        }
    }

    private void HandleWaitingForChoice()
    {
        // 만약 일반 Choice 모드가 아니라 전투의 함성 타겟 대기중인 패킷이 있다면 여기서 처리합니다.
        if (_pendingTargetPacket != null)
        {
            // 좌클릭: 대상 확정
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit minionHit, 100f, minionEntityLayer))
                {
                    GameCardDisplay targetCard = minionHit.collider.GetComponentInParent<GameCardDisplay>();
                    if (targetCard != null)
                    {
                        // 서버가 보낸 유효 타겟 목록 검증 후 전송
                        if (_pendingTargetPacket.ValidTargetIds != null && _pendingTargetPacket.ValidTargetIds.Contains(targetCard.EntityId))
                        {
                            GameClient.Instance.SendTargetReauest(_pendingTargetPacket.CardEntityId, targetCard.EntityId);

                            // 타겟팅 완료되었으므로 정리
                            BattleManager.Instance.ResetHighlights();
                            CleanUpTargetingMode();
                        }
                    }
                }
            }
            // 우클릭: 조준 취소 (또는 손패 복구)
            else if (Input.GetMouseButtonDown(1))
            {
                GameClient.Instance.SendTargetReauest(_pendingTargetPacket.CardEntityId, -1);
                BattleManager.Instance.ResetHighlights();
                CleanUpTargetingMode();
            }
            return; // 일반 Choice 모드 조건 타지 않게 예방
        }

        // 좌클릭 시 대상 또는 위치 선택 확정
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

            // 1. 토큰 소환 위치 선택일 경우 (FieldSlot 감지)
            if (_currentChoiceType == "POSITION")
            {
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, fieldSlotLayer))
                {
                    FieldSlot slot = hit.collider.GetComponent<FieldSlot>();
                    bool isMySlot = GameEntityManager.Instance != null &&
                                    GameEntityManager.Instance.myFieldSlots != null &&
                                    System.Array.IndexOf(GameEntityManager.Instance.myFieldSlots, slot) >= 0;
                    if (slot != null && !slot.IsOccupied && isMySlot) // 내 빈 자리일 때만 허용
                    {
                        GameClient.Instance.SendMakeChoiceRequest(slot.slotIndex, null, -1);
                        CleanUpTargetingMode();
                        ResetInput();
                    }
                }
            }
            // 2. 주문 등의 다중/단일 타겟 선택일 경우 (Entity 감지)
            else if (_currentChoiceType == "TARGET")
            {
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, minionEntityLayer))
                {
                    GameCardDisplay targetCard = hit.collider.GetComponentInParent<GameCardDisplay>();
                    if (targetCard != null)
                    {
                        GameClient.Instance.SendMakeChoiceRequest(-1, null, targetCard.EntityId);
                        CleanUpTargetingMode();
                        ResetInput();
                    }
                }
            }
            // 3. 멤버 스킬 타겟 선택일 경우
            else if (_currentChoiceType == "MEMBER_SKILL")
            {
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, minionEntityLayer))
                {
                    GameCardDisplay targetCard = hit.collider.GetComponentInParent<GameCardDisplay>();
                    if (targetCard != null)
                    {
                        // 유효 타겟 목록이 있을 경우 검증
                        if (_validMemberSkillTargets == null || _validMemberSkillTargets.Count == 0 || _validMemberSkillTargets.Contains(targetCard.EntityId))
                        {
                            Debug.Log($"[GameInputManager] 멤버 스킬 타겟 확정: SourceId={_memberSkillSourceEntityId}, SkillId={_memberSkillPendingSkillId}, TargetId={targetCard.EntityId}");
                            if (GameEntityManager.Instance != null && GameEntityManager.Instance._spawnedEntities.TryGetValue(_memberSkillSourceEntityId, out var srcDisplay))
                            {
                                if (srcDisplay.CurrentEntityData != null)
                                {
                                    srcDisplay.CurrentEntityData.hasUsedSkillThisTurn = true;
                                }
                            }
                            GameClient.Instance?.SendUseMemberSkill(_memberSkillSourceEntityId, _memberSkillPendingSkillId, targetCard.EntityId);
                            CleanUpTargetingMode();
                            ResetInput();
                        }
                    }
                }
            }
        }

        // 우클릭 시 멤버 스킬 조준 취소
        if (Input.GetMouseButtonDown(1))
        {
            if (_currentChoiceType == "MEMBER_SKILL")
            {
                Debug.Log("[GameInputManager] 멤버 스킬 조준 취소");
                CleanUpTargetingMode();
                ResetInput();
            }
        }
    }

    public void HandleRequestTargetForPlay(S_RequestTargetForPlay packet)
    {
        // 1. 패킷 데이터를 멤버 변수에 고이 보관합니다.
        _pendingTargetPacket = packet;

        // 2. 조준선이 시작될 3D 위치를 구합니다.
        Transform spawnSlotTransform = null;
        if (GameEntityManager.Instance != null && GameEntityManager.Instance.myFieldSlots != null &&
            packet.position >= 0 && packet.position < GameEntityManager.Instance.myFieldSlots.Length)
        {
            spawnSlotTransform = GameEntityManager.Instance.myFieldSlots[packet.position].transform;
        }
        else if (GameEntityManager.Instance != null && GameEntityManager.Instance.myLeader != null)
        {
            spawnSlotTransform = GameEntityManager.Instance.myLeader.transform;
        }
        else
        {
            spawnSlotTransform = transform;
        }

        // 3. 입력 상태를 대상 지정 대기 상태로 전환
        currentState = InputState.WaitingForChoice;

        // 4. 조준선 및 하이라이트 켜기
        if (TargetingReticle.Instance != null)
        {
            TargetingReticle.Instance.StartTargeting(spawnSlotTransform);
        }

        if (packet.ValidTargetIds != null)
        {
            BattleManager.Instance.OnReceiveValidTargetsRequestTargetForPlay(packet);
        }

        // 전투의 함성 타겟팅 시 손패 접기 (필드/리더 시야 확보)
        if (HandCardControllManager.instance != null && !HandCardControllManager.instance.isFolded)
        {
            HandCardControllManager.instance.FoldHand();
        }
    }

    // =========================================================
    // 공통: 입력 상태 초기화
    // =========================================================

    private void CleanUpTargetingMode()
    {
        if (TargetingReticle.Instance != null)
            TargetingReticle.Instance.StopTargeting(); // 조준선 끄기

        _pendingTargetPacket = null; // 패킷 비우기
        _currentChoiceType = "";
        _choiceSourceEntityId = -1;
        _memberSkillSourceEntityId = 0;
        _memberSkillPendingSkillId = 0;
        if (_validMemberSkillTargets != null) _validMemberSkillTargets.Clear();

        //if (BattleManager.Instance != null)
            //BattleManager.Instance.ResetHighlights();

        // 접혀있던 손패 다시 펼치기
        if (HandCardControllManager.instance != null && HandCardControllManager.instance.isFolded)
        {
            HandCardControllManager.instance.SpreadHand();
        }

        ResetInput(); // currentState를 Idle 상태로 복구
    }

    /// <summary>
    /// 멤버 스킬 타겟팅 모드를 시작합니다.
    /// </summary>
    public void StartMemberSkillTargeting(int sourceEntityId, MemberSkillData skill, Transform sourceTransform)
    {
        _currentChoiceType = "MEMBER_SKILL";
        _choiceSourceEntityId = sourceEntityId;
        _memberSkillSourceEntityId = sourceEntityId;
        _memberSkillPendingSkillId = skill != null ? skill.skillId : 0;
        if (_validMemberSkillTargets == null) _validMemberSkillTargets = new List<int>();
        _validMemberSkillTargets.Clear();

        currentState = InputState.WaitingForChoice;

        if (TargetingReticle.Instance != null && sourceTransform != null)
        {
            TargetingReticle.Instance.StartTargeting(sourceTransform);
        }

        // 서버에 유효 조준 대상 목록 요청
        GameClient.Instance?.SendValidMemberSkillTargetsRequest(sourceEntityId, _memberSkillPendingSkillId);

        // 손패 접기 (시야 확보)
        if (HandCardControllManager.instance != null && !HandCardControllManager.instance.isFolded)
        {
            HandCardControllManager.instance.FoldHand();
        }
    }

    /// <summary>
    /// 클릭한 개체가 내 필드의 멤버 카드인지 검사합니다.
    /// </summary>
    private bool IsFriendlyMember(GameCardDisplay entity)
    {
        if (entity == null) return false;

        bool isMemberCard = (entity._cardData != null && entity._cardData.cardType == CardType.멤버);
        bool isMemberEntity = (entity.CurrentEntityData != null && entity.CurrentEntityData.isMember);

        if (!isMemberCard && !isMemberEntity) return false;

        // 1. 내 멤버 슬롯에 위치하는지 검사
        if (GameEntityManager.Instance != null && GameEntityManager.Instance.myMemberSlots != null)
        {
            foreach (var slot in GameEntityManager.Instance.myMemberSlots)
            {
                if (slot != null && entity.transform.IsChildOf(slot.transform))
                {
                    return true;
                }
            }
        }

        // 2. 소유자 UID 검사
        if (entity.CurrentEntityData != null && !string.IsNullOrEmpty(entity.CurrentEntityData.ownerUid))
        {
            string myUid = GameClient.Instance != null ? GameClient.Instance.UserUid : "";
            if (!string.IsNullOrEmpty(myUid) && entity.CurrentEntityData.ownerUid == myUid)
            {
                return true;
            }
        }

        return false;
    }

    public void ResetInput()
    {
        CancelFieldMinionHover();
        if (CardDragManager.instance != null)
            CardDragManager.instance.CancelDrag();
        if (EntityAttackManager.Instance != null)
            EntityAttackManager.Instance.ResetState(false);
        currentState = InputState.Idle;
        _selectedHandCard = null;
        _selectedFieldEntity = null;
    }
}