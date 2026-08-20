using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Firestore를 이용한 매치메이킹 로직을 처리합니다.
/// UI에 의존하지 않으며, MatchingManager가 이벤트를 구독하여 UI를 갱신합니다.
/// </summary>
public class MatchmakingService : MonoBehaviour
{
    private FirebaseFirestore db;
    private FirebaseAuth auth;
    private ListenerRegistration matchmakingListener;
    private string currentUserId;
    private Coroutine botMatchCoroutine;

    [Header("설정")]
    [SerializeField] private string gameScene = "Battle";
    [Tooltip("상대를 찾지 못했을 때 봇 대전으로 연결할지 여부")]
    [SerializeField] private bool enableBotMatchFallback = true;
    [Tooltip("봇 대전으로 연결되기 전 '매칭 찾는 중' 대기 시간(초)")]
    [SerializeField] private float botMatchDelaySeconds = 2.5f;

    public event Action OnMatchmakingStarted;
    public event Action OnMatchmakingCancelled;
    public event Action<string> OnMatchmakingFailed;
    public event Action<string, string> OnMatchFound; // (gameId, opponentUid)

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
    /// 매치메이킹을 시작합니다.
    /// 1. 큐에서 상대를 '검색'합니다.
    /// 2. 찾았으면 '트랜잭션'으로 '낚아채기'를 시도합니다.
    /// 3. 못 찾았거나 낚아채기에 실패하면 '대기' 상태로 전환합니다.
    /// </summary>
    public async void StartMatchmaking(DeckData selectedDeck)
    {
        if (string.IsNullOrEmpty(currentUserId) || selectedDeck == null)
        {
            Debug.LogError("[Matchmaking] 로그인이 안되어 있거나 덱이 선택되지 않았습니다.");
            OnMatchmakingFailed?.Invoke("로그인 또는 덱 선택이 필요합니다.");
            return;
        }

        Debug.Log("[Matchmaking] 매치메이킹을 시작합니다...");

        // 내 정보 준비
        int myLevel = 1; // TODO: 실제 유저 레벨 연동
        MatchmakingEntry myEntry = new MatchmakingEntry
        {
            deckId = selectedDeck.deckId,
            level = myLevel,
            status = "waiting"
        };

        // --- [1단계: 검색] ---
        QuerySnapshot potentialOpponentsSnapshot;
        try
        {
            Query potentialOpponentsQuery = db.Collection("MatchmakingQueue")
                .WhereEqualTo("status", "waiting")
                .WhereEqualTo("level", myLevel)
                .WhereNotEqualTo(FieldPath.DocumentId, currentUserId)
                .Limit(1);

            potentialOpponentsSnapshot = await potentialOpponentsQuery.GetSnapshotAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Matchmaking] 큐 검색 오류: {e.Message}. 대기열 등록으로 전환합니다.");
            await RegisterAsWaiter(myEntry);
            return;
        }

        // --- [2단계: 판별] ---
        DocumentSnapshot opponentDoc = potentialOpponentsSnapshot.Documents.FirstOrDefault();

        if (opponentDoc != null)
        {
            // --- [3단계: 낚아채기 (트랜잭션)] ---
            Debug.Log($"[Matchmaking] 상대 발견: {opponentDoc.Id}. 낚아채기 시도...");
            DocumentReference opponentRef = opponentDoc.Reference;
            string gameId = Guid.NewGuid().ToString();

            try
            {
                await db.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot opponentLatestSnapshot = await transaction.GetSnapshotAsync(opponentRef);
                    if (!opponentLatestSnapshot.Exists)
                    {
                        throw new Exception("상대가 큐를 나갔습니다.");
                    }

                    MatchmakingEntry opponentData = opponentLatestSnapshot.ConvertTo<MatchmakingEntry>();
                    if (opponentData.status == "waiting")
                    {
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
                        throw new Exception("다른 유저와 매칭되었습니다.");
                    }
                });

                Debug.Log($"[Matchmaking] ⚔️ 매칭 확정! 게임 ID: {gameId}, 상대: {opponentDoc.Id}");
                OnMatchFound?.Invoke(gameId, opponentDoc.Id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Matchmaking] 낚아채기 실패: {e.Message}. 대기 상태로 전환합니다.");
                await RegisterAsWaiter(myEntry);
            }
        }
        else
        {
            Debug.Log("[Matchmaking] 대기 중인 상대를 찾지 못했습니다. '대기' 상태로 전환합니다.");
            await RegisterAsWaiter(myEntry);
        }
    }

    /// <summary>
    /// 대기열에 등록하고 상대를 기다립니다.
    /// </summary>
    private async Task RegisterAsWaiter(MatchmakingEntry myEntry)
    {
        try
        {
            DocumentReference myQueueDoc = db.Collection("MatchmakingQueue").Document(currentUserId);
            await myQueueDoc.SetAsync(myEntry);

            // UI에 "매칭 찾는 중..." 화면 표시
            OnMatchmakingStarted?.Invoke();
            ListenForMatch(currentUserId);

            // 봇 대전 폴백이 켜져 있는 경우 지정된 시간 동안 대기 화면을 보여준 뒤 봇 매칭 시작
            if (enableBotMatchFallback)
            {
                if (botMatchCoroutine != null) StopCoroutine(botMatchCoroutine);
                botMatchCoroutine = StartCoroutine(DelayedBotMatch(myEntry));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Matchmaking] 대기열 등록 실패: {e.Message}");
            OnMatchmakingFailed?.Invoke($"대기열 등록 중 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 지정된 대기 시간 동안 '매칭 중' 화면을 띄운 뒤 봇 대전을 시작합니다.
    /// </summary>
    private IEnumerator DelayedBotMatch(MatchmakingEntry myEntry)
    {
        yield return new WaitForSeconds(botMatchDelaySeconds);

        string gameId = Guid.NewGuid().ToString();
        myEntry.gameId = gameId;
        Debug.Log($"[Matchmaking] 🤖 봇 대전 연결 완료! (GameId: {gameId})");

        StopListening();
        OnMatchFound?.Invoke(myEntry.gameId, "bot id");
    }

    /// <summary>
    /// 실시간으로 매칭 성사 여부를 감지합니다.
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
                    Debug.Log($"[Matchmaking] ⚔️ 매칭 성사! (상대가 나를 찾음) 상대: {entry.opponentUid}, 게임 ID: {entry.gameId}");
                    if (botMatchCoroutine != null)
                    {
                        StopCoroutine(botMatchCoroutine);
                        botMatchCoroutine = null;
                    }
                    StopListening();
                    OnMatchFound?.Invoke(entry.gameId, entry.opponentUid);
                    myQueueDoc.DeleteAsync();
                }
            }
            else
            {
                Debug.Log("[Matchmaking] 매치메이킹 큐에서 문서가 제거되었습니다.");
                StopListening();
                OnMatchmakingCancelled?.Invoke();
            }
        });
    }

    /// <summary>
    /// 매칭 취소
    /// </summary>
    public async void CancelMatchmaking()
    {
        if (botMatchCoroutine != null)
        {
            StopCoroutine(botMatchCoroutine);
            botMatchCoroutine = null;
        }

        if (string.IsNullOrEmpty(currentUserId)) return;
        Debug.Log("[Matchmaking] 매치메이킹을 취소합니다...");
        StopListening();
        try
        {
            DocumentReference myQueueDoc = db.Collection("MatchmakingQueue").Document(currentUserId);
            await myQueueDoc.DeleteAsync();
            OnMatchmakingCancelled?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Matchmaking] 취소 중 오류: {e.Message}");
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
        if (botMatchCoroutine != null)
        {
            StopCoroutine(botMatchCoroutine);
            botMatchCoroutine = null;
        }
        StopListening();
        if (auth != null)
        {
            auth.StateChanged -= OnAuthStateChanged;
        }
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