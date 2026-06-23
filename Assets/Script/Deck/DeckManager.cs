using System.Collections;
using System.Collections.Generic;
using System.Linq; // 리스트 데이터를 다루는 강력한 도구(LINQ)
using UnityEngine;
using System;
using TMPro;

/// <summary>
/// [현재 편집 중인 덱]을 관리하는 매니저입니다.
/// 오른쪽 화면의 '현재 덱 리스트'에 카드를 추가/제거하고, 규칙(30장 제한 등)을 검사합니다.
/// </summary>
public class DeckManager : MonoBehaviour
{
    // 싱글톤 패턴: 어디서든 DeckManager.instance 로 접근 가능하게 함
    public static DeckManager instance;

    [Header("덱 규칙 설정")]
    [SerializeField] private int maxDeckSize = 30; // 덱 최대 장수 제한

    [Header("사이드 덱 설정")]
    private int maxSideDeckSize = 5;
    public bool isEditingSideDeck = false; // 현재 사이드 덱 편집 중인지 여부

    [Header("UI 연결")]
    public TMP_Text deckName;
    public Transform mainDeckListParent; // 카드 목록이 표시될 UI 부모 (Content)
    public Transform sideDeckListParent; // 카드 목록이 표시될 UI 부모 (Content)
    public GameObject deckCardPrefab; // 목록에 추가될 카드 줄(Item) 프리팹

    // 현재 덱의 직업 (예: "Mage", "Warrior"). 문자열로 저장됩니다.
    private string selectedClass = "";

    // 현재 덱에 포함된 실제 카드 데이터들의 리스트 (장바구니)
    private List<CardData> currentDeck = new List<CardData>();

    // 현재 편집 중인 사이드 덱 장바구니
    private List<CardData> currentSideDeck = new List<CardData>();

    // 지금 편집하고 있는 덱의 껍데기 정보 (이름, ID 등)
    private DeckData currentlyEditingDeck;

    void Awake()
    {
        // 싱글톤 초기화
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// [새 덱 만들기] 빈 덱으로 편집을 시작합니다.
    /// </summary>
    public void StartNewDeck(DeckData newDeck)
    {
        isEditingSideDeck = false;
        currentlyEditingDeck = newDeck;
        selectedClass = newDeck.deckClass; // 덱의 직업 설정
        currentDeck.Clear(); // 장바구니 비우기
        currentSideDeck.Clear();
        UpdateDeckListUI();  // 화면 갱신
        Debug.Log($"'{newDeck.deckName}' 만들기를 시작합니다.");
    }

    // UI의 '사이드 덱 버튼'을 누르면 호출될 함수
    public void ToggleSideDeckEditing()
    {   
        // 현재 편집 중인 덱이 없다면(단순 열람 중이라면) 함수를 바로 종료합니다.
        if (currentlyEditingDeck == null)
        {
            Debug.LogWarning("현재 덱 편성 중이 아닙니다. 새 덱을 만들거나 기존 덱을 선택해 주세요.");
            return;
        }

        isEditingSideDeck = !isEditingSideDeck;
        Debug.Log(isEditingSideDeck ? "사이드 덱 편집 모드" : "메인 덱 편집 모드");
        // TODO: UI에서 시각적으로 어떤 덱을 편집 중인지 표시(강조)하는 로직 추가
    }

    /// <summary>
    /// [기존 덱 불러오기] 저장된 덱을 불러와서 편집을 시작합니다.
    /// </summary>
    public void LoadDeck(DeckData deckToLoad, List<CardData> mainCards, List<CardData> sideCards)
    {
        isEditingSideDeck = false;

        currentlyEditingDeck = deckToLoad;
        selectedClass = deckToLoad.deckClass;
        // 기존 카드 리스트를 복사해서 장바구니에 담습니다.
        currentDeck = new List<CardData>(mainCards);
        currentSideDeck = new List<CardData>(sideCards);
        UpdateDeckListUI();
        Debug.Log($"'{deckToLoad.deckName}' 덱을 불러왔습니다.");
    }

    /// <summary>
    /// 현재 편집 중인 덱을 서버에서 삭제합니다.
    /// </summary>
    public async void DeleteDeck()
    {
        // 서버 매니저에게 삭제 요청
        await DeckSaveManager_Firebase.instance.ServerDeleteDeck(currentlyEditingDeck.deckId);

        // 데이터 초기화
        currentDeck.Clear(); // 장바구니 비우기
        currentSideDeck.Clear();
        currentlyEditingDeck = null;
        selectedClass = null;
        UpdateDeckListUI();
    }

    /// <summary>
    /// [저장 버튼] 현재 장바구니(currentDeck) 상태를 서버에 저장합니다.
    /// </summary>
    public async void SaveCurrentDeck()
    {
        if (currentlyEditingDeck == null)
        {
            Debug.LogWarning("저장할 덱이 선택되지 않았습니다.");
            return;
        }

        if (currentSideDeck.Count > 0 && currentSideDeck.Count != 5)
        {
            Debug.LogWarning("사이드 덱을 5장 꽉 채워야 저장할 수 있습니다.");
            return;
        }

        // 1. 기존에 저장된 ID 목록을 싹 비웁니다.
        currentlyEditingDeck.cardIds.Clear();
        currentlyEditingDeck.sideDeckCardIds.Clear();
        currentlyEditingDeck.sideDeckFirstTurnCardIds.Clear();

        // 2. 메인 덱 30장 저장
        foreach (var card in currentDeck)
        {
            currentlyEditingDeck.cardIds.Add(card.cardID);
        }

        // 3. 사이드 덱 5장 전체 저장
        foreach (var card in currentSideDeck)
        {
            currentlyEditingDeck.sideDeckCardIds.Add(card.cardID);
        }

        // 4. 사이드 덱에 들어온 순서대로 앞의 3장을 선공용으로 자동 지정
        // (Mathf.Min을 써서 만약 3장 미만이어도 에러가 안 나게 안전하게 처리)
        int firstTurnCount = Mathf.Min(3, currentSideDeck.Count);
        for (int i = 0; i < firstTurnCount; i++)
        {
            currentlyEditingDeck.sideDeckFirstTurnCardIds.Add(currentSideDeck[i].cardID);
        }

        // 서버로 데이터 덮어쓰기 요청 전송
        await DeckSaveManager_Firebase.instance.ServerUpdateDeck(currentlyEditingDeck);
        Debug.Log($"'{currentlyEditingDeck.deckName}' 덱(메인+사이드)이 저장되었습니다.");
    }

    /// <summary>
    /// 덱에 카드를 한 장 추가합니다. (왼쪽 리스트에서 클릭 시 호출)
    /// </summary>
    public void AddCard(CardData cardToAdd)
    {
        // 덱을 만들고 있는 상태가 아니면 무시
        if (currentlyEditingDeck == null)
        {
            Debug.LogWarning("먼저 덱을 선택하거나 새로 만들어야 카드를 추가할 수 있습니다.");
            return;
        }

        // 카드를 넣을 수 있는지 규칙 검사 (30장 꽉 찼는지, 직업이 맞는지 등)
        if (!IsCardAddable(cardToAdd))
        {
            return; // 추가 불가능하면 여기서 함수 종료
        }

        // 모드에 따라 알맞은 덱에 추가
        if (isEditingSideDeck)
        {
            currentSideDeck.Add(cardToAdd);
        }
        else
        {
            currentDeck.Add(cardToAdd);
        }
        // 화면 갱신
        UpdateDeckListUI();
    }

    /// <summary>
    /// 덱에서 카드를 한 장 뺍니다. (오른쪽 리스트에서 클릭 시 호출)
    /// </summary>
    public void RemoveCard(CardData cardToRemove)
    {
        if (isEditingSideDeck)
        {
            // 사이드 덱에서 카드 제거 (가장 먼저 넣은 해당 카드를 뺌)
            CardData cardInSideDeck = currentSideDeck.FirstOrDefault(c => c.cardID == cardToRemove.cardID);
            if (cardInSideDeck != null)
            {
                currentSideDeck.Remove(cardInSideDeck);
                UpdateDeckListUI();
            }
        }
        else
        {
            // 메인 덱에서 카드 제거 (기존 로직 유지)
            CardData cardInDeck = currentDeck.FirstOrDefault(c => c.cardID == cardToRemove.cardID);
            if (cardInDeck != null)
            {
                currentDeck.Remove(cardInDeck);
                UpdateDeckListUI();
            }
        }
    }

    /// <summary>
    /// [규칙 검사기] 이 카드를 덱에 넣을 수 있는지 확인합니다.
    /// </summary>
    private bool IsCardAddable(CardData card)
    {
        // 1. 공통 규칙: 직업 제한 확인 (내 직업이거나 중립(강지) 카드여야 함)
        string cardMemberStr = card.cardClass.ToString();
        if (cardMemberStr != selectedClass && card.cardClass != CardClass.Gangzi)
        {
            Debug.LogWarning($"'{card.cardName}' 카드는 '{selectedClass}' 덱에 추가할 수 없습니다.");
            return false;
        }

        // 2. 현재 편집 모드(메인/사이드)에 따른 최대 장수 및 특수 규칙 검사
        if (isEditingSideDeck)
        {
            // 사이드 덱 검사
            if (currentSideDeck.Count >= maxSideDeckSize) // 5장 제한
            {
                Debug.LogWarning("사이드 덱이 가득 찼습니다. (최대 5장)");
                return false;
            }

            if (card.cardType != CardType.하수인 && card.cardType != CardType.주문)
            {
                Debug.LogWarning("사이드 덱에는 하수인과 주문 카드만 넣을 수 있습니다.");
                return false;
            }
        }
        else
        {
            // 메인 덱 검사
            if (currentDeck.Count >= maxDeckSize)
            {
                Debug.LogWarning("메인 덱이 가득 찼습니다.");
                return false;
            }
        }

        // 3. [핵심 수정] 메인 덱과 사이드 덱의 동일 카드 장수를 합산
        int mainDeckCount = currentDeck.Count(c => c.cardID == card.cardID);
        int sideDeckCount = currentSideDeck.Count(c => c.cardID == card.cardID);
        int totalSameCardCount = mainDeckCount + sideDeckCount;

        // 4. 합산된 장수로 일반 2장, 전설 1장 제한 검사
        if (card.rarity == CardRarity.legendary)
        {
            if (totalSameCardCount >= 1)
            {
                Debug.LogWarning("전설 카드는 메인 덱과 사이드 덱을 합쳐 한 장만 넣을 수 있습니다.");
                return false;
            }
        }
        else
        {
            if (totalSameCardCount >= 2)
            {
                Debug.LogWarning("일반 카드는 메인 덱과 사이드 덱을 합쳐 최대 두 장까지만 넣을 수 있습니다.");
                return false;
            }
        }

        return true; // 모든 검사를 통과했으므로 추가 가능
    }

    /// <summary>
    /// 덱 이름을 변경합니다. (InputFieldController에서 호출)
    /// </summary>
    public void UpdateDeckname(string name)
    {
        currentlyEditingDeck.deckName = name;

        if (deckName != null)
        {
            deckName.text = name;
        }
    }

    /// <summary>
    /// [화면 갱신] 현재 덱 리스트(오른쪽) UI를 다시 그립니다.
    /// </summary>
    private void UpdateDeckListUI()
    {
        // 1. 기존 목록 싹 지우기 (DeckPlus 버튼 빼고)
        foreach (Transform child in mainDeckListParent)
        {
            if (child.gameObject.name != "DeckPlus")
            {
                Destroy(child.gameObject);
            }
        }
        foreach (Transform child in sideDeckListParent)
        {
            Destroy(child.gameObject);
        }


        // 사이드 덱 리스트 생성
        for (int i = 0; i < currentSideDeck.Count; i++)
        {
            CardData card = currentSideDeck[i];
            GameObject newDeckCardUI = Instantiate(deckCardPrefab, sideDeckListParent);

            var itemDisplay = newDeckCardUI.GetComponent<ICardDataHolder>() as DeckListItemDisplay;
            if (itemDisplay != null)
            {
                // 사이드 덱은 무조건 1장씩 보여줍니다.
                itemDisplay.Setup(card, 1);
            }

            CardInteraction interaction = newDeckCardUI.GetComponent<CardInteraction>();
            if (interaction != null)
            {
                interaction.location = CardInteraction.CardLocation.Deck;
            }
        }
        
        // 2. 카드 정리하기 (LINQ 사용)
        // 리스트에 [화염구, 화염구, 얼음화살] 이렇게 들어있는 것을
        // -> [화염구 x2], [얼음화살 x1] 형태로 묶어서(GroupBy) 보여줘야 합니다.
        var groupedAndSortedDeck = currentDeck
            .GroupBy(card => card.cardID) // ID가 같은 것끼리 묶어라
            .Select(group => new
            {
                Card = group.First(), // 대표 카드 정보 하나
                Count = group.Count() // 몇 장 있는지
            })
            .OrderBy(item => item.Card.manaCost) // 코스트 낮은 순서로 정렬
            .ThenBy(item => item.Card.cardName); // 코스트 같으면 이름 순으로 정렬

        // 3. 정리된 목록대로 UI 생성
        foreach (var item in groupedAndSortedDeck)
        {
            GameObject newDeckCardUI = Instantiate(deckCardPrefab, mainDeckListParent);

            // UI에 정보 입력 (이름, 코스트, 장수)
            var itemDisplay = newDeckCardUI.GetComponent<ICardDataHolder>() as DeckListItemDisplay;
            if (itemDisplay != null)
            {
                itemDisplay.Setup(item.Card, item.Count);
            }

            // 클릭 시 '덱에서 제거'되도록 위치 설정
            CardInteraction interaction = newDeckCardUI.GetComponent<CardInteraction>();
            if (interaction != null)
            {
                interaction.location = CardInteraction.CardLocation.Deck;
            }
        }
    }
}