using System.Collections;
using System.Collections.Generic;
using System.Linq; // 리스트 데이터를 다루는 강력한 도구(LINQ)
using UnityEngine;
using System;
using TMPro;

/// <summary>
/// [현재 편집 중인 덱]을 관리하는 매니저입니다.
/// 오른쪽 화면의 '현재 덱 리스트'에 카드를 추가/제거하고, 규칙(30장 제한 등)을 검사합니다.
/// </summary>
public class DeckManager : MonoBehaviour
{
    // 싱글톤 패턴: 어디서든 DeckManager.instance 로 접근 가능하게 함
    public static DeckManager instance;

    // 현재 덱의 카드 구성이 변경될 때 발생하는 이벤트 (컬렉션 잔여 수량 갱신용)
    public static event Action OnCurrentDeckCardsChanged;
    // 덱 이름이 변경될 때 발생하는 이벤트 (왼쪽 덱 목록 실시간 반영용)
    public static event Action<DeckData, string> OnDeckNameChanged;

    [Header("덱 규칙 설정")]
    [SerializeField] private int maxDeckSize = 30; // 덱 최대 장수 제한

    [Header("사이드 덱 설정")]
    private int maxSideDeckSize = 5;
    public bool isEditingSideDeck = false; // 현재 사이드 덱 편집 중인지 여부

    [Header("UI 연결")]
    public TMP_Text deckName;
    public Transform mainDeckListParent; // 카드 목록이 표시될 UI 부모 (Content)
    public Transform sideDeckListParent; // 카드 목록이 표시될 UI 부모 (Content)
    public GameObject deckCardPrefab; // 목록에 추가될 카드 줄(Item) 프리팹

    [Header("사이드 덱 전환 UI")]
    [Tooltip("Deck View 배경 이미지 (스프라이트 교체용)")]
    public UnityEngine.UI.Image deckViewImage;
    [Tooltip("메인 덱 배경 스프라이트 (Window_Pink_57x81)")]
    public Sprite mainDeckSprite;
    [Tooltip("사이드 덱 배경 스프라이트 (Window_Green_47x48)")]
    public Sprite sideDeckSprite;
    [Tooltip("사이드 덱 전환 버튼 텍스트 (OpenSideDeckButton 안의 텍스트)")]
    public TMP_Text sideDeckButtonText;
    [Tooltip("Deck View의 ScrollRect")]
    public UnityEngine.UI.ScrollRect deckViewScrollRect;

    [Header("덱 카드 장수 표시 텍스트")]
    [Tooltip("단일 덱 카드 장수 표시 텍스트 (사이드 덱 닫힘: 14/30, 사이드 덱 열림: 3/5)")]
    public TMP_Text deckCountText;

    [Tooltip("개별 텍스트 사용 시 (선택 사항)")]
    public TMP_Text mainDeckCountText;
    public TMP_Text sideDeckCountText;

    // 선택한 덱의 직업 (예: "Mage", "Warrior"). LINQ에서 사용됩니다.
    private string selectedClass = "";
    public string SelectedClass => selectedClass;

    // 현재 덱에 포함된 실제 카드 데이터들의 리스트 (장바구니)
    private List<CardData> currentDeck = new List<CardData>();

    // 현재 편집 중인 사이드 덱 장바구니
    private List<CardData> currentSideDeck = new List<CardData>();

    // 현재 편집 중인 덱의 정보 (이름, ID 등)
    private DeckData currentlyEditingDeck;
    public DeckData CurrentlyEditingDeck => currentlyEditingDeck;

    // 현재 덱에 장착될 선택된 스킨 ID
    public string selectedSkinId { get; private set; } = "";

    // [최적화] 덱 슬롯 UI 재활용 풀 (Destroy/Instantiate 반복 방지)
    private readonly List<DeckListItemDisplay> _mainDeckSlotPool = new List<DeckListItemDisplay>();
    private readonly List<DeckListItemDisplay> _sideDeckSlotPool = new List<DeckListItemDisplay>();

    void Awake()
    {
        // 싱글톤 초기화
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        InitSideDeckComponents();
        UpdateSideDeckVisuals();
        UpdateDeckCountUI();
    }

    /// <summary>
    /// 사이드 덱 전환에 필요한 UI 컴포넌트 및 스프라이트를 초기화/캐싱합니다.
    private bool _isSideDeckComponentsInitialized = false;

    /// <summary>
    /// 사이드 덱 전환에 필요한 UI 컴포넌트 및 스프라이트를 초기화/캐싱합니다.
    /// </summary>
    public void InitSideDeckComponents()
    {
        if (_isSideDeckComponentsInitialized) return;

        if (deckViewImage == null)
        {
            var deckViewObj = GameObject.Find("Canvas/Deck/Deck/Panel/Deck View");
            if (deckViewObj != null)
            {
                deckViewImage = deckViewObj.GetComponent<UnityEngine.UI.Image>();
                deckViewScrollRect = deckViewObj.GetComponent<UnityEngine.UI.ScrollRect>();
            }
        }

        if (sideDeckButtonText == null)
        {
            var btnObj = GameObject.Find("Canvas/Deck/Deck/Panel/Deck View/OpenSideDeckButton");
            if (btnObj != null)
            {
                sideDeckButtonText = btnObj.GetComponentInChildren<TMP_Text>(true);
            }
        }

        _isSideDeckComponentsInitialized = true;
    }

    /// <summary>
    /// 사이드 덱 편집 모드 여부에 맞춰 Deck View 스프라이트, 컨텐츠 활성화, 버튼 텍스트를 갱신합니다.
    /// </summary>
    public void UpdateSideDeckVisuals()
    {
        InitSideDeckComponents();

        // 1. Deck View 내의 메인덱/사이드덱 컨텐츠 활성화 전환
        if (mainDeckListParent != null)
        {
            mainDeckListParent.gameObject.SetActive(!isEditingSideDeck);
        }
        if (sideDeckListParent != null)
        {
            sideDeckListParent.gameObject.SetActive(isEditingSideDeck);
        }

        // 2. ScrollRect 컨텐츠 포인터 전환
        if (deckViewScrollRect != null)
        {
            deckViewScrollRect.content = isEditingSideDeck && sideDeckListParent != null
                ? (sideDeckListParent as RectTransform)
                : (mainDeckListParent as RectTransform);
        }

        // 3. Deck View 배경 이미지 스프라이트 전환 (메인: 핑크, 사이드: 그린)
        if (deckViewImage != null)
        {
            if (isEditingSideDeck && sideDeckSprite != null)
            {
                deckViewImage.sprite = sideDeckSprite;
            }
            else if (!isEditingSideDeck && mainDeckSprite != null)
            {
                deckViewImage.sprite = mainDeckSprite;
            }
        }

        // 4. 전환 버튼 텍스트 갱신
        if (sideDeckButtonText != null)
        {
            if (isEditingSideDeck)
            {
                sideDeckButtonText.text = $"★ 메인 덱으로 ({currentDeck.Count}/{maxDeckSize}) ★";
            }
            else
            {
                sideDeckButtonText.text = $"★ 사이드 덱 ({currentSideDeck.Count}/{maxSideDeckSize}) ★";
            }
        }

        UpdateDeckCountUI();
    }

    /// <summary>
    /// [새 덱 만들기] 새 덱의 기본 정보를 설정합니다.
    /// </summary>
    public void StartNewDeck(DeckData newDeck)
    {
        isEditingSideDeck = false;
        currentlyEditingDeck = newDeck;
        selectedClass = newDeck.deckClass; // 덱의 직업 설정
        selectedSkinId = newDeck.GetEquippedSkinId(); // 직업 기본 스킨 또는 기존 스킨 설정
        currentDeck.Clear(); // 장바구니 비우기
        currentSideDeck.Clear();

        if (deckName != null)
        {
            deckName.text = string.IsNullOrEmpty(newDeck.deckName) ? "새로운 덱" : newDeck.deckName;
        }

        UpdateSideDeckVisuals();
        UpdateDeckListUI();  // 화면 갱신

        // 스킨 목록이 열려 있었다면 카드 목록으로 자동 복귀
        DeckSkinSelectManager.Instance?.ShowCardList();

        Debug.Log($"'{newDeck.deckName}' 만들기를 시작합니다.");
    }

    // UI의 '사이드 덱 버튼'을 누르면 호출될 함수
    public void ToggleSideDeckEditing()
    {   
        // 현재 편집 중인 덱이 없다면(단순 열람 중이라면) 함수를 바로 종료합니다.
        if (currentlyEditingDeck == null)
        {
            Debug.LogWarning("현재 덱 편성 중이 아닙니다. 새 덱을 만들거나 기존 덱을 선택해 주세요.");
            return;
        }

        isEditingSideDeck = !isEditingSideDeck;
        Debug.Log(isEditingSideDeck ? "사이드 덱 편집 모드" : "메인 덱 편집 모드");

        UpdateSideDeckVisuals();
        UpdateDeckListUI();
    }

    /// <summary>
    /// [기존 덱 불러오기] 저장된 덱을 불러와서 편집을 시작합니다.
    /// </summary>
    public void LoadDeck(DeckData deckToLoad, List<CardData> mainCards, List<CardData> sideCards)
    {
        isEditingSideDeck = false;

        currentlyEditingDeck = deckToLoad;
        selectedClass = deckToLoad.deckClass;
        selectedSkinId = deckToLoad.GetEquippedSkinId(); // 기존 덱에 저장된 스킨(또는 기본 스킨) 불러오기
        // 기존 카드 리스트를 복사해서 장바구니에 담습니다.
        currentDeck = new List<CardData>(mainCards);
        currentSideDeck = new List<CardData>(sideCards);

        if (deckName != null)
        {
            deckName.text = deckToLoad.deckName;
        }

        UpdateSideDeckVisuals();
        UpdateDeckListUI();

        // 스킨 목록이 열려 있었다면 카드 목록으로 자동 복귀
        DeckSkinSelectManager.Instance?.ShowCardList();

        Debug.Log($"'{deckToLoad.deckName}' 덱을 불러왔습니다. (장착 스킨: {selectedSkinId})");
    }

    /// <summary>
    /// UI에서 리더 스킨을 선택했을 때 호출하여 현재 선택된 스킨 ID를 설정합니다.
    /// </summary>
    public void SelectSkin(string skinId)
    {
        selectedSkinId = skinId;
        if (currentlyEditingDeck != null)
        {
            currentlyEditingDeck.leaderSkinId = skinId;
        }
        Debug.Log($"[DeckManager] 리더 스킨 선택됨: {skinId}");
    }

    /// <summary>
    /// 현재 편집 중인 덱을 서버에서 삭제합니다. (임시 덱은 서버 삭제 요청 없이 메모리에서만 정리)
    /// </summary>
    public async void DeleteDeck()
    {
        if (currentlyEditingDeck == null) return;

        // 서버에 존재하는 덱(deckId가 있는 덱)만 서버 삭제 요청
        if (!string.IsNullOrEmpty(currentlyEditingDeck.deckId))
        {
            await DeckSaveManager_Firebase.instance.ServerDeleteDeck(currentlyEditingDeck.deckId);
        }

        // 데이터 초기화
        ClearCurrentEditingDeck();
    }

    /// <summary>
    /// [저장 버튼] 현재 장바구니(currentDeck) 상태와 선택된 스킨을 서버에 일괄 저장합니다.
    /// (새 덱 작성 중일 때 저장 버튼을 눌러야 비로소 서버에 덱이 생성/추가됩니다)
    /// </summary>
    public async void SaveCurrentDeck()
    {
        if (currentlyEditingDeck == null)
        {
            Debug.LogWarning("저장할 덱이 선택되지 않았습니다.");
            return;
        }

        if (currentSideDeck.Count > 0 && currentSideDeck.Count != 5)
        {
            Debug.LogWarning("사이드 덱을 5장 꽉 채워야 저장할 수 있습니다.");
            return;
        }

        // 1. 기존에 저장된 ID 목록을 싹 비웁니다.
        currentlyEditingDeck.cardIds.Clear();
        currentlyEditingDeck.sideDeckCardIds.Clear();
        currentlyEditingDeck.sideDeckFirstTurnCardIds.Clear();

        // 2. 메인 덱 30장 저장
        foreach (var card in currentDeck)
        {
            currentlyEditingDeck.cardIds.Add(card.cardID);
        }

        // 3. 사이드 덱 5장 전체 저장
        foreach (var card in currentSideDeck)
        {
            currentlyEditingDeck.sideDeckCardIds.Add(card.cardID);
        }

        // 4. 사이드 덱에 들어온 순서대로 앞의 3장을 선공용으로 자동 지정
        int firstTurnCount = Mathf.Min(3, currentSideDeck.Count);
        for (int i = 0; i < firstTurnCount; i++)
        {
            currentlyEditingDeck.sideDeckFirstTurnCardIds.Add(currentSideDeck[i].cardID);
        }

        // 5. 선택된 스킨 정보 저장
        if (!string.IsNullOrEmpty(selectedSkinId))
        {
            currentlyEditingDeck.leaderSkinId = selectedSkinId;
        }
        else if (string.IsNullOrEmpty(currentlyEditingDeck.leaderSkinId))
        {
            currentlyEditingDeck.leaderSkinId = currentlyEditingDeck.GetEquippedSkinId();
        }

        // 6. [신규 덱 생성 처리]
        // 서버 ID가 없는 새 덱(Draft)인 경우, 유저가 저장을 누른 지금 시점에 비로소 서버에 생성을 요청합니다!
        if (string.IsNullOrEmpty(currentlyEditingDeck.deckId))
        {
            DeckData createdDeck = await DeckSaveManager_Firebase.instance.ServerCreateNewDeck(currentlyEditingDeck.deckClass);
            if (createdDeck == null)
            {
                Debug.LogError("[DeckManager] 서버에 새 덱을 생성하지 못했습니다.");
                return;
            }
            currentlyEditingDeck.deckId = createdDeck.deckId;

            // 유저가 이름을 변경하지 않은 상태면 서버가 생성해 준 기본 덱 이름 사용
            if (string.IsNullOrEmpty(currentlyEditingDeck.deckName) || currentlyEditingDeck.deckName == "새로운 덱")
            {
                currentlyEditingDeck.deckName = createdDeck.deckName;
            }
        }

        // 서버로 데이터 덮어쓰기 요청 전송
        await DeckSaveManager_Firebase.instance.ServerUpdateDeck(currentlyEditingDeck);
        Debug.Log($"'{currentlyEditingDeck.deckName}' 덱(메인+사이드 / 스킨: {currentlyEditingDeck.leaderSkinId})이 저장되었습니다.");

        // 저장 완료 후 편집 중인 덱 비우기 및 초기화
        ClearCurrentEditingDeck();
    }

    /// <summary>
    /// [닫기 버튼] 현재 덱 편집을 종료하고 목록과 편집 상태를 초기화합니다.
    /// (저장하지 않은 새 덱은 서버에 등록되지 않고 깔끔하게 소멸합니다)
    /// </summary>
    public void CloseDeckEditing()
    {
        if (currentlyEditingDeck != null)
        {
            Debug.Log($"'{currentlyEditingDeck.deckName}' 덱 편집을 닫습니다 (저장되지 않은 임시 정보 폐기).");
        }
        ClearCurrentEditingDeck();
    }

    /// <summary>
    /// 현재 편집 중인 덱 데이터와 장바구니, 화면을 모두 초기화합니다.
    /// </summary>
    public void ClearCurrentEditingDeck()
    {
        currentDeck.Clear(); // 장바구니 비우기
        currentSideDeck.Clear();
        currentlyEditingDeck = null;
        selectedClass = null;
        selectedSkinId = "";
        isEditingSideDeck = false;

        if (deckName != null)
        {
            deckName.text = "";
        }

        UpdateSideDeckVisuals();
        UpdateDeckListUI();

        // 스킨 목록이 열려 있었다면 카드 목록으로 자동 복귀
        DeckSkinSelectManager.Instance?.ShowCardList();
    }

    /// <summary>
    /// 덱에 카드를 한 장 추가합니다. (왼쪽 리스트에서 클릭 시 호출)
    /// </summary>
    public void AddCard(CardData cardToAdd)
    {
        // 덱을 만들고 있는 상태가 아니면 무시
        if (currentlyEditingDeck == null)
        {
            Debug.LogWarning("먼저 덱을 선택하거나 새로 만들어야 카드를 추가할 수 있습니다.");
            return;
        }

        // 카드를 넣을 수 있는지 규칙 검사 (30장 꽉 찼는지, 직업이 맞는지 등)
        if (!IsCardAddable(cardToAdd))
        {
            return; // 추가 불가능하면 여기서 함수 종료
        }

        // 모드에 따라 알맞은 덱에 추가
        if (isEditingSideDeck)
        {
            currentSideDeck.Add(cardToAdd);
        }
        else
        {
            currentDeck.Add(cardToAdd);
        }
        // 화면 갱신
        UpdateDeckListUI();
    }

    /// <summary>
    /// 덱에서 카드를 한 장 뺍니다. (오른쪽 리스트에서 클릭 시 호출)
    /// </summary>
    public void RemoveCard(CardData cardToRemove)
    {
        if (isEditingSideDeck)
        {
            // 사이드 덱에서 카드 제거 (가장 먼저 넣은 해당 카드를 뺌)
            CardData cardInSideDeck = currentSideDeck.FirstOrDefault(c => c.cardID == cardToRemove.cardID);
            if (cardInSideDeck != null)
            {
                currentSideDeck.Remove(cardInSideDeck);
                UpdateDeckListUI();
            }
        }
        else
        {
            // 메인 덱에서 카드 제거 (기존 로직 유지)
            CardData cardInDeck = currentDeck.FirstOrDefault(c => c.cardID == cardToRemove.cardID);
            if (cardInDeck != null)
            {
                currentDeck.Remove(cardInDeck);
                UpdateDeckListUI();
            }
        }
    }

    /// <summary>
    /// [규칙 검사기] 이 카드를 덱에 넣을 수 있는지 확인합니다.
    /// </summary>
    private bool IsCardAddable(CardData card)
    {
        // 1. 공통 규칙: 직업 제한 확인 (내 직업이거나 중립(강지) 카드여야 함)
        string cardMemberStr = card.cardClass.ToString();
        if (cardMemberStr != selectedClass && card.cardClass != CardClass.Gangzi)
        {
            Debug.LogWarning($"'{card.cardName}' 카드는 '{selectedClass}' 덱에 추가할 수 없습니다.");
            return false;
        }

        // 2. 현재 편집 모드(메인/사이드)에 따른 최대 장수 및 특수 규칙 검사
        if (isEditingSideDeck)
        {
            // 사이드 덱 검사
            if (currentSideDeck.Count >= maxSideDeckSize) // 5장 제한
            {
                Debug.LogWarning("사이드 덱이 가득 찼습니다. (최대 5장)");
                return false;
            }

            if (card.cardType != CardType.하수인 && card.cardType != CardType.주문)
            {
                Debug.LogWarning("사이드 덱에는 하수인과 주문 카드만 넣을 수 있습니다.");
                return false;
            }
        }
        else
        {
            // 메인 덱 검사
            if (currentDeck.Count >= maxDeckSize)
            {
                Debug.LogWarning("메인 덱이 가득 찼습니다.");
                return false;
            }
        }

        // 3. [핵심 수정] 메인 덱과 사이드 덱의 동일 카드 장수를 합산
        int mainDeckCount = currentDeck.Count(c => c.cardID == card.cardID);
        int sideDeckCount = currentSideDeck.Count(c => c.cardID == card.cardID);
        int totalSameCardCount = mainDeckCount + sideDeckCount;

        // 4. 유저 실제 보유 수량 검사 (미보유 카드 또는 보유 수량 초과 투입 차단)
        int ownedCount = GetOwnedCardCount(card);
        if (ownedCount <= 0)
        {
            Debug.LogWarning($"보유하지 않은 카드('{card.cardName}')는 덱에 추가할 수 없습니다.");
            return false;
        }
        if (totalSameCardCount >= ownedCount)
        {
            Debug.LogWarning($"보유한 '{card.cardName}' 카드를 모두 덱에 넣었습니다. (보유: {ownedCount}장)");
            return false;
        }

        // 5. 합산된 장수로 최대 2장 제한 검사 (전설 카드 포함 동일 카드는 최대 2장)
        if (totalSameCardCount >= 2)
        {
            Debug.LogWarning("동일한 카드는 메인 덱과 사이드 덱을 합쳐 최대 두 장까지만 넣을 수 있습니다.");
            return false;
        }

        return true; // 모든 검사를 통과했으므로 추가 가능
    }

    /// <summary>
    /// 덱 이름을 변경합니다. (InputFieldController에서 호출)
    /// </summary>
    public void UpdateDeckname(string name)
    {
        if (currentlyEditingDeck != null)
        {
            currentlyEditingDeck.deckName = name;
        }

        if (deckName != null)
        {
            deckName.text = name;
        }

        OnDeckNameChanged?.Invoke(currentlyEditingDeck, name);
    }

    /// <summary>
    /// [화면 갱신] 현재 덱 리스트(오른쪽) UI를 슬롯 재활용 풀을 사용하여 다시 그립니다.
    /// (카드를 추가/제거할 때마다 30개 전체를 Destroy/Instantiate하지 않고 재사용합니다)
    /// </summary>
    private void UpdateDeckListUI()
    {
        // -------------------------------------------------------------
        // 1. 사이드 덱 리스트 갱신 (슬롯 풀 재활용)
        // -------------------------------------------------------------
        int sideCount = currentSideDeck.Count;
        while (_sideDeckSlotPool.Count < sideCount)
        {
            GameObject newDeckCardUI = Instantiate(deckCardPrefab, sideDeckListParent);
            DeckListItemDisplay itemDisplay = newDeckCardUI.GetComponent<ICardDataHolder>() as DeckListItemDisplay;
            CardInteraction interaction = newDeckCardUI.GetComponent<CardInteraction>();
            if (interaction != null)
            {
                interaction.location = CardInteraction.CardLocation.Deck;
            }
            _sideDeckSlotPool.Add(itemDisplay);
        }

        for (int i = 0; i < sideCount; i++)
        {
            DeckListItemDisplay itemDisplay = _sideDeckSlotPool[i];
            if (itemDisplay != null)
            {
                if (!itemDisplay.gameObject.activeSelf) itemDisplay.gameObject.SetActive(true);
                itemDisplay.Setup(currentSideDeck[i], 1);
            }
        }

        for (int i = sideCount; i < _sideDeckSlotPool.Count; i++)
        {
            if (_sideDeckSlotPool[i] != null && _sideDeckSlotPool[i].gameObject.activeSelf)
            {
                _sideDeckSlotPool[i].gameObject.SetActive(false);
            }
        }

        // -------------------------------------------------------------
        // 2. 메인 덱 리스트 갱신 (슬롯 풀 재활용)
        // -------------------------------------------------------------
        var groupedAndSortedDeck = currentDeck
            .GroupBy(card => card.cardID)
            .Select(group => new
            {
                Card = group.First(),
                Count = group.Count()
            })
            .OrderBy(item => item.Card.manaCost)
            .ThenBy(item => item.Card.cardName)
            .ToList();

        int mainGroupCount = groupedAndSortedDeck.Count;
        while (_mainDeckSlotPool.Count < mainGroupCount)
        {
            GameObject newDeckCardUI = Instantiate(deckCardPrefab, mainDeckListParent);
            DeckListItemDisplay itemDisplay = newDeckCardUI.GetComponent<ICardDataHolder>() as DeckListItemDisplay;
            CardInteraction interaction = newDeckCardUI.GetComponent<CardInteraction>();
            if (interaction != null)
            {
                interaction.location = CardInteraction.CardLocation.Deck;
            }
            _mainDeckSlotPool.Add(itemDisplay);
        }

        for (int i = 0; i < mainGroupCount; i++)
        {
            DeckListItemDisplay itemDisplay = _mainDeckSlotPool[i];
            if (itemDisplay != null)
            {
                if (!itemDisplay.gameObject.activeSelf) itemDisplay.gameObject.SetActive(true);
                itemDisplay.Setup(groupedAndSortedDeck[i].Card, groupedAndSortedDeck[i].Count);
            }
        }

        for (int i = mainGroupCount; i < _mainDeckSlotPool.Count; i++)
        {
            if (_mainDeckSlotPool[i] != null && _mainDeckSlotPool[i].gameObject.activeSelf)
            {
                _mainDeckSlotPool[i].gameObject.SetActive(false);
            }
        }

        // DeckPlus 버튼이 있다면 항상 맨 아래로 이동
        if (mainDeckListParent != null)
        {
            Transform deckPlus = mainDeckListParent.Find("DeckPlus");
            if (deckPlus != null)
            {
                deckPlus.SetAsLastSibling();
            }
        }

        // 3. 덱 카드 수 및 사이드 덱 버튼/비주얼 갱신 (예: 14/30, 3/5)
        UpdateSideDeckVisuals();

        // 4. 덱 카드 구성 변경 알림 (컬렉션 목록의 카드 잔여 수량 갱신)
        OnCurrentDeckCardsChanged?.Invoke();
    }

    /// <summary>
    /// 현재 덱(메인 + 사이드)에 포함된 특정 카드의 총 장수를 반환합니다.
    /// </summary>
    public int GetCardCountInDeck(string cardId)
    {
        int mainCount = currentDeck.Count(c => c.cardID == cardId);
        int sideCount = currentSideDeck.Count(c => c.cardID == cardId);
        return mainCount + sideCount;
    }

    /// <summary>
    /// 현재 로그인된 유저의 특정 카드 실제 보유 수량을 반환합니다.
    /// 로그인 정보가 없거나 테스트 환경인 경우 기본 최대치(전설 1, 일반 2)를 반환합니다.
    /// </summary>
    public int GetOwnedCardCount(CardData card)
    {
        if (card == null) return 0;

        UserData user = GameClient.Instance?.CurrentUser ?? SinginManager.CurrentUserData;
        if (user != null && user.ownedCards != null)
        {
            if (user.ownedCards.TryGetValue(card.cardID, out int count))
            {
                return count;
            }
            return 0; // 유저 데이터가 존재하는데 목록에 없으면 0장 보유
        }

        // 로그인 데이터가 없는 테스트 환경(Fallback)인 경우 기본 룰 상의 수량 반환 (최대 2장)
        return 2;
    }

    /// <summary>
    /// 유저 보유량과 게임 규칙(동일 카드 최대 2장)을 종합하여 덱에 넣을 수 있는 최대치를 반환합니다.
    /// </summary>
    public int GetMaxUsableCardCount(CardData card)
    {
        if (card == null) return 0;
        int ruleMax = 2; // 전설 카드를 포함하여 동일 카드 최대 2장
        int owned = GetOwnedCardCount(card);
        return Mathf.Min(owned, ruleMax);
    }

    /// <summary>
    /// 현재 카드의 화면 표시 수량을 반환합니다.
    /// - 덱 편집 상태가 아닐 때(currentlyEditingDeck == null): 실제 계정 보유 수량 전체(owned)를 반환 (3장 이상이면 3장 이상 그대로 표시)
    /// - 덱 편집 상태일 때(currentlyEditingDeck != null): 덱 투입 가능 규칙(최대 2장) 내에서 현재 덱에 넣고 남은 잔여 수량을 반환
    /// </summary>
    public int GetRemainingCardCount(CardData card)
    {
        if (card == null) return 0;
        int owned = GetOwnedCardCount(card);

        // 1. 덱 편집 중이 아닐 때는 2장 제한 없이 실제 보유 수량 전체를 반환 (X 3, X 4 등)
        if (currentlyEditingDeck == null)
        {
            return owned;
        }

        // 2. 덱 편집 상태일 때는 룰상 최대치(최대 2장)에서 덱에 투입된 매수를 뺀 잔여 수량 반환
        int ruleMax = 2; // 전설 카드를 포함하여 동일 카드 최대 2장
        int maxUsable = Mathf.Min(owned, ruleMax);
        return Mathf.Max(0, maxUsable - GetCardCountInDeck(card.cardID));
    }

    /// <summary>
    /// 메인 덱 및 사이드 덱의 카드 장수 텍스트를 갱신합니다.
    /// 단일 텍스트(deckCountText) 사용 시 사이드 덱 모드에 따라 14/30 또는 3/5로 자동 전환됩니다.
    /// </summary>
    public void UpdateDeckCountUI()
    {
        // 1. 단일 통합 텍스트 (사이드 덱 열림: 3/5, 닫힘: 14/30)
        if (deckCountText != null)
        {
            if (isEditingSideDeck)
            {
                deckCountText.text = $"{currentSideDeck.Count}/{maxSideDeckSize}";
            }
            else
            {
                deckCountText.text = $"{currentDeck.Count}/{maxDeckSize}";
            }
        }

        // 2. 개별 텍스트가 연결되어 있는 경우에도 지원
        if (mainDeckCountText != null)
        {
            mainDeckCountText.text = $"{currentDeck.Count}/{maxDeckSize}";
        }

        if (sideDeckCountText != null)
        {
            sideDeckCountText.text = $"{currentSideDeck.Count}/{maxSideDeckSize}";
        }
    }
}