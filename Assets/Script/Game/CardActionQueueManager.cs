using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 카드 연출의 진행 상태를 정의합니다.
/// </summary>
public enum ActionState
{
    WaitingForData, // 시각적 오브젝트는 생성되었으나 서버로부터 상세 데이터(CardData)를 기다리는 상태
    Ready           // 오브젝트와 데이터가 모두 매칭되어 연출 실행이 가능한 상태
}

/// <summary>
/// 하나의 카드 액션 연출에 필요한 모든 정보를 담는 컨테이너 클래스입니다.
/// </summary>
public class CardActionRequest
{ 
    public GameObject cardObject;  // 화면에 표시될 카드 게임 오브젝트
    public EntityData entytidata;      // 서버에서 전달받은 카드의 실제 스펙 데이터
    public bool isOpponent;        // 카드를 사용한 주체 (true: 상대방, false: 본인)
    public ActionState state;      // 현재 액션의 준비 상태
}

/// <summary>
/// 서버 메시지(Success, Resolution)의 수신 순서에 상관없이 
/// 카드 사용 연출과 실제 필드 스폰을 동기화하여 실행하는 매니저입니다.
/// </summary>
public class CardActionQueueManager : MonoBehaviour
{
    public static CardActionQueueManager Instance;

    [Header("연출 위치 및 부모 설정")]
    [Tooltip("카드가 공개될 중앙 위치이자 부모가 될 Transform입니다.")]
    public Transform centerShowPosition;

    [Tooltip("대기열에 있는 카드들이 나열될 로컬 간격입니다.")]
    public Vector3 queueOffset = new Vector3(0, 40f, 20f);

    [Header("타이밍 설정")]
    [Tooltip("카드가 이동하는 시간입니다.")]
    public float moveDuration = 0.5f;
    [Tooltip("중앙에서 카드를 보여주며 멈춰있는 시간입니다.")]
    public float stayDuration = 1.0f;

    // 현재 화면에 배치되어 연출을 기다리는 카드 액션 리스트 (순서 보장)
    private List<CardActionRequest> _actionList = new List<CardActionRequest>();

    // [중요] 네트워크 지연으로 인해 CardData(Resolution)가 오브젝트(Success)보다 먼저 도착했을 때 저장하는 버퍼
    private Queue<EntityData> _orphanedDataBuffer = new Queue<EntityData>();

    private bool _isProcessing = false; // 코루틴 중복 실행 방지 플래그
    private Vector3 _baseScale = Vector3.one; // 카드의 기본 크기 저장용
    private bool _hasGrabbedBaseScale = false;

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
    }

    /// <summary>
    /// [1단계: S_PlayCardSuccess 수신 시 호출]
    /// 카드 오브젝트를 먼저 등록하고 대기열로 이동시킵니다.
    /// </summary>
    /// <param name="cardObj">생성된 카드 오브젝트</param>
    /// <param name="isOpponent">상대방 여부</param>
    public void PreparePlay(GameObject cardObj, bool isOpponent)
    {
        // 최초 1회 기본 스케일 값 저장
        if (!_hasGrabbedBaseScale)
        {
            _baseScale = cardObj.transform.localScale;
            _hasGrabbedBaseScale = true;
        }

        // 새로운 액션 요청 생성
        CardActionRequest newRequest = new CardActionRequest
        {
            cardObject = cardObj,
            isOpponent = isOpponent
        };

        // [체크] 만약 데이터(Resolution)가 버퍼에 미리 도착해 있다면 즉시 매칭
        if (_orphanedDataBuffer.Count > 0)
        {
            newRequest.entytidata = _orphanedDataBuffer.Dequeue();
            newRequest.state = ActionState.Ready;
            Debug.Log($"[ActionLog] 데이터가 미리 존재함: {newRequest.entytidata.entityId}와 즉시 매칭되었습니다.");
        }
        else
        {
            // 데이터가 아직 없다면 대기 상태로 설정
            newRequest.entytidata = null;
            newRequest.state = ActionState.WaitingForData;
        }

        // 리스트에 추가하고 부모를 연출용 오브젝트로 변경 (좌표계 동기화)
        _actionList.Add(newRequest);
        cardObj.transform.SetParent(centerShowPosition, false);

        // 대기열 비주얼 갱신 (줄 세우기)
        UpdateQueueVisuals();

        // 연출 프로세스가 작동 중이 아니라면 시작
        if (!_isProcessing) StartCoroutine(ProcessQueueRoutine());
    }

    /// <summary>
    /// [2단계: S_ActionResolution 수신 시 호출]
    /// 서버로부터 받은 카드 데이터를 대기 중인 오브젝트와 매칭합니다.
    /// </summary>
    /// <param name="data">서버에서 보내온 실제 카드 데이터</param>
    public void ResolvePlay(EntityData data)
    {
        // 1. 현재 리스트에서 데이터를 기다리고 있는 가장 앞선 항목을 찾음
        CardActionRequest pending = _actionList.Find(a => a.state == ActionState.WaitingForData);

        if (pending != null)
        {
            // 대기 중인 항목이 있다면 데이터 주입 및 상태 변경
            pending.entytidata = data;
            pending.state = ActionState.Ready;
            Debug.Log($"[ActionLog] {data.entityId} 데이터 매칭 완료. 연출 준비됨.");
        }
        else
        {
            // 2. 만약 매칭할 오브젝트가 아직 없다면(메시지 역전 현상), 버퍼에 임시 보관
            _orphanedDataBuffer.Enqueue(data);
            Debug.Log($"[ActionLog] {data.entityId} 데이터가 먼저 도착함. 버퍼에 보관합니다.");
        }
    }

    /// <summary>
    /// 큐를 순회하며 실제 연출(이동, 소환)을 실행하는 메인 루틴입니다.
    /// </summary>
    private IEnumerator ProcessQueueRoutine()
    {
        _isProcessing = true;

        while (_actionList.Count > 0)
        {
            // 현재 처리할 가장 앞의 액션 참조
            CardActionRequest current = _actionList[0];

            // [핵심] 데이터가 도착할 때까지 이 액션에서 멈춰 대기합니다.
            if (current.state == ActionState.WaitingForData)
            {
                yield return new WaitForSeconds(0.05f);
                continue; // 데이터가 올 때까지 다음 루프로 넘어가지 않음
            }

            // --- 데이터와 오브젝트가 모두 준비된 상태 (Ready) ---
            GameObject currentCard = current.cardObject;
            EntityData currentData = current.entytidata;

            // 1. 중앙 이동 및 확대 연출 (Card Motion)
            currentCard.transform.DOKill();
            currentCard.transform.DOLocalMove(Vector3.zero, moveDuration).SetEase(Ease.OutBack);
            currentCard.transform.DOScale(_baseScale * 1.4f, moveDuration).SetEase(Ease.OutBack);

            // 유저가 카드를 확인할 시간을 줌
            yield return new WaitForSeconds(stayDuration);

            // 2. 실제 필드에 하수인 스폰 (Spawn Logic)
            // 여기서 서버에서 받은 데이터(currentData)와 소유주 정보(isOpponent)를 함께 사용합니다.
            if (GameEntityManager.Instance != null)
            {
                GameEntityManager.Instance.SpawnCard(currentData);
            }

            // 3. 카드 사용 오브젝트 정리
            _actionList.RemoveAt(0); // 처리 완료된 액션 제거
            Destroy(currentCard);    // 연출용 카드 프리팹 파괴

            // 뒤에 기다리던 카드들을 한 칸씩 앞으로 당김
            UpdateQueueVisuals();

            // 액션 사이의 짧은 간격
            yield return new WaitForSeconds(0.2f);
        }

        _isProcessing = false;
    }

    /// <summary>
    /// 대기열 리스트에 있는 모든 카드들의 위치를 로컬 좌표 기준으로 재정렬합니다.
    /// </summary>
    private void UpdateQueueVisuals()
    {
        for (int i = 0; i < _actionList.Count; i++)
        {
            GameObject card = _actionList[i].cardObject;
            if (card == null) continue;

            // 첫 번째 카드(i=0)는 연출을 위해 중앙 자리를 비워두고, i+1부터 대기 위치 지정
            Vector3 targetLocalPos = queueOffset * (i + 1);

            // 부드러운 위치 및 크기 조절
            card.transform.DOLocalMove(targetLocalPos, 0.4f).SetEase(Ease.OutQuad);
            card.transform.DOScale(_baseScale * 0.9f, 0.4f).SetEase(Ease.OutQuad);
        }
    }
}