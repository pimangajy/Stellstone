using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Firebase.Auth;

/// <summary>
/// 모든 씬에서 팝업 스택 및 전역 설정창(소리, 화면, 계정)을 총괄하는 UI 매니저입니다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    #region 1. 팝업 관리 변수
    // 열려있는 팝업들을 관리할 리스트 (스택 방식)
    private List<GameObject> openPopups = new List<GameObject>();
    #endregion

    #region 2. 설정창 UI 연결 (단일 패널)
    [Header("1. 설정창 팝업")]
    [Tooltip("설정창 전체 패널 GameObject")]
    public GameObject settingsPopup;
    [Tooltip("설정창 열기 버튼")]
    public Button settingsOpenButton;
    [Tooltip("설정창 닫기 버튼")]
    public Button settingsCloseButton;

    [Header("2. 소리 설정 UI")]
    public Slider masterVolumeSlider;
    public Slider bgmVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider voiceVolumeSlider;

    [Header("3. 화면 설정 UI")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    [Header("4. 계정 및 시스템 UI")]
    public TextMeshProUGUI accountInfoText;
    [Tooltip("항복 버튼 (배틀 씬에서만 활성화)")]
    public Button surrenderButton;
    public Button logoutButton;
    public Button quitGameButton;
    #endregion

    public SceneLoader sceneLoader;

    // 자주 사용하는 16:9 규격 대표 해상도 4종 프리셋 (창모드/전체화면 최적화)
    private readonly (int width, int height)[] _presetResolutions = new (int, int)[]
    {
        (1280, 720),
        (1600, 900),
        (1920, 1080),
        (2560, 1440)
    };

    private List<Resolution> _filteredResolutions = new List<Resolution>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitSettingsUIEvents();
        InitResolutionDropdown();
        LoadAudioSettings();

        if (settingsPopup != null)
        {
            settingsPopup.SetActive(false);
        }

        UpdateSurrenderButton(SceneManager.GetActiveScene().name);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 변경되면 열려있던 일반 팝업 목록 정리
        ClearPopupList();
        UpdateSurrenderButton(scene.name);
    }

    private void Update()
    {
        // ESC 키 입력 처리
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 1. 열려있는 일반 팝업이 있다면 가장 최근 팝업 닫기
            if (openPopups.Count > 0)
            {
                ClosePopup();
            }
            // 2. 열린 팝업이 없다면 설정창 토글
            else if (settingsPopup != null)
            {
                ToggleSettingsPopup();
            }
        }
    }

    #region 3. 팝업 스택 제어
    /// <summary>
    /// 팝업을 열고 리스트에 추가합니다.
    /// </summary>
    public void OpenPopup(GameObject popupObject)
    {
        if (popupObject == null) return;

        popupObject.SetActive(true);
        if (!openPopups.Contains(popupObject))
        {
            openPopups.Add(popupObject);
        }

        // 설정창이 열릴 경우 최신 계정 정보 갱신
        if (popupObject == settingsPopup)
        {
            UpdateAccountInfo();
        }
    }

    /// <summary>
    /// (ESC 키용) 가장 마지막에 연 팝업을 닫습니다.
    /// </summary>
    public void ClosePopup()
    {
        if (openPopups.Count == 0) return;

        int lastIndex = openPopups.Count - 1;
        GameObject popupToClose = openPopups[lastIndex];
        openPopups.RemoveAt(lastIndex);

        ClosePopupObject(popupToClose);
    }

    /// <summary>
    /// 특정 팝업을 닫고 리스트에서 제거합니다.
    /// </summary>
    public void CloseSpecificPopup(GameObject popupToClose)
    {
        if (popupToClose == null) return;

        if (openPopups.Contains(popupToClose))
        {
            openPopups.Remove(popupToClose);
        }

        ClosePopupObject(popupToClose);
    }

    /// <summary>
    /// 설정창 팝업을 열거나 닫습니다.
    /// </summary>
    public void ToggleSettingsPopup()
    {
        if (settingsPopup == null) return;

        if (settingsPopup.activeSelf)
        {
            CloseSpecificPopup(settingsPopup);
        }
        else
        {
            OpenPopup(settingsPopup);
        }
    }

    /// <summary>
    /// 열려있는 모든 팝업을 닫습니다.
    /// </summary>
    public void ClearPopupList()
    {
        foreach (var popup in openPopups)
        {
            if (popup != null)
            {
                popup.SetActive(false);
            }
        }
        openPopups.Clear();
    }

    private void ClosePopupObject(GameObject popupObject)
    {
        if (popupObject == null) return;

        UIPanelToggler toggler = popupObject.GetComponent<UIPanelToggler>();
        if (toggler != null)
        {
            toggler.HidePanel();
        }
        else
        {
            popupObject.SetActive(false);
        }
    }
    #endregion

    #region 4. 설정창 UI 이벤트 연결
    private void InitSettingsUIEvents()
    {
        // 열기 버튼
        if (settingsOpenButton != null)
        {
            settingsOpenButton.onClick.AddListener(() => ToggleSettingsPopup());
        }

        // 닫기 버튼
        if (settingsCloseButton != null)
        {
            settingsCloseButton.onClick.AddListener(() => CloseSpecificPopup(settingsPopup));
        }

        // 볼륨 슬라이더 연결
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (bgmVolumeSlider != null) bgmVolumeSlider.onValueChanged.AddListener(SetBGMVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        if (voiceVolumeSlider != null) voiceVolumeSlider.onValueChanged.AddListener(SetVoiceVolume);

        // 화면 설정 연결
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnWindowedToggleChanged);

        // 계정 및 시스템 버튼 연결
        if (surrenderButton != null)
        {
            surrenderButton.onClick.RemoveAllListeners();
            surrenderButton.onClick.AddListener(OnSurrenderClicked);
        }
        if (logoutButton != null) logoutButton.onClick.AddListener(OnLogoutClicked);
        if (quitGameButton != null) quitGameButton.onClick.AddListener(OnQuitGameClicked);
    }
    #endregion

    #region 5. 소리 설정 로직
    public void SetMasterVolume(float volume)
    {
        AudioListener.volume = volume;
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetMasterVolume(volume);
        }
        else
        {
            PlayerPrefs.SetFloat("Sound_MasterVolume", volume);
            PlayerPrefs.SetFloat("MasterVolume", volume);
            PlayerPrefs.Save();
        }
    }

    public void SetBGMVolume(float volume)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetBGMVolume(volume);
        }
        else
        {
            PlayerPrefs.SetFloat("Sound_BGMVolume", volume);
            PlayerPrefs.SetFloat("BGMVolume", volume);
            PlayerPrefs.Save();
        }
    }

    public void SetSFXVolume(float volume)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetSFXVolume(volume);
        }
        else
        {
            PlayerPrefs.SetFloat("Sound_SFXVolume", volume);
            PlayerPrefs.SetFloat("SFXVolume", volume);
            PlayerPrefs.Save();
        }
    }

    public void SetVoiceVolume(float volume)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetVoiceVolume(volume);
        }
        else
        {
            PlayerPrefs.SetFloat("Sound_VoiceVolume", volume);
            PlayerPrefs.SetFloat("VoiceVolume", volume);
            PlayerPrefs.Save();
        }
    }

    private void LoadAudioSettings()
    {
        float master = PlayerPrefs.GetFloat("Sound_MasterVolume", PlayerPrefs.GetFloat("MasterVolume", 1f));
        float bgm = PlayerPrefs.GetFloat("Sound_BGMVolume", PlayerPrefs.GetFloat("BGMVolume", 0.8f));
        float sfx = PlayerPrefs.GetFloat("Sound_SFXVolume", PlayerPrefs.GetFloat("SFXVolume", 1f));
        float voice = PlayerPrefs.GetFloat("Sound_VoiceVolume", PlayerPrefs.GetFloat("VoiceVolume", 1f));

        if (masterVolumeSlider != null) masterVolumeSlider.value = master;
        if (bgmVolumeSlider != null) bgmVolumeSlider.value = bgm;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfx;
        if (voiceVolumeSlider != null) voiceVolumeSlider.value = voice;

        AudioListener.volume = master;
    }
    #endregion

    #region 6. 화면 설정 로직
    private void InitResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        resolutionDropdown.ClearOptions();
        _filteredResolutions.Clear();

        List<string> options = new List<string>();

        // 주요 16:9 규격 4종 등록
        for (int i = 0; i < _presetResolutions.Length; i++)
        {
            var preset = _presetResolutions[i];
            Resolution res = new Resolution { width = preset.width, height = preset.height };
            _filteredResolutions.Add(res);
            options.Add($"{preset.width} x {preset.height}");
        }

        resolutionDropdown.AddOptions(options);

        // 첫 시작 시 기본값: 전체화면(창모드 토글 OFF: isWindowed = false) 및 1920x1080
        bool hasSavedSettings = PlayerPrefs.HasKey("IsWindowed") || PlayerPrefs.HasKey("IsFullscreen");
        bool isWindowed = false;

        if (PlayerPrefs.HasKey("IsWindowed"))
        {
            isWindowed = PlayerPrefs.GetInt("IsWindowed", 0) == 1;
        }
        else if (PlayerPrefs.HasKey("IsFullscreen"))
        {
            isWindowed = PlayerPrefs.GetInt("IsFullscreen", 1) == 0;
        }

        int defaultWidth = isWindowed ? 1600 : 1920;
        int defaultHeight = isWindowed ? 900 : 1080;
        int savedWidth = PlayerPrefs.GetInt("ResolutionWidth", defaultWidth);
        int savedHeight = PlayerPrefs.GetInt("ResolutionHeight", defaultHeight);

        // 첫 시작 시 전체화면 1920x1080으로 초기화 및 저장
        if (!hasSavedSettings)
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
            PlayerPrefs.SetInt("IsWindowed", 0);
            PlayerPrefs.SetInt("IsFullscreen", 1);
            PlayerPrefs.SetInt("ResolutionWidth", 1920);
            PlayerPrefs.SetInt("ResolutionHeight", 1080);
            PlayerPrefs.Save();
            isWindowed = false;
            savedWidth = 1920;
            savedHeight = 1080;
        }
        else
        {
            FullScreenMode mode = isWindowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            Screen.SetResolution(savedWidth, savedHeight, mode);
        }

        UpdateDropdownSelection(savedWidth, savedHeight);

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(isWindowed);
        }
    }

    private void UpdateDropdownSelection(int width, int height)
    {
        if (resolutionDropdown == null) return;

        int selectedIndex = -1;
        for (int i = 0; i < _filteredResolutions.Count; i++)
        {
            if (_filteredResolutions[i].width == width && _filteredResolutions[i].height == height)
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex >= 0)
        {
            resolutionDropdown.SetValueWithoutNotify(selectedIndex);
            resolutionDropdown.RefreshShownValue();
        }
    }

    public void SetResolution(int resolutionIndex)
    {
        if (resolutionIndex < 0 || resolutionIndex >= _filteredResolutions.Count) return;

        Resolution resolution = _filteredResolutions[resolutionIndex];
        bool isWindowed = fullscreenToggle != null ? fullscreenToggle.isOn : (PlayerPrefs.GetInt("IsWindowed", 0) == 1);
        FullScreenMode mode = isWindowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;

        Screen.SetResolution(resolution.width, resolution.height, mode);

        PlayerPrefs.SetInt("ResolutionWidth", resolution.width);
        PlayerPrefs.SetInt("ResolutionHeight", resolution.height);
        PlayerPrefs.Save();

        Debug.Log($"[UIManager] 해상도 수동 변경: {resolution.width}x{resolution.height} (창모드: {isWindowed})");
    }

    /// <summary>
    /// 창모드 토글 변경 시 호출 (체크 시 1600x900 창모드 자동 전환 / 해제 시 1920x1080 전체화면 자동 복귀)
    /// </summary>
    public void OnWindowedToggleChanged(bool isWindowed)
    {
        int targetWidth = isWindowed ? 1600 : 1920;
        int targetHeight = isWindowed ? 900 : 1080;

        // 드롭다운 UI 인덱스도 자동 변경 (1600x900 or 1920x1080)
        UpdateDropdownSelection(targetWidth, targetHeight);

        FullScreenMode mode = isWindowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        Screen.SetResolution(targetWidth, targetHeight, mode);

        PlayerPrefs.SetInt("IsWindowed", isWindowed ? 1 : 0);
        PlayerPrefs.SetInt("IsFullscreen", isWindowed ? 0 : 1);
        PlayerPrefs.SetInt("ResolutionWidth", targetWidth);
        PlayerPrefs.SetInt("ResolutionHeight", targetHeight);
        PlayerPrefs.Save();

        Debug.Log($"[UIManager] 화면 모드 변경: {(isWindowed ? "창모드" : "전체화면")} ({targetWidth}x{targetHeight})");
    }

    /// <summary>
    /// 하위 호환용 전체화면 메서드 (외부 호출 대비)
    /// </summary>
    public void SetFullscreen(bool isFullscreen)
    {
        OnWindowedToggleChanged(!isFullscreen);
    }
    #endregion

    #region 7. 계정 및 시스템 로직
    private void UpdateAccountInfo()
    {
        if (accountInfoText == null) return;

        FirebaseUser user = FirebaseAuth.DefaultInstance?.CurrentUser;
        string username = SinginManager.CurrentUserData?.username ?? "사용자";
        string uid = user != null ? user.UserId : "비로그인 상태";

        accountInfoText.text = $"<b>닉네임:</b> {username}\n<b>UID:</b> {uid}";
    }

    public void OnLogoutClicked()
    {
        Debug.Log("[UIManager] 로그아웃 요청");

        // Firebase 로그아웃
        FirebaseAuth.DefaultInstance?.SignOut();
        PlayerPrefs.DeleteKey("CurrentUserId");
        PlayerPrefs.Save();
        SinginManager.CurrentUserData = null;

        // 설정창 닫고 로그인 씬으로 이동
        ClearPopupList();
        if (settingsPopup != null) settingsPopup.SetActive(false);

        SceneManager.LoadScene("Login");
    }

    public void OnQuitGameClicked()
    {
        Debug.Log("[UIManager] 게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 배틀 씬에서 설정창 항복 버튼 클릭 시 호출됩니다.
    /// </summary>
    public void OnSurrenderClicked()
    {
        Debug.Log("[UIManager] 설정창 항복 버튼 클릭 -> 항복 요청 전송");

        // 1. 현재 씬에 SurrenderManager가 존재하면 호출
        SurrenderManager surrenderMgr = FindFirstObjectByType<SurrenderManager>();
        if (surrenderMgr != null)
        {
            surrenderMgr.Surrender();
        }
        // 2. SurrenderManager가 없더라도 GameClient를 통해 직접 항복 요청(CONCEDE) 전송
        else if (GameClient.Instance != null)
        {
            GameClient.Instance.SendConcedeRequest();
        }

        // 설정창 팝업 닫기
        if (settingsPopup != null)
        {
            CloseSpecificPopup(settingsPopup);
        }
    }

    /// <summary>
    /// 씬 이름에 따라 항복 버튼의 활성화 여부를 갱신합니다. (배틀 씬에서만 활성화)
    /// </summary>
    public void UpdateSurrenderButton(string sceneName)
    {
        if (surrenderButton != null)
        {
            bool isBattle = !string.IsNullOrEmpty(sceneName) &&
                            sceneName.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0;
            surrenderButton.gameObject.SetActive(isBattle);
            Debug.Log($"[UIManager] 씬 '{sceneName}' 설정창 항복 버튼 활성화: {isBattle}");
        }
    }
    #endregion
}
