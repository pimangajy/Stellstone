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
        FadeAndScale  // 부드러운 팝업 효과
    }

    [Header("애니메이션 타입")]
    [Tooltip("패널이 나타나고 사라질 때의 방식을 선택합니다.")]
    public AnimationType animationType = AnimationType.FadeAndScale;

    [Header("애니메이션 대상")]
    [Tooltip("애니메이션을 적용할 패널 오브젝트입니다.")]
    public GameObject panelObject;
    [Tooltip("패널 뒤의 반투명 배경 오브젝트입니다. (선택 사항)")]
    public CanvasGroup backgroundOverlay;

    [Header("팝업 애니메이션 설정")]
    [Tooltip("애니메이션이 지속되는 시간입니다.")]
    public float animationDuration = 0.3f;
    [Tooltip("나타날 때 시작할 크기 배율입니다.")]
    public float startScale = 0.9f;
    [Tooltip("애니메이션에 적용될 Easing 함수입니다.")]
    public Ease easeType = Ease.OutQuad;

    private CanvasGroup panelCanvasGroup;

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

        // 선택된 애니메이션 타입에 따라 다른 함수를 실행합니다.
        switch (animationType)
        {
            case AnimationType.Instant:
                ShowInstant();
                break;
            case AnimationType.FadeAndScale:
                ShowAnimated();
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
}
