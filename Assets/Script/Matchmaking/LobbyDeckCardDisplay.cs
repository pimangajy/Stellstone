using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 로비(MatchingManager) 씬의 덱 목록에 표시될 개별 카드 UI 항목입니다.
/// </summary>
public class LobbyDeckCardDisplay : MonoBehaviour
{
    [Header("UI 구성 요소")]
    [Tooltip("카드 이름을 표시할 TextMeshPro")]
    [SerializeField] private TextMeshProUGUI cardNameText;
    [Tooltip("카드 코스트를 표시할 TextMeshPro")]
    [SerializeField] private TextMeshProUGUI costText;
    [Tooltip("카드 이미지를 표시할 Image")]
    [SerializeField] private Image cardImage;
    [Tooltip("중복 카드 개수(예: x2)를 표시할 TextMeshPro")]
    [SerializeField] private TextMeshProUGUI countText;

    /// <summary>
    /// (수정) CardDataFirebase 대신 CardData(ScriptableObject)를 받습니다.
    /// </summary>
    public void Setup(CardData card, int count)
    {
        if (card == null) return;

        // 1. 코스트와 이름 설정
        if (costText != null)
        {
            costText.text = card.manaCost.ToString(); // cost -> manaCost
        }
        if (cardNameText != null)
        {
            cardNameText.text = card.cardName; // name -> cardName
        }

        // 2. 카드 개수 표시
        if (countText != null)
        {
            if (count > 1)
            {
                countText.text = $"x{count}";
            }
            else
            {
                countText.text = "";
            }
        }

        // 3. 카드 이미지 설정 (ResourceManager 덕분에 아주 쉬워졌습니다!)
        if (cardImage != null)
        {
            // 썸네일(thumbnail) 프로퍼티 사용
            if (card.thumbnail != null)
            {
                cardImage.sprite = card.thumbnail;
            }
            else
            {
                // 이미지가 없을 경우 기본색 처리 등
                // cardImage.color = Color.gray; 
            }
        }
    }
}