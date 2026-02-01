using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 손패(Hand)에 있는 카드들을 부채꼴 모양으로 정렬하고,
/// 마우스를 올렸을 때(Hover) 확대되는 효과를 담당합니다.
/// (드래그 기능은 CardDragManager가 담당합니다)
/// </summary>
public class HandInteractionManager : MonoBehaviour
{
    public static HandInteractionManager instance;

    [Header("씬 연결")]
    public Transform handAnchor; // 손패의 기준점 (화면 아래 중앙)
    public GameMulliganManager mulliganManager; // 멀리건(첫 패 교체) 관리자

    [Header("상태")]
    public bool isMulliganPhase = false; // 지금 멀리건 중인가?

    [Header("손패 레이아웃 (부채꼴)")]
    public float handArcRadius = 4.0f; // 부채꼴 반지름 (클수록 완만함)
    public float baseCardSpacingAngle = 8.0f; // 카드 사이 각도 간격
    public float handSpreadMultiplier = 1.0f; // 간격 조절 계수
    public float cardVerticalOffset = 0.05f; // 카드끼리 겹칠 때 높이 차이
    public float shuffleDuration = 0.3f; // 카드 정렬 애니메이션 시간
    public float newCardTravelDuration = 0.4f; // 드로우된 카드가 날아오는 시간

    [Header("카드 호버(Hover) 효과")]
    public LayerMask handHoverPlaneLayer;
    public Vector3 hoverOffset = new Vector3(0, 0.8f, -0.3f); // 호버 시 얼마나 위/앞으로 튀어나올지
    public float hoverScaleMultiplier = 1.2f; // 호버 시 얼마나 커질지
    public float hoverAnimDuration = 0.2f;
    public float maxHoverActivationDistance = 2.0f; // 마우스와 얼마나 가까워야 반응할지

    // --- 내부 변수 ---
    public List<GameObject> handCards = new List<GameObject>(); // 현재 내 손에 있는 카드들
    private Dictionary<GameObject, (Vector3 position, Quaternion rotation)> _cardLayoutTargets = new Dictionary<GameObject, (Vector3, Quaternion)>();
    private Camera _mainCamera;

    private GameObject _currentlyHoveredCard = null; // 현재 마우스가 올라가 있는 카드
    private GameObject _currentlyDraggedCard = null; // 현재 드래그 중인 카드 (정렬에서 제외됨)

    private Vector3 _originalCardScale = Vector3.one;
    private bool _isCardScaleSet = false;
    private Plane _handMathPlane;
    private bool _isHandStable = true; // 카드가 움직이는 중인지 여부

    // 외부에서 읽기 전용으로 접근
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

        // 멀리건 중이면 멀리건 전용 입력 처리
        if (isMulliganPhase)
        {
            HandleMulliganInput();
            return;
        }

        // 드래그 중이 아닐 때만 호버 효과 및 정렬 계산
        if (_currentlyDraggedCard == null)
        {
            // (테스트용) R키 누르면 카드 버리기
            if (Input.GetKeyDown(KeyCode.R) && handCards.Count > 0) RemoveLastCardFromHand();

            if (_isHandStable)
            {
                HandleCardHover_Math(); // 마우스 위치 계산해서 호버 처리
            }
        }
    }

    // --- 외부 연동 함수들 ---

    /// <summary>
    /// 현재 마우스 아래 있는 카드를 반환합니다. (DragManager가 씀)
    /// </summary>
    public GameObject GetHoveredCard() => _currentlyHoveredCard;

    /// <summary>
    /// 드래그 중인 카드를 설정합니다. 드래그 중인 카드는 손패 정렬에서 빠집니다.
    /// </summary>
    public void SetDraggedCard(GameObject card)
    {
        _currentlyDraggedCard = card;
        if (card != null) _currentlyHoveredCard = null; // 드래그 시작하면 호버 해제
    }

    /// <summary>
    /// 카드를 사용해서 손패에서 제거할 때 호출합니다.
    /// </summary>
    public void UseCard(GameObject card) => RemoveCardFromHand(card);

    // --- 내부 로직 ---

    private void HandleMulliganInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                // 내 손패에 있는 카드를 클릭하면 멀리건 매니저에게 알림
                if (handCards.Contains(hit.collider.gameObject))
                    mulliganManager.OnCardClicked(hit.collider.gameObject);
            }
        }
    }

    // 마우스 위치와 가장 가까운 카드를 찾아서 호버 효과를 줍니다.
    private void HandleCardHover_Math()
    {
        if (_mainCamera == null) return;

        // 마우스가 너무 높이(필드 쪽) 있으면 호버 해제
        float handZoneLimit = 0.4f;
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

        // 다른 카드로 호버가 바뀌었을 때
        if (hitCard != null && hitCard != _currentlyHoveredCard)
        {
            if (_currentlyHoveredCard != null) AnimateCardHoverExit(_currentlyHoveredCard); // 이전 카드 복구
            AnimateCardHoverEnter(hitCard); // 새 카드 확대
            _currentlyHoveredCard = hitCard;
        }
        // 허공으로 마우스가 갔을 때
        else if (hitCard == null && _currentlyHoveredCard != null)
        {
            AnimateCardHoverExit(_currentlyHoveredCard);
            _currentlyHoveredCard = null;
        }
    }

    // 새 카드를 손패에 추가하고 애니메이션 실행
    public void AddCardToHand(GameObject newCardObject)
    {
        if (!_isCardScaleSet) // 첫 카드의 크기를 기준 크기로 저장
        {
            _originalCardScale = newCardObject.transform.localScale;
            _isCardScaleSet = true;
        }
        newCardObject.transform.SetParent(handAnchor, true);
        handCards.Add(newCardObject);
        UpdateHandLayout(newCardObject, newCardTravelDuration); // 재정렬
    }


    // 리스트에서만 빼기 (파괴 X, 멀리건 등으로 이동할 때 사용)
    public void RemoveCardFromHandListOnly(GameObject card)
    {
        if (!handCards.Contains(card)) return;
        handCards.Remove(card);
        if (_currentlyHoveredCard == card) _currentlyHoveredCard = null;
    }

    // 손패 강제 정렬
    public void AlignHand()
    {
        UpdateHandLayout(null, shuffleDuration);
    }

    // 특정 카드 손패에서 삭제
    public void RemoveCardFromHand(GameObject cardToRemove)
    {
        if (cardToRemove == null || !handCards.Contains(cardToRemove)) return;
        handCards.Remove(cardToRemove);
        if (cardToRemove == _currentlyHoveredCard) _currentlyHoveredCard = null;

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

    // 호버 시작 애니메이션 (커짐, 위로 올라옴)
    private void AnimateCardHoverEnter(GameObject card)
    {
        card.transform.DOKill();

        // 현재 위치에서 수직으로 위로 올리기 계산
        // (복잡한 수학 계산 생략: 결론적으로 호버 위치 계산)
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

    // 호버 종료 애니메이션 (원래 자리로)
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

    // 손패 전체 위치 재계산 (부채꼴 모양)
    private void UpdateHandLayout(GameObject newCard = null, float newCardDuration = 0.3f)
    {
        int cardCount = handCards.Count;
        if (cardCount == 0) return;

        _isHandStable = false; // 움직이는 중 표시
        float maxDuration = (newCard != null) ? Mathf.Max(newCardDuration, shuffleDuration) : shuffleDuration;
        DOVirtual.DelayedCall(maxDuration, () => { _isHandStable = true; }); // 시간 지나면 안정 상태로 변경

        for (int i = 0; i < cardCount; i++)
        {
            GameObject card = handCards[i];

            // 드래그 중인 카드는 정렬에서 제외 (마우스 따라가야 하니까)
            if (card == _currentlyDraggedCard) continue;

            // 부채꼴 각도 계산
            float angle = ((cardCount - 1) * baseCardSpacingAngle * handSpreadMultiplier / 2.0f) - (i * baseCardSpacingAngle * handSpreadMultiplier);
            Quaternion targetRotation = handAnchor.rotation * Quaternion.Euler(0, angle, 0);
            Vector3 localArcPos = (Quaternion.Euler(0, angle, 0) * Vector3.forward) * handArcRadius;

            // 겹칠 때 살짝 높이 차이 두기
            float verticalPos = (cardCount - 1 - i) * cardVerticalOffset;
            Vector3 targetPosition = handAnchor.TransformPoint(localArcPos + new Vector3(0, verticalPos, 0));

            // 목표 위치 저장
            _cardLayoutTargets[card] = (targetPosition, targetRotation);

            float duration = (card == newCard) ? newCardDuration : shuffleDuration;
            Ease easeType = (card == newCard) ? Ease.InOutSine : Ease.OutQuad;

            // 호버 중인 카드는 위치만 업데이트 (확대된 상태 유지)
            if (card == _currentlyHoveredCard)
            {
                Vector3 currentLocalArcPos = (Quaternion.Euler(0, angle, 0) * Vector3.forward) * handArcRadius;
                Vector3 localHoverPos = new Vector3(currentLocalArcPos.x, hoverOffset.y, hoverOffset.z);
                card.transform.DOMove(handAnchor.TransformPoint(localHoverPos), duration).SetEase(easeType);
                card.transform.DORotateQuaternion(handAnchor.rotation, duration).SetEase(easeType);
                continue;
            }

            // 일반 카드 이동
            card.transform.DOMove(targetPosition, duration).SetEase(easeType);
            card.transform.DORotateQuaternion(targetRotation, duration).SetEase(easeType);
            card.transform.DOScale(_originalCardScale, duration).SetEase(easeType);
        }
    }
}