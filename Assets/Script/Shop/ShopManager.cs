using Firebase.Auth;
using Firebase.Firestore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [JsonConverter(typeof(StringEnumConverter))]
    public ShopCategory category_Id;                      // 카테고리 아이디 (Enum)
    public string productId;                               // 아이템 고유 아이디 (문자열)
    public string productName;                             // 아이템 이름
    public string description;                             // 아이템 설명
    public string image_url;                               // 아이템 이미지 위치
    public int price;                                      // 아이템 가격
    [JsonConverter(typeof(StringEnumConverter))]
    public PriceCurrency currency;                         // 결제 재화 종류
    public bool isActive;                                  // 판매 활성화 여부
    public string sale_Start_Date;                         // 할인 시작일
    public string sale_End_Date;                           // 할인 종료기간
    public Expansion? targetExpansion;
    public List<string> targetClasses;
    public List<string> fixedCardIds;
    public List<string> randomPickPoolIds;
    public int randomPickCount = 1;
    public List<string> customCardPoolIds;
    public int cardsPerPack = 5;
    public int guaranteedLegendaryCount = 0;
    public int guaranteedEpicCount = 0;
    public int guaranteedRareCount = 0;
    public int purchaseLimit = 0;                          // 계정당 구매 제한 횟수 (0: 무제한)
    public int myPurchaseCount = 0;                         // 내가 구매한 누적 횟수
    public int remainingPurchaseLimit = -1;                 // 남은 구매 가능 횟수 (-1: 무제한, 0: 품절)
    public bool isPurchasable = true;                       // 구매 가능 여부
    public bool isVisibleInShop = true;                     // 상점 진열 노출 여부 (false: 기본스킨, 비매품, 비공개 상품)

    /// <summary>
    /// 역직렬화 실패 또는 오설정 시 productId 접두사를 통해 올바른 ShopCategory 복구
    /// </summary>
    public ShopCategory GetActualCategory()
    {
        if (category_Id != ShopCategory.LeaderSkin) return category_Id;

        if (string.IsNullOrEmpty(productId)) return category_Id;

        if (productId.StartsWith("CardPack", StringComparison.OrdinalIgnoreCase) ||
            productId.StartsWith("Pack_", StringComparison.OrdinalIgnoreCase) ||
            productId.EndsWith("Pack", StringComparison.OrdinalIgnoreCase) ||
            productId.Contains("CardPack", StringComparison.OrdinalIgnoreCase))
        {
            return ShopCategory.CardPack;
        }
        if (productId.StartsWith("Prism_", StringComparison.OrdinalIgnoreCase))
        {
            return ShopCategory.PrismCard;
        }
        if (productId.StartsWith("Package_", StringComparison.OrdinalIgnoreCase))
        {
            return ShopCategory.Package;
        }
        if (productId.StartsWith("Emote_", StringComparison.OrdinalIgnoreCase))
        {
            return ShopCategory.Emote;
        }
        if (productId.StartsWith("CardBack_", StringComparison.OrdinalIgnoreCase))
        {
            return ShopCategory.CardBack;
        }
        return category_Id;
    }
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
    public int remainingPacks;             // 구매 후 남은/보유 카드팩 수
    public List<string> obtainedCardIds;   // 뽑힌 카드 ID 목록
    public string obtainedItemId;          // (스킨/이모티콘/팩 구매 시) 획득한 아이템 ID
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
    public TextMeshProUGUI purchaseLimitText;        // 팝업 내 남은 구매 횟수 표시 텍스트
    public Image popupProductImage;                 // 팝업 내 상품 이미지 Image
    public Image currencyIconImage;                 // 팝업 내 결제 재화 아이콘 Image

    [Header("4. 재화 스프라이트 (로컬 등록)")]
    public Sprite goldIconSprite;        // 골드 아이콘 스프라이트
    public Sprite stellastoneIconSprite; // 성석 아이콘 스프라이트
    public Sprite stardustIconSprite;    // 별가루 아이콘 스프라이트

    [Header("5. 팝업 버튼")]
    public Button closeButton;    // 팝업 닫기 버튼
    public Button purchaseButton; // 구매하기 버튼
    public TextMeshProUGUI purchaseButtonText; // 구매하기 버튼 텍스트

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
    public void LoadCategory(ShopCategory category, bool forceReload = false)
    {
        Debug.Log($"[Shop] 카테고리 로드 요청: {category} ({(int)category}) (강제새로고침: {forceReload})");

        if (!forceReload && currentActiveCategory == category)
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
    }

    /// <summary>
    /// 서버로부터 특정 카테고리의 상품 데이터를 가져오는 코루틴 (유저 인증 토큰 포함)
    /// </summary>
    private IEnumerator GetFilteredProductsFromServer(ShopCategory category)
    {
        string requestUrl = $"{productsApiBaseUrl}?category_id={(int)category}";

        // 1. Firebase 인증 토큰 가져오기 (로그인된 경우 서버가 유저별 구매/보유 현황 계산)
        FirebaseUser currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
        string idToken = null;
        if (currentUser != null)
        {
            var tokenTask = currentUser.TokenAsync(false);
            yield return new WaitUntil(() => tokenTask.IsCompleted);
            if (!tokenTask.IsFaulted && !tokenTask.IsCanceled)
            {
                idToken = tokenTask.Result;
            }
        }

        using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
        {
            if (!string.IsNullOrEmpty(idToken))
            {
                webRequest.SetRequestHeader("Authorization", "Bearer " + idToken);
            }

            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError($"[Shop] 서버 통신 오류 ({webRequest.result}): {webRequest.error}");
                    if (category == ShopCategory.LeaderSkin)
                    {
                        Debug.Log("[Shop] 🛍️ 오프라인/로컬 SkinData 에셋 기반으로 리더 스킨 상점 슬롯을 생성합니다.");
                        CreateLeaderSkinFallbackSlots();
                    }
                    break;
                case UnityWebRequest.Result.Success:
                    string jsonResponse = webRequest.downloadHandler.text;

                    try
                    {
                        ProductsApiResponse apiResponse = null;
                        try
                        {
                            apiResponse = JsonConvert.DeserializeObject<ProductsApiResponse>(jsonResponse);
                        }
                        catch (Exception jsonEx)
                        {
                            Debug.LogWarning($"[Shop] Newtonsoft.Json 파싱 실패, JsonUtility 시도: {jsonEx.Message}");
                            apiResponse = JsonUtility.FromJson<ProductsApiResponse>(jsonResponse);
                        }

                        if (apiResponse != null && apiResponse.status == "success" && apiResponse.data != null)
                        {
                            List<ProductData> filteredProducts = apiResponse.data;
                            foreach (var prod in filteredProducts)
                            {
                                if (prod != null)
                                {
                                    prod.category_Id = prod.GetActualCategory();
                                }
                            }

                            CreateShopSlots(filteredProducts);

                            if (category == ShopCategory.LeaderSkin && (filteredProducts == null || filteredProducts.Count == 0 || !filteredProducts.Any(p => p.isVisibleInShop)))
                            {
                                CreateLeaderSkinFallbackSlots();
                            }
                        }
                        else
                        {
                            Debug.LogError($"[Shop] 상품 데이터를 가져오지 못했습니다: {apiResponse?.message}");
                            if (category == ShopCategory.LeaderSkin) CreateLeaderSkinFallbackSlots();
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Shop] JSON 파싱 오류: {e.Message} (응답: {jsonResponse})");
                        if (category == ShopCategory.LeaderSkin) CreateLeaderSkinFallbackSlots();
                    }
                    break;
                default:
                    Debug.LogError($"[Shop] 알 수 없는 오류: {webRequest.result} - {webRequest.error}");
                    if (category == ShopCategory.LeaderSkin) CreateLeaderSkinFallbackSlots();
                    break;
            }
        }
    }

    /// <summary>
    /// 서버 연결이 원활하지 않거나 스킨 상품 목록이 비어있을 때 Resources/Skins 에셋들을 기반으로 상점 슬롯을 자동 생성합니다.
    /// </summary>
    private void CreateLeaderSkinFallbackSlots()
    {
        var masterSkinAssets = Resources.LoadAll<SkinData>("Skins");
        if (masterSkinAssets == null || masterSkinAssets.Length == 0) return;

        var products = new List<ProductData>();
        foreach (var mSkin in masterSkinAssets)
        {
            if (mSkin == null) continue;
            // 기본 스킨(0001) 제외, 판매용 스킨만 상점에 노출
            bool isFreeDefault = mSkin.skinId.EndsWith("0001") || mSkin.skinId.EndsWith("_Default");
            if (!isFreeDefault)
            {
                products.Add(new ProductData
                {
                    category_Id = ShopCategory.LeaderSkin,
                    productId = mSkin.skinId,
                    productName = mSkin.skinName,
                    description = mSkin.description,
                    image_url = $"Items/Skins/{mSkin.skinId}.png",
                    currency = PriceCurrency.Stellastone,
                    price = 500,
                    isActive = true,
                    isVisibleInShop = true,
                    targetClasses = new List<string> { mSkin.targetClass.ToString() }
                });
            }
        }

        if (products.Count > 0)
        {
            CreateShopSlots(products);
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

        foreach (ProductData product in products)
        {
            if (product == null) continue;

            // 비매품(상점 미노출, 비활성화, 무료/가격 미설정 상품)만 제외
            if (!product.isVisibleInShop || !product.isActive || product.price <= 0)
            {
                continue;
            }

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
    /// 상품의 판매 기간이 만료되었는지 확인합니다.
    /// </summary>
    public static bool IsProductExpired(ProductData product)
    {
        if (product == null) return false;
        if (!string.IsNullOrEmpty(product.sale_End_Date))
        {
            if (DateTime.TryParse(product.sale_End_Date, out DateTime endDate))
            {
                return DateTime.Now > endDate;
            }
        }
        return false;
    }

    /// <summary>
    /// 상품 슬롯 클릭 시 팝업을 열고 상세 데이터를 표시하는 함수
    /// </summary>
    public void OpenProductPopup(ProductData product)
    {
        currentSelectedProduct = product;

        PopulatePopupUI(product);

        // 0. 스킨 상품 오류 확인
        bool isProductError = false;
        if (product.category_Id == ShopCategory.LeaderSkin && !product.productId.StartsWith("CardPack", StringComparison.OrdinalIgnoreCase))
        {
            string targetClass = product.targetClasses != null && product.targetClasses.Count > 0 ? product.targetClasses[0] : null;
            if (LeaderCardDisplay.LoadSkinData(product.productId, targetClass, fallbackToDefault: false) == null)
            {
                isProductError = true;
            }
        }

        // 1. 기간 종료 여부 확인
        bool isExpired = IsProductExpired(product);
        // 2. 이미 구매 완료(잔여 수량 0 / 구매 불가) 여부 확인
        bool isAlreadyPurchased = (product.remainingPurchaseLimit == 0) || (!product.isPurchasable && !isExpired);

        if (isProductError)
        {
            SetPurchaseButtonState("상품오류", false);
            if (quantitySelector != null) quantitySelector.SetLimits(0, product.price);
        }
        else if (isExpired)
        {
            SetPurchaseButtonState("기간종료", false);
            if (quantitySelector != null) quantitySelector.SetLimits(0, product.price);
        }
        else if (isAlreadyPurchased)
        {
            SetPurchaseButtonState("이미구매한 상품", false);
            if (quantitySelector != null) quantitySelector.SetLimits(0, product.price);
        }
        else
        {
            SetPurchaseButtonState("구매하기", true);
            if (quantitySelector != null)
            {
                // remainingPurchaseLimit가 0 이상으로 유효하게 전달된 경우 우선 사용
                int limit;
                if (product.remainingPurchaseLimit >= 0)
                {
                    limit = product.remainingPurchaseLimit;
                }
                else if (product.purchaseLimit > 0)
                {
                    limit = Mathf.Max(0, product.purchaseLimit - product.myPurchaseCount);
                }
                else
                {
                    limit = -1; // 무제한
                }
                quantitySelector.SetLimits(limit, product.price);
            }
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
    /// 구매 버튼의 텍스트와 활성화 여부를 설정합니다.
    /// </summary>
    private void SetPurchaseButtonState(string buttonText, bool isInteractable)
    {
        if (purchaseButton != null)
        {
            purchaseButton.interactable = isInteractable;

            TextMeshProUGUI btnText = purchaseButtonText != null
                ? purchaseButtonText
                : purchaseButton.GetComponentInChildren<TextMeshProUGUI>();

            if (btnText != null)
            {
                btnText.text = buttonText;
            }
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
    /// 재화 종류에 따라 등록된 스프라이트를 반환합니다.
    /// </summary>
    public Sprite GetCurrencySprite(PriceCurrency currencyType)
    {
        switch (currencyType)
        {
            case PriceCurrency.Gold:
                return goldIconSprite;
            case PriceCurrency.Stellastone:
                return stellastoneIconSprite;
            case PriceCurrency.Stardust:
                return stardustIconSprite;
            default:
                return null;
        }
    }

    /// <summary>
    /// 재화 종류에 따라 로컬 스프라이트를 아이콘 Image에 설정
    /// </summary>
    private void SetCurrencyIcon(PriceCurrency currencyType)
    {
        Sprite targetSprite = GetCurrencySprite(currencyType);

        if (currencyIconImage != null)
        {
            currencyIconImage.sprite = targetSprite;
            currencyIconImage.preserveAspect = true;
        }

        if (quantitySelector != null && quantitySelector.priceImage != null)
        {
            quantitySelector.priceImage.sprite = targetSprite;
            quantitySelector.priceImage.preserveAspect = true;
        }
    }

    /// <summary>
    /// 팝업 UI 요소에 상품 데이터 채우기 (스킨 상품일 경우 SkinData 에셋과 4개 감정표현 안내를 함께 표시)
    /// </summary>
    private void PopulatePopupUI(ProductData product)
    {
        SetCurrencyIcon(product.currency);

        SkinData masterSkin = null;
        bool isProductError = false;

        if (product.category_Id == ShopCategory.LeaderSkin && !product.productId.StartsWith("CardPack", StringComparison.OrdinalIgnoreCase))
        {
            string targetClass = product.targetClasses != null && product.targetClasses.Count > 0 ? product.targetClasses[0] : null;
            masterSkin = LeaderCardDisplay.LoadSkinData(product.productId, targetClass, fallbackToDefault: false);
            if (masterSkin == null)
            {
                isProductError = true;
            }
        }

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

        if (productDescriptionText != null)
        {
            if (isProductError)
            {
                productDescriptionText.text = "<color=#FF4444><b>[상품오류]</b> 상품 데이터를 정상적으로 불러올 수 없습니다.\n(오타 또는 리소스 에셋 누락)</color>";
            }
            else
            {
                string desc = masterSkin != null ? masterSkin.description : product.description;
                if (masterSkin != null)
                {
                    var emotes = masterSkin.GetValidEmotes();
                    if (emotes != null && emotes.Count > 0)
                    {
                        desc += "\n\n<color=#FFD700><b>[기본 포함 감정표현 4종]</b></color>";
                        for (int i = 0; i < emotes.Count; i++)
                        {
                            var e = emotes[i];
                            desc += $"\n• {e.buttonLabel}: \"{e.speechMessage}\"";
                        }
                    }
                }
                productDescriptionText.text = desc;
            }
        }

        // 남은 구매 횟수 UI 텍스트 갱신
        if (purchaseLimitText != null)
        {
            if (isProductError)
            {
                purchaseLimitText.text = "남은 구매 횟수: <color=#FF5555>구매 불가</color>";
            }
            else if (product.purchaseLimit <= 0)
            {
                purchaseLimitText.text = "남은 구매 횟수: <color=#00E5FF>무제한</color>";
            }
            else if (product.remainingPurchaseLimit == 0 || (!product.isPurchasable && !IsProductExpired(product)))
            {
                purchaseLimitText.text = $"남은 구매 횟수: <color=#FF5555>0</color> / {product.purchaseLimit}회 (품절)";
            }
            else
            {
                int remaining = product.remainingPurchaseLimit > 0 ? product.remainingPurchaseLimit : Mathf.Max(0, product.purchaseLimit - product.myPurchaseCount);
                purchaseLimitText.text = $"남은 구매 횟수: <color=#00E5FF>{remaining}</color> / {product.purchaseLimit}회";
            }
        }

        // 팝업 상품 이미지 로드
        if (popupProductImage != null)
        {
            popupProductImage.sprite = null;

            if (isProductError)
            {
                popupProductImage.gameObject.SetActive(false);
            }
            else if (masterSkin != null)
            {
                Sprite skinSp = masterSkin.skinSprite != null ? masterSkin.skinSprite : masterSkin.GetIconOrMainSprite();
                if (skinSp != null)
                {
                    popupProductImage.sprite = skinSp;
                    popupProductImage.preserveAspect = true;
                    popupProductImage.gameObject.SetActive(true);
                }
                else if (!string.IsNullOrEmpty(product.image_url))
                {
                    LoadProductImage(product.image_url, popupProductImage);
                    popupProductImage.gameObject.SetActive(true);
                }
            }
            else if (!string.IsNullOrEmpty(product.image_url))
            {
                LoadProductImage(product.image_url, popupProductImage);
                popupProductImage.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Resources 또는 로컬 에셋 경로에서 이미지를 로드하여 Image에 표시합니다.
    /// (예: "Items/Skins/Yuni_Skin_0001.png", "Items/Skins/Yuni/Yuni_Skin_0001.png", "Items/Emote/V-V.png")
    /// </summary>
    public static void LoadProductImage(string imagePath, Image targetImage)
    {
        if (targetImage == null || string.IsNullOrWhiteSpace(imagePath)) return;

        // 1. Resources 로드 경로 정리 (확장자 제거 및 슬래시 표준화)
        string cleanPath = imagePath.Replace('\\', '/').TrimStart('/');
        string resPath = cleanPath;
        int dotIndex = resPath.LastIndexOf('.');
        if (dotIndex > 0) resPath = resPath.Substring(0, dotIndex);

        // 1-1. 기본 Resources 경로 시도 (예: "Items/Skins/Yuni_Skin_0001", "Items/Emote/V-V")
        Sprite resSprite = Resources.Load<Sprite>(resPath);

        // 1-2. 만약 못 찾았고 "Items/" 접두사가 누락된 경우 시도
        if (resSprite == null && !resPath.StartsWith("Items/", StringComparison.OrdinalIgnoreCase))
        {
            resSprite = Resources.Load<Sprite>("Items/" + resPath);
        }

        // 1-3. 캐릭터 하위 폴더 검색 시도 (예: "Items/Skins/Yuni/Yuni_Skin_0001" 또는 "Items/Skins/Yuni_Skin_0001")
        if (resSprite == null && resPath.Contains("Skin"))
        {
            string fileName = System.IO.Path.GetFileName(resPath);
            if (fileName.StartsWith("Yuni", StringComparison.OrdinalIgnoreCase))
            {
                resSprite = Resources.Load<Sprite>($"Items/Skins/Yuni/{fileName}") ?? Resources.Load<Sprite>($"Items/Skins/{fileName}");
            }
            else if (fileName.StartsWith("Gangzi", StringComparison.OrdinalIgnoreCase))
            {
                resSprite = Resources.Load<Sprite>($"Items/Skins/Gangzi/{fileName}") ?? Resources.Load<Sprite>($"Items/Skins/{fileName}");
            }
        }

        // 1-4. 카드팩 하위 폴더 및 파일명 보정 검색 시도
        if (resSprite == null && (resPath.IndexOf("CardPack", StringComparison.OrdinalIgnoreCase) >= 0 || resPath.IndexOf("Pack", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            string fileName = System.IO.Path.GetFileName(resPath);
            if (fileName.Equals("NormalPack", StringComparison.OrdinalIgnoreCase))
            {
                resSprite = Resources.Load<Sprite>("Items/CardPack/CardPack_nomal");
            }
            if (resSprite == null)
            {
                resSprite = Resources.Load<Sprite>($"Items/CardPack/{fileName}") ?? Resources.Load<Sprite>(fileName);
            }
        }

        // 1-4. Texture2D로 등록되어 있는 경우 Sprite로 변환 생성
        if (resSprite == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(resPath);
            if (tex != null)
            {
                resSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }

        if (resSprite != null)
        {
            targetImage.sprite = resSprite;
            targetImage.preserveAspect = true;
            return;
        }

        // 2. Resources에 없는 경우 로컬 파일 직접 로드 시도
        string fullPath = System.IO.Path.Combine(Application.dataPath, "Sprite", "Shop", cleanPath);
        if (!System.IO.File.Exists(fullPath))
        {
            fullPath = System.IO.Path.Combine(Application.dataPath, "Sprite", "Shop", "Items", cleanPath);
        }
        if (!System.IO.File.Exists(fullPath))
        {
            fullPath = System.IO.Path.Combine(Application.dataPath, "Resources", cleanPath);
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
                Debug.LogError($"[Shop] 이미지 로컬 파일 로드 중 오류 ({fullPath}): {ex.Message}");
            }
        }

        Debug.LogWarning($"[Shop] 상품 이미지를 찾을 수 없습니다: {imagePath} (시도 경로: Resources/{resPath})");
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
                    
                    // GameClient의 로컬 계정 데이터(재화 및 보유 스킨/아이템) 실시간 갱신
                    if (GameClient.Instance != null)
                    {
                        GameClient.Instance.ApplyPurchaseResult(response);
                    }

                    // StorageManager의 로컬 창고 데이터(재화, 보유 팩, 스킨) 실시간 갱신
                    if (StorageManager.Instance != null)
                    {
                        StorageManager.Instance.ApplyPurchaseResult(response);
                    }

                    // 전역 재화 UI 즉시 직접 동기화
                    if (UserCurrencyDisplay.Instance != null)
                    {
                        UserCurrencyDisplay.Instance.SetCurrency(response.remainingGold, response.remainingStellastone, response.remainingStardust);
                    }

                    OnPurchaseCompleted?.Invoke(response);

                    // 팝업 닫기
                    CloseProductPopup();

                    // 구매 완료 후 상점 슬롯 상태 즉시 갱신 (품절/이미구매 표시)
                    LoadCategory(currentActiveCategory, true);
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
