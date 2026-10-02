using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킨에 장착할 감정표현을 교체/선택하는 모달 팝업입니다.
/// - 보유한 감정표현 및 미보유 감정표현(토글) 목록 그리드 표시
/// - 미보유 감정표현 선택 시 이름 외 '???' 마스킹 및 획득처 안내
/// - 보유 감정표현 선택 시 보이스 미리듣기 및 슬롯 장착 기능
/// </summary>
public class VanityEmoteEditPopup : MonoBehaviour
{
    [Header("UI 기본 패널")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("필터 및 옵션")]
    [SerializeField] private Toggle showUnownedToggle;
    [SerializeField] private TextMeshProUGUI showUnownedToggleText;

    [Header("감정표현 슬롯 그리드")]
    [SerializeField] private Transform emoteSlotParent;
    [SerializeField] private GameObject emoteSlotPrefab;

    [Header("선택된 감정표현 상세 정보")]
    [SerializeField] private Image selectedEmoteIcon;
    [SerializeField] private TextMeshProUGUI selectedEmoteNameText;
    [SerializeField] private TextMeshProUGUI selectedEmoteSpeechText;
    [SerializeField] private TextMeshProUGUI selectedEmoteSourceText; // 획득처 정보
    [SerializeField] private Button voicePlayButton;                 // 보이스 미리듣기
    [SerializeField] private Button equipButton;                     // 장착 버튼
    [SerializeField] private TextMeshProUGUI equipButtonText;

    private SkinData _currentSkinData;
    private int _targetSlotIndex = 0;
    private EmoteData _selectedEmote;
    private bool _isSelectedOwned = false;
    private Action _onEmoteEquippedCallback;

    private List<GameObject> _spawnedSlots = new List<GameObject>();

    private void Awake()
    {
        if (popupRoot == null) popupRoot = gameObject;

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (showUnownedToggle != null)
        {
            showUnownedToggle.isOn = true; // 기본적으로 미보유 감정표현도 목록에 표시
            showUnownedToggle.onValueChanged.AddListener((val) => RefreshEmoteGrid());
        }

        if (voicePlayButton != null)
        {
            voicePlayButton.onClick.AddListener(OnVoicePlayClicked);
        }

        if (equipButton != null)
        {
            equipButton.onClick.AddListener(OnEquipButtonClicked);
        }
    }

    /// <summary>
    /// 감정표현 교체 팝업을 엽니다.
    /// </summary>
    public void Open(SkinData skinData, int slotIndex, Action onEquippedCallback)
    {
        _currentSkinData = skinData;
        _targetSlotIndex = slotIndex;
        _onEmoteEquippedCallback = onEquippedCallback;

        if (popupRoot != null) popupRoot.SetActive(true);

        if (titleText != null)
        {
            string skinName = (skinData != null) ? skinData.skinName : "스킨";
            titleText.text = $"감정표현 변경 [{skinName} - 슬롯 {slotIndex + 1}]";
        }

        RefreshEmoteGrid();
    }

    public bool IsOpened => popupRoot != null && popupRoot.activeSelf;

    public void Close()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
    }

    /// <summary>
    /// 전체 감정표현 데이터베이스에서 감정표현들을 필터링하여 그리드를 채웁니다.
    /// GameClient.Instance.unlockAllEmotesForTest가 true일 경우 직업 제한 및 보유 여부와 관계없이 모든 이모션이 노출/장착 가능합니다.
    /// </summary>
    private void RefreshEmoteGrid()
    {
        ClearGrid();

        // Resources/Emotes 폴더에서 등록된 모든 최신 EmoteData 에셋을 직접 로드 (캐시 누락 방지)
        var loaded = Resources.LoadAll<EmoteData>("Emotes");
        List<EmoteData> allEmotes = (loaded != null) ? new List<EmoteData>(loaded) : new List<EmoteData>();

        // EmotionManager의 database에도 최신화
        if (EmotionManager.Instance != null)
        {
            EmotionManager.Instance.emoteDatabase = new List<EmoteData>(allEmotes);
        }

        bool isTestMode = (GameClient.Instance != null && GameClient.Instance.unlockAllEmotesForTest);
        CardClass targetClass = (_currentSkinData != null) ? _currentSkinData.targetClass : CardClass.Gangzi;
        bool showUnowned = (showUnownedToggle != null && showUnownedToggle.isOn);

        // 계정 보유 감정표현 목록 조회
        List<string> ownedEmoteIds = GetOwnedEmoteIds();

        EmoteData firstSelectCandidate = null;
        bool firstSelectCandidateOwned = false;

        foreach (var emote in allEmotes)
        {
            if (emote == null) continue;

            // 테스트 모드가 아닐 때만 직업 호환성 검사
            if (!isTestMode && !emote.IsCompatibleWith(targetClass))
            {
                continue;
            }

            // 테스트 모드일 때는 모든 이모션을 보유한 것으로 취급하여 자유롭게 장착/테스트 가능
            bool isOwned = isTestMode || IsEmoteOwned(emote.emoteId, ownedEmoteIds);

            // 미보유 감정표현 보기 체크가 꺼져있고 미보유라면 표시 안 함 (테스트 모드일 땐 항상 표시)
            if (!isTestMode && !showUnowned && !isOwned)
            {
                continue;
            }

            CreateEmoteSlot(emote, isOwned);

            if (firstSelectCandidate == null || (!firstSelectCandidateOwned && isOwned))
            {
                firstSelectCandidate = emote;
                firstSelectCandidateOwned = isOwned;
            }
        }

        // 첫 번째 감정표현 자동 선택
        if (firstSelectCandidate != null)
        {
            SelectEmote(firstSelectCandidate, firstSelectCandidateOwned);
        }
        else
        {
            ClearSelection();
        }
    }

    private void CreateEmoteSlot(EmoteData emote, bool isOwned)
    {
        if (emoteSlotPrefab == null || emoteSlotParent == null) return;

        GameObject slotObj = Instantiate(emoteSlotPrefab, emoteSlotParent);
        slotObj.SetActive(true);
        _spawnedSlots.Add(slotObj);

        // 슬롯 UI 구성요소 탐색
        var btn = slotObj.GetComponent<Button>();
        if (btn == null) btn = slotObj.GetComponentInChildren<Button>();

        var img = slotObj.GetComponent<Image>();
        if (img == null) img = slotObj.GetComponentInChildren<Image>();

        var txt = slotObj.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.raycastTarget = false;

        Transform lockObj = slotObj.transform.Find("LockIcon");
        if (lockObj != null) lockObj.gameObject.SetActive(!isOwned);

        string classTag = emote.targetClass == EmoteTargetClass.All ? "[공용]" : $"[{emote.targetClass}]";
        string label = !string.IsNullOrEmpty(emote.buttonLabel) ? emote.buttonLabel : emote.emoteId;

        if (txt != null)
        {
            txt.text = $"{classTag} {label}";
        }

        // 미보유 시 어둡게 톤 다운
        if (img != null)
        {
            img.color = isOwned ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.85f);
        }

        if (btn != null)
        {
            btn.onClick.AddListener(() => SelectEmote(emote, isOwned));
        }
    }

    private void SelectEmote(EmoteData emote, bool isOwned)
    {
        _selectedEmote = emote;
        _isSelectedOwned = isOwned;

        if (emote == null)
        {
            ClearSelection();
            return;
        }

        // 1. 이름 (미보유여도 이름은 표시, 직업 태그 포함)
        if (selectedEmoteNameText != null)
        {
            string classTag = emote.targetClass == EmoteTargetClass.All ? "<color=#88DDFF>[공용]</color>" : $"<color=#FFDD88>[{emote.targetClass}]</color>";
            string label = !string.IsNullOrEmpty(emote.buttonLabel) ? emote.buttonLabel : emote.emoteId;
            selectedEmoteNameText.text = $"{classTag} {label}";
        }

        // 2. 대사 (미보유 시 ???)
        if (selectedEmoteSpeechText != null)
        {
            if (isOwned)
            {
                selectedEmoteSpeechText.text = $"\"{emote.speechMessage}\"";
            }
            else
            {
                selectedEmoteSpeechText.text = "<color=#888888>\"???\"</color>";
            }
        }

        // 3. 획득처 안내
        if (selectedEmoteSourceText != null)
        {
            if (isOwned)
            {
                selectedEmoteSourceText.text = "<color=#7CFC00>● 보유 중</color>";
            }
            else
            {
                selectedEmoteSourceText.text = "<color=#FF9900>🔒 획득처: 상점(이모션 상품) 또는 업적 달성</color>";
            }
        }

        // 4. 보이스 미리듣기 버튼 (보유 시 활성화)
        if (voicePlayButton != null)
        {
            voicePlayButton.interactable = isOwned && (emote.voiceClip != null);
        }

        // 5. 장착 버튼 상태
        if (equipButton != null)
        {
            equipButton.interactable = isOwned;
        }
        if (equipButtonText != null)
        {
            equipButtonText.text = isOwned ? "이 슬롯에 장착" : "미보유 (장착 불가)";
        }
    }

    private void OnVoicePlayClicked()
    {
        if (_selectedEmote != null && _selectedEmote.voiceClip != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayVoice(_selectedEmote.voiceClip);
        }
    }

    private void OnEquipButtonClicked()
    {
        if (_currentSkinData == null || _selectedEmote == null || !_isSelectedOwned) return;

        SkinCustomSettingManager.Instance.SetEquippedEmote(
            _currentSkinData.skinId,
            _currentSkinData,
            _targetSlotIndex,
            _selectedEmote.emoteId
        );

        _onEmoteEquippedCallback?.Invoke();
        Close();
    }

    private void ClearSelection()
    {
        _selectedEmote = null;
        if (selectedEmoteNameText != null) selectedEmoteNameText.text = "";
        if (selectedEmoteSpeechText != null) selectedEmoteSpeechText.text = "";
        if (selectedEmoteSourceText != null) selectedEmoteSourceText.text = "";
        if (voicePlayButton != null) voicePlayButton.interactable = false;
        if (equipButton != null) equipButton.interactable = false;
    }

    private void ClearGrid()
    {
        foreach (var slot in _spawnedSlots)
        {
            if (slot != null) Destroy(slot);
        }
        _spawnedSlots.Clear();
    }

    private List<string> GetOwnedEmoteIds()
    {
        if (GameClient.Instance != null && GameClient.Instance.CurrentUser != null)
        {
            return GameClient.Instance.CurrentUser.ownedEmotes ?? new List<string>();
        }
        return new List<string>();
    }

    private bool IsEmoteOwned(string emoteId, List<string> ownedList)
    {
        if (string.IsNullOrEmpty(emoteId)) return false;

        // 유저 데이터에 있으면 보유
        if (ownedList != null && ownedList.Exists(id => string.Equals(id, emoteId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // 오프라인/테스트 모드일 때: 기본 감정표현(GREETING, THANKS 등 또는 각 직업 기본 4개)은 기본 보유 처리
        if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
        {
            return true;
        }

        return false;
    }
}
