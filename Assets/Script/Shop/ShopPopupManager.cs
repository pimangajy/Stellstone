using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class ShopPopupManager : MonoBehaviour
{
    // --- 싱글톤 패턴 ---
    // 다른 스크립트에서 이 매니저에 쉽게 접근할 수 있도록 싱글톤으로 만듭니다.
    public static ShopPopupManager Instance { get; private set; }

    public UIPanelToggler uIPanelToggler;
    public QuantitySelector quantitySelector;

    [Header("UI Elements")]
    public GameObject popupPanel; // 팝업창 전체를 감싸는 Panel GameObject
    public TextMeshProUGUI productNameText;   // 상품 이름 표시
    public TextMeshProUGUI productDescriptionText; // 상품 설명 표시
    public Image currencyIconImage; // 재화 아이콘 이미지 표시를 위한 Image 컴포넌트

    // public Text productSaleDatesText; // 판매 기간 표시

    [Header("Buttons")]
    public Button closeButton; // 팝업 닫기 버튼
    public Button purchaseButton; // 구매 버튼 (추후 구현)

    // 현재 팝업에 표시 중인 상품 데이터
    private ProductData currentDisplayedProduct;

    private void Awake()
    {
        // 싱글톤 인스턴스 설정
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        // 팝업창은 시작 시 비활성화
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        // 버튼 이벤트 리스너 연결
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(uIPanelToggler.HidePanel);
        }
        // if (purchaseButton != null)
        // {
        //     purchaseButton.onClick.AddListener(OnPurchaseButtonClicked); // 추후 구현
        // }
    }

    // 팝업을 열고 상품 데이터를 표시하는 함수
    public void ShowProductDetails(ProductData product)
    {
        currentDisplayedProduct = product; // 현재 표시할 상품 데이터 저장

        if (popupPanel != null)
        {
            PopulatePopupUI(product);   // UI 요소에 데이터 채우기
            quantitySelector.UpdateQuantity(1);
        }
        else
        {
            Debug.LogError("팝업 패널이 할당되지 않았습니다.");
        }
    }

    // URL에서 이미지를 로드하여 Image 컴포넌트에 할당하는 코루틴 (재사용성을 위해 Image 컴포넌트와 타입 인자 추가)
    private IEnumerator LoadImage(string imageUrl, Image targetImage, string imageType)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            Debug.LogWarning($"{imageType} 이미지 URL이 비어 있습니다.");
            if (targetImage != null) targetImage.sprite = null;
            yield break;
        }
        if (targetImage == null)
        {
            Debug.LogError($"{imageType} 이미지를 표시할 Image 컴포넌트가 할당되지 않았습니다.");
            yield break;
        }

        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError ||
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"{imageType} 이미지 로드 오류 (URL: {imageUrl}): {webRequest.error}");
                targetImage.sprite = null; // 오류 시 이미지 초기화
            }
            else if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);
                if (texture != null)
                {
                    Rect rect = new Rect(0, 0, texture.width, texture.height);
                    Vector2 pivot = new Vector2(0.5f, 0.5f);
                    targetImage.sprite = Sprite.Create(texture, rect, pivot);
                    targetImage.preserveAspect = true;
                }
                else
                {
                    Debug.LogError($"{imageType} 이미지 로드 실패: 텍스처를 가져올 수 없습니다. (URL: {imageUrl})");
                    targetImage.sprite = null;
                }
            }
        }
    }

    // UI 요소에 상품 데이터를 채워 넣는 함수
    private void PopulatePopupUI(ProductData product)
    {
        // 통화 정보 표시 (이제 ProductData에서 직접 접근)
        if (product.Currencies != null)
        {
            if (currencyIconImage != null)
            {
                StartCoroutine(LoadImage(product.Currencies.icon_url, currencyIconImage, "통화 아이콘"));
            }
        }
        else
        {
            Debug.LogWarning($"상품 ID {product.id}에 대한 통화 정보가 없습니다.");
            if (currencyIconImage != null) currencyIconImage.sprite = null;
        }

        // 텍스트 정보 채우기
        if (productNameText != null) productNameText.text = product.name;
        if (productDescriptionText != null) productDescriptionText.text = product.description;

        quantitySelector.itemPrice = product.price;


        // 판매 기간 표시 (날짜 파싱 및 포맷팅)
        /* if (productSaleDatesText != null)
        {
            string startDate = string.IsNullOrEmpty(product.sale_start_date) ? "N/A" : product.sale_start_date;
            string endDate = string.IsNullOrEmpty(product.sale_end_date) ? "N/A" : product.sale_end_date;
            productSaleDatesText.text = $"판매 기간: {startDate} ~ {endDate}";
        } */
    }

    // 구매 버튼 클릭 시 호출될 함수 (추후 구현)
    // private void OnPurchaseButtonClicked()
    // {
    //     if (currentDisplayedProduct != null)
    //     {
    //         Debug.Log($"구매 버튼 클릭됨! 상품 ID: {currentDisplayedProduct.id}, 이름: {currentDisplayedProduct.name}");
    //         // TODO: 실제 구매 로직 (서버에 구매 요청 보내기, 재화 차감 등) 구현
    //     }
    // }
}
