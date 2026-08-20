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
    [Tooltip("재화 이미지입니다.")]
    public Image priceImage;

    [Header("데이터 설정")]
    [Tooltip("아이템의 단일 가격입니다.")]
    public int itemPrice = 100;
    [Tooltip("최대 구매 가능 수량입니다.")]
    public int maxQuantity = 99;
    [Tooltip("최소 구매 가능 수량입니다.")]
    public int minQuantity = 1;

    // 현재 선택된 수량을 저장하는 변수
    private int currentQuantity = 1;

    /// <summary>
    /// 현재 선택된 수량 반환 프로퍼티
    /// </summary>
    public int CurrentQuantity => currentQuantity;

    void Start()
    {
        if (decreaseButton != null) decreaseButton.onClick.AddListener(OnDecreaseClicked);
        if (increaseButton != null) increaseButton.onClick.AddListener(OnIncreaseClicked);
        if (quantityInput != null) quantityInput.onValueChanged.AddListener(OnInputFieldValueChanged);

        UpdateQuantity(1);
    }

    private void OnDecreaseClicked()
    {
        UpdateQuantity(currentQuantity - 1);
    }

    private void OnIncreaseClicked()
    {
        UpdateQuantity(currentQuantity + 1);
    }

    private void OnInputFieldValueChanged(string newText)
    {
        if (int.TryParse(newText, out int newQuantity))
        {
            UpdateQuantity(newQuantity);
        }
    }

    /// <summary>
    /// 수량을 업데이트하고 유효성을 검사하며 UI를 갱신합니다.
    /// </summary>
    public void UpdateQuantity(int newQuantity)
    {
        currentQuantity = Mathf.Clamp(newQuantity, minQuantity, maxQuantity);

        if (quantityInput != null && quantityInput.text != currentQuantity.ToString())
        {
            quantityInput.text = currentQuantity.ToString();
        }

        if (totalPriceText != null)
        {
            int totalPrice = currentQuantity * itemPrice;
            totalPriceText.text = totalPrice.ToString("N0");
        }
    }
}
