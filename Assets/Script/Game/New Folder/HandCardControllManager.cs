using UnityEngine;
using UnityEngine.EventSystems; // 2D UI 클릭/호버 판정을 위해 필수적으로 사용하는 네임스페이스입니다.
using System.Collections.Generic;
using DG.Tweening; // 부드러운 UI 이동 및 애니메이션(DOTween)을 위한 라이브러리입니다.

/// <summary>
/// 2D UI 캔버스 기반: 손패(Hand)에 있는 카드들을 부채꼴 모양으로 예쁘게 정렬하고,
/// 마우스를 올렸을 때(Hover) 카드가 위로 튀어나오며 확대되는 효과를 전담하는 매니저입니다.
/// </summary>
public class HandCardControllManager : MonoBehaviour
{
    public static HandCardControllManager instance;

    [Header("UI 씬 연결")]
    [Tooltip("손패 카드들이 모이는 중심축입니다. (화면 아래 중앙에 위치한 빈 UI 객체여야 합니다)")]
    public RectTransform handAnchor;
    public GameMulliganManager mulliganManager;

    [Header("상태")]
    [Tooltip("현재 게임이 첫 패를 교체하는 멀리건 단계인지 확인하는 변수입니다.")]
    public bool isMulliganPhase = false;
    public bool isMulligan = false;

    [Header("손패 레이아웃 (2D 부채꼴 연출)")]
    [Tooltip("부채꼴을 그릴 가상의 원 반지름입니다. 2D 픽셀 해상도에 맞춰 1500~3000 정도로 크게 설정해야 완만한 곡선이 나옵니다.")]
    public float handArcRadius = 2000f;
    [Tooltip("카드와 카드 사이의 벌어지는 기본 각도입니다.")]
    public float baseCardSpacingAngle = 8.0f;
    [Tooltip("카드가 많아질 때 부채꼴이 너무 넓어지지 않도록 조절하는 계수입니다.")]
    public float handSpreadMultiplier = 1.0f;
    [Tooltip("카드가 섞이거나 정렬될 때 걸리는 애니메이션 시간입니다.")]
    public float shuffleDuration = 0.3f;
    [Tooltip("새로 뽑은 카드가 손패로 날아올 때 걸리는 시간입니다.")]
    public float newCardTravelDuration = 0.4f;
    [Tooltip("새로 뽑은 카드가 손패로 날아올 때 걸리는 시간입니다.")]
    public float radius = 50f;
    [Tooltip("새로 뽑은 카드가 손패로 날아올 때 걸리는 시간입니다.")]
    public float spacingAngle = 1f;

    // 카드가 접혔을 때 각도를 줄이기 위해 원래 각도를 임시 저장하는 변수
    public float temporaryBaseCardSpacingAngle;

    [Header("손패 상태 위치 설정")]
    [Tooltip("카드를 접어둘 때(숨길 때) 이동할 우측 하단의 위치/회전 기준점입니다.")]
    public RectTransform foldAnchor;
    [Tooltip("카드를 펼칠 때 이동할 화면 중앙 하단의 기준점입니다.")]
    public RectTransform spreadAnchor;
    public float foldDuration = 0.5f;
    public bool isFolded = false;
    // 현재 접기/펼치기 애니메이션이 진행 중인지 확인하는 변수
    private bool _isAnimatingFold = false;
    // 손패가 접혔을 때 카드의 크기 비율 (1.0 = 원래 크기, 0.7 = 70% 크기)
    public float foldScaleMultiplier = 0.7f;

    [Header("카드 호버(Hover) 효과")]
    [Tooltip("마우스를 올렸을 때 카드가 튀어 오르는 UI 좌표상의 이동 값입니다. (2D이므로 Z축 대신 Y축 픽셀 값을 100~200 정도로 크게 줍니다)")]
    public Vector2 hoverOffset = new Vector2(0, 150f);
    [Tooltip("마우스를 올렸을 때 커지는 배율입니다.")]
    public float hoverScaleMultiplier = 1.2f;
    public float hoverAnimDuration = 0.2f;

    [Header("잔상 카드 설정")]
    [Tooltip("카드를 드래그할 때 손패에 원래 있던 자리를 표시해 주는 반투명 카드 프리팹입니다.")]
    public GameObject phantomCardPrefab;
    private GameObject _activePhantomCard;

    // --- 내부 변수들 ---
    [Tooltip("현재 손패에 쥐고 있는 실제 카드들의 리스트입니다.")]
    public List<GameObject> handCards = new List<GameObject>();

    // 카드의 목표 위치와 회전값을 기억해 두는 사전(Dictionary)입니다. 
    // 호버링이 끝났을 때 이 값을 참고하여 원래 자리로 돌아갑니다.
    private Dictionary<GameObject, (Vector2 position, Quaternion rotation)> _cardLayoutTargets = new Dictionary<GameObject, (Vector2, Quaternion)>();

    private GameObject _currentlyHoveredCard = null; // 현재 마우스가 올라가 있는 카드
    private GameObject _currentlyDraggedCard = null; // 현재 마우스로 잡고 드래그 중인 카드

    private Vector3 _originalCardScale = Vector3.one; // 처음 생성된 카드의 기본 크기 저장
    private bool _isCardScaleSet = false;
    private bool _isHandStable = true; // 현재 손패 정렬 애니메이션이 끝나고 안정된 상태인지 확인

    // 외부 스크립트에서 참조하기 위한 읽기 전용 속성
    public Vector3 OriginalCardScale => _originalCardScale;
    public bool IsHandStable => _isHandStable;

    private void Awake()
    {
        // 싱글톤 패턴 적용 (어디서든 접근 가능하도록)
        if (instance != null && instance != this) Destroy(gameObject);
        else instance = this;
    }

    void Start()
    {
        if(GameClient.Instance != null)
            isMulliganPhase = true;

        temporaryBaseCardSpacingAngle = baseCardSpacingAngle; // 시작 시 원래 각도 기억
    }

    void Update()
    {
        // (테스트용) R키를 누르면 손패 맨 끝 카드를 버림 / F키를 누르면 손패를 접음
        if (Input.GetKeyDown(KeyCode.R) && handCards.Count > 0) RemoveLastCardFromHand();
        if (Input.GetKeyDown(KeyCode.F)) ToggleHandFold();
    }

    // ==========================================================
    // 손패 접기 / 펼치기 기능
    // ==========================================================
    public void ToggleHandFold()
    {
        if (_isAnimatingFold) return;
        if (CardDragManager.instance.IsWaitingForTarget) return;

        if (!isFolded)
        {
            isFolded = true;
            FoldHand();
        }
        else
        {
            SpreadHand();
        }
    }

    public void FoldHand()
    {
        _isAnimatingFold = true;

        temporaryBaseCardSpacingAngle = baseCardSpacingAngle;

        baseCardSpacingAngle = baseCardSpacingAngle / 2; // 카드를 겹치기 위해 간격 축소
        Sequence spreadSeq = DOTween.Sequence();

        // [수정됨] DOAnchorPos -> DOMove, anchoredPosition -> position 으로 변경
        // 앵커 기준점이 달라도 정확한 목표 지점의 화면 좌표로 이동합니다.
        spreadSeq.Append(handAnchor.DOMove(foldAnchor.position, foldDuration).SetEase(Ease.OutQuart));
        spreadSeq.Join(handAnchor.DORotateQuaternion(foldAnchor.rotation, foldDuration).SetEase(Ease.OutQuart));

        ClearHover();
        AlignHand();

        spreadSeq.OnComplete(() =>
        {
            _isAnimatingFold = false;
         });
    }

    private void SpreadHand()
    {
        isFolded = false;
        _isAnimatingFold = true;

        baseCardSpacingAngle = temporaryBaseCardSpacingAngle; // 간격 원래대로 복구
        Sequence spreadSeq = DOTween.Sequence();

        // [수정됨] 펼칠 때도 월드 좌표(position)와 회전(rotation)을 사용합니다.
        spreadSeq.Append(handAnchor.DOMove(spreadAnchor.position, foldDuration).SetEase(Ease.OutQuart));
        spreadSeq.Join(handAnchor.DORotateQuaternion(spreadAnchor.rotation, foldDuration).SetEase(Ease.OutQuart));
        AlignHand();
        spreadSeq.OnComplete(() => {
            _isAnimatingFold = false;
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

    // ==========================================================
    // UI 호버링 (마우스 올리기) 처리 
    // ==========================================================
    public void ProcessHover(Vector2 mousePosition)
    {
        // 멀리건 중이거나 카드를 집어 든 상태, 혹은 애니메이션 중이면 호버 반응을 무시합니다.
        if (isMulliganPhase || _currentlyDraggedCard != null || !_isHandStable)
        {
            ClearHover();
            return;
        }

        // 마우스가 화면 높이의 40% 위로 넘어가면(필드 쪽으로 가면) 즉시 호버를 풉니다.
        float handZoneLimit = 0.4f;
        if (mousePosition.y / Screen.height > handZoneLimit)
        {
            ClearHover();
            return;
        }

        // [중요] 2D UI 이벤트 시스템 레이캐스트
        // 카메라에서 광선을 쏘는 3D Physics.Raycast 대신, 
        // 캔버스 내 마우스 위치에 존재하는 모든 UI 요소를 찾아내는 EventSystem을 활용합니다.
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = mousePosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        GameObject hitCard = null;
        foreach (var hit in results)
        {
            // UI에 맞은 요소 중 최상위 카드 스크립트(GameCardDisplay)를 찾습니다.
            GameCardDisplay cardDisplay = hit.gameObject.GetComponentInParent<GameCardDisplay>();

            // 그 카드가 현재 손패 리스트에 들어있는 카드라면 타겟으로 확정
            if (cardDisplay != null && handCards.Contains(cardDisplay.gameObject))
            {
                hitCard = cardDisplay.gameObject;
                break;
            }
        }

        // 새로운 카드에 마우스가 올라간 경우
        if (hitCard != null && hitCard != _currentlyHoveredCard)
        {
            ClearHover(); // 이전 카드 원상복구
            AnimateCardHoverEnter(hitCard); // 새 카드 위로 올리기(확대)
            _currentlyHoveredCard = hitCard;
        }
        // 허공으로 마우스가 빠져나간 경우
        else if (hitCard == null && _currentlyHoveredCard != null)
        {
            ClearHover();
        }
    }

    // 떠오른 카드를 원래 자리로 복구하는 함수
    public void ClearHover()
    {
        if (_currentlyHoveredCard != null)
        {
            AnimateCardHoverExit(_currentlyHoveredCard);
            _currentlyHoveredCard = null;
        }
    }

    // ==========================================================
    // 카드 추가 / 삭제 관리
    // ==========================================================
    public void AddCardToHand(GameObject newCardObject)
    {
        handArcRadius += radius;
        if(!isMulligan)
        {
            if (isFolded)
            {
                baseCardSpacingAngle -= spacingAngle / 2;
                temporaryBaseCardSpacingAngle -= spacingAngle;
            }
            else
                baseCardSpacingAngle -= spacingAngle;
        }

        if (!_isCardScaleSet)
        {
            _originalCardScale = newCardObject.transform.localScale;
            _isCardScaleSet = true;
        }

        // 카드를 handAnchor의 자식으로 등록 (UI 레이아웃의 일부로 만들기)
        newCardObject.transform.SetParent(handAnchor, true);
        handCards.Add(newCardObject);

        // 카드 추가 후 둥근 부채꼴 모양으로 전체 재정렬
        UpdateHandLayout(newCardObject, newCardTravelDuration);
    }

    // 특정 위치에 카드 추가
    public void InsertCardToHand(GameObject cardObject, int index)
    {
        if (!_isCardScaleSet)
        {
            _originalCardScale = cardObject.transform.localScale;
            _isCardScaleSet = true;
        }

        // [수정됨] false -> true 로 변경
        cardObject.transform.SetParent(handAnchor, true);

        int targetIndex = Mathf.Clamp(index, 0, handCards.Count);
        handCards.Insert(targetIndex, cardObject);
        UpdateHandLayout(cardObject, newCardTravelDuration);
    }


    // 리스트에서만 삭제
    public void RemoveCardFromHandListOnly(GameObject card)
    {
        if (!handCards.Contains(card)) return;
        handCards.Remove(card);
        if (_currentlyHoveredCard == card) _currentlyHoveredCard = null;
    }

    public void AlignHand() => UpdateHandLayout(null, shuffleDuration);


    // 마지막 카드 제거
    private void RemoveLastCardFromHand()
    {
        if (handCards.Count == 0) return;
        RemoveCardFromHand(handCards[handCards.Count - 1]);
    }


    // 특정 카드 제거
    public void RemoveCardFromHand(GameObject cardToRemove)
    {
        handArcRadius -= radius;
        baseCardSpacingAngle += spacingAngle;

        if (cardToRemove == null || !handCards.Contains(cardToRemove)) return;
        handCards.Remove(cardToRemove);
        if (cardToRemove == _currentlyHoveredCard) _currentlyHoveredCard = null;

        Destroy(cardToRemove); // 실제 게임오브젝트 파괴
        UpdateHandLayout();
    }

    // ==========================================================
    // 호버 애니메이션 연출 (2D 캔버스 맞춤형)
    // ==========================================================
    public void AnimateCardHoverEnter(GameObject card)
    {
        card.transform.DOKill(); // 진행 중인 애니메이션 즉시 정지
        RectTransform cardRect = card.GetComponent<RectTransform>();

        // 미리 계산된 원래 부채꼴 좌표(position)에 마우스 오버 픽셀값(hoverOffset)을 더해 위로 띄웁니다.
        if (_cardLayoutTargets.TryGetValue(card, out var layoutTarget))
        {
            Vector2 targetHoverPosition = layoutTarget.position + hoverOffset;
            cardRect.DOAnchorPos(targetHoverPosition, hoverAnimDuration).SetEase(Ease.OutQuad);
        }

        // 회전값 0으로 만들기 (기울어진 카드가 똑바로 섭니다)
        cardRect.DOLocalRotateQuaternion(Quaternion.identity, hoverAnimDuration).SetEase(Ease.OutQuad);
        cardRect.DOScale(_originalCardScale * hoverScaleMultiplier, hoverAnimDuration).SetEase(Ease.OutQuad);

        // [핵심] Z축(깊이) 대신, UI 계층 구조에서 순서를 맨 끝으로 보내 화면 가장 앞에 보이게 합니다.
        cardRect.SetAsLastSibling();
    }

    public void AnimateCardHoverExit(GameObject card)
    {
        if (!_cardLayoutTargets.TryGetValue(card, out var layoutTarget))
        {
            card.transform.DOScale(_originalCardScale, hoverAnimDuration).SetEase(Ease.OutQuad);
            return;
        }

        card.transform.DOKill();
        RectTransform cardRect = card.GetComponent<RectTransform>();

        // 저장해둔 부채꼴 위치와 회전값으로 복귀
        cardRect.DOAnchorPos(layoutTarget.position, hoverAnimDuration).SetEase(Ease.OutQuad);
        cardRect.DOLocalRotateQuaternion(layoutTarget.rotation, hoverAnimDuration).SetEase(Ease.OutQuad);
        cardRect.DOScale(_originalCardScale, hoverAnimDuration).SetEase(Ease.OutQuad);

        // 손패 내 원래 인덱스를 찾아 겹침 순서(Z-order 역할)를 원상 복구
        int index = handCards.IndexOf(card);
        if (index != -1) cardRect.SetSiblingIndex(index);
    }

    // ==========================================================
    // [중요] 손패 2D 부채꼴 정렬 수학 로직
    // ==========================================================
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

            // 드래그 중인 카드는 부채꼴 공식에서 제외 (마우스를 따라다녀야 하므로)
            if (card == _currentlyDraggedCard) continue;
            // 멀리건으로 선택된 카드라면 손패 정렬(부채꼴) 연산에서 제외!
            if (mulliganManager != null && mulliganManager._selectedCards.Contains(card))
                continue;

            Vector3 targetScale = isFolded ? _originalCardScale * foldScaleMultiplier : _originalCardScale;


            RectTransform cardRect = card.GetComponent<RectTransform>();

            // 1. Z축 2D 회전 각도 계산
            // 카드 개수에 따라 전체 부채꼴이 몇 도나 벌어질지 구하고, 왼쪽 카드부터 차례대로 각도를 배정합니다.
            float totalAngle = (cardCount - 1) * baseCardSpacingAngle * handSpreadMultiplier;
            float startAngle = totalAngle / 2.0f;
            float angle = startAngle - (i * baseCardSpacingAngle * handSpreadMultiplier);

            Quaternion targetRotation = Quaternion.Euler(0, 0, angle); // 2D UI 회전은 Z축만 사용

            // 2. 삼각함수(Sin, Cos)를 이용한 2D 곡선 좌표 계산
            // 예전 3D에서는 Quaternion * Vector3.forward 를 썼지만, UI 캔버스에서는 수학 좌표가 필요합니다.
            // X값: 각도에 따른 좌우 픽셀 이동 (Sin 사용)
            float xPos = Mathf.Sin(-angle * Mathf.Deg2Rad) * handArcRadius;
            // Y값: 원형 곡선의 높이차 (Cos 사용, 1을 빼서 중심축이 맨 아래에 있도록 조절)
            float yPos = (Mathf.Cos(angle * Mathf.Deg2Rad) - 1f) * handArcRadius;

            Vector2 targetPos = new Vector2(xPos, yPos); // 최종 계산된 UI의 앵커(X, Y) 픽셀 좌표

            // 나중에 호버가 끝났을 때 돌아갈 수 있도록 값 기록
            _cardLayoutTargets[card] = (targetPos, targetRotation);

            // 왼쪽 카드가 가장 밑에, 오른쪽 카드가 가장 위에 쌓이게 정렬
            cardRect.SetSiblingIndex(i);

            float duration = (card == newCard) ? newCardDuration : shuffleDuration;
            Ease easeType = (card == newCard) ? Ease.InOutSine : Ease.OutQuad;

            // 지금 호버 중인 카드라면 원래 자리로 가지 않고, 호버된 위치(위로 튀어나온 상태)를 갱신
            if (card == _currentlyHoveredCard)
            {
                cardRect.DOAnchorPos(targetPos + hoverOffset, duration).SetEase(easeType);
                cardRect.DOLocalRotateQuaternion(Quaternion.identity, duration).SetEase(easeType);
                cardRect.DOScale(_originalCardScale * hoverScaleMultiplier, duration).SetEase(easeType);
                cardRect.SetAsLastSibling();
                continue;
            }

            // 일반 카드들은 계산된 부채꼴 곡선 좌표로 부드럽게 이동 (DOMove 대신 2D 전용인 DOAnchorPos 사용)
            cardRect.DOAnchorPos(targetPos, duration).SetEase(easeType);
            cardRect.DOLocalRotateQuaternion(targetRotation, duration).SetEase(easeType);
            cardRect.DOScale(targetScale, duration).SetEase(easeType);
        }
    }

    /// <summary>
    /// 드래그 중인 카드를 설정합니다. 드래그 중인 카드는 손패 정렬에서 빠집니다.
    /// </summary>
    public void SetDraggedCard(GameObject card)
    {
        _currentlyDraggedCard = card;
        if (card != null) _currentlyHoveredCard = null; // 드래그 시작하면 호버 해제
    }

    // ==========================================================
    // 잔상 카드 관리 (드래그 할 때 원래 위치 표시)
    // ==========================================================
    public void CreatePhantomCard(GameObject originalCard)
    {
        if (phantomCardPrefab == null) return;
        int index = handCards.IndexOf(originalCard);
        if (index == -1) return;

        // UI 캔버스 기반으로 Instantiate
        _activePhantomCard = Instantiate(phantomCardPrefab, handAnchor);
        RectTransform originalRect = originalCard.GetComponent<RectTransform>();
        RectTransform phantomRect = _activePhantomCard.GetComponent<RectTransform>();

        // 원래 카드의 UI 앵커 좌표와 회전을 그대로 복사
        phantomRect.anchoredPosition = originalRect.anchoredPosition;
        phantomRect.localRotation = originalRect.localRotation;

        phantomRect.SetSiblingIndex(index); // 원래 카드가 있던 뎁스(순서) 위치로 삽입
    }

    public void RemovePhantomCard(GameObject originalCard)
    {
        if (_activePhantomCard == null) return;
        Destroy(_activePhantomCard);
        Destroy(originalCard);
        _activePhantomCard = null;
        AlignHand();
    }
}