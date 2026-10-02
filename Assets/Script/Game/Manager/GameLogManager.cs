using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 배틀 씬의 /Canvas/GameLog 패널을 제어하는 총괄 매니저입니다. (a.png 디자인)
/// - 서버 S_NewLogEvent 수신 시 시전자 이미지가 담긴 정사각형 로그 타일 생성
/// - 마우스 호버 시 전체 화면 딤(Dim) 어둡게 처리 및 우측에 상세 정보 박스 표시
/// - 최대 타일 개수 초과 시 가장 오래된 로그 자동 밀림 처리
/// </summary>
public class GameLogManager : MonoBehaviour
{
    public static GameLogManager Instance { get; private set; }

    [Header("컨테이너 및 설정")]
    [Tooltip("로그 타일들이 세로로 배치될 RectTransform (비워두면 이 컴포넌트의 RectTransform 사용)")]
    [SerializeField] private RectTransform logContainer;

    [Tooltip("화면에 유지할 최대 로그 타일 수 (초과 시 가장 오래된 로그 제거)")]
    [SerializeField] private int maxLogCount = 9;

    [Tooltip("개별 타일 크기 (너비, 높이)")]
    [SerializeField] private Vector2 tileSize = new Vector2(68f, 68f);

    [Header("호버 연출")]
    [Tooltip("호버 시 화면 전체를 어둡게 덮을 딤 오버레이 (자동 생성 지원)")]
    [SerializeField] private Image dimOverlay;

    [Tooltip("로그 호버 시 우측에 나타날 상세 정보 팝업 (자동 생성 지원)")]
    [SerializeField] private GameLogDetailPopup detailPopup;

    private readonly List<GameLogTile> _activeTiles = new List<GameLogTile>();
    private bool _isLoggingStarted = false;
    private static TMP_FontAsset _nanumGothicFont;

    /// <summary>
    /// Resources/Font/NanumGothic SDF 폰트 에셋을 로드 및 캐싱하여 반환합니다.
    /// </summary>
    public static TMP_FontAsset GetNanumGothicFont()
    {
        if (_nanumGothicFont == null)
        {
            _nanumGothicFont = Resources.Load<TMP_FontAsset>("Font/NanumGothic SDF");
        }
        return _nanumGothicFont;
    }

    private void OnEnable()
    {
        _isLoggingStarted = false;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (logContainer == null)
        {
            logContainer = GetComponent<RectTransform>();
        }

        EnsureLayout();
        EnsureDimOverlay();
        EnsureDetailPopup();
    }

    private void Start()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnNewLogEvent += HandleNewLogEvent;
        }
    }

    private void OnDestroy()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnNewLogEvent -= HandleNewLogEvent;
        }
    }

    /// <summary>
    /// GameLog 컨테이너의 VerticalLayoutGroup 설정을 보정합니다.
    /// </summary>
    private void EnsureLayout()
    {
        if (logContainer == null) return;

        var vlg = logContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = logContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        vlg.spacing = 8f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(6, 6, 8, 8);
    }

    /// <summary>
    /// 마우스 호버 시 화면을 살짝 어둡게 만들어줄 전체 화면 딤 오버레이를 준비합니다.
    /// </summary>
    private void EnsureDimOverlay()
    {
        if (dimOverlay != null) return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        Transform existing = parentCanvas.transform.Find("GameLog_DimOverlay");
        if (existing != null)
        {
            dimOverlay = existing.GetComponent<Image>();
        }
        else
        {
            GameObject dimObj = new GameObject("GameLog_DimOverlay", typeof(RectTransform), typeof(Image));
            dimObj.transform.SetParent(parentCanvas.transform, false);

            // GameLog 바로 뒤에 위치하도록 형제 순서 설정 (GameLog가 딤 위에 오도록)
            dimObj.transform.SetSiblingIndex(transform.GetSiblingIndex());

            var rt = dimObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            dimOverlay = dimObj.GetComponent<Image>();
            dimOverlay.color = new Color(0f, 0f, 0f, 0.45f); // 45% 반투명 블랙
            dimOverlay.raycastTarget = false;
        }

        dimOverlay.gameObject.SetActive(false);
    }

    /// <summary>
    /// 상세 정보 박스(GameLogDetailPopup)를 준비합니다.
    /// </summary>
    private void EnsureDetailPopup()
    {
        if (detailPopup != null) return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        Transform existing = parentCanvas.transform.Find("GameLog_DetailPopup");
        if (existing != null)
        {
            detailPopup = existing.GetComponent<GameLogDetailPopup>();
        }
        else
        {
            detailPopup = CreateDefaultDetailPopup(parentCanvas.transform);
        }

        if (detailPopup != null)
        {
            detailPopup.gameObject.SetActive(false);
        }
    }

    private GameLogDetailPopup CreateDefaultDetailPopup(Transform parent)
    {
        GameObject popupObj = new GameObject("GameLog_DetailPopup", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(GameLogDetailPopup));
        popupObj.transform.SetParent(parent, false);
        popupObj.transform.SetAsLastSibling();

        var rt = popupObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f); // 좌측 중앙 피벗 -> GameLog 패널 우측으로 자연스럽게 팝업
        rt.anchoredPosition = new Vector2(-780f, 0f); // GameLog(x = -850, w = 100) 바로 우측 안착
        rt.sizeDelta = new Vector2(280f, 240f);

        var bgImg = popupObj.GetComponent<Image>();
        bgImg.color = new Color(0.08f, 0.12f, 0.18f, 0.96f);

        var outline = popupObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.65f, 0.95f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        var popup = popupObj.GetComponent<GameLogDetailPopup>();
        popup.popupRect = rt;
        popup.canvasGroup = popupObj.GetComponent<CanvasGroup>();

        // 1. 타이틀 텍스트
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(popupObj.transform, false);
        var titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -8f);
        titleRt.sizeDelta = new Vector2(-20f, 28f);

        var titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.fontSize = 17f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(1f, 0.85f, 0.3f);
        titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
        popup.actionTitleText = titleTmp;

        // 2. 시전자 영역
        GameObject casterObj = new GameObject("CasterSection", typeof(RectTransform), typeof(Image));
        casterObj.transform.SetParent(popupObj.transform, false);
        var casterRt = casterObj.GetComponent<RectTransform>();
        casterRt.anchorMin = new Vector2(0f, 1f);
        casterRt.anchorMax = new Vector2(1f, 1f);
        casterRt.pivot = new Vector2(0.5f, 1f);
        casterRt.anchoredPosition = new Vector2(0f, -40f);
        casterRt.sizeDelta = new Vector2(-20f, 40f);
        casterObj.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.26f, 0.7f);

        GameObject casterIconObj = new GameObject("CasterIcon", typeof(RectTransform), typeof(Image));
        casterIconObj.transform.SetParent(casterObj.transform, false);
        var cIconRt = casterIconObj.GetComponent<RectTransform>();
        cIconRt.anchorMin = new Vector2(0f, 0.5f);
        cIconRt.anchorMax = new Vector2(0f, 0.5f);
        cIconRt.anchoredPosition = new Vector2(22f, 0f);
        cIconRt.sizeDelta = new Vector2(32f, 32f);
        popup.casterIcon = casterIconObj.GetComponent<Image>();

        GameObject casterTextObj = new GameObject("CasterName", typeof(RectTransform), typeof(TextMeshProUGUI));
        casterTextObj.transform.SetParent(casterObj.transform, false);
        var cTextRt = casterTextObj.GetComponent<RectTransform>();
        cTextRt.anchorMin = new Vector2(0f, 0f);
        cTextRt.anchorMax = new Vector2(1f, 1f);
        cTextRt.offsetMin = new Vector2(44f, 2f);
        cTextRt.offsetMax = new Vector2(-6f, -2f);
        var cTmp = casterTextObj.GetComponent<TextMeshProUGUI>();
        cTmp.fontSize = 14f;
        cTmp.alignment = TextAlignmentOptions.MidlineLeft;
        cTmp.color = Color.white;
        popup.casterNameText = cTmp;
        popup.casterSection = casterObj;

        // 3. 피격자 / 대상 영역
        GameObject targetObj = new GameObject("TargetSection", typeof(RectTransform), typeof(Image));
        targetObj.transform.SetParent(popupObj.transform, false);
        var targetRt = targetObj.GetComponent<RectTransform>();
        targetRt.anchorMin = new Vector2(0f, 1f);
        targetRt.anchorMax = new Vector2(1f, 1f);
        targetRt.pivot = new Vector2(0.5f, 1f);
        targetRt.anchoredPosition = new Vector2(0f, -86f);
        targetRt.sizeDelta = new Vector2(-20f, 40f);
        targetObj.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.26f, 0.7f);

        GameObject targetIconObj = new GameObject("TargetIcon", typeof(RectTransform), typeof(Image));
        targetIconObj.transform.SetParent(targetObj.transform, false);
        var tIconRt = targetIconObj.GetComponent<RectTransform>();
        tIconRt.anchorMin = new Vector2(0f, 0.5f);
        tIconRt.anchorMax = new Vector2(0f, 0.5f);
        tIconRt.anchoredPosition = new Vector2(22f, 0f);
        tIconRt.sizeDelta = new Vector2(32f, 32f);
        popup.targetIcon = targetIconObj.GetComponent<Image>();

        GameObject targetTextObj = new GameObject("TargetName", typeof(RectTransform), typeof(TextMeshProUGUI));
        targetTextObj.transform.SetParent(targetObj.transform, false);
        var tTextRt = targetTextObj.GetComponent<RectTransform>();
        tTextRt.anchorMin = new Vector2(0f, 0f);
        tTextRt.anchorMax = new Vector2(1f, 1f);
        tTextRt.offsetMin = new Vector2(44f, 2f);
        tTextRt.offsetMax = new Vector2(-6f, -2f);
        var tTmp = targetTextObj.GetComponent<TextMeshProUGUI>();
        tTmp.fontSize = 14f;
        tTmp.alignment = TextAlignmentOptions.MidlineLeft;
        tTmp.color = Color.white;
        popup.targetNameText = tTmp;
        popup.targetSection = targetObj;

        // 4. 수치 영역
        GameObject valueObj = new GameObject("ValueSection", typeof(RectTransform), typeof(TextMeshProUGUI));
        valueObj.transform.SetParent(popupObj.transform, false);
        var valRt = valueObj.GetComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0f, 1f);
        valRt.anchorMax = new Vector2(1f, 1f);
        valRt.pivot = new Vector2(0.5f, 1f);
        valRt.anchoredPosition = new Vector2(0f, -132f);
        valRt.sizeDelta = new Vector2(-20f, 26f);
        var valTmp = valueObj.GetComponent<TextMeshProUGUI>();
        valTmp.fontSize = 15f;
        valTmp.fontStyle = FontStyles.Bold;
        valTmp.alignment = TextAlignmentOptions.MidlineLeft;
        popup.valueText = valTmp;
        popup.valueSection = valueObj;

        // 5. 요약 메시지
        GameObject msgObj = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        msgObj.transform.SetParent(popupObj.transform, false);
        var msgRt = msgObj.GetComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0f, 0f);
        msgRt.anchorMax = new Vector2(1f, 1f);
        msgRt.offsetMin = new Vector2(10f, 8f);
        msgRt.offsetMax = new Vector2(-10f, -162f);
        var msgTmp = msgObj.GetComponent<TextMeshProUGUI>();
        msgTmp.fontSize = 13f;
        msgTmp.color = new Color(0.82f, 0.88f, 0.94f);
        msgTmp.alignment = TextAlignmentOptions.TopLeft;
        msgTmp.textWrappingMode = TextWrappingModes.Normal;
        popup.messageText = msgTmp;

        TMP_FontAsset nanumFont = GetNanumGothicFont();
        if (nanumFont != null)
        {
            titleTmp.font = nanumFont;
            cTmp.font = nanumFont;
            tTmp.font = nanumFont;
            valTmp.font = nanumFont;
            msgTmp.font = nanumFont;
        }

        return popup;
    }

    /// <summary>
    /// 서버로부터 새 로그 패킷을 수신했을 때 호출됩니다.
    /// </summary>
    private void HandleNewLogEvent(S_NewLogEvent log)
    {
        if (log == null) return;

        // 멀리건 종료 및 첫 턴 드로우 이전의 로그(초기 5장 드로우, 덱 교체 귀환/재드로우 등) 무시
        if (!_isLoggingStarted)
        {
            // 1) 아직 멀리건 단계인 경우 무조건 스킵
            if (BattleManager.Instance != null && BattleManager.Instance.isMulliganPhase)
            {
                return;
            }

            // 2) 멀리건이 끝난 후, 첫 턴 드로우 액션(DRAW)부터 로깅 개시
            if (log.actionType == "DRAW" || (BattleManager.Instance != null && BattleManager.Instance.currentPhase == GamePhase.DRAW))
            {
                _isLoggingStarted = true;
            }
            else if (BattleManager.Instance != null && BattleManager.Instance.currentPhase == GamePhase.MAIN)
            {
                _isLoggingStarted = true;
            }
            else
            {
                return;
            }
        }

        // 1. 시전자 스프라이트 조회
        Sprite casterSprite = ResolveCasterSprite(log);

        // 2. 아군/적군 판단 (내 액션: 파랑, 상대 액션: 빨강)
        string myUid = GameClient.Instance != null ? GameClient.Instance.UserUid : null;
        string myName = GameClient.Instance != null ? GameClient.Instance.MyUsername : null;
        bool isMyAction = false;
        if (!string.IsNullOrEmpty(log.playerUid) && !string.IsNullOrEmpty(myUid))
        {
            isMyAction = (log.playerUid == myUid);
        }
        else if (!string.IsNullOrEmpty(myName) && log.actor == myName)
        {
            isMyAction = true;
        }
        else if (!string.IsNullOrEmpty(myUid) && log.actor == myUid)
        {
            isMyAction = true;
        }

        // 3. 타일 생성 및 리스트 맨 앞에 추가 (하스스톤 스타일: 최신 항목이 index 0 및 상단 배치)
        GameLogTile newTile = CreateLogTile(log, casterSprite, isMyAction);
        _activeTiles.Insert(0, newTile);

        // 4. 최대 개수 초과 시 가장 오래된 로그(리스트의 맨 뒤이자 하단) 제거
        while (_activeTiles.Count > maxLogCount)
        {
            GameLogTile oldest = _activeTiles[_activeTiles.Count - 1];
            _activeTiles.RemoveAt(_activeTiles.Count - 1);
            if (oldest != null)
            {
                Destroy(oldest.gameObject);
            }
        }
    }

    private GameLogTile CreateLogTile(S_NewLogEvent log, Sprite casterSprite, bool? isMyAction)
    {
        GameObject tileObj = new GameObject("LogTile", typeof(RectTransform), typeof(Image), typeof(GameLogTile));
        tileObj.transform.SetParent(logContainer, false);
        tileObj.transform.SetAsFirstSibling(); // 하스스톤 스타일: 최신 타일이 항상 최상단에 오도록 설정

        var rt = tileObj.GetComponent<RectTransform>();
        rt.sizeDelta = tileSize;

        var bgImg = tileObj.GetComponent<Image>();
        bgImg.color = new Color(0.08f, 0.12f, 0.18f, 0.9f);

        // 테두리
        var outline = tileObj.AddComponent<Outline>();
        outline.effectDistance = new Vector2(2f, -2f);

        // 시전자 이미지
        GameObject imgObj = new GameObject("CasterImage", typeof(RectTransform), typeof(Image));
        imgObj.transform.SetParent(tileObj.transform, false);
        var imgRt = imgObj.GetComponent<RectTransform>();
        imgRt.anchorMin = Vector2.zero;
        imgRt.anchorMax = Vector2.one;
        imgRt.offsetMin = new Vector2(3f, 3f);
        imgRt.offsetMax = new Vector2(-3f, -3f);
        var cImg = imgObj.GetComponent<Image>();
        cImg.preserveAspect = true;

        // 배지 텍스트
        GameObject badgeObj = new GameObject("BadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        badgeObj.transform.SetParent(tileObj.transform, false);
        var bRt = badgeObj.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 0f);
        bRt.anchorMax = new Vector2(1f, 0.35f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        var badgeTmp = badgeObj.GetComponent<TextMeshProUGUI>();
        badgeTmp.fontSize = 12f;
        badgeTmp.fontStyle = FontStyles.Bold;
        badgeTmp.alignment = TextAlignmentOptions.Center;
        badgeTmp.color = Color.white;
        TMP_FontAsset nanumFont = GetNanumGothicFont();
        if (nanumFont != null)
        {
            badgeTmp.font = nanumFont;
        }

        var tileComp = tileObj.GetComponent<GameLogTile>();
        tileComp.backgroundImage = bgImg;
        tileComp.casterImage = cImg;
        tileComp.badgeText = badgeTmp;

        // Outline 테두리 조작용 가상 바인딩 (borderImage 대신 outline color 조작 가능하도록)
        GameObject borderObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderObj.transform.SetParent(tileObj.transform, false);
        var borderRt = borderObj.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = Vector2.zero;
        borderRt.offsetMax = Vector2.zero;
        var borderImg = borderObj.GetComponent<Image>();
        borderImg.color = Color.clear;
        borderImg.raycastTarget = false;
        borderImg.enabled = false; // outline 색상 바인딩용 더미 (CasterImage 가림 방지)
        tileComp.borderImage = borderImg;

        // 초기화
        tileComp.Setup(log, casterSprite, isMyAction);

        // outline 색상 맞춤
        if (tileComp.borderImage != null)
        {
            outline.effectColor = tileComp.borderImage.color;
        }

        // 호버 이벤트 구독
        tileComp.OnTileHovered += HandleTileHovered;
        tileComp.OnTileUnhovered += HandleTileUnhovered;

        return tileComp;
    }

    private void HandleTileHovered(GameLogTile tile, S_NewLogEvent log)
    {
        if (tile == null || log == null) return;

        // 1. 전체 화면 딤 활성화
        if (dimOverlay != null)
        {
            dimOverlay.gameObject.SetActive(true);
        }

        // 2. 피격자 / 대상 스프라이트 조회
        Sprite targetSprite = ResolveTargetSprite(log);

        // 3. 상세 팝업 표시 (타일의 RectTransform 전달)
        if (detailPopup != null)
        {
            detailPopup.Display(log, tile.CasterSprite, targetSprite, tile.GetComponent<RectTransform>());
        }
    }

    private void HandleTileUnhovered(GameLogTile tile)
    {
        if (dimOverlay != null)
        {
            dimOverlay.gameObject.SetActive(false);
        }

        if (detailPopup != null)
        {
            detailPopup.Hide();
        }
    }

    /// <summary>
    /// 로그 데이터에서 시전자의 대표 스프라이트를 조회합니다.
    /// </summary>
    private Sprite ResolveCasterSprite(S_NewLogEvent log)
    {
        // 0. 상대방의 단순 카드 드로우인 경우 비밀 보장을 위해 상대 리더 스프라이트로 대체
        if (log.actionType == "DRAW")
        {
            string myUid = GameClient.Instance != null ? GameClient.Instance.UserUid : null;
            bool isMyDraw = (!string.IsNullOrEmpty(log.playerUid) && !string.IsNullOrEmpty(myUid))
                ? (log.playerUid == myUid)
                : (log.actor == (GameClient.Instance != null ? GameClient.Instance.MyUsername : null));

            if (!isMyDraw)
            {
                if (GameEntityManager.Instance != null && GameEntityManager.Instance.opponentLeader != null && GameEntityManager.Instance.opponentLeader.leaderSpriteRenderer != null)
                {
                    return GameEntityManager.Instance.opponentLeader.leaderSpriteRenderer.sprite;
                }
                return null;
            }
        }

        // 1. sourceCardId가 있을 경우 카드 썸네일 조회
        if (!string.IsNullOrEmpty(log.sourceCardId) && ResourceManager.Instance != null)
        {
            if (ResourceManager.Instance.TryGetCardData(log.sourceCardId, out var cd) && cd != null && cd.thumbnail != null)
            {
                return cd.thumbnail;
            }
        }

        // 2. 필드에 소환된 하수인에서 카드 썸네일 조회
        if (log.sourceEntityId > 0 && GameEntityManager.Instance != null)
        {
            var entity = GameEntityManager.Instance.GetEntityDisplay(log.sourceEntityId);
            if (entity != null && entity._cardData != null && entity._cardData.thumbnail != null)
            {
                return entity._cardData.thumbnail;
            }
        }

        // 3. 리더 여부 확인 (EntityId 1=내 리더, 2=상대 리더)
        if (GameEntityManager.Instance != null)
        {
            if (log.sourceEntityId == 1 && GameEntityManager.Instance.myLeader != null)
            {
                if (GameEntityManager.Instance.myLeader.leaderSpriteRenderer != null)
                    return GameEntityManager.Instance.myLeader.leaderSpriteRenderer.sprite;
            }
            else if (log.sourceEntityId == 2 && GameEntityManager.Instance.opponentLeader != null)
            {
                if (GameEntityManager.Instance.opponentLeader.leaderSpriteRenderer != null)
                    return GameEntityManager.Instance.opponentLeader.leaderSpriteRenderer.sprite;
            }
        }

        return null;
    }

    /// <summary>
    /// 로그 데이터에서 피격자/대상의 대표 스프라이트를 조회합니다.
    /// </summary>
    private Sprite ResolveTargetSprite(S_NewLogEvent log)
    {
        if (log.targetEntityId <= 0 || GameEntityManager.Instance == null) return null;

        var entity = GameEntityManager.Instance.GetEntityDisplay(log.targetEntityId);
        if (entity != null && entity._cardData != null && entity._cardData.thumbnail != null)
        {
            return entity._cardData.thumbnail;
        }

        if (log.targetEntityId == 1 && GameEntityManager.Instance.myLeader != null)
        {
            if (GameEntityManager.Instance.myLeader.leaderSpriteRenderer != null)
                return GameEntityManager.Instance.myLeader.leaderSpriteRenderer.sprite;
        }
        else if (log.targetEntityId == 2 && GameEntityManager.Instance.opponentLeader != null)
        {
            if (GameEntityManager.Instance.opponentLeader.leaderSpriteRenderer != null)
                return GameEntityManager.Instance.opponentLeader.leaderSpriteRenderer.sprite;
        }

        return null;
    }
}
