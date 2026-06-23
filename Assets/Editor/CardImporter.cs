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
            CardClass memberType = ParseMemberType(values[1]);

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
            card.cardClass = memberType;        // Index 1: cardClass
            card.cardName = values[5];          // Index 2: Name
            card.manaCost = ParseInt(values[2]);// Index 3: Cost
            card.attack = ParseInt(values[6]);  // Index 4: Attack
            card.health = ParseInt(values[9]);  // Index 5: Health

            card.rarity = ParseEnum<CardRarity>(values[6], CardRarity.common);
            card.cardType = ParseCardType(values[7]);
            card.expansion = (values[8] == "기본") ? Expansion.기본 : ParseEnum<Expansion>(values[8], Expansion.기본);

            card.description = values[9].Replace("\"", "").Replace("\"\"", "\"");

            string rawEffects = values[10].Trim();
            card.keyward = ExtractKeywords(ref rawEffects);

            // 효과
            card.effects = ParseEffects(values[10]);
            card.targetRule = DetermineTargetRule(card.effects);

            card.minionTribe = ParseEnum<CardTribe>(values[11], CardTribe.무소속);

            // 추가 설명
            if (values.Length > 12)
                card.additionalExplanation = values[12].Replace("\"", "");

            EditorUtility.SetDirty(card);
            count++;
        }
        return count;
    }

    // --- Enum 매핑 도우미 ---

    private List<string> ExtractKeywords(ref string effectString)
    {
        List<string> foundKeywords = new List<string>();

        // 정규식으로 [KEYWORDS:...] 패턴 찾기
        Match match = Regex.Match(effectString, @"^\[KEYWORDS:(.*?)\]");

        if (match.Success)
        {
            string keywordContent = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(keywordContent))
            {
                // 콤마로 구분하여 리스트에 저장
                string[] splitKeywords = keywordContent.Split(',');
                foreach (string k in splitKeywords)
                {
                    foundKeywords.Add(k.Trim());
                }
            }

            // 원본 문자열에서 [KEYWORDS:...] 부분을 제거하여 효과 파싱에 방해 안 되게 함
            effectString = effectString.Substring(match.Length).Trim();

            // 제거 후 만약 맨 앞에 \n이 있다면 추가 제거
            if (effectString.StartsWith("\n") || effectString.StartsWith("\r"))
                effectString = effectString.TrimStart();
        }

        return foundKeywords;
    }

    // [신규] CSV의 영문 Class(Gangzi)를 한글 Enum(강지)으로 매핑
    private CardClass ParseMemberType(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return CardClass.Gangzi;

        // 1. Enum과 정확히 일치하는 경우 (예: "강지")
        if (System.Enum.TryParse(value, true, out CardClass result)) return result;

        // 2. CSV가 영문(Gangzi)이고 Enum이 한글(강지)인 경우 매핑
        switch (value.Trim().ToLower())
        {
            case "gangzi": return CardClass.Gangzi;
            case "yuni": return CardClass.Yuni;
            case "huya": return CardClass.Huya;
            // 필요한 경우 추가
            default:
                Debug.LogWarning($"알 수 없는 직업 타입: {value}. 기본값(강지)으로 설정됩니다.");
                return CardClass.Gangzi;
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

    // 제너레이터의 문자열(Target)을 CardData의 TargetRule Enum으로 변환
    private TargetRule DetermineTargetRule(List<EffectInstance> effects)
    {
        // 효과가 없으면 None 반환
        if (effects == null || effects.Count == 0) return TargetRule.None;

        // 카드를 낼 때의 기준 타겟팅은 주로 첫 번째 효과(메인 효과)를 따름
        string primaryTarget = effects[0].target;

        if (string.IsNullOrEmpty(primaryTarget)) return TargetRule.None;

        switch (primaryTarget.ToUpper())
        {
            // 1. 단일 지정 계열 매핑
            case "TARGET":
            case "TARGET_CHARACTER": return TargetRule.Target_All;
            case "TARGET_MINION": return TargetRule.Target_Minion;
            case "TARGET_ENEMY_CHARACTER": return TargetRule.Target_Enemy_All;
            case "TARGET_ENEMY_MINION": return TargetRule.Target_Enemy_Minion;
            case "ENEMY_HERO": return TargetRule.Target_Enemy_Leader;
            case "TARGET_FRIENDLY_CHARACTER": return TargetRule.Target_Friend_All;
            case "TARGET_FRIENDLY_MINION": return TargetRule.Target_Friend_Minion;
            case "FRIENDLY_HERO": return TargetRule.Target_Friend_Leader;

            // 2. 광역/자동 범위 계열 매핑
            case "ALL_CHARACTERS": return TargetRule.All_Characters;
            case "ALL_MINIONS": return TargetRule.All_Minions;
            case "ALL_ENEMIES": return TargetRule.All_Enemies;
            case "ALL_ENEMY_MINIONS": return TargetRule.All_Enemy_Minions;
            case "ALL_FRIENDS": return TargetRule.All_Friends;
            case "ALL_FRIENDLY_MINIONS": return TargetRule.All_Friendly_Minions;

            case "SELF": return TargetRule.Self;

            default: return TargetRule.None; // 무작위(RANDOM_) 등의 경우 None 처리
        }
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
        if (koreanType.Contains("무기")) return CardType.READER;
        if (koreanType.Contains("멤버")) return CardType.멤버;
        return CardType.하수인;
    }

    private string[] SplitCsvLine(string line)
    {
        return Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
    }
}