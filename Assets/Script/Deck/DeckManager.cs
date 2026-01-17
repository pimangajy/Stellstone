using System.Collections;
using System.Collections.Generic;
using System.Linq; // 리스트 데이터를 다루는 강력한 도구(LINQ)
using UnityEngine;
using System;

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

    [Header("UI 연결")]
    public Transform deckListParent; // 카드 목록이 표시될 UI 부모 (Content)
    public GameObject deckCardPrefab; // 목록에 추가될 카드 줄(Item) 프리팹

    // 현재 덱의 직업 (예: "Mage", "Warrior"). 문자열로 저장됩니다.
    private string selectedClass = "";

    // 현재 덱에 포함된 실제 카드 데이터들의 리스트 (장바구니)
    private List<CardData> currentDeck = new List<CardData>();

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
        currentlyEditingDeck = newDeck;
        selectedClass = newDeck.deckClass; // 덱의 직업 설정
        currentDeck.Clear(); // 장바구니 비우기
        UpdateDeckListUI();  // 화면 갱신
        Debug.Log($"'{newDeck.deckName}' 만들기를 시작합니다.");
    }

    /// <summary>
    /// [기존 덱 불러오기] 저장된 덱을 불러와서 편집을 시작합니다.
    /// </summary>
    public void LoadDeck(DeckData deckToLoad, List<CardData> cards)
    {
        currentlyEditingDeck = deckToLoad;
        selectedClass = deckToLoad.deckClass;
        // 기존 카드 리스트를 복사해서 장바구니에 담습니다.
        currentDeck = new List<CardData>(cards);
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

        // 1. 기존에 저장된 ID 목록을 싹 비웁니다.
        currentlyEditingDeck.cardIds.Clear();

        // 2. 현재 장바구니에 있는 카드들의 ID를 다시 채워넣습니다.
        foreach (var card in currentDeck)
        {
            currentlyEditingDeck.cardIds.Add(card.cardID);
        }

        // 3. 서버 매니저에게 업데이트(덮어쓰기) 요청을 보냅니다.
        await DeckSaveManager_Firebase.instance.ServerUpdateDeck(currentlyEditingDeck);
        Debug.Log($"'{currentlyEditingDeck.deckName}' 덱이 저장되었습니다.");
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

        // 리스트에 추가하고 화면 갱신
        currentDeck.Add(cardToAdd);
        UpdateDeckListUI();
    }

    /// <summary>
    /// 덱에서 카드를 한 장 뺍니다. (오른쪽 리스트에서 클릭 시 호출)
    /// </summary>
    public void RemoveCard(CardData cardToRemove)
    {
        // 리스트에서 해당 ID를 가진 첫 번째 카드를 찾습니다.
        CardData cardInDeck = currentDeck.FirstOrDefault(c => c.cardID == cardToRemove.cardID);

        if (cardInDeck != null)
        {
            currentDeck.Remove(cardInDeck);
            UpdateDeckListUI();
        }
    }

    /// <summary>
    /// [규칙 검사기] 이 카드를 덱에 넣을 수 있는지 확인합니다.
    /// </summary>
    private bool IsCardAddable(CardData card)
    {
        // 1. 30장 제한 확인
        if (currentDeck.Count >= maxDeckSize)
        {
            Debug.LogWarning("덱이 가득 찼습니다.");
            return false;
        }

        // 2. 직업 제한 확인 (내 직업이거나 중립 카드여야 함)
        string cardMemberStr = card.member.ToString(); // Enum을 문자열로 변환

        // 선택된 직업과 다르면서 && 중립(강지) 카드도 아니라면 -> 추가 불가
        if (cardMemberStr != selectedClass && card.member != ClassType.강지)
        {
            Debug.LogWarning($"'{card.cardName}' 카드는 '{selectedClass}' 덱에 추가할 수 없습니다.");
            return false;
        }

        // 3. 같은 카드 개수 확인 (일반 2장, 전설 1장 제한)
        int sameCardCount = currentDeck.Count(c => c.cardID == card.cardID);

        if (card.rarity == Rarity.전설)
        {
            if (sameCardCount >= 1)
            {
                Debug.LogWarning($"전설 카드는 덱에 한 장만 넣을 수 있습니다.");
                return false;
            }
        }
        else
        {
            if (sameCardCount >= 2)
            {
                Debug.LogWarning($"일반 카드는 덱에 최대 두 장까지만 넣을 수 있습니다.");
                return false;
            }
        }

        return true; // 모든 검사를 통과했으므로 true 반환
    }

    /// <summary>
    /// 덱 이름을 변경합니다. (InputFieldController에서 호출)
    /// </summary>
    public void UpdateDeckname(string name)
    {
        currentlyEditingDeck.deckName = name;
    }

    /// <summary>
    /// [화면 갱신] 현재 덱 리스트(오른쪽) UI를 다시 그립니다.
    /// </summary>
    private void UpdateDeckListUI()
    {
        // 1. 기존 목록 싹 지우기 (DeckPlus 버튼 빼고)
        foreach (Transform child in deckListParent)
        {
            if (child.gameObject.name != "DeckPlus")
            {
                Destroy(child.gameObject);
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
            GameObject newDeckCardUI = Instantiate(deckCardPrefab, deckListParent);

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