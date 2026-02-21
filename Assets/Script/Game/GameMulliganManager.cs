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
                action = "MULLIGAN_DECISION",
                cardInstanceIdsToReplace = idsToSend
            };
            GameClient.Instance.SendMessageAsync(decision);
            mulliganImg.SetActive(false); // UI 끄기
        });

        if (HandInteractionManager.instance != null)
            HandInteractionManager.instance.isMulliganPhase = false; // 멀리건 모드 종료
    }
}