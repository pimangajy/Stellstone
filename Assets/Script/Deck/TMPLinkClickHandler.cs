using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// TextMeshProUGUI 컴포넌트의 클릭 및 마우스 호버 이벤트를 감지하여,
/// - <link="CARD:ID">: 클릭 시 참조 카드 미리보기 연동
/// - <link="KW:Keyword">: 마우스 호버 시 단어 바로 위에 CardKeywordTooltipPopup 표시
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TMPLinkClickHandler : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
{
    private TextMeshProUGUI textComponent;
    private string currentHoveredLinkId = null;

    private void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
        if (textComponent != null)
        {
            textComponent.raycastTarget = true;
        }
    }

    private void OnDisable()
    {
        ClearKeywordHover();
    }

    /// <summary>
    /// 마우스가 텍스트 위에서 움직일 때 키워드 링크 위에 있는지 실시간 감지합니다.
    /// </summary>
    public void OnPointerMove(PointerEventData eventData)
    {
        if (textComponent == null) return;

        Camera cam = (eventData.enterEventCamera != null) ? eventData.enterEventCamera : eventData.pressEventCamera;
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, eventData.position, cam);

        if (linkIndex != -1 && textComponent.textInfo != null && linkIndex < textComponent.textInfo.linkInfo.Length)
        {
            TMP_LinkInfo linkInfo = textComponent.textInfo.linkInfo[linkIndex];
            string linkId = linkInfo.GetLinkID();

            if (!string.IsNullOrEmpty(linkId) && linkId.StartsWith("KW:"))
            {
                if (currentHoveredLinkId != linkId)
                {
                    currentHoveredLinkId = linkId;
                    string kwToken = linkId.Substring(3);

                    if (CardTextFormatter.TryGetKeyword(kwToken, out var kwInfo))
                    {
                        Vector3 wordTopCenter = GetLinkTopCenterPosition(linkInfo);
                        CardKeywordTooltipPopup.Instance?.Show(kwInfo.title, kwInfo.desc, wordTopCenter);
                    }
                }
                return;
            }
        }

        // 마우스가 키워드 링크 바깥에 있다면 팝업 숨김
        ClearKeywordHover();
    }

    /// <summary>
    /// 마우스가 텍스트 영역을 벗어났을 때 툴팁을 닫습니다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        ClearKeywordHover();
    }

    private void ClearKeywordHover()
    {
        if (!string.IsNullOrEmpty(currentHoveredLinkId))
        {
            currentHoveredLinkId = null;
            CardKeywordTooltipPopup.Instance?.Hide();
        }
    }

    /// <summary>
    /// 링크 단어의 정점 메시를 바탕으로 해당 단어의 상단 중앙 월드 좌표를 정밀 계산합니다.
    /// </summary>
    private Vector3 GetLinkTopCenterPosition(TMP_LinkInfo linkInfo)
    {
        if (textComponent.textInfo == null || linkInfo.linkTextLength <= 0)
        {
            return transform.position;
        }

        int firstChar = linkInfo.linkTextfirstCharacterIndex;
        int lastChar = firstChar + linkInfo.linkTextLength - 1;

        if (firstChar >= 0 && lastChar < textComponent.textInfo.characterInfo.Length)
        {
            var firstInfo = textComponent.textInfo.characterInfo[firstChar];
            var lastInfo = textComponent.textInfo.characterInfo[lastChar];

            // characterInfo 좌표는 textComponent 로컬 기준이므로 월드 좌표로 변환
            Vector3 localTopLeft = firstInfo.topLeft;
            Vector3 localTopRight = lastInfo.topRight;
            Vector3 localTopCenter = (localTopLeft + localTopRight) * 0.5f;

            return textComponent.transform.TransformPoint(localTopCenter);
        }

        return transform.position;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (textComponent == null) return;

        Camera cam = (eventData.pressEventCamera != null) ? eventData.pressEventCamera : null;
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, eventData.position, cam);

        if (linkIndex != -1 && textComponent.textInfo != null && linkIndex < textComponent.textInfo.linkInfo.Length)
        {
            TMP_LinkInfo linkInfo = textComponent.textInfo.linkInfo[linkIndex];
            string linkId = linkInfo.GetLinkID();
            
            if (!string.IsNullOrEmpty(linkId))
            {
                if (linkId.StartsWith("CARD:"))
                {
                    string cardId = linkId.Substring(5);
                    CardData card = CardTextFormatter.FindCardData(cardId);
                    if (card != null && DeckCardPreviewManager.Instance != null)
                    {
                        DeckCardPreviewManager.Instance.ShowPreview(card, transform.position);
                    }
                }
            }
        }
    }
}
