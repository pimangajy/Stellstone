using System;
using System.Collections;
using System.Collections.Generic;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 상점 카테고리 / UI 탭 대분류
/// </summary>
public enum ShopCategory
{
    LeaderSkin = 0, // 리더 스킨
    Emote = 1,      // 이모티콘
    CardBack = 2,   // 카드 뒷면
    MapDecor = 3,   // 맵 꾸미기
    PrismCard = 4,  // 프리즘 카드
    Profile = 5,    // 프로필
    SeasonPass = 6, // 시즌 패스
    Package = 7,    // 패키지
    CardPack = 8,   // 카드 팩
    Cards = 9,      // 카드 낱장
}

/// <summary>
/// 결제 재화 종류 Enum
/// </summary>
public enum PriceCurrency
{
    Gold = 0,        // 골드 (일반 인게임 재화)
    Stellastone = 1, // 성석 (유료 결제 재화)
    Stardust = 2     // 별가루 (카드 제작/분해 재화)
}

/// <summary>
/// 개별 상품 데이터 모델
/// </summary>
[System.Serializable]
public class ProductData
{
    public ShopCategory category_Id;                      // 카테고리 아이디 (Enum)
    public string productId;                               // 아이템 고유 아이디 (문자열)
    public string productName;                             // 아이템 이름
    public string description;                             // 아이템 설명
    public string image_url;                               // 아이템 이미지 위치
    public int price;                                      // 아이템 가격
    public PriceCurrency currency;                         // 결제 재화 종류
    public bool isActive;                                  // 판매 활성화 여부
    public string sale_Start_Date;                         // 할인 시작일
    public string sale_End_Date;                           // 할인 종료기간
}

/// <summary>
/// 상점 상품 목록 조회 응답 래퍼
/// </summary>
[System.Serializable]
public class ProductsApiResponse
{
    public string status;
    public string message;
    public List<ProductData> data; // 실제 반환된 ProductData 목록
}

/// <summary>
/// 상점 상품 구매 요청 패킷 (클라이언트 -> 서버)
/// </summary>
[System.Serializable]
public class PurchaseRequest
{
    public string productId; // 구매할 상품 고유 ID (문자열)
    public int quantity;     // 구매 수량
}

/// <summary>
/// 상점 상품 구매 응답 패킷 (서버 -> 클라이언트)
/// </summary>
[System.Serializable]
public class PurchaseResponse
{
    public string status;                  // "success" 또는 "error"
    public string message;                 // 결과 안내 메시지
    public string productId;               // 구매한 상품 ID (문자열)
    public int quantity;                   // 구매한 수량
    public int remainingGold;              // 구매 후 남은 골드
    public int remainingStellastone;       // 구매 후 남은 성석
    public int remainingStardust;          // 구매 후 남은 별가루
    public List<string> obtainedCardIds;   // (카드팩 구매 시) 뽑힌 카드 ID 목록
    public string obtainedItemId;          // (스킨/이모티콘 구매 시) 획득한 아이템 ID
}

/// <summary>
/// 상점 전체를 총괄하는 메인 싱글톤 매니저
/// (상품 목록 로드, 슬롯 생성, 상세 팝업 바인딩, 서버 구매 처리)
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    // API URL (GameClient.Instance가 null일 경우 기본 서버 주소로 fallback)
    public string productsApiBaseUrl => (GameClient.Instance != null)
        ? $"{GameClient.Instance.BaseApiUrl}/shop/products"
        : "http://175.125.250.226:5123/api/shop/products";

    public string purchaseApiUrl => (GameClient.Instance != null)
        ? $"{GameClient.Instance.BaseApiUrl}/shop/purchase"
        : "http://175.125.250.226:5123/api/shop/purchase";

    [Header("1. 기본 카테고리 설정")]
    public ShopCategory defaultCategory = ShopCategory.CardPack;

    [Header("2. 상점 슬롯 생성 위치 및 프리팹")]
    public Transform shopContentParent; // Scroll View의 Content Transform
    public GameObject shopSlotPrefab;   // 상품 슬롯 프리팹 (ShopSlotUI 부착)

    [Header("3. 상품 상세 팝업 UI 연결")]
    public UIPanelToggler uIPanelToggler;           // 팝업 열기/닫기 토글러
    public QuantitySelector quantitySelector;       // 구매 수량 조절기
    public GameObject popupPanel;                   // 팝업 패널 GameObject
    public TextMeshProUGUI productNameText;         // 팝업 내 상품명 텍스트
    public TextMeshProUGUI productDescriptionText;   // 팝업 내 상품 설명 텍스트
    public Image popupProductImage;                 // 팝업 내 상품 이미지 Image
    public Image currencyIconImage;                 // 팝업 내 결제 재화 아이콘 Image

    [Header("4. 재화 스프라이트 (로컬 등록)")]
    public Sprite goldIconSprite;        // 골드 아이콘 스프라이트
    public Sprite stellastoneIconSprite; // 성석 아이콘 스프라이트
    public Sprite stardustIconSprite;    // 별가루 아이콘 스프라이트

    [Header("5. 팝업 버튼")]
    public Button closeButton;    // 팝업 닫기 버튼
    public Button purchaseButton; // 구매하기 버튼

    // 구매 완료 시 알림 이벤트 (UI 재화 갱신 및 팩 개봉 연출용)
    public event Action<PurchaseResponse> OnPurchaseCompleted;

    // 현재 활성화되어 표시 중인 카테고리 (중복 로드 방지용)
    private ShopCategory currentActiveCategory = (ShopCategory)(-1);
    // 현재 팝업에 선택된 상품 데이터
    private ProductData currentSelectedProduct;

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 팝업창 초기 비활성화
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        // 팝업 버튼 이벤트 리스너 연결
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseProductPopup);
        }

        if (purchaseButton != null)
        {
            purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
        }
    }

    private void Start()
    {
        // 상점 진입 시 기본 카테고리 상품 자동 로드
        LoadCategory(defaultCategory);
    }

    // ==================================================================
    // 1. 카테고리 로드 및 슬롯 생성
    // ==================================================================

    /// <summary>
    /// 카테고리를 변경하고 해당 상품 목록을 서버에서 불러오는 핵심 함수
    /// </summary>
    public void LoadCategory(ShopCategory category)
    {
        Debug.Log($"[Shop] 카테고리 로드 요청: {category} ({(int)category})");

        if (currentActiveCategory == category)
        {
            Debug.Log($"[Shop] 카테고리 {category}는 이미 표시 중입니다.");
            return;
        }

        ClearShopSlots();
        currentActiveCategory = category;
        StartCoroutine(GetFilteredProductsFromServer(category));
    }

    /// <summary>
    /// C# 코드에서 ShopCategory Enum으로 카테고리 버튼 클릭 시 호출
    /// </summary>
    public void OnCategoryButtonClicked(ShopCategory category)
    {
        LoadCategory(category);
    }

    /// <summary>
    /// 유니티 인스펙터 버튼 OnClick(int) 이벤트에서 호출할 수 있는 오버로드 함수
    /// </summary>
    public void OnCategoryButtonClicked(int categoryId)
    {
        LoadCategory((ShopCategory)categoryId);
    }

    /// <summary>
    /// (이전 호환용) 상점 버튼 클릭 시 호출
    /// </summary>
    public void OnShopButtonClicked(int categoryId)
    {
        LoadCategory((ShopCategory)categoryId);
    }

    /// <summary>
    /// 화면에 표시된 모든 상품 슬롯 제거
    /// </summary>
    private void ClearShopSlots()
    {
        if (shopContentParent == null)
        {
            Debug.LogError("[Shop] Shop Content Parent가 설정되지 않았습니다.");
            return;
        }

        foreach (Transform child in shopContentParent)
        {
            Destroy(child.gameObject);
        }
        Debug.Log("[Shop] 이전 상점 슬롯이 모두 제거되었습니다.");
    }

    /// <summary>
    /// 서버로부터 특정 카테고리의 상품 데이터를 가져오는 코루틴
    /// </summary>
    private IEnumerator GetFilteredProductsFromServer(ShopCategory category)
    {
        string requestUrl = $"{productsApiBaseUrl}?category_id={(int)category}";
        Debug.Log($"[Shop] 상품 요청 URL: {requestUrl}");

        using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
        {
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    Debug.LogError($"[Shop] 네트워크 연결 오류: {webRequest.error}");
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError($"[Shop] HTTP 프로토콜 오류: {webRequest.responseCode} - {webRequest.error}");
                    Debug.LogError($"[Shop] 응답 본문: {webRequest.downloadHandler.text}");
                    break;
                case UnityWebRequest.Result.Success:
                    string jsonResponse = webRequest.downloadHandler.text;
                    Debug.Log($"[Shop] 서버 상품 데이터 수신: {jsonResponse}");

                    try
                    {
                        ProductsApiResponse apiResponse = JsonUtility.FromJson<ProductsApiResponse>(jsonResponse);

                        if (apiResponse != null && apiResponse.status == "success" && apiResponse.data != null)
                        {
                            List<ProductData> filteredProducts = apiResponse.data;
                            Debug.Log($"[Shop] 카테고리 [{category}] 상품 개수: {filteredProducts.Count}개");
                            CreateShopSlots(filteredProducts);
                        }
                        else
                        {
                            Debug.LogError($"[Shop] 상품 데이터를 가져오지 못했습니다: {apiResponse?.message}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Shop] JSON 파싱 오류: {e.Message} (응답: {jsonResponse})");
                    }
                    break;
                default:
                    Debug.LogError($"[Shop] 알 수 없는 오류: {webRequest.result} - {webRequest.error}");
                    break;
            }
        }
    }

    /// <summary>
    /// 상품 슬롯 UI 프리팹을 인스턴스화하고 데이터를 채우는 함수
    /// </summary>
    private void CreateShopSlots(List<ProductData> products)
    {
        if (shopContentParent == null || shopSlotPrefab == null)
        {
            Debug.LogError("[Shop] Shop Content Parent 또는 Shop Slot Prefab이 설정되지 않았습니다.");
            return;
        }

        Debug.Log($"[Shop] 총 {products.Count}개의 상점 슬롯을 생성합니다.");
        foreach (ProductData product in products)
        {
            GameObject newSlot = Instantiate(shopSlotPrefab, shopContentParent);
            newSlot.name = $"ShopSlot_{product.productId}";

            ShopSlotUI slotUI = newSlot.GetComponent<ShopSlotUI>();
            if (slotUI != null)
            {
                slotUI.SetProductData(product);
                Debug.Log($"[Shop] 슬롯 생성 완료: {product.productName} (ID: {product.productId}, 가격: {product.price})");
            }
            else
            {
                Debug.LogWarning($"[Shop] ShopSlotPrefab에 ShopSlotUI 컴포넌트가 없습니다: {newSlot.name}");
            }
        }
    }

    // ==================================================================
    // 2. 상품 상세 및 구매 팝업 UI 제어
    // ==================================================================

    /// <summary>
    /// 상품 슬롯 클릭 시 팝업을 열고 상세 데이터를 표시하는 함수
    /// </summary>
    public void OpenProductPopup(ProductData product)
    {
        currentSelectedProduct = product;

        PopulatePopupUI(product);

        if (quantitySelector != null)
        {
            quantitySelector.itemPrice = product.price;
            quantitySelector.UpdateQuantity(1);
        }

        if (uIPanelToggler != null)
        {
            uIPanelToggler.ShowPanel();
        }
        else if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 팝업 닫기 함수
    /// </summary>
    public void CloseProductPopup()
    {
        if (uIPanelToggler != null)
        {
            uIPanelToggler.HidePanel();
        }
        else if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 재화 종류에 따라 로컬 스프라이트를 아이콘 Image에 설정
    /// </summary>
    private void SetCurrencyIcon(PriceCurrency currencyType)
    {
        if (currencyIconImage == null) return;

        switch (currencyType)
        {
            case PriceCurrency.Gold:
                currencyIconImage.sprite = goldIconSprite;
                break;
            case PriceCurrency.Stellastone:
                currencyIconImage.sprite = stellastoneIconSprite;
                break;
            case PriceCurrency.Stardust:
                currencyIconImage.sprite = stardustIconSprite;
                break;
            default:
                currencyIconImage.sprite = null;
                break;
        }
    }

    /// <summary>
    /// 팝업 UI 요소에 상품 데이터 채우기
    /// </summary>
    private void PopulatePopupUI(ProductData product)
    {
        SetCurrencyIcon(product.currency);

        if (productNameText != null) productNameText.text = product.productName;
        if (productDescriptionText != null) productDescriptionText.text = product.description;

        if (popupProductImage != null && !string.IsNullOrEmpty(product.image_url))
        {
            LoadProductImage(product.image_url, popupProductImage);
        }
    }

    /// <summary>
    /// 로컬 에셋 경로(Assets/Sprite/Shop/...)에서 이미지를 로드하여 Image에 표시
    /// </summary>
    public static void LoadProductImage(string imagePath, Image targetImage)
    {
        if (targetImage == null || string.IsNullOrWhiteSpace(imagePath)) return;

        string cleanPath = imagePath.TrimStart('/', '\\');
        string fullPath = System.IO.Path.Combine(Application.dataPath, "Sprite", "Shop", cleanPath);

        if (!System.IO.File.Exists(fullPath))
        {
            fullPath = System.IO.Path.Combine(Application.dataPath, "Sprite", "Shop", "Items", cleanPath);
        }

        if (System.IO.File.Exists(fullPath))
        {
            try
            {
                byte[] fileData = System.IO.File.ReadAllBytes(fullPath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    Rect rect = new Rect(0, 0, texture.width, texture.height);
                    Vector2 pivot = new Vector2(0.5f, 0.5f);
                    targetImage.sprite = Sprite.Create(texture, rect, pivot);
                    targetImage.preserveAspect = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Shop] 로컬 이미지 로드 중 오류 ({fullPath}): {ex.Message}");
            }
        }

        // Resources 폴더 로드 시도
        string resPath = cleanPath;
        int dotIndex = resPath.LastIndexOf('.');
        if (dotIndex > 0) resPath = resPath.Substring(0, dotIndex);

        Sprite resSprite = Resources.Load<Sprite>(resPath);
        if (resSprite != null)
        {
            targetImage.sprite = resSprite;
            targetImage.preserveAspect = true;
        }
    }

    // ==================================================================
    // 3. 서버 상품 구매 통신 로직
    // ==================================================================

    /// <summary>
    /// 팝업 내 구매 버튼 클릭 시 호출
    /// </summary>
    private void OnPurchaseButtonClicked()
    {
        if (currentSelectedProduct == null)
        {
            Debug.LogWarning("[Shop] 선택된 상품이 없습니다.");
            return;
        }

        Debug.Log("구매 클릭");

        int quantity = (quantitySelector != null) ? quantitySelector.CurrentQuantity : 1;
        StartCoroutine(SendPurchaseRequest(currentSelectedProduct.productId, quantity));
    }

    /// <summary>
    /// 서버에 구매 요청 패킷을 전송하는 코루틴
    /// </summary>
    private IEnumerator SendPurchaseRequest(string productId, int quantity)
    {
        FirebaseUser currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (currentUser == null)
        {
            Debug.LogError("[Shop] 로그인된 사용자가 없습니다. 구매를 진행할 수 없습니다.");
            yield break;
        }

        var tokenTask = currentUser.TokenAsync(false);
        yield return new WaitUntil(() => tokenTask.IsCompleted);

        if (tokenTask.IsFaulted || tokenTask.IsCanceled)
        {
            Debug.LogError("[Shop] Firebase 인증 토큰을 가져오지 못했습니다.");
            yield break;
        }

        string idToken = tokenTask.Result;

        // 1. PurchaseRequest 패킷 생성 및 직렬화
        PurchaseRequest requestPacket = new PurchaseRequest
        {
            productId = productId,
            quantity = quantity
        };

        string jsonRequestBody = JsonUtility.ToJson(requestPacket);
        Debug.Log($"[Shop] 구매 요청 전송: {purchaseApiUrl} | 상품ID: {productId}, 수량: {quantity}");

        // 2. 서버 POST 전송
        using (UnityWebRequest webRequest = new UnityWebRequest(purchaseApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequestBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", "Bearer " + idToken);

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = webRequest.downloadHandler.text;
                Debug.Log($"[Shop] 구매 응답 수신: {jsonResponse}");

                PurchaseResponse response = JsonUtility.FromJson<PurchaseResponse>(jsonResponse);
                if (response != null && response.status == "success")
                {
                    Debug.Log($"[Shop] 구매 성공! {response.message} (남은 골드: {response.remainingGold}, 남은 성석: {response.remainingStellastone})");
                    OnPurchaseCompleted?.Invoke(response);

                    // 팝업 닫기
                    CloseProductPopup();
                }
                else
                {
                    Debug.LogWarning($"[Shop] 구매 실패: {response?.message}");
                }
            }
            else
            {
                Debug.LogError($"[Shop] 구매 요청 네트워크 오류: {webRequest.responseCode} - {webRequest.error} | {webRequest.downloadHandler.text}");
            }
        }
    }
}
