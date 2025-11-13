using Firebase.Firestore;

/// <summary>
/// Firestore의 'MatchmakingQueue' 컬렉션에 저장될 문서의 데이터 구조입니다.
/// </summary>
[FirestoreData]
public class MatchmakingEntry
{
    /// <summary>
    /// 현재 매칭 상태 (예: "waiting", "matched")
    /// </summary>
    [FirestoreProperty]
    public string status { get; set; }

    /// <summary>
    /// 매칭에 사용할 유저의 점수 또는 레벨
    /// </summary>
    [FirestoreProperty]
    public int level { get; set; } // TODO: 실제 유저 레벨/점수 시스템과 연동

    /// <summary>
    /// 유저가 선택한 덱의 ID
    /// </summary>
    [FirestoreProperty]
    public string deckId { get; set; }

    /// <summary>
    /// 유저의 닉네임 (상대방에게 표시될 수 있음)
    /// </summary>
    [FirestoreProperty]
    public string playerName { get; set; }

    /// <summary>
    /// 매칭이 성사된 상대방의 UID (매칭 성공 시 채워짐)
    /// </summary>
    [FirestoreProperty]
    public string opponentUid { get; set; }

    /// <summary>
    /// 매칭이 성사된 게임방의 고유 ID (매칭 성공 시 채워짐)
    /// </summary>
    [FirestoreProperty]
    public string gameId { get; set; }
}