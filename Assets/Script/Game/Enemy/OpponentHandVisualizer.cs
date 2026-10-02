using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 2D UI 기반: 상대방의 손패를 가로 일자(Linear) 형태로 정렬하고, 드로우 및 카드 사용 연출을 관리합니다.
/// 상대방 카드는 내 카드와 달리 뒤집혀 있으며, 카드가 많아질수록 자동으로 간격이 좁아지는 기능이 포함되어 있습니다.
/// </summary>
public class OpponentHandVisualizer : MonoBehaviour
{
    public static OpponentHandVisualizer Instance;

    [Header("프리팹 및 위치 (2D UI)")]
    [Tooltip("상대방의 덱에서 뽑혀나올 '카드 뒷면' 프리팹")]
    public GameObject cardBackPrefab;

    [Tooltip("상대방 손패가 나열될 화면 상단의 2D UI 앵커 (RectTransform)")]
    public RectTransform opponentHandAnchor;

    [Tooltip("카드가 생성되고 되돌아갈 상대방의 덱 2D UI 위치")]
    public RectTransform opponentDeckTransform;

    [Header("손패 레이아웃 설정 (가로 정렬)")]
    [Tooltip("상대 카드가 기본적으로 180도 뒤집혀 보이도록 설정하는 회전값")]
    public Vector3 handRotation = new Vector3(0, 0, 180f);

    [Tooltip("카드 사이의 기본 가로 픽셀 간격 (UI 픽셀 단위이므로 100~150 권장)")]
    public float cardSpacing = 120f;

    [Tooltip("카드가 겹칠 때 약간의 Y축 오프셋을 주어 입체감을 살리는 변수")]
    public float cardDepthOffset = 0f;

    [Tooltip("손패가 정렬될 때 걸리는 애니메이션 시간")]
    public float alignDuration = 0.3f;

    [Tooltip("상대방 손패 카드의 크기 비율")]
    public float cardSize = 0.7f;

    [Tooltip("카드를 한 장 뽑을 때마다 줄어들고, 사용할 때마다 늘어날 간격 변화량")]
    public float cardSpacingSize = 5f;

    [Header("드로우 애니메이션 설정")]
    public float drawMoveDuration = 0.5f;
    public float batchDrawInterval = 0.2f;

    [Header("덱 귀환(Return) 연출 설정")]
    public float returnDuration = 0.5f;
    public Ease returnEase = Ease.InQuad;

    [Header("카드 생성(Generate) 연출 설정")]
    [Tooltip("카드가 화면 중앙에 생성될 3D 월드 위치 (미지정 시 CardDrawManager.showCardTransform 자동 참조)")]
    public Transform generateCenterTransform;
    [Tooltip("화면 중앙에서 커지며 나타나는 시간")]
    public float generateSpawnDuration = 0.35f;
    [Tooltip("화면 중앙에서 카드가 머무르는 시간")]
    public float generateShowDuration = 0.65f;
    [Tooltip("중앙에 나타났을 때의 크기 배율")]
    public float generateScaleMultiplier = 1.2f;

    // --- 내부 변수 ---
    private List<GameObject> opponentCards = new List<GameObject>(); // 상대방 손패 리스트
    private Vector3 _originalCardScale = Vector3.one;
    private bool _isScaleSet = false;

    // 인스펙터 창에서 실시간으로 수치를 조절할 때를 감지하기 위한 백업 변수
    private float _lastSpacing;
    private float _lastDepthOffset;

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;

        if (cardBackPrefab != null && !_isScaleSet)
        {
            _originalCardScale = cardBackPrefab.transform.localScale;
            _isScaleSet = true;
        }
    }

    private void Start()
    {
        // 게임 클라이언트 서버 이벤트 연동: 상대가 카드를 냈다는 신호가 오면 PlayUseCardAnimation 실행
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnOpponentPlayCardEvent += PlayUseCardAnimation;
        }

        _lastSpacing = cardSpacing;
        _lastDepthOffset = cardDepthOffset;
    }

    /// <summary>
    /// 상대방이 카드를 한 장 드로우(덱에서 뽑음)합니다.
    /// </summary>
    public void DrawCard()
    {
        if (cardBackPrefab == null || opponentDeckTransform == null || opponentHandAnchor == null) return;

        // 1. 덱 위치에 카드를 생성합니다. (UI 계층이 꼬이지 않게 부모를 덱의 부모로 확실히 지정)
        GameObject newCard = Instantiate(cardBackPrefab, opponentDeckTransform.position, opponentDeckTransform.rotation, opponentDeckTransform.parent);

        // 2. 기준 스케일 저장
        if (!_isScaleSet)
        {
            _originalCardScale = newCard.transform.localScale;
            _isScaleSet = true;
        }

        // 3. 카드의 소속을 손패 앵커로 옮깁니다. (true를 주어 덱 위치에 그대로 멈춰있는 시각적 효과 유지)
        newCard.transform.SetParent(opponentHandAnchor, true);
        opponentCards.Add(newCard);

        // ==========================================================
        // [중요] 다이나믹 간격 조절 및 버그 방지
        // 카드가 추가되었으므로 전체 카드 간격을 줄여줍니다 (cardSpacingSize 만큼).
        // Update() 함수가 이 변화를 '에디터 조작'으로 착각해 순간이동시키지 못하도록 _lastSpacing을 즉시 동기화합니다.
        // ==========================================================
        cardSpacing -= cardSpacingSize;
        _lastSpacing = cardSpacing;

        // 4. 간격이 수정된 상태에서 부드러운 애니메이션 실행
        UpdateHandLayout(newCard);
    }

    /// <summary>
    /// 상대방 손패의 모든 카드를 2D UI 가로 형태로 재정렬합니다.
    /// </summary>
    public void UpdateHandLayout(GameObject newCard = null, bool instant = false)
    {
        int cardCount = opponentCards.Count;
        if (cardCount == 0) return;

        // 전체 카드가 차지할 가로 길이를 구하고, 중앙 정렬을 위한 시작점(startX)을 계산합니다.
        float totalWidth = (cardCount - 1) * cardSpacing;
        float startX = -totalWidth / 2.0f;

        for (int i = 0; i < cardCount; i++)
        {
            GameObject card = opponentCards[i];
            RectTransform cardRect = card.GetComponent<RectTransform>();

            // 카드의 목표 UI 로컬 좌표 계산
            float targetX = startX + (i * cardSpacing);
            float targetY = i * cardDepthOffset;
            Vector2 targetPos = new Vector2(targetX, targetY);
            Quaternion targetLocalRot = Quaternion.Euler(handRotation);

            // Z-Order(계층 순서): 나중에 들어온(오른쪽) 카드가 화면 맨 앞으로 오도록 겹침 순서를 정돈합니다.
            cardRect.SetSiblingIndex(i);

            // 진행 중이던 기존 이동 애니메이션을 정지시켜 애니메이션 꼬임을 방지합니다.
            cardRect.DOKill();

            if (instant)
            {
                // 즉시 이동 (주로 인스펙터 수치 변경 테스트용)
                cardRect.anchoredPosition = targetPos;
                var lp = cardRect.localPosition;
                cardRect.localPosition = new Vector3(lp.x, lp.y, 0f);
                cardRect.localRotation = targetLocalRot;
                cardRect.localScale = _originalCardScale * cardSize;
            }
            else
            {
                // 새로 뽑힌 카드는 좀 더 천천히 날아오고, 기존에 있던 카드는 빠르게 자리를 비켜줍니다.
                float duration = (card == newCard) ? drawMoveDuration : alignDuration;
                Ease easeType = (card == newCard) ? Ease.OutCubic : Ease.OutQuad;

                // 2D UI 환경에 맞게 DOAnchorPos를 사용하여 부드럽게 목표 픽셀 좌표로 이동시키고,
                // 생성 연출 등으로 남아있을 수 있는 Z(깊이) 오프셋을 0으로 안착시킵니다.
                cardRect.DOAnchorPos(targetPos, duration).SetEase(easeType);
                cardRect.DOLocalMoveZ(0f, duration).SetEase(easeType);
                cardRect.DOLocalRotateQuaternion(targetLocalRot, duration).SetEase(easeType);
                cardRect.DOScale(_originalCardScale * cardSize, duration).SetEase(easeType);
            }
        }
    }

    /// <summary>
    /// 상대가 카드를 사용했을 때 호출됩니다.
    /// 손패에서 카드를 빼고, 연출 매니저(CardActionQueueManager)로 넘겨 중앙 화면에 띄워줍니다.
    /// </summary>
    public void PlayUseCardAnimation(S_OpponentPlayCard cardIndex)
    {
        // 유효성 검사
        if (cardIndex.handNum < 0 || cardIndex.handNum >= opponentCards.Count) return;

        // 1. 손패 리스트에서 카드를 먼저 뺍니다. (나머지 카드가 즉시 빈자리를 메우게 하기 위함)
        GameObject card = opponentCards[cardIndex.handNum];
        opponentCards.RemoveAt(cardIndex.handNum);

        // 2. 서버에서 받은 카드 데이터를 시각적 UI(GameCardDisplay)에 입혀줍니다.
        CardInfo cardInfo = cardIndex.cardPlayed;
        CardData cardData = null;
        if (CardDrawManager.Instance != null)
        {
            cardData = CardDrawManager.Instance.GetCardDataById(cardIndex.cardPlayed.cardId);
        }

        if (cardInfo != null && cardData != null)
        {
            GameCardDisplay display = card.GetComponent<GameCardDisplay>();
            if (display != null) display.Setup(cardData, cardInfo);
        }

        // 3. CardActionQueueManager로 넘겨, 화면 중앙에 크게 띄워주는 연출을 맡깁니다.
        // (연출 후 자동으로 파괴됩니다)
        if (CardActionQueueManager.Instance != null)
        {
            CardActionQueueManager.Instance.PreparePlay(card, true);
        }

        // ==========================================================
        // [중요] 다이나믹 간격 조절 복구
        // 카드를 사용해서 손패가 줄었으므로 전체 간격을 다시 넓혀줍니다.
        // 마찬가지로 Update()의 강제 순간이동을 막기 위해 _lastSpacing을 즉시 동기화합니다.
        // ==========================================================
        cardSpacing += cardSpacingSize;
        _lastSpacing = cardSpacing;

        // 4. 간격이 수정된 상태에서 손패를 재정렬합니다.
        UpdateHandLayout();
    }

    /// <summary>
    /// 카드를 상대방 덱으로 되돌리는 연출입니다.
    /// </summary>
    public void ReturnCardToDeck(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= opponentCards.Count) return;
        StartCoroutine(ReturnToDeckRoutine(opponentCards[cardIndex]));
    }

    private IEnumerator ReturnToDeckRoutine(GameObject card)
    {
        // 1. 손패 리스트에서 지우고 즉시 정렬
        opponentCards.Remove(card);
        UpdateHandLayout();

        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.DOKill();

        Sequence returnSeq = DOTween.Sequence();

        // 2. 덱으로 돌아갈 때는 로컬 좌표가 아닌 절대 화면 위치(DOMove)를 향해 날아갑니다.
        // 살짝 화면 위쪽으로 들렸다가(Vector3.up * 50f) 덱 안으로 빨려 들어가는 궤적을 만듭니다.
        returnSeq.Append(cardRect.DOMove(cardRect.position + Vector3.up * 50f, 0.15f).SetEase(Ease.OutQuad));
        returnSeq.Append(cardRect.DOMove(opponentDeckTransform.position, returnDuration).SetEase(returnEase));
        returnSeq.Join(cardRect.DORotateQuaternion(opponentDeckTransform.rotation, returnDuration).SetEase(returnEase));

        // 덱 안으로 들어갈 때 크기를 0으로 줄여 자연스럽게 사라지는 연출을 더합니다.
        returnSeq.Join(cardRect.DOScale(Vector3.zero, returnDuration).SetEase(Ease.InExpo));

        // 애니메이션이 완전히 끝날 때까지 대기
        yield return returnSeq.WaitForCompletion();

        // 3. 메모리에서 완전 삭제
        Destroy(card);
    }

    /// <summary>
    /// 상대방 손패에서 1장을 버리는(Discard) 연출입니다.
    /// </summary>
    public void DiscardOneCard()
    {
        if (opponentCards.Count == 0) return;
        GameObject card = opponentCards[opponentCards.Count - 1];
        opponentCards.RemoveAt(opponentCards.Count - 1);
        cardSpacing += cardSpacingSize;
        _lastSpacing = cardSpacing;
        UpdateHandLayout();
        if (card != null)
        {
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.DOKill();
            cardRect.DOScale(Vector3.zero, 0.25f).OnComplete(() => Destroy(card));
        }
    }

    private void Update()
    {
        // --- 테스트 입력 ---
        if (HandCardControllManager.instance == null || HandCardControllManager.instance.isTestMode)
        {
            if (Input.GetKeyDown(KeyCode.O)) DrawCard();
            if (Input.GetKeyDown(KeyCode.P)) GenerateCard();
        }
    }

    // ==========================================================
    // 카드 생성(Generate) 전용 애니메이션
    // (화면 중앙 등장(뒷면) -> 잠시 대기 -> 상대 손패로 부드럽게 이동)
    // ==========================================================

    private int _pendingGenerateCount = 0;
    private Coroutine _generateBufferCoroutine = null;

    /// <summary>
    /// 상대방 카드 생성(Generate) 연출을 요청합니다.
    /// 동일 프레임 또는 매우 짧은 시간(0.08초) 내에 여러 장이 요청되면 자동으로 묶어 다중 생성 연출을 실행합니다.
    /// </summary>
    public void GenerateCard()
    {
        _pendingGenerateCount++;

        if (_generateBufferCoroutine == null)
        {
            _generateBufferCoroutine = StartCoroutine(ProcessGenerateBufferRoutine());
        }
    }

    private IEnumerator ProcessGenerateBufferRoutine()
    {
        yield return YieldInstructionCache.WaitForSeconds(0.08f);

        int count = _pendingGenerateCount;
        _pendingGenerateCount = 0;
        _generateBufferCoroutine = null;


        if (count == 1)
        {
            StartCoroutine(SingleGenerateRoutine());
        }
        else if (count > 1)
        {
            StartCoroutine(BatchGenerateRoutine(count));
        }
    }

    /// <summary>
    /// 카드가 중앙에 생성될 월드 기준 위치를 반환합니다.
    /// </summary>
    public Vector3 GetGenerateCenterPosition()
    {
        if (generateCenterTransform != null)
        {
            return generateCenterTransform.position;
        }

        if (CardDrawManager.Instance != null)
        {
            if (CardDrawManager.Instance.generateCardTransform != null)
            {
                return CardDrawManager.Instance.generateCardTransform.position;
            }
            if (CardDrawManager.Instance.showCardTransform != null)
            {
                return CardDrawManager.Instance.showCardTransform.position;
            }
        }

        if (CardActionQueueManager.Instance != null && CardActionQueueManager.Instance.centerShowAnchor != null)
        {
            return CardActionQueueManager.Instance.centerShowAnchor.position;
        }

        if (opponentHandAnchor != null)
        {
            return opponentHandAnchor.position + Vector3.down * 300f;
        }

        return Vector3.zero;
    }

    /// <summary>
    /// [단일 카드 생성] 중앙 위치에 카드가 뒷면으로 나타난 후 손패로 들어가는 연출
    /// </summary>
    private IEnumerator SingleGenerateRoutine()
    {
        if (cardBackPrefab == null || opponentHandAnchor == null)
        {
            Debug.LogWarning("[OpponentHandVisualizer] cardBackPrefab 또는 opponentHandAnchor가 누락되었습니다.");
            yield break;
        }

        Vector3 centerPos = GetGenerateCenterPosition();
        Transform parentTransform = opponentHandAnchor.parent != null ? opponentHandAnchor.parent : opponentHandAnchor;
        // 중앙 생성 시 앞면이 카메라를 정면으로 바라보도록 Canvas 평면 회전 적용
        Quaternion rot = parentTransform.rotation;

        GameObject newCard = Instantiate(cardBackPrefab, centerPos, rot, parentTransform);
        newCard.name = "OpponentCard (Generating)";

        if (!_isScaleSet)
        {
            _originalCardScale = newCard.transform.localScale;
            _isScaleSet = true;
        }

        // 초기 스케일 0
        newCard.transform.localScale = Vector3.zero;

        // 1. 화면 중앙에서 앞면이 카메라를 향한 채로 뿅 나타남 (OutBack)
        Vector3 targetScale = _originalCardScale * generateScaleMultiplier;
        newCard.transform.DOScale(targetScale, generateSpawnDuration).SetEase(Ease.OutBack);
        yield return YieldInstructionCache.WaitForSeconds(generateSpawnDuration);

        // 2. 중앙에서 잠시 확인 대기
        yield return YieldInstructionCache.WaitForSeconds(generateShowDuration);

        // 3. 손패로 부드럽게 흡수 이동
        if (newCard != null)
        {
            newCard.name = "OpponentCard";
            newCard.transform.SetParent(opponentHandAnchor, true);
            opponentCards.Add(newCard);

            cardSpacing -= cardSpacingSize;
            cardSpacing = Mathf.Max(30f, cardSpacing);
            _lastSpacing = cardSpacing;

            UpdateHandLayout(newCard);
        }
    }

    /// <summary>
    /// [다중 카드 생성] 여러 장이 동시에 생성될 때 살짝 펼쳐졌다가 순서대로 손패로 들어가는 연출
    /// </summary>
    private IEnumerator BatchGenerateRoutine(int count)
    {
        if (cardBackPrefab == null || opponentHandAnchor == null)
        {
            Debug.LogWarning("[OpponentHandVisualizer] cardBackPrefab 또는 opponentHandAnchor가 누락되었습니다.");
            yield break;
        }

        Vector3 centerPos = GetGenerateCenterPosition();
        Transform parentTransform = opponentHandAnchor.parent != null ? opponentHandAnchor.parent : opponentHandAnchor;
        // 중앙 생성 시 앞면이 카메라를 정면으로 바라보도록 Canvas 평면 회전 적용
        Quaternion baseRot = parentTransform.rotation;

        List<GameObject> spawnedCards = new List<GameObject>();

        if (!_isScaleSet)
        {
            _originalCardScale = cardBackPrefab.transform.localScale;
            _isScaleSet = true;
        }

        // 1단계: 화면 중앙에 약간의 깊이 오프셋을 두고 겹쳐서(Stacked) 등장
        for (int i = 0; i < count; i++)
        {
            Vector3 stackedPos = centerPos + parentTransform.forward * (i * -0.04f);
            GameObject newCard = Instantiate(cardBackPrefab, stackedPos, baseRot, parentTransform);
            newCard.name = $"OpponentCard (Generating_{i})";
            newCard.transform.localScale = Vector3.zero;

            spawnedCards.Add(newCard);

            Vector3 targetScale = _originalCardScale * generateScaleMultiplier;
            newCard.transform.DOScale(targetScale, generateSpawnDuration).SetEase(Ease.OutBack);
        }

        yield return YieldInstructionCache.WaitForSeconds(generateSpawnDuration + 0.1f);

        // 2단계: 중앙에 겹친 카드들이 살짝 가로로 펼쳐짐 (월드 단위 간격)
        float spacing = Mathf.Min(1.8f, 6.0f / Mathf.Max(1, count - 1));
        for (int i = 0; i < count; i++)
        {
            var cardObj = spawnedCards[i];
            if (cardObj == null) continue;

            float offsetRatio = i - (count - 1) * 0.5f;
            Vector3 targetPos = centerPos + parentTransform.right * (offsetRatio * spacing);

            cardObj.transform.DOMove(targetPos, 0.35f).SetEase(Ease.OutCubic);
        }

        // 대기
        yield return YieldInstructionCache.WaitForSeconds(generateShowDuration);

        // 3단계: 순서대로 상대방 손패로 들어감
        for (int i = 0; i < count; i++)
        {
            var cardObj = spawnedCards[i];
            if (cardObj != null)
            {
                cardObj.name = "OpponentCard";
                cardObj.transform.SetParent(opponentHandAnchor, true);
                opponentCards.Add(cardObj);

                cardSpacing -= cardSpacingSize;
                cardSpacing = Mathf.Max(30f, cardSpacing);
                _lastSpacing = cardSpacing;

                UpdateHandLayout(cardObj);
            }
            yield return YieldInstructionCache.WaitForSeconds(0.15f);
        }
    }

    /// <summary>
    /// 여러 장을 일정한 간격을 두고 차례대로 뽑는 연출입니다.
    /// </summary>
    public void PerformBatchDraw(int count)
    {
        StartCoroutine(BatchDrawRoutine(count));
    }

    private IEnumerator BatchDrawRoutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            DrawCard();
            yield return YieldInstructionCache.WaitForSeconds(batchDrawInterval);
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && opponentCards.Count > 0)
        {
            UpdateHandLayout(null, true);
        }
    }
}