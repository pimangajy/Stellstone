using UnityEngine;
using System.Collections.Generic;

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
}
