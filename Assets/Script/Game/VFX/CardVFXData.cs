using UnityEngine;

/// <summary>
/// 효과/스킬 연출의 발동 형태를 정의합니다.
/// </summary>
public enum VFXPlayType
{
    Projectile,      // 시전자 -> 각 대상 위치로 날아가는 투사체 (화살, 마법구 등)
    ImpactOnTarget,  // 각 대상 위치에서 직접 생성되는 폭발/피격 이펙트 (번개, 칼집 등)
    AreaField,       // 대상 진영 필드 중앙에 단 1회 생성되는 광역 이펙트 (화염폭풍, 지진, 눈보라 등)
    AreaFromCaster   // 시전자 본체 중심에서 1회 생성되어 필드로 퍼져나가는 이펙트 (충격파, 포효, 신성폭발 등)
}

// 우클릭 -> Create 메뉴에서 쉽게 생성할 수 있도록 속성 추가
[CreateAssetMenu(fileName = "New VFX Data", menuName = "Card Game/Trigger VFX Data")]
public class CardVFXData : ScriptableObject
{
    public EffectTriggerType triggerType; // ON_PLAY, ON_DEATH 등
    public VFXPlayType playType = VFXPlayType.Projectile; // 연출 방식
    [Header("기본 이펙트 및 약공격 설정")]
    public GameObject vfxPrefab;          // 실행할 파티클/애니메이션 프리팹 (일반/약공격)
    public AudioClip soundEffect;         // 실행할 효과음 (일반/약공격)

    [Header("강공격 연출 설정 (ON_ATTACK 트리거 전용)")]
    [Tooltip("데미지가 기준치 이상일 때 사용할 강공격 투사체/이펙트 프리팹 (미설정 시 기본 vfxPrefab 사용)")]
    public GameObject heavyVfxPrefab;
    [Tooltip("강공격 발동 데미지 기준값 (기본값: 6)")]
    public int heavyDamageThreshold = 6;
    [Tooltip("강공격 발동 시 재생할 효과음 (미설정 시 기본 soundEffect 사용)")]
    public AudioClip heavySoundEffect;

    [Header("타이밍 및 생명주기 설정")]
    [Tooltip("이펙트 생성(또는 적중) 후 데미지 숫자(DamageUI)가 표기되기까지의 지연 시간 (초)")]
    public float damageDelay = 0.35f;

    [Tooltip("생성된 이펙트 프리팹이 씬에서 완전히 파괴(Destroy)되기까지의 유지 시간 (초)")]
    public float vfxDestroyTime = 3.0f;

    [Tooltip("다중 투사체 발사 시 각 투사체 간의 발사 간격(초). 0이면 한 번에 모두 동시 발사되며, 0보다 크면(예: 0.15) 해당 간격마다 순서대로 순차 발사됩니다.")]
    public float projectileInterval = 0f;

    [Header("리더 처치(막타) 연출 (선택)")]
    [Tooltip("이 투사체/스킬 공격으로 적 리더를 처치했을 때 발생할 3D 프레임 패배 모션 (미설정 시 기본 모션 적용)")]
    public LeaderDefeatData leaderDefeatMotion;
}
