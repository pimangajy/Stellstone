#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Storage 씬의 VanityPanelGroup UI 요소들에 PastelUIFree 스프라이트 테마를 적용하는 에디터 툴입니다.
/// </summary>
public static class VanityUIStyler
{
    private const string SCENE_PATH = "Assets/Scenes/Storage.unity";
    private const string SPRITE_PATH = "Assets/Sprite/UI/PastelUIFree.png";

    [MenuItem("StellarDuel/Style Vanity UI with Pastel")]
    public static void ApplyPastelUI()
    {
        // 1. 씬 열기
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != SCENE_PATH)
        {
            EditorSceneManager.OpenScene(SCENE_PATH);
        }

        // 2. 스프라이트 시트 내 서브 에셋 로드
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(SPRITE_PATH);
        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        foreach (var obj in allAssets)
        {
            if (obj is Sprite sp)
            {
                sprites[sp.name] = sp;
            }
        }

        Debug.Log($"[VanityUIStyler] 🎨 로드된 Pastel 스프라이트 개수: {sprites.Count}");

        // 주요 스프라이트 매핑
        Sprite winBlue = sprites.GetValueOrDefault("Window_Blue_87x81");
        Sprite winPink = sprites.GetValueOrDefault("Window_Pink_57x81");
        Sprite winGreen = sprites.GetValueOrDefault("Window_Green_47x48");
        Sprite menuBtn = sprites.GetValueOrDefault("MenuBtn_Blank_52x15");
        Sprite btnPurple0 = sprites.GetValueOrDefault("Button_Purple_0_14x14");
        Sprite btnPurple16 = sprites.GetValueOrDefault("Button_Purple_16_14x14");
        Sprite btnPurple28 = sprites.GetValueOrDefault("Button_Purple_28_14x10");
        Sprite iconBtn0 = sprites.GetValueOrDefault("IconBtn_0_14x14");
        Sprite iconBtn4 = sprites.GetValueOrDefault("IconBtn_4_14x14");
        Sprite iconBtn12 = sprites.GetValueOrDefault("IconBtn_12_14x14");

        // 3. VanityPanelGroup 탐색 (비활성화 상태 고려)
        GameObject vanityRoot = null;
        foreach (var rootGo in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            var target = FindRecursive(rootGo.transform, "VanityPanelGroup");
            if (target != null)
            {
                vanityRoot = target.gameObject;
                break;
            }
        }

        if (vanityRoot == null)
        {
            Debug.LogError("[VanityUIStyler] ❌ VanityPanelGroup 오브젝트를 찾을 수 없습니다!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(vanityRoot, "Style Vanity UI with Pastel");

        // A. 메인 최상위 배경 (화면 어둡게 집중시키는 반투명 딤 처리 유지)
        SetImage(vanityRoot, null, new Color(0.02f, 0.03f, 0.06f, 0.85f), Image.Type.Simple);

        // B. CategorySidebar (좌측 사이드바)
        Transform sidebar = FindRecursive(vanityRoot.transform, "CategorySidebar");
        if (sidebar != null)
        {
            SetImage(sidebar.gameObject, winBlue, Color.white, Image.Type.Sliced);
            SetTextMeshColor(sidebar, "Txt_CategoryTitle", new Color(0.18f, 0.2f, 0.35f, 1f));

            StyleButton(sidebar, "Btn_BackToOverview", menuBtn, new Color(0.2f, 0.22f, 0.32f, 1f));
            StyleButton(sidebar, "Btn_Category_Skin", menuBtn, new Color(0.15f, 0.18f, 0.3f, 1f));
            StyleButton(sidebar, "Btn_Category_Emote", menuBtn, new Color(0.15f, 0.18f, 0.3f, 1f));
            StyleButton(sidebar, "Btn_Category_Other", menuBtn, new Color(0.15f, 0.18f, 0.3f, 1f));
        }

        // C. MainContent_Skin (스킨 뷰)
        Transform mainSkin = FindRecursive(vanityRoot.transform, "MainContent_Skin");
        if (mainSkin != null)
        {
            // 상단 바 버튼
            StyleButton(mainSkin, "Btn_BackToCategories", menuBtn, new Color(0.2f, 0.22f, 0.32f, 1f));
            StyleButton(mainSkin, "Btn_ToggleFilter", menuBtn, new Color(0.2f, 0.22f, 0.32f, 1f));

            // FilterPanel (필터 패널)
            Transform filterPanel = FindRecursive(mainSkin, "FilterPanel");
            if (filterPanel != null)
            {
                SetImage(filterPanel.gameObject, winGreen, Color.white, Image.Type.Sliced);
                SetTextMeshColor(filterPanel, "Txt_FilterTitle", new Color(0.15f, 0.28f, 0.2f, 1f));
                SetTextMeshColor(filterPanel, "Txt_ClassLabel", new Color(0.25f, 0.35f, 0.28f, 1f));

                Sprite filterBtnSp = btnPurple16 != null ? btnPurple16 : menuBtn;
                StyleButton(filterPanel, "Btn_F_All", filterBtnSp, Color.white);
                StyleButton(filterPanel, "Btn_F_Gangzi", filterBtnSp, Color.white);
                StyleButton(filterPanel, "Btn_F_Yuni", filterBtnSp, Color.white);
                StyleButton(filterPanel, "Btn_F_Huya", filterBtnSp, Color.white);
                StyleButton(filterPanel, "Btn_F_Apply", menuBtn, new Color(0.15f, 0.28f, 0.2f, 1f));
            }

            // RightDetailPanel (우측 상세 정보 패널)
            Transform detailPanel = FindRecursive(mainSkin, "RightDetailPanel");
            if (detailPanel != null)
            {
                SetImage(detailPanel.gameObject, winPink, Color.white, Image.Type.Sliced);

                // 가독성 높은 어두운 파스텔 폰트 색상
                SetTextMeshColor(detailPanel, "Txt_SkinTitle", new Color(0.35f, 0.15f, 0.25f, 1f));
                SetTextMeshColor(detailPanel, "Txt_SkinClass", new Color(0.45f, 0.25f, 0.35f, 1f));
                SetTextMeshColor(detailPanel, "Txt_SkinDesc", new Color(0.3f, 0.25f, 0.3f, 1f));
                SetTextMeshColor(detailPanel, "Txt_SkinSource", new Color(0.5f, 0.35f, 0.45f, 1f));

                // 감정표현 4개 슬롯 (Slot_0 ~ Slot_3)
                for (int i = 0; i < 4; i++)
                {
                    Transform slot = FindRecursive(detailPanel, $"Slot_{i}");
                    if (slot != null)
                    {
                        SetImage(slot.gameObject, winBlue, new Color(1f, 1f, 1f, 0.95f), Image.Type.Sliced);
                        SetTextMeshColor(slot, "Txt_Label", new Color(0.2f, 0.25f, 0.4f, 1f));
                        SetTextMeshColor(slot, "Txt_Speech", new Color(0.3f, 0.35f, 0.45f, 1f));

                        Sprite voiceSp = iconBtn4 != null ? iconBtn4 : iconBtn0;
                        StyleButton(slot, "Btn_Voice", voiceSp, Color.white);

                        Sprite editSp = btnPurple28 != null ? btnPurple28 : menuBtn;
                        StyleButton(slot, "Btn_Edit", editSp, Color.white);
                    }
                }

                // 하단 액션 버튼
                StyleButton(detailPanel, "Btn_SetRepresentative", menuBtn, new Color(0.2f, 0.25f, 0.4f, 1f));
                StyleButton(detailPanel, "Btn_GoToShop", menuBtn, new Color(0.4f, 0.2f, 0.25f, 1f));
            }
        }

        // D. EmoteEditPopup (감정표현 수정 팝업)
        Transform emotePopup = FindRecursive(vanityRoot.transform, "EmoteEditPopup");
        if (emotePopup != null)
        {
            SetImage(emotePopup.gameObject, winBlue, Color.white, Image.Type.Sliced);
            SetTextMeshColor(emotePopup, "Title", new Color(0.18f, 0.22f, 0.35f, 1f));

            Sprite closeSp = iconBtn12 != null ? iconBtn12 : btnPurple0;
            StyleButton(emotePopup, "Btn_Close", closeSp, Color.white);

            Sprite popupVoiceSp = iconBtn4 != null ? iconBtn4 : iconBtn0;
            StyleButton(emotePopup, "Btn_PlayVoice", popupVoiceSp, Color.white);
            StyleButton(emotePopup, "Btn_Equip", menuBtn, new Color(0.18f, 0.22f, 0.35f, 1f));

            Transform emoteDetail = FindRecursive(emotePopup, "EmoteDetailPanel");
            if (emoteDetail != null)
            {
                SetImage(emoteDetail.gameObject, winPink, Color.white, Image.Type.Sliced);
                SetTextMeshColor(emoteDetail, "Txt_EmoteName", new Color(0.35f, 0.15f, 0.25f, 1f));
                SetTextMeshColor(emoteDetail, "Txt_EmoteSpeech", new Color(0.3f, 0.25f, 0.3f, 1f));
                SetTextMeshColor(emoteDetail, "Txt_EmoteSource", new Color(0.5f, 0.35f, 0.45f, 1f));
            }
        }

        // E. EmoteSlot 프리팹 스타일링
        string emoteSlotPath = "Assets/Prefab/Storage/EmoteSlot.prefab";
        GameObject emoteSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(emoteSlotPath);
        if (emoteSlotPrefab != null)
        {
            SetImage(emoteSlotPrefab, menuBtn, Color.white, Image.Type.Sliced);
            var txt = emoteSlotPrefab.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.color = new Color(0.18f, 0.22f, 0.35f, 1f);
            }
            EditorUtility.SetDirty(emoteSlotPrefab);
            PrefabUtility.SavePrefabAsset(emoteSlotPrefab);
        }

        // 4. 저장
        EditorUtility.SetDirty(vanityRoot);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[VanityUIStyler] ✨ VanityPanelGroup UI에 Pastel 테마 스타일링이 성공적으로 적용되었습니다!");
    }

    private static void StyleButton(Transform parent, string buttonName, Sprite sprite, Color textColor)
    {
        Transform btnT = FindRecursive(parent, buttonName);
        if (btnT == null) return;

        SetImage(btnT.gameObject, sprite, Color.white, Image.Type.Sliced);

        var btn = btnT.GetComponent<Button>();
        if (btn != null)
        {
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.95f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            btn.colors = colors;
        }

        var tmp = btnT.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.color = textColor;
        }
    }

    private static void SetImage(GameObject go, Sprite sprite, Color color, Image.Type type)
    {
        var img = go.GetComponent<Image>();
        if (img == null)
        {
            img = go.AddComponent<Image>();
        }

        img.sprite = sprite;
        img.color = color;
        img.type = type;
        img.pixelsPerUnitMultiplier = 1f;
    }

    private static void SetTextMeshColor(Transform parent, string childName, Color color)
    {
        Transform target = FindRecursive(parent, childName);
        if (target != null)
        {
            var tmp = target.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color = color;
            }
        }
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindRecursive(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }
}
#endif
