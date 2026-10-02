using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 매칭 씬(Lobby)에서 현재 선택된 덱의 직업에 맞는 보유 스킨 목록을 보여주고,
/// 스킨을 선택하여 즉시 교체할 수 있도록 제어하는 팝업 스크립트입니다.
/// </summary>
public class SkinSelectPopup : MonoBehaviour
{
    [Header("UI 요소 연결")]
    [SerializeField] private Button closeButton;                       // 팝업 닫기 버튼
    [SerializeField] private Transform skinListParent;                 // 스킨 슬롯들이 생성될 Content
    [SerializeField] private GameObject skinSlotPrefab;                // 스킨 슬롯 프리팹 (SkinSlotUI 컴포넌트 부착)

    [Header("상세 팝업 연결")]
    [SerializeField] private SkinDetailPopup skinDetailPopup;          // 스킨 클릭 시 열리는 상세 정보 팝업
    [SerializeField] private UIPanelToggler panelToggler;              // 팝업 열기/닫기 애니메이션용 토글러

    private DeckData targetDeck;
    private Action<string> onSkinSelectedCallback;
    private List<SkinSlotUI> createdSlotUIs = new List<SkinSlotUI>();

    private void Awake()
    {
        if (panelToggler == null)
        {
            panelToggler = GetComponent<UIPanelToggler>();
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }
    }

    /// <summary>
    /// 스킨 선택 팝업을 열고, 현재 선택된 덱의 보유 스킨 목록을 로드합니다.
    /// </summary>
    public void OpenPopup(DeckData currentDeck, Action<string> onSkinChanged)
    {
        if (currentDeck == null)
        {
            Debug.LogWarning("[SkinSelectPopup] 선택된 덱 정보가 없어 팝업을 열 수 없습니다.");
            return;
        }

        targetDeck = currentDeck;
        onSkinSelectedCallback = onSkinChanged;

        // 1. 자기 자신 게임오브젝트를 먼저 활성화
        gameObject.SetActive(true);

        // 2. 팝업 패널 애니메이션 열기
        if (panelToggler != null)
        {
            panelToggler.ShowPanel();
        }

        // 3. 매칭 씬 관리자인 MatchingManager를 통해 안전하게 코루틴 실행
        if (MatchingManager.Instance != null)
        {
            MatchingManager.Instance.StartCoroutine(LoadOwnedSkinsCoroutine());
        }
        else
        {
            StartCoroutine(LoadOwnedSkinsCoroutine());
        }
    }

    /// <summary>
    /// 팝업 창 닫기
    /// </summary>
    public void ClosePopup()
    {
        if (skinDetailPopup != null)
        {
            skinDetailPopup.Close();
        }

        if (panelToggler != null)
        {
            panelToggler.HidePanel();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 현재 덱의 직업에 해당하는 '보유 스킨'만 필터링하여 슬롯을 생성하는 코루틴
    /// </summary>
    private IEnumerator LoadOwnedSkinsCoroutine()
    {
        ClearSkinSlots();

        string deckClass = targetDeck.deckClass;

        // 1. 전체 리더 스킨 목록 가져오기
        List<ProductData> allSkins = null;
        if (GameClient.Instance != null)
        {
            yield return GameClient.Instance.GetLeaderSkinsAsync(skins => allSkins = skins);
        }

        // 1-1. Resources/Skins 에셋들을 마스터 데이터로 병합 보장
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

        // 2. 내 계정의 보유 스킨 ID 목록
        List<string> ownedSkinIds = (GameClient.Instance != null && GameClient.Instance.CurrentUser != null)
            ? GameClient.Instance.CurrentUser.ownedSkins
            : new List<string>();

        // 3. 현재 직업의 '보유 스킨'만 필터링
        List<ProductData> ownedClassSkins = new List<ProductData>();

        if (allSkins != null)
        {
            foreach (var skin in allSkins)
            {
                if (skin == null) continue;

                // 직업 일치 여부 확인 (해당 직업 또는 강지/중립 스킨)
                bool matchesClass = skin.targetClasses == null || 
                                    skin.targetClasses.Count == 0 || 
                                    skin.targetClasses.Contains(deckClass, StringComparer.OrdinalIgnoreCase) ||
                                    skin.targetClasses.Contains("Gangzi", StringComparer.OrdinalIgnoreCase) ||
                                    skin.targetClasses.Contains("Common", StringComparer.OrdinalIgnoreCase);

                if (!matchesClass) continue;

                // 0001 기본 스킨이거나 ownedSkins에 포함되어 보유 중인 스킨만 선별
                bool isOwned = skin.productId.EndsWith("0001") ||
                               skin.productId.EndsWith("_Default") ||
                               !skin.isVisibleInShop ||
                               (ownedSkinIds != null && ownedSkinIds.Contains(skin.productId)) ||
                               (skin.remainingPurchaseLimit == 0);

                if (isOwned && !ownedClassSkins.Any(s => s.productId == skin.productId))
                {
                    ownedClassSkins.Add(skin);
                }
            }
        }

        // 기본 스킨 누락 대비 fallback (강지 기본 스킨 및 해당 직업 기본 스킨)
        if (!ownedClassSkins.Any(s => s.productId.Contains("Gangzi") && (s.productId.EndsWith("0001") || s.productId.EndsWith("_Default"))))
        {
            ownedClassSkins.Add(CreateDefaultSkinProduct("Gangzi"));
        }
        if (!string.Equals(deckClass, "Gangzi", StringComparison.OrdinalIgnoreCase) && 
            !ownedClassSkins.Any(s => s.productId.Contains(deckClass) && (s.productId.EndsWith("0001") || s.productId.EndsWith("_Default"))))
        {
            ownedClassSkins.Add(CreateDefaultSkinProduct(deckClass));
        }

        // 정렬: 강지(중립) 스킨을 최상단에 배치 -> ID 오름차순
        ownedClassSkins = ownedClassSkins.OrderBy(skin =>
        {
            bool isGangzi = (skin.targetClasses != null && skin.targetClasses.Contains("Gangzi", StringComparer.OrdinalIgnoreCase)) ||
                            (skin.productId != null && skin.productId.Contains("Gangzi", StringComparison.OrdinalIgnoreCase));
            return isGangzi ? 0 : 1; // 강지(중립) 스킨 최상단 (0)
        }).ThenBy(s => s.productId).ToList();

        // 4. 현재 장착된 스킨 ID
        string currentEquippedSkinId = targetDeck.GetEquippedSkinId();

        // 5. 슬롯 UI 생성
        foreach (var skinData in ownedClassSkins)
        {
            bool isEquipped = (skinData.productId == currentEquippedSkinId);
            CreateSkinSlot(skinData, isEquipped);
        }
    }

    private void CreateSkinSlot(ProductData skinData, bool isEquipped)
    {
        if (skinListParent == null || skinSlotPrefab == null) return;

        GameObject slotObj = Instantiate(skinSlotPrefab, skinListParent);
        slotObj.name = $"SkinSlot_{skinData.productId}";

        SkinSlotUI slotUI = slotObj.GetComponent<SkinSlotUI>();
        if (slotUI != null)
        {
            // 장착 가능한 보유 스킨이므로 isOwned = true로 전달
            slotUI.SetSkinData(skinData, isEquipped, isOwned: true, OnSkinSlotClicked);
            createdSlotUIs.Add(slotUI);
        }
    }

    /// <summary>
    /// 스킨 슬롯 클릭 시 2차 팝업(상세 정보 및 [사용]/[닫기]) 열기
    /// </summary>
    private void OnSkinSlotClicked(ProductData skinData, bool isOwned)
    {
        if (skinData == null || targetDeck == null) return;

        string currentEquippedSkinId = targetDeck.GetEquippedSkinId();
        bool isEquipped = (skinData.productId == currentEquippedSkinId);

        if (skinDetailPopup != null)
        {
            skinDetailPopup.Open(skinData, isEquipped, isOwned: true, hasSelectedDeck: true, OnSkinUseConfirmed);
        }
    }

    /// <summary>
    /// 상세 팝업에서 [사용] 버튼 클릭 시 호출
    /// </summary>
    private void OnSkinUseConfirmed(ProductData selectedSkin)
    {
        if (selectedSkin == null || targetDeck == null) return;

        Debug.Log($"[SkinSelectPopup] 리더 스킨 교체 확정: {selectedSkin.productName} ({selectedSkin.productId})");

        // 1. 콜백으로 MatchingManager에 새 스킨 ID 전달
        onSkinSelectedCallback?.Invoke(selectedSkin.productId);

        // 2. 슬롯 UI들의 '장착중' 뱃지 상태 즉시 갱신
        foreach (var slot in createdSlotUIs)
        {
            if (slot != null)
            {
                string slotSkinId = slot.MasterSkinData != null ? slot.MasterSkinData.skinId : (slot.CurrentSkinData != null ? slot.CurrentSkinData.productId : "");
                slot.SetEquippedState(string.Equals(slotSkinId, selectedSkin.productId, StringComparison.OrdinalIgnoreCase));
            }
        }

        // 3. 팝업 닫기
        ClosePopup();
    }

    private void ClearSkinSlots()
    {
        createdSlotUIs.Clear();
        if (skinListParent == null) return;

        foreach (Transform child in skinListParent)
        {
            Destroy(child.gameObject);
        }
    }

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
