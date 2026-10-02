#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public static class CardPackPrefabUpdater
{
    private const string PREFAB_PATH = "Assets/Prefab/Storage/Craft/CardPack.prefab";
    private const string NOMAL_PREFAB_PATH = "Assets/Prefab/Storage/Craft/CardPack_nomal.prefab";
    private const string SCENE_PATH = "Assets/Scenes/Storage.unity";

    [MenuItem("StellarDuel/Setup and Link New CardPack Prefab")]
    public static void SetupAndLink()
    {
        // 1. 프리팹 에셋 로드
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        if (prefabRoot == null)
        {
            Debug.LogError($"[CardPackPrefabUpdater] ❌ {PREFAB_PATH} 프리팹을 로드할 수 없습니다!");
            return;
        }

        try
        {
            // 2. BoxCollider 설정
            var boxCol = prefabRoot.GetComponent<BoxCollider>();
            if (boxCol == null)
            {
                boxCol = prefabRoot.AddComponent<BoxCollider>();
            }

            // 기존 CardPack_nomal의 Collider 크기 참고 및 자식 렌더러 bounds 계산
            var renderers = prefabRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }

                // 로컬 좌표계로 변환
                boxCol.center = prefabRoot.transform.InverseTransformPoint(combinedBounds.center);
                Vector3 worldSize = combinedBounds.size;
                Vector3 lossyScale = prefabRoot.transform.lossyScale;
                boxCol.size = new Vector3(
                    Mathf.Abs(lossyScale.x) > 0.0001f ? worldSize.x / lossyScale.x : worldSize.x,
                    Mathf.Abs(lossyScale.y) > 0.0001f ? worldSize.y / lossyScale.y : worldSize.y,
                    Mathf.Abs(lossyScale.z) > 0.0001f ? worldSize.z / lossyScale.z : worldSize.z
                );
            }
            else
            {
                boxCol.size = new Vector3(0.94f, 0.2f, 0.71f);
                boxCol.center = new Vector3(0.008f, 0.04f, -0.03f);
            }

            // 3. CardPackItem 컴포넌트 설정
            var packItem = prefabRoot.GetComponent<CardPackItem>();
            if (packItem == null)
            {
                packItem = prefabRoot.AddComponent<CardPackItem>();
            }

            // 4. Cube(바디)와 Cube.001(뚜껑) 렌더러 탐색
            Renderer boxRenderer = null;
            Renderer lidRenderer = null;
            Transform cubeT = prefabRoot.transform.Find("Cube");
            if (cubeT != null) boxRenderer = cubeT.GetComponent<Renderer>();

            Transform cube001T = prefabRoot.transform.Find("Cube.001");
            if (cube001T != null) lidRenderer = cube001T.GetComponent<Renderer>();

            // 5. 수량 표시 Text (TMP) 자식 오브젝트 설정
            Transform textT = prefabRoot.transform.Find("Text (TMP)");
            GameObject textObj = null;
            if (textT == null)
            {
                textObj = new GameObject("Text (TMP)");
                textObj.transform.SetParent(prefabRoot.transform, false);
                textT = textObj.transform;
            }
            else
            {
                textObj = textT.gameObject;
            }

            textT.localPosition = new Vector3(0f, 0.15f, 0.45f);
            textT.localRotation = Quaternion.Euler(90f, -90f, 0f);
            textT.localScale = new Vector3(0.1f, 0.1f, 0.01f);

            var tmp = textObj.GetComponent<TextMeshPro>();
            if (tmp == null)
            {
                tmp = textObj.AddComponent<TextMeshPro>();
            }

            tmp.text = "x1";
            tmp.fontSize = 20f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;

            var rectT = textObj.GetComponent<RectTransform>();
            if (rectT != null)
            {
                rectT.sizeDelta = new Vector2(20f, 5f);
            }

            // 6. CardPackItem 직렬화 프로퍼티 연결
            SerializedObject soItem = new SerializedObject(packItem);
            soItem.FindProperty("boxRenderer").objectReferenceValue = boxRenderer;
            soItem.FindProperty("lidRenderer").objectReferenceValue = lidRenderer;
            soItem.FindProperty("countText").objectReferenceValue = tmp;
            soItem.FindProperty("countFormat").stringValue = "x{0}";
            soItem.FindProperty("hideIfSingle").boolValue = false;
            soItem.ApplyModifiedProperties();

            // 프리팹 저장
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);
            Debug.Log($"[CardPackPrefabUpdater] ✅ {PREFAB_PATH} 프리팹 인터페이스 보강 완료!");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // 7. Storage 씬 열기 및 CraftManager 프리팹 교체
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != SCENE_PATH)
        {
            EditorSceneManager.OpenScene(SCENE_PATH);
        }

        GameObject newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (newPrefab == null)
        {
            Debug.LogError($"[CardPackPrefabUpdater] ❌ 새 프리팹 에셋을 불러올 수 없습니다: {PREFAB_PATH}");
            return;
        }

        CraftManager craftManager = Object.FindFirstObjectByType<CraftManager>();
        if (craftManager == null)
        {
            Debug.LogError("[CardPackPrefabUpdater] ❌ Storage 씬에서 CraftManager를 찾을 수 없습니다!");
            return;
        }

        SerializedObject soCraft = new SerializedObject(craftManager);
        SerializedProperty propPackPrefab = soCraft.FindProperty("cardPackPrefab");
        if (propPackPrefab != null)
        {
            propPackPrefab.objectReferenceValue = newPrefab;
            soCraft.ApplyModifiedProperties();
            EditorUtility.SetDirty(craftManager);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"[CardPackPrefabUpdater] ✨ Storage 씬의 CraftManager.cardPackPrefab이 성공적으로 '{newPrefab.name}'(으)로 교체 연결되었습니다!");
        }
        else
        {
            Debug.LogError("[CardPackPrefabUpdater] ❌ CraftManager에서 cardPackPrefab 프로퍼티를 찾을 수 없습니다!");
        }
    }
}
#endif
