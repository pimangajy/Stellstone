using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class OpponentHandVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject cardBackPrefab;
    [SerializeField] private Transform handCenterTransform; // 손패 부채꼴의 중심(바닥) 축

    [Header("Fan Shape Settings")]
    [SerializeField] private float radius = 500f; // 부채꼴의 반지름
    [SerializeField] private float angleSpacing = 5f; // 카드 간의 각도 간격
    [SerializeField] private float yOffsetSpacing = 10f; // 겹칠 때 높이 단차
    [SerializeField] private float animationDuration = 0.3f;

    // 현재 상대방 손에 있는 카드 오브젝트(뒷면) 리스트
    private List<GameObject> _opponentCards = new List<GameObject>();

    private void Start()
    {

    }

    /// <summary>
    /// 상대방이 카드를 드로우 했을 때 호출
    /// </summary>
    private void HandleOpponentDraw(int drawCount)
    {
        for (int i = 0; i < drawCount; i++)
        {
            // 실제 프로젝트에서는 Instantiate 대신 Object Pool 사용을 강력히 권장합니다.
            GameObject newCard = Instantiate(cardBackPrefab, handCenterTransform);
            _opponentCards.Add(newCard);
        }

        ArrangeCardsInFanShape();
    }

    /// <summary>
    /// 상대방이 카드를 사용/버렸을 때 호출
    /// </summary>
    private void HandleOpponentPlayCard(int cardIndex)
    {
        if (_opponentCards.Count > 0)
        {
            // 실제 로직에서는 서버가 지정한 인덱스나 무작위 카드를 뽑아 사용 연출로 넘김
            GameObject playedCard = _opponentCards[0];
            _opponentCards.RemoveAt(0);

            // 사용 연출 후 파괴 (임시)
            playedCard.transform.DOMove(handCenterTransform.position + Vector3.forward * 5f, 0.5f)
                .OnComplete(() => Destroy(playedCard));

            ArrangeCardsInFanShape();
        }
    }

    /// <summary>
    /// 카드를 부채꼴 모양으로 정렬하는 핵심 수학 로직 (DOTween 애니메이션 적용)
    /// </summary>
    private void ArrangeCardsInFanShape()
    {
        int cardCount = _opponentCards.Count;
        if (cardCount == 0) return;

        // 중앙을 기준으로 카드가 좌우로 퍼지도록 시작 각도 계산
        float totalAngle = (cardCount - 1) * angleSpacing;
        float startAngle = totalAngle / 2f;

        for (int i = 0; i < cardCount; i++)
        {
            float currentAngle = startAngle - (i * angleSpacing);

            // 1. Z축 회전값 계산 (부채꼴 모양으로 기울어짐)
            Quaternion targetRotation = Quaternion.Euler(0, 0, currentAngle);

            // 2. 위치 계산 (반지름과 삼각함수를 이용한 원호 상의 위치)
            // 각도를 라디안으로 변환
            float angleRad = (currentAngle + 90f) * Mathf.Deg2Rad;

            Vector3 targetLocalPosition = new Vector3(
                Mathf.Cos(angleRad) * radius,
                Mathf.Sin(angleRad) * radius - radius, // 기준점을 원호 아래쪽으로 맞춤
                -i * 0.1f // Z-fighting(텍스처 겹침) 방지를 위해 약간씩 앞으로 뺌
            );

            // Y축 높이를 약간씩 조절하여 부채꼴의 입체감을 더함
            targetLocalPosition.y += Mathf.Abs(i - (cardCount / 2f)) * yOffsetSpacing;

            // 3. DOTween을 이용해 부드럽게 이동 및 회전
            _opponentCards[i].transform.DOLocalMove(targetLocalPosition, animationDuration).SetEase(Ease.OutOut);
            _opponentCards[i].transform.DOLocalRotateQuaternion(targetRotation, animationDuration).SetEase(Ease.OutOut);
        }
    }
}
