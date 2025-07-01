using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class FieldCardController : MonoBehaviour
{
    [Header("드래그 효과 설정")]
    [Tooltip("카드가 필드 위를 떠다닐 높이입니다.")]
    public float floatHeight = 0.5f;
    [Tooltip("레이캐스트가 충돌을 감지할 게임 보드(필드)의 레이어입니다.")]
    public LayerMask gameBoardLayer;
    [Tooltip("마우스 움직임에 따라 카드가 기울어지는 정도입니다.")]
    public float tiltAmount = 15f;
    [Tooltip("카드가 움직이거나 회전하는 속도입니다.")]
    public float moveSpeed = 20f;

    [Header("배치 애니메이션 설정")]
    [Tooltip("카드가 위로 떠오를 높이입니다.")]
    public float hoverHeight = 2.0f;
    [Tooltip("카드가 최대로 커지는 배율입니다.")]
    public float maxScaleMultiplier = 1.5f;
    [Tooltip("애니메이션의 각 단계가 지속되는 시간입니다.")]
    public float animationDuration = 0.3f;
    [Tooltip("특수 연출이 지속되는 시간(임시)")]
    public float effectDuration = 0.5f;

    [Header("공격 조준 효과 설정")]
    public float aimingFloatHeight = 0.8f;
    public float aimingAnimDuration = 0.2f;

    // --- 상태 및 목표 변수 ---
    private GameObject cursorInstance; // ★★★ 생성된 커서의 '실물(인스턴스)'을 담을 변수
    private bool isBeingDragged = false;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isAiming = false; // ★★★ 공격 조준 상태를 나타내는 변수

    private Vector3 originalScale;
    private Quaternion neutralRotation;
    public Vector3 restingPosition; // 카드가 슬롯 위에서 최종적으로 위치할 자리

    [Header("데이터 및 참조")]
    public CardData cardData; // 원본 설계도
    private CardDisplay cardDisplay; // 시각적 표현을 담당하는 스크립트

    [Header("현재 스탯")]
    // private으로 선언하여 외부의 직접 수정을 막고,
    // [SerializeField]를 사용하여 인스펙터에서 디버깅용으로 확인합니다.
    [SerializeField] private int currentAttack;
    [SerializeField] private int currentHealth;

    [Header("소유권 정보")]
    [Tooltip("이 카드가 적의 카드이면 체크합니다.")]
    public bool enermy = false; // ★★★ 아군/적군 구분용 변수

    [Header("참조")]
    [Tooltip("타겟팅되었을 때 활성화될 하이라이트 오브젝트입니다.")]
    public GameObject highlightEffect;
    public bool isTargetable { get; private set; } = false; // ★★★ 외부에서 읽기만 가능한 상태 변수

    // 외부에서는 이 프로퍼티를 통해 값을 '읽기만' 할 수 있습니다.
    public int CurrentAttack => currentAttack;
    public int CurrentHealth => currentHealth;

    void Awake()
    {
        cardDisplay = GetComponent<CardDisplay>();

        neutralRotation = transform.rotation;
        // 시작 시 목표 위치를 현재 위치로 초기화하여 순간이동 방지
        targetPosition = transform.position;
        targetRotation = neutralRotation;

        
        // 시작 시 하이라이트 효과를 확실히 끕니다.
        if (highlightEffect != null) highlightEffect.SetActive(false);
        
    }

    void Update()
    {
        if (isBeingDragged)
        {
            // Lerp/Slerp를 사용하여 목표 지점으로 부드럽게 이동 및 회전
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * moveSpeed);
        }

        if (isAiming)
        {
            // 1. 커서 위치 업데이트
            // UpdateCursorPosition();

        }
    }

    /// <summary>
    /// 카드가 필드에 처음 놓일 때 호출됩니다.
    /// 원본 데이터와 핸드에서 받은 수정치를 기반으로 최종 스탯을 설정합니다.
    /// </summary>
    public void Initialize(CardData data, int attackMod, int healthMod)
    {
        // 1. 전달받은 원본 데이터를 나의 데이터로 설정합니다.
        cardData = data;

        // 2. 원본 스탯에 핸드에서 받은 버프/디버프 값을 더해 최종 초기 스탯을 결정합니다.
        currentAttack = cardData.attack + attackMod;
        currentHealth = cardData.health + healthMod;

        // 3. 시각적 정보를 담당하는 CardDisplay가 있다면,
        if (cardDisplay != null)
        {
            // CardDisplay의 CardData도 전달받은 데이터로 설정해주고,
            cardDisplay.cardData = cardData;
            // CardDisplay에게 기본 모습을 그리라고 명령합니다.
            cardDisplay.ApplyCardData();
            // 그 다음, 버프가 적용된 현재 스탯으로 숫자만 다시 갱신합니다.
            UpdateStatDisplay();
        }
    }

    /// <summary>
    /// 이 카드의 하이라이트 및 타겟 가능 상태를 설정합니다.
    /// FieldManager가 이 함수를 호출하여 타겟을 지정합니다.
    /// </summary>
    public void SetTargetable(bool isTargetable)
    {
        this.isTargetable = isTargetable;
        if (highlightEffect != null)
        {
            highlightEffect.SetActive(isTargetable);
        }
    }

    /// <summary>
    /// 피해를 받는 함수
    /// </summary>
    public void TakeDamage(int damage, CardData cardData)
    {
        currentHealth -= damage;
        Debug.Log(cardData.cardName + "이 " + damage + " 피해를 입었습니다. 남은 체력: " + currentHealth);

        // 화면 갱신
        UpdateStatDisplay();

        if (cardData.keywords.Contains(Keyword.치명타) || currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 필드 위에서 추가적인 버프/디버프를 적용하는 함수
    /// </summary>
    public void ApplyFieldBuff(int attack, int health)
    {
        currentAttack += attack;
        currentHealth += health;
        Debug.Log(cardData.cardName + "의 스탯이 변경되었습니다. 현재 공격력: " + currentAttack + ", 현재 체력: " + currentHealth);

        // 화면 갱신
        UpdateStatDisplay();
    }

    /// <summary>
    /// 현재 스탯을 화면(CardDisplay)에 갱신합니다.
    /// </summary>
    private void UpdateStatDisplay()
    {
        if (cardDisplay == null) return;

        // 필드 카드는 주로 3D 컴포넌트를 사용합니다.
        // 3D 공격력 텍스트가 있다면, 현재 공격력 값으로 업데이트합니다.
        if (cardDisplay.attackText_3D != null)
        {
            cardDisplay.attackText_3D.text = currentAttack.ToString();
        }

        // 3D 체력 텍스트가 있다면, 현재 체력 값으로 업데이트합니다.
        if (cardDisplay.healthText_3D != null)
        {
            cardDisplay.healthText_3D.text = currentHealth.ToString();
        }

        // CardDisplay에게 현재 스탯 정보를 전달하여 UI를 업데이트하도록 합니다.
        // 이 기능을 위해서는 CardDisplay 스크립트의 수정이 필요할 수 있습니다.
        // cardDisplay.UpdateRuntimeStats(currentAttack, currentHealth);
    }

    /// <summary>
    /// 카드가 파괴되는 함수
    /// </summary>
    private void Die()
    {
        Debug.Log(cardData.cardName + "이(가) 파괴되었습니다.");
        // 파괴 애니메이션 실행 후 오브젝트 제거
        Destroy(gameObject, 1f); // 1초 후 파괴
    }

    /// <summary>
    /// 카드를 클릭했을 때 호출됩니다.
    /// </summary>
    private void OnMouseDown()
    {
        if (enermy || DOTween.IsTweening(transform) || isBeingDragged || isAiming) return;

        isAiming = true;

        // ★★★ 핵심 수정: AimingManager에게 조준을 시작하고, 완료 후 실행할 함수(HandleAttackResult)를 등록합니다.
        AimingManager.Instance.StartAiming(this.gameObject);

        transform.DOLocalMoveY(aimingFloatHeight, aimingAnimDuration).SetEase(Ease.OutQuad);
    }

    /// <summary>
    /// 카드에서 마우스 클릭을 뗐을 때 호출됩니다.
    /// </summary>
    private void OnMouseUp()
    {
        if (enermy || !isAiming) return;
        isAiming = false;
        AimingManager.Instance.StopAiming();

        // --- 직접 레이캐스트를 쏴서 타겟 찾기 ---
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        FieldCardController targetController = null;

        if (Physics.Raycast(ray, out hit))
        {
            targetController = hit.collider.GetComponent<FieldCardController>();
        }

        // 유효한 '적' 하수인 타겟을 찾았다면, FieldManager에게 전투를 요청합니다.
        if (targetController != null && targetController.enermy == true)
        {
            FieldManager.Instance.RequestCombat(this, targetController);
        }

        // 카드는 원래 자리로 돌아옵니다.
        transform.DOLocalMove(restingPosition, aimingAnimDuration).SetEase(Ease.OutCubic);
    }

    /// <summary>
    /// AimingManager가 조준 결과를 알려주면 호출될 콜백 함수입니다.
    /// </summary>
    /// <param name="target">AimingManager가 찾아낸 타겟. 없으면 null입니다.</param>
    private void HandleAttackResult(FieldCardController target)
    {
        // 유효한 타겟(FieldCardController가 있고, '적' 카드인 경우)을 찾았다면 공격을 실행합니다.
        if (target != null && target.enermy == true)
        {
            Debug.Log(this.cardData.cardName + "이(가) " + target.cardData.cardName + "을(를) 공격합니다!");

            CardEffectManager.Instance.ExecuteEffects(this.cardData, target);
        }

        // 공격이 성공했든 실패했든, 카드는 원래 자리로 돌아옵니다.
        transform.DOLocalMove(restingPosition, aimingAnimDuration).SetEase(Ease.OutCubic);
    }



    private Vector3 GetPointOnBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinusT = 1f - t;
        return (oneMinusT * oneMinusT * p0) + (2f * oneMinusT * t * p1) + (t * t * p2);
    }

    public void StartDragging()
    {
        isBeingDragged = true;
    }

    // 추가된 함수: 자신과 모든 자식 오브젝트의 레이어를 재귀적으로 변경합니다. ★★★
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;

        obj.layer = newLayer;

        /* 자식들도 레이어 변경
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
        */
    }

    // CardDragDrop 스크립트가 호출할 시작 함수입니다.
    public void StartPlacementAnimation(Transform targetSlot)
    {
        SetLayerRecursively(this.gameObject, LayerMask.NameToLayer("Default"));
        // 배치 애니메이션이 시작되면, 더 이상 드래그 상태가 아닙니다.
        isBeingDragged = false;

        transform.SetParent(targetSlot);
        originalScale = transform.localScale;
        PlayRiseAnimation();
    }

    public void UpdateDragTarget(Vector2 mousePosition, Vector2 mouseDelta)
    {
        if (!isBeingDragged)
        {
            StartDragging();
        }
        // 1. 목표 위치 계산
        Ray ray = Camera.main.ScreenPointToRay(mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f, gameBoardLayer))
        {
            targetPosition = hit.point + new Vector3(0, floatHeight, 0);
        }

        // 2. 목표 회전값 계산
        float tiltX = mouseDelta.y * -tiltAmount * Time.deltaTime;
        float tiltZ = mouseDelta.x * tiltAmount * Time.deltaTime;
        targetRotation = Quaternion.Euler(tiltX, 0, tiltZ) * neutralRotation;
    }
    public void SetInitialPosition(Vector2 mousePosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100f, gameBoardLayer))
        {
            // Lerp 없이 위치를 직접 설정하여 즉시 이동시킵니다.
            transform.position = hit.point + new Vector3(0, floatHeight, 0);
        }
    }

    /// <summary>
    /// 1단계: 카드가 위로 떠오르고 커지는 애니메이션
    /// </summary>
    private void PlayRiseAnimation()
    {

        Sequence riseSequence = DOTween.Sequence();

        // ★★★ 수정: 회전값을 애니메이션 시작과 동시에 즉시 설정합니다. ★★★
        transform.localRotation = Quaternion.identity; // 로컬 회전값을 (0,0,0)으로 즉시 리셋
        // ★★★ 추가: 애니메이션 시작 전, 위치를 슬롯 중앙으로 즉시 설정합니다. ★★★
        transform.localPosition = Vector3.zero;
        riseSequence.Append(transform.DOLocalMoveY(hoverHeight, animationDuration).SetEase(Ease.OutQuad));
        riseSequence.Join(transform.DOScale(originalScale * maxScaleMultiplier, animationDuration).SetEase(Ease.OutQuad));

        // OnComplete: 이 시퀀스가 끝나면 OnRiseComplete 함수를 호출하라는 '예약'입니다.
        riseSequence.OnComplete(OnRiseComplete);
    }

    /// <summary>
    /// 1단계 애니메이션이 끝난 후 호출되는 중간 대기 단계
    /// </summary>
    private void OnRiseComplete()
    {

        // 이 곳에서 원하는 특수 효과(파티클, 사운드 등)를 재생할 수 있습니다.
        // 지금은 임시로 '연출 시간'만큼 기다렸다가 다음 단계를 진행합니다.
        PlaySpecialEffectAndLand();
    }

    /// <summary>
    /// 2단계: 특수 연출 재생(현재는 딜레이로 대체) 및 착지 애니메이션 호출
    /// </summary>
    private void PlaySpecialEffectAndLand()
    {
        // DOVirtual.DelayedCall은 특정 시간 후에 코드를 실행시켜주는 편리한 DOTween 함수입니다.
        DOVirtual.DelayedCall(effectDuration, () => {
            // 지정된 시간이 지나면, 3단계 애니메이션(착지)을 시작합니다.
            PlayLandAnimation();
        });
    }

    /// <summary>
    /// 3단계: 카드가 제자리로 돌아오며 착지하는 애니메이션
    /// </summary>
    private void PlayLandAnimation()
    {

        Sequence landSequence = DOTween.Sequence();
       
        landSequence.Append(transform.DOLocalMove(restingPosition, animationDuration).SetEase(Ease.InQuad));
        landSequence.Join(transform.DOScale(originalScale, animationDuration).SetEase(Ease.InQuad));

        landSequence.OnComplete(() => {
            Debug.Log("최종: 카드 배치 애니메이션 완전 종료!");
            // 모든 애니메이션이 끝났으니, 여기서 카드 효과를 발동시킬 수 있습니다.
        });
    }
}
