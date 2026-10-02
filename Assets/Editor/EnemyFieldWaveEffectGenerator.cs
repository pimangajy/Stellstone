using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 상대 필드 중앙에서 좌우로 퍼져나가는 빛의 파동 이펙트를 원클릭으로 생성하는 에디터 툴입니다.
/// 상단 메뉴 [Tools] -> [Stellar Duel] -> [Generate Enemy Field Wave Effect]를 통해 실행할 수 있습니다.
/// </summary>
public class EnemyFieldWaveEffectGenerator : Editor
{
    private const string TEXTURE_DIR = "Assets/Resources/Effects/Textures";
    private const string MATERIAL_DIR = "Assets/Resources/Effects/Materials";
    private const string PREFAB_DIR = "Assets/Prefab/Effects";
    private const string PREFAB_PATH = PREFAB_DIR + "/EnemyField_LightWave.prefab";

    [MenuItem("Tools/Stellar Duel/Generate Enemy Field Wave Effect")]
    public static void GenerateEffect()
    {
        // 1. 디렉토리 생성
        EnsureDirectories();

        // 2. 텍스처 4종 절차적 생성
        Texture2D texCoreGlow = CreateOrLoadTexture("tex_wave_core_glow.png", GenerateCoreGlowTexture(256, 256));
        Texture2D texHorizontalWave = CreateOrLoadTexture("tex_horizontal_wave.png", GenerateHorizontalWaveTexture(512, 128));
        Texture2D texScanLine = CreateOrLoadTexture("tex_wave_line.png", GenerateScanLineTexture(512, 64));
        Texture2D texSparkle = CreateOrLoadTexture("tex_wave_sparkle.png", GenerateSparkleTexture(128, 128));

        AssetDatabase.Refresh();

        // 3. URP Particles/Unlit Additive 머티리얼 생성
        Shader particleShader = FindParticleShader();
        Material matCoreGlow = CreateOrUpdateMaterial("mat_wave_core_glow.mat", particleShader, texCoreGlow);
        Material matHorizontalWave = CreateOrUpdateMaterial("mat_horizontal_wave.mat", particleShader, texHorizontalWave);
        Material matScanLine = CreateOrUpdateMaterial("mat_wave_line.mat", particleShader, texScanLine);
        Material matSparkle = CreateOrUpdateMaterial("mat_wave_sparkle.mat", particleShader, texSparkle);

        // 4. 파티클 프리팹 오브젝트 조립
        GameObject rootGo = new GameObject("EnemyField_LightWave");
        // 기본 위치를 상대 필드 중앙 (X:0, Y:4.05, Z:6)으로 세팅
        rootGo.transform.position = new Vector3(0f, 4.05f, 6f);

        // 메인 루트 파티클 시스템 (하위 파티클 총괄 제어기)
        ParticleSystem rootPs = rootGo.AddComponent<ParticleSystem>();
        var rootMain = rootPs.main;
        rootMain.duration = 1.0f;
        rootMain.loop = false;
        rootMain.playOnAwake = true;
        rootMain.startLifetime = 0.01f;
        rootMain.startSpeed = 0f;
        rootMain.startSize = 0f;
        var rootEmission = rootPs.emission;
        rootEmission.enabled = false;
        var rootRenderer = rootGo.GetComponent<ParticleSystemRenderer>();
        rootRenderer.enabled = false;

        // 레이어 1: CenterCoreFlash (중앙 순간 섬광)
        CreateCoreFlashLayer(rootGo.transform, matCoreGlow);

        // 레이어 2: HorizontalWaveSweep (좌우로 넓게 퍼지는 메인 광파동)
        CreateHorizontalWaveLayer(rootGo.transform, matHorizontalWave);

        // 레이어 3: GroundScanBeam (바닥 수평 스캔 네온 빔)
        CreateGroundScanLineLayer(rootGo.transform, matScanLine);

        // 레이어 4: StellarSparkles (좌우 영역에 흩날리는 별빛 입자)
        CreateSparkleLayer(rootGo.transform, matSparkle);

        // 5. 프리팹으로 저장
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PREFAB_PATH);
        DestroyImmediate(rootGo);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 6. 결과 알림 및 프로젝트 뷰 포커스
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        EditorUtility.DisplayDialog(
            "이펙트 생성 완료",
            $"빛의 파동 이펙트가 성공적으로 생성되었습니다!\n\n경로: {PREFAB_PATH}\n\nTestMap 씬에 드래그하여 바로 확인하실 수 있습니다.",
            "확인"
        );
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(TEXTURE_DIR)) Directory.CreateDirectory(TEXTURE_DIR);
        if (!Directory.Exists(MATERIAL_DIR)) Directory.CreateDirectory(MATERIAL_DIR);
        if (!Directory.Exists(PREFAB_DIR)) Directory.CreateDirectory(PREFAB_DIR);
    }

    #region 파티클 레이어 조립

    // [레이어 1] 중앙 코어 섬광
    private static void CreateCoreFlashLayer(Transform parent, Material mat)
    {
        GameObject go = new GameObject("01_CoreFlash");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.35f;
        main.startSpeed = 0f;
        main.startSize = 6.5f;
        main.startColor = new Color(0.25f, 0.85f, 1f, 1f); // 밝은 시안 블루

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        // 크기 변화: 0.3에서 1.0으로 팽창
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.3f);
        sizeCurve.AddKey(1f, 1.0f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 색상/알파: 순간적으로 번쩍인 후 페이드아웃
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.2f, 0.8f, 1f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        renderer.sharedMaterial = mat;
    }

    // [레이어 2] 좌우 수평 빛의 파동 (메인)
    private static void CreateHorizontalWaveLayer(Transform parent, Material mat)
    {
        GameObject go = new GameObject("02_HorizontalWave");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.8f;
        main.loop = false;
        main.startLifetime = 0.65f;
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = 6.0f; // 가로 시작 너비
        main.startSizeY = 2.5f; // 세로 두께
        main.startSizeZ = 1f;
        main.startColor = new Color(0.3f, 0.7f, 1f, 0.95f);

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.04f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        // 3D 크기 변화: 가로(X축)로 4배 이상 급격히 팽창하여 좌우로 퍼짐
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.separateAxes = true;

        AnimationCurve xCurve = new AnimationCurve();
        xCurve.AddKey(0f, 0.8f);
        xCurve.AddKey(0.4f, 2.5f);
        xCurve.AddKey(1f, 4.2f); // 6.0 * 4.2 = 약 25 유닛 (상대 필드 전체 폭을 충분히 커버)
        sol.x = new ParticleSystem.MinMaxCurve(1f, xCurve);

        AnimationCurve yCurve = new AnimationCurve();
        yCurve.AddKey(0f, 0.8f);
        yCurve.AddKey(0.5f, 1.4f);
        yCurve.AddKey(1f, 1.8f);
        sol.y = new ParticleSystem.MinMaxCurve(1f, yCurve);

        // 알파 페이드아웃
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.4f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.6f, 0.4f, 1f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.2f, 0f), new GradientAlphaKey(0.95f, 0.2f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        renderer.sharedMaterial = mat;
    }

    // [레이어 3] 바닥 네온 스캔 빔 라인
    private static void CreateGroundScanLineLayer(Transform parent, Material mat)
    {
        GameObject go = new GameObject("03_GroundScanLine");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.02f, 0f); // 살짝 위에 띄움

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.35f;
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = 16f; // 가로 슬롯 전체 라인
        main.startSizeY = 0.9f;
        main.startSizeZ = 1f;
        main.startColor = new Color(0.75f, 0.45f, 1f, 1f); // 네온 퍼플

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.06f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        // 세로는 얇아지면서 가로는 늘어남
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.separateAxes = true;

        AnimationCurve xCurve = new AnimationCurve();
        xCurve.AddKey(0f, 0.4f);
        xCurve.AddKey(1f, 1.4f);
        sol.x = new ParticleSystem.MinMaxCurve(1f, xCurve);

        AnimationCurve yCurve = new AnimationCurve();
        yCurve.AddKey(0f, 1.2f);
        yCurve.AddKey(1f, 0.1f);
        sol.y = new ParticleSystem.MinMaxCurve(1f, yCurve);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.4f, 1f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        renderer.sharedMaterial = mat;
    }

    // [레이어 4] 흩날리는 스텔라 별빛 입자
    private static void CreateSparkleLayer(Transform parent, Material mat)
    {
        GameObject go = new GameObject("04_StellarSparkles");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.1f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.0f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.9f, 0.95f, 1f, 1f);

        var emission = ps.emission;
        emission.rateOverTime = 0;
        // 파동이 지나갈 때 터지는 별빛들
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.08f, 25) });

        // 가로로 긴 박스 형태 범위에서 방출
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(16f, 0.2f, 2.5f);

        // 살짝 위로 피어오름
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.y = new ParticleSystem.MinMaxCurve(0.8f);

        // 서서히 회전
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        // 크기: 시작 시 확대되었다가 서서히 소멸
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.2f);
        sizeCurve.AddKey(0.2f, 1.0f);
        sizeCurve.AddKey(1f, 0.0f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 알파: 깜빡임 후 소멸
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.3f, 0.85f, 1f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = mat;
    }

    #endregion

    #region 머티리얼 및 텍스처 헬퍼

    private static Shader FindParticleShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Mobile/Particles/Additive");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
        return shader;
    }

    private static Material CreateOrUpdateMaterial(string fileName, Shader shader, Texture2D tex)
    {
        string path = Path.Combine(MATERIAL_DIR, fileName).Replace("\\", "/");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        // URP Particle Unlit 투명 Additive 블렌드 세팅
        mat.SetFloat("_Surface", 1.0f); // Transparent
        mat.SetFloat("_Blend", 1.0f);   // Additive
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_BLENDMODE_ADD");
        mat.DisableKeyword("_BLENDMODE_ALPHA");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", Color.white);

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Texture2D CreateOrLoadTexture(string fileName, byte[] pngBytes)
    {
        string path = Path.Combine(TEXTURE_DIR, fileName).Replace("\\", "/");
        File.WriteAllBytes(path, pngBytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    #endregion

    #region 절차적 텍스처 데이터 생성기 (PNG)

    // 원형 코어 소프트 글로우
    private static byte[] GenerateCoreGlowTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float radius = width * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = Mathf.Pow(alpha, 2.2f); // 부드러운 감쇄

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex.EncodeToPNG();
    }

    // 좌우 수평 파동 리본
    private static byte[] GenerateHorizontalWaveTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
        {
            float ny = (float)y / (height - 1); // 0 ~ 1
            float yFactor = Mathf.Sin(ny * Mathf.PI); // 중심(0.5)에서 최대
            yFactor = Mathf.Pow(yFactor, 1.8f);

            for (int x = 0; x < width; x++)
            {
                float nx = (float)x / (width - 1); // 0 ~ 1
                // 가로 중심에서 양 끝으로 갈수록 페이드
                float xFactor = Mathf.Sin(nx * Mathf.PI);

                float alpha = Mathf.Clamp01(xFactor * yFactor);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex.EncodeToPNG();
    }

    // 네온 스캔 빔 라인
    private static byte[] GenerateScanLineTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
        {
            float ny = (float)y / (height - 1);
            float yDist = Mathf.Abs(ny - 0.5f) * 2f; // 0 (중심) ~ 1 (끝)
            float yFactor = Mathf.Clamp01(1f - yDist);
            yFactor = Mathf.Pow(yFactor, 4.0f); // 얇고 날카로운 중앙 선

            for (int x = 0; x < width; x++)
            {
                float nx = (float)x / (width - 1);
                float xFactor = Mathf.Sin(nx * Mathf.PI); // 양 끝 페이드아웃

                float alpha = Mathf.Clamp01(yFactor * Mathf.Pow(xFactor, 0.5f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex.EncodeToPNG();
    }

    // 십자 별빛 스파클
    private static byte[] GenerateSparkleTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float halfW = width * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x - center.x) / halfW;
                float dy = Mathf.Abs(y - center.y) / halfW;

                // 십자 빔 계산
                float beamX = Mathf.Pow(Mathf.Clamp01(1f - dx), 1.5f) * Mathf.Pow(Mathf.Clamp01(1f - dy * 5f), 2f);
                float beamY = Mathf.Pow(Mathf.Clamp01(1f - dy), 1.5f) * Mathf.Pow(Mathf.Clamp01(1f - dx * 5f), 2f);

                // 중앙 구형 코어
                float dist = Vector2.Distance(new Vector2(x, y), center) / halfW;
                float core = Mathf.Pow(Mathf.Clamp01(1f - dist * 2f), 2f);

                float alpha = Mathf.Clamp01(beamX + beamY + core);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex.EncodeToPNG();
    }

    #endregion
}
