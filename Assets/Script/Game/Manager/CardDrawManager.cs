using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 3D 환경: 덱에서 카드를 뽑는 드로우 애니메이션을 담당합니다.
/// 덱 위치에서 3D 카드가 생성되어 -> 화면 중앙(showCardTransform)에 잠시 보였다가 -> 3D 손패로 들어가는 연출을 합니다.
/// </summary>
public class CardDrawManager : MonoBehaviour
{
    public static CardDrawManager Instance;

    [Header("프리팹 및 씬 연결")]
    public GameObject cardPrefab; // 3D 카드 프리팹 (Card.prefab)
    public Transform deckTransform; // 덱 위치 (카드가 생성될 3D 위치)
    [Tooltip("카드를 드로우(Draw)했을 때 잠시 멈춰 보여줄 3D 위치")]
    public Transform showCardTransform;
    [Tooltip("카드가 생성(Generate - 사이드덱, 토큰, 주문 등)되었을 때 화면에 나타날 3D 위치 (미지정 시 showCardTransform 사용)")]
    public Transform generateCardTransform;

    [Header("핵심 연결")]
    public HandCardControllManager handCardControllManager;

    [Header("드로우 시 카드 외형 조절 (각도 및 크기 배율)")]
    [Tooltip("카드가 덱에서 처음 생성될 때의 회전 각도 오프셋입니다 (탑뷰 기준 (90, 0, 0)은 카드 뒷면)")]
    public Vector3 spawnCardRotation = new Vector3(90f, 0f, 0f);

    [Tooltip("덱에서 처음 스폰될 때의 크기 배율입니다 (1.0 = 프리팹 원래 크기 100%)")]
    public float spawnScaleMultiplier = 1.0f;

    [Tooltip("화면 중앙(showCardTransform)에 노출될 때의 추가 각도 오프셋 (탑뷰 기준 (-90, 0, 0)은 카드 앞면)")]
    public Vector3 showCardRotationOffset = new Vector3(-90f, 0f, 0f);

    [Tooltip("화면 중앙에 보여질 때 카드의 크기 배율입니다 (1.0 = 100%, 1.2 = 120% 확대)")]
    public float showScaleMultiplier = 1.0f;

    [Header("애니메이션 설정")]
    public float drawDuration = 0.4f; // 덱 -> 중앙 이동 시간
    [Tooltip("카드가 중앙에 머무는 시간(초)")]
    public float showDuration = 0.6f; // 중앙에 머무는 시간
    [Tooltip("연속 드로우 시 각 카드 드로우 시작 사이의 간격(초)")]
    public float batchDrawInterval = 0.5f; // 여러 장 뽑을 때 간격

    [Header("카드 생성 전용 설정 (화면 등장 -> 펼침 -> 손패)")]
    [Tooltip("카드 생성 시 추가 각도 오프셋 (탑뷰 기준 (0, 0, 0)은 카드 앞면)")]
    public Vector3 generateCardRotationOffset = Vector3.zero;
    [Tooltip("카드 생성 시 나타나는 크기 배율 (1.3 = 130% 확대)")]
    public float generateScaleMultiplier = 1.3f;
    [Tooltip("카드 생성 시 중앙에서 확대되는 시간")]
    public float generateSpawnDuration = 0.35f;
    [Tooltip("카드 생성 시 중앙에서 펼쳐진 후 대기하는 시간")]
    public float generateShowDuration = 0.65f;
    [Tooltip("여러 장 생성 시 카드 간 가로 펼침 간격")]
    public float generateFanSpacing = 2.4f;
    [Tooltip("여러 장 생성 시 카드 간 회전 부채꼴 각도")]
    public float generateFanAngle = 4.5f;
    [Tooltip("여러 장 생성 후 손패로 차례대로 들어가는 간격")]
    public float generateHandEnterInterval = 0.15f;

    [Header("카드 소각(Burn) 연출 설정")]
    [Tooltip("손패 한도(10장) 초과 등으로 카드가 소각될 때 생성할 불꽃 이펙트 프리팹 (미지정 시 Circular_Flame_Burst 자동 로드)")]
    public GameObject burnVfxPrefab;
    [Tooltip("소각 애니메이션 지속 시간 (흔들림 + 소멸)")]
    public float burnDuration = 0.65f;

    /// <summary>
    /// 카드 생성 시 사용할 3D Transform (generateCardTransform 우선, 미지정 시 showCardTransform 사용)
    /// </summary>
    public Transform ActiveGenerateTransform => generateCardTransform != null ? generateCardTransform : showCardTransform;

    [Header("Test CardData")]
    public CardData testCard;

    private readonly List<CardInfo> _pendingGenerateCards = new List<CardInfo>();
    private Coroutine _generateBufferCoroutine;
    private readonly Queue<CardInfo> _drawQueue = new Queue<CardInfo>();
    private Coroutine _drawQueueCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
    }

    private void OnDestroy()
    {
        if (_drawQueueCoroutine != null)
        {
            StopCoroutine(_drawQueueCoroutine);
            _drawQueueCoroutine = null;
        }
        _drawQueue.Clear();
    }

    void Start()
    {
        if (handCardControllManager == null)
            Debug.LogError("[CardDrawManager] HandCardControllManager 연결 안됨!");
    }

    private bool IsTestModeEnabled()
    {
        if (handCardControllManager != null) return handCardControllManager.isTestMode;
        if (HandCardControllManager.instance != null) return HandCardControllManager.instance.isTestMode;
        return true;
    }

    // --- 테스트 코드 (키보드 D, B, S, G, H) ---
    void Update()
    {
        if (!IsTestModeEnabled()) return;

        if (Input.GetKeyDown(KeyCode.D)) // 단일 드로우 테스트
        {
            var testCardData = new CardInfo
            {
                cardId = "cards-gangzi-008",
                instanceId = "instance_" + Random.Range(1000, 9999)
            };
            PerformDrawAnimation(testCardData);
        }

        if (Input.GetKeyDown(KeyCode.B)) // 3장 드로우 테스트
        {
            List<CardInfo> testBatch = new List<CardInfo>();
            for (int i = 0; i < 3; i++)
            {
                testBatch.Add(new CardInfo { cardId = "cards-gangzi-00" + (i + 1), instanceId = "inst_" + Random.Range(10000, 99999) });
            }
            PerformBatchDraw(testBatch);
        }

        if (Input.GetKeyDown(KeyCode.G)) // 단일 카드 생성 테스트 (화면 중앙 등장 -> 손패 이동)
        {
            var testGenData = new CardInfo
            {
                cardId = "cards-gangzi-004",
                instanceId = "gen_" + Random.Range(1000, 9999),
                origin = CardOrigin.Created
            };
            PerformGenerateAnimation(testGenData);
        }

        if (Input.GetKeyDown(KeyCode.H)) // 여러 장 카드 생성 테스트 (화면 중앙 겹침 등장 -> 살짝 펼침 -> 손패 순차 이동)
        {
            List<CardInfo> testGenBatch = new List<CardInfo>();
            for (int i = 0; i < 3; i++)
            {
                testGenBatch.Add(new CardInfo
                {
                    cardId = "cards-gangzi-00" + (i + 2),
                    instanceId = "gen_batch_" + Random.Range(10000, 99999),
                    origin = CardOrigin.Created
                });
            }
            PerformBatchGenerate(testGenBatch);
        }

        if (Input.GetKeyDown(KeyCode.S)) // 상대방 드로우 테스트
        {
            if (OpponentHandVisualizer.Instance != null)
            {
                OpponentHandVisualizer.Instance.DrawCard();
            }
        }
    }

    /// <summary>
    /// 여러 장을 순서대로 뽑습니다. (멀리건 종료 후 등)
    /// </summary>
    public void PerformBatchDraw(List<CardInfo> cards)
    {
        if (cards == null || cards.Count == 0) return;
        foreach (var cardData in cards)
        {
            _drawQueue.Enqueue(cardData);
            //PerformDrawAnimation(cardData);
        }

        if(_drawQueue.Count > 0 && _drawQueueCoroutine == null)
        {
            _drawQueueCoroutine = StartCoroutine(ProcessDrawQueueRoutine());
        }
    }

    /// <summary>
    /// [핵심] 카드 ID를 이용해 실제 데이터를 찾습니다.
    /// </summary>
    public CardData GetCardDataById(string id)
    {
        if (ResourceManager.Instance == null) return null;
        return ResourceManager.Instance.GetCardData(id);
    }

    /// <summary>
    /// 카드 한 장을 뽑는 3D 애니메이션을 큐에 등록하여 순차 실행합니다.
    /// </summary>
    public void PerformDrawAnimation(CardInfo cardData)
    {
        if (cardData == null || string.IsNullOrEmpty(cardData.cardId))
        {
            Debug.LogWarning("[CardDrawManager] cardData가 null이거나 cardId가 비어있어 드로우를 진행하지 않습니다.");
            return;
        }

        _drawQueue.Enqueue(cardData);

        if (_drawQueueCoroutine == null)
        {
            _drawQueueCoroutine = StartCoroutine(ProcessDrawQueueRoutine());
        }
    }

    private IEnumerator ProcessDrawQueueRoutine()
    {
        while (_drawQueue.Count > 0)
        {
            CardInfo nextCard = _drawQueue.Dequeue();
            ExecuteDrawAnimation(nextCard);

            if (_drawQueue.Count > 0)
            {
                yield return YieldInstructionCache.WaitForSeconds(batchDrawInterval);
            }
        }
        _drawQueueCoroutine = null;
    }

    /// <summary>
    /// 카드 한 장을 실제로 덱에서 화면 중앙을 거쳐 손패로 이동시키는 3D 애니메이션을 실행합니다.
    /// </summary>
    private void ExecuteDrawAnimation(CardInfo cardData)
    {
        if (cardPrefab == null || deckTransform == null || showCardTransform == null || handCardControllManager == null)
        {
            Debug.LogWarning("[CardDrawManager] 필수 참조(cardPrefab, deckTransform, showCardTransform, handCardControllManager) 중 하나가 누락되었습니다.");
            return;
        }

        // 덱에 놓여있을 때의 초기 회전값 계산
        Quaternion initialRotation = deckTransform.rotation * Quaternion.Euler(spawnCardRotation);

        // 1. 덱 위치에 3D 카드 생성
        GameObject newCardObject = Instantiate(cardPrefab, deckTransform.position, initialRotation);
        newCardObject.name = $"Card [{cardData.cardId}] (Drawing)";

        // 3D 프리팹 고유의 원래 스케일 읽기
        Vector3 basePrefabScale = newCardObject.transform.localScale;

        // 프리팹 원래 크기를 기준으로 스폰 배율 적용
        newCardObject.transform.localScale = basePrefabScale * spawnScaleMultiplier;

        // 2. 데이터 주입 (이미지, 텍스트 설정)
        CardData staticData = GetCardDataById(cardData.cardId);
        GameCardDisplay display = newCardObject.GetComponent<GameCardDisplay>();

        if (display != null && staticData != null)
        {
            display.Setup(staticData, cardData);
        }
        else if (display != null && GameEntityManager.Instance != null && GameEntityManager.Instance.test)
        {
            display.Setup(testCard, null);
        }

        // 3. DOTween으로 3D 애니메이션 시퀀스 실행
        Quaternion targetShowRotation = showCardTransform.rotation * Quaternion.Euler(showCardRotationOffset);
        Vector3 targetShowScale = basePrefabScale * showScaleMultiplier;

        Sequence drawSequence = DOTween.Sequence();

        // 1단계: 덱 -> 중앙(showCardTransform) 이동, 회전, 크기 배율 조절
        drawSequence.Append(
            newCardObject.transform.DOMove(showCardTransform.position, drawDuration).SetEase(Ease.OutQuad)
        );
        drawSequence.Join(
            newCardObject.transform.DORotateQuaternion(targetShowRotation, drawDuration).SetEase(Ease.OutQuad)
        );
        drawSequence.Join(
            newCardObject.transform.DOScale(targetShowScale, drawDuration).SetEase(Ease.OutQuad)
        );

        // 2단계: 중앙에서 잠시 대기 (유저가 카드 확인)
        drawSequence.AppendInterval(showDuration);

        // 3단계: 손패 매니저에게 카드 넘기기 (또는 손패 한도 초과 시 소각 연출)
        drawSequence.OnComplete(() =>
        {
            if (newCardObject != null)
            {
                if (cardData != null && cardData.isBurned)
                {
                    Debug.Log($"<color=red>[CardDrawManager]</color> 🔥 드로우 카드 [{cardData.cardId}] 손패 초과(10장)로 소각 연출 실행!");
                    PlayCardBurnAnimation(newCardObject);
                }
                else
                {
                    newCardObject.name = $"Card [{cardData.cardId}]";
                    handCardControllManager.AddCardToHand(newCardObject);
                }
            }
        });
    }

    /// <summary>
    /// 손패가 가득 차 카드가 소각(파괴)될 때 실행되는 연출입니다.
    /// 카드가 흔들리며 불꽃 이펙트가 발생하고, 축소되며 파괴됩니다.
    /// </summary>
    public void PlayCardBurnAnimation(GameObject cardObj)
    {
        if (cardObj == null) return;

        Vector3 burnPos = cardObj.transform.position;

        // 1. 불꽃 VFX 생성
        GameObject vfxToSpawn = burnVfxPrefab;
        if (vfxToSpawn == null)
        {
#if UNITY_EDITOR
            vfxToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Effects/Circular_Flame_Burst.prefab");
#endif
            if (vfxToSpawn == null)
            {
                vfxToSpawn = Resources.Load<GameObject>("Prefab/Effects/Circular_Flame_Burst");
            }
        }

        if (vfxToSpawn != null)
        {
            GameObject vfx = Instantiate(vfxToSpawn, burnPos, Quaternion.identity);
            Destroy(vfx, 2.5f);
        }

        // 2. 카드 흔들림 및 축소 소멸 애니메이션 (DOTween)
        Sequence burnSeq = DOTween.Sequence();
        burnSeq.Append(cardObj.transform.DOShakePosition(burnDuration * 0.65f, strength: 0.15f, vibrato: 20));
        burnSeq.Join(cardObj.transform.DOShakeRotation(burnDuration * 0.65f, strength: 15f, vibrato: 20));
        burnSeq.Append(cardObj.transform.DOScale(Vector3.zero, burnDuration * 0.35f).SetEase(Ease.InBack));
        burnSeq.OnComplete(() =>
        {
            if (cardObj != null)
            {
                Destroy(cardObj);
            }
        });
    }

    // ==========================================================
    // 카드 생성(Generate) 전용 애니메이션
    // (화면 중앙 등장 -> 여러 장 시 살짝 펼침 -> 손패 순차 이동)
    // ==========================================================

    /// <summary>
    /// 카드 생성(사이드덱 획득, 주문/토큰 생성 등) 시 호출되는 애니메이션입니다.
    /// 동일 프레임 또는 매우 짧은 시간(0.08초) 내에 여러 장이 요청되면 자동으로 묶어 화면 중앙 겹침 및 펼침(Batch) 연출을 실행합니다.
    /// </summary>
    public void PerformGenerateAnimation(CardInfo cardData)
    {
        if (cardData == null || string.IsNullOrEmpty(cardData.cardId)) return;

        _pendingGenerateCards.Add(cardData);

        if (_generateBufferCoroutine == null)
        {
            _generateBufferCoroutine = StartCoroutine(ProcessGenerateBufferRoutine());
        }
    }

    private IEnumerator ProcessGenerateBufferRoutine()
    {
        yield return YieldInstructionCache.WaitForSeconds(0.08f);

        List<CardInfo> cardsToProcess = new List<CardInfo>(_pendingGenerateCards);
        _pendingGenerateCards.Clear();
        _generateBufferCoroutine = null;


        if (cardsToProcess.Count == 1)
        {
            StartCoroutine(SingleGenerateRoutine(cardsToProcess[0]));
        }
        else if (cardsToProcess.Count > 1)
        {
            StartCoroutine(BatchGenerateRoutine(cardsToProcess));
        }
    }

    /// <summary>
    /// 여러 장의 생성 카드를 한 번에 연출합니다 (화면 중앙 겹침 등장 -> 살짝 부채꼴 펼침 -> 손패로 순차 이동).
    /// </summary>
    public void PerformBatchGenerate(List<CardInfo> cards)
    {
        if (cards == null || cards.Count == 0) return;
        Debug.Log($"<color=green>[CardDrawManager]</color> ✨ [PerformBatchGenerate] 외부 일괄 호출! 카드 수: {cards.Count}");
        if (cards.Count == 1)
        {
            StartCoroutine(SingleGenerateRoutine(cards[0]));
        }
        else
        {
            StartCoroutine(BatchGenerateRoutine(cards));
        }
    }

    /// <summary>
    /// [단일 카드 생성] 화면 중앙(showCardTransform)에 스케일 0으로 나타나 커진 뒤 손패로 쏙 들어가는 연출
    /// </summary>
    private IEnumerator SingleGenerateRoutine(CardInfo cardData)
    {
        Transform targetTransform = ActiveGenerateTransform;

        if (cardPrefab == null || targetTransform == null || handCardControllManager == null)
        {
            Debug.LogWarning("[CardDrawManager] 필수 참조 중 하나가 누락되어 생성 연출을 건너뜁니다.");
            yield break;
        }

        Vector3 spawnPos = targetTransform.position;
        Quaternion targetRot = targetTransform.rotation * Quaternion.Euler(generateCardRotationOffset);

        GameObject cardObj = Instantiate(cardPrefab, spawnPos, targetRot);
        cardObj.name = $"Card [{cardData.cardId}] (Generating)";

        Vector3 basePrefabScale = cardObj.transform.localScale;
        Vector3 targetScale = basePrefabScale * generateScaleMultiplier;

        // 초기 스케일 0
        cardObj.transform.localScale = Vector3.zero;

        CardData staticData = GetCardDataById(cardData.cardId);
        GameCardDisplay display = cardObj.GetComponent<GameCardDisplay>();
        if (display != null && staticData != null)
        {
            display.Setup(staticData, cardData);
        }
        else if (display != null && GameEntityManager.Instance != null && GameEntityManager.Instance.test)
        {
            display.Setup(testCard, null);
        }

        // 1. 지정된 생성 위치에서 뿅 나타남 (OutBack)
        cardObj.transform.DOScale(targetScale, generateSpawnDuration).SetEase(Ease.OutBack);
        yield return YieldInstructionCache.WaitForSeconds(generateSpawnDuration);

        // 2. 잠시 확인 대기
        yield return YieldInstructionCache.WaitForSeconds(generateShowDuration);

        // 3. 손패로 부드럽게 흡수 이동 또는 소각 처리
        if (cardObj != null)
        {
            if (cardData != null && cardData.isBurned)
            {
                Debug.Log($"<color=red>[CardDrawManager]</color> 🔥 단일 생성 카드 [{cardData.cardId}] 손패 초과(10장)로 소각 연출 실행!");
                PlayCardBurnAnimation(cardObj);
            }
            else
            {
                cardObj.name = $"Card [{cardData.cardId}]";
                handCardControllManager.AddCardToHand(cardObj);
            }
        }
    }

    /// <summary>
    /// [다중 카드 생성] 화면 중앙에 겹쳐서 등장 -> 살짝 부채꼴/가로로 펼쳐짐 -> 손패로 차례차례 들어가는 연출
    /// </summary>
    private IEnumerator BatchGenerateRoutine(List<CardInfo> cards)
    {
        Transform targetTransform = ActiveGenerateTransform;
        Debug.Log($"<color=green>[CardDrawManager]</color> 🎬 [BatchGenerateRoutine] 다중 생성 연출 실행! 카드 수: {cards?.Count}");

        if (cardPrefab == null || targetTransform == null || handCardControllManager == null)
        {
            Debug.LogWarning("[CardDrawManager] 필수 참조 중 하나가 누락되어 일괄 생성 연출을 건너뜁니다.");
            yield break;
        }

        int count = cards.Count;
        List<GameObject> spawnedCards = new List<GameObject>();
        Vector3 centerPos = targetTransform.position;
        Quaternion baseRot = targetTransform.rotation * Quaternion.Euler(generateCardRotationOffset);

        // 1단계: 화면 중앙에 겹쳐서(Stacked) 팝업 등장
        for (int i = 0; i < count; i++)
        {
            var cardData = cards[i];
            // 겹칠 때 약간의 전면 오프셋 (깊이 순서 보장)
            Vector3 stackedPos = centerPos + targetTransform.forward * (i * -0.04f);

            GameObject cardObj = Instantiate(cardPrefab, stackedPos, baseRot);
            cardObj.name = $"Card [{cardData.cardId}] (Generating_{i})";

            Vector3 basePrefabScale = cardObj.transform.localScale;
            cardObj.transform.localScale = Vector3.zero;

            CardData staticData = GetCardDataById(cardData.cardId);
            GameCardDisplay display = cardObj.GetComponent<GameCardDisplay>();
            if (display != null && staticData != null)
            {
                display.Setup(staticData, cardData);
            }
            else if (display != null && GameEntityManager.Instance != null && GameEntityManager.Instance.test)
            {
                display.Setup(testCard, null);
            }

            spawnedCards.Add(cardObj);

            // 중앙에서 겹친 채로 스케일 확대
            Vector3 targetScale = basePrefabScale * generateScaleMultiplier;
            cardObj.transform.DOScale(targetScale, generateSpawnDuration).SetEase(Ease.OutBack);
        }

        yield return YieldInstructionCache.WaitForSeconds(generateSpawnDuration + 0.1f);

        // 2단계: 중앙에 겹친 카드들이 살짝 펼쳐짐 (부채꼴 / 가로 원호 펼침)
        float spacing = Mathf.Min(generateFanSpacing, 7.5f / Mathf.Max(1, count - 1));
        float angleStep = Mathf.Min(generateFanAngle, 18f / Mathf.Max(1, count - 1));

        for (int i = 0; i < count; i++)
        {
            var cardObj = spawnedCards[i];
            if (cardObj == null) continue;

            float offsetRatio = i - (count - 1) * 0.5f;

            // 가로로 펼쳐지며 바깥쪽은 자연스러운 원호(Arc)를 그리도록 Y 오프셋 적용
            Vector3 targetPos = centerPos
                + targetTransform.right * (offsetRatio * spacing)
                - targetTransform.up * (Mathf.Abs(offsetRatio) * 0.2f)
                + targetTransform.forward * (i * -0.05f);

            Quaternion targetRot = baseRot * Quaternion.Euler(0f, 0f, -offsetRatio * angleStep);

            cardObj.transform.DOMove(targetPos, 0.4f).SetEase(Ease.OutCubic);
            cardObj.transform.DORotateQuaternion(targetRot, 0.4f).SetEase(Ease.OutCubic);
        }

        // 펼쳐진 상태로 유저가 카드를 확인할 수 있도록 대기
        yield return YieldInstructionCache.WaitForSeconds(generateShowDuration);

        // 3단계: 펼쳐진 카드들이 순서대로 손패로 들어가거나 소각됨
        for (int i = 0; i < count; i++)
        {
            var cardObj = spawnedCards[i];
            var cardData = cards[i];
            if (cardObj != null)
            {
                if (cardData != null && cardData.isBurned)
                {
                    Debug.Log($"<color=red>[CardDrawManager]</color> 🔥 다중 생성 카드 [{cardData.cardId}] 손패 초과(10장)로 소각 연출 실행!");
                    PlayCardBurnAnimation(cardObj);
                }
                else
                {
                    cardObj.name = $"Card [{cardData.cardId}]";
                    handCardControllManager.AddCardToHand(cardObj);
                }
            }
            yield return YieldInstructionCache.WaitForSeconds(generateHandEnterInterval);
        }
    }
}