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
        // 드래그 시작 시 공통 준비 작업
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        thresholdY = Screen.height * playAreaThreshold;

        // 드래그 중인 카드가 다른 UI를 뚫고 보이도록 설정
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        bool currentlyOver = eventData.position.y > thresholdY;

        // 영역 전환 감지
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

        // 현재 상태에 따라 오브젝트 위치 업데이트
        if (isOverPlayArea && fieldCardController != null)
        {
            // 하수인/광역주문: 필드 카드 위치 업데이트
            fieldCardController.UpdateDragTarget(eventData.position, eventData.delta);
        }
        else if (!isAimingFromHand)
        {
            // 핸드 영역에서 드래그 중: UI 카드 위치 업데이트
            rectTransform.position = eventData.position;
        }
        // isAimingFromHand가 true일 때는 AimingManager가 모든 것을 처리하므로 여기선 아무것도 안 함
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 조준 중이었다면, 조준을 멈추고 결과 처리
        if (isAimingFromHand)
        {
            AimingManager.Instance.StopAiming();
            isAimingFromHand = false;

            if (eventData.pointerEnter != null && (eventData.pointerEnter.GetComponent<FieldCardController>() != null || eventData.pointerEnter.GetComponent<EnemyCardTarget>() != null))
            {
                Debug.Log(eventData.pointerEnter.name + "에 단일 주문 발동!");
                HandManager.Instance.RemoveCardFromHand(handCardController);
                Destroy(gameObject);
            }
            else
            {
                ReturnToHand();
            }
            return;
        }

        // 필드 위에 있었다면, 결과 처리
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
            // 광역 주문 발동 (필드 위 아무데나 놓아도 발동)
            else if (handCardController.cardData.cardType == CardType.주문)
            {
                Debug.Log("광역 주문 발동!");
                // 필드 카드에 광역 주문 시전 애니메이션을 요청하고, 그 후 파괴되도록 합니다.
                // 예: fieldCardController.CastAoeSpell(() => Destroy(fieldCardInstance));
                Destroy(fieldCardInstance); // 임시로 즉시 파괴
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

    // ★★★ 여기가 핵심: 필드 영역에 진입했을 때 카드 타입에 따라 행동을 결정합니다. ★★★
    void OnEnterPlayArea(PointerEventData eventData)
    {
        CardData data = handCardController.cardData;

        // 단일 대상 주문 처리
        if (data.cardType == CardType.주문 && data.spellType == SpellType.단일_대상)
        {
            isAimingFromHand = true;
            canvasGroup.alpha = 0; // 핸드 카드는 투명하게
            AimingManager.Instance.StartAiming(this.gameObject);
            Debug.Log("단일 대상 주문 조준 시작!");
        }
        // 하수인 및 기타 주문 처리
        else
        {
            canvasGroup.alpha = 0;
            if (fieldCardPrefab != null)
            {
                fieldCardInstance = Instantiate(fieldCardPrefab);
                fieldCardController = fieldCardInstance.GetComponent<FieldCardController>();
                if (fieldCardController != null)
                {
                    fieldCardController.Initialize(data, handCardController.attackModifier, handCardController.healthModifier);
                    fieldCardController.SetInitialPosition(eventData.position);
                    fieldCardController.StartDragging();
                }
            }
        }
    }

    // 필드에서 핸드 영역으로 돌아왔을 때의 처리
    void OnLeavePlayArea()
    {
        // 조준 중이었다면, 조준을 취소합니다.
        if (isAimingFromHand)
        {
            isAimingFromHand = false;
            AimingManager.Instance.StopAiming();
        }

        // 필드 카드가 생성되어 있었다면, 파괴합니다.
        if (fieldCardInstance != null)
        {
            Destroy(fieldCardInstance);
            fieldCardController = null;
        }

        // 핸드 카드를 다시 보이게 합니다.
        canvasGroup.alpha = 1;
    }

    // 핸드로 카드를 되돌리는 최종 함수
    void ReturnToHand()
    {
        // 조준 중이었다면 확실히 멈춥니다.
        if (isAimingFromHand)
        {
            AimingManager.Instance.StopAiming();
            isAimingFromHand = false;
        }

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1;
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);

        // 핸드 정렬은 HandManager가 RemoveCardFromHand에서 처리하지만,
        // 드롭 실패 시에도 정렬이 필요할 수 있으므로 HandManager를 통해 요청합니다.
        if (HandManager.Instance != null)
        {
            // HandManager에 있는 정렬 함수를 직접 호출하는 것이 더 안전합니다.
            HandManager.Instance.ArrangeCards(); 
        }
    }
}
