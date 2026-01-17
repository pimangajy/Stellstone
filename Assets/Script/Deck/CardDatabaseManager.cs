using UnityEngine;
using Firebase.Firestore;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// 게임 전체에서 단 하나만 존재하는 '카드 도서관' 관리자입니다.
/// Firebase(서버)에서 모든 카드 정보를 가져와서 저장해두고,
/// 다른 스크립트들이 "카드 정보 줘!"라고 하면 꺼내주는 역할을 합니다.
/// </summary>
public class CardDatabaseManager : MonoBehaviour
{
    // Firebase Firestore 데이터베이스를 다루기 위한 도구입니다.
    private FirebaseFirestore db;

    // '싱글톤(Singleton)' 패턴: 
    // 이 변수(instance)를 통해 어디서든 이 스크립트에 접근할 수 있게 합니다.
    // 예: CardDatabaseManager.instance.함수이름()
    public static CardDatabaseManager instance;

    // 한 번 서버에서 가져온 카드 정보는 매번 다시 가져오지 않고 여기에 저장(캐시)해둡니다.
    // string: 카드 ID (주민등록번호 같은 고유 키)
    // CardDataFirebase: 실제 카드 정보 (이름, 공격력, 체력 등)
    private Dictionary<string, CardDataFirebase> allCardsCache = null;

    // 게임 오브젝트가 생성될 때 가장 먼저 실행되는 함수입니다.
    private void Awake()
    {
        // --- 씬 싱글톤 패턴 구현 ---
        // 만약 게임 세상에 나(CardDatabaseManager) 말고 또 다른 내가 있다면?
        if (instance != null && instance != this)
        {
            // 가짜(나중에 생긴 것)는 파괴합니다. 오직 하나만 유지하기 위함입니다.
            Destroy(gameObject);
        }
        else
        {
            // 내가 진짜라면 전역 변수(instance)에 나를 등록합니다.
            instance = this;
            // 씬(장면)이 바뀌어도 나를 삭제하지 말고 유지하라고 유니티에 알립니다.
            DontDestroyOnLoad(gameObject);
        }

        // Firebase Firestore 도구를 초기화(준비)합니다.
        db = FirebaseFirestore.DefaultInstance;
    }

    // 이 오브젝트가 파괴될 때(게임 종료 등) 실행됩니다.
    private void OnDestroy()
    {
        // 내가 사라지니까, 전역 변수 연결도 끊어줍니다.
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Firestore 서버의 "Cards"라는 방(컬렉션)에 있는 모든 카드 종이를 가져옵니다.
    /// 'async Task'는 "시간이 좀 걸리니까 완료될 때까지 기다려줘"라는 뜻의 비동기 함수입니다.
    /// </summary>
    /// <returns>카드 ID를 열쇠(Key)로, 카드 정보를 내용물(Value)로 하는 사전(Dictionary)을 돌려줍니다.</returns>
    public async Task<Dictionary<string, CardDataFirebase>> GetAllCardsAsync()
    {
        // 만약 이미 캐시(저장소)에 받아둔 데이터가 있다면?
        if (allCardsCache != null)
        {
            // 서버에 또 요청하지 않고, 저장해둔 것을 바로 줍니다. (데이터 절약, 속도 향상)
            return allCardsCache;
        }

        // "Cards"라는 이름의 컬렉션(폴더)을 가리킵니다.
        CollectionReference cardsRef = db.Collection("Cards");

        // 서버에게 "거기 있는 문서 다 내놔"라고 요청합니다.
        // await: 서버가 응답할 때까지 여기서 잠시 대기합니다. (게임이 멈추지는 않습니다)
        QuerySnapshot snapshot = await cardsRef.GetSnapshotAsync();

        // 데이터를 담을 빈 사전(Dictionary)을 만듭니다.
        Dictionary<string, CardDataFirebase> allCards = new Dictionary<string, CardDataFirebase>();

        // 받아온 문서 뭉치(snapshot)에서 종이(document)를 한 장씩 꺼내봅니다.
        foreach (DocumentSnapshot document in snapshot.Documents)
        {
            // 서버의 문서 데이터를 우리가 쓰기 편한 C# 클래스(CardDataFirebase) 형태로 변환합니다.
            CardDataFirebase card = document.ConvertTo<CardDataFirebase>();

            // 변환이 잘 되었다면
            if (card != null)
            {
                // 사전에 추가합니다. (ID를 알면 카드 정보를 바로 찾을 수 있게)
                allCards.Add(card.CardID, card);
                // Debug.Log($"불러온 카드: {card.name} (ID: {card.CardID})"); // 확인용 로그
            }
        }

        // 다음에 또 쓸 수 있게 캐시 변수에 저장해둡니다.
        allCardsCache = allCards;

        // 정리된 카드 목록을 반환합니다.
        return allCards;
    }
}