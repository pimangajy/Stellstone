using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 왼쪽 덱 목록 UI의 생성과 업데이트를 관리합니다.
/// DeckSaveManager와 DeckBuilder 사이에서 UI 관련 처리를 중개합니다.
/// </summary>
public class DeckListUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private GameObject deckButtonPrefab; // 덱 버튼 프리팹
    [SerializeField] private Transform deckListParent;    // 덱 버튼들이 생성될 부모 Transform

    // [참조 연결]
    // 인스펙터에서 DeckBuilder가 있는 게임오브젝트를 연결해주세요.
    [SerializeField] private DeckBuilder deckBuilder;

    private void Awake()
    {
        // DeckSaveManager의 이벤트에 구독 신청
        DeckSaveManager.OnDecksChanged += UpdateDeckList;
        DeckSaveManager_Firebase.OnDecksChanged += UpdateDeckList;
    }

    private void OnDisable()
    {
        // DeckSaveManager의 이벤트 구독 해지 (메모리 누수 방지)
        DeckSaveManager.OnDecksChanged -= UpdateDeckList;
        DeckSaveManager_Firebase.OnDecksChanged -= UpdateDeckList;
    }

    private void Start()
    {
        // 처음 시작할 때 한 번 덱 목록을 그려줍니다.
        UpdateDeckList();
    }

    /// <summary>
    /// 저장된 모든 덱을 가져와 UI 목록을 새로 고칩니다.
    /// </summary>
    private void UpdateDeckList()
    {
        // 1. 기존 버튼들을 모두 삭제
        foreach (Transform child in deckListParent)
        {
            Destroy(child.gameObject);
        }

        // 2. DeckSaveManager로부터 모든 덱 데이터를 가져옴
        List<DeckData> allDecks = DeckSaveManager_Firebase.instance.GetAllDecks();

        // 3. 각 덱 데이터에 대해 버튼을 생성
        foreach (DeckData deck in allDecks)
        {
            GameObject buttonGO = Instantiate(deckButtonPrefab, deckListParent);
            DeckButton deckButton = buttonGO.GetComponent<DeckButton>();

            // 버튼에 덱 정보를 설정하고, 클릭했을 때 DeckBuilder의 LoadDeckForEditing 함수를 호출하도록 연결
            deckButton.Setup(deck, deckBuilder.LoadDeckForEditing);
        }
    }
}

