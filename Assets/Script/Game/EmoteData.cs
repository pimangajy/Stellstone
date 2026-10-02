using System;
using UnityEngine;

/// <summary>
/// 감정표현의 적용 대상 직업 (공용 또는 특정 직업 전용)
/// </summary>
public enum EmoteTargetClass
{
    All = 0,    // 모든 직업 공용
    Gangzi = 1, // 강지 전용
    Yuni = 2,   // 유니 전용
    Huya = 3    // 후야 전용
}

/// <summary>
/// 개별 감정표현(대사, 버튼 라벨, 음성 클립, 직업 제한)을 정의하는 독립형 ScriptableObject입니다.
/// 하나의 에셋으로 생성하여 여러 스킨의 감정표현 목록에 재사용할 수 있으며,
/// 상점 구매 아이템이나 업적 보상 단위로도 활용됩니다.
/// </summary>
[CreateAssetMenu(fileName = "Emote_", menuName = "Stellar Duel/Emote Data")]
public class EmoteData : ScriptableObject
{
    [Header("1. 감정표현 식별 정보")]
    [Tooltip("감정표현 고유 식별자 (예: GREETING, THANKS, SORRY, GANGZI_SPECIAL 등)")]
    public string emoteId = "GREETING";

    [Tooltip("버튼에 표시될 짧은 레이블 (예: 인사, 감사, 사과, 기선제압 등)")]
    public string buttonLabel = "인사";

    [Tooltip("말풍선에 출력될 실제 캐릭터 대사")]
    [TextArea(1, 3)]
    public string speechMessage = "안녕하세요!";

    [Tooltip("대사 출력 시 재생될 캐릭터 음성 파일 (AudioClip, 없을 시 무음 텍스트만 출력)")]
    public AudioClip voiceClip;

    [Header("2. 직업 제한 설정")]
    [Tooltip("이 감정표현을 장착/사용할 수 있는 대상 직업. All이면 모든 직업 공용입니다.")]
    public EmoteTargetClass targetClass = EmoteTargetClass.All;

    [Header("3. 메타데이터 (상점/인벤토리/설명)")]
    [Tooltip("상점 또는 인벤토리에서 표시할 아이콘 (선택 사항)")]
    public Sprite emoteIcon;

    [TextArea(1, 2)]
    [Tooltip("감정표현 설명 또는 획득처 정보")]
    public string description;

    /// <summary>
    /// 지정된 직업(CardClass)에서 이 감정표현을 사용할 수 있는지 검사합니다.
    /// </summary>
    public bool IsCompatibleWith(CardClass cardClass)
    {
        if (targetClass == EmoteTargetClass.All) return true;
        int classIndex = (int)cardClass + 1; // Gangzi=1, Yuni=2, Huya=3
        return (int)targetClass == classIndex;
    }
}
