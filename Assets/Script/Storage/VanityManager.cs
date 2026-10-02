using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Storage 씬의 VanityPos(아이템 관리) 구역 라이프사이클 및 화면 전환을 총괄하는 컨트롤러 매니저입니다.
/// - 1단계: VanityPos 진입 시 좌측 카테고리 메뉴만 화면 좌측에 꽉 차게 표시 (중앙/우측 뷰 비활성화)
/// - 2단계: 카테고리([리더 스킨] 등) 버튼 클릭 시 메인 뷰가 화면에 꽉 차게 확장
/// - UIPanelToggler 및 ESC / 뒤로가기 버튼 지원 (스킨 뷰 -> 카테고리 메뉴 -> 전체 조망 복귀 2단계 팝업 스택)
/// </summary>
public class VanityManager : MonoBehaviour
{
    public static VanityManager Instance { get; private set; }

    public enum VanityState
    {
        Closed,          // 전체 조망 상태
        CategoryMenu,    // 좌측 카테고리 메뉴만 열린 초기 상태
        CategoryContent  // 특정 카테고리(스킨 등) 상세 뷰가 꽉 차게 열린 상태
    }

    [Header("1. 카메라 연동")]
    [Tooltip("StorageCameraController 참조 (비워두면 자동 탐색)")]
    [SerializeField] private StorageCameraController cameraController;

    [Tooltip("Vanity 구역 인덱스 (기본: 1)")]
    [SerializeField] private int vanitySectionIndex = 1;

    [Header("2. 메인 패널 & Toggler")]
    [Tooltip("Vanity 구역 UI의 최상위 부모 오브젝트")]
    [SerializeField] private GameObject vanityPanelRoot;

    [Tooltip("패널 등장/퇴장 연출용 UIPanelToggler (선택 사항)")]
    [SerializeField] private UIPanelToggler panelToggler;

    [Header("3. 좌측 세로형 카테고리 사이드바")]
    [SerializeField] private GameObject categorySidebarRoot;
    [SerializeField] private Button btnCategorySkin;
    [SerializeField] private Button btnCategoryEmote;
    [SerializeField] private Button btnCategoryOther;
    [SerializeField] private Button btnBackToOverview;        // 카테고리 화면에서 전체 조망으로 나가는 뒤로가기 버튼

    [Header("4. 카테고리별 서브 뷰 (화면 꽉 차게 확장)")]
    [SerializeField] private VanitySkinView skinView;
    [SerializeField] private GameObject emotePlaceholderView;
    [SerializeField] private GameObject otherPlaceholderView;

    public VanityState CurrentState { get; private set; } = VanityState.Closed;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (btnCategorySkin != null) btnCategorySkin.onClick.AddListener(() => OpenCategory(VanitySkinView_Category.Skin));
        if (btnCategoryEmote != null) btnCategoryEmote.onClick.AddListener(() => OpenCategory(VanitySkinView_Category.Emote));
        if (btnCategoryOther != null) btnCategoryOther.onClick.AddListener(() => OpenCategory(VanitySkinView_Category.Other));
        if (btnBackToOverview != null) btnBackToOverview.onClick.AddListener(CloseAndReturnOverview);
    }

    private void Start()
    {
        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<StorageCameraController>();
        }

        if (cameraController != null)
        {
            cameraController.onSectionEnter.AddListener(OnSectionEnter);
            cameraController.onReturnOverview.AddListener(OnReturnOverview);
        }

        if (panelToggler == null && vanityPanelRoot != null)
        {
            panelToggler = vanityPanelRoot.GetComponent<UIPanelToggler>();
        }

        // 초기 상태: 비활성화
        if (vanityPanelRoot != null)
        {
            vanityPanelRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (cameraController != null)
        {
            cameraController.onSectionEnter.RemoveListener(OnSectionEnter);
            cameraController.onReturnOverview.RemoveListener(OnReturnOverview);
        }
    }

    private void Update()
    {
        // ESC 키 입력 감지 (UIManager와 별개로 Vanity 화면 내 2단계 뒤로가기 보장)
        if (CurrentState != VanityState.Closed && Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapeKey();
        }
    }

    private void OnSectionEnter(int sectionIndex)
    {
        if (sectionIndex == vanitySectionIndex)
        {
            OpenInitialCategoryMenu();
        }
        else
        {
            CloseImmediate();
        }
    }

    private void OnReturnOverview()
    {
        CloseImmediate();
    }

    /// <summary>
    /// VanityPos 진입 시 좌측 카테고리 메뉴를 띄우고 기본 카테고리([리더 스킨])를 즉시 열어 목록을 표시합니다.
    /// </summary>
    public void OpenInitialCategoryMenu()
    {
        if (vanityPanelRoot != null)
        {
            vanityPanelRoot.SetActive(true);
        }

        if (panelToggler != null)
        {
            panelToggler.ShowPanel();
        }

        // 좌측 카테고리 활성화
        if (categorySidebarRoot != null)
        {
            categorySidebarRoot.SetActive(true);
        }

        // 기본으로 [리더 스킨] 카테고리를 즉시 열어 스킨 목록 표시
        OpenCategory(VanitySkinView_Category.Skin);

        Debug.Log("[VanityManager] 🪞 VanityPos 도착: 기본 카테고리(리더 스킨) 즉시 활성화");
    }

    public enum VanitySkinView_Category
    {
        Skin,
        Emote,
        Other
    }

    /// <summary>
    /// 2단계: 카테고리 버튼을 눌렀을 때 해당 뷰가 화면에 꽉 차게 나타납니다.
    /// </summary>
    public void OpenCategory(VanitySkinView_Category category)
    {
        CurrentState = VanityState.CategoryContent;

        if (skinView != null)
        {
            bool isSkin = (category == VanitySkinView_Category.Skin);
            skinView.gameObject.SetActive(isSkin);
            if (isSkin)
            {
                skinView.InitializeView();
            }
        }

        if (emotePlaceholderView != null) emotePlaceholderView.SetActive(category == VanitySkinView_Category.Emote);
        if (otherPlaceholderView != null) otherPlaceholderView.SetActive(category == VanitySkinView_Category.Other);

        Debug.Log($"[VanityManager] 📂 카테고리 '{category}' 열림: 메인 화면 꽉 차게 확장 (2단계)");
    }

    /// <summary>
    /// 스킨 뷰 등의 메인 화면을 닫고 좌측 카테고리 선택 화면(1단계)으로 복귀합니다.
    /// </summary>
    public void CloseActiveCategoryView()
    {
        if (skinView != null) skinView.gameObject.SetActive(false);
        if (emotePlaceholderView != null) emotePlaceholderView.SetActive(false);
        if (otherPlaceholderView != null) otherPlaceholderView.SetActive(false);

        CurrentState = VanityState.CategoryMenu;

        if (categorySidebarRoot != null)
        {
            categorySidebarRoot.SetActive(true);
        }

        Debug.Log("[VanityManager] ◀ 메인 뷰 닫힘 -> 카테고리 선택 화면 복귀");
    }

    /// <summary>
    /// 전체 조망(Overview)으로 복귀합니다.
    /// </summary>
    public void CloseAndReturnOverview()
    {
        CloseImmediate();

        if (cameraController != null)
        {
            cameraController.MoveToOverview();
        }
    }

    public void CloseImmediate()
    {
        CurrentState = VanityState.Closed;

        if (UIManager.Instance != null && vanityPanelRoot != null)
        {
            UIManager.Instance.CloseSpecificPopup(vanityPanelRoot);
        }

        if (panelToggler != null)
        {
            panelToggler.HidePanel();
        }
        else if (vanityPanelRoot != null)
        {
            vanityPanelRoot.SetActive(false);
        }
    }

    /// <summary>
    /// ESC 키 입력 처리: 현재 열려있는 깊이에 따라 순차적으로 닫기
    /// </summary>
    public void HandleEscapeKey()
    {
        // 0. 감정표현 교체 서브 팝업이 열려있다면 서브 팝업 닫기
        if (skinView != null && skinView.gameObject.activeSelf && skinView.IsEmotePopupOpened)
        {
            skinView.CloseEmotePopup();
            return;
        }

        // 1. 필터 패널이 열려있다면 필터 패널 닫기 (스킨 목록 복귀)
        if (skinView != null && skinView.gameObject.activeSelf && skinView.IsFilterPanelOpened)
        {
            skinView.ShowFilterPanel(false);
            return;
        }

        // 2. 카테고리 컨텐츠 뷰(스킨 등)가 열려있다면 1단계(카테고리 메뉴)로 복귀
        if (CurrentState == VanityState.CategoryContent)
        {
            CloseActiveCategoryView();
            return;
        }

        // 3. 카테고리 메뉴만 열려있다면 전체 조망으로 복귀
        if (CurrentState == VanityState.CategoryMenu)
        {
            CloseAndReturnOverview();
        }
    }
}
