using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System; // Action 이벤트를 위해 필요

/// <summary>
/// '덱 선택' 팝업창의 모든 UI와 로직을 관리합니다.
/// 팝업의 열기/닫기 애니메이션은 UIPanelToggler가 담당합니다.
/// </summary>
[RequireComponent(typeof(UIPanelToggler))] // 이 스크립트는 UIPanelToggler와 함께 있어야 함
public class DeckSelectPopup : MonoBehaviour
{
    // [삭제] 팝업 열고 닫기는 UIPanelToggler가 담당하므로 이 변수는 제거합니다.
    // [SerializeField] private GameObject popupPanel;
    [SerializeField] private Button closeButton; // 닫기 버튼

    [Header("덱 목록 (왼쪽)")]
    [SerializeField] private Transform deckListParent; // 덱 버튼 프리팹이 생성될 부모 (ScrollView의 Content)
    [SerializeField] private GameObject deckButtonPrefab; // 덱 버튼 프리팹

    [Header("덱 상세 (오른쪽)")]
    [SerializeField] private TextMeshProUGUI deckNameText; // 선택된 덱의 이름
    [SerializeField] private Image leaderImage; // 선택된 덱의 리더(직업) 이미지
    [SerializeField] private Button selectDeckButton; // 팝업창의 '덱 선택' (확정) 버튼
    [SerializeField] private Button editDeckButton; // '덱 편집' 버튼

    // 현재 팝업창에서 선택(클릭)한 덱의 정보
    private DeckData currentlyViewedDeck;

    // 플레이어가 덱을 최종 '확정'했을 때 발생하는 이벤트
    // LobbyManager가 이 이벤트를 구독하여 선택된 덱 정보를 받습니다.
    public static event Action<DeckData> OnDeckConfirmed;

    // (추가) 팝업 애니메이션을 제어할 Toggler
    [SerializeField] private UIPanelToggler panelToggler;

    private void Awake()
    {
        // UIPanelToggler 컴포넌트를 이 오브젝트에서 직접 가져옵니다.
        panelToggler = GetComponent<UIPanelToggler>();
        if (panelToggler == null)
        {
            Debug.LogError("DeckSelectPopup 스크립트가 있는 오브젝트에 UIPanelToggler가 없습니다!");
        }
    }

    private void Start()
    {
        // 팝업창의 버튼들에 리스너 연결
        closeButton.onClick.AddListener(ClosePopup);
        selectDeckButton.onClick.AddListener(ConfirmSelection);
        editDeckButton.onClick.AddListener(GoToDeckEdit);

        // [삭제] 팝업 숨김 처리는 UIPanelToggler의 Awake()가 담당합니다.
        // popupPanel.SetActive(false);
    }

    /// <summary>
    /// 팝업을 엽니다. (로비의 '덱 선택' 버튼에서 호출)
    /// 이전에 선택한 덱을 전달받습니다.
    /// </summary>
    public void OpenPopup(DeckData previouslySelectedDeck)
    {
        if (panelToggler == null) return;

        // 1. 패널 토글러를 사용해 팝업을 엽니다.
        panelToggler.ShowPanel();

        // 2. 덱 목록을 채웁니다. (이건 항상 실행)
        PopulateDeckList();

        // 3. (수정) 이전에 선택한 덱이 있는지 확인
        if (previouslySelectedDeck != null)
        {
            // 이전에 선택한 덱이 있으면, 그 덱의 상세 정보를 바로 표시
            ShowDeckDetails(previouslySelectedDeck);
        }
        else
        {
            // 이전에 선택한 덱이 없으면 (처음 고르는 경우), 팝업을 초기화
            currentlyViewedDeck = null;
            deckNameText.text = "덱을 선택하세요";
            // leaderImage.sprite = null; // 기본 이미지로 설정
            selectDeckButton.interactable = false; // 덱 선택 전까지 비활성화
            editDeckButton.interactable = false; // 덱 선택 전까지 비활성화
        }
    }

    /// <summary>
    /// 팝업을 닫습니다. (UIPanelToggler에게 닫기를 요청)
    /// </summary>
    public void ClosePopup()
    {
        // [수정] 팝업을 직접 끄는 대신 Toggler의 HidePanel()을 호출합니다.
        panelToggler.HidePanel();
    }

    /// <summary>
    /// DeckSaveManager에서 덱 목록을 가져와 왼쪽 스크롤 뷰를 채웁니다.
    /// </summary>
    private void PopulateDeckList()
    {
        // 1. 기존 덱 버튼들을 모두 삭제
        foreach (Transform child in deckListParent)
        {
            Destroy(child.gameObject);
        }

        // 2. 영구 매니저에서 덱 목록을 가져옵니다. (네트워크 호출 X, 캐시된 데이터 즉시 반환)
        List<DeckData> allDecks = DeckSaveManager_Firebase.instance.GetAllDecks();

        if (allDecks == null || allDecks.Count == 0)
        {
            Debug.LogWarning("불러올 덱이 없습니다.");
            return;
        }

        // 3. 모든 덱에 대해 버튼 생성
        foreach (DeckData deck in allDecks)
        {
            GameObject buttonGO = Instantiate(deckButtonPrefab, deckListParent);
            PopupDeckButton deckButton = buttonGO.GetComponent<PopupDeckButton>();

            // 덱 버튼에 덱 정보(deck)와 클릭 시 호출할 함수(ShowDeckDetails)를 넘겨줍니다.
            deckButton.Setup(deck, ShowDeckDetails);
        }
    }

    /// <summary>
    /// 덱 목록의 버튼(PopupDeckButton)이 클릭되었을 때 호출되는 콜백 함수입니다.
    /// </summary>
    /// <param name="deck">클릭된 버튼이 가지고 있던 덱 데이터</param>
    private void ShowDeckDetails(DeckData deck)
    {
        currentlyViewedDeck = deck;

        deckNameText.text = deck.deckName;

        // 덱에 장착된 리더 스킨 이미지 표시
        if (leaderImage != null)
        {
            string skinId = deck.GetEquippedSkinId();
            var master = LeaderCardDisplay.LoadSkinData(skinId, deck.deckClass);
            if (master != null && master.skinSprite != null)
            {
                leaderImage.sprite = master.skinSprite;
            }
            else
            {
                string fallbackClass = !string.IsNullOrEmpty(deck.deckClass) ? deck.deckClass : "Gangzi";
                ShopManager.LoadProductImage($"Items/Skins/{fallbackClass}_Skin_0001.png", leaderImage);
            }
            leaderImage.gameObject.SetActive(true);
        }

        // 덱이 선택되었으므로 '확정' 버튼 활성화
        selectDeckButton.interactable = true;
    }

    /// <summary>
    /// '덱 선택' (확정) 버튼을 눌렀을 때 호출됩니다.
    /// </summary>
    private void ConfirmSelection()
    {
        if (currentlyViewedDeck != null)
        {
            Debug.Log($"'{currentlyViewedDeck.deckName}' 덱을 선택했습니다.");

            // 로비 매니저에게 덱이 확정되었음을 이벤트를 통해 알림
            OnDeckConfirmed?.Invoke(currentlyViewedDeck);

            // 팝업 닫기
            ClosePopup();
        }
    }

    /// <summary>
    /// '덱 편집' 버튼을 눌렀을 때 호출됩니다.
    /// </summary>
    private void GoToDeckEdit()
    {
        if (currentlyViewedDeck != null)
        {
            Debug.Log($"'{currentlyViewedDeck.deckName}' 덱을 편집하러 덱 빌더 씬으로 이동합니다.");

            // SceneLoader에 편집할 덱 정보를 저장하고 씬 로드를 요청합니다.
            // "DeckBuilderScene" 부분은 실제 덱 편성 씬의 이름으로 정확히 바꿔주세요.
            if (SceneLoader.instance != null)
            {
                SceneLoader.instance.LoadDeckEditorScene(currentlyViewedDeck, "DeckBuildingScene");
            }
            else
            {
                Debug.LogError("SceneLoader 인스턴스를 찾을 수 없습니다!");
            }
        }
    }
}
