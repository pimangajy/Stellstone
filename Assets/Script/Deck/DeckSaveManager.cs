using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // FirstOrDefault 등을 사용하기 위해 필요
using System;     // Guid를 사용하기 위해 필요

/// <summary>
/// 사용자의 모든 덱 데이터를 PlayerPrefs에 저장하고 불러오는 싱글톤 관리자입니다.
/// </summary>
public class DeckSaveManager : MonoBehaviour
{
    public static DeckSaveManager instance;

    private List<DeckData> allDecks;
    private const string SaveKey = "UserDecks"; // PlayerPrefs에 저장될 때 사용될 키

    // 덱 목록에 변경이 생겼을 때 다른 UI에게 알려주기 위한 이벤트
    public static event Action OnDecksChanged;

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

        LoadDecks();
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
    /// PlayerPrefs에서 모든 덱 정보를 불러옵니다.
    /// </summary>
    private void LoadDecks()
    {
        if (PlayerPrefs.HasKey(SaveKey))
        {
            string json = PlayerPrefs.GetString(SaveKey);
            // JsonUtility는 리스트를 직접 변환하지 못하므로, Wrapper 클래스를 사용합니다.
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
    /// 현재 덱 목록 전체를 PlayerPrefs에 저장합니다.
    /// </summary>
    public void SaveDecks()
    {
        DeckListWrapper wrapper = new DeckListWrapper { decks = allDecks };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save(); // 변경사항을 즉시 디스크에 씁니다.
        Debug.Log("모든 덱이 저장되었습니다.");

        // 덱 목록에 변화가 생겼음을 모두에게 알립니다.
        OnDecksChanged?.Invoke();
    }

    /// <summary>
    /// 새로운 덱을 생성하고 목록에 추가합니다. "새로운 덱 n" 형식의 이름을 지능적으로 부여합니다.
    /// </summary>
    public DeckData CreateNewDeck(string className)
    {
        const string defaultDeckNamePrefix = "새로운 덱 ";

        // "새로운 덱 "으로 시작하는 이름을 가진 덱들 중에서 가장 큰 숫자를 찾습니다.
        int maxDeckNumber = allDecks
            .Where(deck => deck.deckName.StartsWith(defaultDeckNamePrefix))
            .Select(deck => {
                string numberPart = deck.deckName.Substring(defaultDeckNamePrefix.Length);
                int.TryParse(numberPart, out int number);
                return number;
            })
            .DefaultIfEmpty(0) // 해당하는 덱이 하나도 없으면 0을 기본값으로 사용
            .Max();

        int newDeckNumber = maxDeckNumber + 1; // 찾은 가장 큰 숫자에 1을 더합니다.

        DeckData newDeck = new DeckData(
            Guid.NewGuid().ToString(), // 고유한 ID 생성
            $"{defaultDeckNamePrefix}{newDeckNumber}", // 최종 이름 조합
            className
        );

        allDecks.Add(newDeck);
        SaveDecks();
        return newDeck;
    }

    /// <summary>
    /// 기존 덱의 정보를 업데이트합니다.
    /// </summary>
    public void UpdateDeck(DeckData updatedDeck)
    {
        DeckData deckToUpdate = allDecks.FirstOrDefault(d => d.deckId == updatedDeck.deckId);
        if (deckToUpdate != null)
        {
            // 리스트 내의 기존 객체 내용을 업데이트
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

    // JsonUtility가 List<T>를 직접 처리하지 못해서 사용하는 Helper 클래스
    [System.Serializable]
    private class DeckListWrapper
    {
        public List<DeckData> decks;
    }
}

