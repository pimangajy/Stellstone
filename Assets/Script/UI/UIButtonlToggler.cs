using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 여러 개의 버튼을 하나의 탭 그룹으로 묶어 관리합니다.
/// 하나의 버튼이 선택되면, 해당 버튼의 이미지를 '선택 상태'로 바꾸고
/// 나머지 모든 버튼은 '기본 상태'로 되돌립니다.
/// </summary>
public class UIButtonlToggler : MonoBehaviour
{
    [Header("탭 버튼 설정")]
    [Tooltip("그룹으로 관리할 모든 버튼을 여기에 연결해주세요.")]
    public List<Button> tabButtons;

    [Header("상태별 이미지")]
    [Tooltip("버튼이 선택되지 않았을 때의 기본 이미지입니다.")]
    public Sprite normalSprite;

    [Tooltip("버튼이 선택되었을 때의 활성화 이미지입니다.")]
    public Sprite selectedSprite;

    // 현재 선택된 버튼을 기억하기 위한 변수
    private Button selectedButton;

    /// <summary>
    /// 스크립트가 처음 시작될 때 한 번 호출됩니다.
    /// </summary>
    void Start()
    {
        // 모든 버튼에 클릭 이벤트를 동적으로 추가합니다.
        foreach (Button button in tabButtons)
        {
            // 각 버튼의 onClick 이벤트에 'SelectTab' 함수를 연결합니다.
            // 버튼이 클릭되면, 자기 자신을 인자로 넘겨주며 SelectTab 함수를 호출하게 됩니다.
            button.onClick.AddListener(() => SelectTab(button));
        }

        // 게임 시작 시, 기본으로 첫 번째 버튼을 선택된 상태로 만듭니다.
        if (tabButtons.Count > 0)
        {
            SelectTab(tabButtons[0]);
        }
    }

    /// <summary>
    /// 특정 버튼을 선택된 상태로 만들고, 나머지는 기본 상태로 되돌립니다.
    /// </summary>
    /// <param name="clickedButton">플레이어가 클릭한 버튼</param>
    public void SelectTab(Button clickedButton)
    {
        // 만약 이전에 선택된 버튼이 있다면, 그 버튼을 기본 상태로 되돌립니다.
        if (selectedButton != null)
        {
            selectedButton.GetComponent<Image>().sprite = normalSprite;
        }

        // 새로 클릭된 버튼을 '선택된 버튼'으로 기억합니다.
        selectedButton = clickedButton;

        // 새로 선택된 버튼의 이미지를 '선택 상태' 이미지로 변경합니다.
        selectedButton.GetComponent<Image>().sprite = selectedSprite;

        // 여기에 추가로, 선택된 탭에 맞는 콘텐츠 패널을 보여주는 로직을 넣을 수 있습니다.
        
    }
}
