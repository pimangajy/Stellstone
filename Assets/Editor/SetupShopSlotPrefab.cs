#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class SetupShopSlotPrefab
{
    [MenuItem("Tools/Shop/Setup Slot Prefab Price UI")]
    public static void Execute()
    {
        string prefabPath = "Assets/Prefab/UI/Shop/Slot.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        if (prefabRoot == null)
        {
            Debug.LogError($"[SetupShopSlotPrefab] 프리팹을 로드할 수 없습니다: {prefabPath}");
            return;
        }

        try
        {
            ShopSlotUI slotUI = prefabRoot.GetComponent<ShopSlotUI>();
            if (slotUI == null)
            {
                Debug.LogError("[SetupShopSlotPrefab] ShopSlotUI 컴포넌트를 찾을 수 없습니다.");
                return;
            }

            // 1. 기존 PriceGroup이 있으면 찾고, 없으면 신규 생성
            Transform priceGroupTr = prefabRoot.transform.Find("PriceGroup");
            GameObject priceGroupGo;

            if (priceGroupTr != null)
            {
                priceGroupGo = priceGroupTr.gameObject;
            }
            else
            {
                priceGroupGo = new GameObject("PriceGroup");
                priceGroupGo.transform.SetParent(prefabRoot.transform, false);
            }

            RectTransform priceGroupRect = priceGroupGo.GetComponent<RectTransform>();
            if (priceGroupRect == null) priceGroupRect = priceGroupGo.AddComponent<RectTransform>();

            // 하단 중앙 배치
            priceGroupRect.anchorMin = new Vector2(0f, 0f);
            priceGroupRect.anchorMax = new Vector2(1f, 0f);
            priceGroupRect.pivot = new Vector2(0.5f, 0f);
            priceGroupRect.anchoredPosition = new Vector2(0f, 16f);
            priceGroupRect.sizeDelta = new Vector2(-40f, 36f);

            HorizontalLayoutGroup layoutGroup = priceGroupGo.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup == null) layoutGroup = priceGroupGo.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.spacing = 8f;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childScaleWidth = false;
            layoutGroup.childScaleHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            // 2. CurrencyIcon (재화 아이콘 Image)
            Transform iconTr = priceGroupGo.transform.Find("CurrencyIcon");
            GameObject iconGo;
            if (iconTr != null)
            {
                iconGo = iconTr.gameObject;
            }
            else
            {
                iconGo = new GameObject("CurrencyIcon");
                iconGo.transform.SetParent(priceGroupGo.transform, false);
            }

            RectTransform iconRect = iconGo.GetComponent<RectTransform>();
            if (iconRect == null) iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(28f, 28f);

            Image iconImg = iconGo.GetComponent<Image>();
            if (iconImg == null) iconImg = iconGo.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // 3. PriceText (가격 TextMeshProUGUI)
            Transform textTr = priceGroupGo.transform.Find("PriceText");
            GameObject textGo;
            if (textTr != null)
            {
                textGo = textTr.gameObject;
            }
            else
            {
                textGo = new GameObject("PriceText");
                textGo.transform.SetParent(priceGroupGo.transform, false);
            }

            RectTransform textRect = textGo.GetComponent<RectTransform>();
            if (textRect == null) textRect = textGo.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(100f, 32f);

            TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = textGo.AddComponent<TextMeshProUGUI>();

            // 기존 Name 텍스트의 폰트 에셋 복사
            if (slotUI.productNameText != null && slotUI.productNameText.font != null)
            {
                tmp.font = slotUI.productNameText.font;
            }
            tmp.text = "0";
            tmp.fontSize = 20f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = new Color(1f, 0.85f, 0.4f, 1f); // 황금빛 노란색
            tmp.raycastTarget = false;

            // 4. ShopSlotUI 컴포넌트 연결
            slotUI.priceText = tmp;
            slotUI.currencyIconImage = iconImg;
            // 스프라이트는 사용자가 인스펙터에서 직접 넣을 수 있도록 초기 null 유지

            // 5. 프리팹 저장
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            Debug.Log($"[SetupShopSlotPrefab] ✅ Slot 프리팹에 가격 및 재화 UI 구성 완료: {prefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif
