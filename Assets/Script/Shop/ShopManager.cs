using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;


// JSON 응답을 역직렬화할 때 사용할 클래스들
// 이 클래스들의 구조는 Flask 서버에서 반환하는 JSON 응답 구조와 정확히 일치해야 합니다.
[System.Serializable]
public class ProductData
{
    public int id;
    public string name;
    public string description;
    public string image_url;
    public int price;
    public int currency_id;
    public int category_id;
    public bool is_active;
    public string sale_start_date; // 날짜는 문자열로 받아서 파싱
    public string sale_end_date;   // 날짜는 문자열로 받아서 파싱
    public ProductCurrencyData Currencies;
}
// ProductData 클래스 내부에 Currencies 정보를 담을 클래스 정의
[System.Serializable]
public class ProductCurrencyData
{
    public string currency_name;
    public string icon_url;
}

[System.Serializable]
public class ProductsApiResponse
{
    public string status;
    public string message;
    public List<ProductData> data; // 여러 개의 ProductData 객체가 담길 리스트
}
public class ShopManager : MonoBehaviour
{
    // Flask 서버의 상품 조회 API URL (기본 경로)
    public string productsApiBaseUrl = "http://localhost:5000/api/products";
    // 인스펙터에서 설정할 카테고리 ID
    public int targetCategoryId = 5;

    // 상점 슬롯들이 배치될 UI 부모 오브젝트 (Scroll View의 Content 등)
    public Transform shopContentParent;
    // 각 상품 정보를 표시할 상점 슬롯 UI 프리팹
    public GameObject shopSlotPrefab;
    // 현재 활성화되어 표시 중인 카테고리 ID (중복 로딩 방지용)
    private int currentActiveCategoryId = -1; // 초기값은 유효하지 않은 ID로 설정

    // --- 카테고리 버튼 클릭 시 호출될 함수 ---
    // 각 카테고리 버튼(예: 카드팩, 리더스킨, 아바타)의 OnClick() 이벤트에 연결합니다.
    // 인스펙터에서 이 함수를 연결할 때, 각 카테고리에 해당하는 int 값을 인자로 넣어주세요.
    // 예: 카드팩 버튼 -> OnCategoryButtonClicked(1)
    //     리더스킨 버튼 -> OnCategoryButtonClicked(2)
    public void OnCategoryButtonClicked(int categoryId)
    {
        Debug.Log($"카테고리 버튼 클릭됨: {categoryId}");

        // 이미 해당 카테고리가 표시 중이라면 불필요한 작업 방지
        if (currentActiveCategoryId == categoryId)
        {
            Debug.Log($"카테고리 {categoryId}는 이미 표시 중입니다. 재로딩하지 않습니다.");
            return;
        }

        // 기존 상점 슬롯 모두 삭제
        ClearShopSlots();

        // 현재 활성화된 카테고리 ID 업데이트
        currentActiveCategoryId = categoryId;

        // 서버에서 필터링된 상품 데이터 가져오기 시작
        StartCoroutine(GetFilteredProductsFromServer(categoryId));
    }

    // 기존에 생성된 모든 상점 슬롯을 삭제하는 함수
    private void ClearShopSlots()
    {
        if (shopContentParent == null)
        {
            Debug.LogError("Shop Content Parent가 설정되지 않았습니다. 슬롯을 삭제할 수 없습니다.");
            return;
        }

        // shopContentParent의 모든 자식 오브젝트를 순회하며 삭제
        foreach (Transform child in shopContentParent)
        {
            Destroy(child.gameObject);
        }
        Debug.Log("기존 상점 슬롯이 모두 삭제되었습니다.");
    }

    // 상점 버튼 클릭 시 호출될 함수
    public void OnShopButtonClicked(int categori_id)
    {
        Debug.Log($"상점 버튼 클릭됨! 카테고리 {targetCategoryId}의 상품을 서버에서 필터링하여 가져옵니다.");
        StartCoroutine(GetFilteredProductsFromServer(categori_id));
    }

    // 서버에서 필터링된 상품 데이터를 가져오는 코루틴
    private IEnumerator GetFilteredProductsFromServer(int categoryId)
    {
        // 쿼리 파라미터를 포함한 URL 생성
        // 예: http://localhost:5000/api/products?category_id=5
        string requestUrl = $"{productsApiBaseUrl}?category_id={categoryId}";
        Debug.Log($"서버 요청 URL: {requestUrl}");

        using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
        {
            yield return webRequest.SendWebRequest();

            // UnityWebRequest.Result 열거형을 직접 사용하여 오류 처리
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError: // 네트워크 연결 문제 (예: 인터넷 끊김, DNS 오류 등)
                    Debug.LogError($"네트워크 연결 오류: {webRequest.error}");
                    break;
                case UnityWebRequest.Result.ProtocolError: // HTTP 프로토콜 오류 (예: 404 Not Found, 500 Internal Server Error 등)
                    Debug.LogError($"HTTP 프로토콜 오류: {webRequest.responseCode} - {webRequest.error}");
                    Debug.LogError($"서버 응답: {webRequest.downloadHandler.text}");
                    break;
                case UnityWebRequest.Result.Success: // 요청 성공
                    string jsonResponse = webRequest.downloadHandler.text;
                    Debug.Log($"서버에서 받은 필터링된 상품 데이터: {jsonResponse}");

                    try
                    {
                        ProductsApiResponse apiResponse = JsonUtility.FromJson<ProductsApiResponse>(jsonResponse);

                        if (apiResponse.status == "success" && apiResponse.data != null)
                        {
                            // 서버에서 이미 필터링된 데이터이므로, 바로 사용합니다.
                            List<ProductData> filteredProducts = apiResponse.data;

                            Debug.Log($"서버에서 가져온 카테고리 ID {categoryId}에 해당하는 상품 개수: {filteredProducts.Count}개");

                            // 이제 filteredProducts 리스트에 특정 카테고리 상품 데이터가 담겨있어요.
                            // 이 데이터를 가지고 상점 슬롯을 생성하는 로직을 여기에 추가하면 됩니다.
                            // 예: CreateShopSlots(filteredProducts);

                            // 가져온 데이터를 바탕으로 상점 슬롯 생성
                            CreateShopSlots(filteredProducts);
                        }
                        else
                        {
                            Debug.LogError($"상품 데이터를 가져오는 데 실패했습니다: {apiResponse.message}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"JSON 파싱 오류: {e.Message}");
                        Debug.LogError($"오류 발생 JSON: {jsonResponse}");
                    }
                    break;
                default: // 기타 예상치 못한 오류 (예: DataProcessingError 등)
                    Debug.LogError($"알 수 없는 UnityWebRequest 오류: {webRequest.result} - {webRequest.error}");
                    break;
            }
        }
    }

    // 상점 슬롯을 생성하고 데이터를 채워 넣는 함수
    private void CreateShopSlots(List<ProductData> products)
    {
        if (shopContentParent == null || shopSlotPrefab == null)
        {
            Debug.LogError("Shop Content Parent 또는 Shop Slot Prefab이 설정되지 않았습니다. 상점 슬롯을 생성할 수 없습니다.");
            return;
        }

        Debug.Log($"총 {products.Count}개의 상점 슬롯을 생성합니다.");
        foreach (ProductData product in products)
        {
            // 프리팹 인스턴스화
            GameObject newSlot = Instantiate(shopSlotPrefab, shopContentParent);
            newSlot.name = $"ShopSlot_{product.id}"; // 오브젝트 이름 설정 (디버깅 용이)

            ShopSlotUI slotUI = newSlot.GetComponent<ShopSlotUI>();
            if (slotUI != null)
            {
                slotUI.SetProductData(product); // ShopSlotUI 스크립트에 SetProductData 함수가 있다고 가정
                Debug.Log($"슬롯 생성 완료: {product.name} (ID: {product.id}, 가격: {product.price})");
            }
            else
            {
                Debug.LogWarning($"ShopSlotPrefab에 ShopSlotUI 컴포넌트가 없습니다: {newSlot.name}");
            }
        }
    }
}
