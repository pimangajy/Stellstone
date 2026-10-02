using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 멀리건(Mulligan) 단계 관리 매니저 (3D 월드 좌표계 기반)
/// HandCardControllManager 및 CardDrawManager와 연동하여 동작합니다.
/// 멀리건 신호(서버 또는 M키) 수신 시 5장 드로우 -> 카드 클릭 시 3D 중앙(centerAnchor) 나열 -> 재클릭 시 원래 손패 위치(인덱스)로 정확히 복원 -> 확인 시 덱 귀환 및 교체분 재드로우
/// </summary>
public class GameMulliganManager : MonoBehaviour
{
    public static GameMulliganManager instance;

    [Header("1. 3D 매니저 및 오브젝트 연결 (World Transform)")]
    [Tooltip("손패 관리를 담당하는 매니저")]
    public HandCardControllManager handManager;

    [Tooltip("카드 드로우 매니저")]
    public CardDrawManager cardDrawManager;

    [Tooltip("선택된 카드들이 모여서 보여질 3D 월드 중앙 기준점 (mulliganPos)")]
    public Transform centerAnchor;

    [Tooltip("교체할 카드들이 버려질(돌아갈) 3D 월드 덱 위치 (deckPos)")]
    public Transform deckTransform;

    [Tooltip("교체를 확정짓는 '확인' 버튼")]
    public Button mulliganCheck;

    [Header("2. 3D 멀리건 연출 설정 (미터 단위)")]
    [Tooltip("3D 중앙에 선택된 카드들이 나열될 때의 간격 (미터 단위, 기본: 1.8m)")]
    public float cardSpacing = 1.8f;

    [Tooltip("카드가 손패 ↔ 중앙으로 이동할 때 걸리는 애니메이션 시간")]
    public float animDuration = 0.3f;

    [Tooltip("멀리건 단계임을 알리는 안내 UI 이미지 (예: '교체할 카드를 선택하세요')")]
    public GameObject mulliganImg;

    [Tooltip("선택되어 중앙으로 온 카드의 크기 배율 (1.0 = 원래 크기 유지)")]
    public float selectedCardScaleMultiplier = 1.0f;

    [Header("3. 로컬/에디터 테스트 단축키")]
    [Tooltip("멀리건 5장 드로우 테스트 단축키 (기본: M)")]
    public KeyCode testMulliganKey = KeyCode.M;

    [Tooltip("멀리건 시작 시 드로우할 카드 수 (기본: 5장)")]
    public int testDrawCount = 5;

    // --- 내부 변수 ---
    [Tooltip("현재 교체하려고 선택한 카드들의 리스트")]
    public List<GameObject> _selectedCards = new List<GameObject>();

    // 최초 5장 드로우 시의 고유 손패 순서 기억 (선택 취소 시 원래 슬롯 위치로 정확히 복원하기 위함)
    private readonly Dictionary<GameObject, int> _originalCardOrder = new Dictionary<GameObject, int>();
    private bool _isConfirming = false;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (mulliganCheck != null)
        {
            mulliganCheck.onClick.RemoveAllListeners();
            mulliganCheck.onClick.AddListener(ConfirmMulligan);
        }
    }

    private void Start()
    {
        if (handManager == null) handManager = HandCardControllManager.instance;
        if (cardDrawManager == null) cardDrawManager = CardDrawManager.Instance;
    }

    private bool IsTestModeEnabled()
    {
        if (handManager != null) return handManager.isTestMode;
        if (HandCardControllManager.instance != null) return HandCardControllManager.instance.isTestMode;
        return true;
    }

    private void Update()
    {
        if (!IsTestModeEnabled()) return;

        // M키 입력: 5장 드로우 및 멀리건 테스트 시작
        if (Input.GetKeyDown(testMulliganKey))
        {
            StartTestMulligan();
        }
    }

    // ==========================================================
    // 멀리건 시작 (5장 드로우 & 상태 진입)
    // ==========================================================

    /// <summary>
    /// 로컬 / 에디터 테스트용: 기존 손패 정리 후 5장 드로우와 함께 멀리건 단계를 시작합니다.
    /// </summary>
    public void StartTestMulligan()
    {
        if (handManager == null) handManager = HandCardControllManager.instance;
        if (cardDrawManager == null) cardDrawManager = CardDrawManager.Instance;

        Debug.Log("[GameMulliganManager] M키 입력: 5장 드로우 멀리건 테스트 시작");

        // 1. 기존 선택 상태 및 손패 초기화
        ClearMulliganState();
        if (handManager != null)
        {
            for (int i = handManager.handCards.Count - 1; i >= 0; i--)
            {
                if (handManager.handCards[i] != null)
                {
                    handManager.handCards[i].transform.DOKill();
                    Destroy(handManager.handCards[i]);
                }
            }
            handManager.handCards.Clear();
        }

        // 2. 멀리건 상태 활성화 (호버 및 드래그 차단)
        if (handManager != null)
        {
            handManager.isMulliganPhase = true;
            handManager.isMulligan = true;
            handManager.ClearHover();
        }
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.isMulliganPhase = true;
        }
        if (mulliganImg != null)
        {
            mulliganImg.SetActive(true);
        }

        // 3. 테스트용 5장 카드 데이터 생성 후 순차 드로우
        List<CardInfo> testCards = new List<CardInfo>();
        for (int i = 0; i < testDrawCount; i++)
        {
            testCards.Add(new CardInfo
            {
                cardId = "cards-gangzi-00" + ((i % 8) + 1),
                instanceId = "mulligan_test_" + Random.Range(10000, 99999)
            });
        }

        if (cardDrawManager != null)
        {
            cardDrawManager.PerformBatchDraw(testCards);
        }
    }

    /// <summary>
    /// 서버로부터 멀리건 카드 정보를 받았을 때 호출되는 진입점
    /// </summary>
    public void StartMulliganPhase(List<CardInfo> cards)
    {
        if (handManager == null) handManager = HandCardControllManager.instance;
        if (cardDrawManager == null) cardDrawManager = CardDrawManager.Instance;

        ClearMulliganState();

        if (handManager != null)
        {
            handManager.isMulliganPhase = true;
            handManager.isMulligan = true;
            handManager.ClearHover();
        }
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.isMulliganPhase = true;
        }
        if (mulliganImg != null)
        {
            mulliganImg.SetActive(true);
        }

        if (cards != null && cards.Count > 0 && cardDrawManager != null)
        {
            cardDrawManager.PerformBatchDraw(cards);
        }
    }

    // ==========================================================
    // 카드 클릭 처리 (선택 <-> 취소 토글)
    // ==========================================================

    /// <summary>
    /// 카드를 클릭했을 때 실행되는 함수 (HandCardControllManager 또는 GameInputManager에서 호출)
    /// </summary>
    public void OnCardClicked(GameObject card)
    {
        if (_isConfirming || card == null) return;

        // 이미 중앙에 선택되어 올라가 있는 카드라면 -> 선택 취소 (다시 손패 원래 위치로 복원)
        if (_selectedCards.Contains(card))
        {
            DeselectCard(card);
        }
        // 손패에 있는 카드라면 -> 교체할 카드로 선택 (3D 중앙 centerAnchor로 이동)
        else
        {
            SelectCard(card);
        }
    }

    /// <summary>
    /// 손패에 있는 모든 카드의 최초 고유 순서(0, 1, 2, ...)를 기록합니다.
    /// </summary>
    private void EnsureOriginalOrderRecorded()
    {
        if (handManager == null) handManager = HandCardControllManager.instance;
        if (handManager == null || handManager.handCards == null) return;

        for (int i = 0; i < handManager.handCards.Count; i++)
        {
            GameObject c = handManager.handCards[i];
            if (c != null && !_originalCardOrder.ContainsKey(c))
            {
                _originalCardOrder[c] = i;
            }
        }
    }

    /// <summary>
    /// 카드 선택: 손패 -> 3D 화면 중앙(centerAnchor)으로 이동 및 나열
    /// </summary>
    private void SelectCard(GameObject card)
    {
        if (handManager == null) handManager = HandCardControllManager.instance;

        // 1. 현재 손패에 있는 카드들의 원래 순서 기록
        EnsureOriginalOrderRecorded();

        // 2. 손패 리스트에서 제외 (리스트만 제외하여 남아있는 손패들이 재정렬되도록 함)
        if (handManager != null)
        {
            handManager.RemoveCardFromHandListOnly(card);
        }

        // 3. 선택 리스트에 추가
        _selectedCards.Add(card);

        // 4. 부모를 3D 중앙 앵커(centerAnchor)로 변경
        Transform anchor = centerAnchor != null ? centerAnchor : transform;
        card.transform.SetParent(anchor, true);

        // 5. 3D 중앙 선택 영역 및 손패 재정렬
        UpdateCenterLayout();
        if (handManager != null)
        {
            handManager.AlignHand();
        }
    }

    /// <summary>
    /// 카드 선택 취소: 3D 화면 중앙 -> 손패의 원래 위치(인덱스)로 정확히 복원
    /// </summary>
    private void DeselectCard(GameObject card)
    {
        if (handManager == null) handManager = HandCardControllManager.instance;

        // 1. 선택 리스트에서 제거
        _selectedCards.Remove(card);

        // 2. 부모를 다시 손패 앵커로 변경
        Transform handAnchor = (handManager != null && handManager.handAnchor != null) ? handManager.handAnchor : transform;
        card.transform.SetParent(handAnchor, true);

        // 3. [핵심] 원래 손패 순서(_originalCardOrder)를 기반으로 정확한 상대 인덱스에 재삽입
        if (handManager != null && handManager.handCards != null)
        {
            int myOrder = _originalCardOrder.TryGetValue(card, out int order) ? order : 0;
            int targetIndex = handManager.handCards.Count; // 기본값: 맨 끝

            for (int i = 0; i < handManager.handCards.Count; i++)
            {
                GameObject otherCard = handManager.handCards[i];
                if (otherCard != null && _originalCardOrder.TryGetValue(otherCard, out int otherOrder))
                {
                    if (myOrder < otherOrder)
                    {
                        targetIndex = i;
                        break;
                    }
                }
            }

            handManager.handCards.Insert(targetIndex, card);
            handManager.AlignHand(); // 원래 위치로 자연스럽게 복귀
        }

        // 4. 중앙에 남아있는 카드들 재정렬
        UpdateCenterLayout();
    }

    // ==========================================================
    // 3D 중앙 선택 영역 가로 정렬 로직 (World Space)
    // ==========================================================
    private void UpdateCenterLayout()
    {
        int count = _selectedCards.Count;
        if (count == 0) return;

        float totalWidth = (count - 1) * cardSpacing;
        float startX = -totalWidth / 2.0f;

        Vector3 baseScale = (handManager != null) ? handManager.OriginalCardScale : Vector3.one;
        Vector3 targetScale = baseScale * selectedCardScaleMultiplier;
        Transform anchor = centerAnchor != null ? centerAnchor : transform;
        Quaternion targetRotation = (handManager != null) ? (anchor.rotation * Quaternion.Euler(handManager.cardBaseRotation)) : anchor.rotation;

        for (int i = 0; i < count; i++)
        {
            GameObject card = _selectedCards[i];
            if (card == null) continue;

            Vector3 targetLocalPos = new Vector3(startX + (i * cardSpacing), 0f, 0f);
            Vector3 targetWorldPos = anchor.TransformPoint(targetLocalPos);

            card.transform.SetAsLastSibling();
            card.transform.DOKill();
            card.transform.DOMove(targetWorldPos, animDuration).SetEase(Ease.OutQuad);
            card.transform.DORotateQuaternion(targetRotation, animDuration).SetEase(Ease.OutQuad);
            card.transform.DOScale(targetScale, animDuration).SetEase(Ease.OutQuad);
        }
    }

    // ==========================================================
    // 멀리건 확정 (3D 덱으로 귀환 & 교체분 재드로우)
    // ==========================================================
    public void ConfirmMulligan()
    {
        if (_isConfirming) return;
        if (handManager == null) handManager = HandCardControllManager.instance;
        if (cardDrawManager == null) cardDrawManager = CardDrawManager.Instance;

        _isConfirming = true;
        int replaceCount = _selectedCards.Count;
        Debug.Log($"[GameMulliganManager] 멀리건 확인 버튼 클릭: {replaceCount}장 교체 진행");

        // 1. 서버 전송용 카드 ID 수집
        List<string> idsToSend = new List<string>();
        foreach (GameObject cardObj in _selectedCards)
        {
            if (cardObj != null)
            {
                var cardScript = cardObj.GetComponent<GameCardDisplay>();
                if (cardScript != null && !string.IsNullOrEmpty(cardScript.InstanceId))
                {
                    idsToSend.Add(cardScript.InstanceId);
                }
            }
        }

        // 2. 복사본 생성 후 선택 리스트 비우기
        List<GameObject> cardsToReturn = new List<GameObject>(_selectedCards);
        _selectedCards.Clear();

        // 3. 3D 덱 위치로 날아가며 회전/축소 및 파괴 연출 시퀀스
        Sequence returnSequence = DOTween.Sequence();
        Transform targetDeck = deckTransform != null ? deckTransform : ((cardDrawManager != null) ? cardDrawManager.deckTransform : null);

        for (int i = 0; i < cardsToReturn.Count; i++)
        {
            GameObject card = cardsToReturn[i];
            if (card == null) continue;

            float startTime = i * 0.1f;
            float flightDuration = 0.4f;

            if (targetDeck != null)
            {
                returnSequence.Insert(startTime, card.transform.DOMove(targetDeck.position, flightDuration).SetEase(Ease.InCubic));
                returnSequence.Insert(startTime, card.transform.DORotateQuaternion(targetDeck.rotation * Quaternion.Euler(0f, 180f, 0f), flightDuration));
            }
            returnSequence.Insert(startTime, card.transform.DOScale(Vector3.zero, flightDuration));
            returnSequence.InsertCallback(startTime + flightDuration, () =>
            {
                if (card != null)
                {
                    card.transform.DOKill();
                    Destroy(card);
                }
            });
        }

        // 4. 귀환 연출 완료 후: 서버 전송 및 교체분 재드로우
        returnSequence.OnComplete(() =>
        {
            // 네트워크 서버로 멀리건 결정 전송
            var decision = new C_MulliganDecision
            {
                action = GameActionType.MULLIGAN_DECISION,
                cardInstanceIdsToReplace = idsToSend
            };

            if (GameClient.Instance != null && GameClient.Instance.IsConnected)
            {
                GameClient.Instance.SendMessageAsync(decision);
            }

            // 교체된 카드 수만큼 새 카드 재드로우
            if (replaceCount > 0)
            {
                // 오프라인 / 테스트 모드일 경우 자체 재드로우 코루틴 실행
                if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
                {
                    StartCoroutine(RedrawReplacementCardsRoutine(replaceCount));
                }
                // (온라인 연결 시에는 서버에서 S_GameReady 수신 후 SyncHandWithServer가 실행되어 자동 드로우)
            }
            else
            {
                // 0장 교체인 경우 즉시 멀리건 종료 및 일반 모드 전환
                EndMulliganPhase();
            }
        });
    }

    /// <summary>
    /// 버려진 카드 수만큼 새 카드를 순차 드로우하고 멀리건을 종료합니다.
    /// </summary>
    private IEnumerator RedrawReplacementCardsRoutine(int count)
    {
        yield return YieldInstructionCache.WaitForSeconds(0.2f);

        List<CardInfo> replacementCards = new List<CardInfo>();
        for (int i = 0; i < count; i++)
        {
            replacementCards.Add(new CardInfo
            {
                cardId = "cards-gangzi-00" + Random.Range(1, 9),
                instanceId = "mulligan_replace_" + Random.Range(10000, 99999)
            });
        }

        if (cardDrawManager != null)
        {
            cardDrawManager.PerformBatchDraw(replacementCards);
            float totalDrawTime = (count * cardDrawManager.batchDrawInterval) + cardDrawManager.drawDuration + cardDrawManager.showDuration + 0.6f;
            yield return YieldInstructionCache.WaitForSeconds(totalDrawTime);
        }

        EndMulliganPhase();
    }

    /// <summary>
    /// 멀리건 단계를 종료하고 일반 플레이 모드로 전환합니다 (호버 및 드래그 정상 복구)
    /// </summary>
    public void EndMulliganPhase()
    {
        Debug.Log("[GameMulliganManager] 멀리건 단계 종료 -> 일반 플레이 모드 전환 (호버/드래그 활성화)");

        if (handManager == null) handManager = HandCardControllManager.instance;
        if (handManager != null)
        {
            handManager.isMulliganPhase = false;
            handManager.isMulligan = false;
            handManager.AlignHand();
        }

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.isMulliganPhase = false;
        }

        if (mulliganImg != null)
        {
            mulliganImg.SetActive(false);
        }

        ClearMulliganState();
        _isConfirming = false;
    }

    public void ClearMulliganState()
    {
        _selectedCards.Clear();
        _originalCardOrder.Clear();
        _isConfirming = false;
    }

    // ==========================================================
    // 씬 뷰 3D 기즈모 (중앙 선택 카드 나열 가이드 및 덱 위치 시각화)
    // ==========================================================
    private void OnDrawGizmosSelected()
    {
        Transform anchor = centerAnchor != null ? centerAnchor : transform;
        Gizmos.color = Color.cyan;

        int previewCount = _selectedCards.Count > 0 ? _selectedCards.Count : 3;
        float totalWidth = (previewCount - 1) * cardSpacing;
        float startX = -totalWidth / 2.0f;

        Vector3 prevPos = Vector3.zero;
        for (int i = 0; i < previewCount; i++)
        {
            Vector3 localPos = new Vector3(startX + (i * cardSpacing), 0f, 0f);
            Vector3 worldPos = anchor.TransformPoint(localPos);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(worldPos, 0.2f);

            if (i > 0)
            {
                Gizmos.DrawLine(prevPos, worldPos);
            }
            prevPos = worldPos;
        }

        if (deckTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(deckTransform.position, new Vector3(1f, 0.1f, 1.4f));
        }
    }
}
