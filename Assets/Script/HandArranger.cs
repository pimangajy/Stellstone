using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandArranger : MonoBehaviour
{
    [Header("Arc Settings")]
    [Tooltip("부채꼴의 반지름 크기입니다. 클수록 완만한 곡선이 됩니다.")]
    public float arcRadius = 600f;

    [Tooltip("카드가 배치될 수 있는 최대 각도입니다.")]
    public float maxArcAngle = 90f;

    [Tooltip("카드 한 장이 차지하는 각도입니다. 카드 사이의 간격을 조절합니다.")]
    public float anglePerCard = 10f;

    [Header("Positioning")]
    [Tooltip("부채꼴의 중심점을 현재 위치에서 얼마나 내릴지 결정합니다.")]
    public float yOffset = -550f;

    // 카드가 추가되거나 제거될 때마다 이 함수를 호출하여 정렬을 업데이트해야 합니다.
    public void ArrangeCards()
    {
        int cardCount = transform.childCount;
        if (cardCount == 0) return;

        // 카드의 총 개수에 따라 전체 부채꼴의 각도를 계산합니다.
        float totalAngle = Mathf.Min(maxArcAngle, (cardCount - 1) * anglePerCard);

        // 카드가 중앙에 정렬되도록 시작 각도를 계산합니다.
        float startAngle = -totalAngle / 2f;

        for (int i = 0; i < cardCount; i++)
        {
            Transform card = transform.GetChild(i);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            if (cardRect == null) continue;

            // 현재 카드의 각도를 계산합니다. 카드가 한 장일때는 0도입니다.
            float angle = (cardCount > 1) ? startAngle + i * anglePerCard : 0;

            // 각도를 라디안으로 변환합니다.
            float angleRad = angle * Mathf.Deg2Rad;

            // 부채꼴의 중심점을 계산합니다.
            Vector2 arcCenter = (Vector2)transform.position + new Vector2(0, yOffset);

            // 삼각함수를 사용하여 카드의 위치를 계산합니다.
            float x = arcCenter.x + arcRadius * Mathf.Sin(angleRad);
            float y = arcCenter.y + arcRadius * Mathf.Cos(angleRad);

            cardRect.position = new Vector3(x, y, 0);

            // 카드가 부채꼴의 중심을 자연스럽게 바라보도록 회전시킵니다.
            cardRect.rotation = Quaternion.Euler(0, 0, -angle);
        }
    }

#if UNITY_EDITOR
    // 유니티 에디터에서 값을 바꿀 때마다 실시간으로 정렬을 확인하기 위한 코드입니다.
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            ArrangeCards();
        }
    }
#endif
}
