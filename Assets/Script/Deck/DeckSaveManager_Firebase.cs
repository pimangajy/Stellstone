using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks; // 비동기 작업(서버 통신 대기)을 위해 필수
using Firebase.Firestore;
using Firebase.Auth;
using System.Linq;
using UnityEngine.Networking; // 웹 요청(API 호출)을 위해 필요
using System.Text; // 글자를 바이트로 변환(Encoding)할 때 필요

/// <summary>
/// 서버(Firebase 및 REST API)와 통신하여 덱을 저장, 로드, 수정, 삭제하는 매니저입니다.
/// 인터넷을 통해 데이터를 주고받으므로 대부분의 함수가 비동기(async/await)로 작동합니다.
/// </summary>
public class DeckSaveManager_Firebase : MonoBehaviour
{
    public static DeckSaveManager_Firebase instance;

    // 덱 목록이 바뀌었을 때(로드 완료, 삭제 등) 다른 스크립트들에게 알리는 이벤트
    public static event Action OnDecksChanged;

    private FirebaseFirestore db; // 데이터베이스 연결
    private FirebaseAuth auth;    // 로그인/회원가입 관리
    private string currentUserId; // 현재 로그인한 유저 ID

    private List<DeckData> allDecks; // 받아온 덱 리스트를 메모리에 저장(캐시)
    private bool isInitialized = false;

    // 서버 주소 (REST API 엔드포인트)
    private const string ApiBaseUrl = "http://175.125.250.226:5123";

    private void Awake()
    {
        // --- 싱글톤 패턴 (설명 생략) ---
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Firebase 도구 준비
        db = FirebaseFirestore.DefaultInstance;
        auth = FirebaseAuth.DefaultInstance;

        allDecks = new List<DeckData>();

        // 로그인 상태가 바뀌는지 감시를 시작합니다.
        auth.StateChanged += HandleAuthStateChanged;
        Debug.Log("DeckSaveManager가 인증 상태 감지를 시작합니다.");
    }

    private void OnDestroy()
    {
        // 감시 해제 (필수)
        if (auth != null)
        {
            auth.StateChanged -= HandleAuthStateChanged;
        }
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// 로그인하거나 로그아웃할 때 자동으로 실행되는 함수입니다.
    /// </summary>
    private async void HandleAuthStateChanged(object sender, EventArgs e)
    {
        FirebaseUser currentUser = auth.CurrentUser;

        if (currentUser != null)
        {
            // 로그인 했고, 아직 데이터를 안 불러왔다면 -> 데이터 로드 시작
            if (currentUser.UserId != currentUserId || !isInitialized)
            {
                Debug.Log($"로그인 감지: {currentUser.UserId}. 덱 로드를 시작합니다.");
                await InitializeAsync(currentUser.UserId);
            }
        }
        else
        {
            // 로그아웃 했다면 -> 데이터 비우기
            if (isInitialized || !string.IsNullOrEmpty(currentUserId))
            {
                Debug.Log("로그아웃 감지. 로컬 덱 캐시를 비웁니다.");
                ClearDecks();
            }
        }
    }

    /// <summary>
    /// 덱 로드 초기화 과정 (기존 것 비우고 -> 서버에서 새로 받기)
    /// </summary>
    public async Task InitializeAsync(string newUserId)
    {
        ClearDecks(); // 청소

        currentUserId = newUserId;
        isInitialized = false;

        if (string.IsNullOrEmpty(currentUserId))
        {
            isInitialized = true;
            return;
        }

        // 서버에서 진짜로 데이터 받아오기
        await ServerLoadDecks();

        isInitialized = true;
    }

    /// <summary>
    /// 메모리에 있는 덱 정보를 싹 비웁니다.
    /// </summary>
    public void ClearDecks()
    {
        currentUserId = null;
        allDecks.Clear();
        isInitialized = false;
        OnDecksChanged?.Invoke(); // UI에게 "목록 비었어"라고 알림
    }

    // (참고: 아래 LoadDecks 함수는 Firestore SDK를 직접 쓰는 구형 방식이고, 
    // ServerLoadDecks는 REST API를 쓰는 신형 방식인 것 같습니다. 혼용되어 있습니다.)
    public async Task LoadDecks()
    {
        // ... (Firestore SDK 직접 사용 코드 생략) ...
        // 동작 방식: Users -> 내ID -> Decks 폴더를 뒤져서 가져옴
    }

    public List<DeckData> GetAllDecks()
    {
        return allDecks;
    }

    // ==================================================================
    // [중요] 덱 로드 (API 통신 방식)
    // ==================================================================
    public async Task ServerLoadDecks()
    {
        if (auth.CurrentUser == null) return;

        // 1. 보안 토큰(신분증)을 발급받습니다.
        string idToken = await auth.CurrentUser.TokenAsync(true);
        // 2. 요청할 주소 설정
        string apiUrl = $"{ApiBaseUrl}/api/decks";

        // 3. UnityWebRequest: 유니티의 웹 브라우저 같은 역할
        // GET 방식: "데이터 조회" 요청
        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            // 헤더에 신분증 첨부
            request.SetRequestHeader("Authorization", "Bearer " + idToken);

            // 4. 전송하고 기다림 (isDone이 될 때까지)
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield(); // 다음 프레임까지 대기
            }

            // 5. 결과 확인
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"실패: {request.error}");
                allDecks = new List<DeckData>();
            }
            else
            {
                // 6. 성공 시 JSON(텍스트)을 받아서 C# 객체로 변환
                string jsonResponse = request.downloadHandler.text;

                // JsonUtility 버그 우회: 리스트를 바로 변환 못하므로 Wrapper 클래스 사용
                UnityDeckListWrapper wrapper = JsonUtility.FromJson<UnityDeckListWrapper>(jsonResponse);

                if (wrapper != null && wrapper.decks != null)
                {
                    allDecks = wrapper.decks;
                }
            }
        }

        // UI 갱신 알림
        OnDecksChanged?.Invoke();
    }

    // JSON 리스트 파싱을 위한 포장지 클래스
    [System.Serializable]
    private class UnityDeckListWrapper
    {
        public List<DeckData> decks;
    }

    // ==================================================================
    // 덱 생성 (API 통신 방식)
    // ==================================================================
    public async Task<DeckData> ServerCreateNewDeck(string className)
    {
        if (auth.CurrentUser == null) return null;

        string idToken = await auth.CurrentUser.TokenAsync(true);
        string apiUrl = $"{ApiBaseUrl}/api/decks/create";

        // 보낼 데이터 준비 (직업 이름)
        UnityCreateDeckRequest requestBody = new UnityCreateDeckRequest { className = className };
        string jsonBody = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        // POST 방식: "데이터 생성" 요청
        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw); // 보낼 데이터
            request.downloadHandler = new DownloadHandlerBuffer(); // 받을 준비

            request.SetRequestHeader("Content-Type", "application/json"); // "나 JSON 보낸다"
            request.SetRequestHeader("Authorization", "Bearer " + idToken); // "나 누구다"

            // 전송 및 대기
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 성공하면 서버가 만들어진 덱 정보를 보내줌
                string jsonResponse = request.downloadHandler.text;
                DeckData newDeck = JsonUtility.FromJson<DeckData>(jsonResponse);

                // 내 목록에 추가하고 알림
                allDecks.Add(newDeck);
                OnDecksChanged?.Invoke();
                return newDeck;
            }
        }
        return null;
    }

    // 서버로 보낼 때 사용할 작은 데이터 클래스 (DTO)
    [System.Serializable]
    private class UnityCreateDeckRequest
    {
        public string className;
    }

    // ==================================================================
    // 덱 업데이트 (저장)
    // ==================================================================
    public async Task ServerUpdateDeck(DeckData updatedDeck)
    {
        if (auth.CurrentUser == null) return;

        string idToken = await auth.CurrentUser.TokenAsync(true);
        string apiUrl = $"{ApiBaseUrl}/api/decks/update/{updatedDeck.deckId}";

        // 업데이트할 내용 포장 (UnityUpdateDeckRequest 사용)
        UnityUpdateDeckRequest requestBody = new UnityUpdateDeckRequest
        {
            deckId = updatedDeck.deckId,
            deckName = updatedDeck.deckName,
            deckClass = updatedDeck.deckClass,
            cardIds = updatedDeck.cardIds
        };

        string jsonBody = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        // PUT 방식: "데이터 수정/덮어쓰기" 요청
        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "PUT"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + idToken);

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 성공 시 로컬 목록도 최신 정보로 갱신
                int index = allDecks.FindIndex(d => d.deckId == updatedDeck.deckId);
                if (index != -1) allDecks[index] = updatedDeck;
                else allDecks.Add(updatedDeck);

                OnDecksChanged?.Invoke();
            }
        }
    }

    [System.Serializable]
    private class UnityUpdateDeckRequest
    {
        public string deckId;
        public string deckName;
        public string deckClass;
        public List<string> cardIds;
    }

    // ==================================================================
    // 덱 삭제
    // ==================================================================
    public async Task ServerDeleteDeck(string deckId)
    {
        if (auth.CurrentUser == null) return;

        string idToken = await auth.CurrentUser.TokenAsync(true);
        string apiUrl = $"{ApiBaseUrl}/api/decks/delete/{deckId}";

        // DELETE 방식: "삭제" 요청
        using (UnityWebRequest request = UnityWebRequest.Delete(apiUrl))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + idToken);

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 로컬 목록에서도 삭제
                allDecks.RemoveAll(d => d.deckId == deckId);
                OnDecksChanged?.Invoke();
            }
        }
    }
}