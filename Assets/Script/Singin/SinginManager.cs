using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement; // 씬 전환을 위해 추가
using Firebase;
using Firebase.Auth;
using Firebase.Extensions; // ContinueWithOnMainThread 사용
using Newtonsoft.Json;

// 서버로 보낼/받을 데이터 구조를 정의하는 클래스들
[System.Serializable]
public class SignupRequestData { public string email; public string password; public string username; }
[System.Serializable]
public class LoginRequestData { public string email; public string password; }
[System.Serializable]
public class VerifyTokenRequestData { public string token; }

[System.Serializable]
public class UserData
{
    public string username;
    public int level;
    public int exp;
    public int score;
    public int winCount;
    public int lossCount;
    public string selectDeck;
    public int gold;
    public int stardust;
    public int stellastone;
    public List<string> ownedSkins = new List<string>();
    public List<string> ownedEmotes = new List<string>();
    public Dictionary<string, int> ownedCards = new Dictionary<string, int>();
    public Dictionary<string, int> ownedPrismCards = new Dictionary<string, int>();
    public Dictionary<string, int> ownedPacks = new Dictionary<string, int>();
}

[System.Serializable]
public class AuthApiResponse 
{ 
    public string status; 
    public string message; 
    public string user_id; 
    public string customToken;
    public string idToken;
    public UserData userData;
}


public class SinginManager : MonoBehaviour
{
    // 로그인된 유저의 최신 계정 데이터 (어디서든 SinginManager.CurrentUserData 로 접근 가능)
    public static UserData CurrentUserData { get; set; }
    // --- UI 참조 변수들 ---
    [Header("Signup UI")]
    public TMP_InputField emailInputField;
    public TMP_InputField passwordInputField;
    public TMP_InputField usernameInputField;
    public TextMeshProUGUI messageText;

    [Header("Login UI")]
    public TMP_InputField emailInputField_login;
    public TMP_InputField passwordInputField_login;
    public TextMeshProUGUI messageTextLogin;

    [Header("API URLs")]
    public string signupApiUrl => (GameClient.Instance != null)
        ? GameClient.Instance.GetApiUrl("auth/signup")
        : "http://175.125.250.226:5123/api/auth/signup";
    public string loginApiUrl => (GameClient.Instance != null)
        ? GameClient.Instance.GetApiUrl("auth/login")
        : "http://175.125.250.226:5123/api/auth/login";
    public string verifyTokenApiUrl => (GameClient.Instance != null)
        ? GameClient.Instance.GetApiUrl("auth/verify-token")
        : "http://175.125.250.226:5123/api/auth/verify-token";

    // --- Firebase 관련 변수 ---
    private FirebaseAuth auth;
    private bool isFirebaseReady = false;


    public static SinginManager Instance { get; private set; }
    private void Awake()
    {
        if(Instance == null && Instance != this)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // 중복 방지
        }
    }


    // Start()는 로그인 씬이 로드될 때마다 실행
    void Start()
    {
        // Enter입력시 회원가입, 로그인 실행
        if (emailInputField_login != null)
        {
            emailInputField_login.onSubmit.AddListener(_ =>
            {
                if (passwordInputField_login != null) passwordInputField_login.Select();
            });
        }
        if (passwordInputField_login != null)
        {
            passwordInputField_login.onSubmit.AddListener(_ => OnLoginButtonClicked());
        }

        // 회원가입 UI 엔터 처리
        if (usernameInputField != null)
        {
            usernameInputField.onSubmit.AddListener(_ => OnSignupButtonClicked());
        }

        // LogText 오브젝트 자동 참조 보강 (인스펙터 연결이 누락되어도 100% 안전하게 동작)
        EnsureLogTextReferences();

        // 씬이 로드될 때마다 UI 메시지 초기화
        DisplayUIMessage(messageText, "", Color.black);
        DisplayUIMessage(messageTextLogin, "", Color.black);

        // Firebase 초기화는 앱 전체에서 한 번만 실행되도록 SDK가 처리해 줍니다.
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                // auth 인스턴스를 가져옵니다. (SDK가 알아서 싱글톤으로 관리)
                auth = FirebaseAuth.DefaultInstance;
                isFirebaseReady = true;

                // 자동 로그인 체크 (메인 스레드에서 안전하게 실행)
                CheckCurrentUser();
            }
            else
            {
                isFirebaseReady = false;
                Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
            }
        });
    }

    /// <summary>
    /// 로그인 및 회원가입 패널의 LogText 오브젝트를 자동 탐색하여 바인딩
    /// </summary>
    private void EnsureLogTextReferences()
    {
        if (messageText == null)
        {
            GameObject go = GameObject.Find("/Canvas/Singup/Main/LogText");
            if (go != null) messageText = go.GetComponent<TextMeshProUGUI>();
        }

        if (messageTextLogin == null)
        {
            GameObject go = GameObject.Find("/Canvas/Login/Main/LogText");
            if (go != null) messageTextLogin = go.GetComponent<TextMeshProUGUI>();
        }
    }

    /// <summary>
    /// 이메일(아이디) 형식 유효성 검사
    /// </summary>
    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(email,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            return email.Contains("@") && email.Contains(".");
        }
    }

    /// <summary>
    /// 현재 로그인된 사용자가 있는지 확인하고 자동 로그인 처리
    /// </summary>
    private void CheckCurrentUser()
    {
        if (auth != null && auth.CurrentUser != null)
        {
            Firebase.Auth.FirebaseUser user = auth.CurrentUser;

            user.TokenAsync(true).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                {
                    string idToken = task.Result;
                    string userId = user.UserId;
                    Debug.Log($"자동 로그인 성공: User ID = {userId}");

                    PlayerPrefs.SetString("CurrentUserId", userId);
                    PlayerPrefs.Save();

                    // 서버에 토큰 검증 요청을 보내 Firestore 유저 데이터 무결성(누락 필드 자동 생성)을 동기화합니다.
                    VerifyTokenRequestData requestData = new VerifyTokenRequestData { token = idToken };
                    string jsonRequestBody = JsonUtility.ToJson(requestData);
                    StartCoroutine(SendVerifyTokenRequest(jsonRequestBody));
                }
                else
                {
                    Debug.LogWarning($"자동 로그인 실패: {task.Exception?.GetBaseException()?.Message}");
                    auth.SignOut();
                    PlayerPrefs.DeleteKey("CurrentUserId");
                }
            });
        }
        else
        {
            Debug.Log("로그인된 사용자가 없습니다. 로그인 UI를 표시합니다.");
        }
    }

    /// <summary>
    /// 회원가입 버튼 클릭 시 호출
    /// </summary>
    public void OnSignupButtonClicked()
    {
        EnsureLogTextReferences();

        string email = emailInputField != null ? emailInputField.text.Trim() : "";
        string password = passwordInputField != null ? passwordInputField.text : "";
        string username = usernameInputField != null ? usernameInputField.text.Trim() : "";

        // 1. 아이디(이메일) 검증
        if (string.IsNullOrWhiteSpace(email))
        {
            DisplayUIMessage(messageText, "아이디(이메일)를 입력해주세요.", Color.red);
            return;
        }
        if (!IsValidEmail(email))
        {
            DisplayUIMessage(messageText, "아이디가 잘못되었습니다.", Color.red);
            return;
        }

        // 2. 비밀번호 검증
        if (string.IsNullOrWhiteSpace(password))
        {
            DisplayUIMessage(messageText, "비밀번호를 입력해주세요.", Color.red);
            return;
        }
        if (password.Length < 6)
        {
            DisplayUIMessage(messageText, "비밀번호는 6자리 이상이어야 합니다.", Color.red);
            return;
        }

        // 3. 닉네임 검증
        if (string.IsNullOrWhiteSpace(username))
        {
            DisplayUIMessage(messageText, "닉네임을 입력해주세요.", Color.red);
            return;
        }

        DisplayUIMessage(messageText, "회원가입 요청 중...", Color.yellow);
        SignupRequestData requestData = new SignupRequestData { email = email, password = password, username = username };
        string jsonRequestBody = JsonUtility.ToJson(requestData);
        StartCoroutine(SendSignupRequest(jsonRequestBody));
    }

    /// <summary>
    /// 로그인 버튼 클릭 시 호출
    /// </summary>
    public void OnLoginButtonClicked()
    {
        EnsureLogTextReferences();

        string email = emailInputField_login != null ? emailInputField_login.text.Trim() : "";
        string password = passwordInputField_login != null ? passwordInputField_login.text : "";

        // 1. 아이디(이메일) 검증
        if (string.IsNullOrWhiteSpace(email))
        {
            DisplayUIMessage(messageTextLogin, "아이디(이메일)를 입력해주세요.", Color.red);
            return;
        }
        if (!IsValidEmail(email))
        {
            DisplayUIMessage(messageTextLogin, "아이디가 잘못되었습니다.", Color.red);
            return;
        }

        // 2. 비밀번호 검증
        if (string.IsNullOrWhiteSpace(password))
        {
            DisplayUIMessage(messageTextLogin, "비밀번호를 입력해주세요.", Color.red);
            return;
        }
        if (password.Length < 6)
        {
            DisplayUIMessage(messageTextLogin, "비밀번호는 6자리 이상이어야 합니다.", Color.red);
            return;
        }

        DisplayUIMessage(messageTextLogin, "서버 로그인 요청 중...", Color.yellow);

        LoginRequestData requestData = new LoginRequestData { email = email, password = password };
        string jsonRequestBody = JsonUtility.ToJson(requestData);
        StartCoroutine(SendLoginRequest(jsonRequestBody));
    }

    /// <summary>
    /// 서버 로그인 API 호출 코루틴 (POST /api/auth/login)
    /// 서버에서 비밀번호를 검증하고 Custom Token 및 유저 데이터를 받아옵니다.
    /// </summary>
    private IEnumerator SendLoginRequest(string jsonRequestBody)
    {
        using (UnityWebRequest webRequest = new UnityWebRequest(loginApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                AuthApiResponse response = JsonConvert.DeserializeObject<AuthApiResponse>(webRequest.downloadHandler.text);
                if (response != null && response.status == "success")
                {
                    CurrentUserData = response.userData;
                    if (GameClient.Instance != null)
                    {
                        GameClient.Instance.UserUid = response.user_id;
                        if (response.userData != null)
                        {
                            GameClient.Instance.SetUserData(response.userData);
                        }
                    }
                    PlayerPrefs.SetString("CurrentUserId", response.user_id);
                    PlayerPrefs.Save();

                    // 서버가 발급해 준 Custom Token으로 클라이언트 Firebase SDK 세션 동기화 (Firestore 리스너 등 지원)
                    if (auth != null && !string.IsNullOrEmpty(response.customToken))
                    {
                        auth.SignInWithCustomTokenAsync(response.customToken).ContinueWithOnMainThread(task =>
                        {
                            if (task.IsFaulted)
                            {
                                Debug.LogWarning($"Firebase 세션 동기화 경고: {task.Exception?.GetBaseException()?.Message}");
                            }
                            DisplayUIMessage(messageTextLogin, $"로그인 성공! 환영합니다.", Color.green);
                            SceneManager.LoadScene("MainScene");
                        });
                    }
                    else
                    {
                        DisplayUIMessage(messageTextLogin, $"로그인 성공! 환영합니다.", Color.green);
                        SceneManager.LoadScene("MainScene");
                    }
                }
                else
                {
                    DisplayUIMessage(messageTextLogin, $"로그인 실패: {response?.message}", Color.red);
                }
            }
            else
            {
                string serverMsg = "";
                try
                {
                    if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                    {
                        AuthApiResponse errRes = JsonConvert.DeserializeObject<AuthApiResponse>(webRequest.downloadHandler.text);
                        serverMsg = errRes?.message ?? "";
                    }
                }
                catch { }

                if (!string.IsNullOrEmpty(serverMsg))
                {
                    DisplayUIMessage(messageTextLogin, serverMsg, Color.red);
                }
                else
                {
                    DisplayUIMessage(messageTextLogin, "서버와의 통신에 실패했습니다. 네트워크를 확인해주세요.", Color.red);
                }
            }
        }
    }

    /// <summary>
    /// 로그아웃 버튼 클릭 시 호출되는 함수 (새로 추가된 기능)
    /// </summary>
    public void OnLogoutButtonClicked()
    {
        // Firebase Auth 인스턴스가 있고, 현재 로그인된 사용자가 있는지 확인
        if (isFirebaseReady && auth.CurrentUser != null)
        {
            Debug.Log($"로그아웃 요청: {auth.CurrentUser.UserId}");

            // Firebase에서 로그아웃
            auth.SignOut();

            // 기기에 저장된 사용자 ID 정보 삭제
            PlayerPrefs.DeleteKey("CurrentUserId");
            PlayerPrefs.Save();

            Debug.Log("로그아웃 성공 및 로컬 데이터 삭제 완료.");

            // 로그인 씬으로 돌아가기 (현재 씬을 다시 로드)
            // 이를 통해 Start() -> CheckCurrentUser()가 다시 실행되며 로그인 UI가 표시됨
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else
        {
            Debug.LogWarning("로그인된 사용자가 없어 로그아웃을 진행할 수 없습니다.");
        }
    }


    // --- 서버 통신 코루틴들 ---

    private IEnumerator SendSignupRequest(string jsonRequestBody)
    {
        using (UnityWebRequest webRequest = new UnityWebRequest(signupApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                AuthApiResponse response = JsonConvert.DeserializeObject<AuthApiResponse>(webRequest.downloadHandler.text);
                if (response != null && response.status == "success")
                {
                    DisplayUIMessage(messageText, response?.message ?? "회원가입이 완료되었습니다!", Color.green);
                }
                else
                {
                    DisplayUIMessage(messageText, response?.message ?? "회원가입에 실패했습니다.", Color.red);
                }
            }
            else
            {
                string serverMsg = "";
                try
                {
                    if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                    {
                        AuthApiResponse errRes = JsonConvert.DeserializeObject<AuthApiResponse>(webRequest.downloadHandler.text);
                        serverMsg = errRes?.message ?? "";
                    }
                }
                catch { }

                if (!string.IsNullOrEmpty(serverMsg))
                {
                    DisplayUIMessage(messageText, serverMsg, Color.red);
                }
                else
                {
                    DisplayUIMessage(messageText, "서버와의 통신에 실패했습니다. 네트워크를 확인해주세요.", Color.red);
                }
            }
        }
    }

    private IEnumerator SendVerifyTokenRequest(string jsonRequestBody)
    {
        using (UnityWebRequest webRequest = new UnityWebRequest(verifyTokenApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                AuthApiResponse response = JsonConvert.DeserializeObject<AuthApiResponse>(webRequest.downloadHandler.text);
                if (response != null && response.status == "success")
                {
                    CurrentUserData = response.userData;
                    if (GameClient.Instance != null)
                    {
                        GameClient.Instance.UserUid = response.user_id;
                        if (response.userData != null)
                        {
                            GameClient.Instance.SetUserData(response.userData);
                        }
                    }
                    PlayerPrefs.SetString("CurrentUserId", response.user_id);
                    PlayerPrefs.Save();
                    DisplayUIMessage(messageTextLogin, $"로그인 성공! 사용자 ID: {response.user_id}", Color.green);
                    // 로그인 성공 후 메인 게임 씬으로 전환
                    SceneManager.LoadScene("MainScene");
                }
                else
                {
                    auth?.SignOut();
                    PlayerPrefs.DeleteKey("CurrentUserId");
                    DisplayUIMessage(messageTextLogin, $"세션이 만료되었습니다. 다시 로그인해주세요.", Color.red);
                }
            }
            else
            {
                auth?.SignOut();
                PlayerPrefs.DeleteKey("CurrentUserId");
                string serverMsg = "";
                try
                {
                    if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                    {
                        AuthApiResponse errRes = JsonConvert.DeserializeObject<AuthApiResponse>(webRequest.downloadHandler.text);
                        serverMsg = errRes?.message ?? "";
                    }
                }
                catch { }

                if (!string.IsNullOrEmpty(serverMsg))
                {
                    DisplayUIMessage(messageTextLogin, $"인증 만료: {serverMsg}", Color.red);
                }
                else
                {
                    DisplayUIMessage(messageTextLogin, $"서버 인증 오류. 다시 로그인해주세요.", Color.red);
                }
            }
        }
    }

    /// <summary>
    /// UI에 메시지를 표시하는 통합 함수
    /// </summary>
    private void DisplayUIMessage(TextMeshProUGUI textElement, string message, Color color)
    {
        if (textElement != null)
        {
            textElement.color = color;
            textElement.text = message;
        }
    }
}


