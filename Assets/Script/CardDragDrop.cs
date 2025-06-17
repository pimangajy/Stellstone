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

    // --- 상태 관리 변수 ---
    private GameObject fieldCardInstance;
    private FieldCardController fieldCardController; // 필드 카드 컨트롤러 참조
    private bool isOverPlayArea = false;
    private float thresholdY;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
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

            // ★★★ 핵심 수정: 슬롯이 존재하고, '비어있는지' 확인합니다. ★★★
            if (targetSlot != null && targetSlot.IsAvailable())
            {
                Debug.Log(targetSlot.name + "에 카드를 성공적으로 놓았습니다!");

                // 슬롯을 점유 상태로 변경합니다.
                targetSlot.OccupySlot();

                // 필드 카드의 배치 애니메이션을 호출합니다.
                fieldCardController.StartPlacementAnimation(targetSlot.transform);

                // ★★★ 핵심 수정 부분 ★★★

                // 1. 핸드 정렬 스크립트의 참조를 미리 가져옵니다.
                HandArranger handArranger = originalParent.GetComponent<HandArranger>();

                // 2. 핸드에 있던 UI 카드를 먼저 파괴합니다.
                Destroy(gameObject);

                // 3. 파괴된 후, 핸드 정렬을 호출하여 남은 카드들을 재정렬합니다.
                if (handArranger != null)
                {
                    handArranger.ArrangeCards();
                }
            }
            else
            {
                // 유효한 슬롯이 아니므로 핸드로 돌아갑니다.
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
        if (fieldCardPrefab != null)
        {
            // 1. 필드 카드를 생성합니다. (임시 위치는 화면 밖으로 설정하여 깜빡임을 방지합니다)
            fieldCardInstance = Instantiate(fieldCardPrefab, new Vector3(0, -1000, 0), Quaternion.identity);

            // 2. 생성된 필드 카드에서 컨트롤러 컴포넌트를 가져옵니다.
            fieldCardController = fieldCardInstance.GetComponent<FieldCardController>();

            // 3. 컨트롤러에게 초기 위치를 설정하라고 명령합니다.
            if (fieldCardController != null)
            {
                fieldCardController.SetInitialPosition(eventData.position);
            }
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
        HandArranger handArranger = originalParent.GetComponent<HandArranger>();
        if (handArranger != null)
        {
            handArranger.ArrangeCards();
        }
    }
}
