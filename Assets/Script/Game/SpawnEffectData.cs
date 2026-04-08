using UnityEngine;

/// <summary>
/// 소환될 때의 카드 이동 물리 연출 종류입니다.
/// </summary>
public enum SpawnMotionType
{
    Normal,     // 기본 이동 (패에서 필드로)
    SkyDrop,    // 하늘에서 강하게 낙하 (쿵!)
    PopUp,      // 바닥에서 솟아오름
    FadeIn,     // 알파값이 변하며 스르륵 나타남
    Portal      // 포탈 이펙트와 함께 등장
}

/// <summary>
/// 카드가 필드에 소환될 때의 시각적(VFX), 청각적(SFX), 물리적(Motion) 연출을 정의하는 데이터 에셋입니다.
/// </summary>
[CreateAssetMenu(fileName = "NewSpawnEffect", menuName = "CardGame/Spawn Effect Data")]
public class SpawnEffectData : ScriptableObject
{
    [Header("1. 애니메이션 설정 (Motion)")]
    [Tooltip("카드가 필드에 배치될 때의 이동 방식입니다.")]
    public SpawnMotionType motionType = SpawnMotionType.Normal;

    [Tooltip("전체 소환 연출이 완료되기까지의 시간(초)입니다.")]
    public float duration = 1.0f;

    [Header("2. 시각 효과 (VFX)")]
    [Tooltip("소환 시 생성될 파티클 이펙트 프리팹입니다.")]
    public GameObject spawnVFXPrefab;

    [Tooltip("오브젝트 위치 기준 미세 조정용 오프셋입니다.")]
    public Vector3 vfxOffset = Vector3.zero;

    [Tooltip("소환 시작 후 이펙트가 터지는 타이밍입니다. (0이면 즉시)")]
    public float vfxDelay = 0.0f;

    [Tooltip("이펙트 인스턴스가 파괴되기까지의 시간입니다.")]
    public float vfxDestroyTime = 3.0f;

    [Header("3. 사운드 효과 (SFX)")]
    [Tooltip("등장할 때 재생할 효과음입니다.")]
    public AudioClip spawnSound;

    [Range(0f, 1f)]
    public float soundVolume = 1.0f;

    [Header("4. 카메라 연출 (Impact)")]
    [Tooltip("소환 시 화면 흔들림 강도입니다. (SkyDrop 등에 사용)")]
    public float cameraShakeStrength = 0.0f;

    [Tooltip("화면 흔들림 지속 시간입니다.")]
    public float shakeDuration = 0.2f;

    /// <summary>
    /// 대상 오브젝트의 위치를 기반으로 이펙트를 생성합니다.
    /// </summary>
    /// <param name="target">이펙트가 생성될 기준 오브젝트</param>
    public void PlaySpawnVFX(Transform target)
    {
        if (spawnVFXPrefab == null || target == null) return;

        // 대상의 현재 위치에 오프셋만 더해 생성합니다.
        Vector3 spawnPos = target.position + vfxOffset;
        GameObject vfx = Instantiate(spawnVFXPrefab, spawnPos, Quaternion.identity);

        Destroy(vfx, vfxDestroyTime);
    }

    /// <summary>
    /// 지정된 AudioSource를 통해 소환음을 재생합니다.
    /// </summary>
    public void PlaySpawnSound(AudioSource source)
    {
        if (spawnSound != null && source != null)
        {
            source.PlayOneShot(spawnSound, soundVolume);
        }
    }
}