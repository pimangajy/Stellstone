using UnityEngine;
using UnityEngine.UI; // Button 사용을 위해 추가
using TMPro; // TextMeshPro 사용을 위해 추가

public class InputFieldController : MonoBehaviour
{
    // 유니티 에디터에서 연결할 변수들
    public TMP_InputField nameInputField; // 입력 필드
    public TMP_Text infoText; // 결과를 표시할 텍스트
    public Button submitButton; // 확인 버튼

    void Start()
    {
        // 1. 버튼 클릭 이벤트에 함수 연결
        // 버튼이 클릭되면 UpdateText_ByButton 메서드를 호출하도록 설정
        submitButton.onClick.AddListener(UpdateText_ByButton);

        // 2. 인풋 필드에서 엔터(submit) 이벤트에 함수 연결
        // 입력 완료(엔터 또는 포커스 아웃) 시 UpdateText_ByEnter 메서드를 호출하도록 설정
        nameInputField.onSubmit.AddListener(UpdateText_ByEnter);
    }

    // 인풋 필드에 있는 텍스트를 결과 텍스트에 업데이트하는 함수
    private void UpdateText()
    {
        // 인풋 필드의 텍스트를 가져와 결과 텍스트에 할당
        infoText.text = nameInputField.text;
    }

    // 확인 버튼을 눌렀을 때 호출될 함수
    public void UpdateText_ByButton()
    {
        // 1. 인풋필드의 텍스트를 가져온다.
        string inputText = nameInputField.text;

        // 2. 입력값이 비어있는지 확인한다.
        if (string.IsNullOrEmpty(inputText))
        {
            // 2-1. 비어있을 경우
            Debug.Log("입력값이 없습니다.");
        }
        else
        {
            // 2-2. 비어있지 않을 경우
            infoText.text = inputText;
            DeckManager.instance.UpdateDeckname(inputText);
        }

        nameInputField.text = "";
    }

    // 엔터 키를 눌렀을 때 호출될 함수 (매개변수가 필요하지만 사용하지 않음)
    public void UpdateText_ByEnter(string text)
    {
        // onSubmit 이벤트는 현재 입력된 텍스트를 매개변수(text)로 전달해줍니다.
        if (string.IsNullOrEmpty(text))
        {
            Debug.Log("입력값이 없습니다.");
        }
        else
        {
            infoText.text = text;
            DeckManager.instance.UpdateDeckname(text);
            UIManager.Instance.ClosePopup();
        }

        nameInputField.text = "";
    }
}