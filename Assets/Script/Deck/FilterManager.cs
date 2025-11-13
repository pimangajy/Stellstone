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
        public string Member;
        public string CardType;
        public string Rarity;
        public string Expansion;
    }

    // 필터 적용 시 호출될 이벤트. 다른 스크립트에서 이 이벤트를 구독하여 필터된 결과를 처리할 수 있습니다.
    public static event Action<FilterSettings> OnFilterApplied;

    [Header("필터 카테고리 (토글 그룹)")]
    [Tooltip("카드 멤버 토글들을 그룹핑한 ToggleGroup")]
    [SerializeField] private ToggleGroup cardMemberGroup;

    [Tooltip("카드 종류 토글들을 그룹핑한 ToggleGroup")]
    [SerializeField] private ToggleGroup cardTypeGroup;

    [Tooltip("레어도 토글들을 그룹핑한 ToggleGroup")]
    [SerializeField] private ToggleGroup rarityGroup;

    [Tooltip("확장팩 토글들을 그룹핑한 ToggleGroup")]
    [SerializeField] private ToggleGroup expansionGroup;

    [Tooltip("직업/멤버 필터의 토글들이 들어있는 부모 오브젝트를 연결해주세요.")]
    public Transform memberToggleParent;


    /// <summary>
    /// '적용' 버튼을 눌렀을 때 호출됩니다. 현재 선택된 필터 정보를 수집하고 이벤트를 발생시킵니다.
    /// </summary>
    public void ApplyFilters()
    {
        FilterSettings currentSettings = new FilterSettings
        {
            Member = GetSelectedToggleValue(cardMemberGroup),
            CardType = GetSelectedToggleValue(cardTypeGroup),
            Rarity = GetSelectedToggleValue(rarityGroup),
            Expansion = GetSelectedToggleValue(expansionGroup)
        };

        Debug.Log($"필터 적용: 카드의 멤버({currentSettings.Member}),  카드 종류({currentSettings.CardType}), 레어도({currentSettings.Rarity}), 확장팩({currentSettings.Expansion})");

        // 필터 적용 이벤트를 발생시켜 다른 시스템(예: 카드 목록 UI)에 알립니다.
        OnFilterApplied?.Invoke(currentSettings);
    }

    /// <summary>
    /// '초기화' 버튼을 눌렀을 때 호출됩니다. 모든 필터를 기본값('전체' 또는 첫 번째 토글)으로 되돌립니다.
    /// </summary>
    public void ResetFilters()
    {
        ResetToggleGroup(cardMemberGroup);
        ResetToggleGroup(cardTypeGroup);
        ResetToggleGroup(rarityGroup);
        ResetToggleGroup(expansionGroup);

        Debug.Log("필터가 초기화되었습니다.");

        // 초기화 후 바로 적용하고 싶다면 아래 주석을 해제하세요.
        // ApplyFilters();
    }

    /// <summary>
    /// 특정 토글 그룹에서 현재 활성화된 토글의 값을 가져옵니다. (FilterToggle 스크립트 사용)
    /// </summary>
    /// <param name="group">값을 가져올 ToggleGroup</param>
    /// <returns>선택된 토글의 filterValue 값</returns>
    private string GetSelectedToggleValue(ToggleGroup group)
    {
        if (group == null) return "전체";

        // 현재 활성화된 토글을 찾습니다.
        Toggle activeToggle = group.ActiveToggles().FirstOrDefault();

        if (activeToggle != null)
        {
            // 활성화된 토글에 부착된 FilterToggle 스크립트를 찾습니다.
            FilterToggle filterToggle = activeToggle.GetComponent<FilterToggle>();
            if (filterToggle != null)
            {
                // FilterToggle 스크립트의 filterValue 값을 반환합니다.
                // '전체' 토글처럼 값이 비어있을 경우 "전체"를 반환합니다.
                return string.IsNullOrEmpty(filterToggle.filterValue) ? "전체" : filterToggle.filterValue;
            }
            else
            {
                // FilterToggle 스크립트가 없는 토글이 선택된 경우 경고를 출력합니다.
                Debug.LogWarning($"선택된 토글 '{activeToggle.gameObject.name}'에 FilterToggle 스크립트가 없습니다. '전체'로 처리합니다.");
            }
        }

        // 선택된 토글이 없거나 FilterToggle 스크립트를 찾지 못하면 '전체'로 간주합니다.
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

    /// <summary>
    /// 모든 필터 UI를 기본 상태('전체' 선택)로 되돌립니다.
    /// </summary>
    public void ResetFilterUI()
    {
        Debug.Log("필터 UI를 초기화합니다.");

        // 관리하는 모든 토글 그룹을 배열에 담아 순회합니다.
        var allGroups = new[] { cardMemberGroup, cardTypeGroup, rarityGroup, expansionGroup };
        foreach (var group in allGroups)
        {
            if (group != null)
            {
                // 자식 토글 중 첫 번째 토글을 '전체' 토글로 간주하고 활성화합니다.
                Toggle firstToggle = group.GetComponentInChildren<Toggle>(true);
                if (firstToggle != null)
                {
                    firstToggle.isOn = true;
                }
            }
        }
    }

    /// <summary>
    /// 멤버(직업) 토글의 활성화 상태를 업데이트합니다.
    /// </summary>
    /// <param name="availableMembers">화면에 보여줄 멤버(직업) 이름 리스트. null이면 모두 보여줍니다.</param>
    public void UpdateMemberToggles(List<string> availableMembers)
    {
        if (memberToggleParent == null) return;

        foreach (Transform toggleTransform in memberToggleParent)
        {
            FilterToggle filterToggle = toggleTransform.GetComponent<FilterToggle>();
            if (filterToggle != null)
            {
                bool shouldBeActive = availableMembers == null ||
                                      filterToggle.filterValue == "전체" ||
                                      filterToggle.filterValue == "Gangzi" ||
                                      availableMembers.Contains(filterToggle.filterValue);

                toggleTransform.gameObject.SetActive(shouldBeActive);
            }
        }
    }
}
