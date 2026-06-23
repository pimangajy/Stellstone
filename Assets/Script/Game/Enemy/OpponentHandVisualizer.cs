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
                cardRect.localRotation = targetLocalRot;
                cardRect.localScale = _originalCardScale;
            }
            else
            {
                // 새로 뽑힌 카드는 좀 더 천천히 날아오고, 기존에 있던 카드는 빠르게 자리를 비켜줍니다.
                float duration = (card == newCard) ? drawMoveDuration : alignDuration;
                Ease easeType = (card == newCard) ? Ease.OutCubic : Ease.OutQuad;

                // 2D UI 환경에 맞게 DOAnchorPos를 사용하여 부드럽게 목표 픽셀 좌표로 이동시킵니다.
                cardRect.DOAnchorPos(targetPos, duration).SetEase(easeType);
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

    private void Update()
    {
        // 유니티 에디터 인스펙터 창에서 개발자가 수치를 변경하면,
        // 게임을 껐다 켜지 않아도 카드가 즉시 움직이며 실시간으로 확인되도록 하는 편의 기능입니다.
        if (!Mathf.Approximately(_lastSpacing, cardSpacing) ||
            !Mathf.Approximately(_lastDepthOffset, cardDepthOffset))
        {
            UpdateHandLayout(null, true);
            _lastSpacing = cardSpacing;
            _lastDepthOffset = cardDepthOffset;
        }

        // --- 테스트 입력 ---
        if (Input.GetKeyDown(KeyCode.O)) DrawCard();

        if (Input.GetKeyDown(KeyCode.K) && opponentCards.Count > 0)
        {
            var testCardData = new CardInfo { cardId = "cards-gangzi-001", instanceId = "inst_" + Random.Range(1000, 9999) };
            var s_OpponentPlayCard = new S_OpponentPlayCard { cardPlayed = testCardData, handNum = Random.Range(0, opponentCards.Count), targetEntityId = 0 };
            PlayUseCardAnimation(s_OpponentPlayCard);
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
            yield return new WaitForSeconds(batchDrawInterval);
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


/*

using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 상대방의 손패를 가로 일자(Linear) 형태로 정렬하고, 드로우 및 카드 사용 연출을 관리합니다.
/// 인스펙터 수치 변경 시 실시간 반영 및 카드 사용 시의 페이드 아웃 연출이 포함되어 있습니다.
/// </summary>
public class OpponentHandVisualizer : MonoBehaviour
{
    public static OpponentHandVisualizer Instance;

    [Header("프리팹 및 위치")]
    public GameObject cardBackPrefab;      // 상대방 카드 뒷면 프리팹
    public Transform opponentHandAnchor;  // 상대방 손패 기준점 (화면 상단)
    public Transform opponentDeckTransform; // 상대방 덱 위치

    [Header("손패 레이아웃 설정 (가로 정렬)")]
    [Tooltip("손패 정렬 회전각도")]
    public Vector3 handRotation = new Vector3(0,0,0);
    [Tooltip("카드 사이의 가로 간격입니다.")]
    public float cardSpacing = 1.2f;
    [Tooltip("카드 간의 겹침 순서를 위한 Y축 오프셋입니다.")]
    public float cardDepthOffset = 0.02f;
    [Tooltip("일반 정렬 애니메이션 시간입니다.")]
    public float alignDuration = 0.3f;

    [Header("드로우 애니메이션 설정")]
    public float drawMoveDuration = 0.5f;
    [Tooltip("연속으로 뽑을시 딜레이 시간.")]
    public float batchDrawInterval = 0.2f;

    [Header("카드 사용(Use) 연출 설정")]
    [Tooltip("카드를 낼 때 앞으로 이동하는 방향과 거리입니다.")]
    public Vector3 useMoveOffset = new Vector3(0, -1.5f, 0);
    public float useSize = 0.8f;
    public float useDuration = 0.6f;
    public float fadeOutDelay = 0.2f;

    [Header("덱 귀환(Return) 연출 설정")]
    public float returnDuration = 0.5f;
    public Ease returnEase = Ease.InQuad;

    private List<GameObject> opponentCards = new List<GameObject>();
    private Vector3 _originalCardScale = Vector3.one;
    private bool _isScaleSet = false;

    // 실시간 변경 감지용 변수
    private float _lastSpacing;
    private float _lastDepthOffset;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
    }

    private void Start()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnOpponentPlayCardEvent += PlayUseCardAnimation;
        }

        _lastSpacing = cardSpacing;
        _lastDepthOffset = cardDepthOffset;
    }

    /// <summary>
    /// 카드를 드로우합니다.
    /// </summary>
    public void DrawCard()
    {
        if (BattleManager.Instance.isPlayerTurn) return;

        if (cardBackPrefab == null || opponentDeckTransform == null || opponentHandAnchor == null) return;

        GameObject newCard = Instantiate(cardBackPrefab, opponentDeckTransform.position, opponentDeckTransform.rotation);

        if (!_isScaleSet)
        {
            _originalCardScale = newCard.transform.localScale;
            _isScaleSet = true;
        }

        newCard.transform.SetParent(opponentHandAnchor);
        opponentCards.Add(newCard);

        UpdateHandLayout(newCard);
    }

    /// <summary>
    /// 손패의 모든 카드를 가로로 재정렬합니다. (Y축 레이어링 적용)
    /// </summary>
    public void UpdateHandLayout(GameObject newCard = null, bool instant = false)
    {
        int cardCount = opponentCards.Count;
        if (cardCount == 0) return;

        float totalWidth = (cardCount - 1) * cardSpacing;
        float startX = -totalWidth / 2.0f;

        for (int i = 0; i < cardCount; i++)
        {
            GameObject card = opponentCards[i];

            // 위치 계산: X는 간격대로, Y는 겹침 방지를 위해 조정
            float targetX = startX + (i * cardSpacing);
            float targetY = i * cardDepthOffset; // 유저 요청에 따라 Z가 아닌 Y축으로 변경

            Vector3 targetLocalPos = new Vector3(targetX, targetY, 0);
            Quaternion targetLocalRot = Quaternion.Euler(handRotation);

            card.transform.DOKill();

            if (instant)
            {
                card.transform.localPosition = targetLocalPos;
                card.transform.localRotation = targetLocalRot;
                card.transform.localScale = _originalCardScale;
            }
            else
            {
                float duration = (card == newCard) ? drawMoveDuration : alignDuration;
                Ease easeType = (card == newCard) ? Ease.OutCubic : Ease.OutQuad;

                card.transform.DOLocalMove(targetLocalPos, duration).SetEase(easeType);
                card.transform.DOLocalRotateQuaternion(targetLocalRot, duration).SetEase(easeType);
                card.transform.DOScale(_originalCardScale, duration).SetEase(easeType);
            }
        }
    }


    /// <summary>
    /// [핵심] 카드를 필드 쪽으로 내는 연출을 실행하고 파괴합니다.
    /// </summary>
    /// <param name="cardIndex">사용할 카드의 인덱스</param>
    public void PlayUseCardAnimation(S_OpponentPlayCard cardIndex)
    {
        if (cardIndex.handNum < 0 || cardIndex.handNum >= opponentCards.Count) return;

        GameObject card = opponentCards[cardIndex.handNum];
        opponentCards.RemoveAt(cardIndex.handNum); // 리스트에서 먼저 제거하여 다른 카드들이 즉시 정렬되게 함

        CardInfo cardInfo = cardIndex.cardPlayed;
        CardData cardData = CardDrawManager.Instance.GetCardDataById(cardIndex.cardPlayed.cardId);


        if (cardInfo != null && cardData != null)
        {
            card.GetComponent<GameCardDisplay>().Setup(cardData, cardInfo);
        }
        else Debug.Log("상대가 카드를 사용했지만 카드데이터 & 카드인포 없음");

        CardActionQueueManager.Instance.PreparePlay(card, true);
        // CardActionQueueManager.Instance.AddToQueue(card, true);
    }

    /// <summary>
    /// 특정 인덱스의 카드를 덱으로 되돌리는 애니메이션을 실행합니다.
    /// </summary>
    public void ReturnCardToDeck(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= opponentCards.Count) return;
        StartCoroutine(ReturnToDeckRoutine(opponentCards[cardIndex]));
    }
    private IEnumerator ReturnToDeckRoutine(GameObject card)
    {
        // 1. 리스트에서 제거 및 즉시 정렬
        opponentCards.Remove(card);
        UpdateHandLayout();

        // 2. 덱으로 날아가는 연출
        card.transform.DOKill();

        // 월드 좌표 기준으로 덱 위치로 이동해야 하므로 부모 해제 혹은 월드 트윈 사용
        // 여기서는 깔끔하게 월드 좌표 이동을 사용합니다.
        Sequence returnSeq = DOTween.Sequence();

        // 살짝 위로 들렸다가 덱으로 들어가는 느낌
        returnSeq.Append(card.transform.DOMove(card.transform.position + Vector3.up * 0.5f, 0.15f).SetEase(Ease.OutQuad));
        returnSeq.Append(card.transform.DOMove(opponentDeckTransform.position, returnDuration).SetEase(returnEase));
        returnSeq.Join(card.transform.DORotateQuaternion(opponentDeckTransform.rotation, returnDuration).SetEase(returnEase));
        returnSeq.Join(card.transform.DOScale(Vector3.zero, returnDuration).SetEase(Ease.InExpo));

        yield return returnSeq.WaitForCompletion();

        Destroy(card);
    }

    private void Update()
    {
        // 실시간 수치 변경 감지
        if (!Mathf.Approximately(_lastSpacing, cardSpacing) ||
            !Mathf.Approximately(_lastDepthOffset, cardDepthOffset))
        {
            UpdateHandLayout(null, true);
            _lastSpacing = cardSpacing;
            _lastDepthOffset = cardDepthOffset;
        }

        // --- 테스트 입력 ---
        if (Input.GetKeyDown(KeyCode.O))
        {
            DrawCard();
        }

        // K키를 누르면 맨 앞의 카드(0번)를 사용하는 연출 실행
        if (Input.GetKeyDown(KeyCode.K) && opponentCards.Count > 0)
        {
            var testCardData = new CardInfo
            {
                cardId = "cards-gangzi-001",
                instanceId = "instance_" + Random.Range(1000, 9999)
            };
            var s_OpponentPlayCard = new S_OpponentPlayCard
            {
                cardPlayed = testCardData,
                handNum = Random.Range(0, opponentCards.Count),
                targetEntityId = 0
            };
            
            PlayUseCardAnimation(s_OpponentPlayCard);
        }
    }

    // 여러장 뻡는 함수
    public void PerformBatchDraw(int count)
    {
        StartCoroutine(BatchDrawRoutine(count));
    }

    private IEnumerator BatchDrawRoutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            DrawCard();
            yield return new WaitForSeconds(batchDrawInterval);
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

*/