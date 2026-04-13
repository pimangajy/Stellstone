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
    public float temporaryBaseCardSpacingAngle;

    [Header("손패 상태 위치 설정")]
    public Transform foldAnchor;   // 우측 하단 (접혔을 때의 위치/회전 기준점)
    public Transform spreadAnchor; // 화면 중앙 하단 (기존 handAnchor의 기본 위치)
    public float foldDuration = 0.5f;
    public bool isFolded = false;  // 현재 접혀있는지 여부

    [Header("카드 호버(Hover) 효과")]
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
        temporaryBaseCardSpacingAngle = baseCardSpacingAngle;
    }

    void Update()
    {
        _handMathPlane = new Plane(handAnchor.up, handAnchor.position);
        // 테스트 모드일 때만 작동하도록 조건문을 걸어두면 좋습니다.
        if (isFolded && Application.isEditor)
        {
            // 매 프레임 anchor의 위치를 동기화 (DOTween이 아닌 직접 대입)
            handAnchor.position = foldAnchor.position;
            handAnchor.rotation = foldAnchor.rotation;
        }

        // (테스트용) R키 누르면 카드 버리기
        if (Input.GetKeyDown(KeyCode.R) && handCards.Count > 0) RemoveLastCardFromHand();
        if(Input.GetKeyDown(KeyCode.F))
        {
            ToggleHandFold(); 
        }

    }

    // --- 외부 연동 함수들 ---

    public void ToggleHandFold()
    {
        // [수정] 접기 시작할 때는 즉시 true로 바꿔서 호버를 막음
        if (!isFolded)
        {
            isFolded = true;
            FoldHand();
        }
        else
        {
            // 펼칠 때는 다 펼쳐진 후(OnComplete)에 false로 바꿈
            SpreadHand();
        }

        AlignHand();
    }

    public void FoldHand()
    {
        baseCardSpacingAngle = baseCardSpacingAngle / 2;

        Sequence spreadSeq = DOTween.Sequence();

        spreadSeq.Append(handAnchor.DOMove(foldAnchor.position, foldDuration).SetEase(Ease.OutQuart));
        spreadSeq.Join(handAnchor.DORotateQuaternion(foldAnchor.rotation, foldDuration).SetEase(Ease.OutQuart));
        ClearHover();

        spreadSeq.OnComplete(() => {
            AlignHand();
        });
    }

    private void SpreadHand()
    {
        baseCardSpacingAngle = temporaryBaseCardSpacingAngle;

        Sequence spreadSeq = DOTween.Sequence();

        spreadSeq.Append(handAnchor.DOMove(spreadAnchor.position, foldDuration).SetEase(Ease.OutQuart));
        spreadSeq.Join(handAnchor.DORotateQuaternion(spreadAnchor.rotation, foldDuration).SetEase(Ease.OutQuart));

        // [수정] 애니메이션이 완전히 끝난 뒤에 호버가 가능하도록 설정
        spreadSeq.OnComplete(() => {
            AlignHand();
            isFolded = false;
        });
    }

    /// <summary>
    /// [GameInputManager가 호출] 멀리건 단계에서 특정 카드가 클릭되었을 때 실행됩니다.
    /// </summary>
    public void OnMulliganCardClicked(GameObject clickedCard)
    {
        // 1. 멀리건 페이즈가 아니면 무시
        if (!isMulliganPhase) return;

        // 2. 내 손패에 있거나, '이미 멀리건 대상으로 선택된' 카드라면 멀리건 매니저에게 알림
        bool isHandCard = handCards.Contains(clickedCard);
        bool isSelectedCard = mulliganManager._selectedCards.Contains(clickedCard);

        if (isHandCard || isSelectedCard)
        {
            mulliganManager.OnCardClicked(clickedCard);
        }
    }

    /// <summary>
    /// [GameInputManager가 호출] 매 프레임 마우스 위치를 받아 호버링(확대) 효과를 계산합니다.
    /// 기존의 수학적 거리 계산(부채꼴 모양 대응)을 그대로 유지합니다.
    /// </summary>
    public void ProcessHover(Vector2 mousePosition)
    {
        if (_mainCamera == null) return;

        // 멀리건 중이거나, 카드를 드래그 중이거나, 카드가 날아오는 중이면 호버 취소
        if (isMulliganPhase || _currentlyDraggedCard != null || !_isHandStable)
        {
            ClearHover();
            return;
        }

        // 마우스가 너무 높이(필드 쪽) 있으면 호버 해제
        float handZoneLimit = 0.4f;
        if (mousePosition.y / Screen.height > handZoneLimit)
        {
            ClearHover();
            return;
        }

        Ray ray = _mainCamera.ScreenPointToRay(mousePosition);
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
            ClearHover(); // 이전 카드 원상복구
            AnimateCardHoverEnter(hitCard); // 새 카드 확대
            _currentlyHoveredCard = hitCard;
        }
        // 허공으로 마우스가 갔을 때
        else if (hitCard == null && _currentlyHoveredCard != null)
        {
            ClearHover();
        }
    }

    /// <summary>
    /// 호버링 효과를 즉시 해제하고 카드를 원래 위치로 되돌립니다.
    /// </summary>
    public void ClearHover()
    {
        if (_currentlyHoveredCard != null)
        {
            AnimateCardHoverExit(_currentlyHoveredCard);
            _currentlyHoveredCard = null;
        }
    }

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

    /// <summary>
    /// 카드를 손패의 특정 위치에 삽입합니다.
    /// </summary>
    public void InsertCardToHand(GameObject cardObject, int index)
    {
        if (!_isCardScaleSet)
        {
            _originalCardScale = cardObject.transform.localScale;
            _isCardScaleSet = true;
        }

        cardObject.transform.SetParent(handAnchor, true);

        // 인덱스 범위 안전성 체크
        int targetIndex = Mathf.Clamp(index, 0, handCards.Count);
        handCards.Insert(targetIndex, cardObject); // List.Insert 사용

        UpdateHandLayout(cardObject, newCardTravelDuration);
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

    /// <summary>
    /// 특정 카드 손패에서 삭제
    /// </summary>
    public void RemoveCardFromHand(GameObject cardToRemove)
    {
        if (cardToRemove == null || !handCards.Contains(cardToRemove)) return;
        handCards.Remove(cardToRemove);
        if (cardToRemove == _currentlyHoveredCard) _currentlyHoveredCard = null;

        Destroy(cardToRemove);
        UpdateHandLayout();
    }

    /// <summary>
    ///  특정 카드를 사용
    /// </summary>
    public void UseCardFromHand(GameObject cardToRemove)
    {
        if (cardToRemove == null || !handCards.Contains(cardToRemove)) return;
        handCards.Remove(cardToRemove);
        if (cardToRemove == _currentlyHoveredCard) _currentlyHoveredCard = null;

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

            // [수정] 부모가 움직여도 카드가 정상적으로 따라가도록 로컬 좌표 사용
            Vector3 targetLocalPosition = localArcPos + new Vector3(0, verticalPos, 0);
            Vector3 targetWorldPosition = handAnchor.TransformPoint(targetLocalPosition);

            // 목표 위치 저장 (기존 호버/Raycast 로직 유지를 위해 월드 좌표로 저장)
            _cardLayoutTargets[card] = (targetWorldPosition, targetRotation);

            float duration = (card == newCard) ? newCardDuration : shuffleDuration;
            Ease easeType = (card == newCard) ? Ease.InOutSine : Ease.OutQuad;

            // 호버 중인 카드는 위치만 업데이트 (확대된 상태 유지)
            if (card == _currentlyHoveredCard)
            {
                Vector3 currentLocalArcPos = (Quaternion.Euler(0, angle, 0) * Vector3.forward) * handArcRadius;
                Vector3 localHoverPos = new Vector3(currentLocalArcPos.x, hoverOffset.y, hoverOffset.z);

                // [수정] DOMove -> DOLocalMove 사용
                card.transform.DOLocalMove(localHoverPos, duration).SetEase(easeType);
                card.transform.DOLocalRotateQuaternion(Quaternion.identity, duration).SetEase(easeType);
                continue;
            }

            // 일반 카드 이동 
            // [수정] DOMove -> DOLocalMove, DORotateQuaternion -> DOLocalRotateQuaternion 사용
            Quaternion targetLocalRotation = Quaternion.Euler(0, angle, 0);
            card.transform.DOLocalMove(targetLocalPosition, duration).SetEase(easeType);
            card.transform.DOLocalRotateQuaternion(targetLocalRotation, duration).SetEase(easeType);
            card.transform.DOScale(_originalCardScale, duration).SetEase(easeType);
        }
    }
}