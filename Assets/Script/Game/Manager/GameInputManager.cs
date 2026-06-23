using UnityEngine;
using UnityEngine.EventSystems; // UI 이벤트 시스템 처리를 위해 필수
using System.Collections.Generic;
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
    [SerializeField]
    private GameCardDisplay _selectedHandCard; // 드래그하려고 잡은 손패 카드
    [SerializeField]
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
        bool isFold = HandCardControllManager.instance.isFolded;

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
    // UI 요소 감지용 함수
    private List<RaycastResult> GetUIElementsUnderPointer()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        return results;
    }

    private void HandleIdleAndHover(bool isMyTurn, bool isFold)
    {
        if (isMyTurn && Input.GetMouseButtonDown(0))
        {
            _mouseDownPos = Input.mousePosition;

            // =======================================================
            // 1단계: UI (2D 캔버스 - 손패 카드) 우선 판정
            // =======================================================
            List<RaycastResult> uiHits = GetUIElementsUnderPointer();
            bool isUIClicked = false;

            foreach (RaycastResult hit in uiHits)
            {
                if (((1 << hit.gameObject.layer) & handCardLayer) != 0)
                {
                    isUIClicked = true;

                    if (HandCardControllManager.instance.isFolded)
                    {
                        HandCardControllManager.instance.ToggleHandFold();
                        return;
                    }

                    if (HandCardControllManager.instance != null && HandCardControllManager.instance.isMulliganPhase)
                    {
                        HandCardControllManager.instance.OnMulliganCardClicked(hit.gameObject);
                        return;
                    }

                    _selectedHandCard = hit.gameObject.GetComponentInParent<GameCardDisplay>();
                    if (_selectedHandCard != null)
                    {
                        currentState = InputState.ReadyToDrag;
                        return;
                    }
                }
            }

            // =======================================================
            // 2단계: UI를 클릭하지 않았다면 3D 물리(Physics) 기반 판정
            // =======================================================
            if (!isUIClicked)
            {
                Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

                // 필드 하수인 클릭 판정 [2]
                if (Physics.Raycast(ray, out RaycastHit minionHit, 100f, minionEntityLayer))
                {
                    if (EntityDetailViewer.Instance != null) EntityDetailViewer.Instance.HideDetail();

                    _selectedFieldEntity = minionHit.collider.GetComponentInParent<GameCardDisplay>();

                    if (_selectedFieldEntity != null)
                    {
                        if (GameEntityManager.Instance != null && GameEntityManager.Instance.test)
                        {
                            currentState = InputState.ReadyToDrag;
                            return;
                        }

                        if (EntityAttackManager.Instance != null && EntityAttackManager.Instance.IsValidAttacker(_selectedFieldEntity))
                        {
                            currentState = InputState.ReadyToDrag;
                        }
                    }
                    return;
                }
                // 필드 배경 클릭 판정 [3]
                else if (Physics.Raycast(ray, out RaycastHit fieldHit, 100f, fieldEntityLayer) && !HandCardControllManager.instance.isMulliganPhase)
                {
                    if (EntityDetailViewer.Instance != null) EntityDetailViewer.Instance.HideDetail();

                    if (!HandCardControllManager.instance.isFolded)
                    {
                        HandCardControllManager.instance.ToggleHandFold();
                    }

                    ResetInput();
                    return;
                }
            }
        }
        else // 호버링 감지
        {
            if (HandCardControllManager.instance != null && !isFold)
            {
                HandCardControllManager.instance.ProcessHover(Input.mousePosition);
            }
        }

        // 우클릭 상세정보 창 띄우기 (3D 기반 유지)
        if (Input.GetMouseButtonDown(1))
        {
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
                currentState = InputState.DraggingField;

                // [연동 완료] EntityAttackManager에게 공격 조준 시작 명령
                if (EntityAttackManager.Instance != null)
                {
                    EntityAttackManager.Instance.StartAttackDrag(_selectedFieldEntity);
                }
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