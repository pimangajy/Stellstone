using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 손패의 레이아웃과 호버(Hover)만 담당합니다.
/// 드래그 기능은 CardDragManager로 이관되었습니다.
/// </summary>
public class HandInteractionManager : MonoBehaviour
{
    public static HandInteractionManager instance;

    [Header("씬 연결")]
    public Transform handAnchor;
    public GameMulliganManager mulliganManager;

    [Header("상태")]
    public bool isMulliganPhase = false;

    [Header("손패 레이아웃")]
    public float handArcRadius = 4.0f;
    public float baseCardSpacingAngle = 8.0f;
    public float handSpreadMultiplier = 1.0f;
    public float cardVerticalOffset = 0.05f;
    public float shuffleDuration = 0.3f;
    public float newCardTravelDuration = 0.4f;

    [Header("카드 호버 효과")]
    public LayerMask handHoverPlaneLayer; // 호버 감지용
    public Vector3 hoverOffset = new Vector3(0, 0.8f, -0.3f);
    public float hoverScaleMultiplier = 1.2f;
    public float hoverAnimDuration = 0.2f;
    public float maxHoverActivationDistance = 2.0f;

    // --- private 변수들 ---
    public List<GameObject> handCards = new List<GameObject>();
    private Dictionary<GameObject, (Vector3 position, Quaternion rotation)> _cardLayoutTargets = new Dictionary<GameObject, (Vector3, Quaternion)>();
    private Camera _mainCamera;

    private GameObject _currentlyHoveredCard = null;
    private GameObject _currentlyDraggedCard = null; // CardDragManager가 설정함

    private Vector3 _originalCardScale = Vector3.one;
    private bool _isCardScaleSet = false;

    private Plane _handMathPlane;
    private bool _isHandStable = true;

    // 외부 참조 프로퍼티
    public Vector3 OriginalCardScale => _originalCardScale;
    public bool IsHandStable => _isHandStable;

    private void Awake()
    {
        if (instance != null && instance != this) Destroy(gameObject);
        else instance = this;
    }

    void Start()
    {
        _mainCamera = Camera.main;
    }

    void Update()
    {
        _handMathPlane = new Plane(handAnchor.up, handAnchor.position);

        if (isMulliganPhase)
        {
            HandleMulliganInput();
            return;
        }

        // 드래그 중이 아닐 때만 호버/정렬 로직 수행
        if (_currentlyDraggedCard == null)
        {
            if (Input.GetKeyDown(KeyCode.R) && handCards.Count > 0) RemoveLastCardFromHand();

            // 레이아웃 변경 감지 (에디터용) 코드는 생략 혹은 필요시 유지

            if (_isHandStable)
            {
                HandleCardHover_Math();
            }
        }
    }

    // --- CardDragManager와의 연동을 위한 공개 함수들 ---

    /// <summary>
    /// 현재 호버 중인 카드를 반환합니다. (CardDragManager가 드래그 시작 시 호출)
    /// </summary>
    public GameObject GetHoveredCard()
    {
        return _currentlyHoveredCard;
    }

    /// <summary>
    /// 드래그 중인 카드를 설정합니다. 
    /// 설정된 카드는 레이아웃 계산에서 제외됩니다.
    /// null을 넣으면 드래그가 끝난 것으로 간주하고 다시 정렬합니다.
    /// </summary>
    public void SetDraggedCard(GameObject card)
    {
        _currentlyDraggedCard = card;

        // 드래그 시작 시 호버 상태는 해제
        if (card != null)
        {
            _currentlyHoveredCard = null;
        }
    }

    /// <summary>
    /// 카드를 사용(소모)했을 때 호출. 리스트에서 제거하고 파괴합니다.
    /// </summary>
    public void UseCard(GameObject card)
    {
        RemoveCardFromHand(card);
    }

    // --- 이하 기존 로직 (호버, 정렬) ---

    private void HandleMulliganInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            // 멀리건 때는 카드 자체 콜라이더를 클릭
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (handCards.Contains(hit.collider.gameObject))
                    mulliganManager.OnCardClicked(hit.collider.gameObject);
            }
        }
    }

    private void HandleCardHover_Math()
    {
        if (_mainCamera == null) return;

        // 드래그 매니저의 영역 설정값을 가져오거나 상수로 관리
        float handZoneLimit = 0.4f; // 예시값
        if (Input.mousePosition.y / Screen.height > handZoneLimit)
        {
            if (_currentlyHoveredCard != null)
            {
                AnimateCardHoverExit(_currentlyHoveredCard);
                _currentlyHoveredCard = null;
            }
            return;
        }

        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        float enter = 0.0f;
        GameObject hitCard = null;

        if (_handMathPlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            hitCard = FindClosestCardToPoint(hitPoint);
        }

        if (hitCard != null && hitCard != _currentlyHoveredCard)
        {
            if (_currentlyHoveredCard != null) AnimateCardHoverExit(_currentlyHoveredCard);
            AnimateCardHoverEnter(hitCard);
            _currentlyHoveredCard = hitCard;
        }
        else if (hitCard == null && _currentlyHoveredCard != null)
        {
            AnimateCardHoverExit(_currentlyHoveredCard);
            _currentlyHoveredCard = null;
        }
    }

    public void AddCardToHand(GameObject newCardObject)
    {
        if (!_isCardScaleSet)
        {
            _originalCardScale = newCardObject.transform.localScale;
            _isCardScaleSet = true;
        }
        newCardObject.transform.SetParent(handAnchor, true);
        handCards.Add(newCardObject);
        UpdateHandLayout(newCardObject, newCardTravelDuration);
    }

    public void RemoveCardFromHandListOnly(GameObject card)
    {
        if (!handCards.Contains(card)) return;
        handCards.Remove(card);
        if (_currentlyHoveredCard == card) _currentlyHoveredCard = null;
    }

    public void AlignHand()
    {
        UpdateHandLayout(null, shuffleDuration);
    }

    private void RemoveCardFromHand(GameObject cardToRemove)
    {
        if (cardToRemove == null || !handCards.Contains(cardToRemove)) return;
        handCards.Remove(cardToRemove);
        if (cardToRemove == _currentlyHoveredCard) _currentlyHoveredCard = null;
        // 드래그 중인 카드는 DragManager가 이미 SetDraggedCard(null)을 호출했거나 할 것이므로 처리 안 함

        Destroy(cardToRemove);
        UpdateHandLayout();
    }

    private void RemoveLastCardFromHand()
    {
        if (handCards.Count == 0) return;
        RemoveCardFromHand(handCards[handCards.Count - 1]);
    }

    private GameObject FindClosestCardToPoint(Vector3 point)
    {
        GameObject closestCard = null;
        float minDistanceSqr = maxHoverActivationDistance * maxHoverActivationDistance;
        foreach (var card in handCards)
        {
            if (_cardLayoutTargets.TryGetValue(card, out var layoutTarget))
            {
                float distSqr = (layoutTarget.position - point).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestCard = card;
                }
            }
        }
        return closestCard;
    }

    private void AnimateCardHoverEnter(GameObject card)
    {
        card.transform.DOKill();
        int i = handCards.IndexOf(card);
        int cardCount = handCards.Count;
        float effectiveSpacingAngle = baseCardSpacingAngle * handSpreadMultiplier;
        float startAngle = (cardCount - 1) * effectiveSpacingAngle / 2.0f;
        float angle = startAngle - (i * effectiveSpacingAngle);

        Vector3 localArcPos = (Quaternion.Euler(0, angle, 0) * Vector3.forward) * handArcRadius;
        Vector3 localHoverPos = new Vector3(localArcPos.x, hoverOffset.y, hoverOffset.z);
        Vector3 targetHoverPosition = handAnchor.TransformPoint(localHoverPos);

        card.transform.DOMove(targetHoverPosition, hoverAnimDuration).SetEase(Ease.OutQuad);
        card.transform.DORotateQuaternion(handAnchor.rotation, hoverAnimDuration).SetEase(Ease.OutQuad);
        card.transform.DOScale(_originalCardScale * hoverScaleMultiplier, hoverAnimDuration).SetEase(Ease.OutQuad);
    }

    private void AnimateCardHoverExit(GameObject card)
    {
        if (!_cardLayoutTargets.TryGetValue(card, out var layoutTarget))
        {
            card.transform.DOScale(_originalCardScale, hoverAnimDuration).SetEase(Ease.OutQuad);
            return;
        }
        card.transform.DOKill();
        card.transform.DOMove(layoutTarget.position, hoverAnimDuration).SetEase(Ease.OutQuad);
        card.transform.DORotateQuaternion(layoutTarget.rotation, hoverAnimDuration).SetEase(Ease.OutQuad);
        card.transform.DOScale(_originalCardScale, hoverAnimDuration).SetEase(Ease.OutQuad);
    }

    private void UpdateHandLayout(GameObject newCard = null, float newCardDuration = 0.3f)
    {
        int cardCount = handCards.Count;
        if (cardCount == 0) return;

        _isHandStable = false;
        float maxDuration = (newCard != null) ? Mathf.Max(newCardDuration, shuffleDuration) : shuffleDuration;
        DOVirtual.DelayedCall(maxDuration, () => { _isHandStable = true; });

        for (int i = 0; i < cardCount; i++)
        {
            GameObject card = handCards[i];

            // (핵심) 드래그 중인 카드는 레이아웃 계산에서 제외
            if (card == _currentlyDraggedCard) continue;

            float angle = ((cardCount - 1) * baseCardSpacingAngle * handSpreadMultiplier / 2.0f) - (i * baseCardSpacingAngle * handSpreadMultiplier);
            Quaternion targetRotation = handAnchor.rotation * Quaternion.Euler(0, angle, 0);
            Vector3 localArcPos = (Quaternion.Euler(0, angle, 0) * Vector3.forward) * handArcRadius;
            float verticalPos = (cardCount - 1 - i) * cardVerticalOffset;
            Vector3 targetPosition = handAnchor.TransformPoint(localArcPos + new Vector3(0, verticalPos, 0));

            _cardLayoutTargets[card] = (targetPosition, targetRotation);

            float duration = (card == newCard) ? newCardDuration : shuffleDuration;
            Ease easeType = (card == newCard) ? Ease.InOutSine : Ease.OutQuad;

            if (card == _currentlyHoveredCard)
            {
                // 호버 중인 카드는 위치만 갱신 (회전은 정면 유지)
                Vector3 currentLocalArcPos = (Quaternion.Euler(0, angle, 0) * Vector3.forward) * handArcRadius;
                Vector3 localHoverPos = new Vector3(currentLocalArcPos.x, hoverOffset.y, hoverOffset.z);
                card.transform.DOMove(handAnchor.TransformPoint(localHoverPos), duration).SetEase(easeType);
                card.transform.DORotateQuaternion(handAnchor.rotation, duration).SetEase(easeType);
                continue;
            }

            card.transform.DOMove(targetPosition, duration).SetEase(easeType);
            card.transform.DORotateQuaternion(targetRotation, duration).SetEase(easeType);
            card.transform.DOScale(_originalCardScale, duration).SetEase(easeType);
        }
    }
}