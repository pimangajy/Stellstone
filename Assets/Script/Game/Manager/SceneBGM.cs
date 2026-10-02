using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬별 BGM 플레이리스트를 관리하고, 드롭다운(TMP_Dropdown)을 통해 유저가 원하는 배경음악을 선택할 수 있는 컴포넌트입니다.
/// </summary>
public class SceneBGM : MonoBehaviour
{
    [Header("1. BGM 플레이리스트")]
    [Tooltip("이 씬에서 선택 가능한 배경음악 목록입니다.")]
    public List<AudioClip> bgmList = new List<AudioClip>();

    [Header("2. UI 연결 (선택 사항)")]
    [Tooltip("BGM 목록을 표시하고 선택할 TMP_Dropdown UI입니다.")]
    public TMP_Dropdown bgmDropdown;

    [Header("3. 재생 및 전환 옵션")]
    [Tooltip("음악 전환 시 크로스페이드 시간(초)")]
    public float crossFadeDuration = 1.0f;
    public bool loop = true;

    [Header("4. 저장 키")]
    [Tooltip("유저가 선택한 BGM 번호를 저장할 PlayerPrefs 키 (비워두면 씬 이름으로 자동 생성)")]
    public string saveKey = "";

    private string CurrentSaveKey => string.IsNullOrEmpty(saveKey) 
        ? $"BGM_Index_{SceneManager.GetActiveScene().name}" 
        : saveKey;

    void Start()
    {
        if (bgmList == null || bgmList.Count == 0) return;

        // 1. 저장된 BGM 인덱스 불러오기
        int savedIndex = PlayerPrefs.GetInt(CurrentSaveKey, 0);
        if (savedIndex < 0 || savedIndex >= bgmList.Count) savedIndex = 0;

        // 2. 드롭다운 UI 초기화 및 목록 채우기
        InitDropdownUI(savedIndex);

        // 3. 배경음악 재생
        PlayBGMByIndex(savedIndex);
    }

    /// <summary>
    /// 드롭다운 UI에 BGM 목록을 채우고 이벤트를 연결합니다.
    /// </summary>
    private void InitDropdownUI(int defaultIndex)
    {
        if (bgmDropdown == null) return;

        bgmDropdown.ClearOptions();

        List<string> options = new List<string>();
        for (int i = 0; i < bgmList.Count; i++)
        {
            if (bgmList[i] != null)
            {
                options.Add(bgmList[i].name);
            }
            else
            {
                options.Add($"음원 {i + 1} (미할당)");
            }
        }

        bgmDropdown.AddOptions(options);
        bgmDropdown.value = defaultIndex;
        bgmDropdown.RefreshShownValue();

        // 드롭다운 값 변경 이벤트 연결
        bgmDropdown.onValueChanged.RemoveAllListeners();
        bgmDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    /// <summary>
    /// 드롭다운에서 곡을 선택했을 때 호출
    /// </summary>
    private void OnDropdownValueChanged(int index)
    {
        if (index < 0 || index >= bgmList.Count) return;

        // 선택한 인덱스 저장
        PlayerPrefs.SetInt(CurrentSaveKey, index);
        PlayerPrefs.Save();

        // 선택한 음악 재생
        PlayBGMByIndex(index);
    }

    /// <summary>
    /// 인덱스에 해당하는 BGM을 SoundManager를 통해 재생합니다.
    /// </summary>
    public void PlayBGMByIndex(int index)
    {
        if (index < 0 || index >= bgmList.Count) return;

        AudioClip clipToPlay = bgmList[index];
        if (clipToPlay != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(clipToPlay, crossFadeDuration, loop);
        }
    }
}
