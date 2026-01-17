using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

/// <summary>
/// [참고] 이 스크립트는 PlayerPrefs(내 컴퓨터 저장소)를 사용하는 방식입니다.
/// 현재 프로젝트는 Firebase(서버)를 사용하는 'DeckSaveManager_Firebase'를 주로 쓰고 있는 것 같습니다.
/// 이 파일은 서버 없이 로컬에서만 테스트할 때 유용합니다.
/// </summary>
public class DeckSaveManager : MonoBehaviour
{
    public static DeckSaveManager instance;

    private List<DeckData> allDecks;
    private const string SaveKey = "UserDecks"; // 저장할 때 쓸 열쇠 이름

    // 덱 변경 알림 이벤트
    public static event Action OnDecksChanged;

    private void Awake()
    {
        // 싱글톤 패턴
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
        }

        LoadDecks(); // 시작하자마자 불러오기
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// 저장소(PlayerPrefs)에서 덱 정보를 불러옵니다.
    /// </summary>
    private void LoadDecks()
    {
        if (PlayerPrefs.HasKey(SaveKey))
        {
            string json = PlayerPrefs.GetString(SaveKey);
            // JSON을 다시 객체로 변환
            DeckListWrapper wrapper = JsonUtility.FromJson<DeckListWrapper>(json);
            allDecks = wrapper.decks;
        }
        else
        {
            allDecks = new List<DeckData>();
        }
        Debug.Log($"{allDecks.Count}개의 덱을 불러왔습니다.");
    }

    /// <summary>
    /// 현재 덱 리스트를 저장소에 저장합니다.
    /// </summary>
    public void SaveDecks()
    {
        // 리스트를 포장지에 싸서 JSON(문자열)으로 변환
        DeckListWrapper wrapper = new DeckListWrapper { decks = allDecks };
        string json = JsonUtility.ToJson(wrapper);

        // 저장 및 디스크 쓰기
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
        Debug.Log("모든 덱이 저장되었습니다.");

        OnDecksChanged?.Invoke(); // 알림
    }

    /// <summary>
    /// 새 덱 생성 (이름 자동 부여: 새로운 덱 1, 새로운 덱 2...)
    /// </summary>
    public DeckData CreateNewDeck(string className)
    {
        const string defaultDeckNamePrefix = "새로운 덱 ";

        // LINQ를 사용해 기존 "새로운 덱 N" 중 가장 높은 숫자를 찾습니다.
        int maxDeckNumber = allDecks
            .Where(deck => deck.deckName.StartsWith(defaultDeckNamePrefix))
            .Select(deck => {
                string numberPart = deck.deckName.Substring(defaultDeckNamePrefix.Length);
                int.TryParse(numberPart, out int number);
                return number;
            })
            .DefaultIfEmpty(0)
            .Max();

        int newDeckNumber = maxDeckNumber + 1;

        // 새 덱 객체 생성
        DeckData newDeck = new DeckData(
            Guid.NewGuid().ToString(), // 랜덤한 고유 ID 생성
            $"{defaultDeckNamePrefix}{newDeckNumber}", // 이름
            className // 직업
        );

        allDecks.Add(newDeck);
        SaveDecks(); // 변경사항 저장
        return newDeck;
    }

    /// <summary>
    /// 기존 덱 업데이트
    /// </summary>
    public void UpdateDeck(DeckData updatedDeck)
    {
        // ID가 같은 덱을 찾아서 내용물 교체
        DeckData deckToUpdate = allDecks.FirstOrDefault(d => d.deckId == updatedDeck.deckId);
        if (deckToUpdate != null)
        {
            deckToUpdate.deckName = updatedDeck.deckName;
            deckToUpdate.deckClass = updatedDeck.deckClass;
            deckToUpdate.cardIds = updatedDeck.cardIds;
            SaveDecks();
        }
    }

    public List<DeckData> GetAllDecks()
    {
        return allDecks;
    }

    // JsonUtility가 List를 바로 저장 못해서 만든 포장지 클래스
    [System.Serializable]
    private class DeckListWrapper
    {
        public List<DeckData> decks;
    }
}