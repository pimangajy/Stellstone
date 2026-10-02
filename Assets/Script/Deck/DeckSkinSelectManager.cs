using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 덱 편성 씬에서 [스킨 선택] / [카드 목록] 토글 및 스킨 목록 생성/선택을 총괄하는 매니저입니다.
/// 덱 선택 여부에 따라 전체 직업 스킨 보기 또는 해당 직업 스킨 필터링 모드로 동작합니다.
/// </summary>
public class DeckSkinSelectManager : MonoBehaviour
{
    public static DeckSkinSelectManager Instance { get; private set; }

    [Header("1. 토글 버튼")]
    [SerializeField] private Button toggleButton;                      // '스킨 선택' / '카드 목록' 전환 버튼
    [SerializeField] private TextMeshProUGUI toggleButtonText;         // 버튼 내 텍스트
    [SerializeField] private string toSkinText = "스킨 선택";          // 카드 목록 보고 있을 때 버튼 텍스트
    [SerializeField] private string toCardText = "카드 목록";          // 스킨 목록 보고 있을 때 버튼 텍스트

    [Header("2. 화면 패널")]
    [SerializeField] private GameObject cardListPanel;                 // 카드 목록 ScrollView 또는 패널
    [SerializeField] private GameObject skinListPanel;                 // 스킨 목록 ScrollView 또는 패널
    [SerializeField] private Transform skinSlotParent;                 // 스킨 슬롯들이 생성될 Content Transform
    [SerializeField] private GameObject skinSlotPrefab;                // 스킨 슬롯 프리팹 (SkinSlotUI 부착)

    [Header("3. 스킨 상세 정보 팝업")]
    [SerializeField] private SkinDetailPopup skinDetailPopup;          // 씬 내의 SkinDetailPopup 참조

    [Header("4. 덱 리더 미리보기 (선택 사항)")]
    [SerializeField] private Image currentLeaderPreviewImage;          // 덱 편집 화면 상단의 리더 이미지

    public bool IsShowingSkins { get; private set; } = false;

    // 현재 생성된 스킨 슬롯 목록
    private List<SkinSlotUI> createdSlotUIs = new List<SkinSlotUI>();

    // 현재 선택된 직업 필터 (null이면 전체)
    private CardClass? activeClassFilter = null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleView);
        }
    }

    private void OnEnable()
    {
        FilterManager.OnFilterApplied += HandleFilterApplied;
    }

    private void OnDisable()
    {
        FilterManager.OnFilterApplied -= HandleFilterApplied;
    }

    private void HandleFilterApplied(FilterManager.FilterSettings settings)
    {
        activeClassFilter = settings.cardClass;

        // 스킨 목록을 보고 있는 도중에 필터가 변경되면 스킨 목록 실시간 갱신
        if (IsShowingSkins)
        {
            StartCoroutine(LoadAndDisplaySkinsCoroutine());
        }
    }

    private void Start()
    {
        // 시작 시 기본적으로 카드 목록 표시, 스킨 패널 숨김
        ShowCardListImmediate();
    }

    /// <summary>
    /// 카드 목록 ↔ 스킨 목록 화면을 토글합니다.
    /// </summary>
    public void ToggleView()
    {
        if (IsShowingSkins)
        {
            ShowCardList();
        }
        else
        {
            ShowSkinList();
        }
    }

    /// <summary>
    /// 스킨 목록 패널을 표시하고 보유 스킨 목록을 로드합니다.
    /// </summary>
    public void ShowSkinList()
    {
        IsShowingSkins = true;

        if (cardListPanel != null) cardListPanel.SetActive(false);
        if (skinListPanel != null) skinListPanel.SetActive(true);

        if (toggleButtonText != null)
        {
            toggleButtonText.text = toCardText;
        }

        // 현재 상태(덱 선택 여부)에 맞게 스킨 목록 생성
        StartCoroutine(LoadAndDisplaySkinsCoroutine());
    }

    /// <summary>
    /// 카드 목록 패널로 복귀합니다.
    /// </summary>
    public void ShowCardList()
    {
        IsShowingSkins = false;

        if (skinListPanel != null) skinListPanel.SetActive(false);
        if (cardListPanel != null) cardListPanel.SetActive(true);

        if (toggleButtonText != null)
        {
            toggleButtonText.text = toSkinText;
        }

        // 스킨 상세 팝업이 열려있다면 닫기
        if (skinDetailPopup != null)
        {
            skinDetailPopup.Close();
        }
    }

    private void ShowCardListImmediate()
    {
        IsShowingSkins = false;
        if (skinListPanel != null) skinListPanel.SetActive(false);
        if (cardListPanel != null) cardListPanel.SetActive(true);
        if (toggleButtonText != null) toggleButtonText.text = toSkinText;
    }

    /// <summary>
    /// 현재 덱 선택 여부에 따라 스킨 목록을 생성합니다.
    /// - 덱 미선택: 모든 직업 스킨을 [직업별 ➔ ID 오름차순]으로 표시 (선택 비활성화)
    /// - 덱 선택 중: 해당 덱 직업의 스킨만 필터링하여 표시 (선택 가능)
    /// </summary>
    private IEnumerator LoadAndDisplaySkinsCoroutine()
    {
        ClearSkinSlots();

        // 1. 덱 선택 여부 및 직업 확인
        bool isDeckSelected = IsDeckSelected();
        string currentClass = isDeckSelected ? GetCurrentDeckClass() : null;

        // 2. GameClient로부터 리더 스킨 상품 전체 목록 가져오기 (캐시 또는 API)
        List<ProductData> allSkins = null;
        if (GameClient.Instance != null)
        {
            yield return GameClient.Instance.GetLeaderSkinsAsync(skins => allSkins = skins);
        }

        // 2-1. Resources/Skins 에셋들을 마스터 데이터로 병합 보장
        var masterSkinAssets = Resources.LoadAll<SkinData>("Skins");
        if (masterSkinAssets != null && masterSkinAssets.Length > 0)
        {
            if (allSkins == null) allSkins = new List<ProductData>();
            foreach (var mSkin in masterSkinAssets)
            {
                if (mSkin == null) continue;
                if (!allSkins.Any(s => string.Equals(s.productId, mSkin.skinId, StringComparison.OrdinalIgnoreCase)))
                {
                    allSkins.Add(new ProductData
                    {
                        productId = mSkin.skinId,
                        productName = mSkin.skinName,
                        description = mSkin.description,
                        image_url = $"Items/Skins/{mSkin.skinId}.png",
                        targetClasses = new List<string> { mSkin.targetClass.ToString() },
                        isVisibleInShop = !mSkin.skinId.EndsWith("0001") && !mSkin.skinId.EndsWith("_Default"),
                        price = mSkin.skinId.EndsWith("0001") ? 0 : 500
                    });
                }
            }
        }

        // 3. 내 계정의 보유 스킨 ID 목록
        List<string> ownedSkinIds = (GameClient.Instance != null && GameClient.Instance.CurrentUser != null)
            ? GameClient.Instance.CurrentUser.ownedSkins
            : new List<string>();

        // 4. 표시할 스킨 목록 구성
        List<ProductData> displaySkins = new List<ProductData>();

        if (activeClassFilter.HasValue)
        {
            // ==============================================================
            // [분기 1] 특정 직업 필터 토글(유니, 강지 등)을 명시적으로 선택한 경우:
            // ➔ 오직 해당 직업의 스킨만 단독으로 필터링 (타 직업/중립 제외)
            // ==============================================================
            string selectedClassStr = activeClassFilter.Value.ToString();

            if (allSkins != null)
            {
                foreach (var skin in allSkins)
                {
                    if (skin == null) continue;

                    bool matchesClass = skin.targetClasses != null &&
                                        skin.targetClasses.Contains(selectedClassStr, StringComparer.OrdinalIgnoreCase);

                    if (matchesClass && !displaySkins.Any(s => s.productId == skin.productId))
                    {
                        displaySkins.Add(skin);
                    }
                }
            }

            // 기본 스킨 누락 대비 fallback
            if (!displaySkins.Any(s => s.productId.Contains(selectedClassStr) && (s.productId.EndsWith("0001") || s.productId.EndsWith("_Default"))))
            {
                displaySkins.Add(CreateDefaultSkinProduct(selectedClassStr));
            }

            // 정렬: 보유 스킨 우선 -> ID 오름차순
            displaySkins = displaySkins.OrderByDescending(skin =>
            {
                bool isFreeOrDefault = skin.productId.EndsWith("0001") || skin.productId.EndsWith("_Default") || !skin.isVisibleInShop;
                bool isOwned = isFreeOrDefault ||
                               (ownedSkinIds != null && ownedSkinIds.Contains(skin.productId)) ||
                               (skin.remainingPurchaseLimit == 0);
                return isOwned;
            }).ThenBy(skin => skin.productId).ToList();
        }
        else if (isDeckSelected && !string.IsNullOrEmpty(currentClass))
        {
            // ==============================================================
            // [분기 2] 직업 필터 '전체' + 덱 편집 중인 경우:
            // ➔ 현재 덱 직업 스킨 + 강지(중립) 스킨 모두 표시 (강지 최상단)
            // ==============================================================
            if (allSkins != null)
            {
                foreach (var skin in allSkins)
                {
                    if (skin == null) continue;

                    bool matchesClass = skin.targetClasses == null || 
                                        skin.targetClasses.Count == 0 || 
                                        skin.targetClasses.Contains(currentClass, StringComparer.OrdinalIgnoreCase) ||
                                        skin.targetClasses.Contains("Gangzi", StringComparer.OrdinalIgnoreCase) ||
                                        skin.targetClasses.Contains("Common", StringComparer.OrdinalIgnoreCase);

                    if (matchesClass && !displaySkins.Any(s => s.productId == skin.productId))
                    {
                        displaySkins.Add(skin);
                    }
                }
            }

            // 기본 스킨 누락 대비 fallback (강지 기본 스킨 및 해당 직업 기본 스킨)
            if (!displaySkins.Any(s => s.productId.Contains("Gangzi") && (s.productId.EndsWith("0001") || s.productId.EndsWith("_Default"))))
            {
                displaySkins.Add(CreateDefaultSkinProduct("Gangzi"));
            }
            if (!string.Equals(currentClass, "Gangzi", StringComparison.OrdinalIgnoreCase) && 
                !displaySkins.Any(s => s.productId.Contains(currentClass) && (s.productId.EndsWith("0001") || s.productId.EndsWith("_Default"))))
            {
                displaySkins.Add(CreateDefaultSkinProduct(currentClass));
            }

            // 정렬: 강지(중립) 스킨을 최상단에 배치 -> 보유 스킨 우선 -> ID 오름차순
            displaySkins = displaySkins.OrderBy(skin =>
            {
                bool isGangzi = (skin.targetClasses != null && skin.targetClasses.Contains("Gangzi", StringComparer.OrdinalIgnoreCase)) ||
                                (skin.productId != null && skin.productId.Contains("Gangzi", StringComparison.OrdinalIgnoreCase));
                return isGangzi ? 0 : 1; // 강지(중립) 우선 (0)
            }).ThenByDescending(skin =>
            {
                bool isFreeOrDefault = skin.productId.EndsWith("0001") || skin.productId.EndsWith("_Default") || !skin.isVisibleInShop;
                bool isOwned = isFreeOrDefault ||
                               (ownedSkinIds != null && ownedSkinIds.Contains(skin.productId)) ||
                               (skin.remainingPurchaseLimit == 0);
                return isOwned;
            }).ThenBy(skin => skin.productId).ToList();
        }
        else
        {
            // ==============================================================
            // [분기 3] 직업 필터 '전체' + 덱 미선택(도감 모드)인 경우:
            // ➔ 모든 직업의 스킨 표시 (강지 최상단)
            // ==============================================================
            if (allSkins != null)
            {
                foreach (var skin in allSkins)
                {
                    if (skin == null) continue;
                    if (!displaySkins.Any(s => s.productId == skin.productId))
                    {
                        displaySkins.Add(skin);
                    }
                }
            }

            // 기본 직업 스킨(Gangzi, Yuni) 누락 시 추가
            string[] knownClasses = new string[] { "Gangzi", "Yuni" };
            foreach (var cls in knownClasses)
            {
                if (!displaySkins.Any(s => (s.targetClasses != null && s.targetClasses.Contains(cls)) && (s.productId.EndsWith("0001") || s.productId.EndsWith("_Default"))))
                {
                    displaySkins.Add(CreateDefaultSkinProduct(cls));
                }
            }

            // 정렬: 강지(중립) 최상단 -> 직업별(Class) 알파벳순 -> 스킨 ID(productId) 오름차순
            displaySkins = displaySkins.OrderBy(skin =>
            {
                bool isGangzi = (skin.targetClasses != null && skin.targetClasses.Contains("Gangzi", StringComparer.OrdinalIgnoreCase)) ||
                                (skin.productId != null && skin.productId.Contains("Gangzi", StringComparison.OrdinalIgnoreCase));
                if (isGangzi) return "00_Gangzi";

                if (skin.targetClasses != null && skin.targetClasses.Count > 0)
                {
                    return skin.targetClasses[0];
                }
                return "ZZ_Common";
            }).ThenBy(skin => skin.productId).ToList();
        }

        // 5. 현재 장착되어 있는 스킨 ID 가져오기
        string currentEquippedSkinId = isDeckSelected ? GetCurrentEquippedSkinId(currentClass) : "";

        // 6. 슬롯 UI 생성 (보유 여부 isOwned 전달)
        foreach (var skinData in displaySkins)
        {
            bool isFreeOrDefault = skinData.productId.EndsWith("0001") || skinData.productId.EndsWith("_Default") || !skinData.isVisibleInShop;
            bool isOwned = isFreeOrDefault ||
                           (ownedSkinIds != null && ownedSkinIds.Contains(skinData.productId)) ||
                           (skinData.remainingPurchaseLimit == 0);

            bool isEquipped = isDeckSelected && (skinData.productId == currentEquippedSkinId);

            CreateSkinSlot(skinData, isEquipped, isOwned);
        }

        // 7. 리더 미리보기 이미지 갱신
        if (isDeckSelected)
        {
            UpdateLeaderPreview(currentEquippedSkinId, displaySkins);
        }
    }

    /// <summary>
    /// 개별 스킨 슬롯 UI 생성
    /// </summary>
    private void CreateSkinSlot(ProductData skinData, bool isEquipped, bool isOwned)
    {
        if (skinSlotParent == null || skinSlotPrefab == null) return;

        GameObject slotObj = Instantiate(skinSlotPrefab, skinSlotParent);
        slotObj.name = $"SkinSlot_{skinData.productId}";

        SkinSlotUI slotUI = slotObj.GetComponent<SkinSlotUI>();
        if (slotUI != null)
        {
            slotUI.SetSkinData(skinData, isEquipped, isOwned, OnSkinSlotClicked);
            createdSlotUIs.Add(slotUI);
        }
    }

    /// <summary>
    /// 스킨 슬롯 클릭 시 호출 -> 상세 정보 팝업 오픈 (보유 여부 및 덱 선택 상태 함께 전달)
    /// </summary>
    private void OnSkinSlotClicked(ProductData skinData, bool isOwned)
    {
        if (skinData == null) return;

        bool isDeckSelected = IsDeckSelected();
        string currentClass = isDeckSelected ? GetCurrentDeckClass() : null;
        string currentEquippedSkinId = isDeckSelected ? GetCurrentEquippedSkinId(currentClass) : "";
        bool isEquipped = isDeckSelected && (skinData.productId == currentEquippedSkinId);

        if (skinDetailPopup != null)
        {
            skinDetailPopup.Open(skinData, isEquipped, isOwned, isDeckSelected, OnSkinSelectedFromPopup);
        }
    }

    /// <summary>
    /// 상세 팝업에서 [선택] 버튼 클릭 시 호출
    /// </summary>
    private async void OnSkinSelectedFromPopup(ProductData selectedSkin)
    {
        if (selectedSkin == null || !IsDeckSelected()) return;

        // 1. DeckManager에 선택된 스킨 설정
        if (DeckManager.instance != null)
        {
            DeckManager.instance.SelectSkin(selectedSkin.productId);

            // 덱 객체에 스킨 반영 및 매칭 단계처럼 서버에 즉시 자동 저장
            if (DeckManager.instance.CurrentlyEditingDeck != null)
            {
                DeckManager.instance.CurrentlyEditingDeck.leaderSkinId = selectedSkin.productId;

                if (!string.IsNullOrEmpty(DeckManager.instance.CurrentlyEditingDeck.deckId) && DeckSaveManager_Firebase.instance != null)
                {
                    await DeckSaveManager_Firebase.instance.ServerUpdateDeck(DeckManager.instance.CurrentlyEditingDeck);
                    Debug.Log($"[DeckSkinSelectManager] 서버에 스킨 변경 사항 즉시 저장 완료: {selectedSkin.productId}");
                }
            }
        }

        Debug.Log($"[DeckSkinSelectManager] 덱 리더 스킨 설정 완료: {selectedSkin.productName} ({selectedSkin.productId})");

        // 2. 슬롯 UI들의 '장착중' 뱃지 상태 실시간 갱신
        UpdateAllSlotEquippedStates(selectedSkin.productId);

        // 3. 리더 미리보기 이미지 갱신
        if (currentLeaderPreviewImage != null)
        {
            var master = LeaderCardDisplay.LoadSkinData(selectedSkin.productId, selectedSkin.targetClasses?.FirstOrDefault());
            if (master != null && master.skinSprite != null)
            {
                currentLeaderPreviewImage.sprite = master.skinSprite;
            }
            else if (!string.IsNullOrEmpty(selectedSkin.image_url))
            {
                ShopManager.LoadProductImage(selectedSkin.image_url, currentLeaderPreviewImage);
            }
        }
    }

    /// <summary>
    /// 모든 슬롯의 장착 뱃지 상태를 갱신합니다.
    /// </summary>
    private void UpdateAllSlotEquippedStates(string equippedSkinId)
    {
        foreach (var slot in createdSlotUIs)
        {
            if (slot != null)
            {
                string slotSkinId = slot.MasterSkinData != null ? slot.MasterSkinData.skinId : (slot.CurrentSkinData != null ? slot.CurrentSkinData.productId : "");
                bool isEquipped = string.Equals(slotSkinId, equippedSkinId, StringComparison.OrdinalIgnoreCase);
                slot.SetEquippedState(isEquipped);
            }
        }
    }

    /// <summary>
    /// 리더 미리보기 이미지를 현재 장착된 스킨으로 갱신합니다.
    /// </summary>
    private void UpdateLeaderPreview(string equippedSkinId, List<ProductData> skinList)
    {
        if (currentLeaderPreviewImage == null) return;

        var master = LeaderCardDisplay.LoadSkinData(equippedSkinId, GetCurrentDeckClass());
        if (master != null && master.skinSprite != null)
        {
            currentLeaderPreviewImage.sprite = master.skinSprite;
            return;
        }

        ProductData targetSkin = skinList.FirstOrDefault(s => s.productId == equippedSkinId);
        if (targetSkin != null && !string.IsNullOrEmpty(targetSkin.image_url))
        {
            ShopManager.LoadProductImage(targetSkin.image_url, currentLeaderPreviewImage);
        }
    }

    /// <summary>
    /// 기존 생성된 모든 스킨 슬롯 제거
    /// </summary>
    private void ClearSkinSlots()
    {
        createdSlotUIs.Clear();
        if (skinSlotParent == null) return;

        foreach (Transform child in skinSlotParent)
        {
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// 현재 편집 중인 덱이 존재하는지(선택되었는지) 여부를 반환합니다.
    /// </summary>
    public bool IsDeckSelected()
    {
        if (DeckManager.instance == null) return false;

        return (DeckManager.instance.CurrentlyEditingDeck != null && !string.IsNullOrEmpty(DeckManager.instance.CurrentlyEditingDeck.deckClass))
            || !string.IsNullOrEmpty(DeckManager.instance.SelectedClass);
    }

    /// <summary>
    /// 현재 편집 중인 덱의 직업명을 반환합니다. 덱이 없으면 null을 반환합니다.
    /// </summary>
    private string GetCurrentDeckClass()
    {
        if (DeckManager.instance != null)
        {
            if (DeckManager.instance.CurrentlyEditingDeck != null && !string.IsNullOrEmpty(DeckManager.instance.CurrentlyEditingDeck.deckClass))
            {
                return DeckManager.instance.CurrentlyEditingDeck.deckClass;
            }
            if (!string.IsNullOrEmpty(DeckManager.instance.SelectedClass))
            {
                return DeckManager.instance.SelectedClass;
            }
        }
        return null;
    }

    /// <summary>
    /// 현재 장착된 스킨 ID를 반환합니다.
    /// </summary>
    private string GetCurrentEquippedSkinId(string deckClass)
    {
        if (DeckManager.instance != null && !string.IsNullOrEmpty(DeckManager.instance.selectedSkinId))
        {
            return DeckManager.instance.selectedSkinId;
        }
        return !string.IsNullOrEmpty(deckClass) ? $"Skin_{deckClass}_Default" : "";
    }

    /// <summary>
    /// 직업 기본 스킨 ProductData를 생성합니다.
    /// </summary>
    private ProductData CreateDefaultSkinProduct(string deckClass)
    {
        var skinAsset = LeaderCardDisplay.LoadSkinData(null, deckClass);
        if (skinAsset != null)
        {
            return new ProductData
            {
                productId = skinAsset.skinId,
                productName = skinAsset.skinName,
                description = skinAsset.description,
                image_url = $"Items/Skins/{deckClass}_Skin_0001.png",
                price = 0,
                isVisibleInShop = false,
                targetClasses = new List<string> { deckClass }
            };
        }

        return new ProductData
        {
            productId = $"Skin_{deckClass}_0001",
            productName = $"{deckClass} 기본 리더",
            description = $"{deckClass}의 기본 리더 스킨입니다.",
            image_url = $"Items/Skins/{deckClass}_Skin_0001.png",
            price = 0,
            isVisibleInShop = false,
            targetClasses = new List<string> { deckClass }
        };
    }
}
