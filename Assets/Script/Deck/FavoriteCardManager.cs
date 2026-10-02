using System;
using UnityEngine;

/// <summary>
/// 유저가 지정한 단 하나의 대표(좋아하는) 카드를 관리하는 매니저입니다.
/// 메인 씬 좌측에 표시될 카드를 결정하며, 덱 편성 씬의 상세 팝업에서 별모양 버튼으로 설정합니다.
/// </summary>
public static class FavoriteCardManager
{
    private const string PREF_KEY_DEFAULT = "FavoriteCard_CardId";

    /// <summary>
    /// 대표 카드가 변경되었을 때 발생하는 이벤트 (새로운 cardID 전달, 없으면 빈 문자열)
    /// </summary>
    public static event Action<string> OnFavoriteCardChanged;

    private static string GetPrefKey()
    {
        string userKey = SinginManager.CurrentUserData?.username;
        if (string.IsNullOrEmpty(userKey))
        {
            userKey = PlayerPrefs.GetString("CurrentUserId", string.Empty);
        }
        return string.IsNullOrEmpty(userKey) ? PREF_KEY_DEFAULT : $"{PREF_KEY_DEFAULT}_{userKey}";
    }

    /// <summary>
    /// 현재 저장된 대표 카드의 CardID를 반환합니다.
    /// </summary>
    public static string GetFavoriteCardId()
    {
        string key = GetPrefKey();
        return PlayerPrefs.GetString(key, string.Empty);
    }

    /// <summary>
    /// 현재 대표 카드로 지정된 CardData를 반환합니다.
    /// 지정된 카드가 없거나 로드 실패 시, 기본 카드를 fallback으로 반환합니다.
    /// </summary>
    public static CardData GetFavoriteCard()
    {
        string cardId = GetFavoriteCardId();
        CardData data = null;

        if (!string.IsNullOrEmpty(cardId))
        {
            data = CardTextFormatter.FindCardData(cardId);
        }

        // 저장된 카드가 없거나 찾을 수 없을 때 fallback 기본 카드 반환
        if (data == null)
        {
            data = CardTextFormatter.FindCardData("cards-gangzi-001");
            if (data == null)
            {
                var allCards = Resources.LoadAll<CardData>("CardData");
                if (allCards != null && allCards.Length > 0)
                {
                    data = allCards[0];
                }
            }
        }

        return data;
    }

    /// <summary>
    /// 특정 카드가 현재 대표 카드인지 여부를 반환합니다.
    /// </summary>
    public static bool IsFavorite(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return false;
        string currentFav = GetFavoriteCardId();
        return string.Equals(currentFav, cardId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 특정 카드를 유일한 대표 카드로 설정하거나 해제합니다.
    /// 단 하나만 가능하므로 새로운 카드를 true로 지정하면 기존 카드는 자동 대체됩니다.
    /// </summary>
    public static void SetFavorite(string cardId, bool favorite)
    {
        string key = GetPrefKey();
        string newCardId = favorite ? cardId : string.Empty;

        PlayerPrefs.SetString(key, newCardId);
        PlayerPrefs.Save();

        Debug.Log($"[FavoriteCardManager] 대표(좋아요) 카드 변경: '{(favorite ? cardId : "해제됨")}'");
        OnFavoriteCardChanged?.Invoke(newCardId);
    }

    /// <summary>
    /// 특정 카드의 대표 카드 상태를 토글합니다.
    /// 이미 대표 카드면 해제하고, 아니면 이 카드를 유일한 대표 카드로 지정합니다.
    /// </summary>
    public static bool ToggleFavorite(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return false;

        bool currentlyFav = IsFavorite(cardId);
        bool newState = !currentlyFav;

        SetFavorite(cardId, newState);
        return newState;
    }
}
