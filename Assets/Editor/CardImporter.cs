using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class CardImporter : EditorWindow
{
    private string csvFolderPath = "Assets/Resources/CSV";
    private string cardAssetPath = "Assets/Resources/CardData";

    [MenuItem("Tools/Import Card Data (CSV)")]
    public static void ShowWindow()
    {
        GetWindow<CardImporter>("Card Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV Card Importer", EditorStyles.boldLabel);
        GUILayout.Space(10);

        csvFolderPath = EditorGUILayout.TextField("CSV Folder Path", csvFolderPath);
        cardAssetPath = EditorGUILayout.TextField("Save Asset Path", cardAssetPath);

        GUILayout.Space(10);

        if (GUILayout.Button("Import All CSVs", GUILayout.Height(40)))
        {
            ImportCards();
        }

        GUILayout.Label("CSV 구조: ID, Class, Name, Cost, Atk, HP...", EditorStyles.miniLabel);
    }

    private void ImportCards()
    {
        if (!Directory.Exists(csvFolderPath))
        {
            Debug.LogError($"CSV 폴더를 찾을 수 없습니다: {csvFolderPath}");
            return;
        }
        if (!Directory.Exists(cardAssetPath))
        {
            Directory.CreateDirectory(cardAssetPath);
        }

        string[] files = Directory.GetFiles(csvFolderPath, "*.csv");
        if (files.Length == 0)
        {
            Debug.LogWarning("해당 폴더에 CSV 파일이 없습니다.");
            return;
        }

        int successCount = 0;
        foreach (string file in files)
        {
            successCount += ParseCSV(file);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"임포트 완료! 총 {successCount}개의 카드가 처리되었습니다.");
    }

    private int ParseCSV(string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length <= 1) return 0;

        int count = 0;

        // 1번 줄부터 데이터 시작 (0번은 헤더)
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] values = SplitCsvLine(line);

            // 데이터 유효성 검사 (ID가 없으면 스킵)
            if (values.Length < 1 || string.IsNullOrEmpty(values[0])) continue;

            // --- 1. 기본 정보 파싱 ---
            // 구조: CardID(0), Class(1), Name(2), Cost(3), Atk(4), HP(5), Rarity(6), Type(7), Exp(8), Desc(9), Effects(10), Tribe(11), Add(12)

            string id = values[0].Trim();

            // [수정됨] CSV의 Class값(Gangzi 등)을 Enum(강지)으로 변환
            ClassType memberType = ParseMemberType(values[1]);

            // [수정됨] 직업별 폴더 경로 설정 (Enum 이름인 '강지', '유니' 폴더로 저장)
            string memberFolderPath = $"{cardAssetPath}/{memberType}";
            if (!Directory.Exists(memberFolderPath))
            {
                Directory.CreateDirectory(memberFolderPath);
            }

            // 에셋 생성/로드
            string assetPath = $"{memberFolderPath}/{id}.asset";
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(assetPath);

            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardData>();
                AssetDatabase.CreateAsset(card, assetPath);
            }

            // --- 2. 데이터 매핑 (인덱스 수정됨) ---
            card.cardID = id;
            card.member = memberType;           // Index 1: Class
            card.cardName = values[2];          // Index 2: Name
            card.manaCost = ParseInt(values[3]); // Index 3: Cost
            card.attack = ParseInt(values[4]);   // Index 4: Attack
            card.health = ParseInt(values[5]);   // Index 5: Health

            card.rarity = ParseEnum<Rarity>(values[6], Rarity.일반);
            card.cardType = ParseCardType(values[7]);
            card.expansion = (values[8] == "기본") ? Expansion.기본 : ParseEnum<Expansion>(values[8], Expansion.기본);

            // 설명 (따옴표 처리)
            card.description = values[9].Replace("\"", "").Replace("\"\"", "\"");

            // 효과
            card.effects = ParseEffects(values[10]);

            card.minionTribe = ParseEnum<MinionTribe>(values[11], MinionTribe.없음);

            // 추가 설명
            if (values.Length > 12)
                card.additionalExplanation = values[12].Replace("\"", "");

            EditorUtility.SetDirty(card);
            count++;
        }
        return count;
    }

    // --- Enum 매핑 도우미 ---

    // [신규] CSV의 영문 Class(Gangzi)를 한글 Enum(강지)으로 매핑
    private ClassType ParseMemberType(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ClassType.강지;

        // 1. Enum과 정확히 일치하는 경우 (예: "강지")
        if (System.Enum.TryParse(value, true, out ClassType result)) return result;

        // 2. CSV가 영문(Gangzi)이고 Enum이 한글(강지)인 경우 매핑
        switch (value.Trim().ToLower())
        {
            case "gangzi": return ClassType.강지;
            case "yuni": return ClassType.유니;
            case "huya": return ClassType.후야;
            // 필요한 경우 추가
            default:
                Debug.LogWarning($"알 수 없는 직업 타입: {value}. 기본값(강지)으로 설정됩니다.");
                return ClassType.강지;
        }
    }

    // --- 기존 효과 파싱 로직 ---
    private List<EffectInstance> ParseEffects(string rawEffects)
    {
        List<EffectInstance> effectList = new List<EffectInstance>();
        if (string.IsNullOrWhiteSpace(rawEffects)) return effectList;

        string[] bundles = rawEffects.Split('&');

        foreach (string bundle in bundles)
        {
            EffectInstance effect = ParseSingleEffectRecursively(bundle.Trim());
            if (effect != null)
                effectList.Add(effect);
        }

        return effectList;
    }

    private EffectInstance ParseSingleEffectRecursively(string effectString)
    {
        string[] branches = effectString.Split('/');

        EffectInstance rootEffect = ParseEffectSegment(branches[0].Trim());

        if (branches.Length > 1 && rootEffect != null)
        {
            string remaining = string.Join("/", branches, 1, branches.Length - 1);
            rootEffect.elseEffect = ParseSingleEffectRecursively(remaining);
        }

        return rootEffect;
    }

    private EffectInstance ParseEffectSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment)) return null;

        EffectInstance instance = new EffectInstance();

        string content = segment;
        if (segment.Contains("|"))
        {
            string[] parts = segment.Split('|');
            instance.trigger = parts[0].Trim();
            if (parts.Length > 1) content = parts[1].Trim();
        }
        else
        {
            instance.trigger = "ON_PLAY";
        }

        string[] p = content.Split(':');

        if (p.Length > 0) instance.effectName = p[0].Trim();
        if (p.Length > 1) instance.value1 = ParseInt(p[1]);
        if (p.Length > 2) instance.value2 = ParseInt(p[2]);
        if (p.Length > 3) instance.target = p[3].Trim();
        if (p.Length > 4) instance.condition = p[4].Trim();
        if (p.Length > 5) instance.conditionValue = p[5].Trim();
        if (p.Length > 6) instance.count = ParseInt(p[6]);

        return instance;
    }

    // --- 유틸리티 ---
    private int ParseInt(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        value = value.Replace("(", "").Replace(")", "");
        if (int.TryParse(value, out int result)) return result;
        return 0;
    }

    private T ParseEnum<T>(string value, T defaultValue) where T : struct
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        if (System.Enum.TryParse(value.Replace(" ", ""), true, out T result)) return result;
        return defaultValue;
    }

    private CardType ParseCardType(string koreanType)
    {
        if (string.IsNullOrEmpty(koreanType)) return CardType.하수인;
        if (koreanType.Contains("하수인")) return CardType.하수인;
        if (koreanType.Contains("주문")) return CardType.주문;
        if (koreanType.Contains("무기")) return CardType.무기;
        if (koreanType.Contains("멤버")) return CardType.멤버;
        return CardType.하수인;
    }

    private string[] SplitCsvLine(string line)
    {
        return Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
    }
}