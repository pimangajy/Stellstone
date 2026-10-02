using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 카드 프리뷰 배치 방식 모드
/// </summary>
public enum PreviewPositionMode
{
    ScreenCenter,  // 방식 1: 화면 위치 기준 (화면 왼쪽 하수인은 오른쪽, 오른쪽/중앙 하수인은 왼쪽)
    SlotIndex      // 방식 2: 사용자 제안 슬롯 기준 (내 1,2,멤버/상대 1,2는 오른쪽, 나머지 왼쪽)
}

/// <summary>
/// 덱 편성 화면 및 배틀 씬에서 카드의 원본 미리보기(하스스톤 스타일)를 표시하는 매니저입니다.
/// - 메인 카드 원본 표시 (DeckCardDisplay)
/// - 카드에 포함된 키워드/고유단어(등장, 퇴장 등)를 설명하는 개별 팝업창들 표시
/// - 참조하는 카드(토큰)가 있을 경우 보조 원본 카드 표시 (재귀 제한: 참조 카드의 서브 참조는 미표시)
/// </summary>
public class DeckCardPreviewManager : MonoBehaviour
{
    public static DeckCardPreviewManager Instance { get; private set; }

    [Header("배치 모드 (테스트용)")]
    [Tooltip("카드 프리뷰가 하수인 기준 어느 쪽에 뜰지 결정하는 모드입니다.")]
    public PreviewPositionMode positionMode = PreviewPositionMode.SlotIndex;

    [Header("미리보기 UI 연결")]
    [Tooltip("씬 캔버스에 미리 배치해 둔 메인 카드 원본 UI (DeckCardDisplay 컴포넌트)")]
    [SerializeField] private DeckCardDisplay previewCardUI;

    [Tooltip("참조 카드를 띄워줄 보조 카드 UI")]
    [SerializeField] private DeckCardDisplay refCardPreviewUI;

    [Header("키워드 / 고유단어 툴팁 UI")]
    [Tooltip("키워드 설명용 작은 팝업창 프리팹")]
    [SerializeField] private GameObject keywordTooltipPrefab;

    [Tooltip("키워드 툴팁들이 정렬될 부모 RectTransform")]
    [SerializeField] private RectTransform keywordTooltipContainer;

    [Header("위치 설정")]
    [Tooltip("기준 위치 대비 메인 카드 가로/세로 오프셋")]
    [SerializeField] private Vector2 offset = new Vector2(220f, 0f);

    [Header("키워드 툴팁 컨테이너 위치 (인스펙터 조절)")]
    [Tooltip("카드가 왼쪽에 떴을 때 키워드 컨테이너의 로컬 위치 (기본: -150, 200)")]
    public Vector2 tooltipPositionLeft = new Vector2(-150f, 200f);

    [Tooltip("카드가 오른쪽에 떴을 때 키워드 컨테이너의 로컬 위치 (기본: 150, 200)")]
    public Vector2 tooltipPositionRight = new Vector2(150f, 200f);

    [Tooltip("툴팁 확장 방향에 맞춰 컨테이너의 Pivot을 자동으로 (1,1) 또는 (0,1)로 변경할지 여부")]
    public bool autoAdjustPivot = true;

    [Header("3개씩 나열 간격 (인스펙터 조절)")]
    [Tooltip("첫 번째 키워드 팝업창의 시작 위치 오프셋")]
    public Vector2 tooltipStartOffset = Vector2.zero;

    [Tooltip("위아래 키워드 팝업창 사이의 순수 여백 (행 간격)")]
    public float rowPadding = 8f;

    [Tooltip("다음 열로 넘어갈 때의 좌우 열 사이 순수 여백 (열 간격)")]
    public float columnPadding = 12f;

    private RectTransform previewRect;
    private RectTransform refCardRect;
    private Canvas canvas;
    private RectTransform canvasRect;

    // 키워드 툴팁 오브젝트 풀
    private readonly List<GameObject> tooltipPool = new List<GameObject>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        // 1. 메인 카드 초기화
        if (previewCardUI != null)
        {
            previewRect = previewCardUI.GetComponent<RectTransform>();
            StripInteractions(previewCardUI.gameObject);
            previewCardUI.gameObject.SetActive(false);
        }

        // 2. 참조 카드 초기화
        if (refCardPreviewUI != null)
        {
            refCardRect = refCardPreviewUI.GetComponent<RectTransform>();
            StripInteractions(refCardPreviewUI.gameObject);
            refCardPreviewUI.gameObject.SetActive(false);
        }

        // 3. 키워드 컨테이너 초기화
        if (keywordTooltipContainer != null)
        {
            keywordTooltipContainer.gameObject.SetActive(false);
        }
    }

    private void StripInteractions(GameObject obj)
    {
        var raycaster = obj.GetComponent<GraphicRaycaster>();
        if (raycaster != null) Destroy(raycaster);

        var interaction = obj.GetComponent<CardInteraction>();
        if (interaction != null) Destroy(interaction);
    }

    /// <summary>
    /// 카드 원본 미리보기와 함께, 연관 키워드 툴팁들과 참조 토큰 카드(있을 경우)를 화면에 표시합니다.
    /// </summary>
    /// <param name="data">표시할 카드 데이터</param>
    /// <param name="slotWorldPosition">마우스를 올린 슬롯/하수인의 월드 좌표</param>
    /// <param name="sourceCard">호버된 카드 오브젝트 (슬롯 판정용)</param>
    public void ShowPreview(CardData data, Vector3 slotWorldPosition, GameCardDisplay sourceCard = null)
    {
        if (data == null || previewCardUI == null) return;

        EnsureCanvas();

        // 1. 메인 카드 세팅
        previewCardUI.Setup(data);
        previewCardUI.transform.SetAsLastSibling();
        previewCardUI.gameObject.SetActive(true);

        // 2. 화면 좌표 계산 (3D 하수인 월드 좌표 -> 스크린 픽셀 좌표 정확히 투영)
        Vector2 mainScreenPos = CalculateScreenPosition(slotWorldPosition, sourceCard != null);

        // 3. 좌/우 방향 결정 (SlotIndex 모드 vs ScreenCenter 모드)
        bool placeOnRight = DeterminePlaceOnRight(mainScreenPos, sourceCard);

        // 4. 메인 카드 위치 계산 및 배치
        UpdateMainPosition(mainScreenPos, placeOnRight);

        // 5. 키워드 및 고유 단어(등장, 퇴장 등) 툴팁들 생성 및 표시
        ShowKeywordTooltips(data, placeOnRight);

        // 6. 참조 카드(토큰) 확인 및 표시 (단일 깊이 제한: 참조 카드의 서브 참조는 미표시)
        ShowReferencedCard(data, placeOnRight);

        // 7. 적용된 버프 목록(EnchantmentInfo) 표시 (aaa.png 디자인: 카드 하단 배너)
        ShowBuffList(sourceCard);
    }

    /// <summary>
    /// 모든 미리보기(메인 카드, 참조 카드, 키워드 툴팁들)를 숨깁니다.
    /// </summary>
    public void HidePreview()
    {
        if (previewCardUI != null)
        {
            previewCardUI.gameObject.SetActive(false);
        }

        if (refCardPreviewUI != null)
        {
            refCardPreviewUI.gameObject.SetActive(false);
        }

        if (keywordTooltipContainer != null)
        {
            keywordTooltipContainer.gameObject.SetActive(false);
        }

        HideBuffList();

        foreach (var item in tooltipPool)
        {
            if (item != null) item.SetActive(false);
        }
    }

    // =========================================================
    // 좌/우 방향 결정 로직 (SlotIndex vs ScreenCenter)
    // =========================================================
    private bool DeterminePlaceOnRight(Vector2 screenPos, GameCardDisplay sourceCard)
    {
        // 1. 슬롯 번호 기준 모드 (SlotIndex)
        if (positionMode == PreviewPositionMode.SlotIndex && sourceCard != null && sourceCard.CurrentEntityData != null)
        {
            var entity = sourceCard.CurrentEntityData;
            bool isMine = sourceCard.CurrentEntityData.ownerUid == GameClient.Instance.UserUid;

            if (isMine)
            {
                // 내 필드: 멤버 칸 또는 1, 2번 슬롯(position 0, 1) -> 오른쪽
                if (entity.isMember || entity.position == 0 || entity.position == 1)
                {
                    return true;
                }
                // 3, 4, 5번 슬롯(position 2, 3, 4) -> 왼쪽
                return false;
            }
            else
            {
                // 상대 필드: 1, 2번 슬롯(position 0, 1) -> 오른쪽
                if (!entity.isMember && (entity.position == 0 || entity.position == 1))
                {
                    return true;
                }
                // 3, 4, 5번 슬롯(position 2, 3, 4) 및 멤버 칸 -> 왼쪽
                return false;
            }
        }

        // 2. 화면 중앙 기준 모드 (ScreenCenter) 혹은 슬롯 정보가 없을 때 (덱 편성 창 등)
        float screenCenterX = Screen.width * 0.5f;
        // 화면 왼쪽(< 중앙)이면 오른쪽(true), 오른쪽이거나 정중앙(>= 중앙)이면 왼쪽(false)
        return screenPos.x < screenCenterX;
    }

    // =========================================================
    // 키워드 및 고유단어 팝업 처리
    // =========================================================
    private void ShowKeywordTooltips(CardData data, bool placeOnRight)
    {
        if (keywordTooltipContainer == null || keywordTooltipPrefab == null) return;

        List<string> tooltips = CardTextFormatter.GetKeywordAndTermTooltips(data);
        if (tooltips == null || tooltips.Count == 0)
        {
            keywordTooltipContainer.gameObject.SetActive(false);
            return;
        }

        keywordTooltipContainer.transform.SetAsLastSibling();
        keywordTooltipContainer.gameObject.SetActive(true);

        foreach (var item in tooltipPool)
        {
            if (item != null) item.SetActive(false);
        }

        UpdateTooltipContainerPosition(placeOnRight);

        float currentColX = placeOnRight ? tooltipStartOffset.x : -tooltipStartOffset.x;
        int totalTooltips = tooltips.Count;
        int numColumns = (totalTooltips + 2) / 3;

        for (int c = 0; c < numColumns; c++)
        {
            float currentY = tooltipStartOffset.y;
            float maxColWidth = 0f;
            int startIdx = c * 3;
            int endIdx = Mathf.Min(startIdx + 3, totalTooltips);

            for (int i = startIdx; i < endIdx; i++)
            {
                GameObject tooltipItem = GetOrCreateTooltipItem(i);
                if (tooltipItem == null) continue;

                tooltipItem.SetActive(true);

                // 텍스트 설정 (전용 컴포넌트 KeywordTooltipDisplay 우선 연동)
                KeywordTooltipDisplay tooltipComp = tooltipItem.GetComponent<KeywordTooltipDisplay>();
                if (tooltipComp != null)
                {
                    tooltipComp.SetText(tooltips[i]);
                }
                else
                {
                    TextMeshProUGUI tmp = tooltipItem.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = tooltips[i];
                    }
                }

                RectTransform itemRect = tooltipItem.GetComponent<RectTransform>();
                if (itemRect != null)
                {
                    if (autoAdjustPivot)
                    {
                        itemRect.pivot = placeOnRight ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                    }

                    // 즉시 레이아웃 강제 갱신 후 실제 선호 가로/세로 크기 측정
                    LayoutRebuilder.ForceRebuildLayoutImmediate(itemRect);
                    float itemWidth = LayoutUtility.GetPreferredWidth(itemRect);
                    if (itemWidth <= 0f) itemWidth = itemRect.rect.width;
                    if (itemWidth <= 0f) itemWidth = 150f;

                    float itemHeight = LayoutUtility.GetPreferredHeight(itemRect);
                    if (itemHeight <= 0f) itemHeight = itemRect.rect.height;
                    if (itemHeight <= 0f) itemHeight = 40f;

                    if (itemWidth > maxColWidth) maxColWidth = itemWidth;

                    // 현재 좌표에 배치
                    itemRect.anchoredPosition = new Vector2(currentColX, currentY);

                    // 다음 행 위치: 현재 창의 실제 높이 + 순수 여백(rowPadding)만큼 아래로 이동
                    currentY -= (itemHeight + rowPadding);
                }
            }

            // 다음 열 위치: 현재 열에서 가장 넓은 창 너비 + 순수 열 여백(columnPadding)만큼 이동
            if (placeOnRight)
            {
                currentColX += (maxColWidth + columnPadding);
            }
            else
            {
                currentColX -= (maxColWidth + columnPadding);
            }
        }
    }

    private GameObject GetOrCreateTooltipItem(int index)
    {
        if (keywordTooltipPrefab == null || keywordTooltipContainer == null) return null;

        while (tooltipPool.Count <= index)
        {
            GameObject newItem = Instantiate(keywordTooltipPrefab, keywordTooltipContainer);
            tooltipPool.Add(newItem);
        }
        return tooltipPool[index];
    }

    // =========================================================
    // 참조 카드(토큰) 처리
    // =========================================================
    private void ShowReferencedCard(CardData data, bool placeOnRight)
    {
        if (refCardPreviewUI == null) return;

        List<string> refCardIds = CardTextFormatter.GetReferencedCardIds(data);
        if (refCardIds == null || refCardIds.Count == 0)
        {
            refCardPreviewUI.gameObject.SetActive(false);
            return;
        }

        CardData refCard = CardTextFormatter.FindCardData(refCardIds[0]);
        if (refCard == null)
        {
            refCardPreviewUI.gameObject.SetActive(false);
            return;
        }

        refCardPreviewUI.Setup(refCard);
        refCardPreviewUI.transform.SetAsLastSibling();
        refCardPreviewUI.gameObject.SetActive(true);

        UpdateRefCardPosition(placeOnRight);
    }

    // =========================================================
    // 위치 계산 및 화면 클램핑
    // =========================================================
    private void EnsureCanvas()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) canvasRect = canvas.GetComponent<RectTransform>();
        }
    }

    private Vector2 CalculateScreenPosition(Vector3 worldPos, bool is3DWorldObject)
    {
        // 3D 하수인 월드 오브젝트인 경우 Camera.main을 통해 실제 화면 픽셀 좌표로 정확히 투영
        if (is3DWorldObject && Camera.main != null)
        {
            Vector3 screen3D = Camera.main.WorldToScreenPoint(worldPos);
            return new Vector2(screen3D.x, screen3D.y);
        }

        // 2D 캔버스 UI 오브젝트(덱 빌더의 슬롯 등)인 경우
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(uiCam, worldPos);
    }

    private void UpdateMainPosition(Vector2 screenPos, bool placeOnRight)
    {
        if (previewRect == null || canvasRect == null) return;

        Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;
        float xDistance = Mathf.Abs(offset.x) > 0 ? Mathf.Abs(offset.x) : 220f;

        Vector2 targetScreenPos;
        targetScreenPos.y = screenPos.y + offset.y;

        if (placeOnRight)
        {
            targetScreenPos.x = screenPos.x + xDistance;
        }
        else
        {
            targetScreenPos.x = screenPos.x - xDistance;
        }

        float halfWidth = (previewRect.rect.width > 0 ? previewRect.rect.width : 200f) * 0.5f;
        float halfHeight = (previewRect.rect.height > 0 ? previewRect.rect.height : 280f) * 0.5f;

        targetScreenPos.x = Mathf.Clamp(targetScreenPos.x, halfWidth + 10f, Screen.width - halfWidth - 10f);
        targetScreenPos.y = Mathf.Clamp(targetScreenPos.y, halfHeight + 10f, Screen.height - halfHeight - 10f);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, targetScreenPos, cam, out Vector2 localPoint))
        {
            previewRect.anchoredPosition = localPoint;
        }
    }

    private void UpdateTooltipContainerPosition(bool placeOnRight)
    {
        if (keywordTooltipContainer == null || previewRect == null) return;

        bool isChildOfCard = keywordTooltipContainer.transform.IsChildOf(previewRect.transform);

        if (placeOnRight)
        {
            // 카드가 오른쪽에 떴을 때 -> 키워드 컨테이너도 카드 우측으로 배치
            if (autoAdjustPivot)
            {
                keywordTooltipContainer.pivot = new Vector2(0f, 1f); // 좌상단 피벗 -> 우측/하단으로 확장
            }

            if (isChildOfCard)
            {
                keywordTooltipContainer.anchoredPosition = tooltipPositionRight;
            }
            else
            {
                keywordTooltipContainer.anchoredPosition = new Vector2(
                    previewRect.anchoredPosition.x + tooltipPositionRight.x,
                    previewRect.anchoredPosition.y + tooltipPositionRight.y
                );
            }
        }
        else
        {
            // 카드가 왼쪽에 떴을 때 -> 키워드 컨테이너도 카드 좌측으로 배치
            if (autoAdjustPivot)
            {
                keywordTooltipContainer.pivot = new Vector2(1f, 1f); // 우상단 피벗 -> 좌측/하단으로 확장
            }

            if (isChildOfCard)
            {
                keywordTooltipContainer.anchoredPosition = tooltipPositionLeft;
            }
            else
            {
                keywordTooltipContainer.anchoredPosition = new Vector2(
                    previewRect.anchoredPosition.x + tooltipPositionLeft.x,
                    previewRect.anchoredPosition.y + tooltipPositionLeft.y
                );
            }
        }
    }

    private void UpdateRefCardPosition(bool placeOnRight)
    {
        if (refCardRect == null || previewRect == null || canvasRect == null) return;

        Camera cam = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        float cardWidth = previewRect.rect.width > 0 ? previewRect.rect.width : 200f;
        float refWidth = refCardRect.rect.width > 0 ? refCardRect.rect.width : cardWidth;

        Vector2 mainCardScreenPos = RectTransformUtility.WorldToScreenPoint(cam, previewRect.position);
        Vector2 targetScreenPos = mainCardScreenPos;

        if (placeOnRight)
        {
            targetScreenPos.x = mainCardScreenPos.x + (cardWidth * 0.5f + refWidth * 0.5f + 15f);
            if (targetScreenPos.x + refWidth * 0.5f > Screen.width - 10f)
            {
                targetScreenPos.x = mainCardScreenPos.x - (cardWidth * 0.5f + refWidth * 0.5f + 15f);
            }
        }
        else
        {
            targetScreenPos.x = mainCardScreenPos.x - (cardWidth * 0.5f + refWidth * 0.5f + 15f);
            if (targetScreenPos.x - refWidth * 0.5f < 10f)
            {
                targetScreenPos.x = mainCardScreenPos.x + (cardWidth * 0.5f + refWidth * 0.5f + 15f);
            }
        }

        float halfWidth = refWidth * 0.5f;
        float halfHeight = (refCardRect.rect.height > 0 ? refCardRect.rect.height : 280f) * 0.5f;

        targetScreenPos.x = Mathf.Clamp(targetScreenPos.x, halfWidth + 10f, Screen.width - halfWidth - 10f);
        targetScreenPos.y = Mathf.Clamp(targetScreenPos.y, halfHeight + 10f, Screen.height - halfHeight - 10f);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, targetScreenPos, cam, out Vector2 localPoint))
        {
            refCardRect.anchoredPosition = localPoint;
        }
    }

    // =========================================================
    // 7. 적용된 버프 목록 (aaa.png 디자인: 메인 카드 하단 배너 나열)
    // =========================================================
    private RectTransform buffContainerRect;
    private readonly List<GameObject> buffItemPool = new List<GameObject>();

    private void EnsureBuffContainer()
    {
        if (buffContainerRect != null) return;
        if (previewCardUI == null) return;

        Transform existing = previewCardUI.transform.Find("BuffListContainer");
        if (existing != null)
        {
            buffContainerRect = existing.GetComponent<RectTransform>();
        }
        else
        {
            GameObject containerObj = new GameObject("BuffListContainer", typeof(RectTransform));
            containerObj.transform.SetParent(previewCardUI.transform, false);
            buffContainerRect = containerObj.GetComponent<RectTransform>();

            // 메인 카드(너비 300) 하단(Anchor 0.5, 0) 기준, 피벗 상단(0.5, 1) -> 아래로만 확장되어 카드가 절대 움직이지 않음
            buffContainerRect.anchorMin = new Vector2(0.5f, 0f);
            buffContainerRect.anchorMax = new Vector2(0.5f, 0f);
            buffContainerRect.pivot = new Vector2(0.5f, 1f);
            buffContainerRect.anchoredPosition = new Vector2(0f, -6f);
            buffContainerRect.sizeDelta = new Vector2(300f, 0f);

            var vlg = containerObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = containerObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private void ShowBuffList(GameCardDisplay sourceCard)
    {
        if (sourceCard == null)
        {
            HideBuffList();
            return;
        }

        List<EnchantmentInfo> enchants = sourceCard.CurrentEntityData?.enchantments;
        if (enchants == null || enchants.Count == 0)
        {
            enchants = sourceCard._cardInfo?.enchantments;
        }

        if (enchants == null || enchants.Count == 0)
        {
            HideBuffList();
            return;
        }

        EnsureBuffContainer();
        if (buffContainerRect == null) return;

        buffContainerRect.gameObject.SetActive(true);

        // 풀링된 기존 아이템 숨김
        foreach (var item in buffItemPool)
        {
            if (item != null) item.SetActive(false);
        }

        for (int i = 0; i < enchants.Count; i++)
        {
            var ench = enchants[i];
            if (ench == null) continue;

            GameObject buffObj;
            if (i < buffItemPool.Count && buffItemPool[i] != null)
            {
                buffObj = buffItemPool[i];
            }
            else
            {
                buffObj = CreateBuffBannerItem(buffContainerRect);
                buffItemPool.Add(buffObj);
            }

            var textComp = buffObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null)
            {
                textComp.text = FormatBuffEntry(ench);
            }

            buffObj.transform.SetAsLastSibling();
            buffObj.SetActive(true);
        }
    }

    private GameObject CreateBuffBannerItem(Transform parent)
    {
        GameObject item = new GameObject("BuffBanner", typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);

        var rt = item.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 36f);

        // 배너 배경 (다크 네이비 반투명)
        var img = item.GetComponent<Image>();
        img.color = new Color(0.06f, 0.11f, 0.18f, 0.94f);

        // 외곽선 (aaa.png 스타일의 밝은 청록/하늘색 테두리)
        var outline = item.AddComponent<Outline>();
        outline.effectColor = new Color(0.38f, 0.72f, 0.95f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 텍스트 오브젝트
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(item.transform, false);

        var textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 2f);
        textRt.offsetMax = new Vector2(-8f, -2f);

        var tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 17f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.color = Color.white;
        var nanumFont = Resources.Load<TMP_FontAsset>("Font/NanumGothic SDF");
        if (nanumFont != null)
        {
            tmp.font = nanumFont;
        }

        return item;
    }

    private string FormatBuffEntry(EnchantmentInfo ench)
    {
        // 시전자 이름 가져오기
        string casterName = ench.sourceCardName;
        if (string.IsNullOrEmpty(casterName) && !string.IsNullOrEmpty(ench.sourceCardId))
        {
            if (ResourceManager.Instance != null && ResourceManager.Instance.TryGetCardData(ench.sourceCardId, out var cd) && cd != null)
            {
                casterName = cd.cardName;
            }
        }
        if (string.IsNullOrEmpty(casterName)) casterName = "효과";

        // 시전자 이름이 5글자 초과 시 말줄임 처리 ('엄청긴이...')
        if (casterName.Length > 5)
        {
            casterName = casterName.Substring(0, 4) + "...";
        }

        // 스탯 수치 버프/디버프 ([축복 +1 / +1], [물에젖음 -1 / -2])
        if (ench.attackMod != 0 || ench.healthMod != 0)
        {
            string atkStr = ench.attackMod >= 0 ? $"+{ench.attackMod}" : $"{ench.attackMod}";
            string hpStr = ench.healthMod >= 0 ? $"+{ench.healthMod}" : $"{ench.healthMod}";
            string colorHex = (ench.attackMod >= 0 && ench.healthMod >= 0) ? "#66FF88" : "#FF6666";
            return $"[{casterName} <color={colorHex}>{atkStr} / {hpStr}</color>]";
        }

        // 키워드 부여 (예: [응원 + "도발"], [모욕 - "질풍"])
        if (ench.effectType == GameEventType.GRANT_KEYWORD || !string.IsNullOrEmpty(ench.grantedKeyword))
        {
            string kw = ench.grantedKeyword;
            return $"[{casterName} <color=#FFDD44>+ \"{kw}\"</color>]";
        }

        // 비용 변동
        if (ench.costMod != 0)
        {
            string costSign = ench.costMod >= 0 ? $"+{ench.costMod}" : $"{ench.costMod}";
            return $"[{casterName} <color=#66CCFF>비용 {costSign}</color>]";
        }

        // 기본 설명 fallback
        string desc = !string.IsNullOrEmpty(ench.description) ? ench.description : "적용됨";
        return $"[{casterName} <color=#66FF88>{desc}</color>]";
    }

    private void HideBuffList()
    {
        if (buffContainerRect != null)
        {
            buffContainerRect.gameObject.SetActive(false);
        }
        foreach (var item in buffItemPool)
        {
            if (item != null) item.SetActive(false);
        }
    }
}
