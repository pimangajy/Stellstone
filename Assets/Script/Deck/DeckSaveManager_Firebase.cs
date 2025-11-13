using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks; // 비동기 작업을 위해 Task 사용
using Firebase.Firestore;
using Firebase.Auth; // 사용자 인증 정보를 가져오기 위해 필요
using System.Linq; // LINQ를 사용하기 위해 추가
using UnityEngine.Networking; // UnityWebRequest를 사용하기 위해 추가
using System.Text; // UTF8 인코딩을 위해 추가

// [FirestoreData] 속성은 DeckData 스크립트 파일에 있어야 합니다.
// DeckData 스크립트는 Firestore 필드와 매칭되도록 속성을 가져야 합니다.
// 예: [FirestoreProperty] public string deckName { get; set; }

public class DeckSaveManager_Firebase : MonoBehaviour
{
    public static DeckSaveManager_Firebase instance;
    public static event Action OnDecksChanged;

    private FirebaseFirestore db;
    private FirebaseAuth auth;
    private string currentUserId;

    private List<DeckData> allDecks; // 메모리에 캐싱된 덱 리스트
    private bool isInitialized = false; // 이미 로드되었는지 확인하는 플래그

    // 서버 API의 기본 URL
    private const string ApiBaseUrl = "http://localhost:5123";

    private void Awake()
    {
        // --- 씬 싱글톤 패턴 구현 ---
        if (instance != null && instance != this)
        {
            // 이미 이 씬에 SinginManager가 있다면, 새로 생긴 것은 파괴
            Destroy(gameObject);
        }
        else
        {
            // 이 씬의 유일한 인스턴스로 등록
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        db = FirebaseFirestore.DefaultInstance;
        auth = FirebaseAuth.DefaultInstance;

        allDecks = new List<DeckData>();
        auth.StateChanged += HandleAuthStateChanged;
        Debug.Log("DeckSaveManager가 인증 상태 감지를 시작합니다.");

    }

    private void OnDestroy()
    {
        // --- 이벤트 구독 해제 ---
        if (auth != null)
        {
            auth.StateChanged -= HandleAuthStateChanged;
        }
        // --- 싱글톤 참조 정리 ---
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Start()
    {
        // OnDecksChanged?.Invoke();
    }

    /// <summary>
    /// (신규) Firebase 인증 상태가 변경될 때마다 호출되는 이벤트 핸들러
    /// </summary>
    private async void HandleAuthStateChanged(object sender, EventArgs e)
    {
        FirebaseUser currentUser = auth.CurrentUser;

        if (currentUser != null)
        {
            // --- 1. 로그인 감지 ---
            // 이미 로그인된 유저가 아니거나, 덱이 로드된 적 없다면
            if (currentUser.UserId != currentUserId || !isInitialized)
            {
                Debug.Log($"로그인 감지: {currentUser.UserId}. 덱 로드를 시작합니다.");
                // 새 유저 ID로 초기화 및 덱 로드
                await InitializeAsync(currentUser.UserId);
            }
        }
        else
        {
            // --- 2. 로그아웃 감지 ---
            if (isInitialized || !string.IsNullOrEmpty(currentUserId))
            {
                Debug.Log("로그아웃 감지. 로컬 덱 캐시를 비웁니다.");
                ClearDecks();
            }
        }
    }

    /// <summary>
    /// 특정 유저 ID로 덱 초기화 및 로드를 시작합니다.
    /// </summary>
    public async Task InitializeAsync(string newUserId)
    {
        // (중요) 덱을 로드하기 전에 기존 캐시를 비웁니다.
        ClearDecks();

        currentUserId = newUserId;
        isInitialized = false; // 로드 시작

        if (string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("유저 ID가 없어 덱을 로드할 수 없습니다.");
            isInitialized = true; // 로드할 것이 없으므로 '완료' 처리
            return;
        }

        await ServerLoadDecks();

        isInitialized = true;
    }

    /// <summary>
    /// 덱 캐시를 비우고 UI 갱신 이벤트를 호출합니다.
    /// </summary>
    public void ClearDecks()
    {
        currentUserId = null;
        allDecks.Clear();
        isInitialized = false;

        // UI(DeckListUI 등)에게 덱 목록이 비었음을 알림
        OnDecksChanged?.Invoke();
    }

    /// <summary>
    /// Firestore에서 현재 사용자의 모든 덱을 비동기적으로 불러옵니다.
    /// </summary>
    public async Task LoadDecks()
    {
        // 1. Users -> {내 ID} -> Decks 서브컬렉션 경로를 지정합니다.
        CollectionReference decksRef = db.Collection("Users").Document(currentUserId).Collection("Decks");

        // 2. 해당 경로의 모든 문서를 비동기적으로 가져옵니다.
        QuerySnapshot snapshot = await decksRef.GetSnapshotAsync();

        allDecks = new List<DeckData>();

        // 3. 가져온 각 문서를 DeckData 객체로 변환하여 리스트에 추가합니다.
        foreach (DocumentSnapshot document in snapshot.Documents)
        {
            DeckData deck = document.ConvertTo<DeckData>();
            // Firestore에서 자동으로 ID를 문서 이름으로 사용하므로, deckId를 직접 설정해줍니다.
            deck.deckId = document.Id;
            allDecks.Add(deck);
            Debug.Log(deck.deckId);
        }

        Debug.Log($"사용자({currentUserId})의 덱 {allDecks.Count}개를 불러왔습니다.");

        // 4. 로드가 완료되었음을 UI에 알립니다.
        OnDecksChanged?.Invoke();
    }

    // ... CreateNewDeck, UpdateDeck, DeleteDeck 함수들도
    // PlayerPrefs가 아닌 Firestore API를 사용하도록 수정해야 합니다. ...

    // 예시: 덱 생성 함수
    public async Task<DeckData> CreateNewDeck(string className)
    {
        const string defaultDeckNamePrefix = "새로운 덱 ";

        // 1. "새로운 덱 "으로 시작하는 덱들의 번호만 추출하여 집합(Set)으로 만듭니다.
        var existingNumbers = allDecks
            .Where(deck => deck.deckName.StartsWith(defaultDeckNamePrefix))
            .Select(deck => {
                // "새로운 덱 " 다음의 숫자 부분만 잘라냅니다.
                string numberPart = deck.deckName.Substring(defaultDeckNamePrefix.Length);
                // 숫자 부분만 int로 변환합니다. 변환에 실패하면 0을 반환합니다.
                int.TryParse(numberPart, out int number);
                return number;
            })
            .Where(number => number > 0) // 유효한 번호(0보다 큰)만 필터링합니다.
            .ToHashSet(); // 중복을 제거하고 빠른 조회를 위해 HashSet으로 변환합니다.

        // 2. 1부터 시작하여, 기존 번호 집합에 없는 가장 작은 숫자를 찾습니다.
        int newDeckNumber = 1;
        while (existingNumbers.Contains(newDeckNumber))
        {
            newDeckNumber++;
        }

        // 3. 찾아낸 번호로 새 덱 이름을 생성합니다.
        string deckName = $"{defaultDeckNamePrefix}{newDeckNumber}";

        DeckData newDeck = new DeckData(null, deckName, className); // ID는 Firestore가 자동 생성

        // Decks 서브컬렉션에 새로운 문서를 추가합니다.
        DocumentReference addedDocRef = await db.Collection("Users").Document(currentUserId).Collection("Decks").AddAsync(newDeck);

        newDeck.deckId = addedDocRef.Id; // 자동 생성된 ID를 객체에 저장
        allDecks.Add(newDeck); // 메모리에 있는 리스트에도 추가

        OnDecksChanged?.Invoke(); // UI 갱신을 위해 이벤트 발생
        return newDeck;
    }

    /// <summary>
    /// 기존 덱의 정보(이름, 카드 목록 등)를 Firestore와 로컬 캐시에 업데이트합니다.
    /// </summary>
    public async Task UpdateDeck(DeckData updatedDeck)
    {
        if (string.IsNullOrEmpty(currentUserId) || string.IsNullOrEmpty(updatedDeck.deckId))
        {
            Debug.LogError("사용자 ID 또는 덱 ID가 유효하지 않아 덱을 업데이트할 수 없습니다.");
            return;
        }

        // 1. Firestore에 있는 특정 덱 문서의 경로를 지정합니다.
        DocumentReference deckRef = db.Collection("Users").Document(currentUserId).Collection("Decks").Document(updatedDeck.deckId);

        // 2. 해당 문서를 updatedDeck 객체의 내용으로 덮어씁니다.
        await deckRef.SetAsync(updatedDeck, SetOptions.Overwrite);

        // 3. 메모리에 있는 로컬 리스트에서도 해당 덱을 찾아 업데이트합니다.
        int index = allDecks.FindIndex(d => d.deckId == updatedDeck.deckId);
        if (index != -1)
        {
            allDecks[index] = updatedDeck;
        }
        else
        {
            // 만약 로컬 리스트에 없다면 (오류 또는 예외 상황), 그냥 추가하고 경고를 남깁니다.
            allDecks.Add(updatedDeck);
            Debug.LogWarning($"로컬 캐시에 없는 덱(ID: {updatedDeck.deckId})이 업데이트되어 새로 추가되었습니다.");
        }

        Debug.Log($"덱 '{updatedDeck.deckName}' (ID: {updatedDeck.deckId}) 정보가 성공적으로 업데이트되었습니다.");

        // 4. UI 갱신을 위해 이벤트를 발생시킵니다.
        OnDecksChanged?.Invoke();
    }

    /// <summary>
    /// 특정 덱을 Firestore와 로컬 캐시에서 삭제합니다.
    /// </summary>
    public async Task DeleteDeck(string deckId)
    {
        if (string.IsNullOrEmpty(currentUserId) || string.IsNullOrEmpty(deckId))
        {
            Debug.LogError("사용자 ID 또는 덱 ID가 유효하지 않아 덱을 삭제할 수 없습니다.");
            return;
        }

        // Firestore에서 덱 문서를 삭제합니다.
        await db.Collection("Users").Document(currentUserId).Collection("Decks").Document(deckId).DeleteAsync();

        // 로컬 리스트에서 해당 덱을 제거합니다.
        allDecks.RemoveAll(d => d.deckId == deckId);

        Debug.Log($"덱(ID: {deckId})이 성공적으로 삭제되었습니다.");

        OnDecksChanged?.Invoke();
    }

    public List<DeckData> GetAllDecks()
    {
        return allDecks;
    }

    /// <summary>
    /// 서버의 CreateDeckRequest DTO와 동일한 역할을 하는 Unity용 클래스
    /// </summary>
    [System.Serializable]
    private class UnityCreateDeckRequest
    {
        public string className;
    }

    // ==================================================================
    // 덱 로드 (API 호출 방식으로 변경됨)
    // ==================================================================
    public async Task ServerLoadDecks()
    {
        if (auth.CurrentUser == null)
        {
            Debug.LogError("로그인하지 않아 덱을 로드할 수 없습니다.");
            return;
        }

        // 1. Firebase에서 최신 ID 토큰을 가져옵니다.
        string idToken = await auth.CurrentUser.TokenAsync(true);
        // 2. 덱 목록을 가져오는 API URL
        string apiUrl = $"{ApiBaseUrl}/api/decks";

        // 3. UnityWebRequest 생성 (GET 메서드)
        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            // 4. 헤더 설정
            request.SetRequestHeader("Authorization", "Bearer " + idToken);

            Debug.Log($"덱 목록 API 호출: {apiUrl}");

            // 5. API 요청 전송
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            // 6. 결과 처리
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"덱 목록 로드 실패: {request.error} - {request.downloadHandler.text}");
                allDecks = new List<DeckData>();
            }
            else
            {
                // 7. 성공 시, 서버가 반환한 JSON을 파싱
                string jsonResponse = request.downloadHandler.text;

                // (중요) JsonUtility는 루트가 리스트인 JSON을 직접 파싱하지 못하므로 래퍼 클래스 사용
                UnityDeckListWrapper wrapper = JsonUtility.FromJson<UnityDeckListWrapper>(jsonResponse);

                if (wrapper != null && wrapper.decks != null)
                {
                    allDecks = wrapper.decks;
                }
                else
                {
                    Debug.LogError($"덱 목록 파싱 실패: {jsonResponse}");
                    allDecks = new List<DeckData>();
                }
            }
        }

        // 8. UI 갱신 이벤트 발생
        OnDecksChanged?.Invoke();
    }

    // (추가) 덱 목록 JSON을 파싱하기 위한 래퍼 클래스
    [System.Serializable]
    private class UnityDeckListWrapper
    {
        public List<DeckData> decks;
    }

    // ==================================================================
    // 덱 생성 ( API 호출 방식으로 변경됨)
    // ==================================================================
    public async Task<DeckData> ServerCreateNewDeck(string className)
    {
        if (auth.CurrentUser == null)
        {
            Debug.LogError("로그인하지 않아 덱을 생성할 수 없습니다.");
            return null;
        }

        // 1. Firebase에서 최신 ID 토큰을 가져옵니다.
        string idToken = await auth.CurrentUser.TokenAsync(true);
        string apiUrl = $"{ApiBaseUrl}/api/decks/create";

        // 2. 서버로 보낼 JSON 본문 생성 (CreateDeckRequest 클래스 사용)
        UnityCreateDeckRequest requestBody = new UnityCreateDeckRequest { className = className };
        string jsonBody = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        // 3. UnityWebRequest 생성 및 설정
        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // 4. 헤더 설정 (매우 중요)
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + idToken);

            Debug.Log($"덱 생성 API 호출: {apiUrl}");

            // 5. API 요청 전송 (비동기 대기)
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            // 6. 결과 처리
            if (request.result != UnityWebRequest.Result.Success)
            {
                // (네트워크 오류 또는 HTTP 에러)
                Debug.LogError($"덱 생성 실패: {request.error} - {request.downloadHandler.text}");
                return null;
            }
            else
            {
                // 7. 성공 시, 서버가 반환한 DeckData JSON을 파싱
                string jsonResponse = request.downloadHandler.text;
                Debug.Log($"덱 생성 성공, 응답: {jsonResponse}");
                DeckData newDeck = JsonUtility.FromJson<DeckData>(jsonResponse);

                // 8. 로컬 캐시 리스트에 추가
                allDecks.Add(newDeck);

                // 9. UI 갱신 이벤트 발생
                OnDecksChanged?.Invoke();
                return newDeck;
            }
        }
    }

    /// <summary>
    /// DeckData와 동일한 구조를 가지지만,
    /// JsonUtility.ToJson()을 위해 프로퍼티( {get;set;} ) 대신 public 필드를 사용하는 클래스
    /// </summary>
    [System.Serializable]
    private class UnityUpdateDeckRequest
    {
        public string deckId;
        public string deckName;
        public string deckClass;
        public List<string> cardIds;
    }

    // ==================================================================
    // 덱 업데이트  API 호출 방식으로 변경됨)
    // ==================================================================
    public async Task ServerUpdateDeck(DeckData updatedDeck)
    {
        if (auth.CurrentUser == null)
        {
            Debug.LogError("로그인하지 않아 덱을 수정할 수 없습니다.");
            return;
        }

        if (string.IsNullOrEmpty(updatedDeck.deckId))
        {
            Debug.LogError("수정할 덱의 ID가 없습니다.");
            return;
        }

        // 1. Firebase에서 최신 ID 토큰을 가져옵니다.
        string idToken = await auth.CurrentUser.TokenAsync(true);
        // 2. API URL에 수정할 덱의 ID를 포함합니다.
        string apiUrl = $"{ApiBaseUrl}/api/decks/update/{updatedDeck.deckId}";

        // 3. 서버로 보낼 JSON 본문 생성 (DeckData 객체 전체)
        // DeckData(프로퍼티 사용)를 JsonUtility가 인식할 수 있는 UnityUpdateDeckRequest(필드 사용)로 복사합니다.
        UnityUpdateDeckRequest requestBody = new UnityUpdateDeckRequest
        {
            deckId = updatedDeck.deckId,
            deckName = updatedDeck.deckName,
            deckClass = updatedDeck.deckClass,
            cardIds = updatedDeck.cardIds
        };

        string jsonBody = JsonUtility.ToJson(requestBody); // DTO 객체를 직렬화

        // (디버깅) 서버로 보내는 JSON이 올바른지 확인합니다.
        Debug.Log("서버로 보내는 JSON (UpdateDeck): " + jsonBody);

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        // 4. UnityWebRequest 생성 및 설정 (메서드를 "PUT"으로 변경)
        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "PUT"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // 5. 헤더 설정
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + idToken);

            Debug.Log($"덱 업데이트 API 호출: {apiUrl}");

            // 6. API 요청 전송
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            // 7. 결과 처리
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"덱 업데이트 실패: {request.error} - {request.downloadHandler.text}");
            }
            else
            {
                Debug.Log($"덱 업데이트 성공: {request.downloadHandler.text}");

                // 8. 로컬 캐시 리스트(allDecks)도 업데이트
                int index = allDecks.FindIndex(d => d.deckId == updatedDeck.deckId);
                if (index != -1)
                {
                    allDecks[index] = updatedDeck;
                }
                else
                {
                    // 덱 생성 직후 바로 저장하는 경우 등, 리스트에 없을 수 있음
                    allDecks.Add(updatedDeck);
                }

                // 9. UI 갱신 이벤트 발생
                OnDecksChanged?.Invoke();
            }
        }
    }

    // ==================================================================
    // 덱 삭제 (API 호출 방식으로 변경됨)
    // ==================================================================
    public async Task ServerDeleteDeck(string deckId)
    {
        if (auth.CurrentUser == null)
        {
            Debug.LogError("로그인하지 않아 덱을 삭제할 수 없습니다.");
            return;
        }

        if (string.IsNullOrEmpty(deckId))
        {
            Debug.LogError("삭제할 덱의 ID가 없습니다.");
            return;
        }

        // 1. Firebase에서 최신 ID 토큰을 가져옵니다.
        string idToken = await auth.CurrentUser.TokenAsync(true);
        // 2. API URL에 삭제할 덱의 ID를 포함합니다.
        string apiUrl = $"{ApiBaseUrl}/api/decks/delete/{deckId}";

        // 3. UnityWebRequest 생성 (DELETE 메서드)
        using (UnityWebRequest request = UnityWebRequest.Delete(apiUrl))
        {
            // 서버로부터 "삭제 성공" 메시지를 받기 위해 DownloadHandler가 필요합니다.
            request.downloadHandler = new DownloadHandlerBuffer();

            // 4. 헤더 설정
            request.SetRequestHeader("Authorization", "Bearer " + idToken);

            Debug.Log($"덱 삭제 API 호출: {apiUrl}");

            // 5. API 요청 전송
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            // 6. 결과 처리
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"덱 삭제 실패: {request.error} - {request.downloadHandler.text}");
            }
            else
            {
                Debug.Log($"덱 삭제 성공: {request.downloadHandler.text}");

                // 7. 로컬 캐시 리스트(allDecks)에서 제거
                allDecks.RemoveAll(d => d.deckId == deckId);

                // 8. UI 갱신 이벤트 발생
                OnDecksChanged?.Invoke();
            }
        }
    }
}
