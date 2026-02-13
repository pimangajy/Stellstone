using UnityEngine;

/// <summary>
/// 내 하수인을 드래그하여 적을 공격합니다.
/// - 드래그 시작 시: 내 하수인이 붕 떠오름 (SetFloatingState)
/// - 적 조준 시: 적 하수인이 빛남 (SetGlowState)
/// </summary>
public class EntityAttackManager : MonoBehaviour
{
    public static EntityAttackManager Instance;

    [Header("설정")]
    public LayerMask entityLayer;
    public float dragThreshold = 10f;

    // --- 상태 변수 ---
    private GameCardDisplay _currentAttacker;   // 공격하는 내 하수인
    private GameCardDisplay _currentTargetInfo; // 조준 당하고 있는 적 하수인

    private bool _isDragging = false;
    private Vector2 _mouseDownPos;
    private Camera _mainCamera;

    private string MyUid => GameClient.Instance != null ? GameClient.Instance.UserUid : "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _mainCamera = Camera.main;
    }

    void Update()
    {
        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsMyTurn) return;

        HandleInput();
    }

    private void HandleInput()
    {
        // 1. 마우스 누름
        if (Input.GetMouseButtonDown(0))
        {
            _mouseDownPos = Input.mousePosition;
            TrySelectAttacker();
        }

        // 2. 마우스 누르고 있음 (드래그)
        if (Input.GetMouseButton(0) && _currentAttacker != null)
        {
            if (!_isDragging)
            {
                // 드래그 시작 감지
                float distance = Vector2.Distance(_mouseDownPos, Input.mousePosition);
                if (distance > dragThreshold)
                {
                    StartAttackDrag();
                }
            }
            else
            {
                // 이미 드래그 중 -> 적 하이라이트 갱신
                UpdateTargetHighlight();
            }
        }

        // 3. 마우스 뗌 (공격 또는 취소)
        if (Input.GetMouseButtonUp(0))
        {
            if (_isDragging)
            {
                TryCompleteAttack();
            }
            ResetState();
        }
    }

    // --- 로직: 공격자 선택 ---
    private void TrySelectAttacker()
    {
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, entityLayer))
        {
            GameCardDisplay cardDisplay = hit.collider.GetComponent<GameCardDisplay>();
            if (IsValidAttacker(cardDisplay))
            {
                _currentAttacker = cardDisplay;
                // (선택 사항) 클릭하자마자 살짝 반응을 주고 싶다면 여기서 Floating을 켜도 됩니다.
                // 하지만 보통 드래그가 확실해질 때 띄우는 게 자연스럽습니다.
            }
        }
    }

    // --- 로직: 드래그 시작 (내 카드 띄우기) ---
    private void StartAttackDrag()
    {
        _isDragging = true;

        // 1. 화살표 켜기
        if (TargetingReticle.Instance != null)
        {
            TargetingReticle.Instance.StartTargeting(_currentAttacker.transform);
        }

        // 2. [연출] 공격자(내 카드) 공중 부양!
        _currentAttacker.SetFloatingState(true);

        Debug.Log($"[Attack] {_currentAttacker.name} 공격 태세 돌입");
    }

    // --- 로직: 드래그 중 (적 카드 빛나게 하기) ---
    private void UpdateTargetHighlight()
    {
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        GameCardDisplay hitCard = null;

        // 마우스 아래 적이 있는지 탐색
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, entityLayer))
        {
            GameCardDisplay tempCard = hit.collider.GetComponent<GameCardDisplay>();
            if (IsValidTarget(tempCard))
            {
                hitCard = tempCard;
            }
        }

        // 대상이 바뀌었는지 체크
        if (_currentTargetInfo != hitCard)
        {
            // 이전 타겟: 빛 끄기
            if (_currentTargetInfo != null)
            {
                _currentTargetInfo.SetGlowState(false);
            }

            // 새 타겟: 빛 켜기
            if (hitCard != null)
            {
                hitCard.SetGlowState(true);
            }

            _currentTargetInfo = hitCard;
        }
    }

    // --- 로직: 공격 확정 ---
    private void TryCompleteAttack()
    {
        // 마지막으로 타겟 확인
        if (_currentTargetInfo != null && IsValidTarget(_currentTargetInfo))
        {
            int attackerId = _currentAttacker.EntityId;
            int targetId = _currentTargetInfo.EntityId;

            Debug.Log($"[Attack] 공격 전송: {attackerId} -> {targetId}");

            if (GameClient.Instance != null)
            {
                GameClient.Instance.SendAttackRequest(attackerId, targetId);
            }
        }
    }

    // --- 로직: 상태 초기화 (원상복구) ---
    private void ResetState()
    {
        // 1. 타겟 빛 끄기
        if (_currentTargetInfo != null)
        {
            _currentTargetInfo.SetGlowState(false);
            _currentTargetInfo = null;
        }

        // 2. 공격자(내 카드) 착륙시키기
        if (_currentAttacker != null)
        {
            _currentAttacker.SetFloatingState(false);
            _currentAttacker = null;
        }

        _isDragging = false;

        // 3. 화살표 끄기
        if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();
    }

    // --- 검증 로직 ---
    private bool IsValidAttacker(GameCardDisplay display)
    {
        if (display == null) return false;
        var data = display.CurrentEntityData;

        // 내 하수인인지 확인
        if (data == null || data.ownerUid != MyUid) return false;

        // (추후) 공격 가능 상태인지 확인: if (!data.canAttack) return false;

        return true;
    }

    private bool IsValidTarget(GameCardDisplay target)
    {
        if (target == null) return false;
        if (target == _currentAttacker) return false; // 자해 불가

        var data = target.CurrentEntityData;
        // 아군 공격 불가
        if (data != null && data.ownerUid == MyUid) return false;

        return true;
    }
}