using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 덱 편성 화면의 스킨 목록 ScrollView에 생성되는 개별 스킨 슬롯 UI입니다.
/// 보유 스킨은 컬러, 미보유 스킨은 흑백으로 표시됩니다.
/// </summary>
public class SkinSlotUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Image skinImage;                    // 스킨 썸네일/아이콘 이미지
    [SerializeField] private TextMeshProUGUI skinNameText;        // 스킨 이름 텍스트
    [SerializeField] private GameObject equippedBadge;            // '장착중' 표시 오브젝트/뱃지
    [SerializeField] private GameObject selectedHighlight;        // 선택/하이라이트 테두리 (선택 사항)
    [SerializeField] private GameObject lockIcon;                 // 미보유 시 표시할 자물쇠 아이콘 (선택 사항)
    [SerializeField] private Button slotButton;                   // 슬롯 전체 클릭 버튼

    [Header("머티리얼 (흑백)")]
    [SerializeField] private Material grayscaleMaterial;          // 흑백 표현용 UI Material (없으면 자동 생성)

    public ProductData CurrentSkinData { get; private set; }
    public SkinData MasterSkinData { get; private set; }
    public bool IsEquipped { get; private set; }
    public bool IsOwned { get; private set; }

    private Action<ProductData, bool> onClickAction;
    private Action<SkinData, bool> onSkinDataClickAction;
    private static Material sharedGrayscaleMaterial;

    private void Awake()
    {
        if (slotButton == null)
        {
            slotButton = GetComponent<Button>();
        }

        if (slotButton != null)
        {
            slotButton.onClick.AddListener(OnSlotClicked);
        }

        // 흑백 머티리얼이 인스펙터에 지정되지 않은 경우 UI/Grayscale 셰이더로 자동 생성
        if (grayscaleMaterial == null)
        {
            if (sharedGrayscaleMaterial == null)
            {
                Shader grayShader = Shader.Find("UI/Grayscale");
                if (grayShader != null)
                {
                    sharedGrayscaleMaterial = new Material(grayShader);
                }
            }
            grayscaleMaterial = sharedGrayscaleMaterial;
        }
    }

    /// <summary>
    /// 신규 SkinData 에셋을 기반으로 슬롯의 데이터를 초기화합니다.
    /// </summary>
    public void SetSkinData(SkinData skinData, bool isEquipped, bool isOwned, Action<SkinData, bool> onClickCallback)
    {
        MasterSkinData = skinData;
        CurrentSkinData = null;
        IsEquipped = isEquipped;
        IsOwned = isOwned;
        onSkinDataClickAction = onClickCallback;
        onClickAction = null;

        if (skinData == null) return;

        // 1. 스킨 이름 설정
        if (skinNameText != null)
        {
            skinNameText.text = skinData.skinName;
        }

        // 2. 스킨 고화질 스프라이트 로드
        if (skinImage != null)
        {
            skinImage.sprite = skinData.GetIconOrMainSprite();
        }

        // 3. 비주얼 갱신
        UpdateVisualState();
    }

    /// <summary>
    /// 기존 ProductData 상품 데이터를 기반으로 슬롯의 데이터를 초기화합니다.
    /// (내부적으로 매칭되는 SkinData 에셋이 있으면 우선적으로 고화질 스프라이트를 적용합니다)
    /// </summary>
    public void SetSkinData(ProductData skinData, bool isEquipped, bool isOwned, Action<ProductData, bool> onClickCallback)
    {
        CurrentSkinData = skinData;
        IsEquipped = isEquipped;
        IsOwned = isOwned;
        onClickAction = onClickCallback;
        onSkinDataClickAction = null;

        if (skinData == null) return;

        // 매칭되는 SkinData 에셋 조회
        MasterSkinData = LeaderCardDisplay.LoadSkinData(skinData.productId, skinData.targetClasses?.FirstOrDefault());

        // 1. 스킨 이름 설정
        if (skinNameText != null)
        {
            skinNameText.text = MasterSkinData != null ? MasterSkinData.skinName : skinData.productName;
        }

        // 2. 스킨 이미지 로드
        if (skinImage != null)
        {
            if (MasterSkinData != null && MasterSkinData.GetIconOrMainSprite() != null)
            {
                skinImage.sprite = MasterSkinData.GetIconOrMainSprite();
            }
            else if (!string.IsNullOrEmpty(skinData.image_url))
            {
                ShopManager.LoadProductImage(skinData.image_url, skinImage);
            }
        }

        // 3. 보유/미보유 상태(흑백/컬러) 및 장착 뱃지 갱신
        UpdateVisualState();
    }

    /// <summary>
    /// 보유(컬러) / 미보유(흑백) 및 장착 상태를 시각적으로 반영합니다.
    /// </summary>
    private void UpdateVisualState()
    {
        if (skinImage != null)
        {
            if (IsOwned)
            {
                // 보유 스킨: 컬러 표시
                skinImage.material = null;
                skinImage.color = Color.white;
            }
            else
            {
                // 미보유 스킨: 흑백 머티리얼 적용 (없을 경우 짙은 회색 틴트)
                if (grayscaleMaterial != null)
                {
                    skinImage.material = grayscaleMaterial;
                    skinImage.color = Color.white;
                }
                else
                {
                    skinImage.material = null;
                    skinImage.color = new Color(0.35f, 0.35f, 0.35f, 0.85f);
                }
            }
        }

        // 자물쇠 아이콘 표시 여부 (미보유 시 표시)
        if (lockIcon != null)
        {
            lockIcon.SetActive(!IsOwned);
        }

        // 장착중 뱃지 표시 (보유 중이면서 장착된 경우에만)
        SetEquippedState(IsEquipped && IsOwned);
    }

    /// <summary>
    /// 장착중 상태 UI를 갱신합니다.
    /// </summary>
    public void SetEquippedState(bool isEquipped)
    {
        IsEquipped = isEquipped;
        if (equippedBadge != null)
        {
            equippedBadge.SetActive(isEquipped && IsOwned);
        }
    }

    /// <summary>
    /// 하이라이트 상태를 설정합니다.
    /// </summary>
    public void SetHighlight(bool isHighlighted)
    {
        if (selectedHighlight != null)
        {
            selectedHighlight.SetActive(isHighlighted);
        }
    }

    private void OnSlotClicked()
    {
        if (MasterSkinData != null && onSkinDataClickAction != null)
        {
            onSkinDataClickAction.Invoke(MasterSkinData, IsOwned);
        }
        else if (CurrentSkinData != null)
        {
            onClickAction?.Invoke(CurrentSkinData, IsOwned);
        }
    }
}
