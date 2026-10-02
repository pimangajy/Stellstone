using UnityEditor;
using UnityEngine;

public static class SleepZPrefabSetup
{
    [MenuItem("Tools/Setup SleepZZZ on Minion Prefab")]
    public static string SetupMinionPrefab()
    {
        string prefabPath = "Assets/Prefab/Battle/Minion.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null) return "Failed to load prefab";

        try
        {
            // 1. SleepZZZ root under Minion
            Transform sleepTr = root.transform.Find("SleepZZZ");
            GameObject sleepObj;
            if (sleepTr == null)
            {
                sleepObj = new GameObject("SleepZZZ");
                sleepObj.transform.SetParent(root.transform, false);
            }
            else
            {
                sleepObj = sleepTr.gameObject;
            }

            // 하수인 카드 상단 우측 위치
            sleepObj.transform.localPosition = new Vector3(0.5f, 0.4f, 0.5f);
            sleepObj.transform.localRotation = Quaternion.identity;
            sleepObj.transform.localScale = Vector3.one;

            // 2. ZSpawner under SleepZZZ
            Transform spawnerTr = sleepObj.transform.Find("ZSpawner");
            GameObject spawnerObj;
            if (spawnerTr == null)
            {
                spawnerObj = new GameObject("ZSpawner");
                spawnerObj.transform.SetParent(sleepObj.transform, false);
            }
            else
            {
                spawnerObj = spawnerTr.gameObject;
            }
            spawnerObj.transform.localPosition = Vector3.zero;
            spawnerObj.transform.localRotation = Quaternion.identity;
            spawnerObj.transform.localScale = Vector3.one;

            // 3. Z_Prefab under SleepZZZ
            Transform zPrefabTr = sleepObj.transform.Find("Z_Prefab");
            GameObject zPrefabObj;
            if (zPrefabTr == null)
            {
                zPrefabObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                zPrefabObj.name = "Z_Prefab";
                zPrefabObj.transform.SetParent(sleepObj.transform, false);
            }
            else
            {
                zPrefabObj = zPrefabTr.gameObject;
            }

            // 불필요한 콜라이더 제거 (레이캐스트 방해 방지)
            Collider col = zPrefabObj.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            zPrefabObj.transform.localPosition = Vector3.zero;
            zPrefabObj.transform.localRotation = Quaternion.Euler(30f, 45f, 15f);
            zPrefabObj.transform.localScale = Vector3.one * 0.2f;

            // ZAnimationParticle 컴포넌트 추가 및 바인딩
            var particle = zPrefabObj.GetComponent<ZAnimationParticle>();
            if (particle == null) particle = zPrefabObj.AddComponent<ZAnimationParticle>();
            particle.meshFilter = zPrefabObj.GetComponent<MeshFilter>();
            particle.meshRenderer = zPrefabObj.GetComponent<MeshRenderer>();

            // 머티리얼 적용
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Battle/highlights.mat");
            if (mat != null && particle.meshRenderer != null)
            {
                particle.meshRenderer.sharedMaterial = mat;
            }

            zPrefabObj.SetActive(false);

            // 4. SleepZSpawner 컴포넌트 설정
            var spawner = sleepObj.GetComponent<SleepZSpawner>();
            if (spawner == null) spawner = sleepObj.AddComponent<SleepZSpawner>();
            spawner.spawnPoint = spawnerObj.transform;
            spawner.zPrefab = zPrefabObj;
            spawner.moveDirection = new Vector3(0.4f, 1.2f, -0.2f);
            spawner.moveDistance = 1.3f;
            spawner.moveDuration = 1.2f;
            spawner.spawnInterval = 0.65f;
            spawner.startScale = Vector3.one * 0.15f;
            spawner.endScale = Vector3.one * 0.4f;

            sleepObj.SetActive(false);

            // 5. FieldCardDisplay의 cannotAttackObject에 연결
            var fieldDisplay = root.GetComponent<FieldCardDisplay>();
            if (fieldDisplay != null)
            {
                fieldDisplay.cannotAttackObject = sleepObj;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return "SUCCESS";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Verify SleepZZZ on Minion Prefab")]
    public static string Verify()
    {
        string prefabPath = "Assets/Prefab/Battle/Minion.prefab";
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (go == null) return "Prefab not found";

        var fcd = go.GetComponent<FieldCardDisplay>();
        var spawner = go.GetComponentInChildren<SleepZSpawner>(true);
        var zPrefab = spawner != null ? spawner.zPrefab : null;
        var zParticle = zPrefab != null ? zPrefab.GetComponent<ZAnimationParticle>() : null;

        return string.Format("FCD cannotAttackObject: {0} | Spawner: {1} | Z_Prefab: {2} | ZParticle: {3}",
            fcd != null && fcd.cannotAttackObject != null ? fcd.cannotAttackObject.name : "null",
            spawner != null ? spawner.name : "null",
            zPrefab != null ? zPrefab.name : "null",
            zParticle != null ? zParticle.name : "null");
    }
}
