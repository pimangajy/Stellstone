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

    [Header("참조 및 설정")]
    [Tooltip("핸드에 생성될 UI 카드 프리팹입니다.")]
    public GameObject cardInHandPrefab;
    [Tooltip("카드들이 자식으로 생성될 부모 Transform입니다.")]
    public RectTransform handPanel;
    [Header("카드 덱 설정")]
    [Tooltip("뽑을 수 있는 카드 데이터의 목록입니다.")]
    public List<CardData> drawableCards = new List<CardData>();

    [Header("핸드 정렬 설정")]
    public float arcRadius = 600f;
    public float maxArcAngle = 90f;
    public float anglePerCard = 10f;
    public float yOffset = -550f;

    // ★★★ 이제 이 데이터 리스트가 핸드 관리의 기준이 됩니다. ★★★
    private List<CardInHandController> cardsInHand = new List<CardInHandController>();

    /// <summary>
    /// UI 버튼에서 호출하여 덱에서 무작위로 카드를 한 장 뽑습니다.
    /// </summary>
    public void DrawRandomCard()
    {
        if (drawableCards.Count == 0)
        {
            Debug.LogWarning("덱(Drawable Cards)에 뽑을 카드가 설정되지 않았습니다!");
            return;
        }
        CardData randomCardData = drawableCards[Random.Range(0, drawableCards.Count)];
        AddCardToHand(randomCardData);
    }

    /// <summary>
    /// 특정 카드 데이터를 기반으로 새로운 카드를 손에 추가하고 리스트에 기록합니다.
    /// </summary>
    public void AddCardToHand(CardData data)
    {
        if (cardInHandPrefab == null || handPanel == null) return;

        GameObject newCardObject = Instantiate(cardInHandPrefab, handPanel);
        CardInHandController newCardController = newCardObject.GetComponent<CardInHandController>();
        CardDisplay newCardDisplay = newCardObject.GetComponent<CardDisplay>();

        if (newCardController != null && newCardDisplay != null)
        {
            newCardDisplay.cardData = data;
            newCardController.cardData = data;
            newCardController.Initialize();

            // 데이터 리스트에 새로 생성된 카드를 추가합니다.
            cardsInHand.Add(newCardController);

            ArrangeCards();
        }
    }

    /// <summary>
    /// 손에서 특정 카드를 제거하고 핸드를 다시 정렬합니다.
    /// </summary>
    public void RemoveCardFromHand(CardInHandController cardToRemove)
    {
        // 데이터 리스트에서 해당 카드를 먼저 제거합니다.
        if (cardsInHand.Contains(cardToRemove))
        {
            cardsInHand.Remove(cardToRemove);
        }
        // 그 후, 남은 카드들로 핸드를 다시 정렬합니다.
        ArrangeCards();
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
    /// handPanel에 있는 카드들을 부채꼴 모양으로 정렬합니다.
    /// </summary>
    public void ArrangeCards()
    {
        if (handPanel == null) return;

        // ★★★ 핵심 수정: transform.childCount 대신, 데이터 리스트의 개수를 기준으로 삼습니다. ★★★
        int cardCount = cardsInHand.Count;
        if (cardCount == 0) return;

        float totalAngle = Mathf.Min(maxArcAngle, (cardCount - 1) * anglePerCard);
        float startAngle = -totalAngle / 2f;
        Vector2 arcCenterLocal = new Vector2(0, yOffset);

        for (int i = 0; i < cardCount; i++)
        {
            // ★★★ 핵심 수정: handPanel의 자식이 아닌, 데이터 리스트에 있는 카드를 직접 가져옵니다. ★★★
            RectTransform cardRect = cardsInHand[i].GetComponent<RectTransform>();
            if (cardRect == null) continue;

            float angle = (cardCount > 1) ? startAngle + i * anglePerCard : 0;
            float angleRad = angle * Mathf.Deg2Rad;
            float x = arcCenterLocal.x + arcRadius * Mathf.Sin(angleRad);
            float y = arcCenterLocal.y + arcRadius * Mathf.Cos(angleRad);
            Vector2 targetLocalPosition = new Vector2(x, y);

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
