using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuantitySelector : MonoBehaviour
{
    [Header("UI 연결")]
    [Tooltip("수량을 줄이는 '-' 버튼입니다.")]
    public Button decreaseButton;
    [Tooltip("수량을 늘리는 '+' 버튼입니다.")]
    public Button increaseButton;
    [Tooltip("수량을 직접 입력하는 InputField입니다.")]
    public TMP_InputField quantityInput;
    [Tooltip("총 가격을 표시할 TextMeshPro UI입니다.")]
    public TextMeshProUGUI totalPriceText;

    [Header("아이템 정보")]
    [Tooltip("아이템의 개당 가격입니다.")]
    public int itemPrice = 100;
    [Tooltip("최대 구매 가능 수량입니다.")]
    public int maxQuantity = 99;
    [Tooltip("최소 구매 가능 수량입니다.")]
    public int minQuantity = 1;

    // 현재 선택된 수량을 저장하는 변수
    private int currentQuantity = 1;

    /// <summary>
    /// 스크립트가 처음 시작될 때 호출됩니다.
    /// </summary>
    void Start()
    {
        // 각 UI 요소에 이벤트 리스너를 동적으로 추가합니다.
        decreaseButton.onClick.AddListener(OnDecreaseClicked);
        increaseButton.onClick.AddListener(OnIncreaseClicked);
        // InputField의 값이 변경될 때마다 함수를 호출하도록 연결합니다.
        quantityInput.onValueChanged.AddListener(OnInputFieldValueChanged);

        // 시작 시 수량을 1로 초기화하고 UI를 업데이트합니다.
        UpdateQuantity(1);
    }

    /// <summary>
    /// '-' 버튼을 클릭했을 때 호출될 함수입니다.
    /// </summary>
    private void OnDecreaseClicked()
    {
        UpdateQuantity(currentQuantity - 1);
    }

    /// <summary>
    /// '+' 버튼을 클릭했을 때 호출될 함수입니다.
    /// </summary>
    private void OnIncreaseClicked()
    {
        UpdateQuantity(currentQuantity + 1);
    }

    /// <summary>
    /// InputField에 직접 값을 입력할 때 호출될 함수입니다.
    /// </summary>
    private void OnInputFieldValueChanged(string newText)
    {
        // 입력된 텍스트를 숫자로 변환하여 수량을 업데이트합니다.
        if (int.TryParse(newText, out int newQuantity))
        {
            UpdateQuantity(newQuantity);
        }
    }

    /// <summary>
    /// 수량을 업데이트하고, 유효성을 검사하며, UI를 갱신하는 핵심 함수입니다.
    /// </summary>
    private void UpdateQuantity(int newQuantity)
    {
        // 수량이 최소/최대 범위를 벗어나지 않도록 값을 제한합니다.
        currentQuantity = Mathf.Clamp(newQuantity, minQuantity, maxQuantity);

        // InputField의 텍스트를 현재 수량으로 업데이트합니다.
        // (무한 루프를 방지하기 위해, 현재 텍스트와 다를 때만 업데이트합니다.)
        if (quantityInput.text != currentQuantity.ToString())
        {
            quantityInput.text = currentQuantity.ToString();
        }

        // 총 가격을 계산하고 UI 텍스트를 업데이트합니다.
        int totalPrice = currentQuantity * itemPrice;
        totalPriceText.text = totalPrice.ToString("N0"); // "N0"는 1,000 단위 콤마를 추가해줍니다.
    }
}
