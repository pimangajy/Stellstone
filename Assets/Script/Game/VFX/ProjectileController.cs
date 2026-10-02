using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// 원거리 하수인이 공격할 때 날아가는 투사체(화살, 마법 등)를 제어합니다.
/// 곡선(포물선) 비행과 목표 도달 시 폭발 효과를 담당합니다.
/// [업데이트] 위아래 포물선뿐만 아니라, 좌우(대각선) 포물선 비행 기능이 추가되었습니다.
/// </summary>
/// <summary>
/// 투사체의 비행 궤적 형태를 정의합니다.
/// </summary>
public enum ProjectileTrajectoryType
{
    Linear,        // 🗡️ 직선 비행 (검기, 총알, 레이저 - 포물선 없이 일직선)
    Parabolic,     // 🏹 포물선 곡사 비행 (화살, 투석, 마법구 - arcHeight 적용)
    Curved,        // 🚀 좌우 커브 / 랜덤 비행 (미사일, 유도 마법구)
    Custom         // ⚙️ 수동 수치 조절
}

/// <summary>
/// 비행 중 투사체의 회전/자세 제어 방식을 정의합니다.
/// </summary>
public enum ProjectileRotationMode
{
    FaceTargetHorizontal, // 🗡️ [검기/슬래시 특화] 프리팹의 X:90 등 기울기를 유지한 채 목표 수평 방향(Y축)만 조준
    FaceFlightDirection,  // 🏹 [화살/미사일] 궤적 곡선에 따라 머리 각도 자연스럽게 추종
    FixedRotation,        // 🧊 프리팹 원래 회전 각도 그대로 고정 비행
    Spinning              // 🌀 비행 중 계속해서 자전 (표창, 회전 도끼 등)
}

/// <summary>
/// 원거리 하수인이 공격할 때 날아가는 투사체(화살, 마법, 검기 등)를 제어합니다.
/// 직선, 포물선, 랜덤 커브 비행과 검기 수평 회전, 자전 등을 지원합니다.
/// </summary>
public class ProjectileController : MonoBehaviour
{
    [Header("비행 모드 및 회전 설정")]
    [Tooltip("비행 궤적 형태 (직선, 포물선, 커브 등)")]
    public ProjectileTrajectoryType trajectoryType = ProjectileTrajectoryType.Parabolic;

    [Tooltip("비행 중 회전/자세 제어 방식")]
    public ProjectileRotationMode rotationMode = ProjectileRotationMode.FaceFlightDirection;

    [Tooltip("기본 비행 회전에 더해줄 추가 회전 오프셋 (Euler 각도: X, Y, Z)")]
    public Vector3 rotationOffset = Vector3.zero;

    [Tooltip("소환 시 프리팹에 세팅된 로컬 회전(예: X: 90)을 기본 오프셋으로 반영할지 여부")]
    public bool usePrefabRotationAsOffset = true;

    [Tooltip("자전(Spinning) 모드 시 회전 속도 (도/초)")]
    public float spinSpeed = 720f;
    [Tooltip("자전(Spinning) 모드 시 회전 축")]
    public Vector3 spinAxis = Vector3.forward;

    [Header("기본 비행 파라미터")]
    [Tooltip("투사체가 날아가는 속도")]
    public float projectileSpeed = 15f;
    [Tooltip("투사체의 포물선 높이 (Parabolic / Custom 모드에서 사용)")]
    public float projectileArcHeight = 1.5f;
    [Tooltip("좌우로 휘어지는 정도 (Curved 기본값 / Custom 모드에서 사용)")]
    public float horizontalArc = 0f;

    [Header("커브 / 랜덤 비행 설정 (Curved 모드 전용)")]
    [Tooltip("좌/우 방향을 무작위(왼쪽 또는 오른쪽)로 결정")]
    public bool randomLeftRight = true;

    [Tooltip("커브의 강도 및 높이를 무작위 범위 내에서 결정할지 여부")]
    public bool useRandomArc = true;

    [Tooltip("포물선 높이(Y) 무작위 범위 (최소, 최대)")]
    public Vector2 randomHeightRange = new Vector2(0.8f, 2.5f);

    [Tooltip("좌우 커브(X/Z) 강도 무작위 범위 (최소, 최대)")]
    public Vector2 randomHorizontalRange = new Vector2(1.0f, 3.0f);

    [Header("도착 연출")]
    [Tooltip("목표에 맞았을 때 터질 이펙트 (비워둬도 됨)")]
    public GameObject hitEffectPrefab;

    [Tooltip("이펙트가 생성될 위치의 오프셋 (기본값: 위로 0.5, 카메라 쪽으로 -0.5)")]
    public Vector3 hitEffectOffset = new Vector3(0f, 0.5f, 0f);

    [Tooltip("이펙트가 생성될 회전 각도 (Euler 각도: X, Y, Z)")]
    public Vector3 hitEffectRotation = Vector3.zero;

    [Tooltip("이펙트의 크기 배율 (기본값: 1, 1, 1)")]
    public Vector3 hitEffectScale = Vector3.one;

    [Tooltip("적중 이펙트가 씬에서 파괴되기까지의 시간(초)")]
    public float hitEffectDestroyTime = 1.0f;

    [Tooltip("목표 적중 후 데미지 숫자가 표시되기까지의 지연 시간 (초, 기본값: 0이면 적중 즉시 표기)")]
    public float damageDelay = 0.0f;

    [Header("사운드 설정 (SFX)")]
    [Tooltip("발사 시 1회 재생할 효과음")]
    public AudioClip launchSound;
    [Range(0f, 1f)] public float launchSoundVolume = 1.0f;

    [Tooltip("날아가는 동안 투사체를 따라다니며 재생할 루프 효과음 (슈우웅, 바람/불꽃 소리 등)")]
    public AudioClip flyingSound;
    [Range(0f, 1f)] public float flyingSoundVolume = 1.0f;

    [Tooltip("목표 적중(폭발) 시 1회 재생할 효과음")]
    public AudioClip hitSound;
    [Range(0f, 1f)] public float hitSoundVolume = 1.0f;

    private AudioSource _flyingAudioSource;
    private Quaternion _initialPrefabRotation = Quaternion.identity;
    private bool _hasInitialRotation = false;

    private void Awake()
    {
        if (!_hasInitialRotation)
        {
            _initialPrefabRotation = transform.rotation;
            _hasInitialRotation = true;
        }
    }

    public void Fire(Vector3 startPos, Vector3 targetPos, Action onHitCallback)
    {
        if (!_hasInitialRotation)
        {
            _initialPrefabRotation = transform.rotation;
            _hasInitialRotation = true;
        }

        transform.position = startPos;

        // 1. 발사 사운드 재생 (SoundManager 풀링 사용, 미존재 시 Fallback)
        if (launchSound != null)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX3D(launchSound, startPos, launchSoundVolume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(launchSound, startPos, launchSoundVolume);
            }
        }

        // 2. 날아가는 동안 루프 사운드 재생 (투사체 오브젝트에 3D 사운드로 부착)
        if (flyingSound != null)
        {
            _flyingAudioSource = gameObject.GetComponent<AudioSource>();
            if (_flyingAudioSource == null) _flyingAudioSource = gameObject.AddComponent<AudioSource>();

            // SoundManager 설정 볼륨 반영
            float effectiveVolume = flyingSoundVolume;
            if (SoundManager.Instance != null)
            {
                effectiveVolume = SoundManager.Instance.IsMuted ? 0f : (flyingSoundVolume * SoundManager.Instance.MasterVolume * SoundManager.Instance.SFXVolume);
            }

            _flyingAudioSource.clip = flyingSound;
            _flyingAudioSource.volume = effectiveVolume;
            _flyingAudioSource.loop = true;
            _flyingAudioSource.spatialBlend = 1f; // 3D 입체 음향
            _flyingAudioSource.rolloffMode = AudioRolloffMode.Linear;
            _flyingAudioSource.maxDistance = 50f;
            _flyingAudioSource.Play();
        }

        // 3. 비행 모드에 따른 궤적 수치 계산
        float effectiveArcHeight = projectileArcHeight;
        float effectiveHorizontalArc = horizontalArc;

        switch (trajectoryType)
        {
            case ProjectileTrajectoryType.Linear:
                effectiveArcHeight = 0f;
                effectiveHorizontalArc = 0f;
                break;

            case ProjectileTrajectoryType.Parabolic:
                effectiveArcHeight = projectileArcHeight;
                effectiveHorizontalArc = 0f;
                break;

            case ProjectileTrajectoryType.Curved:
                if (useRandomArc)
                {
                    effectiveArcHeight = UnityEngine.Random.Range(randomHeightRange.x, randomHeightRange.y);
                    float magnitude = UnityEngine.Random.Range(randomHorizontalRange.x, randomHorizontalRange.y);
                    float sign = (randomLeftRight && UnityEngine.Random.value < 0.5f) ? -1f : 1f;
                    effectiveHorizontalArc = magnitude * sign;
                }
                else
                {
                    effectiveArcHeight = projectileArcHeight;
                    float sign = (randomLeftRight && UnityEngine.Random.value < 0.5f) ? -1f : 1f;
                    effectiveHorizontalArc = horizontalArc * sign;
                }
                break;

            case ProjectileTrajectoryType.Custom:
            default:
                effectiveArcHeight = projectileArcHeight;
                effectiveHorizontalArc = horizontalArc;
                break;
        }

        StartCoroutine(FlyRoutine(startPos, targetPos, projectileSpeed, effectiveArcHeight, effectiveHorizontalArc, onHitCallback));
    }

    private IEnumerator FlyRoutine(Vector3 start, Vector3 target, float speed, float arcHeight, float horizArc, Action onHit)
    {
        // 두 점 사이의 거리를 기반으로 총 비행 시간 계산
        float distance = Vector3.Distance(start, target);

        // (안전장치) 거리가 너무 가깝거나 속도가 0이면 즉시 도착 처리
        if (distance <= 0.01f || speed <= 0f)
        {
            yield return StartCoroutine(CompleteFlightRoutine(target, onHit));
            yield break;
        }

        float duration = distance / speed;
        float elapsedTime = 0f;

        // 1. 투사체가 날아가는 정면(Forward) 방향 계산
        Vector3 forwardDir = (target - start).normalized;

        // 2. 정면을 기준으로 '오른쪽(Right)' 방향 계산 (수직 위 벡터와 외적)
        Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
        if (rightDir == Vector3.zero) rightDir = Vector3.right;

        // 3. 수평 평면상 목표 조준 회전 (FaceTargetHorizontal용)
        Vector3 horizontalDir = new Vector3(target.x - start.x, 0f, target.z - start.z).normalized;
        if (horizontalDir == Vector3.zero) horizontalDir = Vector3.forward;
        Quaternion horizontalTargetRot = Quaternion.LookRotation(horizontalDir, Vector3.up);

        // 4. 기본 회전 오프셋 결합 (프리팹 기본 회전 + 인스펙터 rotationOffset)
        Quaternion baseOffset = usePrefabRotationAsOffset 
            ? (_initialPrefabRotation * Quaternion.Euler(rotationOffset))
            : Quaternion.Euler(rotationOffset);

        // 이전 위치를 기억해서 투사체가 날아가는 방향을 바라보게 만듭니다.
        Vector3 previousPos = start;

        // 초기 회전 적용
        if (rotationMode == ProjectileRotationMode.FaceTargetHorizontal)
        {
            transform.rotation = horizontalTargetRot * baseOffset;
        }
        else if (rotationMode == ProjectileRotationMode.FixedRotation)
        {
            transform.rotation = baseOffset;
        }

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            // 1. 직선 위치 계산 (Lerp)
            Vector3 currentPos = Vector3.Lerp(start, target, t);

            // 2. 포물선 곡률 계산 (Mathf.Sin: 0 -> 1 -> 0 형태)
            float arcMultiplier = Mathf.Sin(t * Mathf.PI);

            // 3. 위/아래 포물선 적용
            currentPos.y += arcMultiplier * arcHeight;

            // 4. 좌/우 커브 적용
            currentPos += rightDir * (arcMultiplier * horizArc);

            // 5. 회전 모드별 회전 갱신
            switch (rotationMode)
            {
                case ProjectileRotationMode.FaceFlightDirection:
                    Vector3 moveDirection = currentPos - previousPos;
                    if (moveDirection != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up) * baseOffset;
                    }
                    break;

                case ProjectileRotationMode.FaceTargetHorizontal:
                    // 수평 조준 + 프리팹 X:90 각도 유지
                    transform.rotation = horizontalTargetRot * baseOffset;
                    break;

                case ProjectileRotationMode.Spinning:
                    transform.rotation = Quaternion.LookRotation(horizontalDir, Vector3.up) * baseOffset;
                    transform.Rotate(spinAxis, spinSpeed * elapsedTime, Space.Self);
                    break;

                case ProjectileRotationMode.FixedRotation:
                default:
                    // 회전 변경 없음
                    break;
            }

            // 위치 적용 및 이전 위치 갱신
            transform.position = currentPos;
            previousPos = currentPos;

            yield return null;
        }

        yield return StartCoroutine(CompleteFlightRoutine(target, onHit));
    }

    /// <summary>
    /// 목표에 도달했을 때의 처리를 담당합니다.
    /// 적중 이펙트 소환 후 damageDelay 대기 뒤 onHit 콜백을 호출합니다.
    /// </summary>
    private IEnumerator CompleteFlightRoutine(Vector3 target, Action onHit)
    {
        transform.position = target;

        // 비행 루프 사운드 정지
        if (_flyingAudioSource != null && _flyingAudioSource.isPlaying)
        {
            _flyingAudioSource.Stop();
        }

        // 투사체 자체의 비주얼(렌더러/파티클) 비활성화하여 데미지 대기 시간 동안 제자리에 멈춰 서 있지 않도록 처리
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;
        var particleSystems = GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particleSystems) ps.Stop();

        // 적중 이펙트 소환 및 자동 파괴 등록
        if (hitEffectPrefab != null)
        {
            // 타겟(적 하수인의 발밑) 위치에 오프셋을 더해서 생성합니다.
            Vector3 spawnPos = target + hitEffectOffset;
            Quaternion spawnRot = Quaternion.Euler(hitEffectRotation);
            GameObject hitObj = Instantiate(hitEffectPrefab, spawnPos, spawnRot);

            // 크기 배율 적용
            hitObj.transform.localScale = Vector3.Scale(hitEffectPrefab.transform.localScale, hitEffectScale);

            // 자식 ParticleSystem들의 scalingMode를 Hierarchy로 설정하여 부모 크기 배율에 맞춰 모든 파티클 크기 및 속도 비례 확대
            float uniformScale = (hitEffectScale.x + hitEffectScale.y + hitEffectScale.z) / 3f;
            var allParticles = hitObj.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allParticles)
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            // TrailRenderer 두께 보정
            var trails = hitObj.GetComponentsInChildren<TrailRenderer>(true);
            foreach (var trail in trails)
            {
                trail.widthMultiplier *= uniformScale;
            }

            // Light 조명 범위 보정
            var lights = hitObj.GetComponentsInChildren<Light>(true);
            foreach (var lt in lights)
            {
                lt.range *= uniformScale;
            }

            // 씬에 오브젝트가 계속 쌓이지 않도록 일정 시간 후 자동 삭제
            if (hitEffectDestroyTime > 0f)
            {
                Destroy(hitObj, hitEffectDestroyTime);
            }
        }

        // 적중/폭발 사운드 재생 (SoundManager 풀링 사용, 미존재 시 Fallback)
        if (hitSound != null)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX3D(hitSound, target, hitSoundVolume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(hitSound, target, hitSoundVolume);
            }
        }

        // 목표 적중 후 데미지 표기까지 대기
        if (damageDelay > 0f)
        {
            yield return YieldInstructionCache.WaitForSeconds(damageDelay);
        }

        // 도달했음을 알림 (데미지 숫자 띄우기, 체력 깎기 등의 타이밍용)
        onHit?.Invoke();

        // 투사체 자신은 파괴
        Destroy(gameObject);
    }
}