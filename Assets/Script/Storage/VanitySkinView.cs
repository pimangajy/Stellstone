using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Storage 씬 VanityPos(아이템 관리)의 [스킨] 카테고리 메인 뷰어입니다.
/// - 상단 필터 바 대신 중앙 목록 상단의 [필터 ⚙️] 버튼으로 필터 설정 패널 토글 전환
/// - 스킨 슬롯 목록 생성 및 선택 (선택된 스킨 하이라이트)
/// - 우측 상세 정보 (보유 시 정식 정보 / 미보유 시 이름 외 '???' 마스킹 및 획득처)
/// - 감정표현 4개 슬롯 및 음성 미리듣기 / 교체 연동
/// - 직업별 대표 스킨 설정 기능
/// </summary>
public class VanitySkinView : MonoBehaviour
{
    [System.Serializable]
    public class EmoteSlotUI
    {
        public GameObject root;
        public TextMeshProUGUI labelText;
        public TextMeshProUGUI speechText;
        public Button playVoiceButton;
        public Button editButton;
        public GameObject lockIcon;
    }

    [Header("1. 상단 내비게이션 & 뒤로가기")]
    [SerializeField] private Button btnBackToCategories;          // 카테고리 선택 화면으로 복귀

    [Header("2. 중앙 스킨 목록 & 필터 전환 패널")]
    [SerializeField] private Button btnToggleFilter;              // [필터 ⚙️] 토글 버튼
    [SerializeField] private TextMeshProUGUI btnToggleFilterText;
    [SerializeField] private GameObject skinListScrollView;       // 스킨 목록 스크롤뷰
    [SerializeField] private Transform skinSlotParent;
    [SerializeField] private GameObject skinSlotPrefab;

    [Header("3. 필터 설정 뷰 (목록 대신 표시)")]
    [SerializeField] private GameObject filterPanel;              // 필터 설정 패널
    [SerializeField] private Button btnFilterAll;
    [SerializeField] private Button btnFilterGangzi;
    [SerializeField] private Button btnFilterYuni;
    [SerializeField] private Button btnFilterHuya;
    [SerializeField] private Toggle showUnownedToggle;
    [SerializeField] private Button btnApplyFilter;               // [적용 및 목록 보기] 버튼

    [Header("4. 우측 상세 정보 패널")]
    [SerializeField] private Image skinIllustrationImage;
    [SerializeField] private TextMeshProUGUI skinTitleText;
    [SerializeField] private TextMeshProUGUI skinClassText;
    [SerializeField] private TextMeshProUGUI skinDescriptionText;
    [SerializeField] private TextMeshProUGUI skinSourceText;
    [SerializeField] private GameObject unownedWarningBadge;

    [Header("5. 감정표현 4종 슬롯")]
    [SerializeField] private List<EmoteSlotUI> emoteSlots = new List<EmoteSlotUI>();
    [SerializeField] private VanityEmoteEditPopup emoteEditPopup;

    [Header("6. 하단 액션 버튼")]
    [SerializeField] private Button btnSetRepresentative;
    [SerializeField] private TextMeshProUGUI btnSetRepresentativeText;
    [SerializeField] private Button btnGoToShop;

    private List<SkinData> _allSkinAssets = new List<SkinData>();
    private List<SkinSlotUI> _spawnedSlots = new List<SkinSlotUI>();
    private CardClass? _activeClassFilter = null;
    private SkinData _selectedSkin;
    private bool _isSelectedOwned = false;
    private bool _isFilterPanelOpened = false;

    public bool IsFilterPanelOpened => _isFilterPanelOpened;
    public bool IsEmotePopupOpened => emoteEditPopup != null && emoteEditPopup.IsOpened;
    public void CloseEmotePopup() => emoteEditPopup?.Close();

    private void Awake()
    {
        if (btnBackToCategories != null)
        {
            btnBackToCategories.onClick.AddListener(OnBackToCategoriesClicked);
        }

        if (btnToggleFilter != null)
        {
            btnToggleFilter.onClick.AddListener(ToggleFilterPanel);
        }

        if (btnApplyFilter != null)
        {
            btnApplyFilter.onClick.AddListener(() => ShowFilterPanel(false));
        }

        // 직업 필터 버튼 바인딩
        if (btnFilterAll != null) btnFilterAll.onClick.AddListener(() => { SetClassFilter(null); ShowFilterPanel(false); });
        if (btnFilterGangzi != null) btnFilterGangzi.onClick.AddListener(() => { SetClassFilter(CardClass.Gangzi); ShowFilterPanel(false); });
        if (btnFilterYuni != null) btnFilterYuni.onClick.AddListener(() => { SetClassFilter(CardClass.Yuni); ShowFilterPanel(false); });
        if (btnFilterHuya != null) btnFilterHuya.onClick.AddListener(() => { SetClassFilter(CardClass.Huya); ShowFilterPanel(false); });

        if (showUnownedToggle != null)
        {
            showUnownedToggle.onValueChanged.AddListener((val) => RefreshSkinGrid());
        }

        if (btnSetRepresentative != null)
        {
            btnSetRepresentative.onClick.AddListener(OnSetRepresentativeClicked);
        }

        if (btnGoToShop != null)
        {
            btnGoToShop.onClick.AddListener(OnGoToShopClicked);
        }

        // 감정표현 4개 슬롯 버튼 바인딩
        for (int i = 0; i < emoteSlots.Count; i++)
        {
            int slotIdx = i;
            var slot = emoteSlots[i];
            if (slot.playVoiceButton != null)
            {
                slot.playVoiceButton.onClick.AddListener(() => PlaySlotVoice(slotIdx));
            }
            if (slot.editButton != null)
            {
                slot.editButton.onClick.AddListener(() => OpenEmoteEditPopup(slotIdx));
            }
        }
    }

    /// <summary>
    /// Vanity 뷰가 열릴 때 초기화합니다.
    /// </summary>
    public void InitializeView()
    {
        ShowFilterPanel(false);
        LoadAllSkinAssets();
        RefreshSkinGrid();
    }

    public void ToggleFilterPanel()
    {
        ShowFilterPanel(!_isFilterPanelOpened);
    }

    public void ShowFilterPanel(bool show)
    {
        _isFilterPanelOpened = show;

        if (filterPanel != null)
        {
            filterPanel.SetActive(show);
        }

        if (skinListScrollView != null)
        {
            skinListScrollView.SetActive(!show);
        }

        if (btnToggleFilterText != null)
        {
            btnToggleFilterText.text = show ? "스킨 목록 보기" : "필터 설정 ⚙️";
        }

        UpdateFilterButtonHighlights();
    }

    private void UpdateFilterButtonHighlights()
    {
        SetFilterButtonVisual(btnFilterAll, _activeClassFilter == null);
        SetFilterButtonVisual(btnFilterGangzi, _activeClassFilter == CardClass.Gangzi);
        SetFilterButtonVisual(btnFilterYuni, _activeClassFilter == CardClass.Yuni);
        SetFilterButtonVisual(btnFilterHuya, _activeClassFilter == CardClass.Huya);
    }

    private void SetFilterButtonVisual(Button btn, bool isSelected)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = isSelected ? new Color(0.2f, 0.6f, 1f, 0.95f) : new Color(0.12f, 0.16f, 0.24f, 0.85f);
        }
    }

    private void LoadAllSkinAssets()
    {
        _allSkinAssets.Clear();
        var skins = Resources.LoadAll<SkinData>("Skins");
        if (skins != null)
        {
            _allSkinAssets = skins.OrderBy(s => s.skinId).ToList();
        }
    }

    public void SetClassFilter(CardClass? cardClass)
    {
        _activeClassFilter = cardClass;
        RefreshSkinGrid();
    }

    /// <summary>
    /// 현재 필터 및 토글 조건에 맞게 스킨 슬롯들을 생성합니다.
    /// </summary>
    public void RefreshSkinGrid()
    {
        ClearGrid();

        if (skinSlotPrefab == null || skinSlotParent == null) return;

        bool showUnowned = (showUnownedToggle != null && showUnownedToggle.isOn);
        List<string> ownedSkinIds = GetOwnedSkinIds();

        SkinData candidate = null;
        bool candidateOwned = false;

        foreach (var skin in _allSkinAssets)
        {
            if (skin == null) continue;

            // 직업 필터
            if (_activeClassFilter.HasValue && skin.targetClass != _activeClassFilter.Value)
            {
                continue;
            }

            bool isOwned = IsSkinOwned(skin.skinId, ownedSkinIds);

            if (!showUnowned && !isOwned)
            {
                continue;
            }

            string repSkinId = SkinCustomSettingManager.Instance.GetRepresentativeSkinId(skin.targetClass);
            bool isRep = string.Equals(repSkinId, skin.skinId, StringComparison.OrdinalIgnoreCase);

            GameObject slotObj = Instantiate(skinSlotPrefab, skinSlotParent);
            var slotUI = slotObj.GetComponent<SkinSlotUI>();
            if (slotUI != null)
            {
                slotUI.SetSkinData(skin, isRep, isOwned, (clickedSkin, owned) =>
                {
                    SelectSkin(clickedSkin, owned);
                });
                _spawnedSlots.Add(slotUI);
            }

            if (candidate == null || (!candidateOwned && isOwned))
            {
                candidate = skin;
                candidateOwned = isOwned;
            }
        }

        if (_selectedSkin != null && _allSkinAssets.Contains(_selectedSkin))
        {
            SelectSkin(_selectedSkin, IsSkinOwned(_selectedSkin.skinId, ownedSkinIds));
        }
        else if (candidate != null)
        {
            SelectSkin(candidate, candidateOwned);
        }
        else
        {
            ClearSelection();
        }
    }

    public void SelectSkin(SkinData skin, bool isOwned)
    {
        _selectedSkin = skin;
        _isSelectedOwned = isOwned;

        if (skin == null)
        {
            ClearSelection();
            return;
        }

        // 슬롯 하이라이트 갱신
        foreach (var slot in _spawnedSlots)
        {
            if (slot != null)
            {
                bool isTarget = (slot.MasterSkinData == skin);
                slot.SetHighlight(isTarget);
            }
        }

        // 1. 대형 전신 일러스트
        if (skinIllustrationImage != null)
        {
            skinIllustrationImage.sprite = skin.skinSprite;
            skinIllustrationImage.color = isOwned ? Color.white : new Color(0.35f, 0.35f, 0.35f, 0.9f);
        }

        // 2. 스킨 이름 (이름은 미보유여도 노출)
        if (skinTitleText != null)
        {
            skinTitleText.text = skin.skinName;
        }

        // 3. 직업 및 설명 (미보유 시 ??? 마스킹)
        if (skinClassText != null)
        {
            skinClassText.text = isOwned ? $"직업: {GetClassDisplayName(skin.targetClass)}" : "직업: <color=#888888>???</color>";
        }

        if (skinDescriptionText != null)
        {
            skinDescriptionText.text = isOwned ? skin.description : "<color=#888888>???\n(보유하지 않은 스킨입니다)</color>";
        }

        // 4. 획득처 안내 뱃지
        if (skinSourceText != null)
        {
            if (isOwned)
            {
                skinSourceText.text = "<color=#7CFC00>● 보유 중인 스킨</color>";
            }
            else
            {
                skinSourceText.text = "<color=#FF9900>🔒 획득처: 상점(스킨 상품) 또는 업적 보상</color>";
            }
        }

        if (unownedWarningBadge != null)
        {
            unownedWarningBadge.SetActive(!isOwned);
        }

        // 5. 감정표현 4종 슬롯 갱신
        RefreshEmoteSlots(skin, isOwned);

        // 6. 하단 버튼 상태
        UpdateActionButtons(skin, isOwned);
    }

    private void RefreshEmoteSlots(SkinData skin, bool isOwned)
    {
        if (skin == null) return;

        List<EmoteData> emotes = SkinCustomSettingManager.Instance.GetEquippedEmotes(skin.skinId, skin);

        for (int i = 0; i < emoteSlots.Count; i++)
        {
            var slot = emoteSlots[i];
            if (slot.root == null) continue;

            if (isOwned && emotes != null && i < emotes.Count && emotes[i] != null)
            {
                var e = emotes[i];
                if (slot.labelText != null) slot.labelText.text = !string.IsNullOrEmpty(e.buttonLabel) ? e.buttonLabel : e.emoteId;
                if (slot.speechText != null) slot.speechText.text = $"\"{e.speechMessage}\"";
                if (slot.playVoiceButton != null) slot.playVoiceButton.interactable = (e.voiceClip != null);
                if (slot.editButton != null) slot.editButton.interactable = true;
                if (slot.lockIcon != null) slot.lockIcon.SetActive(false);
            }
            else
            {
                if (slot.labelText != null) slot.labelText.text = "???";
                if (slot.speechText != null) slot.speechText.text = "<color=#888888>\"???\"</color>";
                if (slot.playVoiceButton != null) slot.playVoiceButton.interactable = false;
                if (slot.editButton != null) slot.editButton.interactable = false;
                if (slot.lockIcon != null) slot.lockIcon.SetActive(true);
            }
        }
    }

    private void UpdateActionButtons(SkinData skin, bool isOwned)
    {
        if (skin == null) return;

        string repSkinId = SkinCustomSettingManager.Instance.GetRepresentativeSkinId(skin.targetClass);
        bool isRepresentative = string.Equals(repSkinId, skin.skinId, StringComparison.OrdinalIgnoreCase);

        if (btnSetRepresentative != null)
        {
            btnSetRepresentative.gameObject.SetActive(isOwned);
            btnSetRepresentative.interactable = isOwned && !isRepresentative;
        }

        if (btnSetRepresentativeText != null)
        {
            btnSetRepresentativeText.text = isRepresentative ? "장착 중 (대표 스킨)" : "대표 스킨으로 설정";
        }

        if (btnGoToShop != null)
        {
            btnGoToShop.gameObject.SetActive(!isOwned);
        }
    }

    private void PlaySlotVoice(int slotIdx)
    {
        if (_selectedSkin == null || !_isSelectedOwned) return;

        List<EmoteData> emotes = SkinCustomSettingManager.Instance.GetEquippedEmotes(_selectedSkin.skinId, _selectedSkin);
        if (emotes != null && slotIdx < emotes.Count && emotes[slotIdx] != null)
        {
            var clip = emotes[slotIdx].voiceClip;
            if (clip != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayVoice(clip);
            }
        }
    }

    private void OpenEmoteEditPopup(int slotIdx)
    {
        if (_selectedSkin == null || !_isSelectedOwned) return;

        if (emoteEditPopup != null)
        {
            emoteEditPopup.Open(_selectedSkin, slotIdx, () =>
            {
                RefreshEmoteSlots(_selectedSkin, _isSelectedOwned);
            });
        }
    }

    private void OnSetRepresentativeClicked()
    {
        if (_selectedSkin == null || !_isSelectedOwned) return;

        SkinCustomSettingManager.Instance.SetRepresentativeSkinId(_selectedSkin.targetClass, _selectedSkin.skinId);
        RefreshSkinGrid();
    }

    private void OnGoToShopClicked()
    {
        SceneManager.LoadScene("ShopScene");
    }

    private void OnBackToCategoriesClicked()
    {
        if (VanityManager.Instance != null)
        {
            VanityManager.Instance.CloseActiveCategoryView();
        }
    }

    private void ClearSelection()
    {
        _selectedSkin = null;
        if (skinIllustrationImage != null) skinIllustrationImage.sprite = null;
        if (skinTitleText != null) skinTitleText.text = "";
        if (skinClassText != null) skinClassText.text = "";
        if (skinDescriptionText != null) skinDescriptionText.text = "";
        if (skinSourceText != null) skinSourceText.text = "";
        if (btnSetRepresentative != null) btnSetRepresentative.gameObject.SetActive(false);
        if (btnGoToShop != null) btnGoToShop.gameObject.SetActive(false);
    }

    private void ClearGrid()
    {
        foreach (var slot in _spawnedSlots)
        {
            if (slot != null && slot.gameObject != null) Destroy(slot.gameObject);
        }
        _spawnedSlots.Clear();
    }

    private List<string> GetOwnedSkinIds()
    {
        if (GameClient.Instance != null && GameClient.Instance.CurrentUser != null)
        {
            return GameClient.Instance.CurrentUser.ownedSkins ?? new List<string>();
        }
        return new List<string>();
    }

    private bool IsSkinOwned(string skinId, List<string> ownedList)
    {
        if (string.IsNullOrEmpty(skinId)) return false;

        if (ownedList != null && ownedList.Exists(id => string.Equals(id, skinId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (GameClient.Instance == null || !GameClient.Instance.IsConnected)
        {
            if (skinId.EndsWith("_0001", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string GetClassDisplayName(CardClass cardClass)
    {
        switch (cardClass)
        {
            case CardClass.Gangzi: return "강지";
            case CardClass.Huya:   return "후야";
            case CardClass.Yuni:   return "유니";
            default:               return cardClass.ToString();
        }
    }
}
