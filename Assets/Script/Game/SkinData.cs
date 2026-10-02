using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 개별 리더 스킨의 외형(이미지/스프라이트), 기본 감정표현(최대 4개),
/// 그리고 향후 확장될 연출 데이터(VFX, 패배 모션 등)를 보관하는 독립형 데이터 에셋입니다.
/// </summary>
[CreateAssetMenu(fileName = "Skin_", menuName = "Stellar Duel/Skin Data")]
public class SkinData : ScriptableObject
{
    [Header("1. 기본 정보")]
    [Tooltip("스킨 고유 식별자 (예: Skin_Gangzi_0001, Skin_Yuni_0001)")]
    public string skinId;

    [Tooltip("스킨 표시 이름 (예: 기본 강지, 여름 바캉스 강지)")]
    public string skinName;

    [Tooltip("스킨 대상 직업")]
    public CardClass targetClass = CardClass.Gangzi;

    [TextArea(2, 4)]
    [Tooltip("스킨 설명 / 배경 스토리")]
    public string description;

    [Header("2. 이미지 및 비주얼")]
    [Tooltip("인게임 리더 및 상세보기에 표시될 메인 스킨 일러스트 스프라이트")]
    public Sprite skinSprite;

    [Tooltip("UI 슬롯이나 목록에 표시될 썸네일 아이콘 (미지정 시 skinSprite 사용)")]
    public Sprite iconSprite;

    [Header("3. 조준선 커스터마이징 (선택 사항)")]
    [Tooltip("이 스킨 착용 시 배틀에서 사용할 조준선 도트 프리팹 (미지정/null 시 기본 조준선 자동 사용)")]
    public GameObject targetingPathPrefab;

    [Tooltip("이 스킨 착용 시 배틀에서 사용할 조준선 화살촉 프리팹 (미지정/null 시 기본 화살촉 자동 사용)")]
    public GameObject targetingArrowHeadPrefab;

    [Header("4. 감정표현 설정 (최대 4개)")]
    [Tooltip("이 스킨에 기본으로 장착되는 EmoteData 목록 (최대 4개)")]
    public List<EmoteData> defaultEmotes = new List<EmoteData>(4);

    [Header("5. 향후 확장 필드 (준비됨)")]
    [Tooltip("리더 소환/등장 시 특수 VFX 프리팹 (추후 사용)")]
    public GameObject spawnVfxPrefab;

    [Tooltip("리더 패배 시 특수 연출 데이터 (추후 사용)")]
    public LeaderDefeatData defeatData;

    [Tooltip("스킨 전용 대기/터치 애니메이션 컨트롤러 (추후 사용)")]
    public RuntimeAnimatorController animatorController;

    /// <summary>
    /// 지정된 인덱스(0~3)의 감정표현을 반환합니다.
    /// </summary>
    public EmoteData GetEmote(int slotIndex)
    {
        if (defaultEmotes != null && slotIndex >= 0 && slotIndex < defaultEmotes.Count)
        {
            return defaultEmotes[slotIndex];
        }
        return null;
    }

    /// <summary>
    /// 최대 4개의 유효한 감정표현 목록을 반환합니다.
    /// </summary>
    public List<EmoteData> GetValidEmotes()
    {
        var list = new List<EmoteData>();
        if (defaultEmotes != null)
        {
            foreach (var emote in defaultEmotes)
            {
                if (emote != null && list.Count < 4)
                {
                    list.Add(emote);
                }
            }
        }
        return list;
    }

    /// <summary>
    /// 특정 EmoteId에 해당하는 감정표현이 이 스킨에 장착되어 있는지 확인합니다.
    /// </summary>
    public bool HasEmote(string emoteId)
    {
        if (string.IsNullOrEmpty(emoteId) || defaultEmotes == null) return false;
        return defaultEmotes.Exists(e => e != null && string.Equals(e.emoteId, emoteId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// UI 목록 등에서 표시할 대표 썸네일 스프라이트를 반환합니다.
    /// </summary>
    public Sprite GetIconOrMainSprite()
    {
        return iconSprite != null ? iconSprite : skinSprite;
    }
}
