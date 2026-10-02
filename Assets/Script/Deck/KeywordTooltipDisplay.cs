using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 키워드 툴팁 팝업창 개별 오브젝트를 제어하는 전용 컴포넌트입니다.
/// - 글자 수에 따른 스마트 자동 줄바꿈 (\n)
/// - 레이아웃 즉시 재계산 (Content Size Fitter 연동)
/// </summary>
public class KeywordTooltipDisplay : MonoBehaviour
{
    [Header("UI 연결")]
    [Tooltip("키워드 설명 텍스트 (비워두면 자식에서 자동 탐색)")]
    public TextMeshProUGUI tooltipText;

    [Header("자동 줄바꿈 설정")]
    [Tooltip("한 줄에 들어갈 최대 글자 수 (0 이하면 줄바꿈 비활성화)")]
    public int maxCharactersPerLine = 15;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (tooltipText == null)
        {
            tooltipText = GetComponentInChildren<TextMeshProUGUI>();
        }
        if (tooltipText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(tooltipText, minSize: 10f, maxSize: 18f, wordWrap: true, overflowMode: TextOverflowModes.Ellipsis);
        }
    }

    /// <summary>
    /// 텍스트를 대입하고, 설정된 글자 수를 초과하면 자동으로 줄바꿈(\n)을 수행합니다.
    /// </summary>
    public void SetText(string rawText)
    {
        if (tooltipText == null)
        {
            tooltipText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (tooltipText == null || string.IsNullOrEmpty(rawText)) return;

        string processedText = FormatTextWithWrapping(rawText, maxCharactersPerLine);
        tooltipText.text = processedText;

        // Content Size Fitter 및 Layout Group이 즉시 크기를 재계산하도록 강제 갱신
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

    /// <summary>
    /// 지정한 글자 수를 넘어가면 띄어쓰기 또는 글자 수 기준으로 줄바꿈(\n)을 삽입합니다.
    /// </summary>
    private string FormatTextWithWrapping(string text, int maxChars)
    {
        if (maxChars <= 0 || text.Length <= maxChars)
        {
            return text;
        }

        // 이미 사용자가 줄바꿈을 넣은 경우 각 줄별로 검사
        string[] lines = text.Split('\n');
        StringBuilder result = new StringBuilder();

        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            string line = lines[lineIdx];
            if (line.Length <= maxChars)
            {
                result.Append(line);
            }
            else
            {
                // 띄어쓰기 단위로 쪼개어 누적
                string[] words = line.Split(' ');
                int currentLineLen = 0;

                for (int w = 0; w < words.Length; w++)
                {
                    string word = words[w];
                    if (string.IsNullOrEmpty(word)) continue;

                    // 단어 자체가 maxChars보다 긴 경우 강제 분할
                    while (word.Length > maxChars)
                    {
                        if (currentLineLen > 0)
                        {
                            result.Append('\n');
                            currentLineLen = 0;
                        }
                        result.Append(word.Substring(0, maxChars));
                        result.Append('\n');
                        word = word.Substring(maxChars);
                    }

                    if (word.Length == 0) continue;

                    if (currentLineLen == 0)
                    {
                        result.Append(word);
                        currentLineLen = word.Length;
                    }
                    else if (currentLineLen + 1 + word.Length <= maxChars)
                    {
                        result.Append(' ');
                        result.Append(word);
                        currentLineLen += 1 + word.Length;
                    }
                    else
                    {
                        result.Append('\n');
                        result.Append(word);
                        currentLineLen = word.Length;
                    }
                }
            }

            if (lineIdx < lines.Length - 1)
            {
                result.Append('\n');
            }
        }

        return result.ToString();
    }
}
