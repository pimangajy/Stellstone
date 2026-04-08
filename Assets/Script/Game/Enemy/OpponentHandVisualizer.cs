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

        CardActionQueueManager.Instance.AddToQueue(card, true);
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