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
    [Tooltip("공격 조준 시 카드가 떠오르는 높이입니다.")]
    public float aimingFloatHeight = 0.8f;
    [Tooltip("공격 조준 애니메이션의 속도입니다.")]
    public float aimingAnimDuration = 0.2f;

    // --- 상태 및 목표 변수 ---
    private bool isBeingDragged = false;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isAiming = false; // ★★★ 공격 조준 상태를 나타내는 변수

    private Vector3 originalScale;
    private Quaternion neutralRotation;
    public Vector3 restingPosition; // 카드가 슬롯 위에서 최종적으로 위치할 자리

    void Awake()
    {
        neutralRotation = transform.rotation;
        // 시작 시 목표 위치를 현재 위치로 초기화하여 순간이동 방지
        targetPosition = transform.position;
        targetRotation = neutralRotation;
    }

    void Update()
    {
        if (isBeingDragged)
        {
            // Lerp/Slerp를 사용하여 목표 지점으로 부드럽게 이동 및 회전
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * moveSpeed);
        }
    }

    // ★★★ 추가된 함수: 카드를 클릭했을 때 호출됩니다. ★★★
    private void OnMouseDown()
    {
        // 소환 중(Tweening)이거나, 드래그 중이거나, 이미 조준 중일 때는 아무것도 하지 않습니다.
        if (DOTween.IsTweening(transform) || isBeingDragged || isAiming) return;

        Debug.Log(gameObject.name + " 클릭! 공격 준비 시작.");
        isAiming = true;
        // 카드를 조준 높이만큼 부드럽게 위로 띄웁니다.
        transform.DOLocalMoveY(aimingFloatHeight, aimingAnimDuration).SetEase(Ease.OutQuad);
    }

    // ★★★ 추가된 함수: 카드에서 마우스 클릭을 뗐을 때 호출됩니다. ★★★
    private void OnMouseUp()
    {
        // 조준 중인 상태가 아니면 아무것도 하지 않습니다.
        if (!isAiming) return;

        Debug.Log("클릭 해제. 공격 취소.");
        isAiming = false;

        // 나중에 여기에 공격 대상을 확인하는 로직을 추가할 수 있습니다.
        // 지금은 공격을 취소하고 원래 자리로 돌아옵니다.
        transform.DOLocalMove(restingPosition, aimingAnimDuration).SetEase(Ease.OutCubic);
    }

    public void StartDragging()
    {
        isBeingDragged = true;
    }

    // CardDragDrop 스크립트가 호출할 시작 함수입니다.
    public void StartPlacementAnimation(Transform targetSlot)
    {
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
        Debug.Log("1단계: 상승 애니메이션 시작");

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
        Debug.Log("2단계: 상승 완료. 특수 연출 대기...");

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
        Debug.Log("3단계: 착지 애니메이션 시작");

        Sequence landSequence = DOTween.Sequence();
       
        landSequence.Append(transform.DOLocalMove(restingPosition, animationDuration).SetEase(Ease.InQuad));
        landSequence.Join(transform.DOScale(originalScale, animationDuration).SetEase(Ease.InQuad));

        landSequence.OnComplete(() => {
            Debug.Log("최종: 카드 배치 애니메이션 완전 종료!");
            // 모든 애니메이션이 끝났으니, 여기서 카드 효과를 발동시킬 수 있습니다.
        });
    }
}
