using UnityEngine;

/// <summary>
/// 게임 내의 모든 마우스 입력(Hover, Click, Drag)을 중앙에서 관리하는 스크립트입니다.
/// 
/// [연동 완료]
/// - HandInteractionManager의 호버링 기능 (ProcessHover)
/// - HandInteractionManager의 멀리건 클릭 기능 (OnMulliganCardClicked)
/// </summary>
public class GameInputManager : MonoBehaviour
{
    public static GameInputManager Instance;

    [Header("테스트")]
    //public DummyEntitySetup dummy;

    [Header("레이어 설정 (우선순위)")]
    [Tooltip("손패 카드 레이어 (가장 먼저 클릭 판정)")]
    public LayerMask handCardLayer;
    [Tooltip("필드 하수인/영웅 레이어 (손패 다음으로 클릭 판정)")]
    public LayerMask minionEntityLayer;
    [Tooltip("필드 레이어 (하수인 다음으로 클릭 판정)")]
    public LayerMask fieldEntityLayer;

    [Header("드래그 설정")]
    public float dragThreshold = 10f; // 이만큼 움직여야 드래그로 인정

    // --- 상태 관리를 위한 열거형(Enum) ---
    public enum InputState
    {
        Idle,           // 아무것도 안 함 (호버링 중)
        ReadyToDrag,    // 마우스를 꾹 눌렀으나 아직 안 움직임
        DraggingHand,   // 손패 카드를 드래그 중
        DraggingField   // 필드 하수인을 드래그 중 (공격 조준)
    }

    [Header("현재 상태 (디버그용)")]
    public InputState currentState = InputState.Idle;

    // --- 내부 변수 ---
    private Camera _mainCamera;
    private Vector2 _mouseDownPos;

    // 현재 선택된 대상들
    private GameCardDisplay _selectedHandCard; // 드래그하려고 잡은 손패 카드
    private GameCardDisplay _selectedFieldEntity; // 공격하려고 잡은 필드 하수인

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _mainCamera = Camera.main;
    }

    void Update()
    {
        // 1. 현재 내 턴인지 확인합니다.
        bool isMyTurn = GameStateManager.Instance == null || GameStateManager.Instance.IsMyTurn;
        bool isFold = HandInteractionManager.instance.isFolded;

        // 상대 턴인데 마우스를 쥐고 있거나 드래그 상태라면 강제로 취소시킵니다 (Idle 상태로 복귀).
        if (!isMyTurn && currentState != InputState.Idle)
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
        }
    }

    // =========================================================
    // 1. 평상시 (Idle) : 호버링(Hover) 감지 및 클릭(Down) 대기
    // =========================================================
    private void HandleIdleAndHover(bool isMyTurn, bool isFold)
    {
        // [클릭 감지] - "내 턴일 때만" 카드를 클릭해서 잡을 수 있도록 isMyTurn 조건 추가!
        if (isMyTurn && Input.GetMouseButtonDown(0))
        {
            _mouseDownPos = Input.mousePosition;

            // 광선을 쏴서 무엇을 클릭했는지 확인합니다. (손패 우선!)
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit handHit, 100f, handCardLayer))
            {
                // 손패가 접힌 상태라면 어떤 카드를 클릭해도 손패를 펼칩니다.
                if (HandInteractionManager.instance.isFolded)
                {
                    HandInteractionManager.instance.ToggleHandFold();
                    return;
                }

                GameObject clickedObject = handHit.collider.gameObject;

                // [연동 완료] 멀리건 단계인지 확인하고 맞으면 멀리건 클릭으로 넘김
                if (HandInteractionManager.instance != null && HandInteractionManager.instance.isMulliganPhase)
                {
                    HandInteractionManager.instance.OnMulliganCardClicked(clickedObject);
                    return; // 멀리건 중이면 카드를 드래그할 수 없으므로 여기서 종료
                }

                // 멀리건이 아닐 경우 일반적인 드래그 준비 상태로 진입
                _selectedHandCard = clickedObject.GetComponent<GameCardDisplay>();
                if (_selectedHandCard != null)
                {
                    currentState = InputState.ReadyToDrag;
                    return; // 손패를 눌렀으면 필드는 검사할 필요 없음
                }
            }
            else if (Physics.Raycast(ray, out RaycastHit minionHit, 100f, minionEntityLayer))
            {
                Debug.Log("하수인 클릭");
                EntityDetailViewer.Instance.HideDetail();

                // 필드 하수인을 클릭함 
                // 테스트
                //if(GameEntityManager.Instance.test)
                //{
                //    dummy = minionHit.collider.GetComponent<DummyEntitySetup>();
                //  }
                //else
                {
                    _selectedFieldEntity = minionHit.collider.GetComponent<GameCardDisplay>();
                }

                if (_selectedFieldEntity != null)
                {
                    // 테스트
                    if(GameEntityManager.Instance.test)
                    {
                        currentState = InputState.ReadyToDrag;
                        return;
                    }
                    // [연동 완료] 내 하수인이 맞는지, 공격 가능한 상태인지 검사
                    if (EntityAttackManager.Instance != null && EntityAttackManager.Instance.IsValidAttacker(_selectedFieldEntity))
                    {
                        currentState = InputState.ReadyToDrag;
                    }
                }
            }
            else if(Physics.Raycast(ray, out RaycastHit fieldHit, 100f, fieldEntityLayer))
            {
                Debug.Log("필드 클릭");
                EntityDetailViewer.Instance.HideDetail();

                if (!HandInteractionManager.instance.isFolded)
                {
                    HandInteractionManager.instance.ToggleHandFold();
                }

                ResetInput();
            }
        }
        // [호버링 감지] - 클릭하지 않고 마우스만 움직일 때 (상대 턴에도 항상 동작)
        else
        {
            // [연동 완료] HandInteractionManager에게 현재 마우스 위치를 던져줌
            if (HandInteractionManager.instance != null && !isFold)
            {
                HandInteractionManager.instance.ProcessHover(Input.mousePosition);
            }
        }

        // 우클릭시 상세정보 창 띄우기
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

            // 필드 하수인을 우클릭했는지 검사
            if (Physics.Raycast(ray, out RaycastHit fieldHit, 100f, minionEntityLayer))
            {
                GameCardDisplay targetCard = fieldHit.collider.GetComponent<GameCardDisplay>();
                if (targetCard != null && EntityDetailViewer.Instance != null)
                {
                    EntityDetailViewer.Instance.ShowDetail(targetCard);
                }
            }
        }
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
                Debug.Log($"[InputManager] 손패 드래그 시작: {_selectedHandCard.name}");

                // [연동 완료] CardDragManager에게 드래그 시작 명령
                if (CardDragManager.instance != null)
                    CardDragManager.instance.StartDrag(_selectedHandCard.gameObject);
            }
            else if (_selectedFieldEntity != null)
            {
                currentState = InputState.DraggingField;
                Debug.Log($"[InputManager] 필드 공격 조준 시작: {_selectedFieldEntity.name}");

                // [연동 완료] EntityAttackManager에게 공격 조준 시작 명령
                if (EntityAttackManager.Instance != null)
                    EntityAttackManager.Instance.StartAttackDrag(_selectedFieldEntity);
            }
        }
    }

    // =========================================================
    // 3. 손패 드래그 중 (DraggingHand)
    // =========================================================
    private void HandleDraggingHand()
    {
        if (Input.GetMouseButtonUp(0))
        {
            Debug.Log($"[InputManager] 손패 드래그 종료 (필드에 소환 시도)");

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
        // [연동 완료] 조준선 갱신 및 타겟 하이라이트 (매 프레임 실행)
        if (EntityAttackManager.Instance != null)
        {
            EntityAttackManager.Instance.UpdateTargetHighlight();
        }

        // 테스트
        if(GameEntityManager.Instance.test)
        {

        }

        if (Input.GetMouseButtonUp(0))
        {
            Debug.Log($"[InputManager] 필드 공격 조준 종료 (공격 실행 시도)");

            // [연동 완료] 공격 실행 및 상태 초기화 명령
            if (EntityAttackManager.Instance != null)
                EntityAttackManager.Instance.TryCompleteAttack();

            ResetInput();
        }
    }

    // =========================================================
    // 공통: 입력 상태 초기화
    // =========================================================
    public void ResetInput()
    {
        currentState = InputState.Idle;
        _selectedHandCard = null;
        _selectedFieldEntity = null;
    }
}