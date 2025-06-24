using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // --- 대부분의 설정 변수들이 FieldCardController로 이동했습니다 ---
    [Header("필드 카드 전환 설정")]
    public GameObject fieldCardPrefab;
    [Range(0f, 1f)]
    public float playAreaThreshold = 0.2f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private int originalSiblingIndex;
    private CardInHandController handCardController; // 이 카드의 데이터 및 상태 관리자

    // --- 상태 관리 변수 ---
    private GameObject fieldCardInstance;
    private FieldCardController fieldCardController; // 필드 카드 컨트롤러 참조
    private CardDisplay cardDisplay;
    private bool isOverPlayArea = false;
    private float thresholdY;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        handCardController = GetComponent<CardInHandController>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        thresholdY = Screen.height * playAreaThreshold;

        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        bool currentlyOver = eventData.position.y > thresholdY;

        if (currentlyOver && !isOverPlayArea)
        {
            isOverPlayArea = true;
            OnEnterPlayArea(eventData);
        }
        else if (!currentlyOver && isOverPlayArea)
        {
            isOverPlayArea = false;
            OnLeavePlayArea();
        }

        // ★★★ 핵심 변경점 ★★★
        if (isOverPlayArea && fieldCardController != null)
        {
            // 이제 필드 카드의 움직임을 직접 계산하지 않고,
            // FieldCardController에게 마우스 정보만 넘겨주고 움직이라고 명령합니다.
            fieldCardController.UpdateDragTarget(eventData.position, eventData.delta);
        }
        else
        {
            // 핸드 카드(UI)는 여전히 여기서 직접 위치를 제어합니다.
            rectTransform.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        if (isOverPlayArea && fieldCardInstance != null)
        {
            FieldSlot targetSlot = null;
            if (eventData.pointerEnter != null)
            {
                targetSlot = eventData.pointerEnter.GetComponent<FieldSlot>();
            }

            if (targetSlot != null && targetSlot.IsAvailable())
            {
                targetSlot.OccupySlot(fieldCardController);
                fieldCardController.StartPlacementAnimation(targetSlot.transform);

                // ★★★ 수정된 부분 ★★★
                // HandManager에게 이 카드가 손에서 제거되었음을 알립니다.
                // HandManager가 리스트 관리와 카드 정렬을 모두 책임집니다.
                HandManager.Instance.RemoveCardFromHand(handCardController);

                // UI 카드 오브젝트를 파괴합니다.
                Destroy(gameObject);
            }
            else
            {
                Destroy(fieldCardInstance);
                ReturnToHand();
            }
        }
        else
        {
            ReturnToHand();
        }
    }

    void OnEnterPlayArea(PointerEventData eventData)
    {
        canvasGroup.alpha = 0;

        if (handCardController == null || handCardController.cardData == null || fieldCardPrefab == null)
        {
            Debug.LogError("카드 정보가 없거나 필드 카드 프리팹이 지정되지 않았습니다!");
            return;
        }

        fieldCardInstance = Instantiate(fieldCardPrefab, new Vector3(0, -1000, 0), Quaternion.identity);
        fieldCardController = fieldCardInstance.GetComponent<FieldCardController>();

        if (fieldCardController != null)
        {
            // ★★★ 이제 Initialize 함수 호출 하나로 모든 설정(데이터, 스탯, 시각적 표현)이 끝납니다. ★★★
            fieldCardController.Initialize(
                handCardController.cardData,
                handCardController.attackModifier,
                handCardController.healthModifier
            );

            fieldCardController.SetInitialPosition(eventData.position);
            fieldCardController.StartDragging();
        }
    }

    void OnLeavePlayArea()
    {
        canvasGroup.alpha = 1;
        if (fieldCardInstance != null)
        {
            Destroy(fieldCardInstance);
            fieldCardController = null; // 참조 초기화
        }
    }

    void ReturnToHand()
    {
        canvasGroup.alpha = 1;
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);
        HandManager handManager = originalParent.GetComponent<HandManager>();
        if (handManager != null)
        {
            handManager.ArrangeCards();
        }
    }
}
