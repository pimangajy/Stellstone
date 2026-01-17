using UnityEngine;
using System.Collections.Generic;
using System.Linq; // 데이터를 필터링(검색)할 때 유용한 기능을 제공합니다.
using System;

/// <summary>
/// 덱 빌더(Deck Builder) 화면에서 보유한 카드를 보여주고,
/// 사용자가 원하는 조건(직업, 코스트, 검색어 등)에 맞춰 필터링하는 핵심 매니저 클래스입니다.
/// </summary>
public class DeckBuilder : MonoBehaviour
{
    [Header("UI & Prefab Settings")]
    // 카드를 화면에 찍어낼 때 사용할 원본 프리팹 (붕어빵 틀)
    public GameObject cardPrefab;
    // 생성된 카드가 들어갈 부모 오브젝트 (Scroll View의 Content 역할)
    public Transform cardListParent;

    [Header("Component References")]
    // 필터 UI(버튼 등)를 관리하는 매니저 스크립트 연결
    public FilterManager filterManager;

    // 게임에 존재하는 모든 카드 데이터를 저장해두는 원본 리스트
    private List<CardData> allCardsList;

    // ---------------------------------------------------------
    // 필터링 상태 변수들 (현재 사용자가 무엇을 선택했는지 저장)
    // ---------------------------------------------------------

    // 1. 메인 직업 필터 (null이면 전체, 값이 있으면 그 직업 + 중립 카드만 표시)
    private ClassType? currentClassFilter = null;

    // 2. 상세 필터 설정값 (하수인/주문, 희귀도, 확장팩 등)
    // FilterManager에서 정의한 구조체를 사용하여, 선택 안 된 항목은 null로 처리합니다.
    private FilterManager.FilterSettings currentDetailFilters;

    // 3. 마나 코스트 필터 (-1이면 필터 없음, 0~10+이면 해당 코스트만 표시)
    private int currentCostFilter = -1;

    // 4. 검색어 필터 (빈 문자열이면 필터 없음)
    private string currentSearchText = "";

    #region Unity Lifecycle (유니티 생명주기)

    // 오브젝트가 활성화될 때 호출됩니다.
    private void OnEnable()
    {
        // FilterManager에서 "필터 적용 버튼이 눌렸다"는 이벤트(OnFilterApplied)를 구독(연결)합니다.
        // 이벤트가 발생하면 HandleDetailFilterApply 함수를 실행합니다.
        FilterManager.OnFilterApplied += HandleDetailFilterApply;
    }

    // 오브젝트가 비활성화될 때 호출됩니다.
    private void OnDisable()
    {
        // 이벤트 구독을 해제합니다. (메모리 누수 방지 및 에러 방지)
        FilterManager.OnFilterApplied -= HandleDetailFilterApply;
    }

    // 게임 시작 시 초기화를 담당합니다.
    void Start()
    {
        // 리소스 매니저에게 모든 카드 데이터를 불러오라고 시킵니다.
        ResourceManager.Instance.LoadAllCards();
        // 불러온 데이터를 변수에 저장합니다.
        allCardsList = ResourceManager.Instance.GetAllCards();

        // 카드가 있다면 화면에 보여줍니다.
        if (allCardsList != null && allCardsList.Count > 0)
        {
            DisplayCards(allCardsList);
        }
    }

    #endregion

    /// <summary>
    /// [핵심 기능] 현재 설정된 모든 필터 조건(직업, 상세, 코스트, 검색어)을 종합하여
    /// 조건에 맞는 카드만 추려내고 화면을 갱신합니다.
    /// </summary>
    private void UpdateCardDisplay()
    {
        // 원본 데이터가 없으면 아무것도 하지 않습니다.
        if (allCardsList == null) return;

        // LINQ를 사용하여 필터링을 단계별로 적용합니다.
        // filteredResult는 아직 리스트로 변환되지 않은 '검색 조건' 상태입니다.
        IEnumerable<CardData> filteredResult = allCardsList;

        // ---------------------------------------------------------
        // 1. 덱 직업 필터 (예: 마법사 덱을 짜면 '마법사' 카드와 '중립(강지)' 카드만 보여야 함)
        // ---------------------------------------------------------
        if (currentClassFilter.HasValue)
        {
            filteredResult = filteredResult.Where(card =>
                card.member == currentClassFilter.Value ||
                card.member == ClassType.강지); // '강지'는 중립(공용) 카드를 의미
        }

        // ---------------------------------------------------------
        // 2. 상세 필터 (FilterManager에서 넘어온 값들)
        // Nullable(?.)을 사용하여 값이 있는 경우에만 필터를 적용합니다.
        // ---------------------------------------------------------

        // 직업 상세 필터 (토글 버튼으로 특정 직업만 보고 싶을 때)
        if (currentDetailFilters.Member.HasValue)
        {
            filteredResult = filteredResult.Where(card => card.member == currentDetailFilters.Member.Value);
        }

        // 카드 타입 필터 (하수인, 주문, 무기 등)
        if (currentDetailFilters.CardType.HasValue)
        {
            filteredResult = filteredResult.Where(card => card.cardType == currentDetailFilters.CardType.Value);
        }

        // 희귀도 필터 (일반, 희귀, 전설 등)
        if (currentDetailFilters.Rarity.HasValue)
        {
            filteredResult = filteredResult.Where(card => card.rarity == currentDetailFilters.Rarity.Value);
        }

        // 확장팩 필터 (어떤 카드팩 출신인지)
        if (currentDetailFilters.Expansion.HasValue)
        {
            filteredResult = filteredResult.Where(card => card.expansion == currentDetailFilters.Expansion.Value);
        }

        // ---------------------------------------------------------
        // 3. 코스트 필터 (마나 수정 버튼)
        // ---------------------------------------------------------
        if (currentCostFilter != -1)
        {
            if (currentCostFilter >= 10)
                // 10코스트 이상인 경우 (보통 10+로 표시됨)
                filteredResult = filteredResult.Where(card => card.manaCost >= currentCostFilter);
            else
                // 정확히 해당 코스트인 경우
                filteredResult = filteredResult.Where(card => card.manaCost == currentCostFilter);
        }

        // ---------------------------------------------------------
        // 4. 검색어 필터 (이름 또는 설명에 검색어가 포함되어 있는지)
        // ---------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(currentSearchText))
        {
            // 대소문자 구분 없이 검색하기 위해 모두 소문자로 변환합니다.
            string lowerSearchText = currentSearchText.ToLower();
            filteredResult = filteredResult.Where(card =>
                (card.cardName != null && card.cardName.ToLower().Contains(lowerSearchText)) ||
                (card.description != null && card.description.ToLower().Contains(lowerSearchText))
            );
        }



        // 필터링이 끝난 최종 결과를 리스트로 변환(.ToList())하여 화면에 그립니다.
        DisplayCards(filteredResult.ToList());
    }

    /// <summary>
    /// 모든 필터 조건을 초기화하고 전체 카드를 보여줍니다.
    /// </summary>
    public void ResetAllFilters()
    {
        currentClassFilter = null;
        currentDetailFilters = new FilterManager.FilterSettings(); // 구조체라 자동으로 모든 필드가 null로 초기화됨
        currentCostFilter = -1;
        currentSearchText = "";

        // 변경된 설정으로 화면 갱신
        UpdateCardDisplay();
    }

    /// <summary>
    /// [새 덱 만들기] 특정 직업을 선택했을 때 호출됩니다.
    /// 서버에 새 덱을 생성하고, 화면을 해당 직업 카드로 필터링합니다.
    /// </summary>
    public async void SetClassFilter(string className)
    {
        // 1. Firebase(서버)에 새 덱 데이터를 생성 요청합니다.
        DeckData newDeck = await DeckSaveManager_Firebase.instance.ServerCreateNewDeck(className);

        // 2. 덱 매니저에게 "이제 이 덱을 편집할 거야"라고 알립니다.
        DeckManager.instance.StartNewDeck(newDeck);

        // 3. 필터들을 초기화합니다.
        currentDetailFilters = new FilterManager.FilterSettings();
        currentCostFilter = -1;
        currentSearchText = "";

        // 4. 선택한 직업(className)을 Enum으로 변환하여 메인 필터로 설정합니다.
        if (Enum.TryParse(className, out ClassType classEnum))
        {
            currentClassFilter = classEnum;
        }
        else
        {
            // 변환 실패 시 기본값(예: 중립)으로 설정
            currentClassFilter = ClassType.강지;
        }

        // 5. FilterManager(UI)에게 직업 탭을 갱신하라고 지시합니다.
        // (내 직업 + 중립 탭만 보이게 설정)
        if (filterManager != null)
        {
            filterManager.ResetFilterUI();
            var availableMembers = new List<string> { className, ClassType.강지.ToString() };
            filterManager.UpdateMemberToggles(availableMembers);
        }

        // 6. 화면 갱신
        UpdateCardDisplay();
    }

    /// <summary>
    /// [덱 수정하기] 이미 만들어진 덱을 불러와서 편집 모드로 들어갑니다.
    /// </summary>
    public void LoadDeckForEditing(DeckData deckToLoad)
    {
        // 덱에 들어있는 카드 ID들을 실제 CardData 객체로 변환합니다.
        List<CardData> cardsForDeck = new List<CardData>();
        foreach (string cardId in deckToLoad.cardIds)
        {
            CardData card = ResourceManager.Instance.GetCardData(cardId);
            if (card != null) cardsForDeck.Add(card);
        }

        // 덱 매니저에 불러온 덱 정보를 전달합니다.
        DeckManager.instance.LoadDeck(deckToLoad, cardsForDeck);

        // 화면의 필터를 해당 덱의 직업에 맞게 설정합니다.
        SetClassFilterForEditing(deckToLoad.deckClass);
    }

    // 덱 수정 시 필터와 UI를 설정하는 내부 함수
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
    /// UI의 마나 코스트 버튼(0~10)을 눌렀을 때 호출됩니다.
    /// </summary>
    public void OnCostButtonClick(int cost)
    {
        // 이미 선택된 코스트를 다시 누르면 필터 해제(-1), 아니면 해당 코스트로 설정
        currentCostFilter = (currentCostFilter == cost) ? -1 : cost;
        UpdateCardDisplay();
    }

    /// <summary>
    /// 검색창에 글자를 입력할 때마다 호출됩니다.
    /// </summary>
    public void OnSearchTextChanged(string searchText)
    {
        currentSearchText = searchText;
        UpdateCardDisplay();
    }

    /// <summary>
    /// FilterManager에서 상세 필터(토글)가 변경되었을 때 이벤트로 호출되는 함수입니다.
    /// </summary>
    private void HandleDetailFilterApply(FilterManager.FilterSettings settings)
    {
        currentDetailFilters = settings;
        UpdateCardDisplay(); // 변경된 상세 필터를 적용하여 화면 갱신
    }

    /// <summary>
    /// [화면 그리기] 최종적으로 걸러진 카드 리스트를 받아서 실제 UI 오브젝트로 만들어 보여줍니다.
    /// </summary>
    void DisplayCards(List<CardData> cardsToDisplay)
    {
        // 1. 기존에 화면에 떠있던 카드들을 모두 삭제(청소)합니다.
        foreach (Transform child in cardListParent)
        {
            Destroy(child.gameObject);
        }

        // 2. 리스트에 있는 카드 개수만큼 새로운 카드 UI를 생성(Instantiate)합니다.
        foreach (var data in cardsToDisplay)
        {
            // 프리팹 생성
            GameObject newCard = Instantiate(cardPrefab, cardListParent);

            // 카드 UI에 데이터(공격력, 체력, 이미지 등)를 입력해줍니다.
            DeckCardDisplay cardDisplay = newCard.GetComponent<DeckCardDisplay>();
            if (cardDisplay != null) cardDisplay.Setup(data);

            // 카드 클릭 등의 상호작용 설정을 '수집품(Collection)' 모드로 설정합니다.
            CardInteraction cardInteraction = newCard.GetComponent<CardInteraction>();
            if (cardInteraction != null) cardInteraction.location = CardInteraction.CardLocation.Collection;
        }
    }
}