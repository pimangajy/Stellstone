#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CardPackAlignmentFix
{
    private const string PREFAB_PATH = "Assets/Prefab/Storage/Craft/CardPack.prefab";
    private const string SCENE_PATH = "Assets/Scenes/Storage.unity";

    [MenuItem("StellarDuel/Fix CardPack Alignment and Lie Down")]
    public static void FixAlignmentAndLieDown()
    {
        // 1. 프리팹 에셋 로드 및 상자/뚜껑 좌표 원본 기준 일치
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        if (prefabRoot == null)
        {
            Debug.LogError($"[CardPackAlignmentFix] ❌ 프리팹을 불러올 수 없습니다: {PREFAB_PATH}");
            return;
        }

        try
        {
            Transform cubeT = prefabRoot.transform.Find("Cube");
            Transform cube001T = prefabRoot.transform.Find("Cube.001");
            Transform textT = prefabRoot.transform.Find("Text (TMP)");

            // A. Cube(상자 바디)를 원점(0, 0, 0) 및 (270, 0, 0)으로 복원
            if (cubeT != null)
            {
                cubeT.localPosition = Vector3.zero;
                cubeT.localEulerAngles = new Vector3(270f, 0f, 0f);
                cubeT.localScale = new Vector3(100f, 100f, 100f);
            }

            // B. Cube.001(뚜껑)을 애니메이션 키프레임 기준 원점인 (1.5, 4.2, 0)으로 복원
            if (cube001T != null)
            {
                cube001T.localPosition = new Vector3(1.5f, 4.2f, 0f);
                cube001T.localEulerAngles = new Vector3(270f, 0f, 0f);
                cube001T.localScale = new Vector3(100f, 100f, 100f);
            }

            // C. Text (TMP) 수량 텍스트 위치 정렬
            if (textT != null)
            {
                textT.localPosition = new Vector3(0.75f, 2.1f, -1.2f);
                textT.localEulerAngles = new Vector3(0f, 0f, 0f);
                textT.localScale = new Vector3(0.1f, 0.1f, 0.01f);
            }

            // D. BoxCollider 재계산 (딱 맞물린 Bounds 기준)
            var boxCol = prefabRoot.GetComponent<BoxCollider>();
            if (boxCol != null && cubeT != null && cube001T != null)
            {
                var r1 = cubeT.GetComponent<Renderer>();
                var r2 = cube001T.GetComponent<Renderer>();
                if (r1 != null && r2 != null)
                {
                    Bounds combined = r1.bounds;
                    combined.Encapsulate(r2.bounds);

                    boxCol.center = prefabRoot.transform.InverseTransformPoint(combined.center);
                    Vector3 lossy = prefabRoot.transform.lossyScale;
                    boxCol.size = new Vector3(
                        Mathf.Abs(lossy.x) > 0.001f ? combined.size.x / lossy.x : combined.size.x,
                        Mathf.Abs(lossy.y) > 0.001f ? combined.size.y / lossy.y : combined.size.y,
                        Mathf.Abs(lossy.z) > 0.001f ? combined.size.z / lossy.z : combined.size.z
                    );
                }
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);
            Debug.Log("[CardPackAlignmentFix] ✅ CardPack.prefab 상자/뚜껑 완벽 정렬 완료 (오차 0%)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        // 2. Storage 씬 열기 및 카드팩 눕히기/높이 최적화
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != SCENE_PATH)
        {
            EditorSceneManager.OpenScene(SCENE_PATH);
        }

        CraftManager craftManager = Object.FindFirstObjectByType<CraftManager>();
        if (craftManager != null)
        {
            SerializedObject soCraft = new SerializedObject(craftManager);

            // A. 중앙 포커스 카드팩 회전값: X축 90도 회전을 주어 테이블 위에 수평으로 눕힘
            SerializedProperty propFocusRot = soCraft.FindProperty("focusPackRotation");
            if (propFocusRot != null)
            {
                propFocusRot.vector3Value = new Vector3(90f, 200f, 0f);
            }

            // B. 카드 부채꼴 연출 높이 오프셋: 카드가 카드팩 윗공간에 시원하게 보이도록 상향
            SerializedProperty propHeightOffset = soCraft.FindProperty("heightOffsetFromCamera");
            if (propHeightOffset != null)
            {
                propHeightOffset.floatValue = 0.05f; // 기존 -0.15f에서 위쪽으로 올려 카드팩에 가려지지 않음
            }

            // C. 카메라 전방 거리 최적화
            SerializedProperty propDist = soCraft.FindProperty("distanceFromCamera");
            if (propDist != null)
            {
                propDist.floatValue = 0.65f; // 너무 코앞(0.1m)이 아닌 시원한 시야 거리로 최적화
            }

            soCraft.ApplyModifiedProperties();
            EditorUtility.SetDirty(craftManager);

            // D. CardPackPos (focusSpot) 위치: 테이블 높이에 맞춰 살짝 낮춤
            GameObject packPosGo = GameObject.Find("CardPackPos");
            if (packPosGo != null)
            {
                Undo.RecordObject(packPosGo.transform, "Adjust CardPackPos Height");
                Vector3 currentPos = packPosGo.transform.position;
                packPosGo.transform.position = new Vector3(currentPos.x, currentPos.y - 0.2f, currentPos.z);
                EditorUtility.SetDirty(packPosGo);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[CardPackAlignmentFix] ✨ Storage 씬 카드팩 눕히기(focusPackRotation) 및 높이 최적화 적용 완료!");
        }
    }
}
#endif
