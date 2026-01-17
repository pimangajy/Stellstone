using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 왼쪽의 '저장된 덱 목록'을 관리하는 매니저입니다.
/// 덱이 추가되거나 삭제되면 목록을 새로고침해서 보여줍니다.
/// </summary>
public class DeckListUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private GameObject deckButtonPrefab; // 덱 버튼 프리팹 (복제할 원본)
    [SerializeField] private Transform deckListParent;    // 버튼들이 들어갈 부모 위치

    // DeckBuilder 스크립트와 연결 (덱 버튼을 누르면 DeckBuilder에게 알려줘야 하니까요)
    [SerializeField] private DeckBuilder deckBuilder;

    private void Awake()
    {
        // "덱 정보가 변경됐다"는 이벤트(OnDecksChanged)에 내 함수(UpdateDeckList)를 등록합니다.
        // 이제 덱을 저장하거나 삭제하면 자동으로 목록이 갱신됩니다.
        DeckSaveManager.OnDecksChanged += UpdateDeckList;
        DeckSaveManager_Firebase.OnDecksChanged += UpdateDeckList;
    }

    private void OnDisable()
    {
        // 이벤트 등록을 해제합니다. (매우 중요: 안 하면 에러 발생 가능)
        DeckSaveManager.OnDecksChanged -= UpdateDeckList;
        DeckSaveManager_Firebase.OnDecksChanged -= UpdateDeckList;
    }

    private void Start()
    {
        // 게임 시작하자마자 덱 목록을 한번 그려줍니다.
        UpdateDeckList();
    }

    /// <summary>
    /// 저장된 덱 리스트를 가져와서 UI 버튼들을 다시 만듭니다.
    /// </summary>
    private void UpdateDeckList()
    {
        // 1. 청소: 기존에 있던 버튼들을 싹 지웁니다.
        foreach (Transform child in deckListParent)
        {
            // "DeckPlus"라는 이름의 버튼(새 덱 만들기 버튼)은 지우지 않고 남겨둡니다.
            if (child.gameObject.name != "DeckPlus")
            {
                Destroy(child.gameObject);
            }
        }

        // 2. 데이터 가져오기: 저장 매니저에게 "모든 덱 내놔"라고 합니다.
        List<DeckData> allDecks = DeckSaveManager_Firebase.instance.GetAllDecks();

        // 3. 생성: 덱 개수만큼 버튼을 만듭니다.
        foreach (DeckData deck in allDecks)
        {
            // 버튼 프리팹 복제
            GameObject buttonGO = Instantiate(deckButtonPrefab, deckListParent);
            DeckButton deckButton = buttonGO.GetComponent<DeckButton>();

            // 버튼 설정: 이 버튼은 어떤 덱이고, 누르면 무슨 함수(LoadDeckForEditing)를 실행할지 알려줍니다.
            deckButton.Setup(deck, deckBuilder.LoadDeckForEditing);
        }
    }
}