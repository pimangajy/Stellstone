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
    [Tooltip("카드 사이의 가로 간격입니다.")]
    public float cardSpacing = 1.2f;
    [Tooltip("카드 간의 겹침 순서를 위한 Y축 오프셋입니다.")]
    public float cardDepthOffset = 0.02f;
    [Tooltip("일반 정렬 애니메이션 시간입니다.")]
    public float alignDuration = 0.3f;

    [Header("드로우 애니메이션 설정")]
    public float drawMoveDuration = 0.5f;
    public float batchDrawInterval = 0.2f;

    [Header("카드 사용(Use) 연출 설정")]
    [Tooltip("카드를 낼 때 앞으로 이동하는 방향과 거리입니다.")]
    public Vector3 useMoveOffset = new Vector3(0, -1.5f, 0);
    public float useDuration = 0.6f;
    public float fadeOutDelay = 0.2f;

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
        _lastSpacing = cardSpacing;
        _lastDepthOffset = cardDepthOffset;
    }

    /// <summary>
    /// 카드를 드로우합니다.
    /// </summary>
    public void DrawCard()
    {
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
            Quaternion targetLocalRot = Quaternion.identity;

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
    public void PlayUseCardAnimation(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= opponentCards.Count) return;

        GameObject card = opponentCards[cardIndex];
        opponentCards.RemoveAt(cardIndex); // 리스트에서 먼저 제거하여 다른 카드들이 즉시 정렬되게 함

        // 2. 사용되는 카드 연출
        Sequence useSeq = DOTween.Sequence();

        // 앞으로 슥 움직임
        useSeq.Append(card.transform.DOLocalMove(card.transform.localPosition + useMoveOffset, useDuration).SetEase(Ease.OutBack));

        // 약간 커지면서 강조 효과 (선택 사항)
        useSeq.Join(card.transform.DOScale(_originalCardScale * 0.8f, useDuration * 0.5f));

        // 서서히 투명해지며 사라짐 (SpriteRenderer 또는 CanvasGroup 대응)
        // 카드 뒷면에 SpriteRenderer가 있다고 가정하거나, 3D Mesh라면 Material을 조정해야 합니다.
        SpriteRenderer sr = card.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            useSeq.Insert(fadeOutDelay, sr.DOFade(0, useDuration - fadeOutDelay));
        }
        else
        {
            // Sprite가 없을 경우 단순 크기 축소로 대체 혹은 예외 처리
            useSeq.Insert(fadeOutDelay, card.transform.DOScale(Vector3.zero, useDuration - fadeOutDelay));
        }

        // 연출 종료 후 삭제
        useSeq.OnComplete(() => {
            Destroy(card);
            UpdateHandLayout();
        });
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
        if (Input.GetKeyDown(KeyCode.O)) DrawCard();

        // K키를 누르면 맨 앞의 카드(0번)를 사용하는 연출 실행
        if (Input.GetKeyDown(KeyCode.K) && opponentCards.Count > 0)
        {
            PlayUseCardAnimation(Random.Range(0, opponentCards.Count));
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