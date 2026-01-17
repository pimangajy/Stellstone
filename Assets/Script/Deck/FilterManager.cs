using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 검색 필터 UI(직업, 종류, 희귀도 등)를 총괄하는 매니저입니다.
/// 버튼들을 자동으로 생성하고, 사용자가 선택한 필터 값을 저장했다가 DeckBuilder에 전달합니다.
/// </summary>
public class FilterManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // 1. 데이터 구조 정의
    // -------------------------------------------------------------------------

    // [중요] 필터 설정값을 담는 가방(구조체)입니다.
    // '?' (Nullable)을 붙여서 "선택 안 함(null)" 상태를 표현할 수 있게 했습니다.
    public struct FilterSettings
    {
        public ClassType? Member;    // 직업 (null이면 전체 직업)
        public CardType? CardType;   // 카드 종류
        public Rarity? Rarity;       // 희귀도
        public Expansion? Expansion; // 확장팩
    }

    // -------------------------------------------------------------------------
    // 2. 이벤트 정의
    // -------------------------------------------------------------------------

    // "필터 적용 버튼이 눌렸다!"라고 알리는 방송(Event)입니다.
    // DeckBuilder가 이 방송을 듣고(구독하고) 화면을 갱신합니다.
    public static event Action<FilterSettings> OnFilterApplied;

    // -------------------------------------------------------------------------
    // 3. UI 컴포넌트 연결
    // -------------------------------------------------------------------------

    [Header("UI 연결 (Parents)")]
    // 토글 버튼들이 생성될 부모 위치 (GridLayoutGroup으로 정렬됨)
    [SerializeField] private Transform memberToggleParent;
    [SerializeField] private Transform cardTypeToggleParent;
    [SerializeField] private Transform rarityToggleParent;
    [SerializeField] private Transform expansionToggleParent;

    [Header("UI 연결 (Groups)")]
    // 라디오 버튼 그룹 (그룹 내에서 하나만 선택되게 함)
    [SerializeField] private ToggleGroup memberToggleGroup;
    [SerializeField] private ToggleGroup cardTypeToggleGroup;
    [SerializeField] private ToggleGroup rarityToggleGroup;
    [SerializeField] private ToggleGroup expansionToggleGroup;

    [Header("UI 연결 (Buttons)")]
    [SerializeField] private Button applyFilterButton; // 필터 적용 버튼

    [Header("Prefab")]
    [SerializeField] private GameObject filterTogglePrefab; // 버튼 원본(프리팹)

    // 현재 선택된 필터 설정값
    private FilterSettings currentSettings;

    // -------------------------------------------------------------------------
    // 4. 초기화 로직
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // 설정값을 모두 null(전체)로 초기화
        currentSettings = new FilterSettings
        {
            Member = null,
            CardType = null,
            Rarity = null,
            Expansion = null
        };

        // 적용 버튼 클릭 시 실행할 함수 연결
        if (applyFilterButton != null)
        {
            applyFilterButton.onClick.AddListener(OnApplyButtonClicked);
        }

        // [핵심] 각 카테고리별 버튼들을 자동으로 생성하는 마법의 함수 호출
        // InitializeCategory<Enum타입> 형태로 호출하여 코드를 재사용합니다.

        // (1) 직업 필터 생성
        InitializeCategory<ClassType>(memberToggleParent, memberToggleGroup, (val) =>
        {
            // 버튼 눌리면 설정값 변수에 저장
            currentSettings.Member = val;
        });

        // (2) 카드 종류 필터 생성
        InitializeCategory<CardType>(cardTypeToggleParent, cardTypeToggleGroup, (val) =>
        {
            currentSettings.CardType = val;
        });

        // (3) 희귀도 필터 생성
        InitializeCategory<Rarity>(rarityToggleParent, rarityToggleGroup, (val) =>
        {
            currentSettings.Rarity = val;
        });

        // (4) 확장팩 필터 생성
        InitializeCategory<Expansion>(expansionToggleParent, expansionToggleGroup, (val) =>
        {
            currentSettings.Expansion = val;
        });

        // UI를 초기 상태로 리셋
        ResetFilterUI();
    }

    // -------------------------------------------------------------------------
    // 5. 주요 기능 함수들
    // -------------------------------------------------------------------------

    /// <summary>
    /// [적용] 버튼 클릭 시 호출
    /// </summary>
    private void OnApplyButtonClicked()
    {
        // 방송을 보내서 DeckBuilder가 알게 함
        NotifyFilterChanged();
    }

    /// <summary>
    /// [범용 생성기] 어떤 Enum(직업, 희귀도 등)이든 받아서 그 항목만큼 버튼을 만들어주는 함수입니다.
    /// <T>는 제네릭이라고 하며, "어떤 타입이든 들어올 수 있다"는 뜻입니다.
    /// </summary>
    private void InitializeCategory<T>(Transform parent, ToggleGroup group, Action<T?> onSelected) where T : struct, Enum
    {
        // 기존 버튼 삭제 (청소)
        foreach (Transform child in parent) Destroy(child.gameObject);

        // 1. "전체" 버튼 생성 (값은 null)
        CreateToggle(parent, group, "전체", true, (isOn) =>
        {
            if (isOn) onSelected(null);
        });

        // 2. Enum에 있는 모든 항목에 대해 버튼 생성
        foreach (T value in Enum.GetValues(typeof(T)))
        {
            CreateToggle(parent, group, value.ToString(), false, (isOn) =>
            {
                if (isOn) onSelected(value); // 켜지면 해당 값 선택
            });
        }
    }

    /// <summary>
    /// 실제로 버튼(프리팹) 하나를 생성하고 설정하는 함수
    /// </summary>
    private void CreateToggle(Transform parent, ToggleGroup group, string label, bool isDefault, UnityAction<bool> callback)
    {
        GameObject newObj = Instantiate(filterTogglePrefab, parent);
        FilterToggle toggleScript = newObj.GetComponent<FilterToggle>();

        if (toggleScript != null)
        {
            // 라벨, 그룹, 할 일(Callback) 전달
            toggleScript.Setup(label, group, callback);
            // 초기 상태(켜짐/꺼짐) 설정
            toggleScript.SetIsOn(isDefault);
        }
    }

    /// <summary>
    /// [직업 필터 갱신] 덱 편집 시에는 '내 직업'과 '중립'만 보여야 하므로 목록을 다시 만듭니다.
    /// DeckBuilder에서 호출합니다.
    /// </summary>
    public void UpdateMemberToggles(List<string> availableMembers)
    {
        foreach (Transform child in memberToggleParent) Destroy(child.gameObject);

        foreach (string memberName in availableMembers)
        {
            if (Enum.TryParse(memberName, out ClassType memberEnum))
            {
                // 첫 번째 직업(내 직업)을 기본 선택으로
                bool isDefault = (memberName == availableMembers[0]);

                CreateToggle(memberToggleParent, memberToggleGroup, memberName, isDefault, (isOn) =>
                {
                    if (isOn)
                    {
                        currentSettings.Member = memberEnum;
                    }
                });
            }
        }
    }

    /// <summary>
    /// 모든 필터를 "전체" 상태로 초기화합니다.
    /// </summary>
    public void ResetFilterUI()
    {
        // 데이터 초기화
        currentSettings = new FilterSettings();

        // UI 초기화: 각 그룹의 첫 번째(전체) 버튼 강제 선택
        ResetToggleGroup(memberToggleGroup);
        ResetToggleGroup(cardTypeToggleGroup);
        ResetToggleGroup(rarityToggleGroup);
        ResetToggleGroup(expansionToggleGroup);
    }

    private void ResetToggleGroup(ToggleGroup group)
    {
        if (group == null) return;
        if (group.transform.childCount > 0)
        {
            // 첫 번째 자식의 Toggle을 켬
            Toggle firstToggle = group.transform.GetChild(0).GetComponent<Toggle>();
            if (firstToggle != null) firstToggle.isOn = true;
        }
    }

    private void NotifyFilterChanged()
    {
        // 구독자들에게 현재 설정값 전송
        OnFilterApplied?.Invoke(currentSettings);
    }
}