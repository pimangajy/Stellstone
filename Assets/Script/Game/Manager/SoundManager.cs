using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using AYellowpaper.SerializedCollections;

/// <summary>
/// 씬별 자동 BGM 매핑 데이터 클래스
/// </summary>
[System.Serializable]
public class SceneBgmMapping
{
    [Tooltip("씬 이름 (예: Login, MainScene, ShopScene, BattleScene)")]
    public string sceneName;
    [Tooltip("이 씬에 진입할 때 재생할 배경음악")]
    public AudioClip bgmClip;
    [Tooltip("크로스페이드 전환 시간 (초)")]
    public float fadeDuration = 1.0f;
    [Tooltip("반복 재생 여부")]
    public bool loop = true;
}

/// <summary>
/// UI 버튼 및 인터랙션 사운드 유형
/// </summary>
public enum UIButtonSoundType
{
    Default,        // 기본 클릭 (일반 버튼)
    Confirm,        // 확인 / 결정 / 수락
    Cancel,         // 취소 / 닫기 / 뒤로가기
    Menu,           // 메뉴 열기 / 옵션 / 설정
    Tab,            // 탭 / 카테고리 전환
    Warning,        // 경고 / 에러 / 불가능
    Purchase,       // 구매 / 결제 / 강화
    Reward          // 보상 획득 / 팝업 열기
}

/// <summary>
/// 게임 전역에서 BGM, SFX(2D/3D), Voice를 통합 관리하는 싱글톤 사운드 매니저입니다.
/// 볼륨 설정 저장/로드(PlayerPrefs), BGM 크로스페이드, 사운드 풀링, 스팸 방지 및 씬별 자동 BGM 재생 기능을 제공합니다.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("0-1. 씬별 자동 배경음악 매핑")]
    [Tooltip("씬 이름에 따라 자동으로 재생할 배경음악 목록입니다.")]
    public List<SceneBgmMapping> sceneBgmList = new List<SceneBgmMapping>();

    [Header("0-2. UI 버튼 사운드 매핑 (SerializedDictionary)")]
    [Tooltip("버튼 종류(UIButtonSoundType)별로 재생할 사운드 딕셔너리입니다. 인스펙터에서 바로 등록/수정할 수 있습니다.")]
    [SerializedDictionary("버튼 유형", "사운드 클립")]
    public SerializedDictionary<UIButtonSoundType, AudioClip> uiSounds = new SerializedDictionary<UIButtonSoundType, AudioClip>();

    [Header("사운드 풀 설정")]
    [Tooltip("2D 효과음용 동시 재생 풀 크기")]
    [SerializeField] private int sfx2DPoolSize = 15;
    [Tooltip("3D 공간 효과음용 동시 재생 풀 크기")]
    [SerializeField] private int sfx3DPoolSize = 10;

    [Header("볼륨 기본값 (0.0 ~ 1.0)")]
    [Range(0f, 1f)] public float defaultMasterVolume = 1.0f;
    [Range(0f, 1f)] public float defaultBGMVolume = 0.8f;
    [Range(0f, 1f)] public float defaultSFXVolume = 1.0f;
    [Range(0f, 1f)] public float defaultVoiceVolume = 1.0f;

    // 실시간 볼륨 프로퍼티
    public float MasterVolume { get; private set; }
    public float BGMVolume { get; private set; }
    public float SFXVolume { get; private set; }
    public float VoiceVolume { get; private set; }
    public bool IsMuted { get; private set; }

    // BGM 크로스페이드용 듀얼 AudioSource
    private AudioSource _bgmSourceA;
    private AudioSource _bgmSourceB;
    private bool _isUsingSourceA = true;
    private Coroutine _bgmCrossFadeCoroutine;

    // 보이스 전용 AudioSource
    private AudioSource _voiceSource;

    // 2D / 3D 효과음 풀
    private List<AudioSource> _sfx2DPool = new List<AudioSource>();
    private List<AudioSource> _sfx3DPool = new List<AudioSource>();

    // 동일 사운드 겹침(스팸) 방지용 쿨다운 딕셔너리
    private Dictionary<AudioClip, float> _sfxCooldowns = new Dictionary<AudioClip, float>();
    private const float SFX_SPAM_COOLDOWN = 0.04f; // 40ms 쿨다운

    // PlayerPrefs 키
    private const string PREFS_MASTER_VOL = "Sound_MasterVolume";
    private const string PREFS_BGM_VOL = "Sound_BGMVolume";
    private const string PREFS_SFX_VOL = "Sound_SFXVolume";
    private const string PREFS_VOICE_VOL = "Sound_VoiceVolume";
    private const string PREFS_IS_MUTED = "Sound_IsMuted";

    private void Awake()
    {
        // 싱글톤 초기화 & 씬 전환 시 파괴 방지
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 씬이 로드될 때 등록된 씬 BGM 매핑 목록을 확인하여 자동 재생합니다.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (sceneBgmList == null || sceneBgmList.Count == 0) return;

        SceneBgmMapping match = sceneBgmList.Find(x => x.sceneName.Equals(scene.name, StringComparison.OrdinalIgnoreCase));
        if (match != null && match.bgmClip != null)
        {
            Debug.Log($"[SoundManager] 씬 진입 BGM 자동 재생: '{scene.name}' -> '{match.bgmClip.name}'");
            PlayBGM(match.bgmClip, match.fadeDuration, match.loop);
        }
    }

    /// <summary>
    /// AudioSource 컴포넌트 생성 및 볼륨 설정 초기화
    /// </summary>
    private void Initialize()
    {
        // 1. BGM용 AudioSource 2개 생성 (크로스페이드용)
        _bgmSourceA = CreateAudioSource("BGM_Source_A", is2D: true, loop: true);
        _bgmSourceB = CreateAudioSource("BGM_Source_B", is2D: true, loop: true);

        // 2. 보이스용 AudioSource 생성
        _voiceSource = CreateAudioSource("Voice_Source", is2D: true, loop: false);

        // 3. 2D 효과음 풀 생성
        GameObject sfx2DContainer = new GameObject("[SFX_2D_Pool]");
        sfx2DContainer.transform.SetParent(transform);
        for (int i = 0; i < sfx2DPoolSize; i++)
        {
            AudioSource source = sfx2DContainer.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f; // 2D 사운드
            _sfx2DPool.Add(source);
        }

        // 4. 3D 공간 효과음 풀 생성
        GameObject sfx3DContainer = new GameObject("[SFX_3D_Pool]");
        sfx3DContainer.transform.SetParent(transform);
        for (int i = 0; i < sfx3DPoolSize; i++)
        {
            AudioSource source = sfx3DContainer.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f; // 3D 사운드
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 50f;
            _sfx3DPool.Add(source);
        }

        // 5. 저장된 볼륨값 로드
        LoadSettings();
        ApplyVolumeSettings();
    }

    private AudioSource CreateAudioSource(string name, bool is2D, bool loop)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform);
        AudioSource source = obj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = is2D ? 0f : 1f;
        return source;
    }

    // ==================================================================
    // 1. BGM 제어 (크로스페이드, 정지)
    // ==================================================================

    /// <summary>
    /// BGM을 재생합니다. 다른 음악이 재생 중일 경우 자연스럽게 크로스페이드됩니다.
    /// </summary>
    public void PlayBGM(AudioClip clip, float fadeDuration = 1.0f, bool loop = true)
    {
        if (clip == null) return;

        AudioSource currentSource = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;
        AudioSource newSource = _isUsingSourceA ? _bgmSourceB : _bgmSourceA;

        // 이미 동일한 음악이 재생 중이라면 리턴
        if (currentSource.isPlaying && currentSource.clip == clip) return;

        if (_bgmCrossFadeCoroutine != null) StopCoroutine(_bgmCrossFadeCoroutine);
        _bgmCrossFadeCoroutine = StartCoroutine(CrossFadeBGMRoutine(currentSource, newSource, clip, fadeDuration, loop));

        _isUsingSourceA = !_isUsingSourceA;
    }

    private IEnumerator CrossFadeBGMRoutine(AudioSource fadeOutSource, AudioSource fadeInSource, AudioClip newClip, float duration, bool loop)
    {
        fadeInSource.clip = newClip;
        fadeInSource.loop = loop;
        fadeInSource.volume = 0f;
        fadeInSource.Play();

        float targetVolume = GetEffectiveBGMVolume();
        float elapsed = 0f;

        float startVolumeOut = fadeOutSource.isPlaying ? fadeOutSource.volume : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (fadeOutSource.isPlaying)
            {
                fadeOutSource.volume = Mathf.Lerp(startVolumeOut, 0f, t);
            }
            fadeInSource.volume = Mathf.Lerp(0f, targetVolume, t);

            yield return null;
        }

        if (fadeOutSource.isPlaying)
        {
            fadeOutSource.Stop();
            fadeOutSource.clip = null;
        }

        fadeInSource.volume = targetVolume;
        _bgmCrossFadeCoroutine = null;
    }

    /// <summary>
    /// 현재 재생 중인 BGM을 부드럽게 멈춥니다.
    /// </summary>
    public void StopBGM(float fadeDuration = 1.0f)
    {
        AudioSource activeSource = _isUsingSourceA ? _bgmSourceA : _bgmSourceB;
        if (activeSource.isPlaying)
        {
            if (_bgmCrossFadeCoroutine != null) StopCoroutine(_bgmCrossFadeCoroutine);
            _bgmCrossFadeCoroutine = StartCoroutine(FadeOutBGMRoutine(activeSource, fadeDuration));
        }
    }

    private IEnumerator FadeOutBGMRoutine(AudioSource source, float duration)
    {
        float startVol = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            yield return null;
        }

        source.Stop();
        source.clip = null;
        _bgmCrossFadeCoroutine = null;
    }

    // ==================================================================
    // 2. 2D 효과음 (UI, 카드 드로우 등)
    // ==================================================================

    /// <summary>
    /// 2D 효과음을 재생합니다. (풀링 시스템 사용)
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1.0f, float pitch = 1.0f)
    {
        if (clip == null || IsMuted) return;
        if (IsSpamming(clip)) return;

        AudioSource availableSource = GetAvailable2DSource();
        if (availableSource != null)
        {
            availableSource.clip = clip;
            availableSource.volume = GetEffectiveSFXVolume() * Mathf.Clamp01(volumeScale);
            availableSource.pitch = pitch;
            availableSource.Play();
        }
    }

    /// <summary>
    /// UI 버튼 유형(UIButtonSoundType)에 맞는 효과음을 재생합니다.
    /// 해당 타입의 사운드가 비어있으면 Default 클릭음으로 fallback 재생됩니다.
    /// </summary>
    public void PlayUISound(UIButtonSoundType soundType, float pitch = 1.0f)
    {
        if (IsMuted) return;

        AudioClip targetClip = null;

        // 1. 요청된 타입 사운드 조회
        if (!uiSounds.TryGetValue(soundType, out targetClip) || targetClip == null)
        {
            // 2. 사운드가 등록되지 않았거나 비어있으면 Default 클릭음으로 fallback
            uiSounds.TryGetValue(UIButtonSoundType.Default, out targetClip);
        }

        if (targetClip != null)
        {
            PlaySFX(targetClip, 1.0f, pitch);
        }
    }

    /// <summary>
    /// [인스펙터 우클릭 메뉴] 모든 UIButtonSoundType 키를 딕셔너리에 자동으로 추가해 줍니다.
    /// </summary>
    [ContextMenu("모든 UI 사운드 키 자동 추가")]
    public void PopulateDefaultUISoundKeys()
    {
        foreach (UIButtonSoundType type in System.Enum.GetValues(typeof(UIButtonSoundType)))
        {
            if (!uiSounds.ContainsKey(type))
            {
                uiSounds.Add(type, null);
            }
        }
    }

    // ==================================================================
    // 3. 3D 공간 효과음 (필드 타격, 폭발 등)
    // ==================================================================

    /// <summary>
    /// 지정된 3D 월드 좌표에서 공간 입체 효과음을 재생합니다.
    /// </summary>
    public void PlaySFX3D(AudioClip clip, Vector3 worldPosition, float volumeScale = 1.0f, float minDistance = 1f, float maxDistance = 50f)
    {
        if (clip == null || IsMuted) return;
        if (IsSpamming(clip)) return;

        AudioSource availableSource = GetAvailable3DSource();
        if (availableSource != null)
        {
            availableSource.transform.position = worldPosition;
            availableSource.clip = clip;
            availableSource.minDistance = minDistance;
            availableSource.maxDistance = maxDistance;
            availableSource.volume = GetEffectiveSFXVolume() * Mathf.Clamp01(volumeScale);
            availableSource.Play();
        }
    }

    // ==================================================================
    // 4. 보이스 (대사)
    // ==================================================================

    /// <summary>
    /// 캐릭터/카드 보이스 대사를 재생합니다.
    /// </summary>
    public void PlayVoice(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip == null || IsMuted) return;

        _voiceSource.clip = clip;
        _voiceSource.volume = GetEffectiveVoiceVolume() * Mathf.Clamp01(volumeScale);
        _voiceSource.Play();
    }

    public void StopVoice()
    {
        if (_voiceSource != null && _voiceSource.isPlaying)
        {
            _voiceSource.Stop();
        }
    }

    // ==================================================================
    // 5. 볼륨 설정 및 저장 (PlayerPrefs 연동)
    // ==================================================================

    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREFS_MASTER_VOL, MasterVolume);
        ApplyVolumeSettings();
    }

    public void SetBGMVolume(float volume)
    {
        BGMVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREFS_BGM_VOL, BGMVolume);
        ApplyVolumeSettings();
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREFS_SFX_VOL, SFXVolume);
        ApplyVolumeSettings();
    }

    public void SetVoiceVolume(float volume)
    {
        VoiceVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREFS_VOICE_VOL, VoiceVolume);
        ApplyVolumeSettings();
    }

    public void SetMute(bool isMute)
    {
        IsMuted = isMute;
        PlayerPrefs.SetInt(PREFS_IS_MUTED, IsMuted ? 1 : 0);
        ApplyVolumeSettings();
    }

    public void ToggleMute()
    {
        SetMute(!IsMuted);
    }

    private void ApplyVolumeSettings()
    {
        float effBgm = GetEffectiveBGMVolume();
        if (_isUsingSourceA)
        {
            if (_bgmSourceA.isPlaying) _bgmSourceA.volume = effBgm;
        }
        else
        {
            if (_bgmSourceB.isPlaying) _bgmSourceB.volume = effBgm;
        }

        if (_voiceSource.isPlaying)
        {
            _voiceSource.volume = GetEffectiveVoiceVolume();
        }
    }

    private float GetEffectiveBGMVolume() => IsMuted ? 0f : (MasterVolume * BGMVolume);
    private float GetEffectiveSFXVolume() => IsMuted ? 0f : (MasterVolume * SFXVolume);
    private float GetEffectiveVoiceVolume() => IsMuted ? 0f : (MasterVolume * VoiceVolume);

    private void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(PREFS_MASTER_VOL, defaultMasterVolume);
        BGMVolume = PlayerPrefs.GetFloat(PREFS_BGM_VOL, defaultBGMVolume);
        SFXVolume = PlayerPrefs.GetFloat(PREFS_SFX_VOL, defaultSFXVolume);
        VoiceVolume = PlayerPrefs.GetFloat(PREFS_VOICE_VOL, defaultVoiceVolume);
        IsMuted = PlayerPrefs.GetInt(PREFS_IS_MUTED, 0) == 1;
    }

    // ==================================================================
    // 6. 내부 헬퍼 (풀링 & 스팸 방지)
    // ==================================================================

    private AudioSource GetAvailable2DSource()
    {
        for (int i = 0; i < _sfx2DPool.Count; i++)
        {
            if (!_sfx2DPool[i].isPlaying) return _sfx2DPool[i];
        }

        // 모든 소스가 사용 중이면 첫 번째 소스 재사용
        return _sfx2DPool.Count > 0 ? _sfx2DPool[0] : null;
    }

    private AudioSource GetAvailable3DSource()
    {
        for (int i = 0; i < _sfx3DPool.Count; i++)
        {
            if (!_sfx3DPool[i].isPlaying) return _sfx3DPool[i];
        }

        return _sfx3DPool.Count > 0 ? _sfx3DPool[0] : null;
    }

    private bool IsSpamming(AudioClip clip)
    {
        if (clip == null) return false;

        float currentTime = Time.realtimeSinceStartup;
        if (_sfxCooldowns.TryGetValue(clip, out float lastTime))
        {
            if (currentTime - lastTime < SFX_SPAM_COOLDOWN)
            {
                return true; // 스팸 방지: 너무 빠른 연속 재생 차단
            }
        }

        _sfxCooldowns[clip] = currentTime;
        return false;
    }
}
