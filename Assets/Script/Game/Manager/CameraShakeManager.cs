using UnityEngine;
using Cinemachine;

/// <summary>
/// Cinemachine Impulse 시스템을 기반으로 게임 내 화면 흔들림(Camera Shake)을 관리하는 싱글톤 매니저입니다.
/// 단발성 덜컹임이 아니라, 강하게 팍 흔들린 뒤 진동 강도가 서서히 잦아들며 멈추는(Decay) 자연스러운 타격 연출을 제공합니다.
/// </summary>
public class CameraShakeManager : MonoBehaviour
{
    private static CameraShakeManager _instance;
    public static CameraShakeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CameraShakeManager>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Impulse Source")]
    [Tooltip("화면 흔들림을 발생시킬 CinemachineImpulseSource 컴포넌트 (비워둘 경우 자동 검색)")]
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Header("Shake Presets")]
    [Tooltip("기본 흔들림 세기")]
    [SerializeField] private float defaultForce = 1.0f;

    [Tooltip("강한 공격 / 대형 투사체 피격 시 흔들림 세기")]
    [SerializeField] private float heavyForce = 2.5f;

    [Header("감쇠(Decay) 및 진동 파라미터")]
    [Tooltip("흔들림이 서서히 잦아들기까지 걸리는 시간(초)")]
    [SerializeField] private float decayDuration = 0.45f;

    [Tooltip("진동 빈도 배율 (값이 클수록 덜덜덜 빠르게 떨리며 감쇠합니다)")]
    [SerializeField] private float frequencyGain = 6.0f;

    [Tooltip("기본 진폭 배율")]
    [SerializeField] private float amplitudeGain = 2.0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // ImpulseSource 자동 탐색 및 바인딩
        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
            if (impulseSource == null)
            {
                impulseSource = FindFirstObjectByType<CinemachineImpulseSource>();
            }
        }

        ConfigureImpulseEnvelope();
    }

    private void OnValidate()
    {
        ConfigureImpulseEnvelope();
    }

    /// <summary>
    /// Cinemachine Impulse의 시간 포락선(Envelope)과 주파수를 감쇠 진동 형태로 보정합니다.
    /// </summary>
    private void ConfigureImpulseEnvelope()
    {
        if (impulseSource == null) return;

        var def = impulseSource.m_ImpulseDefinition;
        if (def != null)
        {
            def.m_TimeEnvelope.m_AttackTime = 0.0f;   // 충격 즉시 발생
            def.m_TimeEnvelope.m_SustainTime = 0.04f; // 순간적인 최대 충격 유지
            def.m_TimeEnvelope.m_DecayTime = decayDuration; // 서서히 잦아드는 감쇠 시간
            def.m_FrequencyGain = frequencyGain;      // 잘게 떨리는 진동 주파수 (~20Hz)
            def.m_AmplitudeGain = amplitudeGain;
        }
    }

    /// <summary>
    /// 지정된 세기(force)로 화면을 흔듭니다.
    /// 화면 2D 평면(X, Y) 방향으로 충격을 주어 흔들림이 서서히 감쇠되도록 합니다.
    /// </summary>
    /// <param name="force">흔들림 세기 (기본값: defaultForce = 1.0f)</param>
    public void Shake(float force = -1.0f)
    {
        if (force < 0f) force = defaultForce;
        if (impulseSource != null)
        {
            ConfigureImpulseEnvelope();

            // 2D 화면 평면(X, Y) 기준 무작위 방향 벡터 생성하여 입체감 있는 진동 연출
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            if (randomDir == Vector2.zero) randomDir = Vector2.up;

            Vector3 velocity = new Vector3(randomDir.x, randomDir.y, 0f) * force;
            impulseSource.GenerateImpulseWithVelocity(velocity);
        }
        else
        {
            Debug.LogWarning("[CameraShakeManager] CinemachineImpulseSource가 연결되지 않았습니다.");
        }
    }

    /// <summary>
    /// 6데미지 이상의 강한 공격 또는 대형 투사체 적중 시 사용하는 강한 감쇠 흔들림입니다.
    /// </summary>
    public void ShakeHeavy()
    {
        Shake(heavyForce);
    }

    /// <summary>
    /// 특정 3D 방향 벡터(velocity)로 화면을 흔듭니다.
    /// </summary>
    public void ShakeWithVelocity(Vector3 velocity)
    {
        if (impulseSource != null)
        {
            ConfigureImpulseEnvelope();
            impulseSource.GenerateImpulseWithVelocity(velocity);
        }
    }

    /// <summary>
    /// 특정 위치(position)에서 충격파가 발생하도록 흔듭니다.
    /// </summary>
    public void ShakeAt(Vector3 position, float force = -1.0f)
    {
        if (force < 0f) force = defaultForce;
        if (impulseSource != null)
        {
            ConfigureImpulseEnvelope();
            impulseSource.GenerateImpulseAt(position, Vector3.down * force);
        }
    }

    [ContextMenu("Test/Heavy Shake Test")]
    public void TestHeavyShake()
    {
        ShakeHeavy();
    }
}
