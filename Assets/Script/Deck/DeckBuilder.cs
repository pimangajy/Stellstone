using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public class DeckBuilder : MonoBehaviour
{
    [Header("UI & Prefab Settings")]
    public GameObject cardPrefab;
    public Transform cardListParent;
    [Header("Component References")]
    [Tooltip("씬에 있는 FilterManager 오브젝트를 연결해주세요.")]
    public FilterManager filterManager;

    private List<CardDataFirebase> allCardsList; // 모든 카드의 원본 리스트

    // --- 모든 필터의 현재 상태를 저장하는 변수들 ---
    private string currentClassFilter = "전체";
    private FilterManager.FilterSettings currentDetailFilters;
    private int currentCostFilter = -1; // -1은 코스트 필터가 적용되지 않았음을 의미
    private string currentSearchText = "";

    #region Unity Lifecycle & Event Subscription

    private void OnEnable()
    {
        FilterManager.OnFilterApplied += HandleDetailFilterApply;
    }

    private void OnDisable()
    {
        FilterManager.OnFilterApplied -= HandleDetailFilterApply;
    }

    #endregion

    async void Start()
    {
        // 1. 모든 카드 정보를 우선 로드합니다.
        var cardDatabase = await CardDatabaseManager.instance.GetAllCardsAsync();

        if (cardDatabase != null && cardDatabase.Count > 0)
        {
            allCardsList = cardDatabase.Values.ToList();

            // --- (수정) SceneLoader에 편집할 덱이 있는지 확인 ---
            if (SceneLoader.instance != null && SceneLoader.instance.DeckToEdit != null)
            {
                // 2. 편집할 덱이 있으면, 덱 로드 함수를 바로 호출
                Debug.Log($"SceneLoader로부터 '{SceneLoader.instance.DeckToEdit.deckName}' 덱을 불러와 편집을 시작합니다.");

                // (중요) LoadDeckForEditing 함수가 DeckManager와 CardDatabase를 모두 사용하므로,
                // 이 씬에 DeckManager.instance와 CardDatabaseManager.instance가 모두 로드된 상태여야 합니다.
                LoadDeckForEditing(SceneLoader.instance.DeckToEdit);

                // 3. (중요) 데이터를 사용했으니 비워줍니다.
                SceneLoader.instance.ClearDeckToEdit();
            }
            else
            {
                UpdateCardDisplay();
            }
        }
        else
        {
            Debug.LogError("카드 정보를 불러오는 데 실패했습니다.");
        }
    }

    /// <summary>
    /// 모든 필터 조건을 종합하여 카드 목록 UI를 업데이트하는 중앙 함수입니다.
    /// </summary>
    private void UpdateCardDisplay()
    {
        if (allCardsList == null) return;

        IEnumerable<CardDataFirebase> filteredResult = allCardsList;

        // 1. 직업 필터 적용
        if (currentClassFilter != "전체")
        {
            filteredResult = filteredResult.Where(card => card.member == currentClassFilter || card.member == "Gangzi");
        }

        // 2. 상세 필터(카드 종류, 레어도, 확장팩) 적용
        if (currentDetailFilters.Member != "전체")
        {
            filteredResult = filteredResult.Where(card => card.member == currentDetailFilters.Member);
        }
        if (currentDetailFilters.CardType != "전체")
        {
            filteredResult = filteredResult.Where(card => card.type == currentDetailFilters.CardType);
        }
        if (currentDetailFilters.Rarity != "전체")
        {
            filteredResult = filteredResult.Where(card => card.rarity == currentDetailFilters.Rarity);
        }
        if (currentDetailFilters.Expansion != "전체")
        {
            filteredResult = filteredResult.Where(card => card.expansion == currentDetailFilters.Expansion);
        }

        // 3. 코스트 필터 적용
        if (currentCostFilter != -1)
        {
            if (currentCostFilter >= 10)
            {
                filteredResult = filteredResult.Where(card => card.cost >= currentCostFilter);
            }
            else
            {
                filteredResult = filteredResult.Where(card => card.cost == currentCostFilter);
            }
        }

        // 4. 텍스트 검색 필터 적용
        if (!string.IsNullOrWhiteSpace(currentSearchText))
        {
            string lowerSearchText = currentSearchText.ToLower();
            filteredResult = filteredResult.Where(card =>
                (card.name != null && card.name.ToLower().Contains(lowerSearchText)) ||
                (card.tribe != null && card.tribe.ToLower().Contains(lowerSearchText)) ||
                (card.description != null && card.description.ToLower().Contains(lowerSearchText))
            );
        }

        DisplayCards(filteredResult.ToList());
    }

    /// <summary>
    /// 주어진 카드 리스트를 UI에 표시합니다.
    /// </summary>
    void DisplayCards(List<CardDataFirebase> cardsToDisplay)
    {
        foreach (Transform child in cardListParent)
        {
            Destroy(child.gameObject);
        }

        foreach (var data in cardsToDisplay)
        {
            GameObject newCard = Instantiate(cardPrefab, cardListParent);
            DeckCardDisplay cardDisplay = newCard.GetComponent<DeckCardDisplay>();
            if (cardDisplay != null)
            {
                cardDisplay.Setup(data);
            }

            // 생성된 카드 오브젝트에 있는 CardInteraction 스크립트를 찾아서 위치 정보를 설정합니다.
            CardInteraction cardInteraction = newCard.GetComponent<CardInteraction>();
            if (cardInteraction != null)
            {
                // DeckBuilder는 중앙 카드 목록을 담당하므로, 여기서 생성되는 카드는 모두 Collection 카드입니다.
                cardInteraction.location = CardInteraction.CardLocation.Collection;
            }
        }
    }

    #region Public Filter Methods (UI에서 호출)

    /// <summary>
    /// 모든 필터를 초기 상태로 리셋합니다. '필터 초기화' 버튼에 연결할 수 있습니다.
    /// </summary>
    public void ResetAllFilters()
    {
        currentClassFilter = "전체";
        currentDetailFilters = new FilterManager.FilterSettings
        {
            CardType = "전체",
            Rarity = "전체",
            Expansion = "전체"
        };
        currentCostFilter = -1;
        currentSearchText = "";

        // TODO: 필터 UI의 표시 상태(토글, 검색창 등)도 초기화하는 신호를 보내면 더 좋습니다.
        // 예를 들어, FilterManager에 ResetUI() 함수를 만들고 여기서 호출할 수 있습니다.

        UpdateCardDisplay();
    }

    /// <summary>
    /// ClassSelectionButton에서 호출할 함수. 다른 필터를 초기화하고 직업 필터를 설정합니다.
    /// </summary>
    public async void SetClassFilter(string className)
    {
        // 1. DeckSaveManager를 통해 새로운 덱 데이터를 생성하고 그 정보를 받아옵니다.
        //DeckData newDeck = DeckSaveManager.instance.CreateNewDeck(className);

        // 1. DeckSaveManager_Firebase를 통해 새로운 덱 데이터를 생성하고 그 정보를 받아옵니다.
        // DeckData newDeck = await DeckSaveManager_Firebase.instance.CreateNewDeck(className);

        DeckData serverNewDeck = await DeckSaveManager_Firebase.instance.ServerCreateNewDeck(className);

        // 2. DeckManager에 방금 만든 덱 정보를 넘겨주어 편집을 시작하도록 합니다.
        DeckManager.instance.StartNewDeck(serverNewDeck);

        // 1. 상세, 코스트, 검색 필터를 초기화합니다.
        currentDetailFilters = new FilterManager.FilterSettings {Member = "전체", CardType = "전체", Rarity = "전체", Expansion = "전체" };
        currentCostFilter = -1;
        currentSearchText = "";

        // 2. 새로운 직업 필터를 설정합니다.
        currentClassFilter = className;

        // 3. FilterManager에 UI 초기화를 요청합니다.
        if (filterManager != null)
        {
            filterManager.ResetFilterUI();

            // 4. 선택된 직업과 중립에 해당하는 멤버 토글만 보이도록 FilterManager에 요청합니다.
            var availableMembers = new List<string> { className, "중립" };
            filterManager.UpdateMemberToggles(availableMembers);
        }

        Debug.Log(currentDetailFilters);

        // 5. 변경된 필터 상태로 화면을 갱신합니다.
        UpdateCardDisplay();
    }

    /// <summary>
    /// DeckListUI의 버튼을 클릭했을 때 호출됩니다.
    /// 저장된 덱을 불러와 편집 모드로 전환합니다.
    /// </summary>
    public void LoadDeckForEditing(DeckData deckToLoad)
    {
        if (allCardsList == null || allCardsList.Count == 0)
        {
            Debug.LogWarning("아직 모든 카드 로딩이 완료되지 않아 덱을 불러올 수 없습니다. 잠시 후 다시 시도하세요.");
            return; // 함수 즉시 종료
        }

        // 1. 불러올 덱의 카드 ID 리스트를 기반으로, 전체 카드 목록(allCardsList)에서
        //    실제 CardDataFirebase 객체 리스트를 만듭니다.
        List<CardDataFirebase> cardsForDeck = new List<CardDataFirebase>();
        var allCardsDict = allCardsList.ToDictionary(card => card.CardID);

        foreach (string cardId in deckToLoad.cardIds)
        {
            if (allCardsDict.TryGetValue(cardId, out CardDataFirebase card))
            {
                cardsForDeck.Add(card);
            }
        }

        // 2. DeckManager에 덱 데이터와 카드 리스트를 전달하여 로드 요청
        DeckManager.instance.LoadDeck(deckToLoad, cardsForDeck);

        // 3. 화면 필터를 불러온 덱의 직업에 맞게 설정
        SetClassFilterForEditing(deckToLoad.deckClass);
    }

    /// <summary>
    /// 덱 로드 시, 필터만 설정하고 새 덱은 만들지 않는 버전의 함수입니다.
    /// </summary>
    private void SetClassFilterForEditing(string className)
    {
        currentDetailFilters = new FilterManager.FilterSettings { Member = "전체", CardType = "전체", Rarity = "전체", Expansion = "전체" };
        currentCostFilter = -1;
        currentSearchText = "";
        currentClassFilter = className;

        if (filterManager != null)
        {
            filterManager.ResetFilterUI();
            var availableMembers = new List<string> { className, "Gangzi" };
            filterManager.UpdateMemberToggles(availableMembers);
        }

        UpdateCardDisplay();
    }

    /// <summary>
    /// 코스트 버튼 클릭 시 호출될 함수.
    /// </summary>
    public void OnCostButtonClick(int cost)
    {
        // 같은 코스트 버튼을 다시 누르면 필터 해제
        currentCostFilter = (currentCostFilter == cost) ? -1 : cost;
        UpdateCardDisplay();
    }

    /// <summary>
    /// 검색창 텍스트 변경 시 호출될 함수.
    /// </summary>
    public void OnSearchTextChanged(string searchText)
    {
        currentSearchText = searchText;
        UpdateCardDisplay();
    }

    /// <summary>
    /// FilterManager로부터 상세 필터 적용 신호를 받았을 때 실행될 함수.
    /// </summary>
    private void HandleDetailFilterApply(FilterManager.FilterSettings settings)
    {
        currentDetailFilters = settings;
        UpdateCardDisplay();
    }

    #endregion
}


