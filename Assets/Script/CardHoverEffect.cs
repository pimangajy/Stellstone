using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale; // 원래 크기를 저장할 변수
    public float hoverScaleMultiplier = 1.2f; // 마우스 오버 시 커질 배율

    void Start()
    {
        // 시작할 때 카드의 원래 크기를 저장해 둡니다.
        originalScale = transform.localScale;
    }

    // 마우스 포인터가 카드(의 Collider) 위에 올라왔을 때 자동으로 호출되는 함수
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 카드의 크기를 키웁니다.
        transform.localScale = originalScale * hoverScaleMultiplier;
    }

    // 마우스 포인터가 카드(의 Collider) 위에서 벗어났을 때 자동으로 호출되는 함수
    public void OnPointerExit(PointerEventData eventData)
    {
        // 카드의 크기를 원래대로 되돌립니다.
        transform.localScale = originalScale;
    }

}
