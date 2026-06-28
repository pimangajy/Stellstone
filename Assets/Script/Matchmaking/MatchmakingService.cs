using Firebase.Firestore;
using Firebase.Auth;
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Linq; // Linq
using System.Collections.Generic; // List

/// <summary>
/// Firestore를 이용한 매치메이킹 로직을 처리합니다.
/// UI가 없으며, MatchingManager와 이벤트로 통신합니다.
/// </summary>
public class MatchmakingService : MonoBehaviour
{
    private FirebaseFirestore db;
    private FirebaseAuth auth;
    private ListenerRegistration matchmakingListener;
    private string currentUserId;

    public event Action OnMatchmakingStarted;
    public event Action OnMatchmakingCancelled;
    public event Action<string> OnMatchmakingFailed;
    public event Action<string, string> OnMatchFound; // (gameId, opponentUid)

    [SerializeField] private string gameScene;

    void Awake()
    {
        db = FirebaseFirestore.DefaultInstance;
        auth = FirebaseAuth.DefaultInstance;

        if (auth.CurrentUser != null)
        {
            currentUserId = auth.CurrentUser.UserId;
        }
        auth.StateChanged += OnAuthStateChanged;

        OnMatchFound += GoGame;
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        currentUserId = auth.CurrentUser?.UserId;
        if (string.IsNullOrEmpty(currentUserId))
        {
            StopListening();
        }
    }

    /// <summary>
    /// 매치메이킹을 시작합니다. (수정된 로직)
    /// 1. 밖에서 상대를 '검색'합니다.
    /// 2. 찾았으면 '트랜잭션'으로 '찜하기'를 시도합니다.
    /// 3. 못 찾았거나 찜하기에 실패하면 '대기' 상태로 전환합니다.
    /// </summary>
    public async void StartMatchmaking(DeckData selectedDeck)
    {
        if (string.IsNullOrEmpty(currentUserId) || selectedDeck == null)
        {
            Debug.LogError("로그인한 유저가 없거나 덱이 선택되지 않았습니다.");
            OnMatchmakingFailed?.Invoke("로그인 또는 덱 선택이 필요합니다.");
            return;
        }

        Debug.Log("매치메킹을 시작합니다...");
        int myLevel = 1; // TODO: 실제 유저 레벨 또는 MMR

        // 1. 내 매칭 정보 준비 (Passive Waiter가 될 경우 사용)
        MatchmakingEntry myEntry = new MatchmakingEntry
        {
            status = "waiting",
            level = myLevel,
            deckId = selectedDeck.deckId,
            playerName = auth.CurrentUser.DisplayName ?? "Player"
        };

        QuerySnapshot potentialOpponentsSnapshot = null;
        try
        {
            // --- [1단계: 검색 (트랜잭션 *밖*)] ---
            // '!=' 쿼리는 단일 필드에서만 작동하거나 복합 인덱스가 필요할 수 있습니다.
            // 여기서는 FieldPath.DocumentId를 사용해봅니다. (작동하지 않으면 Linq로 후처리)
            Query potentialOpponentsQuery = db.Collection("MatchmakingQueue")
                .WhereEqualTo("status", "waiting")
                .WhereEqualTo("level", myLevel)
                .WhereNotEqualTo(FieldPath.DocumentId, currentUserId)
                .Limit(1); // 1명만 찾습니다.

            potentialOpponentsSnapshot = await potentialOpponentsQuery.GetSnapshotAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"매치메이킹 상대 검색 실패: {e.Message}. 대기열 등록으로 전환합니다.");
            // 쿼리 자체에 실패하면(예: 인덱스 문제) 즉시 '대기' 상태로 전환
            await RegisterAsWaiter(myEntry);
            return;
        }

        // --- [2단계: 분기] ---
        DocumentSnapshot opponentDoc = potentialOpponentsSnapshot.Documents.FirstOrDefault();

        if (opponentDoc != null)
        {
            // --- [3단계: 찜하기 (트랜잭션 *안*)] ---
            Debug.Log($"상대 발견: {opponentDoc.Id}. 찜하기(트랜잭션) 시도...");

            // [수정] 찜할 상대의 DocumentReference를 미리 가져옵니다.
            DocumentReference opponentRef = opponentDoc.Reference;
            string gameId = Guid.NewGuid().ToString();

            try
            {
                // 트랜잭션을 실행합니다.
                await db.RunTransactionAsync(async transaction =>
                {
                    // [수정] 이제 DocumentReference로 GetSnapshotAsync를 호출합니다.
                    DocumentSnapshot opponentLatestSnapshot = await transaction.GetSnapshotAsync(opponentRef);

                    if (!opponentLatestSnapshot.Exists)
                    {
                        // 상대가 그새 큐를 나감
                        throw new Exception("상대가 큐를 나갔습니다.");
                    }

                    MatchmakingEntry opponentData = opponentLatestSnapshot.ConvertTo<MatchmakingEntry>();

                    // [핵심] 상태가 여전히 "waiting"인지 트랜잭션 안에서 재확인
                    if (opponentData.status == "waiting")
                    {
                        // "찜하기" 성공! 상대 문서를 업데이트합니다.
                        Dictionary<string, object> updates = new Dictionary<string, object>
                        {
                            { "status", "matched" },
                            { "opponentUid", currentUserId },
                            { "gameId", gameId }
                        };
                        transaction.Update(opponentRef, updates);
                    }
                    else
                    {
                        // "찜하기" 실패 (다른 사람이 채갔음)
                        throw new Exception("상대를 찜하는 데 실패했습니다 (다른 유저가 매칭됨).");
                    }
                });

                // --- [트랜잭션 성공!] ---
                Debug.Log($"매칭 확정! (Active Seeker 성공). 게임 ID: {gameId}, 상대: {opponentDoc.Id}");
                OnMatchFound?.Invoke(gameId, opponentDoc.Id);
            }
            catch (Exception e)
            {
                // --- [트랜잭션 실패!] (찜하기 실패 또는 기타 오류) ---
                Debug.LogWarning($"찜하기 실패: {e.Message}. '대기' 상태로 전환합니다.");
                // "Active Seeker"에 실패했으니, "Passive Waiter"로 전환합니다.
                await RegisterAsWaiter(myEntry);
            }
        }
        else
        {
            // --- [상대 못 찾음] ---
            Debug.Log("상대를 찾지 못했습니다. '대기' 상태로 전환합니다.");
            await RegisterAsWaiter(myEntry);
        }
    }

    /// <summary>
    /// "Passive Waiter" (수동적 대기자)가 되기 위해 큐에 등록하고 리스너를 시작합니다.
    /// (중복 로직을 별도 함수로 분리)
    /// </summary>
    private async Task RegisterAsWaiter(MatchmakingEntry myEntry)
    {
        try
        {
            DocumentReference myQueueDoc = db.Collection("MatchmakingQueue").Document(currentUserId);
            await myQueueDoc.SetAsync(myEntry); // 'myEntry' 객체로 내 문서 생성

            OnMatchmakingStarted?.Invoke(); // UI에 "찾는 중..." 표시
            ListenForMatch(currentUserId); // 내 문서 구독 시작

            // 싱글 테스트
            string gameId = Guid.NewGuid().ToString();
            myEntry.gameId = gameId;
            OnMatchFound?.Invoke(myEntry.gameId, "bot id");
        }
        catch (Exception e)
        {
            Debug.LogError($"대기열 등록 실패: {e.Message}");
            OnMatchmakingFailed?.Invoke($"대기열 등록 중 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 매칭이 되었는지 실시간으로 감지합니다. (Passive Waiter 로직)
    /// </summary>
    private void ListenForMatch(string userId)
    {
        StopListening();
        DocumentReference myQueueDoc = db.Collection("MatchmakingQueue").Document(userId);
        matchmakingListener = myQueueDoc.Listen(snapshot =>
        {
            if (snapshot.Exists)
            {
                MatchmakingEntry entry = snapshot.ConvertTo<MatchmakingEntry>();
                if (entry.status == "matched")
                {
                    Debug.Log($"매칭 성공! (상대가 나를 찾음) 상대: {entry.opponentUid}, 게임 ID: {entry.gameId}");
                    StopListening();
                    OnMatchFound?.Invoke(entry.gameId, entry.opponentUid);
                    myQueueDoc.DeleteAsync();
                }
            }
            else
            {
                Debug.Log("매치메이킹 큐에서 문서가 사라졌습니다. (취소 또는 타임아웃)");
                StopListening();
                OnMatchmakingCancelled?.Invoke();
            }
        });
    }

    /// <summary>
    /// 유저가 직접 "대전 찾기"를 취소합니다.
    /// </summary>
    public async void CancelMatchmaking()
    {
        if (string.IsNullOrEmpty(currentUserId)) return;
        Debug.Log("매치메이킹을 취소합니다...");
        StopListening();
        try
        {
            DocumentReference myQueueDoc = db.Collection("MatchmakingQueue").Document(currentUserId);
            await myQueueDoc.DeleteAsync();
            OnMatchmakingCancelled?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"매치메킹 취소(문서 삭제) 중 오류: {e.Message}");
            OnMatchmakingCancelled?.Invoke();
        }
    }

    public void GoGame(string GameID, string i)
    {
        GameClient.Instance.GameId = GameID;
        SceneLoader.instance.LoadSceneByName(gameScene);
    }

    void OnDestroy()
    {
        StopListening();
        auth.StateChanged -= OnAuthStateChanged;
    }

    private void StopListening()
    {
        if (matchmakingListener != null)
        {
            matchmakingListener.Stop();
            matchmakingListener = null;
        }
    }
}