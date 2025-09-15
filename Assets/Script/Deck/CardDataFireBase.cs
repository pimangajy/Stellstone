using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;

// Firestore의 데이터를 C# 객체로 변환하기 위해 [FirestoreData] 속성을 사용합니다.
[FirestoreData]
public class CardDataFireBase
{
    // Firestore 문서의 필드 이름과 정확히 일치해야 합니다.
    [FirestoreProperty]
    public string CardID { get; set; }

    [FirestoreProperty]
    public string name { get; set; }

    [FirestoreProperty]
    public int cost { get; set; }

    [FirestoreProperty]
    public int attack { get; set; }

    [FirestoreProperty]
    public int health { get; set; }

    [FirestoreProperty]
    public string description { get; set; }

    [FirestoreProperty]
    public string tribe { get; set; }

    [FirestoreProperty]
    public string rarity { get; set; }

    [FirestoreProperty]
    public string imageUrl { get; set; }

    [FirestoreProperty]
    public string effects { get; set; } // effects는 JSON 형태의 문자열이므로 string으로 받습니다.
}
