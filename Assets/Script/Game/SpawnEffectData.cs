using UnityEngine;

// 소환될 때의 움직임 종류
public enum SpawnMotionType
{
    Normal,     // 기본 이동
    SkyDrop,    // 하늘에서 쿵!
    PopUp,      // 땅에서 솟아오름
    FadeIn,     // 스르륵 나타남
    Portal      // 포탈에서 나옴
}

/// <summary>
/// 소환될 때 어떤 멋진 효과(이펙트, 소리, 애니메이션)를 줄지 설정하는 데이터 파일입니다.
/// 프로젝트 창에서 우클릭 -> Create -> CardGame -> Spawn Effect Data로 만들 수 있습니다.
/// </summary>
[CreateAssetMenu(fileName = "NewSpawnEffect", menuName = "CardGame/Spawn Effect Data")]
public class SpawnEffectData : ScriptableObject
{
    [Header("기본 설정")]
    public SpawnMotionType motionType = SpawnMotionType.Normal;
    public float duration = 0.5f; // 애니메이션 시간

    [Header("시각 효과 (VFX)")]
    public GameObject spawnVFXPrefab; // 펑! 터지는 이펙트
    public Vector3 vfxOffset;         // 이펙트 위치 조절
    public float vfxDelay = 0.0f;     // 이펙트 타이밍 조절

    [Header("사운드 (SFX)")]
    public AudioClip spawnSound;      // 등장 소리
    [Range(0f, 1f)] public float soundVolume = 1.0f;

    [Header("추가 연출")]
    public float cameraShakeStrength = 0.0f; // 카메라 흔들림 (쿵! 할 때)
}