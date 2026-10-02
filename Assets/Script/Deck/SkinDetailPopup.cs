using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 덱 편성 화면에서 스킨을 클릭했을 때 열리는 상세 정보 팝업 창입니다.
/// 스킨 일러스트, 이름, 설명 및 [선택] / [닫기] 기능을 제공합니다.
/// 덱 미선택 상태이거나 미보유 스킨인 경우 '선택' 버튼이 비활성화됩니다.
/// </summary>
public class SkinDetailPopup : MonoBehaviour
{
    [Header("UI 요소 연결")]
    [SerializeField] private GameObject popupPanel;                   // 팝업 패널 오브젝트 (없으면 gameObject 사용)
    [SerializeField] private Image skinIllustrationImage;             // 스킨 일러스트 크게 보여줄 이미지
    [SerializeField] private TextMeshProUGUI skinTitleText;            // 스킨 이름 텍스트
    [SerializeField] private TextMeshProUGUI skinDescriptionText;      // 스킨 설명 텍스트
    [SerializeField] private Button selectButton;                      // '선택' 버튼
    [SerializeField] private TextMeshProUGUI selectButtonText;         // '선택' 버튼 내부 텍스트
    [SerializeField] private Button closeButton;                       // '닫기' 버튼
    [SerializeField] private UIPanelToggler panelToggler;              // 애니메이션용 토글러 (선택 사항)

    private ProductData currentSkinData;
    private SkinData masterSkinData;
    private Action<ProductData> onSelectAction;
    private Action<SkinData> onSelectSkinDataAction;
    private bool isCurrentlyOwned = true;
    private bool isDeckCurrentlySelected = true;

    private void Awake()
    {
        if (popupPanel == null) popupPanel = gameObject;

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (selectButton != null)
        {
            selectButton.onClick.AddListener(OnSelectButtonClicked);
        }
    }

    /// <summary>
    /// 신규 SkinData 에셋을 기반으로 팝업을 엽니다.
    /// (스킨 일러스트, 이름, 설명 및 장착된 4개 감정표현 미리보기를 함께 표시합니다)
    /// </summary>
    public void Open(SkinData skinData, bool isEquipped, bool isOwned, bool hasSelectedDeck, Action<SkinData> onSelectCallback)
    {
        masterSkinData = skinData;
        currentSkinData = null;
        onSelectSkinDataAction = onSelectCallback;
        onSelectAction = null;
        isCurrentlyOwned = isOwned;
        isDeckCurrentlySelected = hasSelectedDeck;

        if (skinData == null) return;

        // 1. 이름 설정
        if (skinTitleText != null)
        {
            skinTitleText.text = skinData.skinName;
        }

        // 2. 설명 및 4개 감정표현 미리보기 구성
        if (skinDescriptionText != null)
        {
            string desc = skinData.description;
            var emotes = skinData.GetValidEmotes();
            if (emotes != null && emotes.Count > 0)
            {
                desc += "\n\n<color=#FFD700><b>[장착 감정표현 4종]</b></color>";
                for (int i = 0; i < emotes.Count; i++)
                {
                    var e = emotes[i];
                    desc += $"\n• {e.buttonLabel}: \"{e.speechMessage}\"";
                }
            }
            skinDescriptionText.text = desc;
        }

        // 3. 일러스트 이미지 로드
        if (skinIllustrationImage != null)
        {
            skinIllustrationImage.sprite = skinData.skinSprite;
        }

        // 4. '선택' 버튼 상태 설정
        UpdateSelectButtonState(isEquipped, isOwned, hasSelectedDeck);

        // 5. 팝업 활성화
        if (panelToggler != null) panelToggler.ShowPanel();
        else if (popupPanel != null) popupPanel.SetActive(true);
    }

    /// <summary>
    /// 기존 ProductData 상품 데이터를 받아 팝업을 엽니다.
    /// (내부적으로 매칭되는 SkinData 에셋이 있으면 4개 감정표현 미리보기를 함께 표시합니다)
    /// </summary>
    public void Open(ProductData skinData, bool isEquipped, bool isOwned, bool hasSelectedDeck, Action<ProductData> onSelectCallback)
    {
        currentSkinData = skinData;
        masterSkinData = LeaderCardDisplay.LoadSkinData(skinData?.productId, skinData?.targetClasses?.FirstOrDefault());
        onSelectAction = onSelectCallback;
        onSelectSkinDataAction = null;
        isCurrentlyOwned = isOwned;
        isDeckCurrentlySelected = hasSelectedDeck;

        if (skinData == null) return;

        // 1. 이름 설정
        if (skinTitleText != null)
        {
            skinTitleText.text = masterSkinData != null ? masterSkinData.skinName : skinData.productName;
        }

        // 2. 설명 및 감정표현 채우기
        if (skinDescriptionText != null)
        {
            string desc = masterSkinData != null ? masterSkinData.description : skinData.description;
            if (masterSkinData != null)
            {
                var emotes = masterSkinData.GetValidEmotes();
                if (emotes != null && emotes.Count > 0)
                {
                    desc += "\n\n<color=#FFD700><b>[장착 감정표현 4종]</b></color>";
                    for (int i = 0; i < emotes.Count; i++)
                    {
                        var e = emotes[i];
                        desc += $"\n• {e.buttonLabel}: \"{e.speechMessage}\"";
                    }
                }
            }
            skinDescriptionText.text = desc;
        }

        // 3. 일러스트 이미지 로드
        if (skinIllustrationImage != null)
        {
            if (masterSkinData != null && masterSkinData.skinSprite != null)
            {
                skinIllustrationImage.sprite = masterSkinData.skinSprite;
            }
            else if (!string.IsNullOrEmpty(skinData.image_url))
            {
                ShopManager.LoadProductImage(skinData.image_url, skinIllustrationImage);
            }
        }

        // 4. '선택' 버튼 상태 설정 (덱 미선택 / 미보유 / 장착중 / 선택가능)
        UpdateSelectButtonState(isEquipped, isOwned, hasSelectedDeck);

        // 5. 팝업 활성화
        if (panelToggler != null)
        {
            panelToggler.ShowPanel();
        }
        else if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 보유 및 장착 여부, 덱 선택 상태에 따라 '선택' 버튼의 텍스트와 상호작용 여부를 변경합니다.
    /// </summary>
    public void UpdateSelectButtonState(bool isEquipped, bool isOwned, bool hasSelectedDeck)
    {
        if (selectButton != null)
        {
            TextMeshProUGUI btnText = selectButtonText != null
                ? selectButtonText
                : selectButton.GetComponentInChildren<TextMeshProUGUI>();

            if (!hasSelectedDeck)
            {
                // 1) 덱 미선택 상태
                selectButton.interactable = false;
                if (btnText != null) btnText.text = "덱 선택 필요";
            }
            else if (!isOwned)
            {
                // 2) 미보유 스킨
                selectButton.interactable = false;
                if (btnText != null) btnText.text = "미보유";
            }
            else if (isEquipped)
            {
                // 3) 이미 장착 중인 스킨
                selectButton.interactable = false;
                if (btnText != null) btnText.text = "장착중";
            }
            else
            {
                // 4) 덱이 선택되어 있고 보유 중인 스킨 -> 장착 가능
                selectButton.interactable = true;
                if (btnText != null) btnText.text = "선택";
            }
        }
    }

    /// <summary>
    /// '선택' 버튼 클릭 시 현재 스킨을 덱의 스킨으로 설정하고 팝업을 닫습니다.
    /// </summary>
    private void OnSelectButtonClicked()
    {
        if (isCurrentlyOwned && isDeckCurrentlySelected)
        {
            if (masterSkinData != null && onSelectSkinDataAction != null)
            {
                onSelectSkinDataAction.Invoke(masterSkinData);
            }
            else if (currentSkinData != null && onSelectAction != null)
            {
                onSelectAction.Invoke(currentSkinData);
            }

            UpdateSelectButtonState(true, true, true);
            Close();
        }
    }

    /// <summary>
    /// 팝업 창 닫기
    /// </summary>
    public void Close()
    {
        if (panelToggler != null)
        {
            panelToggler.HidePanel();
        }
        else if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }
}
