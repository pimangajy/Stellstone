using UnityEngine;
using Firebase.Firestore;
using System.Threading.Tasks;
using System.Collections.Generic;

public class CardDatabaseManager : MonoBehaviour
{
    private FirebaseFirestore db;
    public static CardDatabaseManager instance; // 싱글톤 패턴으로 어디서든 접근 가능하게 설정

    void Awake()
    {
        // 싱글톤 인스턴스 설정
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않도록 설정
        }
        else
        {
            Destroy(gameObject);
        }

        // Firebase Firestore 인스턴스 초기화
        db = FirebaseFirestore.DefaultInstance;
    }

    /// <summary>
    /// Firestore의 "Cards" 컬렉션에 있는 모든 카드 정보를 비동기적으로 가져옵니다.
    /// </summary>
    /// <returns>카드 ID를 key로, CardData를 value로 가지는 Dictionary</returns>
    public async Task<Dictionary<string, CardDataFirebase>> GetAllCardsAsync()
    {
        // "Cards" 컬렉션 참조
        CollectionReference cardsRef = db.Collection("Cards");

        // 비동기적으로 컬렉션의 모든 문서를 가져옵니다.
        // await 키워드는 데이터 수신이 완료될 때까지 여기서 코드 실행을 잠시 멈춥니다. (게임은 멈추지 않음)
        QuerySnapshot snapshot = await cardsRef.GetSnapshotAsync();

        Dictionary<string, CardDataFirebase> allCards = new Dictionary<string, CardDataFirebase>();

        foreach (DocumentSnapshot document in snapshot.Documents)
        {
            // DocumentSnapshot을 CardData 클래스 객체로 자동 변환합니다.
            CardDataFirebase card = document.ConvertTo<CardDataFirebase>();
            if (card != null)
            {
                // 딕셔너리에 카드 ID를 키로 하여 카드 데이터를 추가합니다.
                allCards.Add(card.CardID, card);
                Debug.Log($"불러온 카드: {card.name} (ID: {card.CardID})");
            }
        }

        Debug.Log($"총 {allCards.Count}개의 카드를 성공적으로 불러왔습니다.");
        return allCards;
    }
}
