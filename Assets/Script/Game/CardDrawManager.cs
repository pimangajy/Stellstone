using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

/// <summary>
/// (수정) 덱 드로우 애니메이션(덱 -> 보여주기)만 담당합니다.
/// 애니메이션이 끝나면 HandInteractionManager에게 카드를 넘깁니다.
/// </summary>
public class CardDrawManager : MonoBehaviour
{
    public static CardDrawManager Instance;

    [Header("프리팹 및 씬 연결")]
    public GameObject cardPrefab;
    public Transform deckTransform;
    [Tooltip("1단계: 카드를 보여줄 위치 (예: 화면 중앙)")]
    public Transform showCardTransform;

    [Header("핵심 연결")]
    [Tooltip("손패 레이아웃과 상호작용을 담당하는 관리자")]
    public HandInteractionManager handInteractionManager; // (중요)

    [Header("애니메이션 설정")]
    [Tooltip("1단계(덱->보여주기) 애니메이션 시간")]
    public float drawDuration = 0.4f;
    [Tooltip("2단계(보여주기) 대기 시간")]
    public float showDuration = 0.6f; // 유저가 카드를 읽을 시간
    [Tooltip("여러 장 뽑을 때 카드 사이의 간격(초)")]
    public float batchDrawInterval = 0.5f;

    //[Header("데이터베이스")]
    //[Tooltip("게임에 존재하는 모든 카드 데이터를 등록해주세요. (ID로 찾기 위함)")]
    // 실제로는 별도의 ResourceManager나 Addressables를 쓰는 것이 좋지만, 
    // 지금은 간단하게 여기에 리스트를 두겠습니다.
    // public List<CardData> allCardDataList;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        // HandInteractionManager가 연결되었는지 확인
        if (handInteractionManager == null)
        {
            Debug.LogError("[CardDrawManager] HandInteractionManager가 설정되지 않았습니다!");
        }
    }

    // --- 테스트 코드 ---
    void Update()
    {
        // D: 카드 뽑기
        if (Input.GetKeyDown(KeyCode.D))
        {
            var testCardData = new CardInfo
            {
                cardId = "cards-gangzi-001",
                instanceId = "instance_" + Random.Range(1000, 9999)
            };
            PerformDrawAnimation(testCardData);
        }

        // (신규) B: 카드 3장 뽑기(Batch) 테스트
        if (Input.GetKeyDown(KeyCode.B))
        {
            List<CardInfo> testBatch = new List<CardInfo>();
            for (int i = 0; i < 3; i++)
            {
                testBatch.Add(new CardInfo
                {
                    cardId = "BatchCard_" + i,
                    instanceId = "inst_" + Random.Range(10000, 99999)
                });
            }
            PerformBatchDraw(testBatch);
        }

        // R키 테스트는 HandInteractionManager로 이동했습니다.
    }

    /// <summary>
    /// (신규) 여러 장의 카드 정보를 받아 순차적으로 드로우합니다.
    /// 멀리건 종료 후나 '카드 2장 드로우' 같은 효과에 사용하세요.
    /// </summary>
    public void PerformBatchDraw(List<CardInfo> cards)
    {
        if (cards == null || cards.Count == 0) return;

        StartCoroutine(BatchDrawRoutine(cards));
    }
    /// <summary>
    /// (내부용) 리스트의 카드를 일정 간격으로 하나씩 뽑는 코루틴
    /// </summary>
    private IEnumerator BatchDrawRoutine(List<CardInfo> cards)
    {
        foreach (var cardData in cards)
        {
            PerformDrawAnimation(cardData);

            // 다음 카드 뽑기 전까지 대기
            yield return new WaitForSeconds(batchDrawInterval);
        }
    }
    // --- 테스트 코드 끝 ---

    /// <summary>
    /// (내부 헬퍼) ID로 CardData ScriptableObject를 찾습니다.
    /// CardDatabaseManager(Firebase)에 데이터가 있다면 최신 정보를 반영합니다.
    /// </summary>
    private CardData GetCardDataById(string id)
    {
        // 1. [기본] 로컬 리소스/리스트에서 에셋(특히 이미지)이 포함된 원본 데이터를 먼저 찾습니다.
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[CardDrawManager] ResourceManager Instance가 없습니다!");
            return null;
        }

        // ResourceManager에게 ID로 카드 데이터 요청
        CardData localData = ResourceManager.Instance.GetCardData(id);

        if (localData == null)
        {
            Debug.LogWarning($"[CardDrawManager] ResourceManager에서 ID '{id}'인 카드를 찾을 수 없습니다.");
        }


        // 2. [최신화] Firebase 매니저에서 밸런스 패치된 데이터가 있는지 확인합니다.
        if (CardDatabaseManager.instance != null)
        {
            // 주의: 게임 시작 시점에 이미 CardDatabaseManager.GetAllCardsAsync()가 호출되어
            // 데이터가 캐싱되어 있어야 동기적으로 가져올 수 있습니다.
            var task = CardDatabaseManager.instance.GetAllCardsAsync();

            // Task가 완료되었고(캐시 적중), 데이터가 존재하는 경우
            if (task.IsCompleted && task.Result != null && task.Result.ContainsKey(id))
            {
                CardDataFirebase firebaseData = task.Result[id];

                // 로컬 데이터가 있다면 복제(Clone)하여 원본 에셋을 보호하고,
                // 없다면 임시 인스턴스를 만듭니다.
                if (localData != null)
                {
                    localData = Instantiate(localData); // 복제본 생성
                    localData.name = firebaseData.name; // 인스펙터 이름 변경
                }
                else
                {
                    localData = ScriptableObject.CreateInstance<CardData>();
                    localData.cardID = id;
                    // 이미지가 없는 경우 기본 이미지 처리 등이 필요할 수 있음
                }

                // 3. Firebase 데이터로 스탯 및 텍스트 덮어쓰기
                localData.cardName = firebaseData.name;
                localData.description = firebaseData.description;
                localData.manaCost = firebaseData.cost;

                // object 타입 안전하게 int로 변환 (DB에 값이 있다면)
                if (firebaseData.attack != null)
                    localData.attack = System.Convert.ToInt32(firebaseData.attack);

                if (firebaseData.health != null)
                    localData.health = System.Convert.ToInt32(firebaseData.health);

                // 필요 시 Enum 파싱 (예: 종족, 타입 등)
                // if (System.Enum.TryParse(firebaseData.type, out CardType type)) localData.cardType = type;

                return localData;
            }
        }

        if (localData == null)
        {
            Debug.LogWarning($"[CardDrawManager] ID '{id}'에 해당하는 CardData를 찾을 수 없습니다.");
        }

        return localData;
    }

    /// <summary>
    /// (GameClient가 호출)
    /// 서버로부터 받은 카드 데이터로 드로우 애니메이션을 시작합니다.
    /// </summary>
    public void PerformDrawAnimation(CardInfo cardData)
    {
        if (cardPrefab == null || deckTransform == null || showCardTransform == null || handInteractionManager == null)
        {
            Debug.LogError("CardDrawManager: 필수 Transform(프리팹, 덱, 보여주기) 또는 HandInteractionManager가 설정되지 않았습니다!");
            return;
        }

        // 1. 덱 위치에 새 카드 오브젝트 생성
        GameObject newCardObject = Instantiate(
            cardPrefab,
            deckTransform.position,
            deckTransform.rotation
        );

        newCardObject.name = $"Card [{cardData.cardId}] (Drawing)";

        // --- [수정된 부분] 데이터 주입 ---

        // 1) ID에 맞는 원본 데이터(ScriptableObject) 찾기
        CardData staticData = GetCardDataById(cardData.cardId);

        // 2) 프리팹에서 CardDisplay 컴포넌트 가져오기
        GameCardDisplay display = newCardObject.GetComponent<GameCardDisplay>();

        if (display != null && staticData != null)
        {
            // 3) 데이터 세팅 (이미지, 텍스트 등 갱신)
            display.Setup(staticData, cardData);
        }
        else
        {
            if (display == null) Debug.LogError("카드 프리팹에 'CardDisplay' 스크립트가 없습니다!");
        }



        // (중요) 카드의 레이어를 프리팹 설정대로 따르게 합니다.
        // HandInteractionManager가 Raycast할 수 있도록 프리팹의 레이어를 미리 설정해주세요.

        // 2. DOTween Sequence: 덱 -> 보여주기 -> 대기
        Sequence drawSequence = DOTween.Sequence();

        // 1단계: 덱에서 -> "카드 보여주기" 위치로 이동 + 회전
        drawSequence.Append(
            newCardObject.transform.DOMove(showCardTransform.position, drawDuration)
                .SetEase(Ease.OutQuad)
        );
        drawSequence.Join(
            newCardObject.transform.DORotateQuaternion(showCardTransform.rotation, drawDuration)
                .SetEase(Ease.OutQuad)
        );

        // 2단계: "카드 보여주기" 위치에서 잠시 대기
        drawSequence.AppendInterval(showDuration);

        // 3단계: (핵심) 애니메이션이 끝나면 HandInteractionManager에게 카드를 넘김
        drawSequence.OnComplete(() =>
        {
            newCardObject.name = $"Card [{cardData.cardId}]"; // 이름 변경
            handInteractionManager.AddCardToHand(newCardObject);
        });
    }
}