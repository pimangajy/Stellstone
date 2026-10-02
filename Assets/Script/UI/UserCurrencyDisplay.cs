using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬별로 표시할 재화의 종류를 설정하는 데이터 클래스입니다.
/// </summary>
[System.Serializable]
public class SceneCurrencySetting
{
    [Tooltip("씬 이름 (예: DeckBuildingScene, ShopScene, BattleScenes)")]
    public string sceneName;

    [Tooltip("골드 표시 여부")]
    public bool showGold = true;

    [Tooltip("스텔라스톤 표시 여부")]
    public bool showStellastone = true;

    [Tooltip("스타더스트 표시 여부")]
    public bool showStardust = false;

    [Tooltip("항복 버튼")]
    public bool showSurrenderButton = false;

    public SceneCurrencySetting() { }

    public SceneCurrencySetting(string name, bool gold, bool stella, bool dust, bool surrender)
    {
        sceneName = name;
        showGold = gold;
        showStellastone = stella;
        showStardust = dust;
        showSurrenderButton = surrender;
    }
}

/// <summary>
/// 씬 간 이동하는 전역 Canvas에서 유저의 재화(골드, 유료재화 등)를 실시간으로 동기화하고,
/// 씬마다 보여줄 재화의 종류(골드, 스텔라스톤, 스타더스트)를 개별 GameObject 단위로 켜고 끄는 컴포넌트입니다.
/// </summary>
public class UserCurrencyDisplay : MonoBehaviour
{
    public static UserCurrencyDisplay Instance { get; private set; }

    [Header("1. 재화별 GameObject (씬별 활성/비활성화 대상)")]
    [Tooltip("골드 UI 오브젝트 (아이콘+텍스트를 감싼 부모 GameObject)")]
    public GameObject goldObject;

    [Tooltip("스텔라스톤 UI 오브젝트 (아이콘+텍스트를 감싼 부모 GameObject)")]
    public GameObject stellastoneObject;

    [Tooltip("스타더스트 UI 오브젝트 (아이콘+텍스트를 감싼 부모 GameObject)")]
    public GameObject stardustObject;

    [Header("2. 재화 표시 텍스트 (수치 반영 대상)")]
    [Tooltip("골드(인게임 무료 재화) 잔액 텍스트")]
    public TMP_Text goldText;

    [Tooltip("스텔라스톤/성석(유료 결제 재화) 잔액 텍스트")]
    public TMP_Text stellastoneText;

    [Tooltip("스타더스트/별가루(제작 재화) 잔액 텍스트 (선택 사항)")]
    public TMP_Text stardustText;

    [Tooltip("항복 버튼 (배틀 씬에서만 활성화)")]
    public GameObject surrenderButton;

    [Header("3. 씬별 재화 표시 규칙")]
    [Tooltip("인스펙터에서 씬마다 보여줄 재화 종류를 설정합니다.")]
    public List<SceneCurrencySetting> sceneSettings = new List<SceneCurrencySetting>
    {
        new SceneCurrencySetting("DeckBuildingScene", true, true, true, false),   // 덱편성: 3종류
        new SceneCurrencySetting("ShopScene", true, true, false, false),          // 상점: 골드, 스텔라스톤 (2종류)
        new SceneCurrencySetting("BattleScenes", false, false, false, true),     // 배틀: 0종류
        new SceneCurrencySetting("Login", false, false, false, false),            // 로그인: 0종류
        new SceneCurrencySetting("LoadingScene", false, false, false, false),     // 로딩: 0종류
        new SceneCurrencySetting("MainScene", true, true, true, false),           // 메인: 3종류
        new SceneCurrencySetting("Storage", true, true, true, false),             // 창고/보관함: 3종류
    };

    [Header("4. 기본 표시 규칙 (목록에 없는 씬일 때 적용)")]
    public bool defaultShowGold = true;
    public bool defaultShowStellastone = true;
    public bool defaultShowStardust = false;

    [Header("5. 서식 설정")]
    [Tooltip("천 단위 콤마 표기 여부 (체크 시 12,345 형태로 표시)")]
    public bool useNumberFormatting = true;

    private bool _isSubscribed = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        TrySubscribe();
    }

    private void OnEnable()
    {
        // 씬 전환 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
        TrySubscribe();

        // 현재 씬 상태 및 재화 확인
        CheckSceneVisibility(SceneManager.GetActiveScene().name);
        RefreshCurrency();
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_isSubscribed && GameClient.Instance != null)
        {
            GameClient.Instance.OnUserDataUpdated -= UpdateCurrencyUI;
            _isSubscribed = false;
        }
    }

    private void Start()
    {
        TrySubscribe();
        CheckSceneVisibility(SceneManager.GetActiveScene().name);
        RefreshCurrency();
    }

    /// <summary>
    /// 새로운 씬이 로드되었을 때 호출됩니다.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySubscribe();
        CheckSceneVisibility(scene.name);
        RefreshCurrency();
    }

    /// <summary>
    /// GameClient 이벤트에 안전하게 구독합니다. (초기화 타이밍 문제 방지)
    /// </summary>
    public void TrySubscribe()
    {
        if (!_isSubscribed && GameClient.Instance != null)
        {
            GameClient.Instance.OnUserDataUpdated += UpdateCurrencyUI;
            _isSubscribed = true;
        }
    }

    /// <summary>
    /// 상점 구매 등 외부에서 즉시 잔액을 지정하여 텍스트를 갱신할 때 호출합니다.
    /// </summary>
    public void SetCurrency(int gold, int stellastone, int stardust = -1)
    {
        SetText(goldText, gold);
        SetText(stellastoneText, stellastone);
        if (stardust >= 0)
        {
            SetText(stardustText, stardust);
        }

        // 로컬 데이터 객체들도 동기화 유지
        if (GameClient.Instance != null && GameClient.Instance.CurrentUser != null)
        {
            GameClient.Instance.CurrentUser.gold = gold;
            GameClient.Instance.CurrentUser.stellastone = stellastone;
            if (stardust >= 0) GameClient.Instance.CurrentUser.stardust = stardust;
        }

        if (SinginManager.CurrentUserData != null)
        {
            SinginManager.CurrentUserData.gold = gold;
            SinginManager.CurrentUserData.stellastone = stellastone;
            if (stardust >= 0) SinginManager.CurrentUserData.stardust = stardust;
        }

        Debug.Log($"[UserCurrencyDisplay] 💰 재화 실시간 갱신 완료: 골드={gold}, 성석={stellastone}");
    }

    /// <summary>
    /// 현재 씬에 맞춰 각 재화 GameObject의 활성/비활성화 상태를 개별 적용합니다.
    /// (설정 버튼 등이 포함된 상위 부모 패널은 끄지 않고, 재화 오브젝트들만 개별 제어합니다)
    /// </summary>
    public void CheckSceneVisibility(string sceneName)
    {
        ApplySceneCurrencyVisibility(sceneName);
    }

    /// <summary>
    /// 씬 이름에 따라 골드, 스텔라스톤, 스타더스트 및 항복 버튼 GameObject의 활성화 여부를 적용합니다.
    /// </summary>
    public void ApplySceneCurrencyVisibility(string sceneName)
    {
        bool showGold = defaultShowGold;
        bool showStellastone = defaultShowStellastone;
        bool showStardust = defaultShowStardust;
        bool showSurrender = false;

        // 씬별 규칙 탐색 (대소문자 무시)
        if (sceneSettings != null)
        {
            SceneCurrencySetting setting = sceneSettings.Find(s =>
                string.Equals(s.sceneName, sceneName, StringComparison.OrdinalIgnoreCase));

            if (setting != null)
            {
                showGold = setting.showGold;
                showStellastone = setting.showStellastone;
                showStardust = setting.showStardust;
                showSurrender = setting.showSurrenderButton;
            }
            else if (!string.IsNullOrEmpty(sceneName) && sceneName.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                showSurrender = true;
            }
        }
        else if (!string.IsNullOrEmpty(sceneName) && sceneName.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            showSurrender = true;
        }

        // 각 재화 GameObject 켜고 끄기
        if (goldObject != null)
        {
            goldObject.SetActive(showGold);
        }
        if (stellastoneObject != null)
        {
            stellastoneObject.SetActive(showStellastone);
        }
        if (stardustObject != null)
        {
            stardustObject.SetActive(showStardust);
        }
        if (surrenderButton != null)
        {
            surrenderButton.SetActive(showSurrender);
        }

        // 패널 레이아웃 즉시 갱신 (재화 활성화 상태에 따라 메뉴 및 재화 위치 실시간 재계산)
        RectTransform panelRt = goldObject?.transform.parent as RectTransform 
            ?? stellastoneObject?.transform.parent as RectTransform;
        if (panelRt != null)
        {
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panelRt);
        }

        Debug.Log($"[UserCurrencyDisplay] 씬 '{sceneName}' 표시 설정: 골드={showGold}, 성석={showStellastone}, 가루={showStardust}, 항복={showSurrender}");
    }

    /// <summary>
    /// 계정 정보로부터 재화 수치를 읽어와 텍스트를 즉시 최신화합니다.
    /// </summary>
    public void RefreshCurrency()
    {
        TrySubscribe();

        UserData userData = null;

        // 1. GameClient의 런타임 CurrentUser 우선 참조
        if (GameClient.Instance != null && GameClient.Instance.CurrentUser != null)
        {
            userData = GameClient.Instance.CurrentUser;
        }
        // 2. 미존재 시 SinginManager의 전역 UserData fallback 참조
        else if (SinginManager.CurrentUserData != null)
        {
            userData = SinginManager.CurrentUserData;
        }

        UpdateCurrencyUI(userData);
    }

    /// <summary>
    /// 재화 데이터를 UI 텍스트에 포맷팅하여 대입합니다.
    /// </summary>
    private void UpdateCurrencyUI(UserData data)
    {
        if (data == null)
        {
            SetText(goldText, 0);
            SetText(stellastoneText, 0);
            SetText(stardustText, 0);
            return;
        }

        SetText(goldText, data.gold);
        SetText(stellastoneText, data.stellastone);
        SetText(stardustText, data.stardust);
    }

    /// <summary>
    /// 정수값을 설정된 서식에 맞게 TMP_Text에 대입합니다.
    /// </summary>
    private void SetText(TMP_Text textComp, int amount)
    {
        if (textComp != null)
        {
            textComp.text = useNumberFormatting ? amount.ToString("N0") : amount.ToString();
        }
    }
}
