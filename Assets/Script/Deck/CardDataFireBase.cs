using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;

// Firestore의 데이터를 C# 객체로 변환하기 위해 [FirestoreData] 속성을 사용합니다.
[FirestoreData]
public class CardDataFirebase
{
    [FirestoreProperty]
    public string CardID { get; set; }

    [FirestoreProperty]
    public string name { get; set; }

    [FirestoreProperty]
    public int cost { get; set; } // 코스트는 항상 값이 있으므로 int 유지

    // Nullable<int> 대신 object 타입을 사용합니다.
    // object는 참조 타입이므로 null 값을 가질 수 있습니다.
    [FirestoreProperty]
    public object attack { get; set; }

    // Nullable<int> 대신 object 타입을 사용합니다.
    [FirestoreProperty]
    public object health { get; set; }

    [FirestoreProperty]
    public string description { get; set; }

    [FirestoreProperty]
    public string tribe { get; set; }

    [FirestoreProperty]
    public string rarity { get; set; }

    [FirestoreProperty]
    public string imageUrl { get; set; }

    [FirestoreProperty]
    public string effects { get; set; }
}
