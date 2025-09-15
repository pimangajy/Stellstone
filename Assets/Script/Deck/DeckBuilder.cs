using UnityEngine;
using System.Collections.Generic;

public class DeckBuilder : MonoBehaviour
{
    // 모든 카드 정보를 저장할 딕셔너리
    private Dictionary<string, CardDataFireBase> cardDatabase;

    async void Start()
    {
        Debug.Log("카드 데이터베이스 로딩을 시작합니다...");
        // CardDatabaseManager의 인스턴스를 통해 모든 카드 정보를 가져옵니다.
        // await를 사용했으므로 로딩이 끝날 때까지 기다립니다.
        cardDatabase = await CardDatabaseManager.instance.GetAllCardsAsync();

        // 로딩이 완료된 후 실행할 로직
        if (cardDatabase != null && cardDatabase.Count > 0)
        {
            Debug.Log("카드 데이터베이스 로딩 완료!");
            // 예시: 특정 카드 정보 출력해보기
            PrintSpecificCardInfo("cards-gangzi-004");
        }
        else
        {
            Debug.LogError("카드 정보를 불러오는 데 실패했습니다.");
        }
    }

    void PrintSpecificCardInfo(string cardID)
    {
        // 딕셔너리에서 카드 ID로 정보 찾기
        if (cardDatabase.TryGetValue(cardID, out CardDataFireBase card))
        {
            Debug.Log($"--- 카드 정보: {cardID} ---");
            Debug.Log($"이름: {card.name}");
            Debug.Log($"코스트: {card.cost}");
            Debug.Log($"공격력/체력: {card.attack}/{card.health}");
            Debug.Log($"설명: {card.description}");
        }
        else
        {
            Debug.LogWarning($"ID가 '{cardID}'인 카드를 찾을 수 없습니다.");
        }
    }
}
