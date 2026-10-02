using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유저의 스킨별 커스텀 감정표현 장착 정보 및 직업별 대표 스킨 설정을 로컬에 저장/관리하는 싱글톤 매니저입니다.
/// </summary>
public class SkinCustomSettingManager : MonoBehaviour
{
    private static SkinCustomSettingManager _instance;
    public static SkinCustomSettingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<SkinCustomSettingManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("[SkinCustomSettingManager]");
                    _instance = obj.AddComponent<SkinCustomSettingManager>();
                    DontDestroyOnLoad(obj);
                }
            }
            return _instance;
        }
    }

    private const string PREFS_KEY_PREFIX_EMOTE = "SkinEmotes_";
    private const string PREFS_KEY_PREFIX_REP_SKIN = "RepSkin_";

    [System.Serializable]
    private class EmoteListWrapper
    {
        public List<string> emoteIds = new List<string>();
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 특정 스킨에 장착된 4개 감정표현 ID 목록을 가져옵니다.
    /// 커스텀 설정이 없으면 SkinData의 기본 4개 감정표현(defaultEmotes) ID를 반환합니다.
    /// </summary>
    public List<string> GetEquippedEmoteIds(string skinId, SkinData skinData)
    {
        if (string.IsNullOrEmpty(skinId)) return new List<string>();

        string key = PREFS_KEY_PREFIX_EMOTE + skinId;
        if (PlayerPrefs.HasKey(key))
        {
            try
            {
                string json = PlayerPrefs.GetString(key);
                var wrapper = JsonUtility.FromJson<EmoteListWrapper>(json);
                if (wrapper != null && wrapper.emoteIds != null && wrapper.emoteIds.Count > 0)
                {
                    return wrapper.emoteIds;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SkinCustomSettingManager] ⚠️ 스킨 {skinId} 감정표현 불러오기 실패: {ex.Message}");
            }
        }

        // 커스텀 설정이 없으면 기본 에셋의 감정표현 ID 반환
        var result = new List<string>();
        if (skinData != null && skinData.defaultEmotes != null)
        {
            foreach (var emote in skinData.defaultEmotes)
            {
                if (emote != null) result.Add(emote.emoteId);
            }
        }
        return result;
    }

    /// <summary>
    /// 특정 스킨에 장착된 4개 EmoteData 객체 목록을 가져옵니다.
    /// </summary>
    public List<EmoteData> GetEquippedEmotes(string skinId, SkinData skinData)
    {
        var ids = GetEquippedEmoteIds(skinId, skinData);
        var result = new List<EmoteData>();

        if (EmotionManager.Instance != null)
        {
            EmotionManager.Instance.EnsureEmoteDatabaseLoaded();
        }

        foreach (var id in ids)
        {
            EmoteData emote = null;
            if (EmotionManager.Instance != null)
            {
                emote = EmotionManager.Instance.GetEmoteById(id);
            }

            if (emote == null && skinData != null && skinData.defaultEmotes != null)
            {
                emote = skinData.defaultEmotes.Find(e => e != null && string.Equals(e.emoteId, id, StringComparison.OrdinalIgnoreCase));
            }

            if (emote != null)
            {
                result.Add(emote);
            }
        }

        // 부족한 슬롯이 있다면 기본값으로 채움
        if (result.Count < 4 && skinData != null && skinData.defaultEmotes != null)
        {
            foreach (var def in skinData.defaultEmotes)
            {
                if (def != null && !result.Contains(def))
                {
                    result.Add(def);
                    if (result.Count >= 4) break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 특정 스킨의 특정 슬롯(0~3)에 감정표현을 장착합니다.
    /// </summary>
    public void SetEquippedEmote(string skinId, SkinData skinData, int slotIndex, string newEmoteId)
    {
        if (string.IsNullOrEmpty(skinId) || slotIndex < 0 || slotIndex >= 4) return;

        List<string> currentIds = GetEquippedEmoteIds(skinId, skinData);
        while (currentIds.Count < 4)
        {
            currentIds.Add("");
        }

        currentIds[slotIndex] = newEmoteId;

        var wrapper = new EmoteListWrapper { emoteIds = currentIds };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(PREFS_KEY_PREFIX_EMOTE + skinId, json);
        PlayerPrefs.Save();

        Debug.Log($"[SkinCustomSettingManager] 💾 스킨 '{skinId}' 슬롯 {slotIndex}에 감정표현 '{newEmoteId}' 장착 완료!");
    }

    /// <summary>
    /// 특정 직업의 대표 스킨 ID를 가져옵니다.
    /// </summary>
    public string GetRepresentativeSkinId(CardClass cardClass)
    {
        string key = PREFS_KEY_PREFIX_REP_SKIN + cardClass.ToString();
        if (PlayerPrefs.HasKey(key))
        {
            string skinId = PlayerPrefs.GetString(key);
            if (!string.IsNullOrEmpty(skinId)) return skinId;
        }

        // 기본값: 해당 직업의 첫 번째 스킨
        return GetDefaultSkinIdForClass(cardClass);
    }

    /// <summary>
    /// 특정 직업의 대표 스킨 ID를 설정합니다.
    /// </summary>
    public void SetRepresentativeSkinId(CardClass cardClass, string skinId)
    {
        if (string.IsNullOrEmpty(skinId)) return;

        string key = PREFS_KEY_PREFIX_REP_SKIN + cardClass.ToString();
        PlayerPrefs.SetString(key, skinId);
        PlayerPrefs.Save();

        Debug.Log($"[SkinCustomSettingManager] 🌟 직업 '{cardClass}' 대표 스킨을 '{skinId}'로 설정했습니다.");

        // 현재 선택된 덱이 해당 직업이라면 덱의 leaderSkinId도 즉시 갱신
        SyncWithCurrentDeck(cardClass, skinId);
    }

    private void SyncWithCurrentDeck(CardClass cardClass, string skinId)
    {
        if (DeckManager.instance != null && DeckManager.instance.CurrentlyEditingDeck != null)
        {
            if (string.Equals(DeckManager.instance.CurrentlyEditingDeck.deckClass, cardClass.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                DeckManager.instance.CurrentlyEditingDeck.leaderSkinId = skinId;
                Debug.Log($"[SkinCustomSettingManager] 🔄 현재 편집 중인 덱 리더 스킨 동기화 완료: {skinId}");
            }
        }
    }

    private string GetDefaultSkinIdForClass(CardClass cardClass)
    {
        switch (cardClass)
        {
            case CardClass.Gangzi: return "Skin_Gangzi_0001";
            case CardClass.Huya:   return "Skin_Huya_0001";
            case CardClass.Yuni:   return "Skin_Yuni_0001";
            default:               return "Skin_Gangzi_0001";
        }
    }
}
