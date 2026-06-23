using UnityEngine;
using System.Collections.Generic;
using DG.Tweening; // 부드러운 UI 이동 및 애니메이션을 위한 라이브러리
using UnityEngine.UI; // 버튼 등 UI 컴포넌트를 사용하기 위해 필수
using System.Collections;

/// <summary>
/// 2D UI 캔버스 기반: 게임 시작 전, '멀리건(Mulligan)' 단계를 관리합니다.
/// 처음에 뽑힌 카드들 중 마음에 안 드는 카드를 선택하면 덱에 넣고 다른 카드로 교체해주는 시스템입니다.
/// </summary>
public class GameMulliganManager : MonoBehaviour
{
    public static GameMulliganManager instance;

    [Header("연결")]
    [Tooltip("손패 관리를 담당하는 매니저 (카드 선택/취소 시 손패와 연동)")]
    public HandCardControllManager handManager;
    public CardDrawManager cardDrawManager;

    [Tooltip("선택된 카드들이 모여서 보여질 화면 중앙의 UI 빈 객체")]
    public RectTransform centerAnchor;
    [Tooltip("교체할 카드들이 버려질(돌아갈) 덱의 UI 위치")]
    public RectTransform deckTransform;
    [Tooltip("교체를 확정짓는 '확인' 버튼")]
    public Button mulliganCheck;

    [Header("설정")]
    [Tooltip("중앙에 선택된 카드들이 나열될 때의 간격 (UI 픽셀 단위이므로 200~300 등 큰 값 필요)")]
    public float cardSpacing = 250f;
    [Tooltip("카드가 손패 ↔ 중앙으로 이동할 때 걸리는 애니메이션 시간")]
    public float animDuration = 0.3f;
    [Tooltip("멀리건 단계임을 알리는 안내 이미지 (예: '교체할 카드를 선택하세요')")]
    public GameObject mulliganImg;
    [Tooltip("선택되어 중앙으로 온 카드의 크기 배율 (1.0 = 원래 크기 유지)")]
    public float selectedCardScaleMultiplier = 1.0f;

    // --- 내부 변수 ---
    [Tooltip("현재 교체하려고 클릭(선택)한 카드들의 리스트")]
    public List<GameObject> _selectedCards = new List<GameObject>();
    private Dictionary<GameObject, int> _originalIndices = new Dictionary<GameObject, int>();

    private void Awake()
    {
        // 싱글톤 패턴 (어디서든 쉽게 접근 가능하도록)
        if (instance != null && instance != this) Destroy(this.gameObject);
        else instance = this;

        // 확인 버튼에 클릭 이벤트(ConfirmMulligan 함수)를 연결합니다.
        if (mulliganCheck != null) mulliganCheck.onClick.AddListener(ConfirmMulligan);
    }

    private void StartMulligan()
    {
        mulliganImg.SetActive(true);
    }

    /// <summary>
    /// 카드를 클릭했을 때 실행되는 함수입니다. (GameInputManager나 손패 매니저에서 호출됨)
    /// </summary>
    public void OnCardClicked(GameObject card)
    {
        // 이미 중앙에 올라가 있는 카드라면 -> 선택 취소 (다시 손패로)
        if (_selectedCards.Contains(card))
        {
            DeselectCard(card);
        }
        // 손패에 있는 카드라면 -> 교체할 카드로 선택 (중앙으로)
        else
        {
            SelectCard(card);
        }
    }

    // ==========================================================
    // 카드 선택 / 취소 로직
    // ==========================================================
    private void SelectCard(GameObject card)
    {
        // 선택 리스트에 추가
        _selectedCards.Add(card);

        // [중요] 부모를 손패에서 화면 중앙(centerAnchor)으로 변경합니다.
        // 이때 매개변수 true를 주어 화면상의 현재 위치(시각적 좌표)를 유지하게 만들어 순간이동을 방지합니다.
        card.transform.SetParent(centerAnchor, true);

        // 중앙 카드들과 손패 카드들을 각각 예쁘게 재정렬합니다.
        UpdateCenterLayout();
        handManager.AlignHand();
    }

    private void DeselectCard(GameObject card)
    {
        // 선택 리스트에서 제거
        _selectedCards.Remove(card);

        // 부모를 다시 손패 앵커(handAnchor)로 돌려놓습니다. (마찬가지로 true로 순간이동 방지)
        card.transform.SetParent(handManager.handAnchor, true);

        // 중앙 카드들과 손패 카드들을 각각 예쁘게 재정렬합니다.
        UpdateCenterLayout();
        handManager.AlignHand();
    }

    // ==========================================================
    // 중앙 선택 영역 2D UI 정렬 로직
    // ==========================================================
    private void UpdateCenterLayout()
    {
        int count = _selectedCards.Count;
        if (count == 0) return;

        // 선택된 카드 개수에 따라 전체 너비를 구하고, 시작 지점(startX)을 계산해 카드를 가운데 정렬합니다.
        float totalWidth = (count - 1) * cardSpacing;
        float startX = -totalWidth / 2.0f;

        // 기준 스케일값에 배율을 곱해 목표 크기를 계산합니다.
        Vector3 baseScale = (handManager != null) ? handManager.OriginalCardScale : Vector3.one;
        Vector3 targetScale = baseScale * selectedCardScaleMultiplier;

        for (int i = 0; i < count; i++)
        {
            GameObject card = _selectedCards[i];
            RectTransform cardRect = card.GetComponent<RectTransform>();

            // X축 위치만 나란히 띄우고(startX + i * 간격), Y축은 0으로 중앙에 맞춥니다.
            Vector2 targetPos = new Vector2(startX + (i * cardSpacing), 0);

            // 중앙에 뜬 카드가 다른 손패 카드에 가리지 않도록 렌더링 순서를 맨 앞으로 당깁니다.
            cardRect.SetAsLastSibling();

            cardRect.DOKill();
            // 부드러운 이동(DOAnchorPos), 회전(기울어진 카드를 똑바로 폄), 크기 조절 애니메이션 실행
            cardRect.DOAnchorPos(targetPos, animDuration).SetEase(Ease.OutQuad);
            cardRect.DOLocalRotateQuaternion(Quaternion.identity, animDuration).SetEase(Ease.OutQuad);
            cardRect.DOScale(targetScale, animDuration).SetEase(Ease.OutQuad);
        }
    }

    // ==========================================================
    // 멀리건 확정 (덱으로 카드 돌려보내기)
    // ==========================================================
    public void ConfirmMulligan()
    {
        if (deckTransform == null) return;

        HandCardControllManager.instance.isMulligan = true;

        // 서버에 '이 카드들을 교체해주세요'라고 알리기 위해 ID를 모아둘 리스트입니다.
        List<string> idsToSend = new List<string>();

        foreach (GameObject cardObj in _selectedCards)
        {
            var cardScript = cardObj.GetComponent<GameCardDisplay>();
            if (cardScript != null) idsToSend.Add(cardScript.InstanceId);
        }

        // 여러 카드의 애니메이션을 하나로 묶어서 관리하는 DOTween Sequence를 생성합니다.
        Sequence returnSequence = DOTween.Sequence();

        // 원본 리스트를 복사해두고, 선택 리스트는 비웁니다.
        List<GameObject> cardsToReturn = new List<GameObject>(_selectedCards);
        _selectedCards.Clear();

        // 각 카드를 덱으로 날려보내는 연출을 생성합니다.
        for (int i = 0; i < cardsToReturn.Count; i++)
        {
            GameObject card = cardsToReturn[i];
            RectTransform cardRect = card.GetComponent<RectTransform>();

            // 카드가 동시에 날아가지 않고 약간의 시차(0.1초 간격)를 두고 차례대로 날아가도록 시작 시간을 설정합니다.
            float startTime = i * 0.1f;
            float flightDuration = 0.5f;

            // 실제 손패 관리 데이터에서도 이 카드를 완전히 삭제합니다.
            handManager.RemoveCardFromHandListOnly(card);

            // [애니메이션 1] 덱의 화면 위치(position)를 향해 곡선 형태(InCubic)로 가속하며 날아갑니다.
            returnSequence.Insert(startTime, cardRect.DOMove(deckTransform.position, flightDuration).SetEase(Ease.InCubic));

            // [애니메이션 2] 날아가면서 Y축을 180도 회전시켜 덱에 꽂히는 느낌(뒷면 보이기)을 줍니다.
            returnSequence.Insert(startTime, cardRect.DORotateQuaternion(deckTransform.rotation * Quaternion.Euler(0, 180f, 0), flightDuration));

            // [애니메이션 3] 덱 안으로 빨려 들어가는 것처럼 크기를 0으로 줄입니다.
            returnSequence.Insert(startTime, cardRect.DOScale(Vector3.zero, flightDuration));

            // 카드가 덱에 완전히 도착할 시간이 되면 메모리에서 카드를 파괴(삭제)합니다.
            returnSequence.InsertCallback(startTime + flightDuration, () => { Destroy(card); });
        }

        // 모든 카드가 덱으로 들어가는 애니메이션이 완전히 끝났을 때 서버에 메시지를 보냅니다.
        returnSequence.OnComplete(() =>
        {
            Debug.Log($"[Mulligan] 결정 완료. 교체 수: {idsToSend.Count}");

            // 네트워크를 통해 서버로 멀리건 확정 메시지를 전송합니다.
            var decision = new C_MulliganDecision
            {
                action = GameActionType.MULLIGAN_DECISION,
                cardInstanceIdsToReplace = idsToSend
            };

            if (GameClient.Instance != null) GameClient.Instance.SendMessageAsync(decision);

            // "교체할 카드를 선택하세요" 등의 안내 UI를 화면에서 숨깁니다.
            if (mulliganImg != null) mulliganImg.SetActive(false);
        });
    }
}


/*

using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 게임 시작 전, '멀리건(Mulligan)' 단계를 관리합니다.
/// 마음에 안 드는 카드를 선택하면 교체해주는 시스템입니다.
/// </summary>
public class GameMulliganManager : MonoBehaviour
{
    public static GameMulliganManager instance;

    [Header("연결")]
    public HandInteractionManager handManager; // 손패 관리자
    public CardDrawManager cardDrawManager;    // 드로우 관리자
    public Transform centerAnchor;             // 선택된 카드가 모일 중앙 위치
    public Transform deckTransform;            // 카드가 돌아갈 덱 위치
    public Button mulliganCheck;               // '확인(교체)' 버튼

    [Header("설정")]
    public float cardSpacing = 2.5f;           // 중앙 정렬 간격
    public float animDuration = 0.3f;          // 이동 애니메이션 시간
    public GameObject mulliganImg;             // 멀리건 안내 이미지
    public float selectedCardScaleMultiplier = 1.0f; // 선택된 카드 크기

    // 현재 교체하려고 선택한 카드 목록
    public List<GameObject> _selectedCards = new List<GameObject>();
    // 카드의 원래 인덱스를 저장할 사전 추가
    private Dictionary<GameObject, int> _originalIndices = new Dictionary<GameObject, int>();

    private void Awake()
    {
        if (instance != null && instance != this) Destroy(this.gameObject);
        else instance = this;

        mulliganCheck.onClick.AddListener(ConfirmMulligan); // 버튼 클릭 시 함수 연결
    }

    /// <summary>
    /// 카드를 클릭했을 때 (HandInteractionManager가 호출해줌)
    /// </summary>
    public void OnCardClicked(GameObject card)
    {
        if (_selectedCards.Contains(card))
        {
            // 이미 선택된 카드면 -> 선택 취소 (다시 손패로)
            DeselectCard(card);
        }
        else
        {
            // 손패에 있던 카드면 -> 선택 (중앙으로)
            SelectCard(card);
        }
    }

    // 카드 선택 (손패 -> 중앙)
    private void SelectCard(GameObject card)
    {
        // 리스트에서 제거하지 않습니다!
        _selectedCards.Add(card);

        card.transform.SetParent(centerAnchor);

        UpdateCenterLayout();
        // 리스트는 그대로이므로 AlignHand()를 호출해도 빈자리가 생기지 않도록 처리가 필요합니다.
        handManager.AlignHand();
    }

    // 카드 선택 취소 (중앙 -> 손패)
    private void DeselectCard(GameObject card)
    {
        _selectedCards.Remove(card);

        // 다시 손패 앵커로 부모 설정
        card.transform.SetParent(handManager.handAnchor);

        UpdateCenterLayout();
        handManager.AlignHand(); // 원래 위치로 자연스럽게 돌아갑니다.
    }

    // 중앙에 모인 카드들 예쁘게 정렬하기
    private void UpdateCenterLayout()
    {
        int count = _selectedCards.Count;
        if (count == 0) return;

        float totalWidth = (count - 1) * cardSpacing;
        float startX = -totalWidth / 2.0f;

        Vector3 baseScale = (handManager != null) ? handManager.OriginalCardScale : Vector3.one;
        Vector3 targetScale = baseScale * selectedCardScaleMultiplier;

        for (int i = 0; i < count; i++)
        {
            GameObject card = _selectedCards[i];
            Vector3 targetLocalPos = new Vector3(startX + (i * cardSpacing), 0, 0);
            Vector3 targetWorldPos = centerAnchor.TransformPoint(targetLocalPos);

            card.transform.DOMove(targetWorldPos, animDuration).SetEase(Ease.OutQuad);
            card.transform.DORotateQuaternion(centerAnchor.rotation, animDuration).SetEase(Ease.OutQuad);
            card.transform.DOScale(targetScale, animDuration).SetEase(Ease.OutQuad);
        }
    }

    /// <summary>
    /// [확인] 버튼 클릭 시 실행.
    /// 선택된 카드들을 덱으로 보내고, 서버에 교체 요청을 보냅니다.
    /// </summary>
    public void ConfirmMulligan()
    {
        if (deckTransform == null) return;

        List<string> idsToSend = new List<string>(); // 서버에 보낼 ID 목록

        // 선택된 카드들의 ID 추출
        foreach (GameObject cardObj in _selectedCards)
        {
            var cardScript = cardObj.GetComponent<GameCardDisplay>();
            if (cardScript != null) idsToSend.Add(cardScript.InstanceId);
        }

        // 애니메이션: 카드들이 덱으로 날아감
        Sequence returnSequence = DOTween.Sequence();
        List<GameObject> cardsToReturn = new List<GameObject>(_selectedCards);
        _selectedCards.Clear(); // 리스트 비움

        for (int i = 0; i < cardsToReturn.Count; i++)
        {
            GameObject card = cardsToReturn[i];
            float startTime = i * 0.1f;
            float flightDuration = 0.5f;

            // 이제 여기서 실제 손패 리스트에서 제거합니다.
            handManager.RemoveCardFromHandListOnly(card);

            // 덱으로 이동 + 회전
            returnSequence.Insert(startTime, card.transform.DOMove(deckTransform.position, flightDuration).SetEase(Ease.InCubic));
            returnSequence.Insert(startTime, card.transform.DORotateQuaternion(deckTransform.rotation, flightDuration));

            // 도착 후 파괴
            returnSequence.InsertCallback(startTime + flightDuration, () => { Destroy(card); });
        }

        // 애니메이션 끝나면 서버로 전송
        returnSequence.OnComplete(() =>
        {
            Debug.Log($"[Mulligan] 결정 완료. 교체 수: {idsToSend.Count}");

            var decision = new C_MulliganDecision
            {
                action = ActionTypes.MulliganDecision,
                cardInstanceIdsToReplace = idsToSend
            };
            GameClient.Instance.SendMessageAsync(decision);
            mulliganImg.SetActive(false); // UI 끄기
        });

        if (HandInteractionManager.instance != null)
        {
            HandInteractionManager.instance.isMulliganPhase = false; // 멀리건 모드 종료
        }
    }
}

*/