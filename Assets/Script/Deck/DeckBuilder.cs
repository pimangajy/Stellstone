using UnityEngine;
using System.Collections.Generic;
using System.Linq; // 데이터를 필터링(검색)할 때 아주 강력한 도구(LINQ)를 사용합니다.
using System;

/// <summary>
/// 덱 빌더(Deck Builder) 화면의 총감독입니다.
/// 내가 가진 카드를 보여주고, 직업/코스트/검색어에 따라 카드를 걸러서(필터링) 보여줍니다.
/// </summary>
public class DeckBuilder : MonoBehaviour
{
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
    private ClassType? currentClassFilter = null;

    // 2. 상세 필터 (전설 카드만 보기, 짝수 비용만 보기 등)
    private FilterManager.FilterSettings currentDetailFilters;

    // 3. 비용(마나) 필터 (-1이면 필터 안 함, 0~10이면 그 비용만 봄)
    private int currentCostFilter = -1;

    // 4. 검색어 (검색창에 입력한 글자)
    private string currentSearchText = "";

    #region Unity Lifecycle (유니티 생명주기)

    // 이 오브젝트가 켜질 때(활성화) 실행됩니다.
    private void OnEnable()
    {
        // "필터가 적용됐다"는 신호(이벤트)가 오면 HandleDetailFilterApply 함수를 실행하라고 연결(구독)합니다.
        FilterManager.OnFilterApplied += HandleDetailFilterApply;
    }

    // 이 오브젝트가 꺼질 때(비활성화) 실행됩니다.
    private void OnDisable()
    {
        // 연결했던 신호를 끊습니다. (안 끊으면 에러가 나거나 메모리가 낭비됩니다)
        FilterManager.OnFilterApplied -= HandleDetailFilterApply;
    }

    // 게임 시작 시 딱 한 번 실행됩니다.
    void Start()
    {
        // 리소스 매니저에게 "모든 카드 데이터 불러와!"라고 시킵니다.
        ResourceManager.Instance.LoadAllCards();
        // 불러온 데이터를 내 변수에 저장합니다.
        allCardsList = ResourceManager.Instance.GetAllCards();

        // 카드가 있으면 일단 화면에 쫙 뿌려줍니다.
        if (allCardsList != null && allCardsList.Count > 0)
        {
            DisplayCards(allCardsList);
        }
    }

    #endregion

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

        // 1. 직업 필터 적용
        if (currentClassFilter.HasValue)
        {
            // "내 직업이거나" 또는 "중립(강지)" 카드만 남깁니다.
            // .Where는 조건에 맞는 녀석만 통과시키는 거름망 역할을 합니다.
            filteredResult = filteredResult.Where(card =>
                card.member == currentClassFilter.Value ||
                card.member == ClassType.강지);
        }

        // 2. 상세 필터 적용 (필터 매니저 설정값)

        // 직업 전용 필터가 있다면 적용
        if (currentDetailFilters.Member.HasValue)
        {
            filteredResult = filteredResult.Where(card => card.member == currentDetailFilters.Member.Value);
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

        // 4. 검색어 필터
        // 검색창이 비어있지 않다면 실행
        if (!string.IsNullOrWhiteSpace(currentSearchText))
        {
            // 대소문자 구분 없이 찾기 위해 다 소문자로 바꿔서 비교합니다.
            string lowerSearchText = currentSearchText.ToLower();
            filteredResult = filteredResult.Where(card =>
                (card.cardName != null && card.cardName.ToLower().Contains(lowerSearchText)) || // 이름에 포함되거나
                (card.description != null && card.description.ToLower().Contains(lowerSearchText)) // 설명에 포함되거나
            );
        }

        // 필터링된 최종 결과를 리스트로 변환(.ToList())해서 화면 그리기 함수로 넘깁니다.
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

        // 초기화됐으니 화면도 다시 그림
        UpdateCardDisplay();
    }

    /// <summary>
    /// [새 덱 만들기] 직업을 선택했을 때 실행됩니다.
    /// </summary>
    public async void SetClassFilter(string className)
    {
        // 1. 서버(Firebase)에 "이 직업으로 새 덱 하나 만들어줘"라고 요청하고 결과를 기다립니다(await).
        DeckData newDeck = await DeckSaveManager_Firebase.instance.ServerCreateNewDeck(className);

        // 2. 덱 매니저(오른쪽 리스트 관리자)에게 "이제 이 새 덱을 편집할 거야"라고 알려줍니다.
        DeckManager.instance.StartNewDeck(newDeck);

        // 3. 필터들을 깨끗하게 청소합니다.
        currentDetailFilters = new FilterManager.FilterSettings();
        currentCostFilter = -1;
        currentSearchText = "";

        // 4. 문자열로 된 직업 이름(예: "Mage")을 컴퓨터가 이해하는 Enum(ClassType.Mage)으로 바꿉니다.
        if (Enum.TryParse(className, out ClassType classEnum))
        {
            currentClassFilter = classEnum;
        }
        else
        {
            // 변환 실패하면 기본값(강지/중립)으로 설정
            currentClassFilter = ClassType.강지;
        }

        // 5. 필터 UI(책갈피 탭)도 해당 직업만 보이게 갱신합니다.
        if (filterManager != null)
        {
            filterManager.ResetFilterUI();
            var availableMembers = new List<string> { className, ClassType.강지.ToString() };
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
        List<CardData> cardsForDeck = new List<CardData>();
        foreach (string cardId in deckToLoad.cardIds)
        {
            CardData card = ResourceManager.Instance.GetCardData(cardId);
            if (card != null) cardsForDeck.Add(card);
        }

        // 덱 매니저에게 "이 덱 내용으로 채워넣어"라고 시킵니다.
        DeckManager.instance.LoadDeck(deckToLoad, cardsForDeck);

        // 덱의 직업에 맞춰서 필터를 설정합니다.
        SetClassFilterForEditing(deckToLoad.deckClass);
    }

    // 덱 수정 시 필터와 UI를 설정하는 내부 도우미 함수
    private void SetClassFilterForEditing(string className)
    {
        currentDetailFilters = new FilterManager.FilterSettings();
        currentCostFilter = -1;
        currentSearchText = "";

        if (Enum.TryParse(className, out ClassType classEnum))
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
            var availableMembers = new List<string> { className, ClassType.강지.ToString() };
            filterManager.UpdateMemberToggles(availableMembers);
        }
        UpdateCardDisplay();
    }

    /// <summary>
    /// 마나 수정(0~10) 버튼을 눌렀을 때 실행됩니다.
    /// </summary>
    public void OnCostButtonClick(int cost)
    {
        // 이미 3코스트를 보고 있는데 또 3을 누르면 -> 필터 끔(-1)
        // 다른 걸 누르면 -> 그 코스트로 변경
        currentCostFilter = (currentCostFilter == cost) ? -1 : cost;
        UpdateCardDisplay();
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
    /// [화면 그리기] 최종적으로 선택된 카드 리스트를 받아서 실제 게임 오브젝트로 만듭니다.
    /// </summary>
    void DisplayCards(List<CardData> cardsToDisplay)
    {
        // 1. 청소: 기존에 화면에 보여주던 카드들을 싹 지웁니다.
        foreach (Transform child in cardListParent)
        {
            Destroy(child.gameObject);
        }

        // 2. 생성: 리스트에 있는 카드 개수만큼 붕어빵(프리팹)을 찍어냅니다.
        foreach (var data in cardsToDisplay)
        {
            // 프리팹 복제(Instantiate)
            GameObject newCard = Instantiate(cardPrefab, cardListParent);

            // 복제된 카드 UI에 데이터(공격력, 이미지 등)를 채워 넣습니다.
            DeckCardDisplay cardDisplay = newCard.GetComponent<DeckCardDisplay>();
            if (cardDisplay != null) cardDisplay.Setup(data);

            // 이 카드는 '수집품(Collection)'에 있는 카드라고 위치를 지정해줍니다. (클릭 시 덱에 추가되게)
            CardInteraction cardInteraction = newCard.GetComponent<CardInteraction>();
            if (cardInteraction != null) cardInteraction.location = CardInteraction.CardLocation.Collection;
        }
    }
}