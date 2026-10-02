using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 서버 API를 통해 매치메이킹 요청을 전달하고 Firestore를 통해 실시간 매칭 결과를 감지합니다.
/// - 클라이언트는 서버의 /api/match/request 및 /api/match/cancel을 호출
/// - 서버가 Firestore의 MatchmakingQueue를 관리하며, appsettings.json의 EnableSinglePlayerBot 설정에 따라:
///   * true: 2.5초 대기 후 봇 매칭
///   * false: 실제 유저가 들어올 때까지 무한 대기
/// - 매칭 성사 시 Firestore 리스너를 통해 게임 씬으로 안전하게 전환
/// </summary>
public class MatchmakingService : MonoBehaviour
{
    [Serializable]
    private class ServerMatchRequest
    {
        public string deckId;
    }

    [Serializable]
    private class ServerMatchResponse
    {
        public string status; // "waiting", "matched", "error"
        public string gameId;
        public string opponentUid;
        public string opponentName;
        public string message;
    }

    /// <summary>
    /// 덱 구성 검증 실패 시 서버로부터 수신하는 전용 에러 패킷 DTO
    /// 클라이언트 UI 컴포넌트에서 이 패킷을 받아 원하는 알림/팝업을 띄울 수 있습니다.
    /// </summary>
    [Serializable]
    public class MatchDeckErrorResponse
    {
        public string action = "MATCH_DECK_ERROR";
        public string status = "error";
        public string errorCode;            // 에러 코드 (DECK_NOT_FOUND, DECK_COUNT_MISMATCH, DECK_DUPLICATE_CARD, DECK_CLASS_MISMATCH, DECK_SIDE_DECK_INVALID, DECK_INVALID_CARD 등)
        public string message;              // 한글 상세 안내 메시지
        public int currentCount;            // 현재 메인 덱 장수
        public int requiredCount = 30;      // 필요 메인 덱 장수 (30장)
        public List<string> invalidCardIds = new List<string>(); // 문제가 된 카드 ID 목록
    }

    private FirebaseFirestore db;
    private FirebaseAuth auth;
    private ListenerRegistration matchmakingListener;
    private string currentUserId;
    private SynchronizationContext mainThreadContext;
    private bool hasDocumentEverExisted = false;

    [Header("설정")]
    [SerializeField] private string gameScene = "BattleScenes";

    public event Action OnMatchmakingStarted;
    public event Action OnMatchmakingCancelled;
    public event Action<string> OnMatchmakingFailed;
    public event Action<MatchDeckErrorResponse> OnMatchDeckError; // 🌟 덱 구성 오류 전용 이벤트 (UI 바인딩용)
    public event Action<string, string> OnMatchFound; // (gameId, opponentUid)

    void Awake()
    {
        mainThreadContext = SynchronizationContext.Current;

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
    /// 게임 서버에 매치메이킹을 요청합니다.
    /// </summary>
    public async void StartMatchmaking(DeckData selectedDeck)
    {
        if (string.IsNullOrEmpty(currentUserId) || selectedDeck == null)
        {
            Debug.LogError("[Matchmaking] 로그인이 안되어 있거나 덱이 선택되지 않았습니다.");
            OnMatchmakingFailed?.Invoke("로그인 또는 덱 선택이 필요합니다.");
            return;
        }

        if (GameClient.Instance == null)
        {
            Debug.LogError("[Matchmaking] GameClient 인스턴스가 없습니다.");
            OnMatchmakingFailed?.Invoke("GameClient를 찾을 수 없습니다.");
            return;
        }

        // 1차 클라이언트 사전 검증: 메인 덱 30장 검증
        int mainCount = selectedDeck.cardIds?.Count ?? 0;
        if (mainCount != 30)
        {
            var localError = new MatchDeckErrorResponse
            {
                errorCode = "DECK_COUNT_MISMATCH",
                message = $"메인 덱은 정확히 30장이어야 매칭을 시작할 수 있습니다. (현재: {mainCount}장)",
                currentCount = mainCount,
                requiredCount = 30
            };
            Debug.LogWarning($"[Matchmaking] ❌ 덱 검증 실패: {localError.message}");
            OnMatchDeckError?.Invoke(localError);
            OnMatchmakingFailed?.Invoke(localError.message);
            return;
        }

        Debug.Log("[Matchmaking] 🚀 서버에 매치메이킹 요청을 전송합니다...");

        try
        {
            string idToken = await auth.CurrentUser.TokenAsync(true);
            string url = GameClient.Instance.GetApiUrl("match/request");

            ServerMatchRequest requestBody = new ServerMatchRequest { deckId = selectedDeck.deckId };
            string jsonBody = JsonUtility.ToJson(requestBody);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + idToken);

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorJson = request.downloadHandler?.text;
                    Debug.LogError($"[Matchmaking] ❌ 서버 매칭 요청 실패: {request.error} - {errorJson}");

                    MatchDeckErrorResponse deckError = null;
                    if (!string.IsNullOrEmpty(errorJson))
                    {
                        try
                        {
                            deckError = JsonUtility.FromJson<MatchDeckErrorResponse>(errorJson);
                        }
                        catch (Exception parseEx)
                        {
                            Debug.LogWarning($"[Matchmaking] 에러 패킷 역직렬화 예외: {parseEx.Message}");
                        }
                    }

                    if (deckError != null && deckError.status == "error")
                    {
                        OnMatchDeckError?.Invoke(deckError);
                        OnMatchmakingFailed?.Invoke(deckError.message ?? "덱 구성이 올바르지 않습니다.");
                    }
                    else
                    {
                        OnMatchmakingFailed?.Invoke($"매칭 요청 실패: {request.error}");
                    }
                    return;
                }

                ServerMatchResponse response = JsonUtility.FromJson<ServerMatchResponse>(request.downloadHandler.text);

                if (response != null && response.status == "matched")
                {
                    // 즉시 상대와 매칭된 경우
                    Debug.Log($"[Matchmaking] ⚔️ 서버 즉시 매칭 체결! 게임 ID: {response.gameId}, 상대: {response.opponentUid}");
                    OnMatchFound?.Invoke(response.gameId, response.opponentUid);
                }
                else if (response != null && response.status == "error")
                {
                    MatchDeckErrorResponse deckError = null;
                    try { deckError = JsonUtility.FromJson<MatchDeckErrorResponse>(request.downloadHandler.text); } catch { }

                    if (deckError != null) OnMatchDeckError?.Invoke(deckError);
                    OnMatchmakingFailed?.Invoke(response.message ?? "덱 구성 오류가 발생했습니다.");
                }
                else
                {
                    // 대기열 등록 완료 -> UI에 대전 찾는 중 표시 및 Firestore 실시간 감지 시작
                    Debug.Log("[Matchmaking] ⏳ 서버 대기열 진입 완료. 매칭 결과를 대기합니다...");
                    OnMatchmakingStarted?.Invoke();
                    ListenForMatch(currentUserId);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Matchmaking] ❌ 매칭 요청 중 오류 발생: {e.Message}");
            OnMatchmakingFailed?.Invoke($"매칭 오류: {e.Message}");
        }
    }

    /// <summary>
    private Coroutine creationTimeoutCoroutine;

    /// <summary>
    /// Firestore 실시간 리스너를 통해 매칭 성사 여부를 감지합니다.
    /// (서버가 봇 또는 실제 유저 매칭 시 Firestore 문서를 'matched'로 갱신함)
    /// </summary>
    private void ListenForMatch(string userId)
    {
        StopListening();
        hasDocumentEverExisted = false;

        // [안전장치] 10초 동안 서버가 문서를 아예 생성하지 못하면 타임아웃 에러 처리
        creationTimeoutCoroutine = StartCoroutine(CreationTimeoutRoutine());

        DocumentReference myQueueDoc = db.Collection("MatchmakingQueue").Document(userId);
        matchmakingListener = myQueueDoc.Listen(snapshot =>
        {
            // Firebase C++ 백그라운드 스레드에서 수신된 스냅샷을 유니티 메인 스레드로 안전하게 전달
            if (mainThreadContext != null)
            {
                mainThreadContext.Post(_ =>
                {
                    HandleQueueSnapshot(snapshot, userId, myQueueDoc);
                }, null);
            }
            else
            {
                HandleQueueSnapshot(snapshot, userId, myQueueDoc);
            }
        });
    }

    /// <summary>
    /// 유니티 메인 스레드에서 실행되는 안전한 대기열 스냅샷 처리기
    /// </summary>
    private void HandleQueueSnapshot(DocumentSnapshot snapshot, string userId, DocumentReference myQueueDoc)
    {
        if (this == null || gameObject == null) return;

        if (snapshot != null && snapshot.Exists)
        {
            // 1. 서버가 생성한 대기 문서가 정상적으로 감지됨 (대기열 진입 확인)
            if (!hasDocumentEverExisted)
            {
                hasDocumentEverExisted = true;
                if (creationTimeoutCoroutine != null)
                {
                    StopCoroutine(creationTimeoutCoroutine);
                    creationTimeoutCoroutine = null;
                }
                Debug.Log($"[Matchmaking] ✅ 대기열 문서 동기화 완료 ({userId}). 매칭 대기 중...");
            }

            MatchmakingEntry entry = snapshot.ConvertTo<MatchmakingEntry>();
            if (entry != null && entry.status == "matched")
            {
                Debug.Log($"[Matchmaking] ⚔️ 매칭 성사 감지! 상대: {entry.opponentUid} ({entry.opponentName}), 게임 ID: {entry.gameId}");
                if (GameClient.Instance != null && !string.IsNullOrEmpty(entry.opponentName))
                {
                    GameClient.Instance.OpponentUsername = entry.opponentName;
                }
                StopListening();
                OnMatchFound?.Invoke(entry.gameId, entry.opponentUid);
                myQueueDoc.DeleteAsync();
            }
        }
        else
        {
            // 2. 이전에 문서가 존재했었는데 사라진 경우에만 진짜 '취소/종료'로 처리
            if (hasDocumentEverExisted)
            {
                Debug.Log("[Matchmaking] 매치메이킹 큐에서 대기 문서가 제거되었습니다 (취소/종료).");
                StopListening();
                OnMatchmakingCancelled?.Invoke();
            }
            else
            {
                // 3. 첫 스냅샷인데 아직 서버가 쓴 문서가 클라이언트에 도착하지 않은 초기 상태 -> 취소하지 않고 대기
                Debug.Log("[Matchmaking] ⏳ 대기 문서 동기화 대기 중...");
            }
        }
    }

    private IEnumerator CreationTimeoutRoutine()
    {
        yield return YieldInstructionCache.WaitForSeconds(10f);
        Debug.LogError("[Matchmaking] ❌ 서버 대기열 등록 타임아웃: 10초 동안 대기 문서가 생성되지 않았습니다.");
        StopListening();
        OnMatchmakingFailed?.Invoke("매칭 서버 응답 시간 초과");
    }

    /// <summary>
    /// 매칭 취소
    /// </summary>
    public async void CancelMatchmaking()
    {
        if (string.IsNullOrEmpty(currentUserId)) return;
        Debug.Log("[Matchmaking] 매치메이킹 취소 요청을 전송합니다...");
        StopListening();

        try
        {
            if (auth.CurrentUser != null && GameClient.Instance != null)
            {
                string idToken = await auth.CurrentUser.TokenAsync(true);
                string url = GameClient.Instance.GetApiUrl("match/cancel");

                using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
                {
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Authorization", "Bearer " + idToken);

                    var operation = request.SendWebRequest();
                    while (!operation.isDone) await Task.Yield();
                }
            }

            OnMatchmakingCancelled?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Matchmaking] 취소 중 오류: {e.Message}");
            OnMatchmakingCancelled?.Invoke();
        }
    }

    public void GoGame(string GameID, string opponentUid)
    {
        GameClient.Instance.GameId = GameID;
        // Build Settings에 등록된 실제 씬 이름으로 보정 (Battle -> BattleScenes)
        string sceneToLoad = (gameScene == "Battle") ? "BattleScenes" : gameScene;
        SceneLoader.instance.LoadSceneByName(sceneToLoad);
    }

    void OnDestroy()
    {
        StopListening();
        if (auth != null)
        {
            auth.StateChanged -= OnAuthStateChanged;
        }
    }

    private void StopListening()
    {
        if (creationTimeoutCoroutine != null)
        {
            StopCoroutine(creationTimeoutCoroutine);
            creationTimeoutCoroutine = null;
        }

        if (matchmakingListener != null)
        {
            matchmakingListener.Stop();
            matchmakingListener = null;
        }
    }
}