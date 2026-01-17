using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// 필터 UI의 개별 버튼(토글) 스크립트입니다.
/// FilterManager에 의해 자동으로 생성되고 설정됩니다.
/// </summary>
public class FilterToggle : MonoBehaviour
{
    [Header("UI 연결")]
    // 실제 체크박스 기능을 하는 컴포넌트
    [SerializeField] private Toggle toggle;
    // 버튼 옆에 표시될 글씨 (예: "전설", "일반")
    [SerializeField] private TextMeshProUGUI labelText;

    private string _label;

    /// <summary>
    /// [설정 함수] 매니저가 이 버튼을 만들 때 호출하여 정보를 입력해줍니다.
    /// </summary>
    /// <param name="label">표시할 이름</param>
    /// <param name="group">소속될 라디오 버튼 그룹</param>
    /// <param name="onValueChangedAction">클릭되었을 때 실행할 함수</param>
    public void Setup(string label, ToggleGroup group, UnityAction<bool> onValueChangedAction)
    {
        _label = label;

        // 1. 텍스트 설정
        if (labelText != null)
        {
            labelText.text = label;
        }

        // 2. 토글 기능 설정
        if (toggle != null)
        {
            toggle.group = group; // 그룹 지정

            // 기존 연결 제거 (안전장치)
            toggle.onValueChanged.RemoveAllListeners();

            // 클릭 시 실행할 함수 연결
            toggle.onValueChanged.AddListener(onValueChangedAction);
        }
    }

    /// <summary>
    /// 코드로 버튼을 강제로 켜거나 끌 때 사용합니다.
    /// </summary>
    public void SetIsOn(bool isOn)
    {
        if (toggle != null)
        {
            toggle.isOn = isOn;
        }
    }
}