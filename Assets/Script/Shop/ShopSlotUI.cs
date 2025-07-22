using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ShopSlotUI : MonoBehaviour
{
    // --- UI 요소 참조 ---
    // 인스펙터에서 드래그하여 할당할 UI Image 컴포넌트
    public Image productImage;
    // 인스펙터에서 드래그하여 할당할 UI Text 컴포넌트 (상품 이름 표시용)
    public TextMeshProUGUI productNameText;
    public Button button;

    // --- 데이터 저장 ---
    // 이 슬롯이 나타내는 상품의 모든 데이터를 저장할 변수
    private ProductData currentProductData;

    // --- 데이터 설정 함수 ---
    // ShopManager에서 이 슬롯을 생성할 때 호출하여 상품 데이터를 전달합니다.
    public void SetProductData(ProductData product)
    {
        button.onClick.AddListener(ShopPopupManager.Instance.uIPanelToggler.ShowPanel);

        currentProductData = product; // 모든 상품 데이터를 내부 변수에 저장

        // 상품 이름 표시
        if (productNameText != null)
        {
            productNameText.text = product.name;
        }
        else
        {
            Debug.LogWarning("Product Name Text가 할당되지 않았습니다.", this);
        }

        // 이미지 로드 및 표시 (image_url 사용)
        if (productImage != null && !string.IsNullOrEmpty(product.image_url))
        {
            StartCoroutine(LoadImage(product.image_url));
        }
        else if (productImage == null)
        {
            Debug.LogWarning("Product Image가 할당되지 않았습니다.", this);
        }
        else if (string.IsNullOrEmpty(product.image_url))
        {
            Debug.LogWarning($"상품 ID {product.id}의 image_url이 비어 있습니다.", this);
        }
    }

    // URL에서 이미지를 로드하여 Image 컴포넌트에 할당하는 코루틴
    private IEnumerator LoadImage(string imageUrl)
    {
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"이미지 로드 오류 (URL: {imageUrl}): {webRequest.error}");
            }
            else if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                if (texture != null)
                {
                    // Texture2D를 Sprite로 변환하여 Image 컴포넌트에 할당
                    Rect rect = new Rect(0, 0, texture.width, texture.height);
                    Vector2 pivot = new Vector2(0.5f, 0.5f); // 중앙 피봇
                    productImage.sprite = Sprite.Create(texture, rect, pivot);
                    productImage.preserveAspect = true; // 이미지 비율 유지
                }
                else
                {
                    Debug.LogError($"이미지 로드 실패: 텍스처를 가져올 수 없습니다. (URL: {imageUrl})");
                }
            }
        }
    }

    // --- 슬롯 클릭 시 호출될 함수 (버튼에 연결) ---
    // 이 함수는 슬롯 프리팹 내의 Button 컴포넌트의 OnClick() 이벤트에 연결되어야 합니다.
    public void OnSlotClicked()
    {
        if (currentProductData != null)
        {
            Debug.Log($"슬롯 클릭됨! 상품 ID: {currentProductData.id}, 이름: {currentProductData.name}");
            ShopPopupManager.Instance.ShowProductDetails(currentProductData);
        }
        else
        {
            Debug.LogWarning("클릭된 슬롯에 상품 데이터가 없습니다.");
        }
    }
}
