using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class HandManager : MonoBehaviour
{
    // --- 싱글톤 패턴 설정 ---
    public static HandManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    // -------------------------
    // ★★★ 추가된 부분 ★★★
    [Header("카드 덱 설정")]
    [Tooltip("뽑을 수 있는 카드 데이터의 목록입니다.")]
    public List<CardData> drawableCards = new List<CardData>();

    [Header("참조 및 설정")]
    [Tooltip("핸드에 생성될 UI 카드 프리팹입니다.")]
    public GameObject cardInHandPrefab;
    [Tooltip("카드들이 자식으로 생성될 부모 Transform입니다.")]
    public RectTransform handPanel; // Transform 대신 RectTransform으로 변경

    [Header("핸드 정렬 설정")]
    [Tooltip("부채꼴의 반지름 크기입니다. 클수록 완만한 곡선이 됩니다.")]
    public float arcRadius = 600f;
    [Tooltip("카드가 배치될 수 있는 최대 각도입니다.")]
    public float maxArcAngle = 90f;
    [Tooltip("카드 한 장이 차지하는 각도입니다. 카드 사이의 간격을 조절합니다.")]
    public float anglePerCard = 10f;
    [Tooltip("부채꼴의 중심점을 현재 위치에서 얼마나 내릴지 결정합니다.")]
    public float yOffset = -550f;

    // 핸드에 있는 카드들을 관리하는 리스트
    private List<CardInHandController> cardsInHand = new List<CardInHandController>();

    /// <summary>
    /// ★★★ 새로 추가된 함수 ★★★
    /// UI 버튼에서 호출하여 덱에서 무작위로 카드를 한 장 뽑습니다.
    /// </summary>
    public void DrawRandomCard()
    {
        if (drawableCards.Count == 0)
        {
            Debug.LogWarning("덱(Drawable Cards)에 뽑을 카드가 설정되지 않았습니다!");
            return;
        }

        // 리스트에서 무작위로 카드 데이터 하나를 선택합니다.
        CardData randomCardData = drawableCards[Random.Range(0, drawableCards.Count)];

        // 선택된 카드를 손에 추가합니다.
        AddCardToHand(randomCardData);
    }

    /// <summary>
    /// 특정 카드 데이터를 기반으로 새로운 카드를 손에 추가합니다. (카드 뽑기)
    /// </summary>
    public void AddCardToHand(CardData data)
    {
        if (cardInHandPrefab == null || handPanel == null) return;

        GameObject newCardObject = Instantiate(cardInHandPrefab, handPanel);
        CardInHandController newCardController = newCardObject.GetComponent<CardInHandController>();
        CardDisplay newCardDisplay = newCardObject.GetComponent<CardDisplay>();
        newCardObject.GetComponent<Image>().color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));

        if (newCardController != null && newCardDisplay != null)
        {
            newCardDisplay.cardData = data;
            newCardController.cardData = data;
            newCardController.Initialize();
            newCardDisplay.ApplyCardData();
            cardsInHand.Add(newCardController);

            // 카드가 추가되었으니, 즉시 핸드를 재정렬합니다.
            ArrangeCards();
        }
    }

    /// <summary>
    /// 손에서 특정 카드를 제거합니다. (카드를 필드에 냈을 때 호출)
    /// </summary>
    public void RemoveCardFromHand(CardInHandController cardToRemove)
    {
        if (cardsInHand.Contains(cardToRemove))
        {
            cardsInHand.Remove(cardToRemove);
            // 카드가 제거되었으니, 즉시 핸드를 재정렬합니다.
            ArrangeCards();
        }
    }

    /// <summary>
    /// 손에 있는 모든 카드에 스탯 버프를 부여합니다.
    /// </summary>
    public void BuffAllCardsInHand(int attack, int health)
    {
        Debug.Log("손에 있는 모든 카드에 +" + attack + "/+" + health + " 버프를 부여합니다.");
        foreach (CardInHandController card in cardsInHand)
        {
            card.ApplyStatBuff(attack, health);
        }
    }

    /// <summary>
    /// handPanel에 있는 카드들을 부채꼴 모양으로 정렬합니다. (로컬 좌표 기준)
    /// </summary>
    public void ArrangeCards()
    {
        if (handPanel == null) return;

        int cardCount = handPanel.childCount;
        if (cardCount == 0) return;

        float totalAngle = Mathf.Min(maxArcAngle, (cardCount - 1) * anglePerCard);
        float startAngle = -totalAngle / 2f;

        // 부채꼴의 중심점을 handPanel의 로컬 좌표 기준으로 설정합니다.
        Vector2 arcCenterLocal = new Vector2(0, yOffset);

        for (int i = 0; i < cardCount; i++)
        {
            RectTransform cardRect = handPanel.GetChild(i).GetComponent<RectTransform>();
            if (cardRect == null) continue;

            float angle = (cardCount > 1) ? startAngle + i * anglePerCard : 0;
            float angleRad = angle * Mathf.Deg2Rad;

            // 카드의 목표 로컬 위치(anchoredPosition)를 계산합니다.
            float x = arcCenterLocal.x + arcRadius * Mathf.Sin(angleRad);
            float y = arcCenterLocal.y + arcRadius * Mathf.Cos(angleRad);
            Vector2 targetLocalPosition = new Vector2(x, y);

            // DOTween을 사용하여 부드럽게 이동 및 회전
            cardRect.DOAnchorPos(targetLocalPosition, 0.3f);
            cardRect.DORotate(new Vector3(0, 0, -angle), 0.3f);
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
