using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// (신규) 멀리건 단계에서 선택된 카드들을 관리하고 화면 중앙에 정렬합니다.
/// </summary>
public class GameMulliganManager : MonoBehaviour
{
    public static GameMulliganManager instance;

    [Header("연결")]
    [Tooltip("손패 매니저")]
    public HandInteractionManager handManager;
    [Tooltip("드로우 매니저")]
    public CardDrawManager cardDrawManager;
    [Tooltip("선택된 카드가 모일 화면 중앙 위치")]
    public Transform centerAnchor;
    [Tooltip("카드가 돌아갈 덱의 위치 (CardDrawManager의 deckTransform과 같은 오브젝트 연결)")]
    public Transform deckTransform;
    [Tooltip("멀리건 확인 버튼")]
    public Button mulliganCheck;

    [Header("레이아웃 설정")]
    [Tooltip("중앙 정렬 시 카드 간격")]
    public float cardSpacing = 2.5f;
    [Tooltip("카드 이동/정렬 시간")]
    public float animDuration = 0.3f;
    [Tooltip("멀리건 이미지")]
    public GameObject mulliganImg;

    [Header("크기 설정")]
    [Tooltip("선택된 카드의 크기 배율 (1.0이면 원본 크기 유지)")]
    public float selectedCardScaleMultiplier = 1.0f;

    // 현재 멀리건을 위해 선택된 카드들의 리스트
    public List<GameObject> _selectedCards = new List<GameObject>();


    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
        }

        mulliganCheck.onClick.AddListener(ConfirmMulligan);
    }

    /// <summary>
    /// (HandInteractionManager에서 호출)
    /// 카드가 클릭되었을 때 처리합니다.
    /// - 손패에 있던 카드라면 -> 선택 목록(중앙)으로 이동
    /// - 이미 선택된 카드라면 -> 다시 손패로 복귀
    /// </summary>
    public void OnCardClicked(GameObject card)
    {
        if (_selectedCards.Contains(card))
        {
            // 1. 이미 선택된 카드 -> 선택 해제 (손패로 돌려보냄)
            DeselectCard(card);
        }
        else
        {
            // 2. 손패에 있던 카드 -> 선택 (중앙으로 가져옴)
            SelectCard(card);
        }
    }

    private void SelectCard(GameObject card)
    {
        // 손패 매니저에게서 카드의 소유권을 가져옵니다 (리스트에서 제거, 파괴 X)
        handManager.RemoveCardFromHandListOnly(card);

        // 내 리스트에 추가
        _selectedCards.Add(card);

        // 부모를 centerAnchor로 변경
        card.transform.SetParent(centerAnchor);

        // 레이아웃 갱신
        UpdateCenterLayout(); // 나는 중앙 정렬
        handManager.AlignHand(); // 손패는 빈자리 메우기 정렬
    }

    private void DeselectCard(GameObject card)
    {
        // 내 리스트에서 제거
        _selectedCards.Remove(card);

        // 손패 매니저에게 돌려줍니다 (리스트 추가 + 손패 정렬 자동 실행)
        handManager.AddCardToHand(card);

        // 내 레이아웃 갱신 (빈자리 메우기)
        UpdateCenterLayout();
    }

    /// <summary>
    /// 선택된 카드들을 centerAnchor 기준으로 왼쪽부터 오른쪽으로 정렬합니다.
    /// </summary>
    private void UpdateCenterLayout()
    {
        int count = _selectedCards.Count;
        if (count == 0) return;

        // 전체 너비 계산
        float totalWidth = (count - 1) * cardSpacing;
        // 시작 X 좌표 (왼쪽 끝)
        float startX = -totalWidth / 2.0f;

        Vector3 baseScale = (handManager != null) ? handManager.OriginalCardScale : Vector3.one;
        Vector3 targetScale = baseScale * selectedCardScaleMultiplier;

        for (int i = 0; i < count; i++)
        {
            GameObject card = _selectedCards[i];

            // 목표 위치 계산 (X축 나열)
            Vector3 targetLocalPos = new Vector3(startX + (i * cardSpacing), 0, 0);

            // 월드 위치로 변환
            Vector3 targetWorldPos = centerAnchor.TransformPoint(targetLocalPos);

            // 애니메이션 (위치 이동, 회전은 정면)
            card.transform.DOMove(targetWorldPos, animDuration).SetEase(Ease.OutQuad);
            card.transform.DORotateQuaternion(centerAnchor.rotation, animDuration).SetEase(Ease.OutQuad);

            // (수정) 강제로 Vector3.one이 아닌, 원본 스케일(에 배율을 곱한 값)로 변경
            card.transform.DOScale(targetScale, animDuration).SetEase(Ease.OutQuad);
        }
    }

    /// <summary>
    /// (신규) 멀리건 확인 버튼을 눌렀을 때 호출하세요.
    /// 선택된 카드들이 덱으로 날아가 사라지는 애니메이션을 재생합니다.
    /// </summary>
    public void ConfirmMulligan()
    {

        if (deckTransform == null)
        {
            Debug.LogError("MulliganManager: Deck Transform이 연결되지 않았습니다!");
            return;
        }

        // (신규) 1. 카드가 파괴되거나 리스트가 비워지기 전에, ID를 먼저 추출합니다.
        List<string> idsToSend = new List<string>();

        // (신규) 교체할 카드가 하나도 없어도 서버에는 "빈 리스트"를 보내야 합니다.
        if (_selectedCards.Count > 0)
        {
            foreach (GameObject cardObj in _selectedCards)
            {
                // (주의) 카드 오브젝트에 정보를 담고 있는 스크립트(예: CardDisplay)가 있어야 합니다.
                var cardScript = cardObj.GetComponent<GameCardDisplay>();

                if (cardScript != null)
                {
                    // (신규) 서버가 알 수 있는 고유 ID (instanceId)를 담습니다.
                    idsToSend.Add(cardScript.InstanceId);
                }
                else
                {
                    Debug.LogError($"[Mulligan] 카드 {cardObj.name}에서 CardDisplay 컴포넌트를 찾을 수 없습니다!");
                }
            }
        }
        else
        {
            Debug.Log("교체할 카드가 없습니다. (빈 리스트 전송 예정)");
        }

        // 애니메이션 시퀀스 생성
        Sequence returnSequence = DOTween.Sequence();

        // 리스트 복사 (반복문 도중 원본 리스트를 건드리지 않기 위해)
        List<GameObject> cardsToReturn = new List<GameObject>(_selectedCards);

        // 원본 리스트는 즉시 비움 (더 이상 선택 상태가 아님)
        _selectedCards.Clear();

        for (int i = 0; i < cardsToReturn.Count; i++)
        {
            GameObject card = cardsToReturn[i];

            // 0.1초 간격으로 순차적으로 출발
            float startTime = i * 0.1f;
            float flightDuration = 0.5f;

            // 1. 덱 위치로 이동 (Insert를 사용해 병렬+시차 실행)
            // Ease.InCubic을 쓰면 처음엔 느리다 점점 빨라져서 '빨려 들어가는' 느낌이 납니다.
            returnSequence.Insert(startTime,
                card.transform.DOMove(deckTransform.position, flightDuration).SetEase(Ease.InCubic));

            // 2. 덱 회전값(보통 엎어놓음)으로 회전
            returnSequence.Insert(startTime,
                card.transform.DORotateQuaternion(deckTransform.rotation, flightDuration));

            // (선택) 덱에 들어갈 때 작아지게 하려면
            // returnSequence.Insert(startTime, card.transform.DOScale(Vector3.zero, flightDuration));

            // 3. 도착 후 파괴 (Callback)
            // 람다 캡처 문제를 피하기 위해 로컬 변수 사용은 위에서 이미 함
            returnSequence.InsertCallback(startTime + flightDuration, () =>
            {
                Destroy(card);
            });
        }

        // 모든 애니메이션 종료 후
        returnSequence.OnComplete(() =>
        {
            Debug.Log($"[Mulligan] 멀리건 카드 반환 완료. 서버로 결정 전송. 교체 수: {idsToSend.Count}");

            // (신규) C_MulliganDecision 패킷 생성 및 전송
            var decision = new C_MulliganDecision
            {
                action = "MULLIGAN_DECISION",
                cardInstanceIdsToReplace = idsToSend
            };

            // (신규) GameClient를 통해 서버로 전송
            GameClient.Instance.SendMessageAsync(decision);
            mulliganImg.SetActive(false);
        });

        // (신규) 멀리건 UI 조작 금지 등을 위해 플래그 해제 (HandInteractionManager에 해당 변수가 있다고 가정)
        if (HandInteractionManager.instance != null)
        {
            HandInteractionManager.instance.isMulliganPhase = false;
        }
    }

    /// <summary>
    /// (신규) 지정된 개수만큼 카드를 시간차를 두고 뽑습니다.
    /// </summary>
    private IEnumerator DrawCardsRoutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // 1. 카드 데이터 생성 (테스트용)
            var testCardData = new CardInfo
            {
                cardId = "TestCard_001",
                instanceId = "instance_" + Random.Range(10000, 99999)
            };

            // 2. 드로우 요청 (CardDrawManager가 Singleton이어야 함)
            if (CardDrawManager.Instance != null)
            {
                CardDrawManager.Instance.PerformDrawAnimation(testCardData);
            }
            else
            {
                Debug.LogError("CardDrawManager Instance가 없습니다!");
            }

            // 3. (핵심) 다음 카드 뽑기 전까지 대기! (0.5초 정도가 적당)
            yield return new WaitForSeconds(0.6f);
        }
    }
}