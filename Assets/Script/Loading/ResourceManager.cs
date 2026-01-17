using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance;

    // 모든 카드 데이터를 저장할 딕셔너리 (검색 속도 빠름)
    private Dictionary<string, CardData> _cardDatabase = new Dictionary<string, CardData>();

    void Awake()
    {
        // --- (수정) 안전한 싱글톤 패턴 적용 ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return; // 중복이면 초기화 로직 실행 안 함
        }else
        {
            DontDestroyOnLoad(gameObject);
        }

        Instance = this;
    }

    public void LoadAllCards()
    {
        // "Resources/Cards" 폴더에 있는 모든 ScriptableObject를 불러옵니다.
        CardData[] allCards = Resources.LoadAll<CardData>("CardData");

        foreach (var card in allCards)
        {
            if (!_cardDatabase.ContainsKey(card.cardID))
            {
                _cardDatabase.Add(card.cardID, card);
            }
            else
            {
                Debug.LogWarning($"중복된 카드 ID 발견: {card.cardID}");
            }
        }
        Debug.Log($"카드 데이터 로드 완료: {_cardDatabase.Count}장");
    }

    public CardData GetCardData(string cardId)
    {
        if (_cardDatabase.TryGetValue(cardId, out CardData data))
        {
            return data;
        }

        Debug.LogError($"카드를 찾을 수 없음: {cardId}");
        return null;
    }

    public List<CardData> GetAllCards()
    {
        // 딕셔너리의 '값(Value)'들만 모아서 리스트로 변환해 반환
        return _cardDatabase.Values.ToList();
    }
}
