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

    // 이 슬롯이 나타내는 상품 데이터
    private ProductData currentProductData;

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

        // 상품 이름 표시
        if (productNameText != null)
        {
            productNameText.text = product.productName;
        }

        // 로컬 에셋 경로 또는 웹 URL에서 이미지 로드
        if (productImage != null && !string.IsNullOrEmpty(product.image_url))
        {
            LoadProductImage(product.image_url);
        }
        else if (productImage != null && string.IsNullOrEmpty(product.image_url))
        {
            Debug.LogWarning($"[ShopSlot] 상품 ID {product.productId}의 image_url이 비어 있습니다.", this);
        }
    }

    /// <summary>
    /// 로컬 에셋 경로(Assets/Sprite/Shop/...)에서 이미지를 로드하여 Image에 표시
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

        // 2. 로컬 파일 경로 조합 (기본: Assets/Sprite/Shop/)
        // imagePath 예: "Items/Skins/YuniNyang.png" 또는 "Items/CardPack/Yuni_001.png"
        string cleanPath = imagePath.TrimStart('/', '\\');
        
        // 시도 경로 1: Assets/Sprite/Shop/ + imagePath
        string fullPath = Path.Combine(Application.dataPath, "Sprite", "Shop", cleanPath);

        // 시도 경로 2: Assets/Sprite/Shop/Items/ + imagePath (경로에 Items가 중복되지 않은 경우)
        if (!File.Exists(fullPath))
        {
            fullPath = Path.Combine(Application.dataPath, "Sprite", "Shop", "Items", cleanPath);
        }

        // 파일이 존재하면 로컬 바이트를 읽어 스프라이트 생성
        if (File.Exists(fullPath))
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(fullPath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    Rect rect = new Rect(0, 0, texture.width, texture.height);
                    Vector2 pivot = new Vector2(0.5f, 0.5f);
                    productImage.sprite = Sprite.Create(texture, rect, pivot);
                    productImage.preserveAspect = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ShopSlot] 로컬 이미지 로드 중 오류 ({fullPath}): {ex.Message}");
            }
        }

        // 3. Resources 폴더 로드 시도 (확장자 제거)
        string resPath = cleanPath;
        int dotIndex = resPath.LastIndexOf('.');
        if (dotIndex > 0)
        {
            resPath = resPath.Substring(0, dotIndex);
        }

        Sprite resSprite = Resources.Load<Sprite>(resPath);
        if (resSprite != null)
        {
            productImage.sprite = resSprite;
            productImage.preserveAspect = true;
            return;
        }

        Debug.LogWarning($"[ShopSlot] 이미지를 찾을 수 없습니다: '{imagePath}' (확인한 경로: {fullPath})", this);
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
