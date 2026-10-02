using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq; // 데이터를 필터링(검색)할 때 아주 강력한 도구(LINQ)를 사용합니다.
using System;

/// <summary>
/// 덱 빌더(Deck Builder) 화면의 총감독입니다.
/// 내가 가진 카드를 보여주고, 직업/코스트/검색어에 따라 카드를 걸러서(필터링) 보여줍니다.
/// </summary>
public class DeckBuilder : MonoBehaviour
{
    public static DeckBuilder Instance { get; private set; }

    [Header("UI & Prefab Settings")]
    // 카드를 화면에 찍어낼 때 사용할 원본 틀 (붕어빵 틀 같은 프리팹)
    public GameObject cardPrefab;
    // 생성된 카드가 들어갈 부모 UI (Scroll View의 Content 부분)
    public Transform cardListParent;

    [Header("Component References")]
    // 필터 버튼들을 관리하는 매니저와 연결합니다.
    public FilterManager filterManager;

    // 게임의 모든 카드 데이터를 저장해두는 원본 리스트입니다.
    private List<CardData> allCardsList;

    // ---------------------------------------------------------
    // 현재 어떤 필터가 적용되어 있는지 기억하는 변수들
    // ---------------------------------------------------------

    // 1. 메인 직업 (예: 마법사를 선택하면 마법사 카드 + 중립 카드만 보여야 함)
    // '?'는 값이 없을 수도 있다(null 가능)는 뜻입니다.
    private CardClass? currentClassFilter = null;

    // 2. 상세 필터 (전설 카드만 보기, 짝수 비용만 보기 등)
    private FilterManager.FilterSettings currentDetailFilters;

    // 3. 비용(마나) 필터 (-1이면 필터 안 함, 0~10이면 그 비용만 봄)
    private int currentCostFilter = -1;

    // 4. 검색어 (검색창에 입력한 글자)
    private string currentSearchText = "";

    [Header("Cost Filter UI")]
    [Tooltip("마나 코스트 필터 토글/버튼들이 위치한 부모 (Manafilter)")]
    public Transform manaFilterParent;

    // 코스트 토글/버튼들과 인덱스 매핑 (0~10)
    private readonly List<(Toggle toggle, Button button, Image image, TextMeshProUGUI text, int cost)> _costFilterEntries = new List<(Toggle, Button, Image, TextMeshProUGUI, int)>();

    // 코스트 토글 색상 스타일 (PastelUIFree 테마)
    private static readonly Color CostBtnNormalBg = Color.white;                             // 원본 PastelUIFree 보라 버튼 스프라이트
    private static readonly Color CostBtnActiveBg = new Color(1f, 0.70f, 0.85f, 1f);        // 화사한 파스텔 핑크 하이라이트
    private static readonly Color CostBtnNormalText = new Color(0.30f, 0.18f, 0.45f, 1f);   // 짙은 보라 텍스트
    private static readonly Color CostBtnActiveText = new Color(0.48f, 0.10f, 0.28f, 1f);   // 진한 핑크 텍스트 (볼드)

    [Header("Crafting Mode (제작 모드)")]
    [Tooltip("제작 모드 토글 버튼 (/Canvas/Deck/Tool/create)")]
    public Button craftModeButton;
    public TextMeshProUGUI craftModeButtonText;
    public Image craftModeButtonImage;
    public bool isCraftingMode = false;

    private static readonly Color CraftBtnNormalBg = new Color(1f, 0.95f, 0.82f, 1f);       // 파스텔 웜 옐로우
    private static readonly Color CraftBtnActiveBg = new Color(1f, 0.75f, 0.85f, 1f);       // 파스텔 핑크
    private static readonly Color CraftBtnNormalText = new Color(0.42f, 0.28f, 0.10f, 1f); // 짙은 웜 브라운
    private static readonly Color CraftBtnActiveText = new Color(0.48f, 0.10f, 0.28f, 1f); // 진한 핑크

    // [최적화] 카드 UI 오브젝트 재활용 풀 (Destroy/Instantiate 반복 방지)
    private readonly List<DeckCardDisplay> _cardDisplayPool = new List<DeckCardDisplay>();

    void Awake()
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

        // 마나 코스트 필터 토글(0~10+) 초기화 및 리스너 연결
        InitCostFilterButtons();
        // 제작 모드 버튼 초기화
        InitCraftModeButton();
    }

    // 이 오브젝트가 켜질 때(활성화) 실행됩니다.
    private void OnEnable()
    {
        // "필터가 적용됐다"는 신호(이벤트)가 오면 HandleDetailFilterApply 함수를 실행하라고 연결(구독)합니다.
        FilterManager.OnFilterApplied += HandleDetailFilterApply;
        DeckManager.OnCurrentDeckCardsChanged += UpdateAllCardCounts;
    }

    // 이 오브젝트가 꺼질 때(비활성화) 실행됩니다.
    private void OnDisable()
    {
        // 연결했던 신호를 끊습니다. (안 끊으면 에러가 나거나 메모리가 낭비됩니다)
        FilterManager.OnFilterApplied -= HandleDetailFilterApply;
        DeckManager.OnCurrentDeckCardsChanged -= UpdateAllCardCounts;
    }

    // 게임 시작 시 딱 한 번 실행됩니다.
    void Start()
    {
        // 마나 코스트 필터 버튼(0~10+) 초기화 및 클릭 리스너 연결
        InitCostFilterButtons();

        // 리소스 매니저에서 모든 카드 데이터를 가져옵니다.
        allCardsList = ResourceManager.Instance.GetAllCards();

        // 카드가 있으면 초기 필터 규칙(토큰 제외 등)에 맞춰 화면에 표시합니다.
        if (allCardsList != null && allCardsList.Count > 0)
        {
            UpdateCardDisplay();
        }
    }


    /// <summary>
    /// [핵심 기능] 현재 설정된 모든 조건(직업, 비용, 검색어 등)을 종합해서
    /// 조건에 맞는 카드만 쏙쏙 골라내고 화면을 다시 그립니다.
    /// </summary>
    private void UpdateCardDisplay()
    {
        // 카드 데이터가 없으면 일할 필요 없음
        if (allCardsList == null) return;

        // LINQ의 시작: 전체 리스트를 '검색 가능한 상태'로 둡니다.
        IEnumerable<CardData> filteredResult = allCardsList;

        // 0. 토큰 카드 제외 (덱 편성 불가 카드)
        filteredResult = filteredResult.Where(card => !card.isToken);

        // 0.5 보유/미보유 필터링
        if (!isCraftingMode)
        {
            // 기본 모드: 계정이 보유한 카드만 표시 (보유 수량 > 0)
            filteredResult = filteredResult.Where(card => DeckManager.instance == null || DeckManager.instance.GetOwnedCardCount(card) > 0);
        }
        // 제작 모드(isCraftingMode == true): 보유 카드와 미보유(제작 가능) 카드가 모두 함께 표시됨

        // 1. 직업 필터 적용
        if (currentClassFilter.HasValue)
        {
            // "내 직업이거나" 또는 "중립(강지)" 카드만 남깁니다.
            // .Where는 조건에 맞는 녀석만 통과시키는 거름망 역할을 합니다.
            filteredResult = filteredResult.Where(card =>
                card.cardClass == currentClassFilter.Value ||
                card.cardClass == CardClass.Gangzi);
        }

        // 2. 상세 필터 적용 (필터 매니저 설정값)

        // 직업 전용 필터가 있다면 적용
        if (currentDetailFilters.cardClass.HasValue)
        {
            filteredResult = filteredResult.Where(card => card.cardClass == currentDetailFilters.cardClass.Value);
        }

        // 카드 종류(하수인/주문) 필터가 있다면 적용
        if (currentDetailFilters.CardType.HasValue)
        {
            filteredResult = filteredResult.Where(card => card.cardType == currentDetailFilters.CardType.Value);
        }

        // 희귀도(일반/전설) 필터가 있다면 적용
        if (currentDetailFilters.Rarity.HasValue)
        {
            filteredResult = filteredResult.Where(card => card.rarity == currentDetailFilters.Rarity.Value);
        }

        // 확장팩 필터가 있다면 적용
        if (currentDetailFilters.Expansion.HasValue)
        {
            filteredResult = filteredResult.Where(card => card.expansion == currentDetailFilters.Expansion.Value);
        }

        // 3. 마나 코스트 필터
        if (currentCostFilter != -1) // -1이 아니면 필터가 켜진 것
        {
            if (currentCostFilter >= 10)
                // 10 이상 버튼을 눌렀으면 10보다 크거나 같은 애들 다 보여줌
                filteredResult = filteredResult.Where(card => card.manaCost >= currentCostFilter);
            else
                // 그게 아니면 정확히 그 코스트인 애들만 보여줌
                filteredResult = filteredResult.Where(card => card.manaCost == currentCostFilter);
        }

        // 4. 검색어 필터 (무할당 대소문자 무시 비교)
        if (!string.IsNullOrWhiteSpace(currentSearchText))
        {
            string trimmedSearch = currentSearchText.Trim();
            filteredResult = filteredResult.Where(card =>
                (card.cardName != null && card.cardName.IndexOf(trimmedSearch, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (card.description != null && card.description.IndexOf(trimmedSearch, StringComparison.OrdinalIgnoreCase) >= 0)
            );
        }

        // 5. 정렬: 선택된 정렬 기준(7가지) 및 방식(오름차순/내림차순) 적용
        bool isAscending = currentDetailFilters.sortOrder == CardSortOrder.Ascending;

        switch (currentDetailFilters.sortCriterion)
        {
            case CardSortCriterion.Cost:
                filteredResult = isAscending
                    ? filteredResult.OrderBy(card => card.manaCost).ThenBy(card => card.cardID)
                    : filteredResult.OrderByDescending(card => card.manaCost).ThenBy(card => card.cardID);
                break;

            case CardSortCriterion.Class:
                filteredResult = isAscending
                    ? filteredResult.OrderBy(card => (int)card.cardClass).ThenBy(card => card.manaCost).ThenBy(card => card.cardID)
                    : filteredResult.OrderByDescending(card => (int)card.cardClass).ThenBy(card => card.manaCost).ThenBy(card => card.cardID);
                break;

            case CardSortCriterion.Attack:
                filteredResult = isAscending
                    ? filteredResult.OrderBy(card => card.attack).ThenBy(card => card.manaCost).ThenBy(card => card.cardID)
                    : filteredResult.OrderByDescending(card => card.attack).ThenBy(card => card.manaCost).ThenBy(card => card.cardID);
                break;

            case CardSortCriterion.Health:
                filteredResult = isAscending
                    ? filteredResult.OrderBy(card => card.health).ThenBy(card => card.manaCost).ThenBy(card => card.cardID)
                    : filteredResult.OrderByDescending(card => card.health).ThenBy(card => card.manaCost).ThenBy(card => card.cardID);
                break;

            case CardSortCriterion.Rarity:
                filteredResult = isAscending
                    ? filteredResult.OrderBy(card => (int)card.rarity).ThenBy(card => card.manaCost).ThenBy(card => card.cardID)
                    : filteredResult.OrderByDescending(card => (int)card.rarity).ThenBy(card => card.manaCost).ThenBy(card => card.cardID);
                break;

            case CardSortCriterion.Tribe:
                filteredResult = isAscending
                    ? filteredResult.OrderBy(card => (int)card.minionTribe).ThenBy(card => card.manaCost).ThenBy(card => card.cardID)
                    : filteredResult.OrderByDescending(card => (int)card.minionTribe).ThenBy(card => card.manaCost).ThenBy(card => card.cardID);
                break;

            case CardSortCriterion.Type:
                filteredResult = isAscending
                    ? filteredResult.OrderBy(card => (int)card.cardType).ThenBy(card => card.manaCost).ThenBy(card => card.cardID)
                    : filteredResult.OrderByDescending(card => (int)card.cardType).ThenBy(card => card.manaCost).ThenBy(card => card.cardID);
                break;

            default:
                filteredResult = filteredResult
                    .OrderBy(card => card.manaCost)
                    .ThenBy(card => card.cardID);
                break;
        }

        // 필터링과 정렬을 거친 최종 결과를 리스트로 변환하여 화면에 전달합니다.
        DisplayCards(filteredResult.ToList());
    }

    /// <summary>
    /// 모든 필터를 싹 지우고 초기화하는 버튼용 함수입니다.
    /// </summary>
    public void ResetAllFilters()
    {
        currentClassFilter = null;
        currentDetailFilters = new FilterManager.FilterSettings(); // 새 설정(빈 값)으로 덮어쓰기
        currentCostFilter = -1;
        currentSearchText = "";

        UpdateCostButtonVisuals();
        // 초기화됐으니 화면도 다시 그림
        UpdateCardDisplay();
    }

    /// <summary>
    /// [새 덱 만들기] 직업을 선택했을 때 실행됩니다.
    /// (서버에 미리 빈 덱을 생성하지 않고, 유저가 '저장' 버튼을 누를 때만 서버에 등록됩니다)
    /// </summary>
    public void SetClassFilter(string className)
    {
        // 1. 임시(Draft) 새 덱 데이터 생성
        DeckData draftDeck = new DeckData("", "새로운 덱", className);

        // 2. 덱 매니저(오른쪽 리스트 관리자)에게 "이제 이 새 덱을 편집할 거야"라고 알려줍니다.
        DeckManager.instance.StartNewDeck(draftDeck);

        // 3. 필터들을 깨끗하게 청소합니다.
        currentDetailFilters = new FilterManager.FilterSettings();
        currentCostFilter = -1;
        currentSearchText = "";
        UpdateCostButtonVisuals();

        // 4. 문자열로 된 직업 이름(예: "Mage")을 컴퓨터가 이해하는 Enum(ClassType.Mage)으로 바꿉니다.
        if (Enum.TryParse(className, out CardClass classEnum))
        {
            currentClassFilter = classEnum;
        }
        else
        {
            // 변환 실패하면 기본값(강지/중립)으로 설정
            currentClassFilter = CardClass.Gangzi;
        }

        // 5. 필터 UI(책갈피 탭)도 해당 직업만 보이게 갱신합니다.
        if (filterManager != null)
        {
            filterManager.ResetFilterUI();
            var availableMembers = new List<string> { className, CardClass.Gangzi.ToString() };
            filterManager.UpdateMemberToggles(availableMembers);
        }

        // 6. 설정 끝났으니 화면 갱신!
        UpdateCardDisplay();
    }

    /// <summary>
    /// [기존 덱 수정] 저장된 덱을 불러와서 편집 모드로 들어갑니다.
    /// </summary>
    public void LoadDeckForEditing(DeckData deckToLoad)
    {
        // 덱에는 카드 ID(문자열)만 들어있으므로, 실제 카드 데이터(객체)로 바꿔주는 작업입니다.
        // 메인덱
        List<CardData> mainCardsForDeck = new List<CardData>();
        foreach (string cardId in deckToLoad.cardIds)
        {
            CardData card = ResourceManager.Instance.GetCardData(cardId);
            if (card != null) mainCardsForDeck.Add(card);
        }
        // 사이드 덱
        List<CardData> sideCardsForDeck = new List<CardData>();
        foreach (string cardId in deckToLoad.sideDeckCardIds)
        {
            CardData card = ResourceManager.Instance.GetCardData(cardId);
            if (card != null) sideCardsForDeck.Add(card);
        }

        // 덱 매니저에게 "이 덱 내용으로 채워넣어"라고 시킵니다.
        DeckManager.instance.LoadDeck(deckToLoad, mainCardsForDeck, sideCardsForDeck);

        // 덱의 직업에 맞춰서 필터를 설정합니다.
        SetClassFilterForEditing(deckToLoad.deckClass);
    }

    // 덱 수정 시 필터와 UI를 설정하는 내부 도우미 함수
    private void SetClassFilterForEditing(string className)
    {
        currentDetailFilters = new FilterManager.FilterSettings();
        currentCostFilter = -1;
        currentSearchText = "";
        UpdateCostButtonVisuals();

        if (Enum.TryParse(className, out CardClass classEnum))
        {
            currentClassFilter = classEnum;
        }
        else
        {
            currentClassFilter = null;
        }

        if (filterManager != null)
        {
            filterManager.ResetFilterUI();
            var availableMembers = new List<string> { className, CardClass.Gangzi.ToString() };
            filterManager.UpdateMemberToggles(availableMembers);
        }
        UpdateCardDisplay();
    }

    /// <summary>
    /// 마나 필터(0~10+ 코스트) 토글/버튼들을 검색하여 이벤트 및 파스텔 테마 비주얼을 초기화합니다.
    /// </summary>
    public void InitCostFilterButtons()
    {
        if (manaFilterParent == null)
        {
            var go = GameObject.Find("Manafilter");
            if (go != null) manaFilterParent = go.transform;
        }

        if (manaFilterParent == null) return;

        // ToggleGroup이 켜져 있으면 토글 전환 시 상호 간섭 및 재진입 취소 버그가 발생하므로 비활성화합니다.
        var tg = manaFilterParent.GetComponent<ToggleGroup>();
        if (tg != null)
        {
            tg.enabled = false;
        }

        _costFilterEntries.Clear();

        for (int i = 0; i < manaFilterParent.childCount; i++)
        {
            var child = manaFilterParent.GetChild(i);
            var toggle = child.GetComponent<Toggle>();
            var btn = child.GetComponent<Button>();
            if (toggle == null && btn == null) continue;

            // 이름 또는 텍스트에서 코스트 파싱 (0cost ~ 10+cost)
            int cost = i;
            string childName = child.name.ToLower();
            if (childName.Contains("10"))
            {
                cost = 10;
            }
            else
            {
                var match = System.Text.RegularExpressions.Regex.Match(childName, @"\d+");
                if (match.Success)
                {
                    int.TryParse(match.Value, out cost);
                }
            }

            var img = child.GetComponent<Image>();
            var txt = child.GetComponentInChildren<TextMeshProUGUI>(true);

            int capturedCost = cost;
            if (toggle != null)
            {
                toggle.group = null; // ToggleGroup 연결 해제 (개별 제어)
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener((isOn) => OnCostToggleChanged(capturedCost, isOn));
            }
            else if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnCostButtonClick(capturedCost));
            }

            _costFilterEntries.Add((toggle, btn, img, txt, capturedCost));
        }

        UpdateCostButtonVisuals();
    }

    /// <summary>
    /// 현재 선택된 코스트 필터(currentCostFilter)에 맞춰 토글 상태 및 배경색, 텍스트를 하이라이트합니다.
    /// </summary>
    public void UpdateCostButtonVisuals()
    {
        foreach (var entry in _costFilterEntries)
        {
            bool isActive = (currentCostFilter != -1 && (
                (entry.cost < 10 && entry.cost == currentCostFilter) ||
                (entry.cost >= 10 && currentCostFilter >= 10)
            ));

            if (entry.toggle != null && entry.toggle.isOn != isActive)
            {
                entry.toggle.SetIsOnWithoutNotify(isActive);
            }

            if (entry.image != null)
            {
                entry.image.color = isActive ? CostBtnActiveBg : CostBtnNormalBg;
            }

            if (entry.text != null)
            {
                entry.text.color = isActive ? CostBtnActiveText : CostBtnNormalText;
                entry.text.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }

    /// <summary>
    /// 마나 코스트 토글 상태가 변경되었을 때 실행됩니다.
    /// - 다른 코스트를 누르면: 해당 코스트로 바로 전환 (기존 코스트 해제, 새 코스트 켜짐)
    /// - 이미 활성화된 코스트를 다시 누르면: 해당 코스트 해제 (전체 보기)
    /// </summary>
    public void OnCostToggleChanged(int cost, bool isOn)
    {
        if (isOn)
        {
            // 다른 코스트 토글을 켜면 즉시 해당 코스트로 필터 전환
            currentCostFilter = cost;
        }
        else
        {
            // 이미 활성화된 토글을 다시 눌러서 껐을 때만 필터 해제
            if (currentCostFilter == cost)
            {
                currentCostFilter = -1;
            }
        }

        UpdateCostButtonVisuals();
        UpdateCardDisplay();
    }

    /// <summary>
    /// 마나 수정(0~10) 버튼을 눌렀을 때 실행됩니다. (버튼 호환용)
    /// </summary>
    public void OnCostButtonClick(int cost)
    {
        // 이미 해당 코스트를 보고 있는데 또 누르면 -> 필터 끔(-1)
        // 다른 걸 누르면 -> 그 코스트로 변경
        currentCostFilter = (currentCostFilter == cost) ? -1 : cost;
        UpdateCostButtonVisuals();
        UpdateCardDisplay();
    }

    /// <summary>
    /// 제작 모드 버튼 초기화 및 이벤트 연결
    /// </summary>
    public void InitCraftModeButton()
    {
        if (craftModeButton == null)
        {
            var go = GameObject.Find("/Canvas/Deck/Tool/create");
            if (go != null)
            {
                craftModeButton = go.GetComponent<Button>();
                craftModeButtonImage = go.GetComponent<Image>();
                craftModeButtonText = go.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (craftModeButton != null)
        {
            craftModeButton.onClick.RemoveAllListeners();
            craftModeButton.onClick.AddListener(ToggleCraftingMode);
            UpdateCraftButtonVisuals();
        }
    }

    /// <summary>
    /// 제작 모드 On/Off 토글
    /// </summary>
    public void ToggleCraftingMode()
    {
        isCraftingMode = !isCraftingMode;
        UpdateCraftButtonVisuals();
        UpdateCardDisplay();
    }

    /// <summary>
    /// 제작 모드 버튼 비주얼(텍스트 및 색상) 갱신
    /// </summary>
    public void UpdateCraftButtonVisuals()
    {
        if (craftModeButtonText != null)
        {
            craftModeButtonText.text = isCraftingMode ? "보유 카드" : "제작";
        }
        if (craftModeButtonImage != null)
        {
            craftModeButtonImage.color = isCraftingMode ? CraftBtnActiveBg : CraftBtnNormalBg;
        }
        if (craftModeButtonText != null)
        {
            craftModeButtonText.color = isCraftingMode ? CraftBtnActiveText : CraftBtnNormalText;
        }
    }

    /// <summary>
    /// 검색창에 글자를 칠 때마다 실행됩니다.
    /// </summary>
    public void OnSearchTextChanged(string searchText)
    {
        currentSearchText = searchText;
        UpdateCardDisplay();
    }

    /// <summary>
    /// FilterManager(상세 필터 UI)에서 뭔가 바뀌었을 때 신호를 받아 실행되는 함수입니다.
    /// </summary>
    private void HandleDetailFilterApply(FilterManager.FilterSettings settings)
    {
        currentDetailFilters = settings;
        UpdateCardDisplay();
    }

    /// <summary>
    /// [화면 그리기] 선택된 카드 리스트를 받아서 기존 UI 오브젝트를 재활용(Pool)하여 표시합니다.
    /// (Destroy/Instantiate를 매번 반복하지 않아 검색 및 필터 전환 시 렉을 방지합니다)
    /// </summary>
    void DisplayCards(List<CardData> cardsToDisplay)
    {
        if (cardsToDisplay == null) return;

        int targetCount = cardsToDisplay.Count;

        // 1. 풀에 오브젝트가 부족하면 필요한 만큼만 새로 생성하여 추가
        while (_cardDisplayPool.Count < targetCount)
        {
            GameObject newCard = Instantiate(cardPrefab, cardListParent);
            DeckCardDisplay cardDisplay = newCard.GetComponent<DeckCardDisplay>();

            // '수집품(Collection)' 카드 설정 (클릭 시 덱에 추가되도록)
            CardInteraction cardInteraction = newCard.GetComponent<CardInteraction>();
            if (cardInteraction != null)
            {
                cardInteraction.location = CardInteraction.CardLocation.Collection;
            }

            _cardDisplayPool.Add(cardDisplay);
        }

        // 2. 필요한 카드 슬롯을 켜고(SetActive true) 데이터만 갱신
        for (int i = 0; i < targetCount; i++)
        {
            DeckCardDisplay cardDisplay = _cardDisplayPool[i];
            CardData data = cardsToDisplay[i];

            if (!cardDisplay.gameObject.activeSelf)
            {
                cardDisplay.gameObject.SetActive(true);
            }

            int remainingCount = DeckManager.instance != null
                ? DeckManager.instance.GetRemainingCardCount(data)
                : 2;
            int ownedCount = DeckManager.instance != null
                ? DeckManager.instance.GetOwnedCardCount(data)
                : 2;

            cardDisplay.Setup(data, remainingCount);
            cardDisplay.SetRemainingState(remainingCount, ownedCount);
        }

        // 3. 이번 목록에 필요 없는 남은 슬롯은 비활성화(숨김)
        for (int i = targetCount; i < _cardDisplayPool.Count; i++)
        {
            if (_cardDisplayPool[i] != null && _cardDisplayPool[i].gameObject.activeSelf)
            {
                _cardDisplayPool[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 현재 덱의 상태에 맞춰 활성화된 모든 카드의 잔여 수량 및 사용 불가 상태를 실시간으로 갱신합니다.
    /// (GetComponent 탐색 대신 풀링 리스트를 직접 순회하여 성능을 최적화합니다)
    /// </summary>
    public void UpdateAllCardCounts()
    {
        if (DeckManager.instance == null) return;

        for (int i = 0; i < _cardDisplayPool.Count; i++)
        {
            DeckCardDisplay cardDisplay = _cardDisplayPool[i];
            if (cardDisplay == null || !cardDisplay.gameObject.activeSelf) continue;

            CardData card = cardDisplay.GetCardData();
            if (card != null)
            {
                int remaining = DeckManager.instance.GetRemainingCardCount(card);
                int owned = DeckManager.instance.GetOwnedCardCount(card);
                cardDisplay.SetRemainingState(remaining, owned);
            }
        }

        if (isCraftingMode)
        {
            UpdateCardDisplay();
        }
    }
}