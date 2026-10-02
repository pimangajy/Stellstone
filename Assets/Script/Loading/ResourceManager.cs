using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance;

    // 모든 카드 데이터를 저장하는 딕셔너리 (검색 속도 최적화)
    private Dictionary<string, CardData> _cardDatabase = new Dictionary<string, CardData>();

    void Awake()
    {
        // 씬 전환 시 유지되는 싱글톤 설정
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            DontDestroyOnLoad(gameObject);
        }

        Instance = this;
    }

    // [최적화] GetAllCards() 호출 시 매번 힙에 새 List를 생성하지 않도록 캐싱
    private List<CardData> _cachedCardList;

    public void LoadAllCards()
    {
        // "Resources/CardData" 경로에 있는 모든 CardData ScriptableObject를 로드합니다.
        CardData[] allCards = Resources.LoadAll<CardData>("CardData");

        foreach (var card in allCards)
        {
            if (!_cardDatabase.ContainsKey(card.cardID))
            {
                _cardDatabase.Add(card.cardID, card);
            }
            else
            {
                Debug.LogWarning($"[ResourceManager] 중복된 카드 ID 추가 시도: {card.cardID}");
            }
        }

        _cachedCardList = new List<CardData>(_cardDatabase.Values);
    }

    /// <summary>
    /// 카드 ID로 데이터를 조회합니다. 존재하지 않을 경우 에러 로그를 출력합니다.
    /// </summary>
    public CardData GetCardData(string cardId, string callerContext = null)
    {
        if (_cardDatabase.TryGetValue(cardId, out CardData data))
        {
            return data;
        }

        if (!string.IsNullOrEmpty(callerContext))
        {
            Debug.LogError($"[ResourceManager] 카드를 찾을 수 없음: {cardId} (호출처: {callerContext})");
        }
        else
        {
            Debug.LogError($"[ResourceManager] 카드를 찾을 수 없음: {cardId}");
        }
        return null;
    }

    /// <summary>
    /// 에러 로그 출력 없이 안전하게 카드 데이터 존재 여부를 확인합니다.
    /// </summary>
    public bool TryGetCardData(string cardId, out CardData data)
    {
        return _cardDatabase.TryGetValue(cardId, out data);
    }

    public List<CardData> GetAllCards()
    {
        if (_cachedCardList == null || _cachedCardList.Count != _cardDatabase.Count)
        {
            _cachedCardList = _cardDatabase.Values.ToList();
        }
        return _cachedCardList;
    }
}
