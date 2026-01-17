using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro를 사용하기 위해 필요합니다.
using UnityEngine.Events; // 이벤트를 사용하기 위해 필요합니다.

/// <summary>
/// 필터 버튼(토글) 하나하나에 붙어있는 스크립트입니다.
/// 버튼의 글자(라벨)를 바꾸거나, 클릭되었을 때 어떤 행동을 할지 설정합니다.
/// </summary>
public class FilterToggle : MonoBehaviour
{
    [Header("UI 연결")]
    // 유니티 인스펙터에서 연결해줘야 하는 실제 체크박스 컴포넌트
    [SerializeField] private Toggle toggle;
    // 버튼 옆에 표시될 글자 (예: "전사", "전설" 등)
    [SerializeField] private TextMeshProUGUI labelText;

    // 디버깅용으로 현재 이 버튼이 무슨 라벨인지 저장해두는 변수
    private string _label;

    /// <summary>
    /// [초기화 함수] 버튼이 생성될 때 FilterManager가 이 함수를 호출해서 세팅해줍니다.
    /// </summary>
    /// <param name="label">버튼에 표시될 이름 (예: "마법사")</param>
    /// <param name="group">이 버튼이 속할 그룹 (직업 그룹, 희귀도 그룹 등)</param>
    /// <param name="onValueChangedAction">버튼이 눌렸을 때 실행할 함수(기능)</param>
    public void Setup(string label, ToggleGroup group, UnityAction<bool> onValueChangedAction)
    {
        _label = label; // 내부 변수에 이름 저장

        // 1. 화면에 보이는 글자를 설정합니다.
        if (labelText != null)
        {
            labelText.text = label;
        }

        // 2. 토글(체크박스)의 기능을 설정합니다.
        if (toggle != null)
        {
            // 이 버튼이 어느 그룹 소속인지 알려줍니다. 
            // (그룹으로 묶이면, 그 중 하나만 선택할 수 있게 됩니다. 라디오 버튼 기능)
            toggle.group = group;

            // 혹시라도 이전에 연결된 기능이 있다면 깔끔하게 지웁니다. (중복 실행 방지)
            toggle.onValueChanged.RemoveAllListeners();

            // "값이 바뀌면(클릭하면) 아까 받은 그 함수(onValueChangedAction)를 실행해!"라고 등록합니다.
            toggle.onValueChanged.AddListener(onValueChangedAction);
        }
    }

    /// <summary>
    /// 외부(FilterManager)에서 강제로 버튼을 켜거나 끌 때 사용합니다.
    /// 예: "초기화" 버튼을 눌렀을 때 모든 버튼을 끄고 "전체" 버튼만 켤 때 사용
    /// </summary>
    public void SetIsOn(bool isOn)
    {
        if (toggle != null)
        {
            // isOn이 true면 체크되고, false면 체크가 해제됩니다.
            // 주의: 이걸 코드로 바꾸면 onValueChanged 이벤트가 발동될 수 있습니다.
            toggle.isOn = isOn;
        }
    }
}