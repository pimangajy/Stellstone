using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnEndController : MonoBehaviour
{
    [Header("UI References")]
    public Button turnButton;
    public TextMeshProUGUI statusText;

    [Tooltip("시간 표시를 위한 슬라이더 컴포넌트")]
    public Slider timerSlider;

    [Tooltip("슬라이더의 색상을 변경하기 위한 Fill 이미지")]
    public Image sliderFillImage;

    [Header("Settings")]
    public float maxTurnTime = 60f;
    [Tooltip("경고 색상으로 변경될 남은 시간 기준 (초)")]
    public float warningThreshold = 10f;

    [Header("Colors")]
    public Color myTurnColor = new Color(0.2f, 0.8f, 0.4f); // 초록색
    public Color enemyTurnColor = new Color(0.9f, 0.3f, 0.2f); // 빨간색
    public Color warningColor = new Color(1f, 0.6f, 0f); // 주황색/노란색

    private float currentTimer;
    private bool isMyTurn = true;

    void Start()
    {
        TimerSetting();

        currentTimer = maxTurnTime;
        UpdateUI();

        if (turnButton != null)
            turnButton.onClick.AddListener(ToggleTurn);
    }

    void Update()
    {
        UpdateTimer();
    }

    void UpdateTimer()
    {
        if (currentTimer > 0)
        {
            currentTimer -= Time.deltaTime;

            if (timerSlider != null)
            {
                timerSlider.value = currentTimer;
            }

            // 10초 이하일 때 색상 변경 로직
            if (currentTimer <= warningThreshold && sliderFillImage != null)
            {
                sliderFillImage.color = warningColor;
            }

            if (currentTimer <= 0)
            {
                currentTimer = 0;
                ToggleTurn();
            }
        }
    }

    // 타이머 초기화 및 최대값 변경
    public void TimerSetting()
    {
        if ((timerSlider != null))
        {
            timerSlider.maxValue = maxTurnTime;
            timerSlider.minValue = 0;
            timerSlider.value = maxTurnTime;
            timerSlider.interactable = false;
        }
    }

    // 턴 변경
    public void ToggleTurn()
    {
        isMyTurn = !isMyTurn;
        currentTimer = maxTurnTime;

        if (timerSlider != null)
        {
            timerSlider.value = maxTurnTime;
        }

        UpdateUI();
    }

    // 턴 버튼 텍스트 변경
    void UpdateUI()
    {
        if (isMyTurn) 
        {
            statusText.text = "나의 턴";
            statusText.color = Color.white;

            // 내 턴 시작 시 기본 색상으로 초기화 (10초 이상일 때)
            if (sliderFillImage != null)
                sliderFillImage.color = myTurnColor;
        }
        else
        {
            statusText.text = "상대의 턴";
            statusText.color = Color.gray;

            if (sliderFillImage != null)
                sliderFillImage.color = enemyTurnColor;
        }
    }
}