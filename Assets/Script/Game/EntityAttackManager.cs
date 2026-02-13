using UnityEngine;

/// <summary>
/// 필드에 이미 소환된 내 하수인을 마우스로 드래그해서
/// 적(하수인 또는 영웅)을 공격하도록 명령하는 관리자 스크립트입니다.
/// 
/// [핵심 역할]
/// 1. 마우스 클릭: 내 하수인을 선택했는지 감지
/// 2. 드래그: 화살표(TargetingReticle)를 그려서 공격 대상을 조준
/// 3. 마우스 뗌: 적을 제대로 조준했다면 서버에 "공격해!"라고 신호 보냄
/// </summary>
public class EntityAttackManager : MonoBehaviour
{
    // 싱글톤 패턴: 이 스크립트는 게임 내에 단 하나만 존재하며, 어디서든 접근 가능하게 합니다.
    public static EntityAttackManager Instance;

    [Header("필수 설정")]
    [Tooltip("레이캐스트(광선)가 하수인이나 영웅만 인식하도록 필터링하는 레이어입니다. 꼭 설정해야 합니다!")]
    public LayerMask entityLayer;

    [Tooltip("마우스를 클릭한 상태로 이 거리만큼은 움직여야 '드래그'로 인정합니다. (단순 클릭과 구별하기 위함)")]
    public float dragThreshold = 10f;

    // --- 내부에서 사용하는 변수들 ---

    // 현재 내가 공격하려고 잡고 있는 내 하수인
    private GameCardDisplay _currentAttacker;

    // 지금 드래그 모드(화살표가 나오는 상태)인지 여부
    private bool _isDragging = false;

    // 마우스를 처음 누른 위치 (드래그 거리를 계산하기 위해 저장)
    private Vector2 _mouseDownPos;

    // 화면을 비추는 메인 카메라 (마우스 위치를 게임 월드 좌표로 변환할 때 필요)
    private Camera _mainCamera;

    // 내 플레이어 ID (아군과 적군을 구별하기 위해 사용)
    // GameClient가 연결되어 있다면 거기서 가져오고, 없으면 빈 문자열 반환
    private string MyUid => GameClient.Instance != null ? GameClient.Instance.UserUid : "";

    private void Awake()
    {
        // 싱글톤 초기화: 나(Instance)는 오직 하나만 존재해야 함
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _mainCamera = Camera.main;
    }

    // 매 프레임마다 실행되는 함수 (약 1초에 60번 실행)
    void Update()
    {
        // 1. 안전 장치: 게임 매니저가 없거나, 내 턴이 아니면 아무것도 하지 않음
        // (상대 턴에 내 하수인을 조작하면 안 되니까요!)
        if (GameStateManager.Instance != null && !GameStateManager.Instance.IsMyTurn) return;

        // 2. 입력 처리: 마우스 행동을 감시합니다.
        HandleInput();
    }

    /// <summary>
    /// 마우스의 클릭, 드래그, 뗌 동작을 처리하는 함수입니다.
    /// </summary>
    private void HandleInput()
    {
        // [상황 1] 마우스 왼쪽 버튼을 막 눌렀을 때
        if (Input.GetMouseButtonDown(0))
        {
            _mouseDownPos = Input.mousePosition; // 누른 위치 저장
            TrySelectAttacker(); // "혹시 내 하수인을 눌렀나?" 확인
        }

        // [상황 2] 마우스 버튼을 누른 채로 이동 중일 때 (그리고 공격자가 선택된 상태일 때)
        if (Input.GetMouseButton(0) && _currentAttacker != null && !_isDragging)
        {
            // 처음 누른 위치와 현재 마우스 위치의 거리를 잰다.
            float distance = Vector2.Distance(_mouseDownPos, Input.mousePosition);

            // 살짝 떨리는 정도가 아니라, 확실히 '드래그'하려는 의도로 움직였는지 확인 (Threshold 넘음)
            if (distance > dragThreshold)
            {
                StartAttackDrag(); // 드래그 모드 시작! 화살표 켜기
            }
        }

        // [상황 3] 마우스 버튼을 뗐을 때
        if (Input.GetMouseButtonUp(0))
        {
            // 드래그 중이었다면 -> 공격 시도 (적 위에 놓았나?)
            if (_isDragging)
            {
                TryCompleteAttack();
            }

            // 드래그가 아니었거나 공격이 끝났으니 상태 초기화
            ResetState();
        }
    }

    /// <summary>
    /// 마우스 위치에서 광선(Ray)을 쏘아 내 하수인을 선택하는 함수입니다.
    /// </summary>
    private void TrySelectAttacker()
    {
        // 화면상의 마우스 위치에서 게임 월드 쪽으로 광선을 쏩니다.
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

        // 광선이 'entityLayer'에 있는 무언가와 부딪혔는지 확인
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, entityLayer))
        {
            // 부딪힌 물체에서 카드 정보 스크립트(GameCardDisplay)를 가져옵니다.
            GameCardDisplay cardDisplay = hit.collider.GetComponent<GameCardDisplay>();

            // 그 카드가 공격 가능한 내 하수인인지 꼼꼼히 검사합니다.
            if (IsValidAttacker(cardDisplay))
            {
                _currentAttacker = cardDisplay; // 공격자로 등록!
            }
        }
    }

    /// <summary>
    /// 본격적으로 드래그 모드에 진입합니다. (화살표 연출 시작)
    /// </summary>
    private void StartAttackDrag()
    {
        _isDragging = true;

        // 기존에 만들어둔 '조준선(TargetingReticle)' 시스템을 빌려 씁니다.
        // "내 하수인 위치(_currentAttacker.transform)에서부터 화살표를 그려줘!"라고 요청
        if (TargetingReticle.Instance != null)
        {
            TargetingReticle.Instance.StartTargeting(_currentAttacker.transform);
        }

        Debug.Log($"[Attack] {_currentAttacker.name} 공격 조준 시작");
    }

    /// <summary>
    /// 마우스를 뗐을 때, 적절한 대상 위라면 서버에 공격을 요청합니다.
    /// </summary>
    private void TryCompleteAttack()
    {
        // 마우스를 뗀 위치로 다시 광선을 쏩니다.
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

        // 무언가(적 하수인 등)에 맞았는지 확인
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, entityLayer))
        {
            GameCardDisplay targetDisplay = hit.collider.GetComponent<GameCardDisplay>();

            // 그 대상이 공격해도 되는 적군인지 검사
            if (IsValidTarget(targetDisplay))
            {
                // 공격 실행! 
                // 여기서 직접 때리는 게 아니라, 서버에 "나 쟤 때릴래"라고 편지(패킷)를 보냅니다.
                int attackerId = _currentAttacker.EntityId;
                int targetId = targetDisplay.EntityId;

                Debug.Log($"[Attack] 공격 요청 전송: {attackerId}번이 {targetId}번을 공격");

                if (GameClient.Instance != null)
                {
                    GameClient.Instance.SendAttackRequest(attackerId, targetId);
                }
            }
        }

        // 드래그가 끝났으니 화살표를 끕니다.
        if (TargetingReticle.Instance != null)
        {
            TargetingReticle.Instance.StopTargeting();
        }
    }

    /// <summary>
    /// 모든 상태를 초기화합니다. (공격자 선택 해제, 드래그 종료 등)
    /// </summary>
    private void ResetState()
    {
        _currentAttacker = null;
        _isDragging = false;

        // 혹시 켜져 있을지 모를 조준선을 확실히 끕니다.
        if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();
    }

    // --- 검증 로직 (규칙 판정) ---

    /// <summary>
    /// 선택한 카드가 내가 조종할 수 있는 유효한 공격자인지 판단합니다.
    /// </summary>
    private bool IsValidAttacker(GameCardDisplay display)
    {
        if (display == null) return false;

        // [주의] 아래 로직을 사용하려면 GameCardDisplay.cs에 CurrentEntityData 프로퍼티가 있어야 합니다.
        // 현재는 데이터를 가져오는 코드가 주석 처리되어 있습니다.

        /*
        var data = display.CurrentEntityData; 
        if (data == null) return false;
        
        // 1. 내 하수인이 아니면 조종 불가
        if (data.ownerUid != MyUid) return false; 
        
        // 2. 공격권이 없으면(이번 턴에 이미 공격함, 소환 직후 등) 불가
        if (!data.canAttack) return false; 
        
        // 3. 공격력이 0이면 공격 불가
        if (data.attack <= 0) return false; 
        */

        // 지금은 테스트를 위해 무조건 통과(True)시킵니다.
        return true;
    }

    /// <summary>
    /// 마우스 아래 있는 카드가 유효한 공격 대상(적군)인지 판단합니다.
    /// </summary>
    private bool IsValidTarget(GameCardDisplay target)
    {
        if (target == null) return false;

        // 자해(자기 자신을 공격) 금지
        if (target == _currentAttacker) return false;

        // [추후 구현] 아군 팀킬 금지
        // var data = target.CurrentEntityData;
        // if (data != null && data.ownerUid == MyUid) return false; // 아군이면 공격 불가

        return true;
    }
}