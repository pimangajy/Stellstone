using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Newtonsoft.Json;

public class CardImporter : EditorWindow
{
    private string csvFolderPath = "Assets/Resources/CSV/CardData";
    private string cardAssetPath = "Assets/Resources/CardData";

    // 유니티 에디터 상단 툴바에 메뉴 추가
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
        if (lines.Length <= 1) return 0; // 헤더만 있는 경우 제외

        // 헤더 인덱스 매핑 (대소문자 무시)
        string[] headers = Regex.Split(lines[0], ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")
            .Select(h => h.Replace("\"", "").Trim())
            .ToArray();

        Dictionary<string, int> col = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < headers.Length; c++)
        {
            if (!col.ContainsKey(headers[c])) col[headers[c]] = c;
        }

        string GetCol(string[] row, string colName, string defVal = "")
        {
            if (col.TryGetValue(colName, out int idx) && idx < row.Length)
            {
                return row[idx].Replace("\"", "").Trim();
            }
            return defVal;
        }

        string GetRawCol(string[] row, string colName, string defVal = "")
        {
            if (col.TryGetValue(colName, out int idx) && idx < row.Length)
            {
                string raw = row[idx].Trim();
                if (raw.StartsWith("\"") && raw.EndsWith("\"") && raw.Length >= 2)
                {
                    raw = raw.Substring(1, raw.Length - 2);
                }
                return raw.Replace("\"\"", "\"").Trim();
            }
            return defVal;
        }

        int count = 0;

        // 0번 줄은 헤더이므로 1번 줄부터 시작
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] values = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

            string id = GetCol(values, "CardID");
            if (string.IsNullOrEmpty(id)) continue;

            string cardClass = GetCol(values, "Class");
            CardClass memberType = ParseMemberType(cardClass);

            // 클래스별로 폴더를 나누어 저장
            string memberFolderPath = $"{cardAssetPath}/{memberType}";

            // 해당 이름의 폴더가 존재하지 않는다면 새로 생성
            if (!Directory.Exists(memberFolderPath))
            {
                Directory.CreateDirectory(memberFolderPath);
            }

            // 에셋 생성 경로를 해당 클래스 폴더 내부로 지정
            string assetPath = $"{memberFolderPath}/{id}.asset";
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(assetPath);

            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardData>();
                AssetDatabase.CreateAsset(card, assetPath);
            }

            // --- 데이터 매핑 ---
            card.cardID = id;
            card.cardClass = memberType;
            card.cardName = GetCol(values, "Name");

            card.manaCost = ParseInt(GetCol(values, "Cost"));
            card.attack = ParseInt(GetCol(values, "Attack"));
            card.health = ParseInt(GetCol(values, "Health"));

            card.rarity = ParseEnum<CardRarity>(GetCol(values, "Rarity"), CardRarity.common);
            card.cardType = ParseCardType(GetCol(values, "Type"));
            card.expansion = ParseEnum<Expansion>(GetCol(values, "Expansion"), Expansion.기본);
            card.minionTribe = ParseEnum<CardTribe>(GetCol(values, "Tribe"), CardTribe.무소속);

            // Description 매핑
            card.description = GetCol(values, "Description");

            string rawTargeting = GetCol(values, "Targeting").ToLower();
            card.targeting = (rawTargeting == "true" || rawTargeting == "1");

            // IsToken 매핑 (CSV 마지막 또는 지정된 위치의 IsToken/Token 헤더 파싱)
            string rawIsToken = GetCol(values, "IsToken");
            if (string.IsNullOrEmpty(rawIsToken)) rawIsToken = GetCol(values, "Token");
            rawIsToken = rawIsToken.ToLower();
            card.isToken = (rawIsToken == "true" || rawIsToken == "1");

            // 특수 오라 스탯 5종 매핑
            card.spellAmp = ParseInt(GetCol(values, "SpellAmp"));
            card.spellWeakness = ParseInt(GetCol(values, "SpellWeakness"));
            card.buffAmpAttack = ParseInt(GetCol(values, "BuffAmpAttack"));
            card.buffAmpHealth = ParseInt(GetCol(values, "BuffAmpHealth"));
            string rawDrawSeal = GetCol(values, "DrawSeal").ToLower();
            card.drawSeal = (rawDrawSeal == "true" || rawDrawSeal == "1");

            // Keywords 매핑
            string rawKeywords = GetCol(values, "Keywords").Replace("[", "").Replace("]", "").Replace("\"", "");
            card.keywords = new List<CardKeywords>();

            if (!string.IsNullOrWhiteSpace(rawKeywords))
            {
                string[] keywordStrings = rawKeywords.Split(',');

                foreach (string kw in keywordStrings)
                {
                    if (System.Enum.TryParse(kw.Trim(), true, out CardKeywords parsedKeyword))
                    {
                        card.keyward.Add(parsedKeyword);
                    }
                    else
                    {
                        Debug.LogWarning($"알 수 없는 키워드입니다: {kw.Trim()} (CardID: {id})");
                    }
                }
            }

            // MemberSkills 파싱
            string rawMemberSkills = GetRawCol(values, "MemberSkills");
            if (!string.IsNullOrWhiteSpace(rawMemberSkills) && rawMemberSkills != "[]")
            {
                try
                {
                    card.memberSkills = JsonConvert.DeserializeObject<List<MemberSkillData>>(rawMemberSkills) ?? new List<MemberSkillData>();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[CardImporter] MemberSkills 파싱 실패: {id} - {ex.Message}");
                    card.memberSkills = new List<MemberSkillData>();
                }
            }
            else
            {
                card.memberSkills = new List<MemberSkillData>();
            }

            // 스크립터블 오브젝트 변경사항 저장 예약
            EditorUtility.SetDirty(card);
            count++;
        }
        return count;
    }

    // --- 유틸리티 메서드 ---

    private int ParseInt(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        value = value.Replace("\"", "").Replace("(", "").Replace(")", "").Trim();
        if (int.TryParse(value, out int result)) return result;
        return 0;
    }

    private T ParseEnum<T>(string value, T defaultValue) where T : struct
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        value = value.Replace("\"", "").Replace(" ", "").Trim();
        if (System.Enum.TryParse(value, true, out T result)) return result;
        return defaultValue;
    }

    private CardClass ParseMemberType(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return CardClass.Gangzi;
        if (System.Enum.TryParse(value, true, out CardClass result)) return result;

        switch (value.Trim().ToLower())
        {
            case "Gangzi": return CardClass.Gangzi;
            case "Yuni": return CardClass.Yuni;
            case "Huya": return CardClass.Huya;
            default:
                Debug.LogWarning($"알 수 없는 직업 타입: {value}. 기본값(Gangzi)으로 설정됩니다.");
                return CardClass.Gangzi;
        }
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
}