using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DeckBuilder : MonoBehaviour
{
    [Header("UI & Prefab Settings")]
    public GameObject cardPrefab; // 유니티 에디터에서 CardPrefab을 연결
    public Transform cardListParent; // 생성된 카드들이 위치할 부모 오브젝트 (예: Scroll View의 Content)

    private Dictionary<string, CardDataFirebase> cardDatabase;

    async void Start()
    {
        Debug.Log("카드 데이터베이스 로딩을 시작합니다...");
        cardDatabase = await CardDatabaseManager.instance.GetAllCardsAsync();

        if (cardDatabase != null && cardDatabase.Count > 0)
        {
            Debug.Log("카드 데이터베이스 로딩 완료! UI 생성을 시작합니다.");
            GenerateCardListUI();
        }
        else
        {
            Debug.LogError("카드 정보를 불러오는 데 실패했습니다.");
        }
    }

    /// <summary>
    /// cardDatabase에 있는 모든 카드에 대한 UI를 생성합니다.
    /// </summary>
    void GenerateCardListUI()
    {
        foreach (var cardPair in cardDatabase)
        {
            CardDataFirebase data = cardPair.Value;

            // 1. 프리팹을 복제하여 새 카드 게임 오브젝트를 만듭니다.
            GameObject newCard = Instantiate(cardPrefab, cardListParent);

            // 2. 새 카드의 CardDisplay 스크립트 컴포넌트를 가져옵니다.
            DeckCardDisplay cardDisplay = newCard.GetComponent<DeckCardDisplay>();

            // 3. Setup 함수를 호출하여 카드 데이터를 넘겨줍니다.
            if (cardDisplay != null)
            {
                cardDisplay.Setup(data);
            }
        }
    }

    /// <summary>
    /// 주어진 카드 리스트를 UI에 표시합니다.
    /// </summary>
    /// <param name="cardsToDisplay">화면에 표시할 카드 데이터 리스트</param>
    void DisplayCards(List<CardDataFirebase> cardsToDisplay)
    {
        // 1. 기존에 표시된 카드 UI들을 모두 삭제 (화면을 깨끗하게 비움)
        foreach (Transform child in cardListParent)
        {
            Destroy(child.gameObject);
        }

        // 2. 주어진 리스트의 카드들만 새로 생성
        foreach (var data in cardsToDisplay)
        {
            GameObject newCard = Instantiate(cardPrefab, cardListParent);
            DeckCardDisplay cardDisplay = newCard.GetComponent<DeckCardDisplay>();
            if (cardDisplay != null)
            {
                cardDisplay.Setup(data);
            }
        }
    }


    /// <summary>
    /// 코스트 버튼을 클릭했을 때 호출될 함수
    /// </summary>
    public void OnSearchButtonClick(int cost)
    {
        if(cost >= 10)
        {
            List<CardDataFirebase> filteredCards10 = cardDatabase.Values
            .Where(card => card.cost >= cost)
            .ToList();

            Debug.Log($"{cost} 코스트 카드 {filteredCards10.Count}장을 찾았습니다.");
            DisplayCards(filteredCards10);
            return;
        }
        // LINQ를 사용해 조건에 맞는 카드만 필터링
        // cardDatabase.Values: 딕셔너리의 모든 CardDataFirebase 값들을 가져옴
        // .Where(card => card.cost == cost): cost가 입력된 값과 같은 카드만 골라냄
        List<CardDataFirebase> filteredCards = cardDatabase.Values
            .Where(card => card.cost == cost)
            .ToList();

        Debug.Log($"{cost} 코스트 카드 {filteredCards.Count}장을 찾았습니다.");
        DisplayCards(filteredCards);
    }

}
