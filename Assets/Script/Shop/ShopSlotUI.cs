using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ShopSlotUI : MonoBehaviour
{
    [Header("UI 요소 참조")]
    public Image productImage;              // 상품 이미지 Image 컴포넌트
    public TextMeshProUGUI productNameText; // 상품 이름 TextMeshProUGUI 컴포넌트
    public Button button;                   // 슬롯 버튼
    public TextMeshProUGUI statusText;      // 상태 텍스트 (기간종료, 이미구매한 상품 등)
    public GameObject disabledOverlay;      // 비활성화/품절 오버레이
    public TextMeshProUGUI priceText;       // 상품 가격 TextMeshProUGUI 컴포넌트
    public Image currencyIconImage;         // 소비 재화 아이콘 Image 컴포넌트

    [Header("재화 아이콘 스프라이트 (인스펙터 직접 설정 가능)")]
    public Sprite goldIconSprite;          // 골드 아이콘 스프라이트
    public Sprite stellastoneIconSprite;   // 성석 아이콘 스프라이트
    public Sprite stardustIconSprite;      // 별가루 아이콘 스프라이트

    // 이 슬롯이 나타내는 상품 데이터
    private ProductData currentProductData;

    /// <summary>
    /// 재화 종류에 따라 슬롯 자체 등록 스프라이트 또는 ShopManager 기본 스프라이트 반환
    /// </summary>
    public Sprite GetCurrencySprite(PriceCurrency currencyType)
    {
        Sprite selectedSprite = null;

        // 1. 슬롯 인스펙터에 직접 등록된 스프라이트 우선 확인
        switch (currencyType)
        {
            case PriceCurrency.Gold:
                selectedSprite = goldIconSprite;
                break;
            case PriceCurrency.Stellastone:
                selectedSprite = stellastoneIconSprite;
                break;
            case PriceCurrency.Stardust:
                selectedSprite = stardustIconSprite;
                break;
        }

        // 2. 미등록 시 ShopManager의 공용 스프라이트로 fallback
        if (selectedSprite == null && ShopManager.Instance != null)
        {
            selectedSprite = ShopManager.Instance.GetCurrencySprite(currencyType);
        }

        return selectedSprite;
    }

    /// <summary>
    /// ShopManager에서 슬롯 생성 시 호출하여 상품 데이터를 바인딩합니다.
    /// </summary>
    public void SetProductData(ProductData product)
    {
        currentProductData = product;

        // 슬롯 클릭 시 팝업 열기 이벤트 연결
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnSlotClicked);
        }

        // 리더 스킨 상품인 경우 신규 SkinData 에셋 우선 조회 (fallbackToDefault: false로 기본스킨 대체 방지)
        SkinData masterSkin = null;
        bool isProductError = false;

        if (product.category_Id == ShopCategory.LeaderSkin && !product.productId.StartsWith("CardPack", StringComparison.OrdinalIgnoreCase))
        {
            string targetClass = product.targetClasses != null && product.targetClasses.Count > 0 ? product.targetClasses[0] : null;
            masterSkin = LeaderCardDisplay.LoadSkinData(product.productId, targetClass, fallbackToDefault: false);

            // 스킨 카테고리인데 해당하는 SkinData 에셋을 찾지 못한 경우 (오타, 에셋 누락 등)
            if (masterSkin == null)
            {
                isProductError = true;
                Debug.LogWarning($"[ShopSlot] ⚠️ 스킨 상품 에셋 누락/오타 감지: {product.productId} (상품오류로 표시)");
            }
        }

        // 상품 이름 표시 (오류 시 '상품오류' 표시, 정상 시 SkinData 또는 product.productName)
        if (productNameText != null)
        {
            if (isProductError)
            {
                productNameText.text = "상품오류";
            }
            else
            {
                productNameText.text = masterSkin != null ? masterSkin.skinName : product.productName;
            }
        }

        // 상태 확인 (상품오류 / 기간 종료 / 이미 구매 완료)
        bool isExpired = ShopManager.IsProductExpired(product);
        bool isAlreadyPurchased = (product.remainingPurchaseLimit == 0) || (!product.isPurchasable && !isExpired);

        if (isProductError)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "상품오류";
            }
            if (disabledOverlay != null) disabledOverlay.SetActive(true);
            if (button != null) button.interactable = false; // 구매 불가 차단
        }
        else if (isExpired)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "기간종료";
            }
            if (disabledOverlay != null) disabledOverlay.SetActive(true);
            if (button != null) button.interactable = true;
        }
        else if (isAlreadyPurchased)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "이미구매한 상품";
            }
            if (disabledOverlay != null) disabledOverlay.SetActive(true);
            if (button != null) button.interactable = true;
        }
        else
        {
            if (statusText != null) statusText.gameObject.SetActive(false);
            if (disabledOverlay != null) disabledOverlay.SetActive(false);
            if (button != null) button.interactable = true;
        }

        // 가격 및 소비 재화 아이콘 표시
        if (priceText != null)
        {
            if (isProductError)
            {
                priceText.text = "-";
            }
            else
            {
                priceText.text = product.price > 0 ? product.price.ToString("N0") : "무료";
            }
        }

        if (currencyIconImage != null)
        {
            if (isProductError || product.price <= 0)
            {
                currencyIconImage.gameObject.SetActive(false);
            }
            else
            {
                Sprite currencySprite = GetCurrencySprite(product.currency);
                currencyIconImage.sprite = currencySprite;
                currencyIconImage.preserveAspect = true;
                currencyIconImage.gameObject.SetActive(currencySprite != null);
            }
        }

        // 이미지 로드 (오류 시 기본 스킨 이미지가 노출되지 않도록 스프라이트 제거)
        if (isProductError)
        {
            if (productImage != null)
            {
                productImage.sprite = null;
            }
        }
        else if (masterSkin != null && masterSkin.GetIconOrMainSprite() != null && productImage != null)
        {
            productImage.sprite = masterSkin.GetIconOrMainSprite();
        }
        else if (productImage != null && !string.IsNullOrEmpty(product.image_url))
        {
            LoadProductImage(product.image_url);
        }
        else if (productImage != null && string.IsNullOrEmpty(product.image_url))
        {
            Debug.LogWarning($"[ShopSlot] 상품 ID {product.productId}의 image_url이 비어 있습니다.", this);
        }
    }

    /// <summary>
    /// 로컬 에셋 경로 또는 Resources에서 이미지를 로드하여 Image에 표시
    /// </summary>
    private void LoadProductImage(string imagePath)
    {
        if (productImage == null || string.IsNullOrWhiteSpace(imagePath)) return;

        // 1. 웹 URL인 경우 비동기 다운로드 처리
        if (imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            StartCoroutine(LoadImageFromWeb(imagePath));
            return;
        }

        // 2. ShopManager의 종합 이미지 로더 활용 (Resources, Items 하위폴더, 카드팩 등 자동 대응)
        ShopManager.LoadProductImage(imagePath, productImage);
    }

    /// <summary>
    /// 웹 URL인 경우 백업으로 다운로드하여 표시
    /// </summary>
    private IEnumerator LoadImageFromWeb(string imageUrl)
    {
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                if (texture != null && productImage != null)
                {
                    Rect rect = new Rect(0, 0, texture.width, texture.height);
                    Vector2 pivot = new Vector2(0.5f, 0.5f);
                    productImage.sprite = Sprite.Create(texture, rect, pivot);
                    productImage.preserveAspect = true;
                }
            }
            else
            {
                Debug.LogError($"[ShopSlot] 웹 이미지 다운로드 실패 (URL: {imageUrl}): {webRequest.error}");
            }
        }
    }

    /// <summary>
    /// 슬롯 클릭 시 ShopManager를 통해 상품 상세 팝업 오픈
    /// </summary>
    public void OnSlotClicked()
    {
        if (currentProductData != null)
        {
            Debug.Log($"[ShopSlot] 슬롯 클릭됨: {currentProductData.productName} (ID: {currentProductData.productId})");
            ShopManager.Instance.OpenProductPopup(currentProductData);
        }
        else
        {
            Debug.LogWarning("[ShopSlot] 클릭된 슬롯에 상품 데이터가 없습니다.");
        }
    }
}
