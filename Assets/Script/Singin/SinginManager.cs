using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

// 서버로 보낼 JSON 데이터 구조를 정의하는 클래스
// [System.Serializable] 속성은 JsonUtility가 이 클래스를 JSON으로 변환할 수 있게 합니다.
[System.Serializable]
public class SignupRequestData
{
    public string email;
    public string password;
    public string username;
}

[System.Serializable]
public class LoginRequestData
{
    public string email;
    public string password;
}

// 서버 응답 JSON 데이터 구조를 정의하는 클래스 (회원가입 및 로그인 공용)
// 로그인 시 access_token, refresh_token, expires_in 필드가 추가될 수 있으므로 포함
[System.Serializable]
public class AuthApiResponse
{
    public string status;
    public string message;
    public string user_id;
    public string access_token;  // 로그인 성공 시 JWT 토큰
    public string refresh_token; // 토큰 갱신용
    public int expires_in;       // 토큰 만료까지 남은 시간 (초)
}
public class SinginManager : MonoBehaviour
{
    // --- UI InputField 참조 ---
    // 인스펙터에서 각 InputField를 드래그하여 할당합니다.
    public TMP_InputField emailInputField;    // 이메일 입력 필드
    public TMP_InputField passwordInputField; // 비밀번호 입력 필드
    public TMP_InputField usernameInputField; // 사용자 이름 입력 필드

    // --- 메시지 표시용 Text (선택 사항) ---
    // 사용자에게 성공/실패 메시지를 보여줄 Text 컴포넌트
    public TextMeshProUGUI messageText; // TextMeshPro Text 사용

    // Flask 서버의 회원가입 API URL
    public string signupApiUrl = "http://localhost:5000/api/auth/signup";

    // --- UI InputField 참조 (로그인) ---
    public TMP_InputField emailInputField_login;    // 로그인 이메일 입력 필드
    public TMP_InputField passwordInputField_login; // 로그인 비밀번호 입력 필드

    // --- 메시지 표시용 Text (로그인) ---
    public TextMeshProUGUI messageTextLogin; // TextMeshPro Text 사용 (로그인 메시지)

    // Flask 서버의 로그인 API URL
    public string loginApiUrl = "http://localhost:5000/api/auth/login";

    public void OnSignupButtonClicked()
    {
        // InputField에서 입력된 텍스트 값을 가져옵니다.
        string email = emailInputField != null ? emailInputField.text : "";
        string password = passwordInputField != null ? passwordInputField.text : "";
        string username = usernameInputField != null ? usernameInputField.text : "";

        // 입력 값 유효성 검사 (간단한 예시)
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            DisplayMessage("이메일과 비밀번호를 입력해주세요.", Color.red);
            return; // 필수 입력값이 없으면 함수 종료
        }

        DisplayMessage("회원가입 요청 전송 중...", Color.yellow);

        // SignupRequestData 객체 생성 및 값 할당
        SignupRequestData requestData = new SignupRequestData
        {
            email = email,
            password = password,
            username = username
        };
        // 객체를 JSON 문자열로 변환
        string jsonRequestBody = JsonUtility.ToJson(requestData);
        Debug.Log($"전송할 JSON: {jsonRequestBody}");

        // Flask 서버의 회원가입 API를 호출하는 코루틴 시작
        StartCoroutine(SendSignupRequest(jsonRequestBody));
    }

    // 서버로 회원가입 요청을 보내는 코루틴
    private IEnumerator SendSignupRequest(string jsonRequestBody)
    {
        using (UnityWebRequest webRequest = new UnityWebRequest(signupApiUrl, "POST"))
        {
            // 요청 본문에 JSON 데이터 추가
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer(); // 응답 받을 준비

            // Content-Type 헤더 설정 (서버가 JSON을 예상하도록)
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // 요청 보내기
            yield return webRequest.SendWebRequest();

            // 응답 결과 확인
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    DisplayMessage($"네트워크 연결 오류: {webRequest.error}", Color.red);
                    Debug.LogError($"회원가입 네트워크 오류: {webRequest.error}");
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    // 서버에서 HTTP 오류 코드(4xx, 5xx)를 반환했을 때
                    string errorResponse = webRequest.downloadHandler.text;
                    Debug.LogError($"회원가입 HTTP 오류: {webRequest.responseCode} - {webRequest.error}");
                    Debug.LogError($"서버 응답 오류: {errorResponse}");

                    try
                    {
                        // 서버 응답을 파싱하여 구체적인 오류 메시지 표시
                        AuthApiResponse apiErrorResponse = JsonUtility.FromJson<AuthApiResponse>(errorResponse);
                        DisplayMessage($"회원가입 실패: {apiErrorResponse.message}", Color.red);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"오류 응답 파싱 실패: {e.Message}");
                        DisplayMessage($"회원가입 실패: 알 수 없는 오류 ({webRequest.responseCode})", Color.red);
                    }
                    break;
                case UnityWebRequest.Result.Success:
                    // 요청 성공
                    string jsonResponse = webRequest.downloadHandler.text;
                    Debug.Log($"회원가입 성공 응답: {jsonResponse}");

                    try
                    {
                        // JSON 응답을 C# 객체로 역직렬화
                        AuthApiResponse apiResponse = JsonUtility.FromJson<AuthApiResponse>(jsonResponse);

                        if (apiResponse.status == "success")
                        {
                            DisplayMessage($"회원가입 성공! 사용자 ID: {apiResponse.user_id}", Color.green);
                            Debug.Log($"회원가입 성공! 사용자 ID: {apiResponse.user_id}");
                            // TODO: 로그인 씬으로 전환하거나 다음 단계로 진행하는 로직 추가
                        }
                        else
                        {
                            DisplayMessage($"회원가입 실패: {apiResponse.message}", Color.red);
                            Debug.LogError($"회원가입 실패 (서버 메시지): {apiResponse.message}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"회원가입 응답 JSON 파싱 오류: {e.Message}");
                        Debug.LogError($"오류 발생 JSON: {jsonResponse}");
                        DisplayMessage("회원가입 실패: 응답 파싱 오류", Color.red);
                    }
                    break;
                default:
                    DisplayMessage($"알 수 없는 오류: {webRequest.result} - {webRequest.error}", Color.red);
                    Debug.LogError($"알 수 없는 UnityWebRequest 오류: {webRequest.result} - {webRequest.error}");
                    break;
            }
        }
    }

    public void OnLoginButtonClicked()
    {
        // InputField에서 입력된 텍스트 값을 가져옵니다.
        string email = emailInputField_login != null ? emailInputField_login.text : "";
        string password = passwordInputField_login != null ? passwordInputField_login.text : "";

        // 입력 값 유효성 검사 (간단한 예시)
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            DisplayMessageLogin("이메일과 비밀번호를 입력해주세요.", Color.red);
            return; // 필수 입력값이 없으면 함수 종료
        }

        DisplayMessageLogin("로그인 요청 전송 중...", Color.yellow);

        // SignupRequestData 객체 생성 및 값 할당
        LoginRequestData requestData = new LoginRequestData
        {
            email = email,
            password = password,
        };
        // 객체를 JSON 문자열로 변환
        string jsonRequestBody = JsonUtility.ToJson(requestData);
        Debug.Log($"전송할 JSON: {jsonRequestBody}");

        // Flask 서버의 회원가입 API를 호출하는 코루틴 시작
        StartCoroutine(SendLoginRequest(jsonRequestBody));
    }

    // 서버로 로그인 요청을 보내는 코루틴
    private IEnumerator SendLoginRequest(string jsonRequestBody)
    {
        using (UnityWebRequest webRequest = new UnityWebRequest(loginApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    DisplayMessageLogin($"네트워크 연결 오류: {webRequest.error}", Color.red);
                    Debug.LogError($"로그인 네트워크 오류: {webRequest.error}");
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    string errorResponse = webRequest.downloadHandler.text;
                    Debug.LogError($"로그인 HTTP 오류: {webRequest.responseCode} - {webRequest.error}");
                    Debug.LogError($"서버 응답 오류: {errorResponse}");

                    try
                    {
                        AuthApiResponse apiErrorResponse = JsonUtility.FromJson<AuthApiResponse>(errorResponse);
                        DisplayMessageLogin($"로그인 실패: {apiErrorResponse.message}", Color.red);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"로그인 오류 응답 파싱 실패: {e.Message}");
                        DisplayMessageLogin($"로그인 실패: 알 수 없는 오류 ({webRequest.responseCode})", Color.red);
                    }
                    break;
                case UnityWebRequest.Result.Success:
                    string jsonResponse = webRequest.downloadHandler.text;
                    Debug.Log($"로그인 성공 응답: {jsonResponse}");

                    try
                    {
                        AuthApiResponse apiResponse = JsonUtility.FromJson<AuthApiResponse>(jsonResponse);

                        if (apiResponse.status == "success")
                        {
                            // 로그인 성공 시 JWT 토큰 및 사용자 ID 저장 (예: PlayerPrefs)
                            PlayerPrefs.SetString("UserAccessToken", apiResponse.access_token);
                            PlayerPrefs.SetString("UserRefreshToken", apiResponse.refresh_token);
                            PlayerPrefs.SetString("CurrentUserId", apiResponse.user_id);
                            PlayerPrefs.SetInt("AccessTokenExpiresIn", apiResponse.expires_in);
                            PlayerPrefs.Save(); // 변경사항 저장

                            DisplayMessageLogin($"로그인 성공! 사용자 ID: {apiResponse.user_id}", Color.green);
                            Debug.Log($"로그인 성공! 사용자 ID: {apiResponse.user_id}, Access Token: {apiResponse.access_token}");
                            // TODO: 다음 씬으로 전환하거나 게임 로비로 이동하는 로직 추가
                        }
                        else
                        {
                            DisplayMessageLogin($"로그인 실패: {apiResponse.message}", Color.red);
                            Debug.LogError($"로그인 실패 (서버 메시지): {apiResponse.message}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"로그인 응답 JSON 파싱 오류: {e.Message}");
                        Debug.LogError($"오류 발생 JSON: {jsonResponse}");
                        DisplayMessageLogin("로그인 실패: 응답 파싱 오류", Color.red);
                    }
                    break;
                default:
                    DisplayMessageLogin($"알 수 없는 오류: {webRequest.result} - {webRequest.error}", Color.red);
                    Debug.LogError($"알 수 없는 UnityWebRequest 오류: {webRequest.result} - {webRequest.error}");
                    break;
            }
        }
    }

    // 외부에서 메시지를 업데이트할 수 있는 함수 (선택 사항)
    public void DisplayMessage(string message, Color color)
    {
        if (messageText != null)
        {
            messageText.color = color;
            messageText.text = message;
        }
    }

    public void DisplayMessageLogin(string message, Color color)
    {
        if (messageTextLogin != null)
        {
            messageTextLogin.color = color;
            messageTextLogin.text = message;
        }
    }
}

