using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 덱 이름을 변경하는 입력창(Input Field)을 관리합니다.
/// 확인 버튼을 누르거나 엔터를 쳤을 때 이름을 반영합니다.
/// </summary>
public class InputFieldController : MonoBehaviour
{
    public TMP_InputField nameInputField; // 입력창
    public TMP_Text infoText; // (옵션) 결과를 보여줄 텍스트
    public Button submitButton; // 확인 버튼

    void Start()
    {
        // 1. 버튼 클릭 시 UpdateText_ByButton 실행
        submitButton.onClick.AddListener(UpdateText_ByButton);

        // 2. 엔터 입력 시 UpdateText_ByEnter 실행
        nameInputField.onSubmit.AddListener(UpdateText_ByEnter);
    }

    // (참고용) 단순히 텍스트만 옮기는 함수
    private void UpdateText()
    {
        infoText.text = nameInputField.text;
    }

    // 버튼 눌렀을 때
    public void UpdateText_ByButton()
    {
        string inputText = nameInputField.text;

        // 빈 칸이면 무시
        if (string.IsNullOrEmpty(inputText))
        {
            Debug.Log("입력값이 없습니다.");
        }
        else
        {
            // 덱 매니저에게 이름 변경 요청
            infoText.text = inputText;
            DeckManager.instance.UpdateDeckname(inputText);
        }

        nameInputField.text = ""; // 입력창 비우기
    }

    // 엔터 쳤을 때 (onSubmit은 입력된 텍스트를 매개변수로 줍니다)
    public void UpdateText_ByEnter(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.Log("입력값이 없습니다.");
        }
        else
        {
            infoText.text = text;
            DeckManager.instance.UpdateDeckname(text);
            UIManager.Instance.ClosePopup(); // 팝업 닫기
        }

        nameInputField.text = "";
    }
}