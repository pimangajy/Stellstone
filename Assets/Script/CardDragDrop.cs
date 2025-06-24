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

    // ★★★ 주문 타입별 상태를 관리하는 새로운 변수들 ★★★
    private bool isAimingFromHand = false; // 단일 주문을 손에서 조준 중인가?
    private bool isCastingAoeSpell = false; // 광역 주문을 시전 중인가?

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

        // ★★★ 드래그 시작 시 카드 타입 확인 ★★★
        CardData data = handCardController.cardData;
        if (data.cardType == CardType.주문 && data.spellType == SpellType.단일_대상)
        {
            // 단일 대상 주문이면, 손에서 조준 상태로 전환
            isAimingFromHand = true;
            // ★★★ 핵심: AimingManager에게 조준 시작을 요청합니다. ★★★
            // 이 카드(UI)의 transform을 시작점으로 넘겨줍니다.
            AimingManager.Instance.StartAiming(this.gameObject);
            Debug.Log("단일 대상 주문 조준 시작!");
        }

        // 일반적인 드래그 준비
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isAimingFromHand)
        {
            return;
        }

        // --- 하수인 / 광역 주문 드래그 로직 ---
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

        if (isOverPlayArea && fieldCardController != null)
        {
            fieldCardController.UpdateDragTarget(eventData.position, eventData.delta);
        }
        else
        {
            rectTransform.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // --- 단일 대상 주문 발동 ---
        if (isAimingFromHand)
        {
            // ★★★ 핵심: AimingManager에게 조준 중단을 요청합니다. ★★★
            AimingManager.Instance.StopAiming();
            isAimingFromHand = false;

            // 타겟 확인 로직 (예시)
            if (eventData.pointerEnter != null && eventData.pointerEnter.GetComponent<EnemyCardTarget>() != null)
            {
                Debug.Log(eventData.pointerEnter.name + "에 단일 주문 발동!");
                // 주문 효과 적용...
                Destroy(gameObject); // 주문 카드 파괴
                HandManager.Instance.RemoveCardFromHand(handCardController);
            }
            else
            {
                // 유효한 타겟이 아니면 핸드로 복귀
                ReturnToHand();
            }
            return;
        }

        // --- 하수인 / 광역 주문 발동 ---
        if (isOverPlayArea && fieldCardInstance != null)
        {
            FieldSlot targetSlot = null;
            if (eventData.pointerEnter != null)
            {
                targetSlot = eventData.pointerEnter.GetComponent<FieldSlot>();
            }

            // 하수인 배치
            if (handCardController.cardData.cardType == CardType.하수인 && targetSlot != null && targetSlot.IsAvailable())
            {
                targetSlot.OccupySlot(fieldCardController);
                fieldCardController.StartPlacementAnimation(targetSlot.transform);
                HandManager.Instance.RemoveCardFromHand(handCardController);
                Destroy(gameObject);
            }
            // 광역 주문 발동
            else if (handCardController.cardData.cardType == CardType.주문 && handCardController.cardData.spellType == SpellType.범위_광역)
            {
                Debug.Log("광역 주문 발동!");
                // 필드 카드를 특정 위치로 이동시켜 발동 연출 시작
                // 예: fieldCardController.CastAoeSpell(spellCastPosition);
                HandManager.Instance.RemoveCardFromHand(handCardController);
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
