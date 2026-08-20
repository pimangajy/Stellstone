using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class CardImporter : EditorWindow
{
    private string csvFolderPath = "Assets/Resources/CSV";
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

        int count = 0;

        // 0번 줄은 헤더이므로 1번 줄부터 시작
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            // 정규식을 사용하여 따옴표("") 안의 쉼표는 분리하지 않도록 처리
            string[] values = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

            // 데이터 유효성 검사 (CardID가 비어있으면 스킵)
            if (values.Length < 11 || string.IsNullOrEmpty(values[0])) continue;

            string id = values[0].Replace("\"", "").Trim();
            string cardClass = values[1].Replace("\"", "").Trim();
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
            // CSV 헤더 기준: CardID(0), Class(1), Name(2), Cost(3), Attack(4), Health(5), 
            // Rarity(6), Type(7), Expansion(8), Tribe(9), Description(10), Targeting(11), Effects(12), Keywords(13)

            card.cardID = id;
            card.cardClass = memberType;
            card.cardName = values[2].Replace("\"", "").Trim();

            card.manaCost = ParseInt(values[3]);
            card.attack = ParseInt(values[4]);
            card.health = ParseInt(values[5]);

            card.rarity = ParseEnum<CardRarity>(values[6], CardRarity.common);
            card.cardType = ParseCardType(values[7]);
            card.expansion = ParseEnum<Expansion>(values[8], Expansion.기본);
            card.minionTribe = ParseEnum<CardTribe>(values[9], CardTribe.무소속);

            // Description은 따옴표 제거 후 매핑
            card.description = values[10].Replace("\"", "").Trim();

            if (values.Length > 11 && !string.IsNullOrWhiteSpace(values[11]))
            {
                string rawValue = values[11].Replace("\"", "").Trim().ToLower();
                card.targeting = (rawValue == "true" || rawValue == "1");
            }

            // Effects(인덱스 12)는 무시합니다.

            // Keywords(인덱스 13)가 존재할 경우 매핑
            if (values.Length > 13 && !string.IsNullOrWhiteSpace(values[13]))
            {
                // 1. 괄호 및 따옴표 제거
                string rawKeywords = values[13].Replace("[", "").Replace("]", "").Replace("\"", "");

                // 2. Enum 리스트 초기화
                card.keyward = new List<CardKeywords>();

                // 3. 내용이 비어있지 않은 경우에만 분리 및 변환
                if (!string.IsNullOrWhiteSpace(rawKeywords))
                {
                    string[] keywordStrings = rawKeywords.Split(',');

                    foreach (string kw in keywordStrings)
                    {
                        // 4. 문자열을 CardKeywords Enum으로 변환 (대소문자 무시: true)
                        if (System.Enum.TryParse(kw.Trim(), true, out CardKeywords parsedKeyword))
                        {
                            card.keyward.Add(parsedKeyword);
                        }
                        else
                        {
                            // Enum에 정의되지 않은 키워드가 들어올 경우를 대비한 예외 처리
                            Debug.LogWarning($"알 수 없는 키워드입니다: {kw.Trim()} (CardID: {id})");
                        }
                    }
                }
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