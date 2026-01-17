using UnityEngine;
using Firebase.Firestore;
using System.Threading.Tasks;
using System.Collections.Generic;

public class CardDatabaseManager : MonoBehaviour
{
    private FirebaseFirestore db;
    public static CardDatabaseManager instance; // 싱글톤 패턴으로 어디서든 접근 가능하게 설정

    // 모든 카드 정보를 캐시할 딕셔너리
    private Dictionary<string, CardDataFirebase> allCardsCache = null;

    private void Awake()
    {
        // --- 씬 싱글톤 패턴 구현 ---
        if (instance != null && instance != this)
        {
            // 이미 이 씬에 SinginManager가 있다면, 새로 생긴 것은 파괴
            Destroy(gameObject);
        }
        else
        {
            // 이 씬의 유일한 인스턴스로 등록
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Firebase Firestore 인스턴스 초기화
        db = FirebaseFirestore.DefaultInstance;
    }

    private void OnDestroy()
    {
        // 씬이 변경되거나 이 오브젝트가 파괴될 때,
        // static 참조를 스스로 정리(null로 만듦)합니다.
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Firestore의 "Cards" 컬렉션에 있는 모든 카드 정보를 비동기적으로 가져옵니다.
    /// </summary>
    /// <returns>카드 ID를 key로, CardData를 value로 가지는 Dictionary</returns>
    public async Task<Dictionary<string, CardDataFirebase>> GetAllCardsAsync()
    {
        if (allCardsCache != null)
        {
            return allCardsCache;
        }

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
                // Debug.Log($"불러온 카드: {card.name} (ID: {card.CardID})");
            }
        }

        allCardsCache = allCards;
        return allCards;
    }
}
