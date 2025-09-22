using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using System; // Action 이벤트를 위해 필요

/// <summary>
/// 필터 UI의 전반적인 동작을 관리하는 메인 스크립트입니다.
/// </summary>
public class FilterManager : MonoBehaviour
{
    // 필터 설정을 담을 구조체
    public struct FilterSettings
    {
        public string CardType;
        public string Rarity;
        public string Expansion;
    }

    // 필터 적용 시 호출될 이벤트. 다른 스크립트에서 이 이벤트를 구독하여 필터된 결과를 처리할 수 있습니다.
    public static event Action<FilterSettings> OnFilterApplied;

    [Header("필터 카테고리 (토글 그룹)")]
    [Tooltip("카드 종류 토글들을 그룹핑한 ToggleGroup")]
    [SerializeField] private ToggleGroup cardTypeGroup;

    [Tooltip("레어도 토글들을 그룹핑한 ToggleGroup")]
    [SerializeField] private ToggleGroup rarityGroup;

    [Tooltip("확장팩 토글들을 그룹핑한 ToggleGroup")]
    [SerializeField] private ToggleGroup expansionGroup;


    [Header("버튼")]
    [SerializeField] private Button applyButton;    // 적용 버튼
    [SerializeField] private Button resetButton;    // 초기화 버튼

    private void Start()
    {
        // 각 버튼에 리스너(OnClick 이벤트) 연결
        if (applyButton != null)
        {
            applyButton.onClick.AddListener(ApplyFilters);
        }
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetFilters);
        }
    }

    /// <summary>
    /// '적용' 버튼을 눌렀을 때 호출됩니다. 현재 선택된 필터 정보를 수집하고 이벤트를 발생시킵니다.
    /// </summary>
    public void ApplyFilters()
    {
        FilterSettings currentSettings = new FilterSettings
        {
            CardType = GetSelectedToggleValue(cardTypeGroup),
            Rarity = GetSelectedToggleValue(rarityGroup),
            Expansion = GetSelectedToggleValue(expansionGroup)
        };

        Debug.Log($"필터 적용: 카드 종류({currentSettings.CardType}), 레어도({currentSettings.Rarity}), 확장팩({currentSettings.Expansion})");

        // 필터 적용 이벤트를 발생시켜 다른 시스템(예: 카드 목록 UI)에 알립니다.
        OnFilterApplied?.Invoke(currentSettings);
    }

    /// <summary>
    /// '초기화' 버튼을 눌렀을 때 호출됩니다. 모든 필터를 기본값('전체' 또는 첫 번째 토글)으로 되돌립니다.
    /// </summary>
    public void ResetFilters()
    {
        ResetToggleGroup(cardTypeGroup);
        ResetToggleGroup(rarityGroup);
        ResetToggleGroup(expansionGroup);

        Debug.Log("필터가 초기화되었습니다.");

        // 초기화 후 바로 적용하고 싶다면 아래 주석을 해제하세요.
        // ApplyFilters();
    }

    /// <summary>
    /// 특정 토글 그룹에서 현재 활성화된 토글의 값을 가져옵니다.
    /// </summary>
    /// <param name="group">값을 가져올 ToggleGroup</param>
    /// <returns>선택된 토글의 텍스트 값</returns>
    private string GetSelectedToggleValue(ToggleGroup group)
    {
        if (group == null) return "전체";

        // 현재 활성화된 토글들을 찾습니다.
        Toggle activeToggle = group.ActiveToggles().FirstOrDefault();

        if (activeToggle != null)
        {
            // 활성화된 토글의 자식 Text 컴포넌트에서 텍스트를 가져옵니다.
            // 여기서는 토글의 이름으로 값을 식별한다고 가정합니다.
            // 더 좋은 방법은 FilterToggle.cs 같은 별도 컴포넌트를 사용하는 것입니다.
            Text toggleText = activeToggle.GetComponentInChildren<Text>();
            if (toggleText != null)
            {
                return toggleText.text;
            }
        }

        // 선택된 토글이 없으면 '전체'로 간주
        return "전체";
    }

    /// <summary>
    /// 특정 토글 그룹의 선택을 첫 번째 자식 토글(보통 '전체' 토글)로 설정합니다.
    /// </summary>
    /// <param name="group">초기화할 ToggleGroup</param>
    private void ResetToggleGroup(ToggleGroup group)
    {
        if (group == null) return;

        // 그룹 내의 모든 토글을 가져옵니다.
        Toggle[] toggles = group.GetComponentsInChildren<Toggle>();
        if (toggles.Length > 0)
        {
            // 모든 토글을 끈 뒤, 첫 번째 토글만 켭니다.
            for (int i = 0; i < toggles.Length; i++)
            {
                toggles[i].isOn = (i == 0);
            }
        }
    }
}
