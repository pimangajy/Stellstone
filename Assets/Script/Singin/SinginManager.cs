using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement; // 씬 전환을 위해 추가
using Firebase;
using Firebase.Auth;

// 서버로 보낼/받을 데이터 구조를 정의하는 클래스들
[System.Serializable]
public class SignupRequestData { public string email; public string password; public string username; }
[System.Serializable]
public class VerifyTokenRequestData { public string token; }
[System.Serializable]
public class AuthApiResponse { public string status; public string message; public string user_id; }


public class SinginManager : MonoBehaviour
{
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
    public string signupApiUrl => GameClient.Instance.GetApiUrl("auth/signup");
    public string verifyTokenApiUrl => GameClient.Instance.GetApiUrl("auth/verify-token");

    // --- Firebase 관련 변수 ---
    private FirebaseAuth auth;
    private bool isFirebaseReady = false;


    // Start()는 로그인 씬이 로드될 때마다 실행
    void Start()
    {
        // 씬이 로드될 때마다 UI 메시지 초기화
        DisplayUIMessage(messageText, "", Color.black);
        DisplayUIMessage(messageTextLogin, "", Color.black);

        // Firebase 초기화는 앱 전체에서 한 번만 실행되도록 SDK가 처리해 줍니다.
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                // auth 인스턴스를 가져옵니다. (SDK가 알아서 싱글톤으로 관리)
                auth = FirebaseAuth.DefaultInstance;
                isFirebaseReady = true;

                // 자동 로그인 체크
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
    /// 현재 로그인된 사용자가 있는지 확인하고 자동 로그인 처리
    /// </summary>
    private void CheckCurrentUser()
    {
        if (auth.CurrentUser != null)
        {
            Firebase.Auth.FirebaseUser user = auth.CurrentUser;

            // 토큰을 강제로 갱신하여 사용자가 서버에 실제로 유효한지 확인합니다.
            user.TokenAsync(true).ContinueWith(task => {
                if (task.IsCanceled || task.IsFaulted)
                {
                    // 토큰 갱신 실패! (계정이 삭제되었거나 비활성화된 경우)
                    Debug.LogWarning("자동 로그인 실패: 사용자가 서버에 존재하지 않거나 토큰이 만료되었습니다.");
                    auth.SignOut(); // 확실하게 로컬 세션도 정리
                    PlayerPrefs.DeleteKey("CurrentUserId");
                    return;
                }

                // 토큰 갱신 성공! 진짜 유효한 사용자.
                string userId = user.UserId;
                Debug.Log($"자동 로그인 성공: User ID = {userId}");

                // PlayerPrefs 저장은 메인 스레드에서 해야 안전합니다.
                // (실제 구현 시에는 UnityMainThreadDispatcher 같은 도구 사용 권장)
                PlayerPrefs.SetString("CurrentUserId", userId);

                // 예시: 자동 로그인 성공 시 바로 메인 게임 씬으로 전환
                // SceneManager.LoadScene("MainGameScene"); 
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
        string email = emailInputField.text;
        string password = passwordInputField.text;
        string username = usernameInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            DisplayUIMessage(messageText, "이메일과 비밀번호를 입력해주세요.", Color.red);
            return;
        }

        DisplayUIMessage(messageText, "회원가입 요청중...", Color.yellow);
        SignupRequestData requestData = new SignupRequestData { email = email, password = password, username = username };
        string jsonRequestBody = JsonUtility.ToJson(requestData);
        StartCoroutine(SendSignupRequest(jsonRequestBody));
    }

    /// <summary>
    /// 로그인 버튼 클릭 시 호출
    /// </summary>
    public async void OnLoginButtonClicked()
    {
        if (!isFirebaseReady)
        {
            DisplayUIMessage(messageTextLogin, "Firebase가 준비되지 않았습니다. 잠시 후 다시 시도해주세요.", Color.red);
            return;
        }

        string email = emailInputField_login.text;
        string password = passwordInputField_login.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            DisplayUIMessage(messageTextLogin, "이메일과 비밀번호를 입력해주세요.", Color.red);
            return;
        }

        DisplayUIMessage(messageTextLogin, "로그인 중...", Color.yellow);

        try
        {
            AuthResult authResult = await auth.SignInWithEmailAndPasswordAsync(email, password);
            FirebaseUser user = authResult.User;
            Debug.Log($"Firebase 로그인 성공: {user.UserId}");

            string idToken = await user.TokenAsync(true);
            Debug.Log($"Firebase ID Token acquired.");

            VerifyTokenRequestData requestData = new VerifyTokenRequestData { token = idToken };
            string jsonRequestBody = JsonUtility.ToJson(requestData);
            StartCoroutine(SendVerifyTokenRequest(jsonRequestBody));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Firebase 로그인 오류: {ex.Message}");
            DisplayUIMessage(messageTextLogin, $"로그인 실패: {ex.GetBaseException().Message}", Color.red);
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
                AuthApiResponse response = JsonUtility.FromJson<AuthApiResponse>(webRequest.downloadHandler.text);
                if (response.status == "success")
                {
                    DisplayUIMessage(messageText, $"회원가입 성공! 사용자 ID: {response.user_id}", Color.green);
                }
                else
                {
                    DisplayUIMessage(messageText, $"회원가입 실패: {response.message}", Color.red);
                }
            }
            else
            {
                DisplayUIMessage(messageText, $"오류: {webRequest.error}", Color.red);
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
                AuthApiResponse response = JsonUtility.FromJson<AuthApiResponse>(webRequest.downloadHandler.text);
                if (response.status == "success")
                {
                    PlayerPrefs.SetString("CurrentUserId", response.user_id);
                    PlayerPrefs.Save();
                    DisplayUIMessage(messageTextLogin, $"로그인 성공! 사용자 ID: {response.user_id}", Color.green);
                    // TODO: 로그인 성공 후 메인 게임 씬으로 전환
                    // SceneManager.LoadScene("MainGameScene");
                }
                else
                {
                    DisplayUIMessage(messageTextLogin, $"로그인 실패: {response.message}", Color.red);
                }
            }
            else
            {
                DisplayUIMessage(messageTextLogin, $"서버 검증 오류: {webRequest.error}", Color.red);
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


