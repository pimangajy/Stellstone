using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class inputTXTClear : MonoBehaviour
{
    // --- UI InputField 참조 ---
    // 인스펙터에서 각 InputField를 드래그하여 할당합니다.
    public TMP_InputField emailInputField;    // 이메일 입력 필드
    public TMP_InputField passwordInputField; // 비밀번호 입력 필드
    public TMP_InputField usernameInputField; // 사용자 이름 입력 필드
    public bool Singin;

    public void TextClear()
    {
        emailInputField.text = "";
        passwordInputField.text = "";

        if (Singin)
        {
            usernameInputField.text = "";
        }
    }
}
