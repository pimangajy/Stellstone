using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 시작 지점부터 불꽃이 둥글게 원형으로 퍼져나가는 화염 폭발 파티클을 원클릭으로 생성하는 에디터 툴입니다.
/// 상단 메뉴 [Tools] -> [Stellar Duel] -> [Generate Circular Flame Burst Effect]를 통해 실행할 수 있습니다.
/// </summary>
public class CircularFlameBurstGenerator : Editor
{
    private const string TEXTURE_DIR = "Assets/Resources/Effects/Textures";
    private const string MATERIAL_DIR = "Assets/Resources/Effects/Materials";
    private const string PREFAB_DIR = "Assets/Prefab/Effects";
    private const string PREFAB_PATH = PREFAB_DIR + "/Circular_Flame_Burst.prefab";

    [MenuItem("Tools/Stellar Duel/Generate Circular Flame Burst Effect")]
    public static void GenerateEffect()
    {
        // 1. 디렉토리 준비
        EnsureDirectories();

        // 2. 화염 전용 절차적 텍스처 생성 (PNG)
        Texture2D texCoreGlow = CreateOrLoadTexture("tex_flame_core.png", GenerateCoreGlowTexture(256, 256));
        Texture2D texFlameRing = CreateOrLoadTexture("tex_flame_ring.png", GenerateFlameRingTexture(512, 512));
        Texture2D texFlameTongue = CreateOrLoadTexture("tex_flame_tongue.png", GenerateFlameTongueTexture(256, 256));
        Texture2D texEmber = CreateOrLoadTexture("tex_ember_spark.png", GenerateEmberSparkTexture(128, 128));
        Texture2D texSmoke = CreateOrLoadTexture("tex_smoke_puff.png", GenerateSmokeTexture(256, 256));

        AssetDatabase.Refresh();

        // 3. 머티리얼 생성 (Additive 4종 + AlphaBlend 연기 1종)
        Shader particleShader = FindParticleShader();
        Material matCore = CreateOrUpdateMaterial("mat_flame_core.mat", particleShader, texCoreGlow, isAdditive: true);
        Material matRing = CreateOrUpdateMaterial("mat_flame_ring.mat", particleShader, texFlameRing, isAdditive: true);
        Material matTongue = CreateOrUpdateMaterial("mat_flame_tongue.mat", particleShader, texFlameTongue, isAdditive: true);
        Material matEmber = CreateOrUpdateMaterial("mat_ember_spark.mat", particleShader, texEmber, isAdditive: true);
        Material matSmoke = CreateOrUpdateMaterial("mat_smoke_puff.mat", particleShader, texSmoke, isAdditive: false);

        // 4. 파티클 프리팹 계층 조립
        GameObject rootGo = new GameObject("Circular_Flame_Burst");
        rootGo.transform.position = new Vector3(0f, 4.05f, 6f); // 상대 필드 중앙 기본값

        // 루트 총괄 파티클 시스템
        ParticleSystem rootPs = rootGo.AddComponent<ParticleSystem>();
        var rootMain = rootPs.main;
        rootMain.duration = 1.2f;
        rootMain.loop = false;
        rootMain.playOnAwake = true;
        rootMain.startLifetime = 0.01f;
        rootMain.startSpeed = 0f;
        rootMain.startSize = 0f;
        var rootEmission = rootPs.emission;
        rootEmission.enabled = false;
        var rootRenderer = rootGo.GetComponent<ParticleSystemRenderer>();
        rootRenderer.enabled = false;

        // [레이어 1] 중앙 화염 코어 폭발
        CreateCentralCoreLayer(rootGo.transform, matCore);

        // [레이어 2] 바닥에 밀착하여 둥글게 퍼져나가는 원형 화염 링 충격파
        CreateRadialFireRingLayer(rootGo.transform, matRing);

        // [레이어 3] 360도로 방사되는 역동적인 불꽃 줄기
        CreateFlameTonguesLayer(rootGo.transform, matTongue);

        // [레이어 4] 사방으로 튀어나가 떠오르는 불티 & 스파크
        CreateEmberSparksLayer(rootGo.transform, matEmber);

        // [레이어 5] 화염 소멸 후 남는 은은한 연기 잔상
        CreateSmokeLayer(rootGo.transform, matSmoke);

        // 5. 프리팹 저장
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PREFAB_PATH);
        DestroyImmediate(rootGo);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 6. 완료 알림
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        EditorUtility.DisplayDialog(
            "불꽃 파티클 생성 완료",
            $"원형 화염 방사 이펙트가 성공적으로 생성되었습니다!\n\n경로: {PREFAB_PATH}\n\nTestMap 씬에 드래그하여 재생해 보실 수 있습니다.",
            "확인"
        );
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(TEXTURE_DIR)) Directory.CreateDirectory(TEXTURE_DIR);
        if (!Directory.Exists(MATERIAL_DIR)) Directory.CreateDirectory(MATERIAL_DIR);
        if (!Directory.Exists(PREFAB_DIR)) Directory.CreateDirectory(PREFAB_DIR);
    }

    #region 파티클 레이어 생성 (옵션 A 컬러 팔레트: 골드 -> 오렌지 -> 크림슨 레드)

    // [레이어 1] 중앙 화염 폭발 코어
    private static void CreateCentralCoreLayer(Transform parent, Material mat)
    {
        GameObject go = new GameObject("01_CentralFireCore");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.35f;
        main.startSpeed = 0f;
        main.startSize = 5.0f;
        main.startColor = new Color(1f, 0.95f, 0.65f, 1f); // 밝은 황금빛 백색

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        // 크기 변화: 팽창
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.25f);
        curve.AddKey(1f, 1.1f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // 색상 변화: 백황색 -> 진한 오렌지 -> 레드
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.8f), 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0.05f), 0.4f),
                new GradientColorKey(new Color(0.9f, 0.1f, 0.02f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.2f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        renderer.sharedMaterial = mat;
    }

    // [레이어 2] 바닥에 밀착하여 둥글게 퍼져나가는 원형 화염 링 파동
    private static void CreateRadialFireRingLayer(Transform parent, Material mat)
    {
        GameObject go = new GameObject("02_RadialFireRing");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.02f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.8f;
        main.loop = false;
        main.startLifetime = 0.65f;
        main.startSpeed = 0f;
        main.startSize = 3.5f; // 시작 직경
        main.startColor = new Color(1f, 0.75f, 0.2f, 1f);

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.02f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        // 크기 변화: 원형으로 5배 이상 급격히 팽창 (지름 약 18 유닛까지 커짐)
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.8f);
        sizeCurve.AddKey(0.4f, 3.2f);
        sizeCurve.AddKey(1f, 5.2f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 색상 변화: 중심 밝은 주황 -> 선명한 불꽃 레드 -> 페이드아웃
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0f),
                new GradientColorKey(new Color(1f, 0.4f, 0.05f), 0.5f),
                new GradientColorKey(new Color(0.85f, 0.08f, 0.01f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.4f, 0f),
                new GradientAlphaKey(0.95f, 0.25f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        renderer.sharedMaterial = mat;
    }

    // [레이어 3] 360도로 방사되는 불꽃 줄기 파편
    private static void CreateFlameTonguesLayer(Transform parent, Material mat)
    {
        GameObject go = new GameObject("03_FlameTongues");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.03f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.7f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 0.6f, 0.1f, 1f);

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.04f, 22) });

        // 수평 원형(Circle)으로 360도 전방향 방사
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.6f;
        shape.radiusThickness = 0.3f;
        shape.rotation = new Vector3(90f, 0f, 0f); // XZ 수평 평면

        // 속도 감속 (Dampen)
        var limitVel = ps.limitVelocityOverLifetime;
        limitVel.enabled = true;
        limitVel.dampen = 0.45f;
        limitVel.limit = 1.0f;

        // 크기 변화
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.5f);
        sizeCurve.AddKey(0.25f, 1.2f);
        sizeCurve.AddKey(1f, 0f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 회전
        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        // 색상
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f),
                new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.5f),
                new GradientColorKey(new Color(0.7f, 0.05f, 0.02f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.3f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        renderer.sharedMaterial = mat;
    }

    // [레이어 4] 사방으로 튀어나가 떠오르는 불티 & 스파크
    private static void CreateEmberSparksLayer(Transform parent, Material mat)
    {
        GameObject go = new GameObject("04_EmberSparks");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.1f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.0f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 0.95f, 0.7f, 1f);

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.05f, 35) });

        // 반구(Hemisphere) 형태로 사방 및 약간 위로 방출
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.5f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // 위쪽을 향하도록 회전

        // 감속 및 열기로 인한 상승
        var limitVel = ps.limitVelocityOverLifetime;
        limitVel.enabled = true;
        limitVel.dampen = 0.4f;
        limitVel.limit = 1.5f;

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.y = new ParticleSystem.MinMaxCurve(1.2f); // 공중으로 피어오름

        // 크기 소멸
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.4f);
        sizeCurve.AddKey(0.2f, 1.0f);
        sizeCurve.AddKey(1f, 0.0f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 색상: 백황색 -> 밝은 주황 -> 암적색
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.8f), 0f),
                new GradientColorKey(new Color(1f, 0.6f, 0.1f), 0.5f),
                new GradientColorKey(new Color(0.9f, 0.15f, 0.02f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(0.8f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = mat;
    }

    // [레이어 5] 화염 소멸 후 남는 은은한 연기 잔상
    private static void CreateSmokeLayer(Transform parent, Material mat)
    {
        GameObject go = new GameObject("05_SmokeDissolve");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.15f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(2.0f, 3.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.25f, 0.18f, 0.18f, 0.45f); // 어두운 잿빛 연기

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.12f, 14) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.8f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        // 연기 회전 및 위로 피어오름
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.y = new ParticleSystem.MinMaxCurve(0.8f);

        var rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-1f, 1f);

        // 연기가 퍼지며 서서히 커짐
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.6f);
        sizeCurve.AddKey(1f, 2.2f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 페이드아웃
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.5f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            }
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

    private static Material CreateOrUpdateMaterial(string fileName, Shader shader, Texture2D tex, bool isAdditive)
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

        // URP Particle Unlit 투명 렌더링 설정 필수 태그 및 키워드
        mat.SetFloat("_Surface", 1.0f); // 1 = Transparent
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        if (isAdditive)
        {
            mat.SetFloat("_Blend", 1.0f); // Additive
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.DisableKeyword("_BLENDMODE_ALPHA");
        }
        else
        {
            mat.SetFloat("_Blend", 0.0f); // Alpha Blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_BLENDMODE_ALPHA");
            mat.DisableKeyword("_BLENDMODE_ADD");
        }

        mat.DisableKeyword("_ALPHATEST_ON");
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

    #region 절차적 화염 텍스처 생성기 (PNG)

    // 원형 코어 글로우
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
                alpha = Mathf.Pow(alpha, 2.0f);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex.EncodeToPNG();
    }

    // 이글거리는 원형 화염 링 텍스처
    private static byte[] GenerateFlameRingTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float maxR = width * 0.5f;
        float ringCenterR = maxR * 0.62f;
        float ringThickness = maxR * 0.25f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);
                float angle = Mathf.Atan2(pos.y - center.y, pos.x - center.x);

                // 각도 기반 불꽃 톱니 노이즈 합성
                float flameNoise = Mathf.Sin(angle * 12f) * 0.05f + Mathf.Sin(angle * 24f) * 0.03f + Mathf.Cos(angle * 36f) * 0.02f;
                float effectiveDist = dist + flameNoise * maxR;

                // 도넛 링 거리 계산
                float ringDist = Mathf.Abs(effectiveDist - ringCenterR) / ringThickness;
                float alpha = Mathf.Clamp01(1f - ringDist);
                alpha = Mathf.Pow(alpha, 1.8f);

                // 외곽 사각형 모서리를 원천 차단하는 완벽한 원형 페이드 마스크
                float outerMask = Mathf.Clamp01((maxR * 0.92f - dist) / (maxR * 0.15f));
                // 중심부 구멍도 깔끔하게 뚫리도록 내부 마스크
                float innerMask = Mathf.Clamp01((dist - maxR * 0.25f) / (maxR * 0.15f));

                alpha *= outerMask * innerMask;

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex.EncodeToPNG();
    }

    // 불꽃 혀 (물방울/화염 파편)
    private static byte[] GenerateFlameTongueTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
        {
            float ny = (float)y / (height - 1); // 0 (아래) ~ 1 (위)
            // 위로 갈수록 뾰족해지는 폭
            float halfWidthAtY = Mathf.Sin(ny * Mathf.PI * 0.8f) * 0.5f;

            for (int x = 0; x < width; x++)
            {
                float nx = (float)x / (width - 1);
                float xDist = Mathf.Abs(nx - 0.5f);

                float alpha = 0f;
                if (halfWidthAtY > 0.001f)
                {
                    float factor = Mathf.Clamp01(1f - (xDist / halfWidthAtY));
                    alpha = Mathf.Pow(factor, 1.5f) * Mathf.Sin(ny * Mathf.PI);
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex.EncodeToPNG();
    }

    // 날카로운 불티/스파크
    private static byte[] GenerateEmberSparkTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float radius = width * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x - center.x) / radius;
                float dy = Mathf.Abs(y - center.y) / radius;

                // 마름모/원형 혼합의 강렬한 스파크
                float diamond = Mathf.Clamp01(1f - (dx + dy));
                float circle = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), center) / radius);

                float alpha = Mathf.Pow(Mathf.Max(diamond, circle), 2.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex.EncodeToPNG();
    }

    // 부드러운 뭉게구름형 연기
    private static byte[] GenerateSmokeTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float radius = width * 0.45f;

        // 3개의 서브 센터를 블렌딩하여 비대칭 유기적 구름 형태 생성
        Vector2 c1 = center + new Vector2(width * 0.08f, height * 0.05f);
        Vector2 c2 = center + new Vector2(-width * 0.1f, -height * 0.06f);
        Vector2 c3 = center + new Vector2(-width * 0.04f, height * 0.1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 p = new Vector2(x, y);
                float d0 = Mathf.Clamp01(1f - Vector2.Distance(p, center) / radius);
                float d1 = Mathf.Clamp01(1f - Vector2.Distance(p, c1) / (radius * 0.8f));
                float d2 = Mathf.Clamp01(1f - Vector2.Distance(p, c2) / (radius * 0.75f));
                float d3 = Mathf.Clamp01(1f - Vector2.Distance(p, c3) / (radius * 0.7f));

                float combined = (d0 * 0.4f + d1 * 0.25f + d2 * 0.2f + d3 * 0.15f);
                float alpha = Mathf.SmoothStep(0f, 1f, combined);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex.EncodeToPNG();
    }

    #endregion
}
