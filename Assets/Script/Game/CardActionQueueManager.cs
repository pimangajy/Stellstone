using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 카드 연출의 진행 상태를 정의합니다.
/// </summary>
public enum ActionState
{
    WaitingForData,
    Ready
}

public class CardActionRequest
{
    public GameObject cardObject;
    public EntityData entityData;
    public bool isOpponent;
    public ActionState state;
}

/// <summary>
/// 2D UI 기반: 카드 사용 연출 매니저
/// - 내 카드: 중앙 연출 생략하고 즉시 필드 스폰
/// - 상대 카드: 대기열 없이 중앙에 하나씩만 보여줌 (새 카드 사용 시 기존 카드 즉시 덮어쓰기)
/// </summary>
public class CardActionQueueManager : MonoBehaviour
{
    public static CardActionQueueManager Instance;

    [Header("UI 씬 연결")]
    [Tooltip("상대방 카드가 공개될 화면 중앙 위치 (RectTransform)")]
    public RectTransform centerShowAnchor;

    [Header("타이밍 설정")]
    public float moveDuration = 0.4f; // 카드가 중앙으로 오는 시간
    public float stayDuration = 1.0f; // 중앙에서 멈춰서 보여주는 시간

    // 네트워크 동기화를 위한 보이지 않는 내부 데이터 리스트 (시각적 대기열 아님)
    private List<CardActionRequest> _internalList = new List<CardActionRequest>();
    private Queue<EntityData> _orphanedDataBuffer = new Queue<EntityData>();
    private bool _isProcessing = false;

    // 현재 화면 중앙에서 보여주고 있는 상대방 카드
    private GameObject _currentShownCard = null;
    private Coroutine _hideCardCoroutine = null;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
    }

    /// <summary>
    /// 카드 사용 오브젝트가 생성되었을 때 호출 (클라이언트 드래그 또는 서버 메시지)
    /// </summary>
    public void PreparePlay(GameObject cardObj, bool isOpponent)
    {
        CardActionRequest newRequest = new CardActionRequest
        {
            cardObject = cardObj,
            isOpponent = isOpponent
        };

        if (_orphanedDataBuffer.Count > 0)
        {
            newRequest.entityData = _orphanedDataBuffer.Dequeue();
            newRequest.state = ActionState.Ready;
        }
        else
        {
            newRequest.entityData = null;
            newRequest.state = ActionState.WaitingForData;
        }

        _internalList.Add(newRequest);

        if (!_isProcessing) StartCoroutine(ProcessQueueRoutine());
    }

    /// <summary>
    /// 서버로부터 실제 카드 스펙 데이터가 도착했을 때 호출
    /// </summary>
    public void ResolvePlay(EntityData data)
    {
        CardActionRequest pending = _internalList.Find(a => a.state == ActionState.WaitingForData);

        if (pending != null)
        {
            pending.entityData = data;
            pending.state = ActionState.Ready;
        }
        else
        {
            _orphanedDataBuffer.Enqueue(data);
        }
    }

    private IEnumerator ProcessQueueRoutine()
    {
        _isProcessing = true;

        while (_internalList.Count > 0)
        {
            CardActionRequest current = _internalList[0];

            // 데이터가 올 때까지 대기
            if (current.state == ActionState.WaitingForData)
            {
                yield return new WaitForSeconds(0.05f);
                continue;
            }

            GameObject currentCard = current.cardObject;
            EntityData currentData = current.entityData;

            // ==========================================================
            // 1. 내 카드 처리 (연출 생략, 즉시 소환)
            // ==========================================================
            if (!current.isOpponent)
            {
                // UI에서 보여줄 필요 없이 바로 필드 스폰 실행
                if (GameEntityManager.Instance != null)
                {
                    GameEntityManager.Instance.SpawnCard(currentData);
                }

                // 내 카드는 드래그하던 손패 UI 오브젝트이므로 역할이 끝났으니 파괴
                if (currentCard != null) HandCardControllManager.instance.RemoveCardFromHand(currentCard);
            }
            // ==========================================================
            // 2. 상대방 카드 처리 (중앙 단일 슬롯 연출)
            // ==========================================================
            else
            {
                // [핵심] 기존에 화면 중앙에 보여주고 있던 다른 상대방 카드가 있다면 즉시 파괴!
                if (_currentShownCard != null)
                {
                    Destroy(_currentShownCard);
                    if (_hideCardCoroutine != null) StopCoroutine(_hideCardCoroutine);
                }

                _currentShownCard = currentCard;
                RectTransform cardRect = currentCard.GetComponent<RectTransform>();

                // UI 부모 설정 및 렌더링 순서 맨 앞으로
                cardRect.SetParent(centerShowAnchor, false); // 중앙 앵커 기준 0,0,0으로 시작
                cardRect.SetAsLastSibling();

                // DOTween UI 애니메이션 (DOAnchorPos)
                cardRect.DOKill();
                cardRect.DOAnchorPos(Vector2.zero, moveDuration).SetEase(Ease.OutQuad);
                cardRect.DOLocalRotateQuaternion(Quaternion.identity, moveDuration).SetEase(Ease.OutQuad);

                // 상대로부터 날아온 느낌을 주기 위해 살짝 큼직하게 띄움
                cardRect.DOScale(Vector3.one * 1.3f, moveDuration).SetEase(Ease.OutQuad);

                // 유저가 카드를 확인할 시간을 줌
                yield return new WaitForSeconds(stayDuration);

                // 실제 필드에 하수인 스폰
                if (GameEntityManager.Instance != null)
                {
                    GameEntityManager.Instance.SpawnCard(currentData);
                }

                // 일정 시간이 지나면 보여줬던 카드를 자연스럽게 치우기 (도중에 새 카드가 오면 위에서 강제 파괴됨)
                _hideCardCoroutine = StartCoroutine(HideShownCardRoutine(_currentShownCard));
            }

            // 리스트에서 처리 완료된 항목 제거
            _internalList.RemoveAt(0);

            // 대기열 연출이 없으므로 사이 간격을 매우 짧게 줍니다.
            yield return new WaitForSeconds(0.1f);
        }

        _isProcessing = false;
    }

    /// <summary>
    /// 상대방 카드를 보여준 후 자연스럽게 축소하며 파괴하는 코루틴
    /// </summary>
    private IEnumerator HideShownCardRoutine(GameObject targetCard)
    {
        // 소환 완료 후 0.5초 정도 더 보여주다가 사라짐
        yield return new WaitForSeconds(1.0f);

        if (targetCard != null)
        {
            RectTransform rect = targetCard.GetComponent<RectTransform>();
            rect.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).OnComplete(() => {
                if (targetCard != null) Destroy(targetCard);
            });
        }
    }
}