using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 정렬 기준 분류 (7종류)
/// </summary>
public enum CardSortCriterion
{
    Cost,       // 코스트
    Class,      // 직업
    Attack,     // 공격력
    Health,     // 체력
    Rarity,     // 레어도
    Tribe,      // 종족
    Type        // 타입
}

/// <summary>
/// 정렬 방식 (오름차순 / 내림차순)
/// </summary>
public enum CardSortOrder
{
    Ascending,  // 오름차순
    Descending  // 내림차순
}

/// <summary>
/// 검색 필터 UI(직업, 종류, 희귀도 등) 및 정렬 UI를 총괄하는 매니저입니다.
/// 버튼들을 자동으로 생성하고, 사용자가 선택한 필터 및 정렬 값을 저장했다가 DeckBuilder에 전달합니다.
/// </summary>
public class FilterManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // 1. 데이터 구조 정의
    // -------------------------------------------------------------------------

    // 필터 및 정렬 설정값을 담는 구조체입니다.
    public struct FilterSettings
    {
        public CardClass? cardClass;        // 직업 (null이면 전체 직업)
        public CardType? CardType;          // 카드 종류
        public CardRarity? Rarity;          // 희귀도
        public Expansion? Expansion;        // 확장팩
        public CardSortCriterion sortCriterion; // 정렬 기준 (기본: Cost)
        public CardSortOrder sortOrder;         // 정렬 순서 (기본: Ascending)
    }

    // -------------------------------------------------------------------------
    // 2. 이벤트 정의
    // -------------------------------------------------------------------------

    // "필터 및 정렬 적용 버튼이 눌렸다!"라고 알리는 이벤트입니다.
    // DeckBuilder 등이 구독하여 카드 목록을 갱신합니다.
    public static event Action<FilterSettings> OnFilterApplied;

    // -------------------------------------------------------------------------
    // 3. UI 컴포넌트 연결
    // -------------------------------------------------------------------------

    [Header("UI 탭 (Filter / Sort)")]
    [SerializeField] private Toggle filterTabToggle;
    [SerializeField] private Toggle sortTabToggle;
    [SerializeField] private GameObject filterContentPanel; // enumeration 패널
    [SerializeField] private GameObject sortContentPanel;   // sortPanel 패널
    [SerializeField] private TextMeshProUGUI titleText;      // 상단 제목 텍스트

    [Header("필터 UI 연결 (Parents)")]
    [SerializeField] private Transform memberToggleParent;
    [SerializeField] private Transform cardTypeToggleParent;
    [SerializeField] private Transform rarityToggleParent;
    [SerializeField] private Transform expansionToggleParent;

    [Header("필터 UI 연결 (Groups)")]
    [SerializeField] private ToggleGroup memberToggleGroup;
    [SerializeField] private ToggleGroup cardTypeToggleGroup;
    [SerializeField] private ToggleGroup rarityToggleGroup;
    [SerializeField] private ToggleGroup expansionToggleGroup;

    [Header("정렬 UI 연결 (Parents)")]
    [SerializeField] private Transform sortCriterionParent;
    [SerializeField] private Transform sortOrderParent;

    [Header("정렬 UI 연결 (Groups)")]
    [SerializeField] private ToggleGroup sortCriterionGroup;
    [SerializeField] private ToggleGroup sortOrderGroup;

    [Header("UI 연결 (Buttons)")]
    [SerializeField] private Button applyFilterButton; // 적용 버튼

    [Header("Prefab")]
    [SerializeField] private GameObject filterTogglePrefab; // 버튼 원본(프리팹)

    // 현재 선택된 필터/정렬 설정값
    private FilterSettings currentSettings;

    // -------------------------------------------------------------------------
    // 4. 초기화 로직
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // 설정값 초기화
        currentSettings = new FilterSettings
        {
            cardClass = null,
            CardType = null,
            Rarity = null,
            Expansion = null,
            sortCriterion = CardSortCriterion.Cost,
            sortOrder = CardSortOrder.Ascending
        };

        // 적용 버튼 클릭 시 실행할 함수 연결
        if (applyFilterButton != null)
        {
            applyFilterButton.onClick.AddListener(OnApplyButtonClicked);
        }

        // 탭 토글 리스너 연결
        if (filterTabToggle != null)
        {
            filterTabToggle.onValueChanged.AddListener((isOn) =>
            {
                if (isOn) SwitchTab(true);
            });
        }

        if (sortTabToggle != null)
        {
            sortTabToggle.onValueChanged.AddListener((isOn) =>
            {
                if (isOn) SwitchTab(false);
            });
        }

        // (1) 직업 필터 생성
        InitializeCategory<CardClass>(memberToggleParent, memberToggleGroup, (val) =>
        {
            currentSettings.cardClass = val;
        });

        // (2) 카드 종류 필터 생성
        InitializeCategory<CardType>(cardTypeToggleParent, cardTypeToggleGroup, (val) =>
        {
            currentSettings.CardType = val;
        });

        // (3) 희귀도 필터 생성
        InitializeCategory<CardRarity>(rarityToggleParent, rarityToggleGroup, (val) =>
        {
            currentSettings.Rarity = val;
        });

        // (4) 확장팩 필터 생성
        InitializeCategory<Expansion>(expansionToggleParent, expansionToggleGroup, (val) =>
        {
            currentSettings.Expansion = val;
        });

        // (5) 정렬 UI 생성
        InitializeSortUI();

        // UI를 초기 상태로 리셋
        ResetFilterUI();

        // 초기에는 필터 탭 활성화
        SwitchTab(true);
    }

    // -------------------------------------------------------------------------
    // 5. 탭 전환 및 정렬 UI 구성 로직
    // -------------------------------------------------------------------------

    /// <summary>
    /// 필터 탭 / 정렬 탭 화면 전환
    /// </summary>
    private void SwitchTab(bool isFilterTab)
    {
        if (filterContentPanel != null) filterContentPanel.SetActive(isFilterTab);
        if (sortContentPanel != null) sortContentPanel.SetActive(!isFilterTab);
        if (titleText != null)
        {
            titleText.text = isFilterTab ? "필터" : "정렬";
        }
    }

    /// <summary>
    /// 정렬 기준(7가지) 및 정렬 방식(2가지) 버튼을 생성합니다.
    /// </summary>
    private void InitializeSortUI()
    {
        // 1. 정렬 기준 버튼 생성 (코스트, 직업, 공격력, 체력, 레어도, 종족, 타입)
        if (sortCriterionParent != null)
        {
            foreach (Transform child in sortCriterionParent) Destroy(child.gameObject);

            var criteria = new (string label, CardSortCriterion criterion)[]
            {
                ("코스트", CardSortCriterion.Cost),
                ("직업", CardSortCriterion.Class),
                ("공격력", CardSortCriterion.Attack),
                ("체력", CardSortCriterion.Health),
                ("레어도", CardSortCriterion.Rarity),
                ("종족", CardSortCriterion.Tribe),
                ("타입", CardSortCriterion.Type)
            };

            for (int i = 0; i < criteria.Length; i++)
            {
                var item = criteria[i];
                bool isDefault = (i == 0); // 코스트가 기본 선택
                CreateToggle(sortCriterionParent, sortCriterionGroup, item.label, isDefault, (isOn) =>
                {
                    if (isOn)
                    {
                        currentSettings.sortCriterion = item.criterion;
                    }
                });
            }
        }

        // 2. 정렬 방식 버튼 생성 (오름차순, 내림차순)
        if (sortOrderParent != null)
        {
            foreach (Transform child in sortOrderParent) Destroy(child.gameObject);

            var orders = new (string label, CardSortOrder order)[]
            {
                ("오름차순", CardSortOrder.Ascending),
                ("내림차순", CardSortOrder.Descending)
            };

            for (int i = 0; i < orders.Length; i++)
            {
                var item = orders[i];
                bool isDefault = (i == 0); // 오름차순이 기본 선택
                CreateToggle(sortOrderParent, sortOrderGroup, item.label, isDefault, (isOn) =>
                {
                    if (isOn)
                    {
                        currentSettings.sortOrder = item.order;
                    }
                });
            }
        }
    }

    // -------------------------------------------------------------------------
    // 6. 주요 기능 함수들
    // -------------------------------------------------------------------------

    /// <summary>
    /// [적용] 버튼 클릭 시 호출
    /// </summary>
    private void OnApplyButtonClicked()
    {
        NotifyFilterChanged();
    }

    /// <summary>
    /// [범용 생성기] 어떤 Enum(직업, 희귀도 등)이든 받아서 그 항목만큼 버튼을 만들어주는 함수입니다.
    /// </summary>
    private void InitializeCategory<T>(Transform parent, ToggleGroup group, Action<T?> onSelected) where T : struct, Enum
    {
        if (parent == null) return;
        foreach (Transform child in parent) Destroy(child.gameObject);

        // 1. "전체" 버튼 생성 (값은 null)
        CreateToggle(parent, group, "전체", true, (isOn) =>
        {
            if (isOn) onSelected(null);
        });

        // 2. Enum에 있는 모든 항목 순회 버튼 생성
        foreach (T value in Enum.GetValues(typeof(T)))
        {
            if (value is CardType cardType)
            {
                if (cardType == CardType.UNKNOWN || cardType == CardType.READER)
                {
                    continue;
                }
            }

            CreateToggle(parent, group, value.ToString(), false, (isOn) =>
            {
                if (isOn) onSelected(value);
            });
        }
    }

    /// <summary>
    /// 실제로 버튼(프리팹) 하나를 생성하고 설정하는 함수
    /// </summary>
    private void CreateToggle(Transform parent, ToggleGroup group, string label, bool isDefault, UnityAction<bool> callback)
    {
        if (filterTogglePrefab == null || parent == null) return;

        GameObject newObj = Instantiate(filterTogglePrefab, parent);
        FilterToggle toggleScript = newObj.GetComponent<FilterToggle>();

        if (toggleScript != null)
        {
            toggleScript.Setup(label, group, callback);
            toggleScript.SetIsOn(isDefault);
        }
    }

    /// <summary>
    /// [직업 필터 갱신] 덱 편집 시에는 '전체', '내 직업', '중립(강지)' 순서로 토글을 생성합니다.
    /// </summary>
    public void UpdateMemberToggles(List<string> availableMembers)
    {
        if (memberToggleParent == null) return;
        foreach (Transform child in memberToggleParent) Destroy(child.gameObject);

        CreateToggle(memberToggleParent, memberToggleGroup, "전체", true, (isOn) =>
        {
            if (isOn)
            {
                currentSettings.cardClass = null;
            }
        });

        foreach (string memberName in availableMembers)
        {
            if (Enum.TryParse(memberName, out CardClass memberEnum))
            {
                CreateToggle(memberToggleParent, memberToggleGroup, memberName, false, (isOn) =>
                {
                    if (isOn)
                    {
                        currentSettings.cardClass = memberEnum;
                    }
                });
            }
        }
    }

    /// <summary>
    /// 모든 필터와 정렬을 기본 상태로 초기화합니다.
    /// </summary>
    public void ResetFilterUI()
    {
        currentSettings = new FilterSettings
        {
            cardClass = null,
            CardType = null,
            Rarity = null,
            Expansion = null,
            sortCriterion = CardSortCriterion.Cost,
            sortOrder = CardSortOrder.Ascending
        };

        ResetToggleGroup(memberToggleGroup);
        ResetToggleGroup(cardTypeToggleGroup);
        ResetToggleGroup(rarityToggleGroup);
        ResetToggleGroup(expansionToggleGroup);

        ResetToggleGroup(sortCriterionGroup);
        ResetToggleGroup(sortOrderGroup);

        if (filterTabToggle != null)
        {
            filterTabToggle.isOn = true;
        }
        SwitchTab(true);
    }

    private void ResetToggleGroup(ToggleGroup group)
    {
        if (group == null) return;
        if (group.transform.childCount > 0)
        {
            Toggle firstToggle = group.transform.GetChild(0).GetComponent<Toggle>();
            if (firstToggle != null) firstToggle.isOn = true;
        }
    }

    private void NotifyFilterChanged()
    {
        OnFilterApplied?.Invoke(currentSettings);
    }
}