using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 필터 UI 시스템을 총괄하는 매니저 클래스입니다.
/// 직업, 카드 종류, 희귀도 등의 토글 버튼을 자동으로 생성하고,
/// 사용자의 선택을 관리하며, [적용] 버튼이 눌렸을 때 변경된 내용을 전파합니다.
/// </summary>
public class FilterManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // 1. 데이터 구조 정의
    // -------------------------------------------------------------------------

    // [중요] 필터 설정값을 담는 구조체입니다.
    // 물음표(?)가 붙은 'Nullable' 타입을 사용하여 '선택 안 함(전체)' 상태를 null로 표현합니다.
    public struct FilterSettings
    {
        public ClassType? Member;    // 직업 (null이면 전체 직업)
        public CardType? CardType;   // 카드 종류 (하수인/주문 등, null이면 전체)
        public Rarity? Rarity;       // 희귀도 (null이면 전체)
        public Expansion? Expansion; // 확장팩 (null이면 전체)
    }

    // -------------------------------------------------------------------------
    // 2. 이벤트 정의 (방송 시스템)
    // -------------------------------------------------------------------------

    // 필터가 최종적으로 적용되었을 때, 이 이벤트(방송)를 구독하고 있는 스크립트(DeckBuilder)들에게 알립니다.
    // static으로 선언되어 어디서든 쉽게 접근할 수 있습니다.
    public static event Action<FilterSettings> OnFilterApplied;

    // -------------------------------------------------------------------------
    // 3. UI 컴포넌트 연결 (Inspector 설정)
    // -------------------------------------------------------------------------

    [Header("UI 연결 (Parents)")]
    // 토글 버튼들이 생성될 부모 오브젝트들입니다. (Grid Layout Group 등이 붙어있어 자동 정렬됨)
    [SerializeField] private Transform memberToggleParent;    // 직업 토글이 들어갈 곳
    [SerializeField] private Transform cardTypeToggleParent;  // 카드 종류 토글이 들어갈 곳
    [SerializeField] private Transform rarityToggleParent;    // 희귀도 토글이 들어갈 곳
    [SerializeField] private Transform expansionToggleParent; // 확장팩 토글이 들어갈 곳

    [Header("UI 연결 (Groups)")]
    // 토글 그룹 컴포넌트입니다. 같은 그룹 내에서는 오직 하나만 선택되도록(라디오 버튼 기능) 해줍니다.
    [SerializeField] private ToggleGroup memberToggleGroup;
    [SerializeField] private ToggleGroup cardTypeToggleGroup;
    [SerializeField] private ToggleGroup rarityToggleGroup;
    [SerializeField] private ToggleGroup expansionToggleGroup;

    [Header("UI 연결 (Buttons)")]
    // [추가 기능] 필터를 즉시 적용하지 않고, 이 버튼을 눌렀을 때 적용하기 위한 버튼입니다.
    [SerializeField] private Button applyFilterButton;

    [Header("Prefab")]
    // 공장에서 찍어낼 버튼의 원본(프리팹)입니다.
    [SerializeField] private GameObject filterTogglePrefab;

    // 현재 사용자가 선택한 설정값을 임시로 저장해두는 변수입니다.
    private FilterSettings currentSettings;

    // -------------------------------------------------------------------------
    // 4. 초기화 로직 (Awake)
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // 설정값을 모두 null(전체 선택 상태)로 초기화합니다.
        currentSettings = new FilterSettings
        {
            Member = null,
            CardType = null,
            Rarity = null,
            Expansion = null
        };

        // 적용 버튼에 클릭 리스너를 달아줍니다. "클릭되면 OnApplyButtonClicked 함수를 실행해!"
        if (applyFilterButton != null)
        {
            applyFilterButton.onClick.AddListener(OnApplyButtonClicked);
        }

        // [핵심 로직] 각 카테고리별로 토글 버튼들을 자동으로 생성합니다.
        // InitializeCategory 함수는 제네릭(T)을 사용하여 코드를 재사용합니다.

        // (1) 직업 필터 생성
        InitializeCategory<ClassType>(memberToggleParent, memberToggleGroup, (val) =>
        {
            // 버튼이 눌리면 currentSettings 변수만 업데이트하고 기다립니다.
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

        // UI를 기본 상태(전체 선택)로 초기화합니다.
        ResetFilterUI();
    }

    // -------------------------------------------------------------------------
    // 5. 주요 기능 함수들
    // -------------------------------------------------------------------------

    /// <summary>
    /// [적용] 버튼이 클릭되었을 때 호출됩니다.
    /// 임시로 저장해둔 설정값(currentSettings)을 가지고 이벤트를 발생시켜 덱 빌더에 알립니다.
    /// </summary>
    private void OnApplyButtonClicked()
    {
        NotifyFilterChanged();
    }

    /// <summary>
    /// [자동 생성기] 특정 Enum 타입(예: Rarity)의 모든 값에 대해 토글 버튼을 생성하는 범용 함수입니다.
    /// T 자리에는 ClassType, Rarity 등 어떤 Enum이든 들어갈 수 있습니다.
    /// </summary>
    /// <typeparam name="T">Enum 타입</typeparam>
    /// <param name="parent">버튼이 생성될 부모 위치</param>
    /// <param name="group">버튼이 속할 토글 그룹</param>
    /// <param name="onSelected">버튼이 선택되었을 때 실행할 콜백 함수</param>
    private void InitializeCategory<T>(Transform parent, ToggleGroup group, Action<T?> onSelected) where T : struct, Enum
    {
        // 1. 기존에 있던 테스트용 버튼이나 찌꺼기들을 모두 삭제합니다.
        foreach (Transform child in parent) Destroy(child.gameObject);

        // 2. 맨 앞에 "전체" 버튼을 생성합니다. (값은 null)
        CreateToggle(parent, group, "전체", true, (isOn) =>
        {
            // 켜지면(isOn == true) 값을 null로 설정합니다.
            if (isOn) onSelected(null);
        });

        // 3. Enum에 정의된 모든 값들을 하나씩 가져와서 버튼을 생성합니다.
        foreach (T value in Enum.GetValues(typeof(T)))
        {
            CreateToggle(parent, group, value.ToString(), false, (isOn) =>
            {
                // 켜지면 해당 Enum 값(value)으로 설정합니다.
                if (isOn) onSelected(value);
            });
        }
    }

    /// <summary>
    /// 실제 토글 프리팹(GameObject)을 생성하고 설정하는 헬퍼 함수입니다.
    /// </summary>
    private void CreateToggle(Transform parent, ToggleGroup group, string label, bool isDefault, UnityAction<bool> callback)
    {
        // 프리팹 복제(생성)
        GameObject newObj = Instantiate(filterTogglePrefab, parent);

        // 생성된 오브젝트에서 스크립트를 가져옵니다.
        FilterToggle toggleScript = newObj.GetComponent<FilterToggle>();

        if (toggleScript != null)
        {
            // 스크립트에 라벨, 그룹, 클릭 시 할 일을 전달합니다.
            toggleScript.Setup(label, group, callback);

            // [안전 장치] 초기 선택 상태(isDefault)에 따라 확실하게 켜거나 끕니다.
            // 프리팹이 기본적으로 켜져있어 발생하는 오류를 방지합니다.
            toggleScript.SetIsOn(isDefault);
        }
    }

    /// <summary>
    /// [특수 기능] DeckBuilder에서 호출합니다.
    /// 덱을 편집할 때는 '내 직업'과 '중립' 카드만 보여야 하므로, 직업 필터 목록을 갱신합니다.
    /// </summary>
    /// <param name="availableMembers">보여줄 직업 이름 리스트 (예: ["전사", "강지"])</param>
    public void UpdateMemberToggles(List<string> availableMembers)
    {
        // 기존 직업 버튼들을 싹 지웁니다.
        foreach (Transform child in memberToggleParent) Destroy(child.gameObject);

        // 허용된 직업 리스트만큼만 반복해서 버튼을 만듭니다.
        foreach (string memberName in availableMembers)
        {
            // 문자열 이름을 실제 Enum 타입으로 변환합니다.
            if (Enum.TryParse(memberName, out ClassType memberEnum))
            {
                // 리스트의 첫 번째 직업(보통 내 직업)을 기본 선택 상태로 둡니다.
                bool isDefault = (memberName == availableMembers[0]);

                CreateToggle(memberToggleParent, memberToggleGroup, memberName, isDefault, (isOn) =>
                {
                    if (isOn)
                    {
                        currentSettings.Member = memberEnum;
                        // 참고: 여기서도 NotifyFilterChanged()를 호출하지 않으므로,
                        // 덱 편집 모드 진입 후에도 [적용]을 눌러야 필터가 먹힐 수 있습니다.
                        // 만약 덱 편집 들어가자마자 필터가 되길 원하면 여기서 한 번 호출해주는 게 좋을 수도 있습니다.
                    }
                });
            }
        }
    }

    /// <summary>
    /// 모든 필터 UI와 설정값을 초기 상태(전체 선택)로 되돌립니다.
    /// </summary>
    public void ResetFilterUI()
    {
        // 1. 내부 데이터 초기화
        currentSettings = new FilterSettings(); // 다 null로 초기화됨

        // 2. UI 초기화: 각 그룹의 첫 번째 토글("전체")을 찾아서 강제로 켭니다.
        // (Member 토글은 UpdateMemberToggles에서 처리되므로 여기서는 제외)
        ResetToggleGroup(memberToggleGroup);
        ResetToggleGroup(cardTypeToggleGroup);
        ResetToggleGroup(rarityToggleGroup);
        ResetToggleGroup(expansionToggleGroup);

        // [선택] 초기화 즉시 반영하고 싶다면 주석 해제, [적용] 눌러야 초기화되게 하려면 주석 유지
        // NotifyFilterChanged(); 
    }

    // 토글 그룹에서 첫 번째 자식("전체" 버튼)을 찾아 켜주는 내부 함수
    private void ResetToggleGroup(ToggleGroup group)
    {
        if (group == null) return;

        // 자식이 하나라도 있다면
        if (group.transform.childCount > 0)
        {
            // 첫 번째 자식의 Toggle 컴포넌트를 가져와서 켭니다.
            Toggle firstToggle = group.transform.GetChild(0).GetComponent<Toggle>();
            if (firstToggle != null) firstToggle.isOn = true;
        }
    }

    // 이벤트를 발생시켜 변경된 설정을 전파하는 함수
    private void NotifyFilterChanged()
    {
        // 구독자가 있다면(?.) Invoke를 통해 방송을 송출합니다.
        OnFilterApplied?.Invoke(currentSettings);
    }
}