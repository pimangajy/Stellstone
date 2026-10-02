using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 로그 타일에 마우스 커서를 올렸을 때 화면 우측에 나타나는 상세 정보 팝업 UI입니다. (a.png 디자인)
/// - 시전자 (아이콘 + 이름)
/// - 피격자 (아이콘 + 이름, 대상이 있을 때만 표시)
/// - 수치 (피해량, 회복량, 버프치 등)
/// - 상세 요약 문장 (서버 로그 메시지)
/// </summary>
public class GameLogDetailPopup : MonoBehaviour
{
    [Header("기본 UI")]
    public RectTransform popupRect;
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI actionTitleText;
    public TextMeshProUGUI messageText;

    [Header("시전자 영역")]
    public GameObject casterSection;
    public Image casterIcon;
    public TextMeshProUGUI casterNameText;

    [Header("피격자 / 대상 영역")]
    public GameObject targetSection;
    public Image targetIcon;
    public TextMeshProUGUI targetNameText;

    public enum PopupPositionMode
    {
        FixedPosition,      // 사용자가 에디터에서 배치한 위치(anchoredPosition) 고정 유지
        FollowTileY         // 호버된 타일의 높이(Y축)에 맞춰 표시
    }

    [Header("위치 설정")]
    [Tooltip("상세 팝업 위치 모드 (FixedPosition: 에디터에서 배치한 위치 유지, FollowTileY: 호버된 타일 높이에 맞춤)")]
    public PopupPositionMode positionMode = PopupPositionMode.FixedPosition;

    [Header("수치 영역")]
    public GameObject valueSection;
    public TextMeshProUGUI valueText;

    /// <summary>
    /// 상세 정보를 채우고 팝업을 표시합니다.
    /// </summary>
    public void Display(S_NewLogEvent log, Sprite casterSprite, Sprite targetSprite, RectTransform sourceTileRect = null)
    {
        if (log == null) return;

        gameObject.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        // 1. 타이틀 & 요약 메시지
        if (actionTitleText != null)
        {
            actionTitleText.text = GetActionTitle(log.actionType);
        }
        if (messageText != null)
        {
            messageText.text = FormatMessageText(log);
        }

        // 2. 시전자 정보
        if (casterSection != null)
        {
            casterSection.SetActive(true);
            if (casterIcon != null)
            {
                if (casterSprite != null)
                {
                    casterIcon.sprite = casterSprite;
                    casterIcon.gameObject.SetActive(true);
                }
                else
                {
                    casterIcon.gameObject.SetActive(false);
                }
            }
            if (casterNameText != null)
            {
                string cName = ResolveActorDisplayName(log);
                casterNameText.text = $"<b>시전자:</b> {cName}";
            }
        }

        // 3. 피격자 / 대상 정보 (없으면 숨김)
        string targetDisplayName = ResolveTargetDisplayName(log);
        bool hasTarget = !string.IsNullOrEmpty(targetDisplayName);
        if (targetSection != null)
        {
            targetSection.SetActive(hasTarget);
            if (hasTarget)
            {
                if (targetIcon != null)
                {
                    if (targetSprite != null)
                    {
                        targetIcon.sprite = targetSprite;
                        targetIcon.gameObject.SetActive(true);
                    }
                    else
                    {
                        targetIcon.gameObject.SetActive(false);
                    }
                }
                if (targetNameText != null)
                {
                    targetNameText.text = $"<b>대상:</b> {targetDisplayName}";
                }
            }
        }

        // 4. 수치 정보 (피해, 힐, 버프 및 서브 이벤트 목록)
        bool hasValue = log.value != 0 || log.value2 != 0 || (log.subEvents != null && log.subEvents.Count > 0);
        if (valueSection != null)
        {
            valueSection.SetActive(hasValue);
            if (hasValue && valueText != null)
            {
                valueText.text = FormatValueText(log);
            }
        }

        // 5. 위치 조정
        if (positionMode == PopupPositionMode.FollowTileY && sourceTileRect != null && popupRect != null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
                Camera uiCam = (parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? parentCanvas.worldCamera : null;
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCam, sourceTileRect.position);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCam, out Vector2 localPoint))
                {
                    float popupX = popupRect.anchoredPosition.x;
                    float halfHeight = popupRect.rect.height * 0.5f;
                    float targetY = Mathf.Clamp(localPoint.y, -canvasRect.rect.height * 0.5f + halfHeight + 20f, canvasRect.rect.height * 0.5f - halfHeight - 20f);
                    popupRect.anchoredPosition = new Vector2(popupX, targetY);
                }
            }
        }
        // FixedPosition 모드일 때는 사용자가 에디터에서 설정한 위치(anchoredPosition)를 100% 그대로 유지합니다.
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private string GetActionTitle(string actionType)
    {
        return actionType switch
        {
            "PLAY_CARD" => "카드 사용",
            "SUMMON" => "하수인 소환",
            "ATTACK" => "공격 실행",
            "DAMAGE" => "피해 발생",
            "HEAL" => "체력 회복",
            "DEATH" => "하수인 파괴",
            "EFFECT" => "효과 발동",
            "DRAW" => "카드 드로우",
            "ADD_TO_HAND" => "손패 획득",
            "GET_FROM_SIDE_DECK" => "사이드덱 획득",
            "BUFF_HAND" => "손패 버프",
            "BUFF_DECK" => "덱 버프",
            "BURN_CARD" => "카드 소멸",
            "DISCARD" => "카드 버림",
            _ => actionType
        };
    }

    private string FormatValueText(S_NewLogEvent log)
    {
        // 🌟 통합 액션 스코프의 세부 결과(피해, 힐, 드로우 등)가 담겨 있는 경우 목록 형식으로 표시
        if (log.subEvents != null && log.subEvents.Count > 0)
        {
            List<string> lines = new List<string>();
            foreach (var sub in log.subEvents)
            {
                string line = sub.type switch
                {
                    "DAMAGE" => $"<color=#FF5555>• <b>피해:</b> {sub.targetName} -{sub.value}</color>",
                    "HEAL" => $"<color=#55FF77>• <b>회복:</b> {sub.targetName} +{sub.value}</color>",
                    "DRAW" => $"<color=#66CCFF>• <b>드로우:</b> {sub.value}장</color>",
                    "SUMMON" => $"• <b>소환:</b> {sub.targetName} ({sub.value}번 슬롯)",
                    "DEATH" => $"<color=#AAAAAA>• <b>파괴:</b> {sub.targetName}</color>",
                    _ => $"• {sub.type}: {sub.targetName} {sub.value}"
                };
                lines.Add(line);
            }
            return string.Join("\n", lines);
        }

        return log.actionType switch
        {
            "ATTACK" => $"<color=#FF5555><b>피해량:</b> -{log.value}</color>" + (log.value2 > 0 ? $" / <color=#FFAA55><b>반격:</b> -{log.value2}</color>" : ""),
            "DAMAGE" => $"<color=#FF5555><b>피해량:</b> -{log.value}</color>",
            "HEAL" => $"<color=#55FF77><b>회복량:</b> +{log.value}</color>",
            "SUMMON" => $"<b>필드 슬롯:</b> {log.value + 1}번 슬롯",
            "BUFF_HAND" or "BUFF_DECK" => $"<color=#66CCFF><b>버프:</b> +{log.value} / +{log.value2}</color>",
            _ => $"<b>수치:</b> {log.value}" + (log.value2 != 0 ? $" / {log.value2}" : "")
        };
    }

    /// <summary>
    /// 시전자의 표시 이름을 사람이 읽을 수 있는 한글 카드명 또는 계정 닉네임으로 변환합니다.
    /// </summary>
    private string ResolveActorDisplayName(S_NewLogEvent log)
    {
        if (log == null) return "";

        string myUid = GameClient.Instance != null ? GameClient.Instance.UserUid : null;
        string myName = GameClient.Instance != null ? GameClient.Instance.MyUsername : "나";
        string oppName = GameClient.Instance != null ? GameClient.Instance.OpponentUsername : "상대방";

        // 1. 엔티티 ID 기반 리더/하수인 확인
        if (log.sourceEntityId > 0)
        {
            string entityName = ResolveEntityName(log.sourceEntityId);
            if (!string.IsNullOrEmpty(entityName)) return entityName;
        }

        // 2. 플레이어/리더 UID 또는 액터 확인
        if (!string.IsNullOrEmpty(log.playerUid))
        {
            if (log.playerUid == myUid)
            {
                return myName;
            }
            string oppUid = GetOpponentUid();
            if (!string.IsNullOrEmpty(oppUid) && log.playerUid == oppUid)
            {
                return oppName;
            }
        }

        if (!string.IsNullOrEmpty(log.actor))
        {
            if (log.actor == myUid || log.actor == myName || log.actor == "나")
            {
                return myName;
            }
            string oppUid = GetOpponentUid();
            if (log.actor == oppName || (!string.IsNullOrEmpty(oppUid) && log.actor == oppUid))
            {
                return oppName;
            }
            if (log.actor == "System" || log.actor == "시스템")
            {
                // 시스템인 경우 카드나 스킬 정보가 있으면 해당 이름 우선
                if (!string.IsNullOrEmpty(log.sourceCardName))
                {
                    return GetReadableCardName(log.sourceCardName, log.sourceCardId);
                }
                if (!string.IsNullOrEmpty(log.sourceCardId))
                {
                    return GetReadableCardName(log.sourceCardId, null);
                }
                return "시스템";
            }

            // 액터 자체가 카드 ID인 경우 (cards-xxx-xxx)
            string readableFromActor = GetReadableCardName(log.actor, null);
            if (readableFromActor != log.actor) return readableFromActor;
        }

        // 3. 카드 이름 / 카드 ID 확인
        if (!string.IsNullOrEmpty(log.sourceCardName))
        {
            return GetReadableCardName(log.sourceCardName, log.sourceCardId);
        }
        if (!string.IsNullOrEmpty(log.sourceCardId))
        {
            return GetReadableCardName(log.sourceCardId, null);
        }

        return !string.IsNullOrEmpty(log.actor) ? log.actor : "알 수 없음";
    }

    /// <summary>
    /// 피격자/대상의 표시 이름을 사람이 읽을 수 있는 한글 카드명 또는 계정 닉네임으로 변환합니다.
    /// </summary>
    private string ResolveTargetDisplayName(S_NewLogEvent log)
    {
        if (log == null) return "";

        string myUid = GameClient.Instance != null ? GameClient.Instance.UserUid : null;
        string myName = GameClient.Instance != null ? GameClient.Instance.MyUsername : "나";
        string oppName = GameClient.Instance != null ? GameClient.Instance.OpponentUsername : "상대방";

        // 1. 엔티티 ID 기반 리더/하수인 확인
        if (log.targetEntityId > 0)
        {
            string entityName = ResolveEntityName(log.targetEntityId);
            if (!string.IsNullOrEmpty(entityName)) return entityName;
        }

        // 2. targetCardName 기반 확인
        if (!string.IsNullOrEmpty(log.targetCardName))
        {
            if (log.targetCardName == myUid || log.targetCardName == myName || log.targetCardName == "내 리더" || log.targetCardName == "아군 리더")
            {
                return myName;
            }
            string oppUid = GetOpponentUid();
            if (log.targetCardName == oppName || (!string.IsNullOrEmpty(oppUid) && log.targetCardName == oppUid) || log.targetCardName == "적 리더" || log.targetCardName == "상대 리더")
            {
                return oppName;
            }

            // 리더 기본 명칭(강지, 유니, 칸나 등)이 들어온 경우 아군/적군 판별 가능하면 닉네임 반환
            if (GameEntityManager.Instance != null)
            {
                if (GameEntityManager.Instance.myLeader != null && GameEntityManager.Instance.myLeader._cardData != null && log.targetCardName == GameEntityManager.Instance.myLeader._cardData.cardName)
                {
                    return myName;
                }
                if (GameEntityManager.Instance.opponentLeader != null && GameEntityManager.Instance.opponentLeader._cardData != null && log.targetCardName == GameEntityManager.Instance.opponentLeader._cardData.cardName)
                {
                    return oppName;
                }
            }

            return GetReadableCardName(log.targetCardName, null);
        }

        if (log.targetEntityId > 0)
        {
            return $"개체 #{log.targetEntityId}";
        }

        return "";
    }

    /// <summary>
    /// 필드 엔티티 ID를 통해 리더(계정 닉네임) 또는 필드 하수인(한글 카드명)을 조회합니다.
    /// </summary>
    private string ResolveEntityName(int entityId)
    {
        if (GameEntityManager.Instance == null) return null;

        string myName = GameClient.Instance != null ? GameClient.Instance.MyUsername : "나";
        string oppName = GameClient.Instance != null ? GameClient.Instance.OpponentUsername : "상대방";

        // 아군 리더
        if (GameEntityManager.Instance.myLeader != null && GameEntityManager.Instance.myLeader.EntityId == entityId)
        {
            return myName;
        }
        // 적군 리더
        if (GameEntityManager.Instance.opponentLeader != null && GameEntityManager.Instance.opponentLeader.EntityId == entityId)
        {
            return oppName;
        }

        // 고정 ID 규칙 보정 (1 or 10000 = 아군 리더, 2 or 20000 = 적군 리더)
        if (entityId == 1 || entityId == 10000) return myName;
        if (entityId == 2 || entityId == 20000) return oppName;

        // 필드 하수인 조회
        var display = GameEntityManager.Instance.GetEntityDisplay(entityId);
        if (display != null)
        {
            if (display is LeaderCardDisplay leaderDisplay)
            {
                return (leaderDisplay == GameEntityManager.Instance.myLeader) ? myName : oppName;
            }
            if (display._cardData != null && !string.IsNullOrEmpty(display._cardData.cardName))
            {
                return display._cardData.cardName;
            }
        }

        return null;
    }

    /// <summary>
    /// 카드 ID 또는 영문/식별자 이름을 Resources에 등록된 한글 카드명으로 변환합니다.
    /// </summary>
    private string GetReadableCardName(string rawNameOrId, string fallbackCardId)
    {
        if (string.IsNullOrEmpty(rawNameOrId)) return "";

        if (ResourceManager.Instance != null)
        {
            if (ResourceManager.Instance.TryGetCardData(rawNameOrId, out var directData) && !string.IsNullOrEmpty(directData.cardName))
            {
                return directData.cardName;
            }
            if (!string.IsNullOrEmpty(fallbackCardId) && ResourceManager.Instance.TryGetCardData(fallbackCardId, out var fallbackData) && !string.IsNullOrEmpty(fallbackData.cardName))
            {
                return fallbackData.cardName;
            }
        }

        return rawNameOrId;
    }

    /// <summary>
    /// 요약 메시지 본문에 포함된 내부 UID 및 카드 ID를 보기 좋은 닉네임과 한글 카드명으로 치환합니다.
    /// </summary>
    private string FormatMessageText(S_NewLogEvent log)
    {
        if (log == null || string.IsNullOrEmpty(log.message)) return "";

        string msg = log.message;

        // 1. 내 UID 및 상대 UID -> 닉네임 치환
        if (GameClient.Instance != null)
        {
            if (!string.IsNullOrEmpty(GameClient.Instance.UserUid) && !string.IsNullOrEmpty(GameClient.Instance.MyUsername))
            {
                msg = msg.Replace(GameClient.Instance.UserUid, GameClient.Instance.MyUsername);
            }
            string oppUid = GetOpponentUid();
            if (!string.IsNullOrEmpty(oppUid) && !string.IsNullOrEmpty(GameClient.Instance.OpponentUsername))
            {
                msg = msg.Replace(oppUid, GameClient.Instance.OpponentUsername);
            }
        }

        // 2. 원본 카드 ID -> 한글 카드명 치환
        if (!string.IsNullOrEmpty(log.sourceCardId) && ResourceManager.Instance != null)
        {
            if (ResourceManager.Instance.TryGetCardData(log.sourceCardId, out var cardData) && !string.IsNullOrEmpty(cardData.cardName))
            {
                msg = msg.Replace(log.sourceCardId, cardData.cardName);
            }
        }

        // 3. 메시지 내 불필요한 내부 시스템 ID 태그 정리 "(피해 원인 ID: 102)", "(치유 원인 ID: 102)"
        msg = System.Text.RegularExpressions.Regex.Replace(msg, @"\s*\((피해|치유) 원인 ID:\s*\d+\)", "");

        return msg;
    }

    /// <summary>
    /// 상대방 리더의 ownerUid를 안전하게 조회합니다.
    /// </summary>
    private string GetOpponentUid()
    {
        if (GameEntityManager.Instance != null && GameEntityManager.Instance.opponentLeader != null && GameEntityManager.Instance.opponentLeader.CurrentEntityData != null)
        {
            return GameEntityManager.Instance.opponentLeader.CurrentEntityData.ownerUid;
        }
        return null;
    }
}
