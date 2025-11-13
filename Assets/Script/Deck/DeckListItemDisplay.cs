using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro를 사용하기 위해 필요합니다.

/// <summary>
/// 오른쪽 덱 목록에 표시될 각 카드 항목 UI를 제어하는 스크립트입니다.
/// </summary>
public class DeckListItemDisplay : MonoBehaviour, ICardDataHolder
{
    [Header("UI 요소 연결")]
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private TextMeshProUGUI cardCostText;
    [SerializeField] private TextMeshProUGUI cardCountText; // "x1", "x2" 등을 표시할 텍스트

    private CardDataFirebase cardData; // 이 항목이 표시하는 카드 데이터

    /// <summary>
    /// 카드 데이터와 덱에 포함된 개수를 받아 UI를 설정합니다.
    /// </summary>
    public void Setup(CardDataFirebase data, int count)
    {
        this.cardData = data;

        cardNameText.text = data.name;
        cardCostText.text = data.cost.ToString();

        // 카드의 희귀도가 '전설'이면 개수를 표시하지 않고, 그 외에는 "x"와 함께 개수를 표시합니다.
        if (data.rarity == "legendary")
        {
            // 전설 카드는 1장만 들어가므로 굳이 개수를 표시하지 않아도 됩니다. (디자인 선택)
            cardCountText.text = "";
        }
        else
        {
            cardCountText.text = "x" + count;
        }

        // TODO: 카드 이름에 희귀도별로 색상을 입히는 로직을 추가하면 좋습니다.
    }

    /// <summary>
    /// 이 UI 항목에 연결된 원본 카드 데이터를 반환합니다.
    /// </summary>
    public CardDataFirebase GetCardData()
    {
        return cardData;
    }
}
