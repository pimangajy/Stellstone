using UnityEngine;
using TMPro;

/// <summary>
/// Tab / Shift+Tab 키로 등록된 InputField 목록 간 포커스를 순환 이동시키는 컨트롤러입니다.
/// </summary>
public class LoginTabController : MonoBehaviour
{
    [Header("순서대로 이동할 입력창 목록")]
    [SerializeField] private TMP_InputField[] inputFields;


    void Update()
    {
        if (inputFields == null || inputFields.Length == 0) return;

        // 1. Shift + Tab (이전 입력칸으로)
        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.Tab))
        {
            NavigateTab(-1);
        }
        // 2. Tab (다음 입력칸으로)
        else if (Input.GetKeyDown(KeyCode.Tab))
        {
            NavigateTab(1);
        }
    }

    private void NavigateTab(int direction)
    {
        // 현재 포커스되어 있는 필드의 인덱스 찾기
        int currentIndex = -1;
        for (int i = 0; i < inputFields.Length; i++)
        {
            if (inputFields[i] != null && inputFields[i].isFocused)
            {
                currentIndex = i;
                break;
            }
        }

        // 아무 것도 포커스되어 있지 않다면 첫 번째 필드 선택
        if (currentIndex == -1)
        {
            SelectInputField(0);
            return;
        }

        // 방향에 따라 다음 인덱스 계산 (순환)
        int nextIndex = currentIndex + direction;
        if (nextIndex >= inputFields.Length) nextIndex = 0;
        else if (nextIndex < 0) nextIndex = inputFields.Length - 1;

        SelectInputField(nextIndex);
    }

    private void SelectInputField(int index)
    {
        if (index >= 0 && index < inputFields.Length && inputFields[index] != null)
        {
            inputFields[index].Select();
            inputFields[index].ActivateInputField(); // 깜빡이는 커서 활성화 (즉시 타이핑 가능)
        }
    }
}
