using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

/// <summary>
/// 덱에서 카드를 뽑는 '드로우 애니메이션'을 담당합니다.
/// 덱 위치에서 카드가 생성되어 -> 화면 중앙에 잠시 보였다가 -> 손패로 들어가는 연출을 합니다.
/// </summary>
public class CardDrawManager : MonoBehaviour
{
    public static CardDrawManager Instance;

    [Header("프리팹 및 씬 연결")]
    public GameObject cardPrefab; // 카드 모형
    public Transform deckTransform; // 덱 위치 (카드가 나오는 곳)
    [Tooltip("카드를 뽑았을 때 잠시 보여줄 위치 (보통 화면 중앙)")]
    public Transform showCardTransform;

    [Header("핵심 연결")]
    public HandInteractionManager handInteractionManager; // 다 뽑고나면 여기로 넘김

    [Header("애니메이션 설정")]
    public float drawDuration = 0.4f; // 덱 -> 중앙 이동 시간
    public float showDuration = 0.6f; // 중앙에서 머무는 시간
    public float batchDrawInterval = 0.5f; // 여러 장 뽑을 때 간격

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
    }

    void Start()
    {
        if (handInteractionManager == null)
            Debug.LogError("[CardDrawManager] HandInteractionManager 연결 안됨!");
    }

    // --- 테스트 코드 (키보드 D, B) ---
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.D)) // 단일 드로우 테스트
        {
            var testCardData = new CardInfo
            {
                cardId = "cards-gangzi-001",
                instanceId = "instance_" + Random.Range(1000, 9999)
            };
            PerformDrawAnimation(testCardData);
        }

        if (Input.GetKeyDown(KeyCode.B)) // 3장 드로우 테스트
        {
            List<CardInfo> testBatch = new List<CardInfo>();
            for (int i = 0; i < 3; i++)
            {
                testBatch.Add(new CardInfo { cardId = "BatchCard_" + i, instanceId = "inst_" + Random.Range(10000, 99999) });
            }
            PerformBatchDraw(testBatch);
        }
    }

    /// <summary>
    /// 여러 장을 순서대로 뽑습니다. (멀리건 종료 후 등)
    /// </summary>
    public void PerformBatchDraw(List<CardInfo> cards)
    {
        if (cards == null || cards.Count == 0) return;
        StartCoroutine(BatchDrawRoutine(cards));
    }

    private IEnumerator BatchDrawRoutine(List<CardInfo> cards)
    {
        foreach (var cardData in cards)
        {
            PerformDrawAnimation(cardData);
            yield return new WaitForSeconds(batchDrawInterval); // 한 장 뽑고 대기
        }
    }

    /// <summary>
    /// [핵심] 카드 ID를 이용해 실제 데이터를 찾습니다. (DB나 리소스 매니저 사용)
    /// </summary>
    public CardData GetCardDataById(string id)
    {
        if (ResourceManager.Instance == null) return null;

        // 1. 로컬 리소스에서 원본 찾기
        CardData localData = ResourceManager.Instance.GetCardData(id);

        // 2. Firebase(서버)에서 최신 밸런스 데이터 확인 (공격력/체력 수정 등)
        if (CardDatabaseManager.instance != null)
        {
            var task = CardDatabaseManager.instance.GetAllCardsAsync();
            if (task.IsCompleted && task.Result != null && task.Result.ContainsKey(id))
            {
                CardDataFirebase firebaseData = task.Result[id];

                // 원본을 훼손하지 않기 위해 복사본(Instance)을 만듭니다.
                if (localData != null)
                {
                    localData = Instantiate(localData);
                    localData.name = firebaseData.name;
                }
                else
                {
                    localData = ScriptableObject.CreateInstance<CardData>();
                    localData.cardID = id;
                }

                // 서버 데이터로 덮어쓰기
                localData.cardName = firebaseData.name;
                localData.description = firebaseData.description;
                localData.manaCost = firebaseData.cost;
                if (firebaseData.attack != null) localData.attack = System.Convert.ToInt32(firebaseData.attack);
                if (firebaseData.health != null) localData.health = System.Convert.ToInt32(firebaseData.health);

                return localData;
            }
        }
        return localData;
    }

    /// <summary>
    /// 카드 한 장을 뽑는 애니메이션을 실행합니다.
    /// </summary>
    public void PerformDrawAnimation(CardInfo cardData)
    {
        if (cardPrefab == null || deckTransform == null || showCardTransform == null || handInteractionManager == null) return;

        // 1. 덱 위치에 카드 생성
        GameObject newCardObject = Instantiate(cardPrefab, deckTransform.position, deckTransform.rotation);
        newCardObject.name = $"Card [{cardData.cardId}] (Drawing)";

        // 2. 데이터 주입 (이미지, 텍스트 설정)
        CardData staticData = GetCardDataById(cardData.cardId);
        GameCardDisplay display = newCardObject.GetComponent<GameCardDisplay>();

        if (display != null && staticData != null)
        {
            display.Setup(staticData, cardData);
        }

        // 3. DOTween으로 애니메이션 시퀀스 만들기
        Sequence drawSequence = DOTween.Sequence();

        // 1단계: 덱 -> 중앙 이동
        drawSequence.Append(
            newCardObject.transform.DOMove(showCardTransform.position, drawDuration).SetEase(Ease.OutQuad)
        );
        drawSequence.Join(
            newCardObject.transform.DORotateQuaternion(showCardTransform.rotation, drawDuration).SetEase(Ease.OutQuad)
        );

        // 2단계: 잠시 대기 (유저가 확인)
        drawSequence.AppendInterval(showDuration);

        // 3단계: 손패 매니저에게 카드 넘기기 (알아서 손으로 날아감)
        drawSequence.OnComplete(() =>
        {
            newCardObject.name = $"Card [{cardData.cardId}]";
            handInteractionManager.AddCardToHand(newCardObject);
        });
    }
}