using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening; // ★★★ DOKill()을 사용하기 위해 필요합니다.

public class CardDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // --- 인스펙터 설정 변수 ---
    [Header("참조 및 설정")]
    [Tooltip("생성될 기본 필드 카드 프리팹입니다.")]
    public GameObject fieldCardPrefab;
    [Tooltip("핸드에 남겨질 잔상용 카드 프리팹입니다. (상호작용 스크립트가 없는 깨끗한 버전)")]
    public GameObject ghostCardPrefab; // 잔상 전용 프리팹을 연결할 변수
    [Tooltip("필드로 인식될 화면 하단으로부터의 높이 비율입니다.")]
    [Range(0f, 1f)]
    public float playAreaThreshold = 0.2f;

    // --- 내부 참조 변수 ---
    private RectTransform rectTransform;        // 이 UI 카드의 RectTransform
    private CanvasGroup canvasGroup;            // 투명도 및 상호작용 제어를 위한 CanvasGroup
    private CardInHandController handCardController; // 이 카드의 데이터 및 상태 관리자

    // --- 드래그 시작 시 상태를 저장하는 변수들 ---
    private Transform originalParent;           // 드래그 시작 전의 부모 (Hand Panel)
    private int originalSiblingIndex;           // 드래그 시작 전의 순서
    private Vector2 originalAnchoredPosition;   // 드래그 시작 전의 UI상 위치

    // --- 현재 드래그 상태를 관리하는 변수들 ---
    private bool isOverPlayArea = false;        // 마우스가 필드 영역 위에 있는지 여부
    private float thresholdY;                   // 필드 영역으로 인식될 실제 Y좌표

    // --- 동적으로 생성/파괴되는 인스턴스 관리 변수들 ---
    private GameObject fieldCardInstance;       // 필드에 생성된 카드 인스턴스
    private FieldCardController fieldCardController; // 생성된 필드 카드의 컨트롤러
    private GameObject ghostCardInstance;       // 핸드에 남겨진 잔상 카드 인스턴스

    // --- 주문 카드 상태 변수 ---
    private bool isAimingFromHand = false;      // 단일 주문을 손에서 조준 중인지 여부

    /// <summary>
    /// 스크립트가 처음 활성화될 때 한 번 호출됩니다.
    /// 필요한 컴포넌트들의 참조를 미리 가져와 변수에 저장합니다.
    /// </summary>
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        handCardController = GetComponent<CardInHandController>();
    }

    /// <summary>
    /// 드래그를 시작하는 첫 순간에 한 번 호출됩니다.
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 드래그를 취소하고 되돌릴 때를 대비해, 시작 시점의 위치, 부모, 순서 정보를 모두 저장합니다.
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalAnchoredPosition = rectTransform.anchoredPosition;
        thresholdY = Screen.height * playAreaThreshold; // 현재 화면 높이 기준으로 필드 영역 Y좌표 계산

        // 드래그 중인 카드가 다른 UI 요소들에 가려지지 않도록,
        // 일시적으로 최상위 Canvas의 자식으로 옮기고 가장 위에 보이도록 설정합니다.
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();

        // 드래그 중인 카드 자체는 레이캐스트의 대상이 되지 않도록 하여,
        // 카드 뒤에 있는 필드 슬롯이나 다른 카드를 감지할 수 있게 합니다.
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 드래그를 하는 동안 매 프레임 호출됩니다.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        bool currentlyOver = eventData.position.y > thresholdY;

        // 필드 영역에 처음 진입하거나, 필드 영역에서 처음 벗어나는 순간을 감지합니다.
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

        // 현재 상태에 따라 드래그 중인 오브젝트의 위치를 업데이트합니다.
        if (isOverPlayArea && fieldCardController != null)
        {
            // 필드 카드가 생성되었다면, FieldCardController가 스스로 움직임을 제어하도록 명령합니다.
            fieldCardController.UpdateDragTarget(eventData.position, eventData.delta);
        }
        else if (!isAimingFromHand)
        {
            // 아직 필드 카드로 전환되지 않았고, 주문 조준 상태도 아니라면,
            // 핸드 카드의 위치를 마우스 커서 위치로 직접 업데이트합니다.
            rectTransform.position = eventData.position;
        }
    }

    /// <summary>
    /// 드래그를 끝내는 순간(마우스 버튼을 뗄 때)에 한 번 호출됩니다.
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        // 단일 주문 조준 중이었다면, 그 결과를 먼저 처리합니다.
        if (isAimingFromHand)
        {
            AimingManager.Instance.StopAiming(); // 조준선 비활성화
            isAimingFromHand = false;

            // 마우스 커서 아래에 유효한 타겟(아군 또는 적 카드)이 있는지 확인합니다.
            if (eventData.pointerEnter != null && (eventData.pointerEnter.GetComponent<FieldCardController>() != null || eventData.pointerEnter.GetComponent<EnemyCardTarget>() != null))
            {
                Debug.Log(eventData.pointerEnter.name + "에 단일 주문 발동!");
                DestroyGhostCard(); // 잔상 제거
                HandManager.Instance.RemoveCardFromHand(handCardController); // 핸드에서 카드 정보 제거
                Destroy(gameObject); // 핸드 카드 오브젝트 파괴
            }
            else
            {
                ReturnToHand(); // 유효한 타겟이 아니면 핸드로 복귀
            }
            return; // 주문 처리가 끝났으므로 함수를 종료합니다.
        }

        // 필드 위에 카드가 있는 상태로 드래그를 끝냈다면, 그 결과를 처리합니다.
        if (isOverPlayArea && fieldCardInstance != null)
        {
            FieldSlot targetSlot = null;
            if (eventData.pointerEnter != null)
            {
                targetSlot = eventData.pointerEnter.GetComponent<FieldSlot>();
            }

            // 하수인 카드이고, 유효한 빈 슬롯 위에 놓았다면
            if (handCardController.cardData.cardType == CardType.하수인 && targetSlot != null && targetSlot.IsAvailable())
            {
                DestroyGhostCard(); // 잔상 제거
                targetSlot.OccupySlot(fieldCardController); // 슬롯을 점유 상태로 변경
                fieldCardController.StartPlacementAnimation(targetSlot.transform); // 배치 애니메이션 시작
                HandManager.Instance.RemoveCardFromHand(handCardController); // 핸드에서 카드 정보 제거
                Destroy(gameObject); // 핸드 카드 오브젝트 파괴
            }
            else
            {
                ReturnToHand(); // 유효하지 않은 슬롯이거나 다른 타입의 카드일 경우 핸드로 복귀
            }
        }
        else
        {
            ReturnToHand(); // 필드 위가 아닌 곳에서 드래그를 끝냈을 경우
        }
    }

    /// <summary>
    /// 필드 영역에 처음 진입했을 때 호출됩니다.
    /// 카드 타입에 따라 잔상 생성, 필드 카드 전환, 주문 조준 시작 등의 행동을 결정합니다.
    /// </summary>
    void OnEnterPlayArea(PointerEventData eventData)
    {
        // 잔상이 없다면, 상호작용 없는 깨끗한 잔상 프리팹을 사용해 생성합니다.
        if (ghostCardInstance == null && ghostCardPrefab != null)
        {
            ghostCardInstance = Instantiate(ghostCardPrefab, originalParent);
            ghostCardInstance.transform.SetSiblingIndex(originalSiblingIndex);
            ghostCardInstance.GetComponent<RectTransform>().anchoredPosition = originalAnchoredPosition;

            // 잔상이 원래 카드와 똑같이 보이도록 데이터를 설정합니다.
            CardDisplay ghostDisplay = ghostCardInstance.GetComponent<CardDisplay>();
            if (ghostDisplay != null)
            {
                CardDisplay originalDisplay = GetComponent<CardDisplay>();
                ghostDisplay.cardData = originalDisplay.cardData;
                ghostDisplay.ApplyCardData();
            }

            // 잔상을 투명하게 만들고 상호작용을 막습니다.
            CanvasGroup ghostCG = ghostCardInstance.GetComponent<CanvasGroup>();
            if (ghostCG != null)
            {
                ghostCG.alpha = 0.4f;
                ghostCG.blocksRaycasts = false;
            }
        }

        // 원래 드래그하던 카드는 화면에서 보이지 않도록 투명하게 만듭니다.
        canvasGroup.alpha = 0;

        CardData data = handCardController.cardData;

        // 카드 타입에 따라 다른 행동을 처리합니다.
        if (data.cardType == CardType.주문 && data.spellType == SpellType.단일_대상)
        {
            isAimingFromHand = true;
            AimingManager.Instance.StartAiming(ghostCardInstance);
        }
        else
        {
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

    /// <summary>
    /// 필드에서 핸드 영역으로 처음 벗어났을 때 호출됩니다.
    /// 생성했던 모든 임시 오브젝트(잔상, 필드카드)와 상태를 초기화합니다.
    /// </summary>
    void OnLeavePlayArea()
    {
        DestroyGhostCard();

        if (isAimingFromHand)
        {
            isAimingFromHand = false;
            AimingManager.Instance.StopAiming();
        }

        DestroyFieldCard();

        canvasGroup.alpha = 1; // 원래 카드를 다시 보이게 합니다.
    }

    /// <summary>
    /// 드래그를 취소하고 카드를 원래 손 위치로 되돌리는 최종 정리 함수입니다.
    /// </summary>
    void ReturnToHand()
    {
        // 모든 생성된 오브젝트와 상태를 확실하게 정리합니다.
        DestroyGhostCard();
        if (isAimingFromHand)
        {
            AimingManager.Instance.StopAiming();
            isAimingFromHand = false;
        }
        DestroyFieldCard();

        // UI 카드를 원래의 부모, 순서, 상태로 되돌립니다.
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1;
        transform.SetParent(originalParent);
        transform.SetSiblingIndex(originalSiblingIndex);

        // HandManager에게 카드 정렬을 다시 요청하여 핸드를 깔끔하게 만듭니다.
        if (HandManager.Instance != null)
        {
            HandManager.Instance.ArrangeCards();
        }
    }

    /// <summary>
    /// 잔상 카드를 안전하게 파괴합니다.
    /// </summary>
    private void DestroyGhostCard()
    {
        if (ghostCardInstance != null)
        {
            Debug.Log("잔상 파괴");
            Destroy(ghostCardInstance);
        }
    }

    /// <summary>
    /// 필드 카드를 안전하게 파괴합니다.
    /// </summary>
    private void DestroyFieldCard()
    {
        if (fieldCardInstance != null)
        {
            // 필드 카드는 DOTween 애니메이션을 가질 수 있으므로, 안전하게 DOKill()을 호출합니다.
            fieldCardInstance.transform.DOKill();
            Destroy(fieldCardInstance);
            fieldCardController = null;
        }
    }
}
