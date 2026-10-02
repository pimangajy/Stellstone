using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 버튼 클릭 시 SoundManager에 설정된 사운드를 자동으로 재생해 주는 UI 컴포넌트입니다.
/// 인스펙터에서 buttonType(메뉴, 확인, 취소 등)만 선택해 주면 동작합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler
{
    [Header("UI 버튼 사운드 설정")]
    [Tooltip("버튼의 역할(enum)을 선택하면 SoundManager에 등록된 해당 사운드가 자동으로 재생됩니다.")]
    public UIButtonSoundType buttonType = UIButtonSoundType.Default;

    [Header("선택 옵션")]
    [Tooltip("연속 클릭 시 기계적인 느낌을 줄이기 위해 미세하게 피치를 흔듭니다 (±3%).")]
    [SerializeField] private bool randomizePitch = true;

    [Tooltip("SoundManager 설정을 무시하고 이 특정 버튼에만 다른 소리를 쓰고 싶을 때 사용 (선택 사항)")]
    [SerializeField] private AudioClip overrideClip;

    [Tooltip("마우스 포인터를 올렸을 때(Hover) 사운드 재생 여부")]
    [SerializeField] private bool playHoverSound = false;
    [SerializeField] private UIButtonSoundType hoverSoundType = UIButtonSoundType.Default;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(OnClick);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnClick);
        }
    }

    /// <summary>
    /// 버튼 클릭 시 자동 호출
    /// </summary>
    public void OnClick()
    {
        // 버튼이 비활성화(클릭 불가) 상태면 소리를 내지 않음
        if (_button != null && !_button.interactable) return;

        float pitch = 1.0f;
        if (randomizePitch)
        {
            pitch += Random.Range(-0.035f, 0.035f);
        }

        // 1. 개별 오버라이드 클립이 지정된 경우 해당 클립 재생
        if (overrideClip != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(overrideClip, 1.0f, pitch);
            return;
        }

        // 2. SoundManager의 UI 사운드 딕셔너리에서 buttonType에 매핑된 사운드 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUISound(buttonType, pitch);
        }
    }

    /// <summary>
    /// 마우스 포인터 진입(Hover) 시
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!playHoverSound) return;
        if (_button != null && !_button.interactable) return;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayUISound(hoverSoundType, 1.15f);
        }
    }
}
