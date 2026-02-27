using UnityEngine;

/// <summary>
/// 내 하수인을 드래그하여 적을 공격합니다.
/// [수정됨] GameInputManager에 의해 수동(Passive)으로 제어되도록 변경되었습니다.
/// 스스로 입력을 감지하는 Update()와 HandleInput()이 삭제되었습니다.
/// </summary>
public class EntityAttackManager : MonoBehaviour
{
    public static EntityAttackManager Instance;

    [Header("설정")]
    public LayerMask entityLayer;

    // --- 상태 변수 ---
    private GameCardDisplay _currentAttacker;   // 공격하는 내 하수인
    private GameCardDisplay _currentTargetInfo; // 조준 당하고 있는 적 하수인

    private Camera _mainCamera;

    private string MyUid => GameClient.Instance != null ? GameClient.Instance.UserUid : "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _mainCamera = Camera.main;
    }

    // --- 로직: 드래그 시작 (GameInputManager에서 호출) ---
    public void StartAttackDrag(GameCardDisplay attacker)
    {
        _currentAttacker = attacker;

        // 1. 화살표 켜기
        if (TargetingReticle.Instance != null)
        {
            TargetingReticle.Instance.StartTargeting(_currentAttacker.transform);
        }

        // 2. [연출] 공격자(내 카드) 공중 부양!
        _currentAttacker.SetFloatingState(true);

        Debug.Log($"[Attack] {_currentAttacker.name} 공격 태세 돌입");
    }

    // --- 로직: 드래그 중 타겟 갱신 (GameInputManager에서 매 프레임 호출) ---
    public void UpdateTargetHighlight()
    {
        if (_currentAttacker == null) return;

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

    // --- 로직: 공격 확정 (GameInputManager에서 호출) ---
    public void TryCompleteAttack()
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

        ResetState();
    }

    // --- 로직: 상태 초기화 (원상복구) ---
    public void ResetState()
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

        // 3. 화살표 끄기
        if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();
    }

    // --- 검증 로직 (GameInputManager에서도 사용하므로 public으로 변경) ---
    public bool IsValidAttacker(GameCardDisplay display)
    {
        if (display == null) return false;
        var data = display.CurrentEntityData;

        // 내 하수인인지 확인
        if (data == null || data.ownerUid != MyUid)
        {
            Debug.Log("플레이어 하수인이 아님");
            return false;
        }

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