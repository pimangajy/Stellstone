using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("애니메이션 설정")]
    [Tooltip("마우스를 올렸을 때 커질 배율입니다.")]
    public float hoverScale = 1.05f;
    [Tooltip("아이콘이 회전할 각도입니다.")]
    public float iconRotationAngle = -15f;
    [Tooltip("애니메이션이 지속되는 시간입니다.")]
    public float animationDuration = 0.2f;

    [Header("참조")]
    [Tooltip("회전시킬 아이콘의 Transform입니다.")]
    public Transform iconTransform; // 인스펙터에서 아이콘 오브젝트를 연결해주세요.

    private Vector3 initialScale;
    private Quaternion initialIconRotation;

    void Awake()
    {
        // 시작 시 버튼과 아이콘의 원래 크기 및 회전값을 저장해 둡니다.
        initialScale = transform.localScale;
        if (iconTransform != null)
        {
            initialIconRotation = iconTransform.localRotation;
        }
    }

    /// <summary>
    /// 마우스 포인터가 버튼 위에 올라왔을 때 호출됩니다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 현재 실행 중인 모든 애니메이션을 멈추고 새로운 애니메이션을 시작합니다.
        transform.DOKill();
        if (iconTransform != null)
        {
            iconTransform.DOKill();
        }

        // 버튼 크기를 키웁니다.
        transform.DOScale(initialScale * hoverScale, animationDuration).SetEase(Ease.OutQuad);

        // 아이콘을 회전시킵니다.
        if (iconTransform != null)
        {
            iconTransform.DOLocalRotate(new Vector3(0, 0, iconRotationAngle), animationDuration).SetEase(Ease.OutQuad);
        }
    }

    /// <summary>
    /// 마우스 포인터가 버튼 위에서 벗어났을 때 호출됩니다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        // 현재 실행 중인 모든 애니메이션을 멈추고 원래 상태로 돌아가는 애니메이션을 시작합니다.
        transform.DOKill();
        if (iconTransform != null)
        {
            iconTransform.DOKill();
        }

        // 버튼 크기를 원래대로 되돌립니다.
        transform.DOScale(initialScale, animationDuration).SetEase(Ease.OutQuad);

        // 아이콘을 원래 각도로 되돌립니다.
        if (iconTransform != null)
        {
            iconTransform.DOLocalRotateQuaternion(initialIconRotation, animationDuration).SetEase(Ease.OutQuad);
        }
    }
}
