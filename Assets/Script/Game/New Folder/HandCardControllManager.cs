using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// 3D 손패(Hand) 카드 관리 매니저 - 원 둘레 부채꼴 정렬, F/R 삭제 기능, 손패 접기/펼치기, GameInputManager 연동 고정 Z높이 호버
/// </summary>
public class HandCardControllManager : MonoBehaviour
{
    public static HandCardControllManager instance;

    [Header("0. 테스트 모드 제어 (통합 스위치)")]
    [Tooltip("true이면 D, B, S, F, R, M 등 모든 키보드 테스트 단축키가 활성화됩니다.\nfalse이면 모든 테스트 단축키가 차단되어 실제 게임(서버 패킷 연동)처럼 동작합니다.")]
    public bool isTestMode = true;

    [Header("1. 3D 중심 앵커 (손패 기준점)")]
    [Tooltip("손패 카드들이 모이는 3D 중심축입니다.")]
    public Transform handAnchor;

    [Header("2. 원 둘레 부채꼴 레이아웃 (실시간 인스펙터 조절)")]
    [Tooltip("부채꼴 곡선을 만드는 가상 원의 반지름(미터 단위)입니다. 클수록 완만하고, 작을수록 둥글게 모입니다.")]
    public float circleRadius = 5.0f;

    [Tooltip("원 둘레를 따라 카드와 카드 사이의 각도 간격(도 단위)입니다.")]
    public float cardSpacingAngle = 5.0f;

    [Tooltip("카드가 들어오는 순서대로 우측 카드가 위로 올라오는 Y축 계단식 높이 간격입니다.")]
    public float cardDepthOffset = 0.02f;

    [Tooltip("Z축 아치 곡선의 위/아래 방향을 반전합니다. (중앙이 위, 양옆이 아래로 가도록 설정)")]
    public bool invertZCurve = false;

    [Tooltip("부채꼴 기울기 회전 방향을 반전합니다.")]
    public bool invertRotation = false;

    [Tooltip("카드가 카메라를 바라보는 기본 각도입니다 (탑뷰 기본값: X = -90)")]
    public Vector3 cardBaseRotation = new Vector3(-90f, 0f, 0f);

    [Tooltip("손패에 들고 있을 때 프리팹 원래 크기 대비 배율입니다 (1.0 = 100%)")]
    public float handScaleMultiplier = 1.0f;

    [Header("3. 손패 접기 / 펼치기 (Fold & Spread)")]
    [Tooltip("손패 접기/펼치기를 토글할 테스트 단축키입니다 (기본값: Tab).")]
    public KeyCode foldToggleKey = KeyCode.Tab;

    [Tooltip("손패가 접혔을 때 이동할 기준점 Transform입니다. (미지정 시 현재 자리에서 부채만 접힙니다)")]
    public Transform foldAnchor;

    [Tooltip("손패가 접혔을 때 카드 크기 배율입니다.")]
    public float foldScaleMultiplier = 0.8f;

    [Tooltip("손패가 접혔을 때 카드 간격 각도 축소 배율입니다 (카드가 가지런히 겹치도록 축소).")]
    public float foldAngleMultiplier = 0.2f;

    [Tooltip("손패가 접히거나 펼쳐질 때 걸리는 애니메이션 시간입니다.")]
    public float foldDuration = 0.35f;

    [Tooltip("현재 손패가 접혀있는 상태인지 여부입니다.")]
    public bool isFolded = false;

    [Header("4. 호버(Hover) 설정 (실시간 인스펙터 조절)")]
    [Tooltip("호버가 동작할 수 있는 화면 최대 높이 비율입니다 (기본: 0.35 = 화면 하단 35% 이하에서만 호버 동작).")]
    public float maxHoverHeightRatio = 0.35f;

    [Tooltip("호버 시 모든 카드가 도달할 고정 Z축 높이입니다. (좌우 카드가 원래 아래에 있더라도 모두 이 동일한 Z 높이까지 올라옵니다)")]
    public float hoverTargetZ = 1.0f;

    [Tooltip("호버 시 다른 손패 카드들보다 앞(위)으로 올라오도록 추가하는 Y축 높이입니다.")]
    public float hoverElevationY = 0.05f;

    [Tooltip("호버 시 카드 확대 배율입니다 (1.2 = 120%)")]
    public float hoverScaleMultiplier = 1.2f;

    [Tooltip("호버 전환 애니메이션 시간(초)입니다.")]
    public float hoverAnimDuration = 0.2f;

    [Header("5. 기본 애니메이션 설정")]
    public float moveDuration = 0.3f;

    [Header("6. 현재 손패 목록")]
    public List<GameObject> handCards = new List<GameObject>();

    // --- 내부 변수 ---
    private Vector3 _originalCardScale = Vector3.one;
    private bool _isCardScaleSet = false;

    // 카드별 원래 부채꼴 배치 좌표 (호버 종료 시 복구용)
    private struct CardRestingTransform
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }
    private readonly Dictionary<GameObject, CardRestingTransform> _cardRestingTransforms = new Dictionary<GameObject, CardRestingTransform>();

    // 카드별 레이아웃 기준 기본 스케일 (접힘/펼침/호버 상태에 따라 계산되는 목표 스케일)
    private readonly Dictionary<GameObject, Vector3> _cardBaseScales = new Dictionary<GameObject, Vector3>();

    // 카드별 버프 펀치 배율 (기본 1.0, 버프 연출 시 1.25 -> 1.0으로 애니메이션)
    private readonly Dictionary<GameObject, float> _cardBuffMultipliers = new Dictionary<GameObject, float>();

    // 스케일 및 버프 연출 트윈 참조
    private readonly Dictionary<GameObject, Tween> _cardScaleTweens = new Dictionary<GameObject, Tween>();
    private readonly Dictionary<GameObject, Tween> _cardBuffTweens = new Dictionary<GameObject, Tween>();

    private GameObject _currentlyHoveredCard = null;
    private GameObject _currentlyDraggedCard = null;

    // --- 외부 스크립트 호환용 프로퍼티/필드 (컴파일 보장) ---
    public Vector3 OriginalCardScale => _originalCardScale;
    public bool IsHandStable => true;
    public bool isMulliganPhase = false;
    public bool isMulligan = false;

    // --- 최적화: 매 프레임 레이캐스트 GC 할당 방지용 재사용 버퍼 ---
    private Camera _mainCamera;
    private readonly List<GameObject> _hoverCandidates = new List<GameObject>(16);
    private readonly List<RaycastResult> _hoverRaycastResults = new List<RaycastResult>(16);
    private readonly RaycastHit[] _hoverRaycastHits = new RaycastHit[16];
    private PointerEventData _hoverPointerData;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        // 테스트 모드일 때만 F/R 단축키 동작
        if (isTestMode)
        {
            // F키: 가장 먼저 드로우한 카드(0번 맨 왼쪽) 버리기
            if (Input.GetKeyDown(KeyCode.F))
            {
                RemoveFirstCardFromHand();
            }

            // R키: 랜덤한 카드 1장 버리기
            if (Input.GetKeyDown(KeyCode.R))
            {
                RemoveRandomCardFromHand();
            }

            // U키: 손패 첫 번째 카드의 비용 1 감소 및 스탯 증가 (UPDATE_HAND_CARDS 버프 테스트)
            if (Input.GetKeyDown(KeyCode.U))
            {
                TestBuffHandCard();
            }
        }

        // 접기/펼치기 단축키 토글 (기본: Tab)
        if (Input.GetKeyDown(foldToggleKey))
        {
            ToggleHandFold();
        }
    }

    /// <summary>
    /// 인스펙터에서 값을 변경할 때마다 인게임에 즉시 반영
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying && handCards != null && handCards.Count > 0)
        {
            UpdateHandLayout(immediate: true);
        }
    }

    // ==========================================================
    // 카드 추가 / 삭제 및 단축키 기능 (F / R)
    // ==========================================================

    /// <summary>
    /// F키 동작: 가장 먼저 들어온 카드(0번)를 손패에서 버립니다.
    /// </summary>
    public void RemoveFirstCardFromHand()
    {
        if (handCards == null || handCards.Count == 0) return;
        Debug.Log("[HandCardControllManager] F키 입력: 가장 먼저 드로우한 카드 버림");
        RemoveCardFromHand(handCards[0]);
    }

    /// <summary>
    /// R키 동작: 손패 중 무작위 1장을 선택해 버립니다.
    /// </summary>
    public void RemoveRandomCardFromHand()
    {
        if (handCards == null || handCards.Count == 0) return;
        int randomIndex = Random.Range(0, handCards.Count);
        Debug.Log($"[HandCardControllManager] R키 입력: 랜덤 카드 버림 (인덱스 {randomIndex})");
        RemoveCardFromHand(handCards[randomIndex]);
    }

    /// <summary>
    /// 드로우된 새 카드를 손패로 추가하고 정렬합니다.
    /// </summary>
    public void AddCardToHand(GameObject newCardObject)
    {
        if (newCardObject == null) return;

        if (!_isCardScaleSet)
        {
            if (CardDrawManager.Instance != null && CardDrawManager.Instance.cardPrefab != null)
                _originalCardScale = CardDrawManager.Instance.cardPrefab.transform.localScale;
            else
                _originalCardScale = newCardObject.transform.localScale;
            _isCardScaleSet = true;
        }

        Transform parentAnchor = handAnchor != null ? handAnchor : transform;
        newCardObject.transform.SetParent(parentAnchor, true);

        handCards.Add(newCardObject);
        UpdateHandLayout(immediate: false);
    }

    public void InsertCardToHand(GameObject cardObject, int index)
    {
        if (cardObject == null) return;
        Transform parentAnchor = handAnchor != null ? handAnchor : transform;
        cardObject.transform.SetParent(parentAnchor, true);

        int targetIndex = Mathf.Clamp(index, 0, handCards.Count);
        handCards.Insert(targetIndex, cardObject);
        UpdateHandLayout(immediate: false);
    }

    public void RemoveCardFromHand(GameObject card)
    {
        if (card == null || !handCards.Contains(card)) return;
        if (card == _currentlyHoveredCard) _currentlyHoveredCard = null;
        handCards.Remove(card);
        _cardRestingTransforms.Remove(card);
        if (_cardScaleTweens.TryGetValue(card, out var stw) && stw != null) stw.Kill();
        if (_cardBuffTweens.TryGetValue(card, out var btw) && btw != null) btw.Kill();
        _cardScaleTweens.Remove(card);
        _cardBuffTweens.Remove(card);
        _cardBaseScales.Remove(card);
        _cardBuffMultipliers.Remove(card);
        card.transform.DOKill();
        Destroy(card);
        UpdateHandLayout(immediate: false);
    }

    public void RemoveCardFromHandListOnly(GameObject card)
    {
        if (!handCards.Contains(card)) return;
        if (card == _currentlyHoveredCard) _currentlyHoveredCard = null;
        handCards.Remove(card);
        _cardRestingTransforms.Remove(card);
        if (_cardScaleTweens.TryGetValue(card, out var stw) && stw != null) stw.Kill();
        if (_cardBuffTweens.TryGetValue(card, out var btw) && btw != null) btw.Kill();
        _cardScaleTweens.Remove(card);
        _cardBuffTweens.Remove(card);
        _cardBaseScales.Remove(card);
        _cardBuffMultipliers.Remove(card);
        UpdateHandLayout(immediate: false);
    }

    public void AlignHand() => UpdateHandLayout(immediate: false);

    // ==========================================================
    // 손패 접기 / 펼치기 (Fold & Spread)
    // ==========================================================

    /// <summary>
    /// 손패 접힘/펼침 상태를 토글합니다.
    /// </summary>
    public void ToggleHandFold()
    {
        if (isFolded) SpreadHand();
        else FoldHand();
    }

    /// <summary>
    /// 손패를 가지런히 접습니다.
    /// </summary>
    public void FoldHand()
    {
        ClearHover();
        isFolded = true;
        UpdateHandLayout(immediate: false, customDuration: foldDuration);
    }

    /// <summary>
    /// 손패를 다시 부채꼴로 펼칩니다.
    /// </summary>
    public void SpreadHand()
    {
        isFolded = false;
        UpdateHandLayout(immediate: false, customDuration: foldDuration);
    }

    // ==========================================================
    // [핵심 1] GameInputManager 연동 호버(Hover) 시스템
    // ==========================================================

    /// <summary>
    /// GameInputManager에서 매 프레임 호출되는 마우스 호버 처리 함수
    /// </summary>
    public void ProcessHover(Vector2 mousePosition)
    {
        // 멀리건 중이거나 드래그 중이거나 손패가 접혀 있으면 호버 해제
        if (isMulliganPhase || _currentlyDraggedCard != null || isFolded)
        {
            ClearHover();
            return;
        }

        // 커서가 지정된 높이 비율(기본 화면 하단 35%)을 초과하면 호버 해제 및 신규 호버 차단
        if ((mousePosition.y / Screen.height) > maxHoverHeightRatio)
        {
            ClearHover();
            return;
        }

        GameObject hitCard = RaycastForHandCard(mousePosition);

        // 새로운 카드 위로 마우스가 올라간 경우
        if (hitCard != null && hitCard != _currentlyHoveredCard)
        {
            ClearHover();
            _currentlyHoveredCard = hitCard;
            AnimateHoverEnter(_currentlyHoveredCard);
        }
        // 마우스가 카드 밖으로 벗어난 경우
        else if (hitCard == null && _currentlyHoveredCard != null)
        {
            ClearHover();
        }
    }

    /// <summary>
    /// 마우스 위치에 있는 손패 카드를 uGUI 및 3D Physics Raycast로 감지합니다.
    /// 카드가 확대되어 여러 장이 마우스 아래에 겹친 경우, 원래 휴식 위치(Resting Position)의 화면 X좌표와 마우스 사이의 거리가 가장 가까운 카드를 선택합니다.
    /// </summary>
    private GameObject RaycastForHandCard(Vector2 mousePosition)
    {
        _hoverCandidates.Clear();

        // 1. uGUI EventSystem Raycast (캔버스 UI 기반 카드 감지)
        if (EventSystem.current != null)
        {
            if (_hoverPointerData == null)
            {
                _hoverPointerData = new PointerEventData(EventSystem.current);
            }
            _hoverPointerData.position = mousePosition;
            _hoverRaycastResults.Clear();
            EventSystem.current.RaycastAll(_hoverPointerData, _hoverRaycastResults);

            for (int r = 0; r < _hoverRaycastResults.Count; r++)
            {
                var hit = _hoverRaycastResults[r];
                if (hit.gameObject == null) continue;
                for (int i = 0; i < handCards.Count; i++)
                {
                    GameObject card = handCards[i];
                    if (card != null && (hit.gameObject == card || hit.gameObject.transform.IsChildOf(card.transform)))
                    {
                        if (!_hoverCandidates.Contains(card))
                        {
                            _hoverCandidates.Add(card);
                        }
                    }
                }
            }
        }

        // 2. 3D Physics Raycast (3D 콜라이더 기반 카드 감지 - NonAlloc 버퍼 재사용)
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            Ray ray = _mainCamera.ScreenPointToRay(mousePosition);
            int hitCount = Physics.RaycastNonAlloc(ray, _hoverRaycastHits, 100f);
            for (int h = 0; h < hitCount; h++)
            {
                var col = _hoverRaycastHits[h].collider;
                if (col == null) continue;
                for (int i = 0; i < handCards.Count; i++)
                {
                    GameObject card = handCards[i];
                    if (card != null && (col.gameObject == card || col.transform.IsChildOf(card.transform)))
                    {
                        if (!_hoverCandidates.Contains(card))
                        {
                            _hoverCandidates.Add(card);
                        }
                    }
                }
            }
        }

        if (_hoverCandidates.Count == 0) return null;
        if (_hoverCandidates.Count == 1) return _hoverCandidates[0];

        // 3. 카드가 2장 이상 겹친 경우: 원래 휴식 위치의 화면 X 좌표와 마우스 X 사이의 거리가 가장 가까운 카드를 선택
        GameObject bestCard = _hoverCandidates[0];
        float minDistance = float.MaxValue;
        Transform anchor = handAnchor != null ? handAnchor : transform;

        for (int i = 0; i < _hoverCandidates.Count; i++)
        {
            GameObject card = _hoverCandidates[i];
            Vector3 worldPos;
            if (_cardRestingTransforms.TryGetValue(card, out var resting))
            {
                worldPos = anchor.TransformPoint(resting.localPosition);
            }
            else
            {
                worldPos = card.transform.position;
            }

            Vector2 screenPos = _mainCamera != null ? (Vector2)_mainCamera.WorldToScreenPoint(worldPos) : (Vector2)worldPos;
            float distX = Mathf.Abs(mousePosition.x - screenPos.x);

            if (distX < minDistance)
            {
                minDistance = distX;
                bestCard = card;
            }
        }

        return bestCard;
    }

    // ==========================================================
    // 카드 스케일 및 버프 펀치 애니메이션 통합 관리
    // ==========================================================

    /// <summary>
    /// 카드의 최종 로컬 스케일을 (기본 레이아웃 스케일 * 버프 펀치 배율)로 즉시 적용합니다.
    /// </summary>
    private void ApplyCardScale(GameObject card)
    {
        if (card == null) return;

        if (!_cardBaseScales.TryGetValue(card, out Vector3 baseScale))
        {
            baseScale = isFolded ? (_originalCardScale * handScaleMultiplier * foldScaleMultiplier)
                                 : (_originalCardScale * handScaleMultiplier);
            _cardBaseScales[card] = baseScale;
        }

        float buffMult = _cardBuffMultipliers.TryGetValue(card, out float m) ? m : 1f;
        card.transform.localScale = baseScale * buffMult;
    }

    /// <summary>
    /// 카드의 기준 스케일을 즉시 설정합니다.
    /// </summary>
    private void SetCardBaseScale(GameObject card, Vector3 targetScale)
    {
        if (card == null) return;

        if (_cardScaleTweens.TryGetValue(card, out var tw) && tw != null && tw.IsActive())
        {
            tw.Kill();
        }

        _cardBaseScales[card] = targetScale;
        ApplyCardScale(card);
    }

    /// <summary>
    /// 카드의 기준 스케일을 duration 동안 부드럽게 전환합니다. (버프 연출 배율과 합성되어 실시간 반영)
    /// </summary>
    private void TweenCardBaseScale(GameObject card, Vector3 targetScale, float duration)
    {
        if (card == null) return;

        if (_cardScaleTweens.TryGetValue(card, out var tw) && tw != null && tw.IsActive())
        {
            tw.Kill();
        }

        if (!_cardBaseScales.TryGetValue(card, out Vector3 startScale))
        {
            float m = _cardBuffMultipliers.TryGetValue(card, out float bm) && bm > 0.001f ? bm : 1f;
            startScale = card.transform.localScale / m;
            _cardBaseScales[card] = startScale;
        }

        Vector3 curScale = startScale;
        _cardScaleTweens[card] = DOTween.To(
            () => curScale,
            x => {
                curScale = x;
                _cardBaseScales[card] = x;
                ApplyCardScale(card);
            },
            targetScale,
            duration
        ).SetEase(Ease.OutQuad).OnComplete(() => {
            _cardBaseScales[card] = targetScale;
            ApplyCardScale(card);
        });
    }

    /// <summary>
    /// 카드 버프 시 튀어오르는 펀치 연출을 실행합니다.
    /// 손패 접기/펼치기/이동 트윈을 방해하지 않고 독립적인 배율 펀치로 합성되어 동작합니다.
    /// </summary>
    public void PlayCardBuffPunch(GameObject card)
    {
        if (card == null || card == _currentlyDraggedCard) return;

        if (_cardBuffTweens.TryGetValue(card, out var tw) && tw != null && tw.IsActive())
        {
            tw.Kill();
        }

        Vector3 punchVector = Vector3.zero;
        _cardBuffMultipliers[card] = 1f;

        _cardBuffTweens[card] = DOTween.Punch(
            () => punchVector,
            offset => {
                punchVector = offset;
                _cardBuffMultipliers[card] = 1f + offset.x;
                ApplyCardScale(card);
            },
            new Vector3(0.26f, 0.26f, 0.26f),
            0.45f,
            4,
            0.5f
        ).SetEase(Ease.OutQuad).OnComplete(() => {
            _cardBuffMultipliers[card] = 1f;
            ApplyCardScale(card);
        });
    }

    /// <summary>
    /// 카드가 호버될 때 애니메이션: 고정 Z 높이로 상승, 똑바로 직립, 확대
    /// </summary>
    public void AnimateHoverEnter(GameObject card)
    {
        if (card == null) return;

        Vector3 restingPos = card.transform.localPosition;
        Vector3 restingScale = card.transform.localScale;
        if (_cardRestingTransforms.TryGetValue(card, out var resting))
        {
            restingPos = resting.localPosition;
            restingScale = resting.localScale;
        }

        // [핵심] Y축은 다른 모든 손패보다 맨 위로, Z축은 카드의 원래 위치와 상관없이 동일한 고정 hoverTargetZ로 설정
        float topY = (handCards.Count * cardDepthOffset) + hoverElevationY;
        Vector3 hoverPos = new Vector3(restingPos.x, topY, hoverTargetZ);

        // 정면 직립 회전 및 확대 배율 적용
        Quaternion hoverRotation = Quaternion.Euler(cardBaseRotation.x, 0f, cardBaseRotation.z);
        Vector3 hoverScale = restingScale * hoverScaleMultiplier;

        // UI 캔버스 상 최상위로 올리기
        card.transform.SetAsLastSibling();

        card.transform.DOKill();
        card.transform.DOLocalMove(hoverPos, hoverAnimDuration).SetEase(Ease.OutQuad);
        card.transform.DOLocalRotateQuaternion(hoverRotation, hoverAnimDuration).SetEase(Ease.OutQuad);
        TweenCardBaseScale(card, hoverScale, hoverAnimDuration);
    }

    /// <summary>
    /// 호버 해제 시 애니메이션: 원래 원 둘레 배치, 부채꼴 기울기, 크기로 복구
    /// </summary>
    public void AnimateHoverExit(GameObject card)
    {
        if (card == null) return;

        if (_cardRestingTransforms.TryGetValue(card, out var resting))
        {
            card.transform.DOKill();
            card.transform.DOLocalMove(resting.localPosition, hoverAnimDuration).SetEase(Ease.OutQuad);
            card.transform.DOLocalRotateQuaternion(resting.localRotation, hoverAnimDuration).SetEase(Ease.OutQuad);
            TweenCardBaseScale(card, resting.localScale, hoverAnimDuration);
        }
        else
        {
            card.transform.DOKill();
            Vector3 defaultScale = isFolded ? (_originalCardScale * handScaleMultiplier * foldScaleMultiplier)
                                            : (_originalCardScale * handScaleMultiplier);
            TweenCardBaseScale(card, defaultScale, hoverAnimDuration);
        }

        // 원래 손패 순서 계층으로 복귀
        int originalIndex = handCards.IndexOf(card);
        if (originalIndex != -1)
        {
            card.transform.SetSiblingIndex(originalIndex);
        }
    }

    /// <summary>
    /// 현재 호버된 카드를 원상 복구하고 호버 상태를 해제합니다.
    /// </summary>
    public void ClearHover()
    {
        if (_currentlyHoveredCard != null)
        {
            AnimateHoverExit(_currentlyHoveredCard);
            _currentlyHoveredCard = null;
        }
    }

    // ==========================================================
    // [핵심 2] 원 둘레 기반 손패 정렬 로직 (호버 상태 유지 지원)
    // ==========================================================
    public void UpdateHandLayout(bool immediate = false, float customDuration = -1f)
    {
        int cardCount = handCards.Count;
        if (cardCount == 0) return;

        float centerIndex = (cardCount - 1) / 2.0f;
        float duration = (customDuration > 0f) ? customDuration : moveDuration;

        // 접힌 상태일 때는 각도와 크기 축소 적용
        float effectiveSpacing = isFolded ? (cardSpacingAngle * foldAngleMultiplier) : cardSpacingAngle;
        Vector3 targetScale = isFolded ? (_originalCardScale * handScaleMultiplier * foldScaleMultiplier)
                                       : (_originalCardScale * handScaleMultiplier);

        Transform anchor = handAnchor != null ? handAnchor : transform;
        Vector3 foldOffset = Vector3.zero;
        if (isFolded && foldAnchor != null)
        {
            foldOffset = anchor.InverseTransformPoint(foldAnchor.position);
        }

        _cardRestingTransforms.Clear();

        for (int i = 0; i < cardCount; i++)
        {
            GameObject card = handCards[i];
            if (card == null) continue;

            float u = i - centerIndex;
            float angle = u * effectiveSpacing;
            float rad = angle * Mathf.Deg2Rad;

            // X-Z 원 둘레 좌표 계산
            float xPos = circleRadius * Mathf.Sin(rad);
            float zOffset = circleRadius * (1f - Mathf.Cos(rad));
            float zPos = invertZCurve ? -zOffset : zOffset;
            float yPos = i * cardDepthOffset; // 좌->우 계단식 쌓임

            Vector3 targetLocalPos = new Vector3(xPos, yPos, zPos) + foldOffset;

            // Y축 부채꼴 회전 계산 (좌측 ↖, 중앙 ↑, 우측 ↗)
            float yAngle = invertRotation ? angle : -angle;
            Quaternion targetRotation = Quaternion.Euler(cardBaseRotation.x, yAngle, cardBaseRotation.z);
            if (isFolded && foldAnchor != null)
            {
                targetRotation = Quaternion.Inverse(anchor.rotation) * foldAnchor.rotation * targetRotation;
            }

            // 휴식 상태 트랜스폼 캐싱 (호버 종료 시 복구에 사용)
            _cardRestingTransforms[card] = new CardRestingTransform
            {
                localPosition = targetLocalPos,
                localRotation = targetRotation,
                localScale = targetScale
            };

            // 만약 현재 드래그 중인 카드라면, 손패 레이아웃 이동에서 제외 (마우스 추적 방해 방지)
            if (card == _currentlyDraggedCard)
            {
                continue;
            }

            // 만약 현재 카드가 호버 중인 카드라면, 고정 Z높이 호버 위치를 유지
            if (card == _currentlyHoveredCard && !isFolded)
            {
                float topY = (cardCount * cardDepthOffset) + hoverElevationY;
                Vector3 hoverPos = new Vector3(targetLocalPos.x, topY, hoverTargetZ);
                Quaternion hoverRotation = Quaternion.Euler(cardBaseRotation.x, 0f, cardBaseRotation.z);
                Vector3 hoverScale = targetScale * hoverScaleMultiplier;

                card.transform.DOKill();
                if (immediate)
                {
                    card.transform.localPosition = hoverPos;
                    card.transform.localRotation = hoverRotation;
                    SetCardBaseScale(card, hoverScale);
                }
                else
                {
                    card.transform.DOLocalMove(hoverPos, duration).SetEase(Ease.OutQuad);
                    card.transform.DOLocalRotateQuaternion(hoverRotation, duration).SetEase(Ease.OutQuad);
                    TweenCardBaseScale(card, hoverScale, duration);
                }
                card.transform.SetAsLastSibling();
            }
            else
            {
                card.transform.DOKill();
                if (immediate)
                {
                    card.transform.localPosition = targetLocalPos;
                    card.transform.localRotation = targetRotation;
                    SetCardBaseScale(card, targetScale);
                }
                else
                {
                    card.transform.DOLocalMove(targetLocalPos, duration).SetEase(Ease.OutQuad);
                    card.transform.DOLocalRotateQuaternion(targetRotation, duration).SetEase(Ease.OutQuad);
                    TweenCardBaseScale(card, targetScale, duration);
                }
            }
        }
    }

    // ==========================================================
    // 씬 뷰 기즈모 (원 둘레 곡선 및 카드 방향, 호버 높이 라인 시각화)
    // ==========================================================
    private void OnDrawGizmosSelected()
    {
        Transform anchor = handAnchor != null ? handAnchor : transform;
        Gizmos.color = Color.cyan;

        int previewCount = handCards.Count > 0 ? handCards.Count : 5;
        float centerIndex = (previewCount - 1) / 2.0f;

        Vector3 prevPos = Vector3.zero;
        for (int i = 0; i < previewCount; i++)
        {
            float u = i - centerIndex;
            float angle = u * cardSpacingAngle;
            float rad = angle * Mathf.Deg2Rad;

            float xPos = circleRadius * Mathf.Sin(rad);
            float zOffset = circleRadius * (1f - Mathf.Cos(rad));
            float zPos = invertZCurve ? -zOffset : zOffset;
            float yPos = i * cardDepthOffset;

            Vector3 localPos = new Vector3(xPos, yPos, zPos);
            Vector3 worldPos = anchor.TransformPoint(localPos);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(worldPos, 0.15f);

            // 카드가 바라보는 상단(머리) 방향 레이 (↖ ↑ ↗ 시각화)
            float yAngle = invertRotation ? angle : -angle;
            Quaternion rot = anchor.rotation * Quaternion.Euler(cardBaseRotation.x, yAngle, cardBaseRotation.z);
            Vector3 cardUpDir = rot * Vector3.forward;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(worldPos, cardUpDir * 0.5f);

            Gizmos.color = Color.cyan;
            if (i > 0)
            {
                Gizmos.DrawLine(prevPos, worldPos);
            }
            prevPos = worldPos;

            // 호버 시 도달할 고정 Z 높이 라인 기즈모 (마젠타색 구체)
            Vector3 hoverLocalPos = new Vector3(xPos, (previewCount * cardDepthOffset) + hoverElevationY, hoverTargetZ);
            Vector3 hoverWorldPos = anchor.TransformPoint(hoverLocalPos);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(hoverWorldPos, 0.08f);
        }
    }

    // ==========================================================
    // 외부 스크립트 호환용 프로퍼티 및 메서드
    // ==========================================================
    /// <summary>
    /// 서버로부터 손패 카드 상태 변경 패킷(UPDATE_HAND_CARDS)을 받았을 때,
    /// 일치하는 카드의 스탯(비용, 공격력, 체력 등)을 실시간으로 갱신하고 연출합니다.
    /// </summary>
    public void UpdateHandCards(List<CardInfo> updatedCards)
    {
        if (updatedCards == null || updatedCards.Count == 0) return;

        List<GameObject> cardsToAnimate = new List<GameObject>();

        foreach (var updatedCardInfo in updatedCards)
        {
            if (updatedCardInfo == null) continue;

            // 1. 현재 손패 리스트(handCards)에서 instanceId가 일치하는 카드를 찾습니다.
            GameObject targetCardObj = handCards.Find(card =>
            {
                if (card == null) return false;
                var display = card.GetComponent<GameCardDisplay>();
                return display != null && display.InstanceId == updatedCardInfo.instanceId;
            });

            if (targetCardObj != null)
            {
                var display = targetCardObj.GetComponent<GameCardDisplay>();
                if (display != null)
                {
                    // 이전 비용이나 스탯을 백업하여 "실제 버프/변화"가 일어났는지 감지
                    int prevCost = display._cardInfo != null ? display._cardInfo.currentCost : (display._cardData != null ? display._cardData.manaCost : 0);
                    int prevAtk = display._cardInfo != null ? display._cardInfo.currentAttack : (display._cardData != null ? display._cardData.attack : 0);
                    int prevHp = display._cardInfo != null ? display._cardInfo.currentHealth : (display._cardData != null ? display._cardData.health : 0);
                    int prevCustomValue = display._cardInfo != null ? display._cardInfo.customValue : 0;
                    int prevDynamicDamage = display._cardInfo != null ? display._cardInfo.dynamicDamage : 0;
                    int prevSpellAmp = display._cardInfo != null ? display._cardInfo.spellAmpBonus : 0;
                    int prevMinionDmg = display._cardInfo != null ? display._cardInfo.minionDamageBonus : 0;
                    int prevBuffAtk = display._cardInfo != null ? display._cardInfo.dynamicBuffAttack : 0;
                    int prevBuffHp = display._cardInfo != null ? display._cardInfo.dynamicBuffHealth : 0;

                    // 2. 최신 스펙 데이터로 UI를 갱신합니다 (스탯 색상 및 본문 텍스트 포함).
                    display.UpdateCardStats(updatedCardInfo);

                    // 3. 스탯이나 효과 수치(누적 피해량 등)에 무언가 이로운 변화가 생겼을 때 연출 리스트에 담기
                    bool isBuffed = (updatedCardInfo.currentCost < prevCost) ||
                                   (updatedCardInfo.currentAttack > prevAtk) ||
                                   (updatedCardInfo.currentHealth > prevHp) ||
                                   (updatedCardInfo.customValue > prevCustomValue) ||
                                   (updatedCardInfo.dynamicDamage > prevDynamicDamage) ||
                                   (updatedCardInfo.spellAmpBonus > prevSpellAmp) ||
                                   (updatedCardInfo.minionDamageBonus > prevMinionDmg) ||
                                   (updatedCardInfo.dynamicBuffAttack > prevBuffAtk) ||
                                   (updatedCardInfo.dynamicBuffHealth > prevBuffHp);

                    if (isBuffed)
                    {
                        cardsToAnimate.Add(targetCardObj);
                    }
                }
            }
        }

        // 4. 버프 대상 카드들에게 튀어오르는(PunchScale) 연출 실행 (손패 접기/펼치기 트윈과 합성)
        foreach (var cardObj in cardsToAnimate)
        {
            if (cardObj == null || cardObj == _currentlyDraggedCard) continue;

            DOVirtual.DelayedCall(0.05f, () =>
            {
                if (cardObj != null && cardObj != _currentlyDraggedCard)
                {
                    PlayCardBuffPunch(cardObj);
                }
            });
        }
    }

    /// <summary>
    /// 로컬 테스트용: 손패 첫 번째 카드의 스탯 및 효과 텍스트 수치를 변경하여 UpdateHandCards 동작을 확인합니다.
    /// </summary>
    private void TestBuffHandCard()
    {
        if (handCards == null || handCards.Count == 0) return;
        for (int i = 0; i < handCards.Count; i++)
        {
            var display = handCards[i]?.GetComponent<GameCardDisplay>();
            if (display != null && display._cardInfo != null)
            {
                int nextCustomValue = display._cardInfo.customValue + 1;
                int nextDynamicDamage = display._cardInfo.dynamicDamage > 0 ? display._cardInfo.dynamicDamage + 1 : nextCustomValue;

                var updated = new CardInfo
                {
                    cardId = display._cardInfo.cardId,
                    instanceId = display._cardInfo.instanceId,
                    currentCost = Mathf.Max(0, display._cardInfo.currentCost - 1),
                    currentAttack = display._cardInfo.currentAttack + 1,
                    currentHealth = display._cardInfo.currentHealth + 1,
                    customValue = nextCustomValue,
                    dynamicDamage = nextDynamicDamage
                };
                Debug.Log($"[TestBuffHandCard] {display.gameObject.name} 카드 버프 테스트: 비용 {display._cardInfo.currentCost}->{updated.currentCost}, 공격력 {display._cardInfo.currentAttack}->{updated.currentAttack}, 누적효과수치 {display._cardInfo.customValue}->{updated.customValue}");
                UpdateHandCards(new List<CardInfo> { updated });
                break;
            }
        }
    }
    public void OnMulliganCardClicked(GameObject cardRoot)
    {
        if (GameMulliganManager.instance != null)
        {
            GameMulliganManager.instance.OnCardClicked(cardRoot);
        }
    }
    public void SetDraggedCard(GameObject card)
    {
        _currentlyDraggedCard = card;
        if (card != null)
        {
            ClearHover();
            if (_cardBuffTweens.TryGetValue(card, out var btw) && btw != null) btw.Kill();
            _cardBuffMultipliers[card] = 1f;
        }
    }
    public void CreatePhantomCard(GameObject originalCard) {}
    public void RemovePhantomCard(GameObject originalCard) {}
}
