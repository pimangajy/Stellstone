using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// /Canvas/GameLog 레일 내부에 생성되는 개별 정사각형 로그 타일 UI 컴포넌트입니다. (a.png 디자인)
/// - 시전자 이미지(하수인, 리더, 주문)를 네모난 형태로 표시
/// - 아군/적군/시스템에 따른 테두리 색상 표시
/// - 마우스 호버 감지 및 상세 정보 이벤트 발행
/// </summary>
public class GameLogTile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 바인딩")]
    public Image backgroundImage;
    public Image casterImage;
    public Image borderImage;
    public TextMeshProUGUI badgeText;

    public S_NewLogEvent LogData { get; private set; }
    public Sprite CasterSprite { get; private set; }

    public event Action<GameLogTile, S_NewLogEvent> OnTileHovered;
    public event Action<GameLogTile> OnTileUnhovered;

    /// <summary>
    /// 로그 데이터를 바탕으로 타일을 초기화합니다.
    /// </summary>
    public void Setup(S_NewLogEvent log, Sprite casterSprite, bool? isMyAction)
    {
        LogData = log;
        CasterSprite = casterSprite;

        // 1. 시전자 이미지 설정
        if (casterImage != null)
        {
            if (casterSprite != null)
            {
                casterImage.sprite = casterSprite;
                casterImage.color = Color.white;
                casterImage.gameObject.SetActive(true);
            }
            else
            {
                casterImage.gameObject.SetActive(false);
            }
        }

        // 2. 아군/적군 피아식별 테두리 색상 (노란색 제외: 내 액션은 블루, 그 외는 레드)
        if (borderImage != null)
        {
            if (isMyAction == true)
            {
                borderImage.color = new Color(0.25f, 0.65f, 1.0f, 1.0f); // 아군 블루
            }
            else
            {
                borderImage.color = new Color(1.0f, 0.35f, 0.35f, 1.0f); // 적군 레드
            }
        }

        // 3. 액션 요약 배지 텍스트
        if (badgeText != null)
        {
            badgeText.text = GetActionShortBadge(log.actionType);
        }
    }

    private string GetActionShortBadge(string actionType)
    {
        return actionType switch
        {
            "PLAY_CARD" => "사용",
            "SUMMON" => "소환",
            "ATTACK" => "공격",
            "DAMAGE" => "피해",
            "HEAL" => "회복",
            "DEATH" => "사망",
            "EFFECT" => "효과",
            "DRAW" => "드로우",
            "ADD_TO_HAND" => "획득",
            "GET_FROM_SIDE_DECK" => "사이드",
            "BUFF_HAND" => "버프",
            "BUFF_DECK" => "덱버프",
            "BURN_CARD" => "소멸",
            "DISCARD" => "버림",
            _ => actionType
        };
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnTileHovered?.Invoke(this, LogData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnTileUnhovered?.Invoke(this);
    }
}
