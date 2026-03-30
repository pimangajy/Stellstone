using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 플레이어와 적이 사용한 카드를 큐(Queue)에 쌓아 순차적으로 보여주는 매니저입니다.
/// 현재 활성화된 카드와 대기열 사이의 시각적 간격을 유지하며, 등장 시 회전 연출을 포함합니다.
/// </summary>
public class CardActionQueueManager : MonoBehaviour
{
    public static CardActionQueueManager Instance;

    [Header("연출 위치 및 오프셋")]
    [Tooltip("카드가 공개될 중앙 위치입니다.")]
    public Transform centerShowPosition;
    [Tooltip("대기 중인 카드들이 나열될 방향과 간격입니다.")]
    public Vector3 queueOffset = new Vector3(0, 0.4f, 0.2f);

    [Header("크기(Scale) 설정")]
    [Tooltip("중앙에서 활성화되어 강조될 때의 카드 크기 배율입니다.")]
    public float activeScaleMultiplier = 1.4f;
    [Tooltip("대기열에서 줄 서 있을 때의 카드 크기 배율입니다.")]
    public float waitingScaleMultiplier = 0.9f;

    [Header("타이밍 설정")]
    [Tooltip("대기열 진입 및 중앙 이동 시간")]
    public float moveDuration = 0.5f;
    [Tooltip("중앙에서 카드를 보여주는 시간")]
    public float stayDuration = 1.0f;
    [Tooltip("연출 종료 후 사라지는 시간")]
    public float fadeOutDuration = 0.4f;

    private Queue<GameObject> _actionQueue = new Queue<GameObject>();
    private List<GameObject> _visualList = new List<GameObject>();
    private bool _isProcessing = false;

    private Vector3 _baseScale = Vector3.one;
    private bool _hasGrabbedBaseScale = false;

    // 애니메이션 보호를 위한 ID 정의
    private const string ROTATION_TWEEN_ID = "CardRotation";

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
    }

    private void Start()
    {

    }

    /// <summary>
    /// 카드를 사용 큐에 추가하고 연출을 시작합니다.
    /// </summary>
    public void AddToQueue(GameObject card, bool isOpponent)
    {
        if (!_hasGrabbedBaseScale)
        {
            _baseScale = card.transform.localScale;
            _hasGrabbedBaseScale = true;
        }

        _actionQueue.Enqueue(card);
        _visualList.Add(card);

        card.transform.SetParent(null, true);

        // 첫 번째 카드는 0번 위치, 그 뒤는 한 칸 띄우고 2번 위치부터 시작
        int initialOffsetIndex = (_visualList.Count == 1) ? 0 : _visualList.Count;
        Vector3 targetPos = centerShowPosition.position + (queueOffset * initialOffsetIndex);

        float rotateAngle = isOpponent ? 180f : 360f;

        // 이동과 크기 애니메이션은 기존 것을 끄고 새로 시작
        card.transform.DOKill(false);
        card.transform.DOMove(targetPos, moveDuration).SetEase(Ease.OutCubic);
        card.transform.DOScale(_baseScale * waitingScaleMultiplier, moveDuration).SetEase(Ease.OutCubic);

        // [중요] 회전 애니메이션에 ID를 부여하고, 기존 회전이 있다면 그것만 끄고 새로 시작
        // RotateMode.FastBeyond360을 사용하여 360도 회전 시 멈추지 않고 끝까지 돌게 함
        card.transform.DORotate(new Vector3(0, 0, rotateAngle), moveDuration, RotateMode.FastBeyond360)
            .SetId(ROTATION_TWEEN_ID)
            .SetEase(Ease.OutCubic);

        if (!_isProcessing)
        {
            StartCoroutine(ProcessQueueRoutine());
        }
    }

    /// <summary>
    /// 대기열에 남은 카드들의 위치를 갱신합니다.
    /// </summary>
    private void UpdateQueueVisuals()
    {
        for (int i = 0; i < _visualList.Count; i++)
        {
            GameObject card = _visualList[i];
            Vector3 targetPos = centerShowPosition.position + (queueOffset * (i + 2));

            // [중요] 이동과 크기 트윈만 개별적으로 실행하거나, 회전 ID를 제외하고 Kill 해야 함
            // 여기서는 이동과 크기만 덮어쓰도록 처리
            card.transform.DOMove(targetPos, 0.4f).SetEase(Ease.OutQuad);
            card.transform.DOScale(_baseScale * waitingScaleMultiplier, 0.4f).SetEase(Ease.OutQuad);

            // 회전은 AddToQueue에서 시작된 ID("CardRotation")가 끝날 때까지 건드리지 않습니다.
        }
    }       

    public void CardSetUP(GameObject card)
    {

    }

    /// <summary>
    /// 큐를 순회하며 순차적으로 카드 연출을 실행하는 루틴입니다.
    /// </summary>
    private IEnumerator ProcessQueueRoutine()
    {
        _isProcessing = true;

        while (_actionQueue.Count > 0)
        {
            // 1. 큐에서 연출할 카드를 꺼냄
            GameObject currentCard = _actionQueue.Dequeue();

            // 2. 중앙 노출 연출 시작 (이전의 모든 트윈 종료)
            currentCard.transform.DOKill();
            currentCard.transform.DOMove(centerShowPosition.position, moveDuration).SetEase(Ease.OutBack);
            currentCard.transform.DOScale(_baseScale * activeScaleMultiplier, moveDuration).SetEase(Ease.OutBack);

            // 중앙에서는 회전값을 0으로 리셋하여 정면을 보여줌
            currentCard.transform.DORotate(new Vector3(0, 0, 0), moveDuration).SetEase(Ease.OutBack);

            // 3. 중앙에서 머무르며 카드 공개
            yield return new WaitForSeconds(stayDuration);

            // 4. 퇴장 연출 (위로 이동하며 투명해짐)
            Sequence fadeSeq = DOTween.Sequence();
            fadeSeq.Append(currentCard.transform.DOMove(centerShowPosition.position + Vector3.up * 1.5f, fadeOutDuration).SetEase(Ease.InQuad));

            SpriteRenderer sr = currentCard.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
                fadeSeq.Join(sr.DOFade(0, fadeOutDuration));
            else
                fadeSeq.Join(currentCard.transform.DOScale(Vector3.one * 0.01f, fadeOutDuration));

            yield return fadeSeq.WaitForCompletion();

            // 카드 파괴 및 다음 카드 전 대기
            Destroy(currentCard);
            yield return new WaitForSeconds(0.15f);

            _visualList.Remove(currentCard);
            UpdateQueueVisuals();
        }


        _isProcessing = false;

        // 모든 연출이 끝난 후 손패 레이아웃 최종 정리
        if (OpponentHandVisualizer.Instance != null)
        {
            OpponentHandVisualizer.Instance.UpdateHandLayout();
        }
    }
}