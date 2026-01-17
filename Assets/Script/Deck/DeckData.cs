using System;
using System.Collections.Generic;
using Firebase.Firestore;

/// <summary>
/// 하나의 '덱' 정보를 담는 가방(Data Class)입니다.
/// 이 클래스는 저장(Save)과 로드(Load)를 위해 사용됩니다.
/// </summary>

// [System.Serializable]: 유니티가 이 클래스의 내용을 파일로 저장하거나(JSON), 인스펙터 창에서 보여줄 수 있게 합니다.
[System.Serializable]
public class DeckData
{
    // 덱의 고유 ID (주민등록번호). Firebase 문서 이름으로도 쓰입니다.
    // public 필드로 선언해야 JsonUtility가 데이터를 읽고 쓸 수 있습니다.
    public string deckId;

    // 유저가 지은 덱 이름 (예: "천하무적 마법사")
    public string deckName;

    // 덱의 직업 (예: "Mage", "Warrior")
    public string deckClass;

    // 덱에 포함된 카드들의 ID 리스트 (예: ["card_001", "card_005", ...])
    // 실제 카드 객체 전체를 저장하면 용량이 너무 커지므로 ID만 저장합니다.
    public List<string> cardIds;

    // 기본 생성자: new DeckData() 할 때 호출됨
    public DeckData()
    {
        // 변수들을 빈 값으로 초기화해줍니다. 안 그러면 에러 날 수 있어요.
        deckId = "";
        deckName = "";
        deckClass = "";
        cardIds = new List<string>();
    }

    // 편의용 생성자: 덱을 만들 때 값을 바로 넣으면서 생성할 수 있게 해줍니다.
    public DeckData(string id, string name, string className)
    {
        deckId = id;
        deckName = name;
        deckClass = className;
        cardIds = new List<string>(); // 카드는 처음에 없으니까 빈 리스트
    }
}