using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class UIPanelToggler : MonoBehaviour
{
    // --- 애니메이션 타입 정의 ---
    public enum AnimationType
    {
        Instant,      // 즉시 켜고 끄기
        FadeAndScale,  // 부드러운 팝업 효과
        MoveFromPosition, // 지정된 위치에서 이동하는 효과
        AnimatePanelSize
    }

    [Header("애니메이션 타입")]
    [Tooltip("패널이 나타나고 사라질 때의 방식을 선택합니다.")]
    public AnimationType animationType = AnimationType.FadeAndScale;

    [Header("애니메이션 대상")]
    [Tooltip("애니메이션을 적용할 패널 오브젝트입니다.")]
    public GameObject panelObject;
    [Tooltip("패널 뒤의 반투명 배경 오브젝트입니다. (선택 사항)")]
    public CanvasGroup backgroundOverlay;

    [Header("팝업 & 이동 애니메이션 설정")]
    [Tooltip("애니메이션이 지속되는 시간입니다.")]
    public float animationDuration = 0.3f;
    [Tooltip("나타날 때 시작할 크기 배율입니다. (FadeAndScale 전용)")]
    public float startScale = 0.9f;
    [Tooltip("애니메이션에 적용될 Easing 함수입니다.")]
    public Ease easeType = Ease.OutQuad;
    [Tooltip("패널이 이동을 시작할 위치입니다. (MoveFromPosition 전용)")]
    public RectTransform startPosition; // 이동 시작 위치

    [Header("크기 조절 애니메이션 설정")]
    [Tooltip("크기를 키울 목표치입니다. (너비, 높이)")]
    public Vector2 targetSize = new Vector2(550, 650);
    [Tooltip("크기 조절 애니메이션이 지속되는 시간입니다.")]
    public float resizeDuration = 0.5f;
    [Tooltip("크기 조절 애니메이션에 적용될 Easing 함수입니다.")]
    public Ease resizeEaseType = Ease.OutBack;

    private CanvasGroup panelCanvasGroup;
    private RectTransform panelRectTransform;
    public bool hidengUI = true;      // 시작할때 숨겨지는 ui인지 확인 
    private Vector2 originalPosition; // 패널의 원래 위치를 저장할 변수
    private Vector2 originalSize;     // 패널의 원래 크기

    void Awake()
    {
        // 시작 시 패널이 보이지 않도록 확실하게 비활성화합니다.
        if (panelObject != null)
        {
            panelCanvasGroup = panelObject.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = panelObject.AddComponent<CanvasGroup>();
            }

            // RectTransform 컴포넌트 가져오기 및 원래 위치 저장
            panelRectTransform = panelObject.GetComponent<RectTransform>();
            if (panelRectTransform != null)
            {
                originalPosition = panelRectTransform.anchoredPosition;
                originalSize = panelRectTransform.sizeDelta;
            }
        }

        if(hidengUI)
        {
            panelObject.SetActive(false);
        }

        if (backgroundOverlay != null)
        {
            backgroundOverlay.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 패널을 보여줍니다. (UI 버튼의 OnClick 이벤트에 연결)
    /// </summary>
    public void ShowPanel()
    {
        if (panelObject == null) return;

        UIManager.Instance.OpenPopup(panelObject.gameObject);

        // 선택된 애니메이션 타입에 따라 다른 함수를 실행합니다.
        switch (animationType)
        {
            case AnimationType.Instant:
                ShowInstant();
                break;
            case AnimationType.FadeAndScale:
                ShowAnimated();
                break;
            case AnimationType.MoveFromPosition:
                ShowMoveAnimated();
                break;
            case AnimationType.AnimatePanelSize:
                ExpandPanel();
                break;
        }
    }

    /// <summary>
    /// 패널을 숨깁니다. (패널 안의 닫기/확인 버튼에 연결)
    /// </summary>
    public void HidePanel()
    {
        if (panelObject == null) return;

        switch (animationType)
        {
            case AnimationType.Instant:
                HideInstant();
                break;
            case AnimationType.FadeAndScale:
                HideAnimated();
                break;
            case AnimationType.MoveFromPosition:
                HideMoveAnimated();
                break;
            case AnimationType.AnimatePanelSize:
                ShrinkPanel();
                break;
        }
    }

    // --- 애니메이션 방식별 실제 구현 함수 ---

    private void ShowInstant()
    {
        if (backgroundOverlay != null) backgroundOverlay.gameObject.SetActive(true);
        panelObject.SetActive(true);
    }

    private void HideInstant()
    {
        if (backgroundOverlay != null) backgroundOverlay.gameObject.SetActive(false);
        panelObject.SetActive(false);
    }

    private void ShowAnimated()
    {
        if (backgroundOverlay != null)
        {
            backgroundOverlay.gameObject.SetActive(true);
            backgroundOverlay.alpha = 0;
            backgroundOverlay.DOFade(1, animationDuration);
        }

        panelObject.SetActive(true);
        panelCanvasGroup.alpha = 0;
        panelObject.transform.localScale = Vector3.one * startScale;

        panelObject.transform.DOScale(1f, animationDuration).SetEase(easeType);
        panelCanvasGroup.DOFade(1, animationDuration).SetEase(easeType);
    }

    private void HideAnimated()
    {
        if (backgroundOverlay != null)
        {
            backgroundOverlay.DOFade(0, animationDuration);
        }

        panelObject.transform.DOScale(startScale, animationDuration).SetEase(easeType);
        panelCanvasGroup.DOFade(0, animationDuration).SetEase(easeType)
            .OnComplete(() => {
                panelObject.SetActive(false);
                if (backgroundOverlay != null)
                {
                    backgroundOverlay.gameObject.SetActive(false);
                }
            });
    }

    public void ExpandPanel()
    {
        // [수정] isResizing 체크를 제거하고 DOKill()을 추가합니다.
        if (panelRectTransform == null) return;

        // 이전에 실행 중이던 크기 조절 애니메이션(닫기 등)이 있다면 즉시 중단합니다.
        panelRectTransform.DOKill();

        panelRectTransform.DOSizeDelta(targetSize, resizeDuration).SetEase(resizeEaseType);
    }

    public void ShrinkPanel()
    {
        // [수정] isResizing 체크를 제거하고 DOKill()을 추가합니다.
        if (panelRectTransform == null) return;

        // 이전에 실행 중이던 크기 조절 애니메이션(열기 등)이 있다면 즉시 중단합니다.
        panelRectTransform.DOKill();

        panelRectTransform.DOSizeDelta(originalSize, resizeDuration).SetEase(resizeEaseType)
            .OnComplete(() => {
                // [추가] 애니메이션이 완료된 후 패널을 비활성화합니다.
                // UIManager는 HidePanel() 호출만 담당하므로, 비활성화 처리는 여기서 해야 합니다.
                //panelObject.SetActive(false);
                if (backgroundOverlay != null)
                {
                    backgroundOverlay.gameObject.SetActive(false);
                }
            });
    }

    private void ShowMoveAnimated()
    {
        if (panelRectTransform == null || startPosition == null)
        {
            Debug.LogWarning("MoveFromPosition 애니메이션을 사용하려면 Panel Object의 RectTransform과 Start Position이 설정되어야 합니다. Instant 방식으로 대체합니다.");
            ShowInstant();
            return;
        }

        if (backgroundOverlay != null)
        {
            backgroundOverlay.gameObject.SetActive(true);
            backgroundOverlay.alpha = 0;
            backgroundOverlay.DOFade(1, animationDuration);
        }

        panelObject.SetActive(true);
        //panelCanvasGroup.alpha = 0;
        // 패널을 시작 위치로 즉시 이동
        panelRectTransform.anchoredPosition = startPosition.anchoredPosition;

        // 원래 위치로 이동하면서 투명도 조절
        panelRectTransform.DOAnchorPos(originalPosition, animationDuration).SetEase(easeType);
        //panelCanvasGroup.DOFade(1, animationDuration).SetEase(easeType);
    }

    private void HideMoveAnimated()
    {
        if (panelRectTransform == null || startPosition == null)
        {
            Debug.LogWarning("MoveFromPosition 애니메이션을 사용하려면 Panel Object의 RectTransform과 Start Position이 설정되어야 합니다. Instant 방식으로 대체합니다.");
            HideInstant();
            return;
        }

        if (backgroundOverlay != null)
        {
            backgroundOverlay.DOFade(0, animationDuration);
        }

        // 시작 위치로 이동하면서 투명도 조절
        panelRectTransform.DOAnchorPos(startPosition.anchoredPosition, animationDuration).SetEase(easeType)

            // [수정] 주석 처리된 OnComplete를 활성화하고 UIManager 로직에 맞게 수정합니다.
            // HideAnimated와 마찬가지로, 애니메이션 완료 후 비활성화 처리가 필요합니다.
            .OnComplete(() => {
                panelObject.SetActive(false);
                if (backgroundOverlay != null)
                {
                    backgroundOverlay.gameObject.SetActive(false);
                }
                // 애니메이션이 끝난 후 패널을 원래 위치로 되돌려 놓습니다.
                panelRectTransform.anchoredPosition = originalPosition;
            });

        // [수정] OnComplete 밖으로 나와 있던 코드를 OnComplete 내부로 이동시켰습니다.
        // panelRectTransform.anchoredPosition = originalPosition;
    }
}
