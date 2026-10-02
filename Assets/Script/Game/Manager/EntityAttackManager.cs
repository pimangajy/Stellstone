using System;
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

    private Camera _mainCamera;

    private string MyUid => GameClient.Instance != null ? GameClient.Instance.UserUid : "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            GameClient.Instance.DebugCheckSubscribers();
        }
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

        // 서버에 타겟팅 가능한 대상 요청
        if (GameClient.Instance != null)
        {
            GameClient.Instance.SendValidAttackTargetsRequest(_currentAttacker.EntityId);
        }
    }



    // --- 로직: 드래그 중 타겟 갱신 (GameInputManager에서 매 프레임 호출) ---
    public void UpdateTargetHighlight()
    {
        if (_currentAttacker == null) return;

        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        GameCardDisplay hitCard = null;

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, entityLayer))
        {
            GameCardDisplay tempCard = hit.collider.GetComponentInParent<GameCardDisplay>();
            if (tempCard != null)
            {
                // ★ 수정: 사망 예정인 타겟이 아니고 서버가 승인한 대상 목록에 있는지만 확인
                bool isPendingDead = GameEntityManager.Instance != null && GameEntityManager.Instance.IsPendingDead(tempCard.EntityId);
                if (!isPendingDead && BattleManager.Instance != null && BattleManager.Instance.IsServerValidTarget(tempCard.EntityId))
                {
                    hitCard = tempCard;
                }
            }
        }

        _currentTargetInfo = hitCard;
    }

    // --- 로직: 공격 확정 (GameInputManager에서 호출) ---
    public void TryCompleteAttack()
    {
        bool attackSent = false;

        // ★ 수정: 마지막으로 타겟이 서버 승인 대상인지 확인
        if (_currentTargetInfo != null && BattleManager.Instance != null && BattleManager.Instance.IsServerValidTarget(_currentTargetInfo.EntityId))
        {
            int attackerId = _currentAttacker.EntityId;
            int targetId = _currentTargetInfo.EntityId;

            if (GameEntityManager.Instance.test)
            {
                GameEntityManager.Instance.TestAttack(_currentAttacker, _currentTargetInfo);
                ResetState(false);
                return;
            }

            if (GameClient.Instance != null)
            {
                GameClient.Instance.SendAttackRequest(attackerId, targetId);
                attackSent = true;
            }
        }

        // 공격 명령이 정상 전송되었다면 공중 부양 상태를 유지(keepAttackerFloating: true), 취소/실패 시 즉시 착지
        ResetState(keepAttackerFloating: attackSent);
    }

    // --- 로직: 상태 초기화 (원상복구) ---
    public void ResetState(bool keepAttackerFloating = false)
    {
        _currentTargetInfo = null;

        if (_currentAttacker != null)
        {
            if (!keepAttackerFloating)
            {
                _currentAttacker.SetFloatingState(false);
            }
            _currentAttacker = null;
        }

        if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();

        // ★ 추가: 조준이 끝나면 반짝임 하이라이트 및 서버 타겟 목록 초기화
        // if (BattleManager.Instance != null) BattleManager.Instance.ResetHighlights();
    }

    // --- 검증 로직 (GameInputManager에서도 사용하므로 public으로 변경) ---
    /// <summary>
    /// 내 소유의 일반 필드 하수인인지 검사합니다 (마우스 클릭 시 드래그 준비 허용 여부 판별).
    /// </summary>
    public bool IsFriendlyMinion(GameCardDisplay display)
    {
        if (display == null) return false;
        if (display.IsFloating) return false;

        var data = display.CurrentEntityData;
        if (data == null || data.ownerUid != MyUid) return false;
        if (data.isMember || data.isLeader) return false;

        return true;
    }

    /// <summary>
    /// 해당 하수인이 현재 실제로 공격을 개시할 수 있는 상태인지 엄격하게 검증합니다.
    /// </summary>
    public bool CanAttack(GameCardDisplay display)
    {
        if (!IsFriendlyMinion(display)) return false;

        // 사망 예정 하수인 확인
        if (GameEntityManager.Instance != null && GameEntityManager.Instance.IsPendingDead(display.EntityId))
            return false;

        // 테스트 모드인 경우
        if (GameEntityManager.Instance != null && GameEntityManager.Instance.test)
            return true;

        // 내 턴인지 확인
        if (BattleManager.Instance != null && !BattleManager.Instance.IsMyTurn)
            return false;

        var data = display.CurrentEntityData;
        if (data == null) return false;

        // 서버에서 관리되는 공격 가능 상태 플래그 (소환 턴 질풍/속공 여부, 공격 횟수 제한, 속박 등)
        if (!data.canAttack) return false;

        // 공격력이 0 이하인 경우 공격 불가
        if (data.attack <= 0) return false;

        return true;
    }

    public bool IsValidAttacker(GameCardDisplay display)
    {
        return CanAttack(display);
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