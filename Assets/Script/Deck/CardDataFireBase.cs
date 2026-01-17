using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;

// [FirestoreData]: 이 클래스가 Firestore 데이터베이스와 1:1로 연결되는 설계도임을 알려줍니다.
[FirestoreData]
public class CardDataFirebase
{
    // [FirestoreProperty]: 이 변수가 데이터베이스의 필드(항목)와 연결된다는 표시입니다.
    // { get; set; }은 값을 읽고(get) 쓸(set) 수 있다는 뜻입니다.

    // 카드의 고유 식별 번호 (예: "card_001")
    [FirestoreProperty]
    public string CardID { get; set; }

    // 카드의 이름 (예: "화염구")
    [FirestoreProperty]
    public string name { get; set; }

    // 카드를 내기 위해 필요한 마나 비용
    [FirestoreProperty]
    public int cost { get; set; } // 숫자는 무조건 존재하므로 기본 int형 사용

    // 공격력 (하수인은 공격력이 있지만, 주문은 없을 수 있습니다)
    // object 타입을 쓴 이유: 숫자일 수도 있고, null(없음)일 수도 있기 때문입니다.
    [FirestoreProperty]
    public object attack { get; set; }

    // 체력 (위와 마찬가지로 주문 카드는 체력이 없으므로 object 사용)
    [FirestoreProperty]
    public object health { get; set; }

    // 카드 하단에 적혀있는 효과 설명 텍스트
    [FirestoreProperty]
    public string description { get; set; }

    // 종족 값 (예: "야수", "용족", "기계" 등)
    [FirestoreProperty]
    public string tribe { get; set; }

    // 어느 확장팩에서 나온 카드인지 (예: "오리지널", "낙스라마스")
    [FirestoreProperty]
    public string expansion { get; set; }

    // 카드의 종류 (예: "하수인", "주문", "무기")
    [FirestoreProperty]
    public string type { get; set; }

    // 직업 전용 카드인지 (예: "마법사", "전사", "중립")
    [FirestoreProperty]
    public string member { get; set; }

    // 희귀도 (예: "일반", "희귀", "영웅", "전설")
    [FirestoreProperty]
    public string rarity { get; set; }

    // 카드 일러스트 이미지의 인터넷 주소(URL)
    [FirestoreProperty]
    public string imageUrl { get; set; }

    // 실제 게임 로직에서 사용될 효과 코드나 ID
    [FirestoreProperty]
    public string effects { get; set; }
}