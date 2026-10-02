using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 개별 감정표현(Emote) 버튼을 제어하는 컴포넌트입니다.
/// 리더 주위에 원형으로 소환/배치되며, 클릭 시 감정표현 매니저에 이벤트를 전달합니다.
/// emotionButton.prefab에 부착되어 사용됩니다.
/// </summary>
public class EmotionButton : MonoBehaviour
{
    [Header("감정표현 설정")]
    [Tooltip("감정표현 고유 식별자 (예: GREETING, THANKS, SORRY, WELL_PLAYED, THINKING, SURPRISED 등)")]
    public string emoteId = "GREETING";

    [Tooltip("버튼에 표시될 짧은 라벨 (예: 인사, 감사, 도발 등)")]
    public string buttonLabel = "인사";

    [Tooltip("말풍선(emotion)에 출력될 대사")]
    public string emoteMessage = "안녕하세요!";

    [Header("UI 컴포넌트 바인딩 (자동 탐색)")]
    public Button button;
    public TextMeshProUGUI text;

    private Action<EmotionButton> _onClickCallback;
    private Vector3 _originalScale = Vector3.one * 10f;
    private bool _isScaleInitialized = false;
    private Tween _activeTween;

    private void Awake()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        if (button == null)
        {
            button = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);
        }

        if (text == null)
        {
            text = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
        {
            if (canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }
            canvas.sortingOrder = 10;
        }

        if (!_isScaleInitialized && transform.localScale != Vector3.zero)
        {
            _originalScale = transform.localScale;
            _isScaleInitialized = true;
        }

        if (text != null)
        {
            string displayText = !string.IsNullOrEmpty(buttonLabel) ? buttonLabel : emoteMessage;
            if (!string.IsNullOrEmpty(displayText))
            {
                text.text = displayText;
            }
        }

        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }
    }

    /// <summary>
    /// 풀에서 꺼내어 감정표현 데이터(ID, 대사, 버튼 라벨) 및 클릭 콜백을 설정합니다.
    /// </summary>
    public void Setup(string id, string message, string label, Action<EmotionButton> onClickCallback)
    {
        InitializeComponents();

        this.emoteId = id;
        this.emoteMessage = message;
        this.buttonLabel = label;
        this._onClickCallback = onClickCallback;

        if (text != null)
        {
            text.text = !string.IsNullOrEmpty(label) ? label : message;
        }
    }

    /// <summary>
    /// 풀에서 꺼내어 감정표현 데이터 및 클릭 콜백을 설정합니다. (라벨 미지정 시 대사를 버튼에 표시)
    /// </summary>
    public void Setup(string id, string message, Action<EmotionButton> onClickCallback)
    {
        Setup(id, message, null, onClickCallback);
    }

    /// <summary>
    /// 원형 배치 위치로 팝업(띠용) 등장 애니메이션 실행
    /// </summary>
    public void PlaySpawnAnimation(Vector3 targetPos, float delay = 0f)
    {
        InitializeComponents();

        transform.position = targetPos;
        gameObject.SetActive(true);

        transform.DOKill();
        _activeTween = null;

        transform.localScale = Vector3.zero;
        _activeTween = transform.DOScale(_originalScale, 0.35f)
            .SetEase(Ease.OutBack)
            .SetDelay(delay);
    }

    /// <summary>
    /// 지정 위치 및 회전값으로 팝업(띠용) 등장 애니메이션 실행
    /// </summary>
    public void PlaySpawnAnimation(Vector3 targetPos, Quaternion targetRot, float delay = 0f)
    {
        transform.rotation = targetRot;
        PlaySpawnAnimation(targetPos, delay);
    }

    /// <summary>
    /// 버튼이 사라질 때의 축소 퇴장 애니메이션
    /// </summary>
    public void PlayDespawnAnimation(Action onComplete = null)
    {
        transform.DOKill();
        _activeTween = transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                _activeTween = null;
                onComplete?.Invoke();
            });
    }

    /// <summary>
    /// 즉시 비활성화 및 스케일 초기화
    /// </summary>
    public void HideImmediate()
    {
        transform.DOKill();
        _activeTween = null;
        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    private void OnClick()
    {
        _onClickCallback?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
    }
}
