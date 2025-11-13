using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    public static DeckManager instance; // 다른 스크립트에서 쉽게 접근하기 위한 싱글톤 인스턴스

    [Header("덱 규칙 설정")]
    [SerializeField] private int maxDeckSize = 30; // 덱에 들어갈 수 있는 최대 카드 수

    [Header("UI 연결")]
    [Tooltip("오른쪽 덱 목록 UI들이 생성될 부모 Transform")]
    public Transform deckListParent;
    [Tooltip("오른쪽 덱 목록에 사용될 카드 UI 프리팹")]
    public GameObject deckCardPrefab; // 아직 없다면 다음 단계에서 만들 것입니다.
    [Tooltip("덱의 설정 버튼")]
    public TextMeshProUGUI deckName;

    private string selectedClass = ""; // 현재 만들고 있는 덱의 직업
    private List<CardDataFirebase> currentDeck = new List<CardDataFirebase>(); // 현재 덱에 포함된 카드들의 리스트

    // 현재 편집 중인 덱의 원본 데이터를 저장합니다.
    private DeckData currentlyEditingDeck;

    private void Awake()
    {
        // --- 씬 싱글톤 패턴 구현 ---
        if (instance != null && instance != this)
        {
            // 이미 이 씬에 SinginManager가 있다면, 새로 생긴 것은 파괴
            Destroy(gameObject);
        }
        else
        {
            // 이 씬의 유일한 인스턴스로 등록
            instance = this;
        }
        // --- DontDestroyOnLoad(gameObject)는 사용하지 않음 ---
    }

    private void OnDestroy()
    {
        // 씬이 변경되거나 이 오브젝트가 파괴될 때,
        // static 참조를 스스로 정리(null로 만듦)합니다.
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// 새로운 덱 빌드를 시작합니다.
    /// 이제 문자열 대신 DeckData 객체를 직접 받습니다.
    /// </summary>
    public void StartNewDeck(DeckData newDeck)
    {
        // 이제부터 이 덱을 편집한다고 기억합니다.
        currentlyEditingDeck = newDeck;
        selectedClass = newDeck.deckClass;
        currentDeck.Clear();
        UpdateDeckListUI();
        Debug.Log($"'{newDeck.deckName}' 만들기를 시작합니다.");
    }

    /// <summary>
    /// 현재 편집 중인 덱의 카드 목록을 DeckSaveManager에 저장합니다.
    /// 화면 좌측 상단의 '뒤로 가기' 또는 '완료' 버튼에 연결해야 합니다.
    /// </summary>
    public async void SaveCurrentDeck()
    {
        // 편집 중인 덱이 없으면 아무것도 하지 않습니다.
        if (currentlyEditingDeck == null)
        {
            Debug.LogWarning("저장할 덱이 선택되지 않았습니다.");
            return;
        }

        // 1. 기존 카드 ID 목록을 비웁니다.
        currentlyEditingDeck.cardIds.Clear();

        // 2. 현재 DeckManager가 들고 있는 카드들의 ID만 추출하여 다시 채웁니다.
        foreach (var card in currentDeck)
        {
            currentlyEditingDeck.cardIds.Add(card.CardID);
        }

        // 3. DeckSaveManager에 덱 정보 업데이트를 요청합니다.
        await DeckSaveManager_Firebase.instance.ServerUpdateDeck(currentlyEditingDeck);

        Debug.Log($"'{currentlyEditingDeck.deckName}' 덱이 저장되었습니다.");
    }

    public async void DeleteDeck()
    {
        if (currentlyEditingDeck == null)
        {
            Debug.LogWarning("삭제할 덱이 선택되지 않았습니다.");
            return;
        }

        await DeckSaveManager_Firebase.instance.ServerDeleteDeck(currentlyEditingDeck.deckId);



        Debug.Log($"'{currentlyEditingDeck.deckName}' 덱이 삭제되었습니다.");
    }

    /// <summary>
    /// 덱의 이름을 업데이트합니다.
    /// </summary>
    public async void UpdateDeckname(string name)
    {
        currentlyEditingDeck.deckName = name;
        await DeckSaveManager_Firebase.instance.UpdateDeck(currentlyEditingDeck);
    }

    /// <summary>
    /// 저장된 덱 데이터를 불러와 편집 상태로 만듭니다.
    /// </summary>
    public void LoadDeck(DeckData deckToLoad, List<CardDataFirebase> cards)
    {
        currentlyEditingDeck = deckToLoad;
        selectedClass = deckToLoad.deckClass;
        deckName.text = deckToLoad.deckName;
        currentDeck = new List<CardDataFirebase>(cards); // 카드 리스트를 통째로 교체
        UpdateDeckListUI();
        Debug.Log($"'{deckToLoad.deckName}' 덱을 불러왔습니다.");
    }

    /// <summary>
    /// 중앙 카드 목록에서 카드를 우클릭했을 때 호출되어 덱에 카드를 추가합니다.
    /// </summary>
    public void AddCard(CardDataFirebase cardToAdd)
    {
        // 직업이 선택되지 않았다면 카드를 추가할 수 없습니다.
        if (string.IsNullOrEmpty(selectedClass) || selectedClass == "전체")
        {
            Debug.LogWarning("먼저 직업을 선택해야 카드를 추가할 수 있습니다.");
            return;
        }

        // 덱 규칙(카드 수, 직업, 동일 카드 개수)을 검사해서 추가할 수 없으면 함수를 종료합니다.
        if (!IsCardAddable(cardToAdd))
        {
            return;
        }

        currentDeck.Add(cardToAdd);
        Debug.Log($"'{cardToAdd.name}' 카드를 덱에 추가했습니다. (현재: {currentDeck.Count}/{maxDeckSize})");

        UpdateDeckListUI();
    }

    /// <summary>
    /// 오른쪽 덱 목록의 카드를 우클릭했을 때 호출되어 덱에서 카드를 제거합니다.
    /// </summary>
    public void RemoveCard(CardDataFirebase cardToRemove)
    {
        // 덱에 제거할 카드가 실제로 있는지 확인합니다.
        CardDataFirebase cardInDeck = currentDeck.FirstOrDefault(c => c.CardID == cardToRemove.CardID);
        if (cardInDeck != null)
        {
            currentDeck.Remove(cardInDeck);
            Debug.Log($"'{cardToRemove.name}' 카드를 덱에서 제거했습니다. (현재: {currentDeck.Count}/{maxDeckSize})");
            UpdateDeckListUI();
        }
    }

    /// <summary>
    /// 특정 카드를 덱에 추가할 수 있는지 모든 규칙을 검사합니다.
    /// </summary>
    private bool IsCardAddable(CardDataFirebase card)
    {
        // 1. 덱이 가득 찼는지 검사
        if (currentDeck.Count >= maxDeckSize)
        {
            Debug.LogWarning("덱이 가득 찼습니다. 더 이상 카드를 추가할 수 없습니다.");
            return false;
        }

        // 2. 직업 제한 검사 (해당 직업의 카드거나, 중립('Gangzi') 카드여야 함)
        if (card.member != selectedClass && card.member != "Gangzi")
        {
            Debug.LogWarning($"'{card.name}' 카드는 '{selectedClass}' 덱에 추가할 수 없는 카드입니다.");
            return false;
        }

        // 3. 동일 카드 개수 제한 검사
        int sameCardCount = currentDeck.Count(c => c.CardID == card.CardID);

        if (card.rarity == "legendary") // 레어도가 '전설'일 경우
        {
            if (sameCardCount >= 1)
            {
                Debug.LogWarning($"전설 카드는 덱에 한 장만 넣을 수 있습니다.");
                return false;
            }
        }
        else // 전설이 아닌 모든 카드
        {
            if (sameCardCount >= 2)
            {
                Debug.LogWarning($"'{card.name}' 카드는 덱에 최대 두 장까지만 넣을 수 있습니다.");
                return false;
            }
        }

        return true; // 모든 규칙을 통과하면 true 반환
    }

    /// <summary>
    /// 현재 덱 리스트(currentDeck)의 상태를 실제 UI에 반영합니다.
    /// </summary>
    private void UpdateDeckListUI()
    {
        // 1. 기존에 생성된 덱 목록 UI를 모두 삭제합니다.
        foreach (Transform child in deckListParent)
        {
            Destroy(child.gameObject);
        }

        // 2. 덱의 카드들을 그룹화하고 정렬합니다.
        // GroupBy: 동일한 카드 ID를 가진 카드들을 하나로 묶습니다.
        // OrderBy: 코스트 순 -> 이름 순으로 정렬합니다.
        var groupedAndSortedDeck = currentDeck
            .GroupBy(card => card.CardID)
            .Select(group => new
            {
                Card = group.First(), // 그룹의 첫 번째 카드를 대표 카드로 사용
                Count = group.Count()   // 그룹에 포함된 카드 개수
            })
            .OrderBy(item => item.Card.cost)
            .ThenBy(item => item.Card.name);

        // 3. 정렬된 리스트를 바탕으로 UI 오브젝트를 생성합니다.
        foreach (var item in groupedAndSortedDeck)
        {
            GameObject newDeckCardUI = Instantiate(deckCardPrefab, deckListParent);

            // 생성된 UI 오브젝트에 카드 정보와 개수를 전달하여 화면에 표시합니다.
            DeckListItemDisplay itemDisplay = newDeckCardUI.GetComponent<DeckListItemDisplay>();
            if (itemDisplay != null)
            {
                itemDisplay.Setup(item.Card, item.Count);
            }

            // 우클릭으로 덱에서 카드를 제거할 수 있도록 CardInteraction 스크립트도 설정합니다.
            CardInteraction interaction = newDeckCardUI.GetComponent<CardInteraction>();
            if (interaction != null)
            {
                interaction.location = CardInteraction.CardLocation.Deck;
            }
        }

        // TODO: 덱의 총 카드 수를 표시하는 텍스트 UI 업데이트 로직 추가
    }
}
