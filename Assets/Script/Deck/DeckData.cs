using System;
using System.Collections.Generic;
using Firebase.Firestore; // Firestore 속성을 사용하기 위해 추가해야 합니다.

/// <summary>
/// Firestore와 데이터를 주고받기 위한 덱 정보 클래스입니다.
/// Firestore 변환을 위해 [FirestoreData]와 [FirestoreProperty] 속성이 반드시 필요합니다.
/// </summary>
/// 
/*
[FirestoreData] // 1. 이 클래스가 Firestore 데이터 모델임을 선언합니다.
[System.Serializable]
public class DeckData
{
    // Firestore는 문서를 객체로 변환할 때, 문서의 '이름(ID)'을 자동으로 채워주지 않습니다.
    // 따라서 deckId는 Firestore에 저장할 필요가 없으므로 FirestoreProperty를 붙이지 않습니다.
    // 대신 LoadDecks 함수에서 직접 값을 할당해줍니다.
    public string deckId { get; set; }

    [FirestoreProperty] // 2. 각 프로퍼티가 Firestore 필드와 매칭됨을 선언합니다.
    public string deckName { get; set; }

    [FirestoreProperty]
    public string deckClass { get; set; }

    [FirestoreProperty]
    public List<string> cardIds { get; set; }

    /// <summary>
    /// 3. Firestore가 객체를 생성하고 데이터를 채워넣기 위해 반드시 필요한
    /// 매개변수 없는 생성자입니다.
    /// </summary>
    public DeckData()
    {
        // 비어 있어도 괜찮습니다. 존재 자체가 중요합니다.
    }

    /// <summary>
    /// 코드 내에서 새로운 덱 객체를 생성할 때 편의를 위해 사용하는 생성자입니다.
    /// </summary>
    public DeckData(string id, string name, string className)
    {
        deckId = id;
        deckName = name;
        deckClass = className;
        cardIds = new List<string>();
    }
}
*/

// 유니티 인스펙터와 JsonUtility 둘 다를 위해 필요합니다.
[System.Serializable]
public class DeckData
{
    // 2. (변경) 프로퍼티(get;set;) -> public 필드
    // 유니티의 JsonUtility가 JSON을 파싱(변환)할 수 있도록
    // 반드시 public 필드(field)로 선언해야 합니다.
    public string deckId;
    public string deckName;
    public string deckClass;
    public List<string> cardIds;

    // 3. (유지) JsonUtility와 new()를 위한 기본 생성자
    public DeckData()
    {
        // 필드 초기화
        deckId = "";
        deckName = "";
        deckClass = "";
        cardIds = new List<string>();
    }

    // (유지) 코드 내에서 객체 생성을 위한 생성자
    public DeckData(string id, string name, string className)
    {
        deckId = id;
        deckName = name;
        deckClass = className;
        cardIds = new List<string>();
    }
}
