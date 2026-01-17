using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

public class DeckManager : MonoBehaviour
{
    public static DeckManager instance;

    [Header("덱 규칙 설정")]
    [SerializeField] private int maxDeckSize = 30;

    [Header("UI 연결")]
    public Transform deckListParent;
    public GameObject deckCardPrefab;

    private string selectedClass = ""; // DeckData는 여전히 string으로 직업을 저장함

    private List<CardData> currentDeck = new List<CardData>();
    private DeckData currentlyEditingDeck;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    public void StartNewDeck(DeckData newDeck)
    {
        currentlyEditingDeck = newDeck;
        selectedClass = newDeck.deckClass;
        currentDeck.Clear();
        UpdateDeckListUI();
        Debug.Log($"'{newDeck.deckName}' 만들기를 시작합니다.");
    }

    public void LoadDeck(DeckData deckToLoad, List<CardData> cards)
    {
        currentlyEditingDeck = deckToLoad;
        selectedClass = deckToLoad.deckClass;
        currentDeck = new List<CardData>(cards);
        UpdateDeckListUI();
        Debug.Log($"'{deckToLoad.deckName}' 덱을 불러왔습니다.");
    }

    public async void DeleteDeck()
    {
        await DeckSaveManager_Firebase.instance.DeleteDeck(currentlyEditingDeck.deckId);
        currentlyEditingDeck = null;
        selectedClass = null;
        UpdateDeckListUI();
    }

    public async void SaveCurrentDeck()
    {
        if (currentlyEditingDeck == null)
        {
            Debug.LogWarning("저장할 덱이 선택되지 않았습니다.");
            return;
        }

        currentlyEditingDeck.cardIds.Clear();

        foreach (var card in currentDeck)
        {
            currentlyEditingDeck.cardIds.Add(card.cardID);
        }

        await DeckSaveManager_Firebase.instance.ServerUpdateDeck(currentlyEditingDeck);
        Debug.Log($"'{currentlyEditingDeck.deckName}' 덱이 저장되었습니다.");
    }

    public void AddCard(CardData cardToAdd)
    {
        if (currentlyEditingDeck == null)
        {
            Debug.LogWarning("먼저 덱을 선택하거나 새로 만들어야 카드를 추가할 수 있습니다.");
            return;
        }

        if (!IsCardAddable(cardToAdd))
        {
            return;
        }

        currentDeck.Add(cardToAdd);
        UpdateDeckListUI();
    }

    public void RemoveCard(CardData cardToRemove)
    {
        CardData cardInDeck = currentDeck.FirstOrDefault(c => c.cardID == cardToRemove.cardID);

        if (cardInDeck != null)
        {
            currentDeck.Remove(cardInDeck);
            UpdateDeckListUI();
        }
    }

    private bool IsCardAddable(CardData card)
    {
        if (currentDeck.Count >= maxDeckSize)
        {
            Debug.LogWarning("덱이 가득 찼습니다.");
            return false;
        }

        // [수정] 직업 제한 확인 (Enum vs String 비교)
        // selectedClass는 string("강지")이고 card.member는 Enum(ClassType.강지)입니다.
        // Enum을 string으로 변환하여 비교하거나, string을 Enum으로 변환하여 비교합니다.

        string cardMemberStr = card.member.ToString();

        if (cardMemberStr != selectedClass && card.member != ClassType.강지)
        {
            Debug.LogWarning($"'{card.cardName}' 카드는 '{selectedClass}' 덱에 추가할 수 없습니다.");
            return false;
        }

        int sameCardCount = currentDeck.Count(c => c.cardID == card.cardID);

        // [수정] 희귀도 확인 (Enum 사용)
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

        return true;
    }

    public void UpdateDeckname(string name)
    {
        currentlyEditingDeck.deckName = name;
    }

    private void UpdateDeckListUI()
    {
        foreach (Transform child in deckListParent)
        {
            if(child.gameObject.name != "DeckPlus")
            {
                Destroy(child.gameObject);
            }
        }

        var groupedAndSortedDeck = currentDeck
            .GroupBy(card => card.cardID)
            .Select(group => new
            {
                Card = group.First(),
                Count = group.Count()
            })
            .OrderBy(item => item.Card.manaCost)
            .ThenBy(item => item.Card.cardName);

        foreach (var item in groupedAndSortedDeck)
        {
            GameObject newDeckCardUI = Instantiate(deckCardPrefab, deckListParent);

            var itemDisplay = newDeckCardUI.GetComponent<ICardDataHolder>() as DeckListItemDisplay;
            if (itemDisplay != null)
            {
                itemDisplay.Setup(item.Card, item.Count);
            }

            CardInteraction interaction = newDeckCardUI.GetComponent<CardInteraction>();
            if (interaction != null)
            {
                interaction.location = CardInteraction.CardLocation.Deck;
            }
        }
    }
}