using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Firestore; 
using Firebase.Auth;      
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Text;
using System;
using System.Linq;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Collections;

/// <summary>
/// 메인 로비/매치메이킹 씬의 UI와 상태를 관리합니다.
/// DeckSelectPopup과 통신하여 선택된 덱을 화면에 표시합니다.
/// </summary>
public class MatchingManager : MonoBehaviour
{
    public static MatchingManager Instance { get; private set; }

    [System.Serializable]
    private class SelectDeckResponse
    {
        public string status;
        public DeckData deck; // DeckData 전체를 받음
    }

    [Header("UI 연결 (로비 화면)")]
    [SerializeField] private Button openDeckSelectButton; // '덱 선택' 텍스트/버튼
    [SerializeField] private TextMeshProUGUI selectedDeckNameText; // '덱 선택' 버튼 안의 텍스트
    [SerializeField] private Image selectedLeaderImage; // '스킨 변경' 버튼 안의 이미지
    [SerializeField] private Button openSkinSelectButton; // '스킨 선택' 텍스트/버튼

    [Header("팝업 참조")]
    [SerializeField] private DeckSelectPopup deckSelectPopup; // 씬에 있는 DeckSelectPopup 스크립트
    [SerializeField] private SkinSelectPopup skinSelectPopup; // 씬에 있는 SkinSelectPopup 스크립트

    // (추가) 로비의 덱 카드 목록 UI
    [Header("덱 카드 목록 UI (로비)")]
    [Tooltip("선택된 메인 덱의 카드 목록이 표시될 스크롤 뷰의 Content")]
    [SerializeField] private Transform deckCardListParent;
    [Tooltip("선택된 사이드 덱의 카드 목록이 표시될 스크롤 뷰의 Content")]
    [SerializeField] private Transform  sideDeckCardListParent;
    [Tooltip("카드 목록에 사용될 프리팹 (LobbyDeckCardDisplay.cs 스크립트 포함)")]
    [SerializeField] private GameObject deckCardPrefab;


    [Header("매치메이킹")]
    [Tooltip("씬에 있는 MatchmakingService 스크립트를 연결")]
    [SerializeField] private MatchmakingService matchmakingService;
    [Tooltip("유저가 누를 '대전 찾기' 버튼")]
    [SerializeField] private Button findMatchButton;
    [Tooltip("유저가 누를 '찾기 취소' 버튼")]
    [SerializeField] private Button cancelMatchButton;

    [Header("매치메이킹 UI")]
    [Tooltip("'대전 찾는 중...' 상태일 때 켤 패널")]
    [SerializeField] private GameObject searchingPanel;
    [Tooltip("대전 찾는 시간")]
    [SerializeField] private TextMeshProUGUI machingTime;
    [Tooltip("기본 로비 화면 (대전 찾기 버튼이 있는)")]
    [SerializeField] private GameObject lobbyPanel;

    [Header("매칭 에러 안내 UI")]
    [Tooltip("매칭 덱 구성 에러 팝업 패널 (machingError)")]
    [SerializeField] private GameObject machingErrorPanel;
    [Tooltip("매칭 에러 메시지 텍스트 (machingError 하위 본문 TextMeshProUGUI)")]
    [SerializeField] private TextMeshProUGUI machingErrorText;
    [Tooltip("에러 팝업 닫기/확인 버튼 (있을 경우)")]
    [SerializeField] private Button machingErrorCloseButton;

    //  DeckSaveManager에서 서버 주소 복사

    private string ApiBaseUrl = GameClient.Instance.BaseUrl;
    private string selectdeckRequestUrl => GameClient.Instance.GetApiUrl("user/select-deck");

    //  서버로 보낼 DTO(데이터 전송 객체)
    [System.Serializable]
    private class SelectDeckRequest
    {
        public string deckId;
    }

    // 내부 변수
    private DeckData currentSelectedDeck; // 현재 유저가 선택한 덱
    private FirebaseFirestore db;
    private FirebaseAuth auth;
    private string currentUserId;
    private Coroutine matchingTimerCoroutine;

    void Awake()
    {
        Instance = this;

        // DeckSelectPopup이 보낼 '덱 확정' 이벤트를 구독(Subscribe)합니다.
        DeckSelectPopup.OnDeckConfirmed += HandlePopupDeckConfirmed;

        // 버튼에 팝업 여는 기능 연결
        if (openDeckSelectButton != null)
        {
            openDeckSelectButton.onClick.AddListener(OnOpenDeckPopup);
        }

        if (openSkinSelectButton != null)
        {
            openSkinSelectButton.onClick.AddListener(OnOpenSkinPopup);
        }

        // Firestore 및 Auth 초기화
        db = FirebaseFirestore.DefaultInstance;
        auth = FirebaseAuth.DefaultInstance;

        // 인증 상태 감지 (DeckSaveManager와 동일한 로직)
        auth.StateChanged += HandleAuthStateChanged;

        // "대전 찾기" 버튼에 래퍼(Wrapper) 함수 연결
        if (findMatchButton != null)
        {
            findMatchButton.onClick.AddListener(OnFindMatchClicked);
        }

        // "찾기 취소" 버튼에 취소 함수 연결
        if (cancelMatchButton != null)
        {
            cancelMatchButton.onClick.AddListener(OnCancelMatchClicked);
        }

        // MatchmakingService의 이벤트들을 구독해서 UI를 변경합니다.
        if (matchmakingService != null)
        {
            matchmakingService.OnMatchmakingStarted += HandleMatchmakingStarted;
            matchmakingService.OnMatchmakingCancelled += HandleMatchmakingCancelled;
            matchmakingService.OnMatchmakingFailed += HandleMatchmakingFailed;
            matchmakingService.OnMatchDeckError += HandleMatchDeckError;
            matchmakingService.OnMatchFound += HandleMatchFound;
        }

        // 시작 시 UI 상태 초기화
        searchingPanel.SetActive(false);
        // lobbyPanel.SetActive(true);

        InitMatchingErrorUI();
        if (machingErrorPanel != null)
        {
            machingErrorPanel.SetActive(false);
        }
    }

    private async void Start()
    {
        // 씬 진입 시 이미 로그인이 되어 있다면 마지막 선택 덱 및 스킨 로드
        if (auth != null && auth.CurrentUser != null)
        {
            currentUserId = auth.CurrentUser.UserId;
            await LoadLastSelectedDeck(currentUserId);
        }
        else if (GameClient.Instance != null && GameClient.Instance.CurrentUser != null)
        {
            string selectDeckId = GameClient.Instance.CurrentUser.selectDeck;
            if (!string.IsNullOrEmpty(selectDeckId) && DeckSaveManager_Firebase.instance != null)
            {
                var myDeck = DeckSaveManager_Firebase.instance.GetAllDecks()?.FirstOrDefault(d => d.deckId == selectDeckId);
                if (myDeck != null)
                {
                    HandleDeckConfirmed(myDeck, false);
                }
            }
        }
    }

    void OnDestroy()
    {
        StopMatchingTimer();

        // 씬이 파괴될 때 이벤트 구독을 해지합니다. (메모리 누수 방지)
        DeckSelectPopup.OnDeckConfirmed -= HandlePopupDeckConfirmed;
        auth.StateChanged -= HandleAuthStateChanged;

        if (matchmakingService != null)
        {
            matchmakingService.OnMatchmakingStarted -= HandleMatchmakingStarted;
            matchmakingService.OnMatchmakingCancelled -= HandleMatchmakingCancelled;
            matchmakingService.OnMatchmakingFailed -= HandleMatchmakingFailed;
            matchmakingService.OnMatchDeckError -= HandleMatchDeckError;
            matchmakingService.OnMatchFound -= HandleMatchFound;
        }
    }

    /// <summary>
    /// '덱 선택' 버튼을 눌러 팝업을 엽니다.
    /// </summary>
    private void OnOpenDeckPopup()
    {
        if (deckSelectPopup != null)
        {
            // (수정) 팝업을 열 때 '현재 선택된 덱' 정보를 전달합니다.
            deckSelectPopup.OpenPopup(currentSelectedDeck);
        }
    }

    /// <summary>
    /// '스킨 변경' 버튼을 눌러 스킨 선택 팝업을엽니다.
    /// </summary>
    private void OnOpenSkinPopup()
    {
        if (currentSelectedDeck == null)
        {
            Debug.LogWarning("[MatchingManager] 스킨을 변경할 덱이 선택되지 않았습니다. 먼저 덱을 선택해주세요.");
            return;
        }

        if (skinSelectPopup != null)
        {
            skinSelectPopup.gameObject.SetActive(true);
            skinSelectPopup.OpenPopup(currentSelectedDeck, OnLeaderSkinChanged);
        }
        else
        {
            Debug.LogWarning("[MatchingManager] SkinSelectPopup 참조가 설정되지 않았습니다.");
        }
    }

    /// <summary>
    /// 스킨 선택 팝업에서 스킨을 선택/확정했을 때 호출됩니다.
    /// </summary>
    private async void OnLeaderSkinChanged(string newSkinId)
    {
        if (currentSelectedDeck == null) return;

        currentSelectedDeck.leaderSkinId = newSkinId;
        Debug.Log($"[MatchingManager] 덱 '{currentSelectedDeck.deckName}' 리더 스킨 변경: {newSkinId}");

        // 1. 로비의 리더 스킨 이미지 즉시 갱신
        UpdateLeaderSkinImage(newSkinId, currentSelectedDeck.deckClass);

        // 2. 변경된 스킨 정보를 서버 DB에 비동기 저장
        if (DeckSaveManager_Firebase.instance != null)
        {
            await DeckSaveManager_Firebase.instance.ServerUpdateDeck(currentSelectedDeck);
            Debug.Log($"[MatchingManager] 서버에 스킨 변경 사항 저장 완료: {newSkinId}");
        }
    }

    /// <summary>
    /// 로비 화면의 리더 스킨 이미지를 갱신합니다.
    /// </summary>
    private void UpdateLeaderSkinImage(string skinId, string deckClass)
    {
        if (selectedLeaderImage == null) return;

        // 1. [최우선] 로컬 Resources/Skins 에셋에서 즉시(0초) 로드하여 지연 없이 반영
        var masterSkin = LeaderCardDisplay.LoadSkinData(skinId, deckClass);
        if (masterSkin != null && masterSkin.skinSprite != null)
        {
            selectedLeaderImage.sprite = masterSkin.skinSprite;
            return;
        }

        // 2. 로컬 에셋에 없는 경우 비동기 네트워크 이미지 로드
        StartCoroutine(UpdateLeaderSkinImageCoroutine(skinId, deckClass));
    }

    private IEnumerator UpdateLeaderSkinImageCoroutine(string skinId, string deckClass)
    {
        // 1. 스킨 도감 데이터가 비어있다면 로드 대기
        if (GameClient.Instance != null && (GameClient.Instance.AllLeaderSkins == null || GameClient.Instance.AllLeaderSkins.Count == 0))
        {
            yield return GameClient.Instance.GetLeaderSkinsAsync(null);
        }

        // 2. GameClient에서 해당 스킨의 image_url 찾기
        string imageUrl = null;
        if (GameClient.Instance != null && GameClient.Instance.AllLeaderSkins != null && !string.IsNullOrEmpty(skinId))
        {
            var skinData = GameClient.Instance.AllLeaderSkins.FirstOrDefault(s => s.productId == skinId);
            if (skinData != null && !string.IsNullOrEmpty(skinData.image_url))
            {
                imageUrl = skinData.image_url;
            }
        }

        // 3. 못 찾은 경우 기본 경로 fallback
        if (string.IsNullOrEmpty(imageUrl))
        {
            string fallbackClass = !string.IsNullOrEmpty(deckClass) ? deckClass : "Gangzi";
            if (!string.IsNullOrEmpty(skinId))
            {
                if (skinId.IndexOf("Yuni", StringComparison.OrdinalIgnoreCase) >= 0) fallbackClass = "Yuni";
                else if (skinId.IndexOf("Huya", StringComparison.OrdinalIgnoreCase) >= 0) fallbackClass = "Huya";
                else if (skinId.IndexOf("Gangzi", StringComparison.OrdinalIgnoreCase) >= 0) fallbackClass = "Gangzi";
            }
            imageUrl = $"Items/Skins/{fallbackClass}_Skin_0001.png";
        }

        // 4. 로비 Image에 로드
        if (selectedLeaderImage != null)
        {
            ShopManager.LoadProductImage(imageUrl, selectedLeaderImage);
        }
    }

    /// <summary>
    /// 로그인 상태가 변경되면 호출됩니다.
    /// </summary>
    private async void HandleAuthStateChanged(object sender, System.EventArgs e)
    {
        if (auth.CurrentUser != null)
        {
            currentUserId = auth.CurrentUser.UserId;
            await LoadLastSelectedDeck(currentUserId);
        }
        else
        {
            currentUserId = null;
            selectedDeckNameText.text = "덱 선택";
            currentSelectedDeck = null;
            UpdateDeckCardList(null); // 로그아웃 시 목록 비우기
        }
    }

    /// <summary>
    /// (수정) 서버 API를 통해 마지막으로 선택한 덱의 '전체 데이터'를 불러와 UI에 적용합니다.
    /// </summary>
    private async Task LoadLastSelectedDeck(string userId)
    {
        if (string.IsNullOrEmpty(userId) || auth.CurrentUser == null) return;

        try
        {
            string idToken = await auth.CurrentUser.TokenAsync(true);

            using (UnityWebRequest request = UnityWebRequest.Get(selectdeckRequestUrl))
            {
                request.SetRequestHeader("Authorization", "Bearer " + idToken);

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    // JSON 파싱
                    SelectDeckResponse response = JsonUtility.FromJson<SelectDeckResponse>(jsonResponse);

                    if (response != null && response.deck != null && !string.IsNullOrEmpty(response.deck.deckId))
                    {
                        DeckData lastSelectedDeck = response.deck;

                        Debug.Log($"서버에서 불러온 마지막 덱: '{lastSelectedDeck.deckName}'");

                        // (핵심) 받아온 덱 데이터를 바로 UI에 적용합니다.
                        // 서버 저장(saveToServer)은 false로 설정합니다 (이미 서버에서 가져온 거니까요).
                        HandleDeckConfirmed(lastSelectedDeck, false);
                    }
                    else
                    {
                        Debug.Log("서버에 저장된 선택 덱이 없거나 유효하지 않습니다.");
                    }
                }
                else
                {
                    Debug.LogError($"마지막 덱 불러오기 실패: {request.error}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"마지막 선택 덱 로드 중 오류: {e.Message}");
        }
    }


    /// <summary>
    ///  DeckSelectPopup의 'Action<DeckData>' 시그니처와
    /// HandleDeckConfirmed(DeckData, bool) 시그니처를 연결해주는 '어댑터' 함수입니다.
    /// </summary>
    private void HandlePopupDeckConfirmed(DeckData selectedDeck)
    {
        // 팝업에서 덱이 확정되면, 항상 서버에 저장해야 하므로 'true'를 붙여 호출합니다.
        HandleDeckConfirmed(selectedDeck, true);
    }

    /// <summary>
    /// 덱 확정 시의 '실제' 로직.
    /// 서버 저장 여부를 결정하는 파라미터 추가
    /// </summary>
    private async void HandleDeckConfirmed(DeckData selectedDeck, bool saveToServer = true)
    {
        currentSelectedDeck = selectedDeck;
        selectedDeckNameText.text = selectedDeck.deckName;

        // 로비의 리더 스킨 이미지 갱신
        UpdateLeaderSkinImage(selectedDeck.GetEquippedSkinId(), selectedDeck.deckClass);

        // 덱 카드 목록 갱신
        UpdateDeckCardList(selectedDeck);

        if (saveToServer)
        {
            await SaveSelectedDeckToServer(selectedDeck.deckId);
        }
    }

    /// <summary>
    /// 로비의 카드 목록 UI를 선택된 덱의 카드로 채웁니다.
    /// </summary>
    private void UpdateDeckCardList(DeckData deck)
    {
        // 1. 기존 UI 삭제
        if (deckCardListParent == null || sideDeckCardListParent == null || deckCardPrefab == null) return;
        foreach (Transform child in deckCardListParent) Destroy(child.gameObject);
        foreach (Transform child in sideDeckCardListParent) Destroy(child.gameObject);

        // 2. 덱 검사
        if (deck == null || deck.cardIds == null || deck.cardIds.Count == 0) return;

        // [핵심 변경] ResourceManager 사용
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("ResourceManager가 없습니다! 로비 씬에 ResourceManager가 있는지 확인하세요.");
            return;
        }

        // 3. 덱의 카드 ID들을 순회하며 실제 CardData(ScriptableObject) 찾기
        List<CardData> cardsInDeck = new List<CardData>();
        foreach (string cardId in deck.cardIds)
        {
            // ResourceManager에서 ID로 카드 데이터를 '즉시' 가져옵니다.
            CardData card = ResourceManager.Instance.GetCardData(cardId);
            if (card != null)
            {
                cardsInDeck.Add(card);
            }
            else
            {
                Debug.LogWarning($"ResourceManager에서 ID가 '{cardId}'인 카드를 찾을 수 없습니다.");
            }
        }

        // 4. 덱의 카드 ID들을 순회하며 실제 CardData(ScriptableObject) 찾기 사이드 덱 전용
        List<CardData> sideCardsInDeck = new List<CardData>();
        foreach (string cardId in deck.sideDeckCardIds)
        {
            // ResourceManager에서 ID로 카드 데이터를 '즉시' 가져옵니다.
            CardData card = ResourceManager.Instance.GetCardData(cardId);
            if (card != null)
            {
                sideCardsInDeck.Add(card);
            }
            else
            {
                Debug.LogWarning($"ResourceManager에서 ID가 '{cardId}'인 카드를 찾을 수 없습니다.");
            }
        }

        // 5. 메인 덱 정렬 및 그룹화 (CardData 기준)
        var groupedAndSortedDeck = cardsInDeck
            .GroupBy(card => card.cardID) // ID 기준 그룹화
            .Select(group => new
            {
                Card = group.First(),
                Count = group.Count()
            })
            .OrderBy(item => item.Card.manaCost) // cost -> manaCost
            .ThenBy(item => item.Card.cardName); // name -> cardName

        // 6. 사이드 덱 정렬 및 그룹화 (CardData 기준)
        var sideGroupedAndSortedDeck = sideCardsInDeck
            .OrderBy(card => card.manaCost) // cost -> manaCost
            .ThenBy(card => card.cardName); // name -> cardName

        // 7. 메인 덱 리스트 UI 생성
        foreach (var item in groupedAndSortedDeck)
        {
            GameObject newDeckCardUI = Instantiate(deckCardPrefab, deckCardListParent);
            LobbyDeckCardDisplay itemDisplay = newDeckCardUI.GetComponent<LobbyDeckCardDisplay>();

            if (itemDisplay != null)
            {
                // (수정) CardData 객체를 그대로 전달
                itemDisplay.Setup(item.Card, 1);
            }
        }

        // 8. 사이드 덱 리스트 UI 생성
        foreach (var item in sideGroupedAndSortedDeck)
        {
            GameObject newDeckCardUI = Instantiate(deckCardPrefab, sideDeckCardListParent);
            LobbyDeckCardDisplay itemDisplay = newDeckCardUI.GetComponent<LobbyDeckCardDisplay>();

            if (itemDisplay != null)
            {
                // (수정) CardData 객체를 그대로 전달
                itemDisplay.Setup(item, 1);
            }
        }
    }

    /// <summary>
    /// 유저가 선택한 덱 ID를 '서버'에 저장합니다.
    /// </summary>
    private async Task SaveSelectedDeckToServer(string deckId)
    {
        if (string.IsNullOrEmpty(currentUserId) || auth.CurrentUser == null)
        {
            Debug.LogError("로그인한 유저가 없어 선택한 덱을 저장할 수 없습니다.");
            return;
        }

        try
        {
            // 1. Firebase 인증 토큰 가져오기
            string idToken = await auth.CurrentUser.TokenAsync(true);

            // 3. 서버로 보낼 JSON 본문 생성
            SelectDeckRequest requestBody = new SelectDeckRequest { deckId = deckId };
            string jsonBody = JsonUtility.ToJson(requestBody);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            // 4. UnityWebRequest 생성 (PUT 방식)
            using (UnityWebRequest request = new UnityWebRequest(selectdeckRequestUrl, "PUT"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                // 5. 헤더 설정 (인증 토큰 + Content-Type)
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + idToken);

                Debug.Log($"선택 덱 저장 API 호출: {selectdeckRequestUrl} (DeckID: {deckId})");

                // 6. API 요청 전송
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                // 7. 결과 처리
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"선택 덱 저장 실패: {request.error} - {request.downloadHandler.text}");
                }
                else
                {
                    Debug.Log($"선택 덱 저장 성공: {request.downloadHandler.text}");
                    // (선택) 여기서 currentSelectedDeck = selectedDeck; 를 확정하거나
                    // 로비 UI에 "저장 완료" 같은 피드백을 줄 수 있습니다.
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"선택 덱 저장 중 예외 발생: {e.Message}");
        }
    }

    /// <summary>
    /// "대전 찾기" 버튼 클릭 시 호출될 '래퍼' 함수입니다.
    /// </summary>
    private void OnFindMatchClicked()
    {
        // 1. (중요) 덱을 선택했는지 먼저 검사합니다.
        if (currentSelectedDeck == null)
        {
            Debug.LogWarning("매칭을 시작하기 전에 덱을 선택해야 합니다.");
            // TODO: 유저에게 "덱을 선택하세요"라는 알림 UI를 띄워주세요.
            return;
        }

        // 2. 덱이 선택되었다면, 서비스를 호출합니다.
        // 'currentSelectedDeck' 변수 (LoadLastSelectedDeck 등에서 이미 설정됨)를 넘겨줍니다.
        matchmakingService.StartMatchmaking(currentSelectedDeck);
    }

    /// <summary>
    /// "찾기 취소" 버튼 클릭 시 호출될 함수입니다.
    /// </summary>
    private void OnCancelMatchClicked()
    {
        matchmakingService.CancelMatchmaking();
    }

    // ----- MatchmakingService 이벤트 핸들러 -----

    /// <summary>
    /// 서비스가 "대전 찾기 시작됨" 이벤트를 보냈을 때 호출됩니다.
    /// </summary>
    private void HandleMatchmakingStarted()
    {
        Debug.Log("UI: 대전 찾기 시작...");
        StartMatchingTimer();

        UIPanelToggler uIPanelToggler = searchingPanel.GetComponent<UIPanelToggler>();
        if (uIPanelToggler != null)
        {
            uIPanelToggler.ShowPanel();
        }
    }

    /// <summary>
    /// 서비스가 "취소됨" 이벤트를 보냈을 때 호출됩니다.
    /// </summary>
    private void HandleMatchmakingCancelled()
    {
        Debug.Log("UI: 대전 찾기 취소됨.");
        StopMatchingTimer();

        UIPanelToggler uIPanelToggler = searchingPanel.GetComponent<UIPanelToggler>();
        if (uIPanelToggler != null)
        {
            uIPanelToggler.HidePanel();
        }
    }

    /// <summary>
    /// 서비스가 "실패" 이벤트를 보냈을 때 호출됩니다.
    /// </summary>
    private void HandleMatchmakingFailed(string errorMessage)
    {
        Debug.LogError($"UI: 매치메이킹 실패: {errorMessage}");
        StopMatchingTimer();

        UIPanelToggler uIPanelToggler = searchingPanel.GetComponent<UIPanelToggler>();
        if (uIPanelToggler != null)
        {
            uIPanelToggler.HidePanel();
        }

        // machingError 패널이 아직 안 떴다면 기본 실패 메시지 표시
        if (machingErrorPanel == null || !machingErrorPanel.activeSelf)
        {
            ShowMatchingError(errorMessage);
        }
    }

    /// <summary>
    /// 덱 구성 검증 실패 전용 에러 패킷(MatchDeckErrorResponse)을 수신했을 때 호출됩니다.
    /// </summary>
    private void HandleMatchDeckError(MatchmakingService.MatchDeckErrorResponse error)
    {
        if (error == null) return;

        Debug.LogWarning($"[MatchingManager] ⚠️ 덱 구성 검증 실패 수신: {error.errorCode} - {error.message}");
        StopMatchingTimer();

        if (searchingPanel != null)
        {
            UIPanelToggler uIPanelToggler = searchingPanel.GetComponent<UIPanelToggler>();
            if (uIPanelToggler != null) uIPanelToggler.HidePanel();
            else searchingPanel.SetActive(false);
        }

        ShowMatchingError(error.message);
    }

    /// <summary>
    /// 매칭 에러 안내 팝업(machingError)을 띄우고 오류 메시지를 표시합니다.
    /// </summary>
    public void ShowMatchingError(string message)
    {
        InitMatchingErrorUI();

        if (machingErrorText != null)
        {
            machingErrorText.text = !string.IsNullOrEmpty(message) ? message : "매칭 중 오류가 발생했습니다.";
        }

        if (machingErrorPanel != null)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenPopup(machingErrorPanel);
            }
            else
            {
                machingErrorPanel.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 매칭 에러 안내 팝업을 닫습니다.
    /// </summary>
    public void CloseMatchingError()
    {
        if (machingErrorPanel != null)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.CloseSpecificPopup(machingErrorPanel);
            }
            else
            {
                machingErrorPanel.SetActive(false);
            }
        }
    }

    /// <summary>
    /// machingError 오브젝트 및 하위 본문 텍스트 컴포넌트를 초기화하고 클릭 닫기 이벤트를 연결합니다.
    /// </summary>
    private void InitMatchingErrorUI()
    {
        if (machingErrorPanel == null)
        {
            var go = GameObject.Find("machingError");
            if (go != null)
            {
                machingErrorPanel = go;
            }
            else
            {
                var bg = GameObject.Find("BackGround");
                if (bg != null)
                {
                    var child = bg.transform.Find("machingError");
                    if (child != null) machingErrorPanel = child.gameObject;
                }
            }
        }

        if (machingErrorPanel != null)
        {
            if (machingErrorText == null)
            {
                var texts = machingErrorPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
                // 첫 번째는 제목("경고"), 두 번째가 본문 내용 텍스트
                if (texts != null && texts.Length > 1)
                {
                    machingErrorText = texts[1];
                }
                else if (texts != null && texts.Length > 0)
                {
                    machingErrorText = texts[0];
                }
            }

            // 닫기 버튼이 명시되지 않았으면 패널 자체를 클릭하여 닫을 수 있도록 Button 부착
            if (machingErrorCloseButton != null)
            {
                machingErrorCloseButton.onClick.RemoveListener(CloseMatchingError);
                machingErrorCloseButton.onClick.AddListener(CloseMatchingError);
            }
            else
            {
                Button panelBtn = machingErrorPanel.GetComponent<Button>();
                if (panelBtn == null)
                {
                    panelBtn = machingErrorPanel.AddComponent<Button>();
                }
                panelBtn.onClick.RemoveListener(CloseMatchingError);
                panelBtn.onClick.AddListener(CloseMatchingError);
            }
        }
    }

    /// <summary>
    /// 서비스가 "매칭 성공!" 이벤트를 보냈을 때 호출됩니다.
    /// </summary>
    private void HandleMatchFound(string gameId, string opponentUid)
    {
        StopMatchingTimer();

        UIPanelToggler uIPanelToggler = searchingPanel.GetComponent<UIPanelToggler>();
        if (uIPanelToggler != null)
        {
            uIPanelToggler.HidePanel();
        }
    }

    // ----- 매칭 타이머 (00:00) 기능 -----

    /// <summary>
    /// 매칭 시작 시 타이머를 0초부터 시작
    /// </summary>
    private void StartMatchingTimer()
    {
        StopMatchingTimer();
        matchingTimerCoroutine = StartCoroutine(MatchingTimerCoroutine());
    }

    /// <summary>
    /// 매칭 취소/완료 시 타이머 정지
    /// </summary>
    private void StopMatchingTimer()
    {
        if (matchingTimerCoroutine != null)
        {
            StopCoroutine(matchingTimerCoroutine);
            matchingTimerCoroutine = null;
        }
    }

    /// <summary>
    /// 매초 경과 시간을 00:00 포맷으로 machingTime 텍스트에 갱신
    /// </summary>
    private System.Collections.IEnumerator MatchingTimerCoroutine()
    {
        int totalSeconds = 0;

        // 시작 즉시 00:00 표시
        if (machingTime != null)
        {
            machingTime.text = "00:00";
        }

        while (true)
        {
            yield return YieldInstructionCache.WaitForSeconds(1f);
            totalSeconds++;

            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            if (machingTime != null)
            {
                machingTime.text = string.Format("{0:D2}:{1:D2}", minutes, seconds);
            }
        }
    }
}
