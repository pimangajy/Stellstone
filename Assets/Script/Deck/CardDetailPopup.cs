using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 덱 편성 화면에서 카드를 클릭했을 때 열리는 [카드 상세 정보 및 분해 팝업] 매니저입니다.
/// - 큰 복사본 카드 표시 (동적 프리팹 생성 또는 씬 내 미리 배치된 Display 지원)
/// - 카드의 세부 스탯, 텍스트, 플레이버 텍스트(additionalExplanation) 및 실제 보유 수량 표시
/// - 분해 버튼 및 닫기 기능 제공
/// </summary>
public class CardDetailPopup : MonoBehaviour
{
    public static CardDetailPopup Instance { get; private set; }

    [Header("1. 팝업 패널 루트")]
    [Tooltip("팝업 전체 루트 GameObject (비워두면 이 스크립트가 붙은 오브젝트 사용)")]
    [SerializeField] private GameObject popupPanel;

    [Header("2. 복사본 큰 카드 표시 영역")]
    [Tooltip("복사본 카드가 생성될 부모 Transform (프리팹 동적 생성 방식 사용 시)")]
    [SerializeField] private Transform bigCardParent;

    [Tooltip("복제에 사용할 카드 프리팹 (Assets/Prefab/Deck/Card.prefab)")]
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("동적 생성 시 카드 확대 배율 (기본: 1.35)")]
    [SerializeField] private float cardScale = 1.35f;

    [Header("3. 카드 상세 정보 텍스트 (선택 사항)")]
    [Tooltip("카드 이름 텍스트")]
    [SerializeField] private TextMeshProUGUI cardNameText;

    [Tooltip("카드 분류 텍스트 (예: '하수인 - 중립 - 일반')")]
    [SerializeField] private TextMeshProUGUI cardCategoryText;

    [Tooltip("카드 종족 텍스트")]
    [SerializeField] private TextMeshProUGUI cardTribeText;

    [Tooltip("카드 효과 설명 텍스트")]
    [SerializeField] private TextMeshProUGUI cardDescriptionText;

    [Tooltip("추가 플레이버 텍스트 (additionalExplanation)")]
    [SerializeField] private TextMeshProUGUI cardFlavorText;

    [Header("4. 버튼류")]
    [Tooltip("분해 버튼")]
    [SerializeField] private Button disenchantButton;

    [Tooltip("분해 시 획득할 스타더스트 수치 텍스트 (예: '+20 가루')")]
    [SerializeField] private TMP_Text disenchantDustText;

    [Tooltip("제작 버튼")]
    [SerializeField] private Button craftButton;

    [Tooltip("제작 시 소모할 스타더스트 수치 텍스트 (예: '40 가루')")]
    [SerializeField] private TMP_Text craftDustText;

    [Tooltip("닫기 버튼")]
    [SerializeField] private Button closeButton;

    [Tooltip("팝업 바깥 어두운 배경 (클릭 시 닫기)")]
    [SerializeField] private Button backgroundBlockerButton;

    [Header("5. 희귀도별 분해 획득 스타더스트 설정 (서버 미동기화 시 기본값)")]
    [SerializeField] private int commonDust = 5;
    [SerializeField] private int rareDust = 20;
    [SerializeField] private int epicDust = 100;
    [SerializeField] private int legendaryDust = 400;

    [Header("6. 희귀도별 제작 소모 스타더스트 설정 (서버 미동기화 시 기본값)")]
    [SerializeField] private int craftCommonDust = 40;
    [SerializeField] private int craftRareDust = 100;
    [SerializeField] private int craftEpicDust = 400;
    [SerializeField] private int craftLegendaryDust = 1600;

    [Header("7. 로딩 패널 (중복 조작 방지)")]
    [Tooltip("통신 중 표시될 로딩 패널 (터치/클릭 차단용)")]
    [SerializeField] private GameObject loadingPanel;

    [Header("8. 대표 카드(좋아요) 설정")]
    [Tooltip("우측 상단 별모양 좋아요 버튼")]
    [SerializeField] private Button favoriteStarButton;
    [Tooltip("좋아요 별 텍스트 (★)")]
    [SerializeField] private TMP_Text favoriteStarText;
    [Tooltip("좋아요 상태 라벨 (선택 사항)")]
    [SerializeField] private TMP_Text favoriteLabelText;
    [Tooltip("좋아요 별 이미지 (아이콘 사용 시)")]
    [SerializeField] private Image favoriteStarImage;

    // 분해/제작 요청 시 외부(서버 통신 등)로 알릴 이벤트
    public static event Action<CardData, int> OnDisenchantRequested;
    public static event Action<CardData, int> OnCraftRequested;

    // 서버에서 가져온 제작/분해 가루 레이트 캐시
    private static DustRatesResponse cachedDustRates;
    private static bool isFetchingDustRates = false;

    private CardData currentCardData;
    private GameObject spawnedCardObject;
    private DeckCardDisplay cachedCardDisplay;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (popupPanel == null)
        {
            popupPanel = gameObject;
        }

        // 버튼 리스너 등록
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (backgroundBlockerButton != null)
        {
            backgroundBlockerButton.onClick.AddListener(Close);
        }

        if (disenchantButton != null)
        {
            disenchantButton.onClick.AddListener(OnDisenchantButtonClicked);
        }

        if (craftButton != null)
        {
            craftButton.onClick.AddListener(OnCraftButtonClicked);
        }

        if (favoriteStarButton != null)
        {
            favoriteStarButton.onClick.AddListener(OnFavoriteStarButtonClicked);
        }

        // 시작 시에는 팝업 및 로딩 패널 닫힘 상태 유지
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 전달받은 카드의 상세 정보와 복사본을 띄우며 팝업을 엽니다.
    /// </summary>
    public void Open(CardData cardData)
    {
        if (cardData == null) return;

        currentCardData = cardData;

        // 키워드 툴팁이 혹시 열려 있다면 닫기
        CardKeywordTooltipPopup.Instance?.Hide();

        // 1. 팝업 활성화
        if (popupPanel != null)
        {
            // popupPanel.SetActive(true); UIManager  사용
            UIManager.Instance.OpenPopup(popupPanel);
        }

        // 2. 복사본 큰 카드 표시
        SetupBigCardDisplay(cardData);

        // 3. 상세 정보 텍스트 바인딩
        BindCardDetails(cardData);

        // 4. 분해 및 제작 버튼 상태 갱신
        UpdateDisenchantUI(cardData);
        UpdateCraftUI(cardData);

        // 5. 대표 카드(좋아요) 상태 갱신
        UpdateFavoriteUI(cardData);

        // 6. 서버 가루 레이트 최신화 (캐시가 없으면 비동기 조회)
        if (cachedDustRates == null)
        {
            StartCoroutine(FetchDustRatesCoroutine());
        }

        Debug.Log($"[CardDetailPopup] '{cardData.cardName}' 카드 상세 팝업 열림");
    }

    /// <summary>
    /// 복사본 카드를 최초 1회만 생성하고 이후에는 재사용하여 정보를 채웁니다.
    /// </summary>
    private void SetupBigCardDisplay(CardData cardData)
    {
        // 팝업용 카드가 아직 생성되지 않았다면 최초 1회만 Instantiate
        if (spawnedCardObject == null)
        {
            if (cardPrefab != null && bigCardParent != null)
            {
                spawnedCardObject = Instantiate(cardPrefab, bigCardParent);
                spawnedCardObject.transform.localScale = Vector3.one * cardScale;
                cachedCardDisplay = spawnedCardObject.GetComponent<DeckCardDisplay>();

                // 팝업 내부의 카드는 추가 클릭 인터랙션 제거
                CardInteraction interaction = spawnedCardObject.GetComponent<CardInteraction>();
                if (interaction != null)
                {
                    Destroy(interaction);
                }
            }
        }

        if (spawnedCardObject != null)
        {
            if (!spawnedCardObject.activeSelf)
            {
                spawnedCardObject.SetActive(true);
            }

            if (cachedCardDisplay == null)
            {
                cachedCardDisplay = spawnedCardObject.GetComponent<DeckCardDisplay>();
            }

            // 카드 정보 채우기
            if (cachedCardDisplay != null)
            {
                // 실제 유저 보유 수량을 전달하여 카드의 countText(예: X 2)에 반영
                int owned = (DeckManager.instance != null)
                    ? DeckManager.instance.GetOwnedCardCount(cardData)
                    : 0;

                cachedCardDisplay.Setup(cardData, owned);

                // 팝업에서는 카드를 선명하게 볼 수 있도록 음영 패널만 비활성화 (수량 텍스트는 표시 유지)
                if (cachedCardDisplay.disabledOverlayPanel != null)
                {
                    cachedCardDisplay.disabledOverlayPanel.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// 카드의 세부 정보를 텍스트 UI에 바인딩합니다.
    /// </summary>
    private void BindCardDetails(CardData cardData)
    {
        if (cardNameText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(cardNameText, minSize: 18f, maxSize: 42f, wordWrap: false, overflowMode: TextOverflowModes.Ellipsis);
            cardNameText.text = cardData.cardName;
        }

        if (cardCategoryText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(cardCategoryText, minSize: 12f, maxSize: 24f, wordWrap: false, overflowMode: TextOverflowModes.Ellipsis);
            string typeStr = cardData.cardType.ToString();
            string classStr = (cardData.cardClass == CardClass.Gangzi) ? "중립" : cardData.cardClass.ToString();
            string rarityStr = GetRarityKorean(cardData.rarity);
            cardCategoryText.text = $"{typeStr} · {classStr} · {rarityStr}";
        }

        // 종족 정보 바인딩
        if (cardTribeText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(cardTribeText, minSize: 12f, maxSize: 24f, wordWrap: false, overflowMode: TextOverflowModes.Ellipsis);
            if (cardData.minionTribe != CardTribe.무소속)
            {
                cardTribeText.gameObject.SetActive(true);
                cardTribeText.text = cardData.minionTribe.ToString();
            }
            else
            {
                cardTribeText.gameObject.SetActive(false);
            }
        }

        if (cardDescriptionText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(cardDescriptionText, minSize: 14f, maxSize: 30f, wordWrap: true, overflowMode: TextOverflowModes.Ellipsis);
            CardTextFormatter.FormatAndBind(cardDescriptionText, cardData.description, cardData);
        }

        if (cardFlavorText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(cardFlavorText, minSize: 11f, maxSize: 22f, wordWrap: true, overflowMode: TextOverflowModes.Ellipsis);
            if (!string.IsNullOrEmpty(cardData.additionalExplanation))
            {
                cardFlavorText.gameObject.SetActive(true);
                cardFlavorText.text = cardData.additionalExplanation;
            }
            else
            {
                cardFlavorText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 분해 버튼 활성화 상태 및 획득 가루 수치를 갱신합니다.
    /// </summary>
    private void UpdateDisenchantUI(CardData cardData)
    {
        if (cardData == null) return;

        bool isDefault = cardData.isDefaultCard;
        int dustGain = GetDisenchantDustAmount(cardData.rarity);

        if (disenchantDustText != null)
        {
            if (isDefault)
            {
                disenchantDustText.text = "기본 카드";
            }
            else
            {
                disenchantDustText.text = $"+{dustGain} 스타더스트";
            }
        }

        if (disenchantButton != null)
        {
            int owned = (DeckManager.instance != null)
                ? DeckManager.instance.GetOwnedCardCount(cardData)
                : 1;

            // 기본 카드는 분해 불가, 보유량이 0장이어도 분해 불가
            disenchantButton.interactable = !isDefault && (owned > 0);
        }
    }

    /// <summary>
    /// 희귀도에 따른 분해 스타더스트 획득량을 반환합니다. (서버 레이트 우선)
    /// </summary>
    public int GetDisenchantDustAmount(CardRarity rarity)
    {
        if (cachedDustRates != null && cachedDustRates.disenchant != null)
        {
            switch (rarity)
            {
                case CardRarity.common: return cachedDustRates.disenchant.common;
                case CardRarity.rare: return cachedDustRates.disenchant.rare;
                case CardRarity.epic: return cachedDustRates.disenchant.epic;
                case CardRarity.legendary: return cachedDustRates.disenchant.legendary;
            }
        }

        switch (rarity)
        {
            case CardRarity.common: return commonDust;
            case CardRarity.rare: return rareDust;
            case CardRarity.epic: return epicDust;
            case CardRarity.legendary: return legendaryDust;
            default: return commonDust;
        }
    }

    /// <summary>
    /// 희귀도에 따른 제작 스타더스트 소모량을 반환합니다. (서버 레이트 우선)
    /// </summary>
    public int GetCraftDustAmount(CardRarity rarity)
    {
        if (cachedDustRates != null && cachedDustRates.craft != null)
        {
            switch (rarity)
            {
                case CardRarity.common: return cachedDustRates.craft.common;
                case CardRarity.rare: return cachedDustRates.craft.rare;
                case CardRarity.epic: return cachedDustRates.craft.epic;
                case CardRarity.legendary: return cachedDustRates.craft.legendary;
            }
        }

        switch (rarity)
        {
            case CardRarity.common: return craftCommonDust;
            case CardRarity.rare: return craftRareDust;
            case CardRarity.epic: return craftEpicDust;
            case CardRarity.legendary: return craftLegendaryDust;
            default: return craftCommonDust;
        }
    }

    /// <summary>
    /// 제작 버튼 활성화 상태 및 소모 가루 수치를 갱신합니다.
    /// </summary>
    private void UpdateCraftUI(CardData cardData)
    {
        if (cardData == null) return;

        int cost = GetCraftDustAmount(cardData.rarity);

        if (craftDustText != null)
        {
            craftDustText.text = $"{cost} 스타더스트";
        }

        if (craftButton != null)
        {
            int currentStardust = GameClient.Instance?.CurrentUser?.stardust
                ?? SinginManager.CurrentUserData?.stardust
                ?? 0;

            // 현재 가루가 제작 비용 이상일 때만 활성화
            craftButton.interactable = (currentStardust >= cost);
        }
    }

    /// <summary>
    /// 서버에서 최신 제작/분해 가루 레이트(GET /api/cards/dust-rates)를 비동기 수신하여 캐싱합니다.
    /// </summary>
    private IEnumerator FetchDustRatesCoroutine()
    {
        if (cachedDustRates != null || isFetchingDustRates) yield break;
        isFetchingDustRates = true;

        string url = (GameClient.Instance != null)
            ? GameClient.Instance.GetApiUrl("cards/dust-rates")
            : "http://175.125.250.226:5123/api/cards/dust-rates";

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string json = webRequest.downloadHandler.text;
                try
                {
                    DustRatesResponse res = JsonUtility.FromJson<DustRatesResponse>(json);
                    if (res != null && res.status == "success")
                    {
                        cachedDustRates = res;
                        Debug.Log($"[CardDetailPopup] 📥 서버 가루 레이트 동기화 완료");
                        if (currentCardData != null)
                        {
                            UpdateDisenchantUI(currentCardData);
                            UpdateCraftUI(currentCardData);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CardDetailPopup] 가루 레이트 파싱 실패, 기본값 사용: {e.Message}");
                }
            }
        }

        isFetchingDustRates = false;
    }

    /// <summary>
    /// 분해 버튼 클릭 시 호출됩니다.
    /// </summary>
    private void OnDisenchantButtonClicked()
    {
        if (currentCardData == null || currentCardData.isDefaultCard) return;

        // 분해 통신 코루틴 시작 (로딩 패널 활성화로 연타 방지)
        StartCoroutine(SendDisenchantRequestCoroutine(currentCardData));
    }

    /// <summary>
    /// 제작 버튼 클릭 시 호출됩니다.
    /// </summary>
    private void OnCraftButtonClicked()
    {
        if (currentCardData == null) return;

        // 제작 통신 코루틴 시작 (로딩 패널 활성화로 연타 방지)
        StartCoroutine(SendCraftRequestCoroutine(currentCardData));
    }

    /// <summary>
    /// 대표 카드(좋아요) 별모양 버튼 클릭 시 호출됩니다. (단 하나만 선택 가능)
    /// </summary>
    private void OnFavoriteStarButtonClicked()
    {
        if (currentCardData == null) return;

        // 미보유 카드(보유량 0)는 즐겨찾기 불가
        int owned = (DeckManager.instance != null)
            ? DeckManager.instance.GetOwnedCardCount(currentCardData)
            : 0;
        if (owned <= 0)
        {
            Debug.LogWarning($"[CardDetailPopup] '{currentCardData.cardName}' 카드는 미보유 상태이므로 대표 카드로 지정할 수 없습니다.");
            return;
        }

        bool isFav = FavoriteCardManager.ToggleFavorite(currentCardData.cardID);
        UpdateFavoriteUI(currentCardData);
    }

    /// <summary>
    /// 카드 상세 팝업의 대표 카드(좋아요) UI 상태를 갱신합니다.
    /// </summary>
    public void UpdateFavoriteUI(CardData cardData)
    {
        if (cardData == null) return;

        int owned = (DeckManager.instance != null)
            ? DeckManager.instance.GetOwnedCardCount(cardData)
            : 0;
        bool isOwned = owned > 0;

        bool isFav = isOwned && FavoriteCardManager.IsFavorite(cardData.cardID);

        if (favoriteStarButton != null)
        {
            favoriteStarButton.interactable = isOwned;
        }

        if (favoriteStarText != null)
        {
            favoriteStarText.text = "★";
            if (!isOwned)
            {
                favoriteStarText.color = new Color(0.6f, 0.6f, 0.6f, 0.4f); // 미보유 비활성 그레이
            }
            else
            {
                favoriteStarText.color = isFav 
                    ? new Color(1f, 0.82f, 0.2f, 1f)      // 황금빛 별 (#FFD133)
                    : new Color(0.72f, 0.65f, 0.78f, 1f); // 파스텔 라벤더 (#B8A6C7)
            }
        }

        if (favoriteStarImage != null)
        {
            if (!isOwned)
            {
                favoriteStarImage.color = new Color(0.6f, 0.6f, 0.6f, 0.3f);
            }
            else
            {
                favoriteStarImage.color = isFav 
                    ? new Color(1f, 0.82f, 0.2f, 1f)
                    : new Color(0.72f, 0.65f, 0.78f, 0.6f);
            }
        }

        if (favoriteLabelText != null)
        {
            if (!isOwned)
            {
                favoriteLabelText.text = "미보유";
                favoriteLabelText.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            }
            else
            {
                favoriteLabelText.text = isFav ? "대표 카드" : "좋아요";
                favoriteLabelText.color = isFav 
                    ? new Color(0.42f, 0.22f, 0.05f, 1f)
                    : new Color(0.45f, 0.35f, 0.52f, 1f);
            }
        }
    }

    /// <summary>
    /// 서버에 카드 분해 API(POST /api/cards/disenchant)를 호출합니다.
    /// </summary>
    private IEnumerator SendDisenchantRequestCoroutine(CardData cardData)
    {
        if (cardData == null) yield break;

        // 1. 중복 조작 방지용 로딩 패널 활성화
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        // 2. Firebase 토큰 획득
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth == null || auth.CurrentUser == null)
        {
            Debug.LogError("[CardDetailPopup] ❌ 로그인된 Firebase 유저 정보가 없습니다.");
            if (loadingPanel != null) loadingPanel.SetActive(false);
            yield break;
        }

        var tokenTask = auth.CurrentUser.TokenAsync(true);
        yield return new WaitUntil(() => tokenTask.IsCompleted);

        if (tokenTask.IsFaulted || string.IsNullOrEmpty(tokenTask.Result))
        {
            Debug.LogError($"[CardDetailPopup] ❌ 토큰 발급 실패: {tokenTask.Exception?.Message}");
            if (loadingPanel != null) loadingPanel.SetActive(false);
            yield break;
        }

        string idToken = tokenTask.Result;

        // 3. API 엔드포인트 및 요청 바디 생성
        string url = (GameClient.Instance != null)
            ? GameClient.Instance.GetApiUrl("cards/disenchant")
            : "http://175.125.250.226:5123/api/cards/disenchant";

        DisenchantCardRequest req = new DisenchantCardRequest
        {
            cardId = cardData.cardID,
            count = 1
        };

        string jsonBody = JsonUtility.ToJson(req);
        Debug.Log($"[CardDetailPopup] 🔨 카드 분해 요청 전송: {url} | CardID: {cardData.cardID}");

        // 4. HTTP POST 통신 전송
        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", "Bearer " + idToken);

            yield return webRequest.SendWebRequest();

            // 통신 완료 후 로딩 패널 닫기
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = webRequest.downloadHandler.text;
                Debug.Log($"[CardDetailPopup] ✅ 분해 응답 수신: {jsonResponse}");

                DisenchantCardResponse res = JsonUtility.FromJson<DisenchantCardResponse>(jsonResponse);
                if (res != null && res.status == "success")
                {
                    // 1) 로컬 유저 계정 데이터(보유 카드 및 스타더스트) 즉시 동기화
                    ApplyDisenchantResult(res);

                    // 2) 팝업 내 복사본 큰 카드 수량 및 분해 버튼 상태 갱신
                    SetupBigCardDisplay(currentCardData);
                    UpdateDisenchantUI(currentCardData);

                    // 3) 배경의 덱 보관함 전체 카드의 수량 뱃지 및 반투명 음영 패널 동기화
                    if (DeckBuilder.Instance != null)
                    {
                        DeckBuilder.Instance.UpdateAllCardCounts();
                    }

                    // 4) 외부 이벤트 발행
                    OnDisenchantRequested?.Invoke(currentCardData, res.gainedStardust);
                }
                else
                {
                    Debug.LogError($"[CardDetailPopup] ❌ 분해 처리 실패: {res?.message ?? jsonResponse}");
                }
            }
            else
            {
                Debug.LogError($"[CardDetailPopup] ❌ 분해 통신 에러: {webRequest.error} | {webRequest.downloadHandler?.text}");
            }
        }
    }

    /// <summary>
    /// 서버 분해 결과를 로컬 계정 데이터(GameClient, SinginManager, UserCurrencyDisplay)에 반영합니다.
    /// </summary>
    private void ApplyDisenchantResult(DisenchantCardResponse res)
    {
        // 1. GameClient.CurrentUser 갱신
        if (GameClient.Instance != null && GameClient.Instance.CurrentUser != null)
        {
            var user = GameClient.Instance.CurrentUser;
            user.stardust = res.remainingStardust;
            if (user.ownedCards == null) user.ownedCards = new Dictionary<string, int>();
            user.ownedCards[res.cardId] = res.remainingCardCount;
        }

        // 2. SinginManager.CurrentUserData 갱신
        if (SinginManager.CurrentUserData != null)
        {
            var user = SinginManager.CurrentUserData;
            user.stardust = res.remainingStardust;
            if (user.ownedCards == null) user.ownedCards = new Dictionary<string, int>();
            user.ownedCards[res.cardId] = res.remainingCardCount;
        }

        // 3. 상단 전역 재화 UI(UserCurrencyDisplay) 실시간 갱신
        if (UserCurrencyDisplay.Instance != null)
        {
            int gold = GameClient.Instance?.CurrentUser?.gold ?? SinginManager.CurrentUserData?.gold ?? 0;
            int stella = GameClient.Instance?.CurrentUser?.stellastone ?? SinginManager.CurrentUserData?.stellastone ?? 0;
            UserCurrencyDisplay.Instance.SetCurrency(gold, stella, res.remainingStardust);
        }

        // 분해 후 보유 가루 변경에 따른 제작 버튼 상태 동기화
        if (currentCardData != null)
        {
            UpdateCraftUI(currentCardData);
        }
    }

    /// <summary>
    /// 서버에 카드 제작 API(POST /api/cards/craft)를 호출합니다.
    /// </summary>
    private IEnumerator SendCraftRequestCoroutine(CardData cardData)
    {
        if (cardData == null) yield break;

        // 1. 중복 조작 방지용 로딩 패널 활성화
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        // 2. Firebase 토큰 획득
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        if (auth == null || auth.CurrentUser == null)
        {
            Debug.LogError("[CardDetailPopup] ❌ 로그인된 Firebase 유저 정보가 없습니다.");
            if (loadingPanel != null) loadingPanel.SetActive(false);
            yield break;
        }

        var tokenTask = auth.CurrentUser.TokenAsync(true);
        yield return new WaitUntil(() => tokenTask.IsCompleted);

        if (tokenTask.IsFaulted || string.IsNullOrEmpty(tokenTask.Result))
        {
            Debug.LogError($"[CardDetailPopup] ❌ 토큰 발급 실패: {tokenTask.Exception?.Message}");
            if (loadingPanel != null) loadingPanel.SetActive(false);
            yield break;
        }

        string idToken = tokenTask.Result;

        // 3. API 엔드포인트 및 요청 바디 생성
        string url = (GameClient.Instance != null)
            ? GameClient.Instance.GetApiUrl("cards/craft")
            : "http://175.125.250.226:5123/api/cards/craft";

        CraftCardRequest req = new CraftCardRequest
        {
            cardId = cardData.cardID,
            count = 1
        };

        string jsonBody = JsonUtility.ToJson(req);
        Debug.Log($"[CardDetailPopup] ✨ 카드 제작 요청 전송: {url} | CardID: {cardData.cardID}");

        // 4. HTTP POST 통신 전송
        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", "Bearer " + idToken);

            yield return webRequest.SendWebRequest();

            // 통신 완료 후 로딩 패널 닫기
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = webRequest.downloadHandler.text;
                Debug.Log($"[CardDetailPopup] ✅ 제작 응답 수신: {jsonResponse}");

                CraftCardResponse res = JsonUtility.FromJson<CraftCardResponse>(jsonResponse);
                if (res != null && res.status == "success")
                {
                    // 1) 로컬 유저 계정 데이터(보유 카드 및 스타더스트) 즉시 동기화
                    ApplyCraftResult(res);

                    // 2) 팝업 내 복사본 큰 카드 수량 및 분해/제작 버튼 상태 갱신
                    SetupBigCardDisplay(currentCardData);
                    UpdateDisenchantUI(currentCardData);
                    UpdateCraftUI(currentCardData);

                    // 3) 배경의 덱 보관함 전체 카드의 수량 뱃지 및 반투명 음영 패널 동기화
                    if (DeckBuilder.Instance != null)
                    {
                        DeckBuilder.Instance.UpdateAllCardCounts();
                    }

                    // 4) 외부 이벤트 발행
                    OnCraftRequested?.Invoke(currentCardData, res.consumedStardust);
                }
                else
                {
                    Debug.LogError($"[CardDetailPopup] ❌ 제작 처리 실패: {res?.message ?? jsonResponse}");
                }
            }
            else
            {
                Debug.LogError($"[CardDetailPopup] ❌ 제작 통신 에러: {webRequest.error} | {webRequest.downloadHandler?.text}");
            }
        }
    }

    /// <summary>
    /// 서버 제작 결과를 로컬 계정 데이터(GameClient, SinginManager, UserCurrencyDisplay)에 반영합니다.
    /// </summary>
    private void ApplyCraftResult(CraftCardResponse res)
    {
        // 1. GameClient.CurrentUser 갱신
        if (GameClient.Instance != null && GameClient.Instance.CurrentUser != null)
        {
            var user = GameClient.Instance.CurrentUser;
            user.stardust = res.remainingStardust;
            if (user.ownedCards == null) user.ownedCards = new Dictionary<string, int>();
            user.ownedCards[res.cardId] = res.remainingCardCount;
        }

        // 2. SinginManager.CurrentUserData 갱신
        if (SinginManager.CurrentUserData != null)
        {
            var user = SinginManager.CurrentUserData;
            user.stardust = res.remainingStardust;
            if (user.ownedCards == null) user.ownedCards = new Dictionary<string, int>();
            user.ownedCards[res.cardId] = res.remainingCardCount;
        }

        // 3. 상단 전역 재화 UI(UserCurrencyDisplay) 실시간 갱신
        if (UserCurrencyDisplay.Instance != null)
        {
            int gold = GameClient.Instance?.CurrentUser?.gold ?? SinginManager.CurrentUserData?.gold ?? 0;
            int stella = GameClient.Instance?.CurrentUser?.stellastone ?? SinginManager.CurrentUserData?.stellastone ?? 0;
            UserCurrencyDisplay.Instance.SetCurrency(gold, stella, res.remainingStardust);
        }
    }

    /// <summary>
    /// 팝업을 닫고 생성된 큰 카드를 정리합니다.
    /// </summary>
    public void Close()
    {
        StopAllCoroutines();

        // 키워드 툴팁 닫기
        CardKeywordTooltipPopup.Instance?.Hide();

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        if (spawnedCardObject != null)
        {
            spawnedCardObject.SetActive(false);
        }

        if (popupPanel != null)
        {
            UIManager.Instance.CloseSpecificPopup(popupPanel);
        }

        currentCardData = null;
    }

    private void OnDestroy()
    {
        if (spawnedCardObject != null)
        {
            Destroy(spawnedCardObject);
            spawnedCardObject = null;
            cachedCardDisplay = null;
        }
    }

    private string GetRarityKorean(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.common: return "일반";
            case CardRarity.rare: return "희귀";
            case CardRarity.epic: return "특급";
            case CardRarity.legendary: return "전설";
            default: return "일반";
        }
    }
}

/// <summary>
/// 카드 분해 요청 DTO (클라이언트 -> 서버)
/// </summary>
[System.Serializable]
public class DisenchantCardRequest
{
    public string cardId;
    public int count = 1;
}

/// <summary>
/// 카드 분해 응답 DTO (서버 -> 클라이언트)
/// </summary>
[System.Serializable]
public class DisenchantCardResponse
{
    public string status;
    public string message;
    public string cardId;
    public int disenchantedCount;
    public int gainedStardust;
    public int remainingCardCount;
    public int remainingStardust;
}

/// <summary>
/// 카드 제작 요청 DTO (클라이언트 -> 서버)
/// </summary>
[System.Serializable]
public class CraftCardRequest
{
    public string cardId;
    public int count = 1;
}

/// <summary>
/// 카드 제작 응답 DTO (서버 -> 클라이언트)
/// </summary>
[System.Serializable]
public class CraftCardResponse
{
    public string status;
    public string message;
    public string cardId;
    public int craftedCount;
    public int consumedStardust;
    public int remainingCardCount;
    public int remainingStardust;
}

/// <summary>
/// 희귀도별 스타더스트 수치 그룹 DTO
/// </summary>
[System.Serializable]
public class DustRatesGroup
{
    public int common;
    public int rare;
    public int epic;
    public int legendary;
}

/// <summary>
/// 서버 가루 레이트 응답 DTO (GET /api/cards/dust-rates)
/// </summary>
[System.Serializable]
public class DustRatesResponse
{
    public string status;
    public DustRatesGroup disenchant;
    public DustRatesGroup craft;
}
