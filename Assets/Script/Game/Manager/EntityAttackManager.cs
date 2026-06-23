using System.Collections.Generic;
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
    // 드래그 중 하이라이트가 켜진 타겟들을 저장해둘 리스트
    private List<GameCardDisplay> _highlightedTargets = new List<GameCardDisplay>();

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

        // 3. [추가] 타겟팅 시작 즉시 모든 유효한 대상 하이라이트 켜기
        HighlightAllValidTargets();
    }

    // 필드의 모든 카드를 확인하여 타겟팅 가능한 대상만 하이라이트 표시
    private void HighlightAllValidTargets()
    {
        _highlightedTargets.Clear();

        // 필드 위의 모든 GameCardDisplay 오브젝트를 찾습니다.
        List<GameCardDisplay> allCards = new List<GameCardDisplay>(GameEntityManager.Instance._spawnedEntities.Values);

        foreach (var targetCard in allCards)
        {
            // IsValidAttackTarget 검증을 통과한 유효한 적(타겟)인 경우
            if (CardTargetingManager.Instance.IsValidAttackTarget(_currentAttacker, targetCard))
            {
                targetCard.SetGlowState(true);          // 하이라이트 켜기
                _highlightedTargets.Add(targetCard);    // 초기화 시 끄기 위해 리스트에 보관
            }
        }
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
            if (CardTargetingManager.Instance.IsValidAttackTarget(_currentAttacker, tempCard))
            {
                hitCard = tempCard;
            }
        }

        _currentTargetInfo = hitCard;
    }

    // --- 로직: 공격 확정 (GameInputManager에서 호출) ---
    public void TryCompleteAttack()
    {
        // 마지막으로 타겟 확인
        if (_currentTargetInfo != null && CardTargetingManager.Instance.IsValidAttackTarget(_currentAttacker, _currentTargetInfo))
        {
            int attackerId = _currentAttacker.EntityId;
            int targetId = _currentTargetInfo.EntityId;

            // 테스트
            if(GameEntityManager.Instance.test)
            {
                GameEntityManager.Instance.TestAttack(_currentAttacker, _currentTargetInfo);
                return;
            }
            // 실제 전투
            GameEntityManager.Instance.PerformAttack(attackerId, targetId);

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
        // 1. [수정] 켜져있던 모든 타겟의 빛 끄기
        foreach (var target in _highlightedTargets)
        {
            if (target != null)
            {
                target.SetGlowState(false);
            }
        }
        _highlightedTargets.Clear();
        _currentTargetInfo = null;

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