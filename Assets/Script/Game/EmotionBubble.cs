using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 감정표현(Emote) 말풍선(Speech Bubble) 오브젝트를 제어하는 컴포넌트입니다.
/// emotion.prefab에 부착되어 대사 텍스트 출력, 띠용(Bounce/Pop) 등장 애니메이션, 
/// 일정 시간 후 자동 페이드/축소 및 오브젝트 풀 반환을 담당합니다.
/// </summary>
public class EmotionBubble : MonoBehaviour
{
    [Header("UI 바인딩 (미할당 시 자동 탐색)")]
    public TextMeshProUGUI messageText;

    [Header("애니메이션 설정")]
    [Tooltip("띠용 등장 애니메이션 시간 (초)")]
    public float popDuration = 0.4f;

    [Tooltip("퇴장 축소 애니메이션 시간 (초)")]
    public float hideDuration = 0.2f;

    [Tooltip("등장 이징 (OutBack으로 띠용 튀어나오는 효과)")]
    public Ease popEase = Ease.OutBack;

    [Tooltip("퇴장 이징")]
    public Ease hideEase = Ease.InBack;

    private Vector3 _originalScale = Vector3.one * 10f;
    private bool _isScaleInitialized = false;
    private Coroutine _autoHideRoutine;
    private Action _onHideCallback;
    private Tween _activeTween;

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        if (_onHideCallback == null && (_activeTween == null || !_activeTween.IsActive()))
        {
            gameObject.SetActive(false);
        }
    }

    private void InitializeComponents()
    {
        if (messageText == null)
        {
            messageText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (!_isScaleInitialized && transform.localScale != Vector3.zero)
        {
            _originalScale = transform.localScale;
            _isScaleInitialized = true;
        }
    }

    /// <summary>
    /// 말풍선을 지정된 3D 월드 위치에 띠용 애니메이션과 함께 띄웁니다.
    /// </summary>
    public void Show(string message, Vector3 worldPosition, float duration = 3.0f, Action onHide = null)
    {
        InitializeComponents();

        _onHideCallback = onHide;

        if (messageText != null)
        {
            messageText.text = message;
        }

        transform.position = worldPosition;
        gameObject.SetActive(true);

        // 이전 애니메이션 및 타이머 정리
        transform.DOKill();
        _activeTween = null;
        if (_autoHideRoutine != null) StopCoroutine(_autoHideRoutine);

        // 띠용 등장 연출 (0에서 원래 스케일로 OutBack 이징)
        transform.localScale = Vector3.zero;
        _activeTween = transform.DOScale(_originalScale, popDuration).SetEase(popEase);

        // 일정 시간 후 자동 소멸
        if (duration > 0f && gameObject.activeInHierarchy)
        {
            _autoHideRoutine = StartCoroutine(AutoHideRoutine(duration));
        }
    }

    private IEnumerator AutoHideRoutine(float delay)
    {
        yield return YieldInstructionCache.WaitForSeconds(delay);

        // 퇴장 애니메이션
        transform.DOKill();
        _activeTween = transform.DOScale(Vector3.zero, hideDuration)
            .SetEase(hideEase)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                _activeTween = null;
                _onHideCallback?.Invoke();
                _onHideCallback = null;
            });
    }

    /// <summary>
    /// 대사 진행 중 재클릭이나 새로운 감정표현 등으로 말풍선을 즉시 닫을 때 호출합니다.
    /// </summary>
    public void HideImmediate()
    {
        if (_autoHideRoutine != null)
        {
            StopCoroutine(_autoHideRoutine);
            _autoHideRoutine = null;
        }

        transform.DOKill();
        _activeTween = null;

        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);

        var callback = _onHideCallback;
        _onHideCallback = null;
        callback?.Invoke();
    }
}
