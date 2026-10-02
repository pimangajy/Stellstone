#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Storage 씬에 사용자 피드백(좌측 카테고리 꽉 찬 배치, 2단계 뷰, [필터] 버튼 및 토글 패널, UIPanelToggler 연동, 우측 상단 여백 확보)을
/// 완벽하게 반영하여 UI를 재구성하는 에디터 툴입니다.
/// </summary>
public static class SetupVanityUI
{
    [MenuItem("Tools/Stellar Duel/Setup Vanity UI in Storage Scene")]
    public static void Setup()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Storage")
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Storage.unity");
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[SetupVanityUI] ❌ Storage 씬에서 Canvas를 찾을 수 없습니다!");
            return;
        }

        StorageCameraController camCtrl = Object.FindFirstObjectByType<StorageCameraController>();
        StorageManager storageMgr = Object.FindFirstObjectByType<StorageManager>();

        if (camCtrl == null || storageMgr == null)
        {
            Debug.LogError("[SetupVanityUI] ❌ StorageCameraController 또는 StorageManager를 찾을 수 없습니다!");
            return;
        }

        TMP_FontAsset mainFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/NotoSansKR-Bold SDF.asset");
        GameObject skinSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Deck/SkinSlot.prefab");
        GameObject emoteSlotPrefab = EnsureEmoteSlotPrefab(mainFont);

        // 기존 VanityPanelGroup 제거 후 재구성
        Transform existing = canvas.transform.Find("VanityPanelGroup");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // =========================================================================
        // A. VanityPanelGroup 루트 생성 및 UIPanelToggler 부착
        // =========================================================================
        GameObject vanityRoot = new GameObject("VanityPanelGroup", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(vanityRoot, "Create VanityPanelGroup");
        vanityRoot.transform.SetParent(canvas.transform, false);
        vanityRoot.layer = LayerMask.NameToLayer("UI");

        RectTransform rootRt = vanityRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // 반투명 어두운 배경
        Image bgImg = vanityRoot.AddComponent<Image>();
        bgImg.color = new Color(0.03f, 0.04f, 0.07f, 0.88f);

        // Back 버튼 및 NetworkPanel 아래에 오도록 Sibling Index 설정
        Transform backTransform = canvas.transform.Find("Back");
        if (backTransform != null)
        {
            vanityRoot.transform.SetSiblingIndex(backTransform.GetSiblingIndex());
        }

        UIPanelToggler rootToggler = vanityRoot.AddComponent<UIPanelToggler>();
        rootToggler.panelObject = vanityRoot;
        rootToggler.animationType = UIPanelToggler.AnimationType.FadeAndScale;
        rootToggler.animationDuration = 0.25f;
        rootToggler.hidengUI = true;

        // =========================================================================
        // B. 1단계: 좌측 세로형 카테고리 사이드바 (좌측 상단 씬 나가기 Back 버튼 영역인 상단 85px 비워둠)
        // =========================================================================
        GameObject sidebarObj = CreateUIObject("CategorySidebar", vanityRoot.transform,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(230f, -85f));
        
        Image sidebarBg = sidebarObj.AddComponent<Image>();
        sidebarBg.color = new Color(0.07f, 0.09f, 0.14f, 0.95f);
        Outline sidebarOutline = sidebarObj.AddComponent<Outline>();
        sidebarOutline.effectColor = new Color(0.2f, 0.35f, 0.55f, 0.4f);
        sidebarOutline.effectDistance = new Vector2(2f, 0f);

        // 상단 뒤로가기 버튼 [◀ 창고 조망]
        Button btnBackOverview = CreateCategoryButton("Btn_BackToOverview", sidebarObj.transform, "◀ 창고 복귀", mainFont, new Color(0.25f, 0.3f, 0.4f), 50f);
        SetRectAnchors(btnBackOverview.GetComponent<RectTransform>(), new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.98f));

        // 카테고리 타이틀
        TextMeshProUGUI sidebarTitle = CreateTMPText("Txt_CategoryTitle", sidebarObj.transform, "보관함 카테고리", mainFont, 16, TextAlignmentOptions.Center, new Color(0.6f, 0.8f, 1f));
        SetRectAnchors(sidebarTitle.rectTransform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.86f));

        // 카테고리 버튼 컨테이너 (VerticalLayoutGroup)
        GameObject catContainer = CreateUIObject("ButtonContainer", sidebarObj.transform,
            new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.76f), new Vector2(0.5f, 1f),
            Vector2.zero, Vector2.zero);
        
        VerticalLayoutGroup catLayout = catContainer.AddComponent<VerticalLayoutGroup>();
        catLayout.spacing = 15;
        catLayout.childAlignment = TextAnchor.UpperCenter;
        catLayout.childControlWidth = true;
        catLayout.childControlHeight = false;
        catLayout.childForceExpandWidth = true;
        catLayout.childForceExpandHeight = false;

        Button btnSkin = CreateCategoryButton("Btn_Category_Skin", catContainer.transform, "🎭 리더 스킨", mainFont, new Color(0.18f, 0.5f, 0.85f), 65f);
        Button btnEmote = CreateCategoryButton("Btn_Category_Emote", catContainer.transform, "💬 감정표현\n(준비중)", mainFont, new Color(0.13f, 0.16f, 0.23f), 65f);
        btnEmote.interactable = false;

        Button btnOther = CreateCategoryButton("Btn_Category_Other", catContainer.transform, "📦 기타 아이템\n(준비중)", mainFont, new Color(0.13f, 0.16f, 0.23f), 65f);
        btnOther.interactable = false;

        // =========================================================================
        // C. 2단계: 메인 스킨 관리 화면 (MainContent_Skin) - 화면에 꽉 차게 확장
        // (우측 상단 400px x 85px 재화/설정 구역 침범 금지)
        // =========================================================================
        GameObject skinViewObj = CreateUIObject("MainContent_Skin", vanityRoot.transform,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(240f, 15f), new Vector2(-20f, -85f));
        
        VanitySkinView skinView = skinViewObj.AddComponent<VanitySkinView>();

        // 1) 중앙 상단 제어 바 (스킨 목록 위 [뒤로가기] 및 [필터 ⚙️] 버튼)
        GameObject middleTopBar = CreateUIObject("MiddleTopBar", skinViewObj.transform,
            new Vector2(0f, 1f), new Vector2(0.58f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -50f), new Vector2(0f, 0f));
        
        HorizontalLayoutGroup midTopLayout = middleTopBar.AddComponent<HorizontalLayoutGroup>();
        midTopLayout.spacing = 10;
        midTopLayout.childAlignment = TextAnchor.MiddleLeft;
        midTopLayout.childControlWidth = false;
        midTopLayout.childControlHeight = true;

        Button btnBackCat = CreateSimpleButton("Btn_BackToCategories", middleTopBar.transform, "◀ 카테고리 목록", mainFont, 140f);
        Button btnToggleFilter = CreateSimpleButton("Btn_ToggleFilter", middleTopBar.transform, "필터 설정 ⚙️", mainFont, 130f);
        TextMeshProUGUI btnToggleFilterTxt = btnToggleFilter.GetComponentInChildren<TextMeshProUGUI>();

        // 2) 중앙 스킨 목록 ScrollView
        GameObject scrollObj = CreateUIObject("SkinListScrollView", skinViewObj.transform,
            new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(0f, -60f));
        
        Image scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0.06f, 0.08f, 0.12f, 0.6f);

        ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreateUIObject("Viewport", scrollObj.transform, Vector2.zero, Vector2.one, new Vector2(0f, 1f), new Vector2(5f, 5f), new Vector2(-5f, -5f));
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUIObject("Content", viewport.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 300f));
        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150f, 210f);
        grid.spacing = new Vector2(15f, 15f);
        grid.padding = new RectOffset(15, 15, 15, 15);
        grid.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content.GetComponent<RectTransform>();

        // 3) [필터] 설정 패널 (스킨 목록 대신 중앙에 표시되는 패널)
        GameObject filterPanelObj = CreateUIObject("FilterPanel", skinViewObj.transform,
            new Vector2(0f, 0f), new Vector2(0.58f, 1f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(0f, -60f));
        
        Image filterBg = filterPanelObj.AddComponent<Image>();
        filterBg.color = new Color(0.07f, 0.1f, 0.16f, 0.98f);
        Outline filterOutline = filterPanelObj.AddComponent<Outline>();
        filterOutline.effectColor = new Color(0.2f, 0.5f, 0.9f, 0.4f);

        // 필터 타이틀
        TextMeshProUGUI filterTitle = CreateTMPText("Txt_FilterTitle", filterPanelObj.transform, "🔍 스킨 목록 필터 설정", mainFont, 20, TextAlignmentOptions.Center, Color.white);
        SetRectAnchors(filterTitle.rectTransform, new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.95f));

        // 직업 필터 라벨
        TextMeshProUGUI classLabel = CreateTMPText("Txt_ClassLabel", filterPanelObj.transform, "직업별 보기", mainFont, 16, TextAlignmentOptions.Left, new Color(0.7f, 0.85f, 1f));
        SetRectAnchors(classLabel.rectTransform, new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.80f));

        // 직업 버튼 그룹 (Grid)
        GameObject classBtnGroup = CreateUIObject("ClassButtonGroup", filterPanelObj.transform,
            new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.70f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        
        GridLayoutGroup classGrid = classBtnGroup.AddComponent<GridLayoutGroup>();
        classGrid.cellSize = new Vector2(160f, 50f);
        classGrid.spacing = new Vector2(15f, 12f);
        classGrid.childAlignment = TextAnchor.UpperCenter;

        Button btnFAll = CreateSimpleButton("Btn_F_All", classBtnGroup.transform, "전체 직업", mainFont, 160f);
        Button btnFGangzi = CreateSimpleButton("Btn_F_Gangzi", classBtnGroup.transform, "강지", mainFont, 160f);
        Button btnFYuni = CreateSimpleButton("Btn_F_Yuni", classBtnGroup.transform, "유니", mainFont, 160f);
        Button btnFHuya = CreateSimpleButton("Btn_F_Huya", classBtnGroup.transform, "후야", mainFont, 160f);

        // 미보유 토글
        Toggle toggleFUnowned = CreateSimpleToggle("Toggle_F_Unowned", filterPanelObj.transform, "미보유 스킨도 목록에 표시", mainFont);
        SetRectAnchors(toggleFUnowned.GetComponent<RectTransform>(), new Vector2(0.1f, 0.32f), new Vector2(0.9f, 0.40f));

        // 필터 적용 버튼
        Button btnFApply = CreateSimpleButton("Btn_F_Apply", filterPanelObj.transform, "필터 적용 및 목록 보기", mainFont, 240f);
        SetRectAnchors(btnFApply.GetComponent<RectTransform>(), new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.22f));

        filterPanelObj.SetActive(false);

        // 4) 우측 상세 정보 패널 (RightDetailPanel)
        GameObject detailPanel = CreateUIObject("RightDetailPanel", skinViewObj.transform,
            new Vector2(0.60f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            Vector2.zero, Vector2.zero);
        
        Image detailBg = detailPanel.AddComponent<Image>();
        detailBg.color = new Color(0.08f, 0.11f, 0.17f, 0.95f);

        // 스킨 일러스트 (상단 왼쪽)
        GameObject illuObj = CreateUIObject("SkinIllustration", detailPanel.transform,
            new Vector2(0.04f, 0.50f), new Vector2(0.48f, 0.96f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image illuImg = illuObj.AddComponent<Image>();
        illuImg.preserveAspect = true;

        // 스킨 정보 텍스트 (상단 오른쪽)
        GameObject infoGroup = CreateUIObject("InfoGroup", detailPanel.transform,
            new Vector2(0.52f, 0.50f), new Vector2(0.96f, 0.96f), new Vector2(0f, 1f),
            Vector2.zero, Vector2.zero);
        
        TextMeshProUGUI txtTitle = CreateTMPText("Txt_SkinTitle", infoGroup.transform, "스킨 이름", mainFont, 24, TextAlignmentOptions.TopLeft, Color.white);
        SetRectAnchors(txtTitle.rectTransform, new Vector2(0f, 0.78f), new Vector2(1f, 1f));

        TextMeshProUGUI txtClass = CreateTMPText("Txt_SkinClass", infoGroup.transform, "직업: 강지", mainFont, 16, TextAlignmentOptions.TopLeft, new Color(0.7f, 0.85f, 1f));
        SetRectAnchors(txtClass.rectTransform, new Vector2(0f, 0.65f), new Vector2(1f, 0.78f));

        TextMeshProUGUI txtDesc = CreateTMPText("Txt_SkinDesc", infoGroup.transform, "스킨 설명 문구가 표시됩니다.", mainFont, 14, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.85f, 0.85f));
        SetRectAnchors(txtDesc.rectTransform, new Vector2(0f, 0.22f), new Vector2(1f, 0.65f));

        TextMeshProUGUI txtSource = CreateTMPText("Txt_SkinSource", infoGroup.transform, "● 보유 중", mainFont, 14, TextAlignmentOptions.BottomLeft, new Color(0.5f, 1f, 0.3f));
        SetRectAnchors(txtSource.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.2f));

        // 감정표현 4개 슬롯 영역 (하단)
        GameObject emoteGroup = CreateUIObject("EmoteGroup", detailPanel.transform,
            new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.48f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        
        TextMeshProUGUI emoteHeader = CreateTMPText("EmoteHeader", emoteGroup.transform, "🎭 장착 감정표현 (4종)", mainFont, 16, TextAlignmentOptions.TopLeft, new Color(1f, 0.85f, 0.3f));
        SetRectAnchors(emoteHeader.rectTransform, new Vector2(0f, 0.85f), new Vector2(1f, 1f));

        GameObject slotsContainer = CreateUIObject("SlotsContainer", emoteGroup.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0.82f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        
        GridLayoutGroup emoteGrid = slotsContainer.AddComponent<GridLayoutGroup>();
        emoteGrid.cellSize = new Vector2(250f, 52f);
        emoteGrid.spacing = new Vector2(10f, 8f);
        emoteGrid.childAlignment = TextAnchor.UpperLeft;

        var emoteSlotList = new List<VanitySkinView.EmoteSlotUI>();
        for (int i = 0; i < 4; i++)
        {
            var slotUI = CreateEmoteSlotUI($"Slot_{i}", slotsContainer.transform, mainFont, i + 1);
            emoteSlotList.Add(slotUI);
        }

        // 하단 액션 버튼 그룹
        GameObject bottomBar = CreateUIObject("BottomActionBar", detailPanel.transform,
            new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.13f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        
        Button btnRep = CreateSimpleButton("Btn_SetRepresentative", bottomBar.transform, "대표 스킨으로 설정", mainFont, 240f);
        SetRectAnchors(btnRep.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
        btnRep.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 44f);
        TextMeshProUGUI btnRepTxt = btnRep.GetComponentInChildren<TextMeshProUGUI>();

        Button btnShop = CreateSimpleButton("Btn_GoToShop", bottomBar.transform, "상점에서 구매하기 ➔", mainFont, 240f);
        SetRectAnchors(btnShop.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
        btnShop.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 44f);
        btnShop.gameObject.SetActive(false);

        // =========================================================================
        // D. 감정표현 교체 서브 팝업 (VanityEmoteEditPopup)
        // (우측 상단 재화창 및 설정창과 겹치지 않도록 Y 위치를 -50f로 내려 안전 영역 확보)
        // =========================================================================
        GameObject popupObj = CreateUIObject("EmoteEditPopup", vanityRoot.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -50f), new Vector2(820f, 500f));
        
        Image popupBg = popupObj.AddComponent<Image>();
        popupBg.color = new Color(0.07f, 0.09f, 0.14f, 0.98f);
        Outline popupOutline = popupObj.AddComponent<Outline>();
        popupOutline.effectColor = new Color(0.3f, 0.6f, 1f, 0.5f);
        popupOutline.effectDistance = new Vector2(2f, -2f);

        VanityEmoteEditPopup emotePopup = popupObj.AddComponent<VanityEmoteEditPopup>();

        // 팝업 상단 바
        GameObject pTop = CreateUIObject("PopupTop", popupObj.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(20f, -50f), new Vector2(-20f, -10f));
        
        TextMeshProUGUI pTitle = CreateTMPText("Title", pTop.transform, "감정표현 변경", mainFont, 20, TextAlignmentOptions.Left, Color.white);
        SetRectAnchors(pTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0.52f, 1f));

        Toggle pToggleUnowned = CreateSimpleToggle("Toggle_ShowUnownedEmote", pTop.transform, "미보유 포함", mainFont);
        SetRectAnchors(pToggleUnowned.GetComponent<RectTransform>(), new Vector2(0.55f, 0f), new Vector2(0.85f, 1f));

        Button pCloseBtn = CreateSimpleButton("Btn_Close", pTop.transform, "✕", mainFont, 40f);
        SetRectAnchors(pCloseBtn.GetComponent<RectTransform>(), new Vector2(0.88f, 0f), new Vector2(0.98f, 1f));

        // 팝업 중앙 그리드 스크롤뷰
        GameObject pScroll = CreateUIObject("EmoteGridScroll", popupObj.transform,
            new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(0f, 0.5f),
            new Vector2(20f, 20f), new Vector2(-10f, -65f));
        Image pScrollBg = pScroll.AddComponent<Image>();
        pScrollBg.color = new Color(0.04f, 0.06f, 0.09f, 0.6f);

        ScrollRect pScrollRect = pScroll.AddComponent<ScrollRect>();
        pScrollRect.horizontal = false;
        pScrollRect.vertical = true;

        GameObject pViewport = CreateUIObject("Viewport", pScroll.transform, Vector2.zero, Vector2.one, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        pViewport.AddComponent<RectMask2D>();

        GameObject pContent = CreateUIObject("Content", pViewport.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 200f));
        GridLayoutGroup pGrid = pContent.AddComponent<GridLayoutGroup>();
        pGrid.cellSize = new Vector2(195f, 65f);
        pGrid.spacing = new Vector2(10f, 10f);
        pGrid.padding = new RectOffset(10, 10, 10, 10);
        ContentSizeFitter pCsf = pContent.AddComponent<ContentSizeFitter>();
        pCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        pScrollRect.viewport = pViewport.GetComponent<RectTransform>();
        pScrollRect.content = pContent.GetComponent<RectTransform>();

        // 팝업 우측 상세 패널
        GameObject pDetail = CreateUIObject("EmoteDetailPanel", popupObj.transform,
            new Vector2(0.6f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(10f, 20f), new Vector2(-20f, -65f));
        Image pDetailBg = pDetail.AddComponent<Image>();
        pDetailBg.color = new Color(0.05f, 0.07f, 0.11f, 0.8f);

        TextMeshProUGUI pEmoteName = CreateTMPText("Txt_EmoteName", pDetail.transform, "감정표현 이름", mainFont, 20, TextAlignmentOptions.TopLeft, Color.white);
        SetRectAnchors(pEmoteName.rectTransform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.95f));

        TextMeshProUGUI pEmoteSpeech = CreateTMPText("Txt_EmoteSpeech", pDetail.transform, "\"대사 내용\"", mainFont, 16, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.9f, 1f));
        SetRectAnchors(pEmoteSpeech.rectTransform, new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.75f));

        TextMeshProUGUI pEmoteSource = CreateTMPText("Txt_EmoteSource", pDetail.transform, "● 보유 중", mainFont, 14, TextAlignmentOptions.TopLeft, new Color(0.5f, 1f, 0.3f));
        SetRectAnchors(pEmoteSource.rectTransform, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.48f));

        Button pVoiceBtn = CreateSimpleButton("Btn_PlayVoice", pDetail.transform, "🔊 음성 미리듣기", mainFont, 160f);
        SetRectAnchors(pVoiceBtn.GetComponent<RectTransform>(), new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.32f));

        Button pEquipBtn = CreateSimpleButton("Btn_Equip", pDetail.transform, "이 슬롯에 장착", mainFont, 160f);
        SetRectAnchors(pEquipBtn.GetComponent<RectTransform>(), new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.17f));
        TextMeshProUGUI pEquipBtnTxt = pEquipBtn.GetComponentInChildren<TextMeshProUGUI>();

        popupObj.SetActive(false);

        // =========================================================================
        // E. 직렬화 필드 연결
        // =========================================================================
        
        // 1) VanitySkinView 연결
        SerializedObject skinViewSo = new SerializedObject(skinView);
        skinViewSo.FindProperty("btnBackToCategories").objectReferenceValue = btnBackCat;
        skinViewSo.FindProperty("btnToggleFilter").objectReferenceValue = btnToggleFilter;
        skinViewSo.FindProperty("btnToggleFilterText").objectReferenceValue = btnToggleFilterTxt;
        skinViewSo.FindProperty("skinListScrollView").objectReferenceValue = scrollObj;
        skinViewSo.FindProperty("skinSlotParent").objectReferenceValue = content.transform;
        skinViewSo.FindProperty("skinSlotPrefab").objectReferenceValue = skinSlotPrefab;

        skinViewSo.FindProperty("filterPanel").objectReferenceValue = filterPanelObj;
        skinViewSo.FindProperty("btnFilterAll").objectReferenceValue = btnFAll;
        skinViewSo.FindProperty("btnFilterGangzi").objectReferenceValue = btnFGangzi;
        skinViewSo.FindProperty("btnFilterYuni").objectReferenceValue = btnFYuni;
        skinViewSo.FindProperty("btnFilterHuya").objectReferenceValue = btnFHuya;
        skinViewSo.FindProperty("showUnownedToggle").objectReferenceValue = toggleFUnowned;
        skinViewSo.FindProperty("btnApplyFilter").objectReferenceValue = btnFApply;

        skinViewSo.FindProperty("skinIllustrationImage").objectReferenceValue = illuImg;
        skinViewSo.FindProperty("skinTitleText").objectReferenceValue = txtTitle;
        skinViewSo.FindProperty("skinClassText").objectReferenceValue = txtClass;
        skinViewSo.FindProperty("skinDescriptionText").objectReferenceValue = txtDesc;
        skinViewSo.FindProperty("skinSourceText").objectReferenceValue = txtSource;
        skinViewSo.FindProperty("btnSetRepresentative").objectReferenceValue = btnRep;
        skinViewSo.FindProperty("btnSetRepresentativeText").objectReferenceValue = btnRepTxt;
        skinViewSo.FindProperty("btnGoToShop").objectReferenceValue = btnShop;
        skinViewSo.FindProperty("emoteEditPopup").objectReferenceValue = emotePopup;

        SerializedProperty emoteSlotsProp = skinViewSo.FindProperty("emoteSlots");
        emoteSlotsProp.ClearArray();
        for (int i = 0; i < emoteSlotList.Count; i++)
        {
            emoteSlotsProp.InsertArrayElementAtIndex(i);
            SerializedProperty elem = emoteSlotsProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("root").objectReferenceValue = emoteSlotList[i].root;
            elem.FindPropertyRelative("labelText").objectReferenceValue = emoteSlotList[i].labelText;
            elem.FindPropertyRelative("speechText").objectReferenceValue = emoteSlotList[i].speechText;
            elem.FindPropertyRelative("playVoiceButton").objectReferenceValue = emoteSlotList[i].playVoiceButton;
            elem.FindPropertyRelative("editButton").objectReferenceValue = emoteSlotList[i].editButton;
            elem.FindPropertyRelative("lockIcon").objectReferenceValue = emoteSlotList[i].lockIcon;
        }
        skinViewSo.ApplyModifiedProperties();

        // 2) VanityEmoteEditPopup 연결
        SerializedObject popupSo = new SerializedObject(emotePopup);
        popupSo.FindProperty("popupRoot").objectReferenceValue = popupObj;
        popupSo.FindProperty("closeButton").objectReferenceValue = pCloseBtn;
        popupSo.FindProperty("titleText").objectReferenceValue = pTitle;
        popupSo.FindProperty("showUnownedToggle").objectReferenceValue = pToggleUnowned;
        popupSo.FindProperty("emoteSlotParent").objectReferenceValue = pContent.transform;
        popupSo.FindProperty("emoteSlotPrefab").objectReferenceValue = emoteSlotPrefab;
        popupSo.FindProperty("selectedEmoteNameText").objectReferenceValue = pEmoteName;
        popupSo.FindProperty("selectedEmoteSpeechText").objectReferenceValue = pEmoteSpeech;
        popupSo.FindProperty("selectedEmoteSourceText").objectReferenceValue = pEmoteSource;
        popupSo.FindProperty("voicePlayButton").objectReferenceValue = pVoiceBtn;
        popupSo.FindProperty("equipButton").objectReferenceValue = pEquipBtn;
        popupSo.FindProperty("equipButtonText").objectReferenceValue = pEquipBtnTxt;
        popupSo.ApplyModifiedProperties();

        // 3) VanityManager 연결
        VanityManager vanityMgr = storageMgr.GetComponent<VanityManager>();
        if (vanityMgr == null) vanityMgr = storageMgr.gameObject.AddComponent<VanityManager>();

        SerializedObject vanityMgrSo = new SerializedObject(vanityMgr);
        vanityMgrSo.FindProperty("cameraController").objectReferenceValue = camCtrl;
        vanityMgrSo.FindProperty("vanitySectionIndex").intValue = 1;
        vanityMgrSo.FindProperty("vanityPanelRoot").objectReferenceValue = vanityRoot;
        vanityMgrSo.FindProperty("panelToggler").objectReferenceValue = rootToggler;
        vanityMgrSo.FindProperty("categorySidebarRoot").objectReferenceValue = sidebarObj;
        vanityMgrSo.FindProperty("btnCategorySkin").objectReferenceValue = btnSkin;
        vanityMgrSo.FindProperty("btnCategoryEmote").objectReferenceValue = btnEmote;
        vanityMgrSo.FindProperty("btnCategoryOther").objectReferenceValue = btnOther;
        vanityMgrSo.FindProperty("btnBackToOverview").objectReferenceValue = btnBackOverview;
        vanityMgrSo.FindProperty("skinView").objectReferenceValue = skinView;
        vanityMgrSo.ApplyModifiedProperties();

        // 시작 시 스킨 뷰는 비활성화, 카테고리 사이드바만 켜진 상태로 대기
        skinViewObj.SetActive(false);
        vanityRoot.SetActive(false);

        EditorUtility.SetDirty(storageMgr.gameObject);
        EditorUtility.SetDirty(canvas.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        Debug.Log("✨ [SetupVanityUI] 사용자 요구사항을 반영한 VanityPos UI 셋업이 성공적으로 완료되었습니다!");
    }

    private static GameObject CreateUIObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return obj;
    }

    private static void SetRectAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI CreateTMPText(string name, Transform parent, string text, TMP_FontAsset font, float size, TextAlignmentOptions align, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.layer = LayerMask.NameToLayer("UI");

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateCategoryButton(string name, Transform parent, string label, TMP_FontAsset font, Color color, float height = 60f)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.layer = LayerMask.NameToLayer("UI");

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = height;

        Image img = obj.AddComponent<Image>();
        img.color = color;

        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock cb = btn.colors;
        cb.highlightedColor = color * 1.25f;
        cb.pressedColor = color * 0.85f;
        cb.disabledColor = color * 0.4f;
        btn.colors = cb;

        TextMeshProUGUI txt = CreateTMPText("Text", obj.transform, label, font, 16, TextAlignmentOptions.Center, Color.white);
        SetRectAnchors(txt.rectTransform, Vector2.zero, Vector2.one);

        return btn;
    }

    private static Button CreateSimpleButton(string name, Transform parent, string label, TMP_FontAsset font, float width)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.layer = LayerMask.NameToLayer("UI");

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = width;

        Image img = obj.AddComponent<Image>();
        img.color = new Color(0.15f, 0.22f, 0.32f, 0.95f);

        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.25f, 0.4f, 0.6f);
        cb.pressedColor = new Color(0.1f, 0.15f, 0.25f);
        cb.disabledColor = new Color(0.1f, 0.1f, 0.15f, 0.5f);
        btn.colors = cb;

        TextMeshProUGUI txt = CreateTMPText("Text", obj.transform, label, font, 14, TextAlignmentOptions.Center, Color.white);
        SetRectAnchors(txt.rectTransform, Vector2.zero, Vector2.one);

        return btn;
    }

    private static Toggle CreateSimpleToggle(string name, Transform parent, string label, TMP_FontAsset font)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.layer = LayerMask.NameToLayer("UI");

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = 200f;
        le.preferredHeight = 35f;

        Toggle toggle = obj.AddComponent<Toggle>();

        GameObject bgObj = CreateUIObject("Background", obj.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -12f), new Vector2(24f, 12f));
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.25f, 0.35f);

        GameObject checkObj = CreateUIObject("Checkmark", bgObj.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(3f, 3f), new Vector2(-3f, -3f));
        Image checkImg = checkObj.AddComponent<Image>();
        checkImg.color = new Color(0.2f, 0.8f, 1f);

        toggle.graphic = checkImg;
        toggle.targetGraphic = bgImg;
        toggle.isOn = false;

        TextMeshProUGUI txt = CreateTMPText("Label", obj.transform, label, font, 14, TextAlignmentOptions.Left, Color.white);
        SetRectAnchors(txt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f));
        txt.rectTransform.offsetMin = new Vector2(32f, 0f);

        return toggle;
    }

    private static VanitySkinView.EmoteSlotUI CreateEmoteSlotUI(string name, Transform parent, TMP_FontAsset font, int index)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        obj.layer = LayerMask.NameToLayer("UI");

        Image bg = obj.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.16f, 0.24f, 0.9f);

        GameObject lockObj = CreateUIObject("LockIcon", obj.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -10f), new Vector2(28f, 10f));
        TextMeshProUGUI lockTxt = CreateTMPText("LockTxt", lockObj.transform, "🔒", font, 14, TextAlignmentOptions.Center, Color.yellow);
        SetRectAnchors(lockTxt.rectTransform, Vector2.zero, Vector2.one);
        lockObj.SetActive(false);

        TextMeshProUGUI labelTxt = CreateTMPText("Txt_Label", obj.transform, $"감정 {index}", font, 13, TextAlignmentOptions.TopLeft, new Color(0.4f, 0.8f, 1f));
        SetRectAnchors(labelTxt.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.65f, 0.95f));
        labelTxt.rectTransform.offsetMin = new Vector2(32f, 0f);

        TextMeshProUGUI speechTxt = CreateTMPText("Txt_Speech", obj.transform, "\"안녕하세요!\"", font, 12, TextAlignmentOptions.BottomLeft, new Color(0.85f, 0.85f, 0.85f));
        SetRectAnchors(speechTxt.rectTransform, new Vector2(0f, 0.05f), new Vector2(0.65f, 0.55f));
        speechTxt.rectTransform.offsetMin = new Vector2(32f, 0f);

        Button voiceBtn = CreateSimpleButton("Btn_Voice", obj.transform, "🔊", font, 36f);
        SetRectAnchors(voiceBtn.GetComponent<RectTransform>(), new Vector2(0.68f, 0.15f), new Vector2(0.81f, 0.85f));

        Button editBtn = CreateSimpleButton("Btn_Edit", obj.transform, "변경", font, 44f);
        SetRectAnchors(editBtn.GetComponent<RectTransform>(), new Vector2(0.83f, 0.15f), new Vector2(0.97f, 0.85f));

        var slot = new VanitySkinView.EmoteSlotUI
        {
            root = obj,
            labelText = labelTxt,
            speechText = speechTxt,
            playVoiceButton = voiceBtn,
            editButton = editBtn,
            lockIcon = lockObj
        };
        return slot;
    }

    private static GameObject EnsureEmoteSlotPrefab(TMP_FontAsset font)
    {
        string prefabPath = "Assets/Prefab/Deck/EmoteSlot.prefab";
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null)
        {
            return existingPrefab;
        }

        // 프리팹이 없으면 새로 생성
        GameObject obj = new GameObject("EmoteSlot", typeof(RectTransform));
        obj.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(195f, 65f);

        Image img = obj.AddComponent<Image>();
        img.color = new Color(0.13f, 0.18f, 0.28f, 0.95f);

        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        cb.pressedColor = new Color(0.75f, 0.85f, 1f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;

        // Outline
        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.45f, 0.7f, 0.5f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 자물쇠 아이콘
        GameObject lockObj = CreateUIObject("LockIcon", obj.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-24f, -24f), Vector2.zero);
        TextMeshProUGUI lockTxt = CreateTMPText("LockTxt", lockObj.transform, "🔒", font, 14, TextAlignmentOptions.Center, Color.yellow);
        SetRectAnchors(lockTxt.rectTransform, Vector2.zero, Vector2.one);
        lockTxt.raycastTarget = false;
        lockObj.SetActive(false);

        // 텍스트 라벨 (클릭 방해 방지 raycastTarget = false)
        TextMeshProUGUI txt = CreateTMPText("Text", obj.transform, "감정표현", font, 14, TextAlignmentOptions.Center, Color.white);
        SetRectAnchors(txt.rectTransform, Vector2.zero, Vector2.one);
        txt.rectTransform.offsetMin = new Vector2(10f, 6f);
        txt.rectTransform.offsetMax = new Vector2(-10f, -6f);
        txt.raycastTarget = false;

        // 프리팹 파일로 저장
        System.IO.Directory.CreateDirectory("Assets/Prefab/Deck");
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(obj, prefabPath);
        Object.DestroyImmediate(obj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SetupVanityUI] 📦 새로운 감정표현 슬롯 프리팹 생성 완료: {prefabPath}");
        return savedPrefab;
    }
}
#endif
