using UnityEngine;

public enum SpawnMotionType
{
    Normal,     // 기본: 카드 위치에서 슬롯으로 이동
    SkyDrop,    // 하늘에서 쿵! 하고 떨어짐
    PopUp,      // 땅속에서 솟아오름
    FadeIn,     // 제자리에서 서서히 나타남 (투명 -> 불투명)
    Portal      // 포탈이 열리고 나옴 (크기 0 -> 1)
}

[CreateAssetMenu(fileName = "NewSpawnEffect", menuName = "CardGame/Spawn Effect Data")]
public class SpawnEffectData : ScriptableObject
{
    [Header("기본 설정")]
    public SpawnMotionType motionType = SpawnMotionType.Normal;
    public float duration = 0.5f; // 애니메이션 시간

    [Header("시각 효과 (VFX)")]
    [Tooltip("소환 시 재생할 파티클 이펙트 프리팹")]
    public GameObject spawnVFXPrefab;
    [Tooltip("이펙트가 생성될 위치 오프셋 (0,0,0이면 슬롯 위치)")]
    public Vector3 vfxOffset;
    [Tooltip("이펙트 생성 타이밍 (0이면 시작과 동시에, 0.5면 중간에)")]
    public float vfxDelay = 0.0f;

    [Header("사운드 (SFX)")]
    public AudioClip spawnSound;
    [Range(0f, 1f)] public float soundVolume = 1.0f;

    [Header("추가 연출")]
    [Tooltip("착지 시 카메라 흔들림 강도 (SkyDrop 등에서 사용)")]
    public float cameraShakeStrength = 0.0f;
}