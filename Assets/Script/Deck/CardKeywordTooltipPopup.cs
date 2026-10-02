using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 덱 편성 및 카드 상세 보기 화면에서 특정 단어(키워드, 특수 속성)에 마우스를 올렸을 때,
/// 해당 단어 바로 위에 간단한 설명을 띄워주는 전용 팝업 컴포넌트입니다.
/// (유저가 직접 제작한 팝업 UI 오브젝트에 부착하여 사용)
/// </summary>
public class CardKeywordTooltipPopup : MonoBehaviour
{
    public static CardKeywordTooltipPopup Instance { get; private set; }

    [Header("UI 연결")]
    [Tooltip("팝업 전체 루트 GameObject (비워둘 경우 이 스크립트가 붙은 GameObject 사용)")]
    [SerializeField] private GameObject popupRoot;

    [Tooltip("키워드 명칭 텍스트 (예: '도발', '속공') - 선택 사항")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("키워드 상세 설명 텍스트")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Tooltip("단일 통합 텍스트 (제목과 설명을 한 텍스트에 노출하고 싶을 때 사용) - 선택 사항")]
    [SerializeField] private TextMeshProUGUI combinedText;

    [Header("위치 및 오프셋")]
    [Tooltip("단어 상단 중앙 기준 위로 띄울 오프셋 (기본: Y + 25)")]
    [SerializeField] private Vector2 positionOffset = new Vector2(0f, 25f);

    [Tooltip("화면 밖으로 팝업이 나가지 않도록 패딩(여백)을 고려하여 클램프할지 여부")]
    [SerializeField] private bool clampToScreen = true;

    [Tooltip("화면 가장자리 여백 (픽셀 단위)")]
    [SerializeField] private float screenPadding = 20f;

    private RectTransform rectTransform;
    private Canvas rootCanvas;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            // 중복 방지 (기존 인스턴스 유지)
            Destroy(gameObject);
            return;
        }

        if (popupRoot == null)
        {
            popupRoot = gameObject;
        }

        rectTransform = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();

        // 시작 시에는 숨김 상태
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 지정된 단어 위치(월드 좌표) 바로 위에 키워드 설명 팝업을 표시합니다.
    /// </summary>
    /// <param name="title">키워드 명칭 (예: 도발)</param>
    /// <param name="description">키워드 상세 설명</param>
    /// <param name="targetWorldPosition">해당 단어의 상단 중앙 월드 좌표</param>
    public void Show(string title, string description, Vector3 targetWorldPosition)
    {
        if (popupRoot == null) popupRoot = gameObject;
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        // 1. 텍스트 바인딩
        if (titleText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(titleText, 10f, 18f, false, TextOverflowModes.Ellipsis);
            titleText.text = title;
        }

        if (descriptionText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(descriptionText, 9f, 16f, true, TextOverflowModes.Ellipsis);
            if (titleText == null && combinedText == null)
            {
                // 제목 텍스트 슬롯이 없으면 설명 텍스트 상단에 볼드로 제목 추가
                descriptionText.text = $"<b>{title}</b>\n{description}";
            }
            else
            {
                descriptionText.text = description;
            }
        }

        if (combinedText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(combinedText, 9f, 16f, true, TextOverflowModes.Ellipsis);
            combinedText.text = $"<b>{title}</b>\n{description}";
        }

        // 2. 팝업 활성화
        popupRoot.SetActive(true);

        // 3. 레이아웃 강제 갱신 (Content Size Fitter가 있을 경우 크기 즉시 반영)
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        // 4. 위치 지정 (단어 상단 중앙 기준)
        rectTransform.position = targetWorldPosition;
        rectTransform.anchoredPosition += positionOffset;

        // 5. 화면 밖 짤림 방지 (클램프)
        if (clampToScreen)
        {
            ClampToScreenBounds();
        }
    }

    /// <summary>
    /// 팝업을 숨깁니다.
    /// </summary>
    public void Hide()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 팝업이 화면 가장자리 밖으로 벗어나지 않도록 좌표를 보정합니다.
    /// </summary>
    private void ClampToScreenBounds()
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        float minX = corners[0].x;
        float maxX = corners[2].x;
        float minY = corners[0].y;
        float maxY = corners[2].y;

        Vector3 shift = Vector3.zero;

        if (minX < screenPadding)
        {
            shift.x = screenPadding - minX;
        }
        else if (maxX > Screen.width - screenPadding)
        {
            shift.x = (Screen.width - screenPadding) - maxX;
        }

        if (minY < screenPadding)
        {
            shift.y = screenPadding - minY;
        }
        else if (maxY > Screen.height - screenPadding)
        {
            shift.y = (Screen.height - screenPadding) - maxY;
        }

        if (shift != Vector3.zero)
        {
            rectTransform.position += shift;
        }
    }
}
