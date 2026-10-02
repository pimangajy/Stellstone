using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// BattleManager의 턴 종료 버튼, 타이머 슬라이더, 마나 크리스탈 시각화 및 하이라이트 UI 전담 partial 클래스입니다.
/// </summary>
public partial class BattleManager
{
    [Header("Mana UI Settings (마나 시각화)")]
    public TextMeshProUGUI manaText;      // "3 / 5" 처럼 숫자로 표시할 텍스트
    public Image[] playerManaCrystals;    // 10개의 마나 이미지 배열
    public Sprite manaOnSprite;           // 채워진 마나 이미지 (On)
    public Sprite manaOffSprite;          // 사용한 마나 이미지 (Empty Slot)
    public Sprite manaLockedSprite;       // 아직 잠긴 마나 이미지 (선택 사항, 투명하게 처리 가능)
    public Color manaHighlightColor = Color.yellow; // 카드 드래그 시 소모될 마나 강조 색상

    [Header("Turn End UI (턴 종료 UI)")]
    public Button turnButton;          // 턴 종료 버튼
    public TextMeshProUGUI statusText; // 버튼 중앙 텍스트 ("나의 턴" 등)
    public Slider timerSlider;         // 시간 게이지 슬라이더
    public Image sliderFillImage;      // 슬라이더 색상 변경을 위한 이미지
    public float remainingTime;        // 서버 동기화 기반 남은 시간
    private long _turnEndTimeTimestamp; // 서버에서 보낸 종료 시점
    private float prevRemainingTime;   // 턴종료 타이밍을 위한 변수

    [Header("UI Settings (UI 설정)")]
    public float warningThreshold = 10f; // 경고 색상 시작 시간 (초)
    public Color myTurnColor = new Color(0.2f, 0.8f, 0.4f);   // 내 턴 색상 (초록)
    public Color enemyTurnColor = new Color(0.9f, 0.3f, 0.2f); // 상대 턴 색상 (빨강)
    public Color warningColor = new Color(1f, 0.6f, 0f);       // 경고 색상 (주황)

    /// <summary>
    /// Start 시 UI 컴포넌트 초기화
    /// </summary>
    private void InitUI()
    {
        // 버튼 클릭 이벤트 연결 (멀리건 결정 & 턴 종료 통합)
        if (turnButton != null)
        {
            turnButton.onClick.AddListener(OnTurnButtonClicked);
        }

        // 초기 시작 상태: 멀리건 단계 UI 설정 ("결정")
        SetMulliganUI();

        // 슬라이더 초기 설정
        if (timerSlider != null)
        {
            timerSlider.minValue = 0;
            timerSlider.interactable = false;
        }
    }

    /// <summary>
    /// Update에서 매 프레임 호출되는 타이머 및 슬라이더 갱신
    /// </summary>
    private void UpdateTimerAndUI()
    {
        // 1. 서버 동기화 기반 시간 계산
        if (_turnEndTimeTimestamp > 0)
        {
            UpdateRemainingTime();
        }

        // 2. UI 슬라이더 업데이트
        UpdateTimerUI();
    }

    /// <summary>
    /// 10개의 마나 수정을 현재 마나와 최대 마나에 맞춰 시각화합니다.
    /// </summary>
    public void UpdateManaUI()
    {
        // 1. 텍스트 업데이트 (예: 3 / 5)
        if (manaText != null)
        {
            manaText.text = $"{playerCurrentMana} / {playerMaxMana}";
        }

        // 2. 이미지 배열 업데이트 (최대 10개 가정)
        if (playerManaCrystals == null || playerManaCrystals.Length == 0) return;

        for (int i = 0; i < playerManaCrystals.Length; i++)
        {
            int slotNumber = i + 1;

            if (slotNumber <= playerCurrentMana)
            {
                playerManaCrystals[i].sprite = manaOnSprite;
                playerManaCrystals[i].gameObject.SetActive(true);
                playerManaCrystals[i].color = Color.white;
            }
            else if (slotNumber <= playerMaxMana)
            {
                playerManaCrystals[i].sprite = manaOffSprite;
                playerManaCrystals[i].gameObject.SetActive(true);
                playerManaCrystals[i].color = Color.white;
            }
            else
            {
                if (manaLockedSprite != null)
                {
                    playerManaCrystals[i].sprite = manaLockedSprite;
                    playerManaCrystals[i].gameObject.SetActive(true);
                }
                else
                {
                    playerManaCrystals[i].gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// 카드를 드래그하거나 호버할 때 호출하여 소모될 예정인 마나를 시각적으로 강조합니다.
    /// </summary>
    public void HighlightManaCost(int cost)
    {
        UpdateManaUI();

        bool canHighlight = isPlayerTurn || (GameClient.Instance == null || !GameClient.Instance.IsConnected);
        if (cost <= 0 || !canHighlight) return;

        if (playerManaCrystals == null || playerManaCrystals.Length == 0) return;

        int highlightedCount = 0;
        for (int i = playerCurrentMana - 1; i >= 0 && highlightedCount < cost; i--)
        {
            if (i >= 0 && i < playerManaCrystals.Length)
            {
                playerManaCrystals[i].color = manaHighlightColor;
                highlightedCount++;
            }
        }

        if (cost > playerCurrentMana)
        {
            for (int i = 0; i < playerCurrentMana && i < playerManaCrystals.Length; i++)
            {
                playerManaCrystals[i].color = Color.red;
            }
        }
    }

    // 턴종료 타이밍을 맞추기 위한 변수 초기화
    public void SetTimer()
    {
        prevRemainingTime = float.MaxValue;
    }

    // [최적화] 타이머 계산 (밀리초 정밀도로 60fps 부드러운 슬라이더 애니메이션 제공)
    private void UpdateRemainingTime()
    {
        double currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        float diff = (float)(_turnEndTimeTimestamp - currentUnixTime);
        remainingTime = Mathf.Max(0f, diff);

        if (prevRemainingTime > 0f && remainingTime <= 0f)
        {
            Debug.Log("시간 초과로 턴 변경");
            RequestEndTurn();
        }

        prevRemainingTime = remainingTime;
    }

    // 타이머 UI 슬라이더 감소
    private void UpdateTimerUI()
    {
        if (timerSlider == null) return;

        timerSlider.value = remainingTime;

        if (isPlayerTurn && remainingTime <= warningThreshold)
        {
            if (sliderFillImage != null) sliderFillImage.color = warningColor;
        }
    }

    // 멀리건 버튼 상태 설정 ("결정")
    public void SetMulliganUI()
    {
        if (turnButton == null || statusText == null) return;

        statusText.text = "결정";
        statusText.color = Color.white;
        turnButton.interactable = true;
        if (sliderFillImage != null) sliderFillImage.color = myTurnColor;
    }

    // 턴/멀리건 통합 버튼 클릭 이벤트 핸들러
    private void OnTurnButtonClicked()
    {
        if (isMulliganPhase)
        {
            if (GameMulliganManager.instance != null)
            {
                GameMulliganManager.instance.ConfirmMulligan();
            }

            if (statusText != null)
            {
                statusText.text = "대기 중...";
                statusText.color = Color.gray;
            }
            if (turnButton != null) turnButton.interactable = false;
        }
        else
        {
            if (isPlayerTurn && !isTurnEndRequested)
            {
                // 연타 방지: 클릭 즉시 버튼 비활성화 및 안내 텍스트 표시
                if (turnButton != null) turnButton.interactable = false;
                if (statusText != null)
                {
                    statusText.text = "턴 종료 중...";
                    statusText.color = Color.gray;
                }

                RequestEndTurn();
            }
        }
    }

    // 턴종료 버튼 UI 설정
    public void RefreshTurnUI()
    {
        if (turnButton == null || statusText == null) return;

        if (isMulliganPhase)
        {
            SetMulliganUI();
            return;
        }

        if (isPlayerTurn)
        {
            isTurnEndRequested = false; // 내 턴이 시작되면 턴 종료 요청 플래그 초기화
            statusText.text = "턴 종료";
            statusText.color = Color.white;
            turnButton.interactable = true;
            if (sliderFillImage != null) sliderFillImage.color = myTurnColor;
        }
        else
        {
            statusText.text = "상대의 턴";
            statusText.color = Color.gray;
            turnButton.interactable = false;
            if (sliderFillImage != null) sliderFillImage.color = enemyTurnColor;
        }
    }
}
