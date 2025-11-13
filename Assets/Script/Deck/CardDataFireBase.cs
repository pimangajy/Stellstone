using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;

// Firestore의 데이터를 C# 객체로 변환하기 위해 [FirestoreData] 속성을 사용합니다.
[FirestoreData]
public class CardDataFirebase
{
    // 카드 아이디
    [FirestoreProperty]
    public string CardID { get; set; }

    // 이름
    [FirestoreProperty]
    public string name { get; set; }

    // 코스트
    [FirestoreProperty]
    public int cost { get; set; } // 코스트는 항상 값이 있으므로 int 유지

    // Nullable<int> 대신 object 타입을 사용합니다.
    // object는 참조 타입이므로 null 값을 가질 수 있습니다.
    [FirestoreProperty]
    public object attack { get; set; }

    // Nullable<int> 대신 object 타입을 사용합니다.
    [FirestoreProperty]
    public object health { get; set; }

    // 효과 텍스트
    [FirestoreProperty]
    public string description { get; set; }

    // 종족
    [FirestoreProperty]
    public string tribe { get; set; }

    // 확장팩
    [FirestoreProperty]
    public string expansion { get; set; }

    // 카드 종류
    [FirestoreProperty]
    public string type { get; set; }

    // 카드 직업
    [FirestoreProperty]
    public string member { get; set; }

    // 레어도
    [FirestoreProperty]
    public string rarity { get; set; }

    // 이미지 경로
    [FirestoreProperty]
    public string imageUrl { get; set; }

    // 효과 
    [FirestoreProperty]
    public string effects { get; set; }
}
