using DG.Tweening;
using UnityEngine;

/// <summary>
/// 3D 리더 프레임 및 받침대의 물리적 파괴/퇴장 연출 종류입니다.
/// </summary>
public enum Leader3DMotionType
{
    Sink,        // 바닥 아래로 가라앉음 (기본 진동 후 침몰)
    Collapse,    // 비스듬히 기울어지며 무너짐 (회전 + 추락)
    TiltBack,    // 뒤로 젖혀지며 넘어감
    Shatter,     // 파편처럼 강하게 떨린 뒤 축소 소멸
    Explode,     // 폭발 반동으로 솟구쳤다가 파괴
    Surrender,   // 조용하고 정중하게 가라앉음 (항복용)
    CustomShake  // 단순 카메라 및 위치 진동
}

/// <summary>
/// 특정 공격 유형 또는 사망 상황에 맞춘 리더의 3D 오브젝트(프레임/필드) 파괴 연출 ScriptableObject입니다.
/// Sprite(캐릭터 일러스트)와 분리되어 3D 프레임 및 물리적 환경의 파괴 연출을 전담합니다.
/// </summary>
[CreateAssetMenu(fileName = "DefeatMotion_", menuName = "Card Game/Leader Defeat Data")]
public class LeaderDefeatData : ScriptableObject
{
    [Header("1. 연출 식별 정보")]
    [Tooltip("패배 모션의 식별 키 (예: Default, Surrender, Heavy, Slash, Explosion 등)")]
    public string defeatTypeKey = "Default";

    [TextArea(2, 4)]
    [Tooltip("모션 설명")]
    public string description;

    [Header("2. 타이밍 설정 (결과창 지연 시간)")]
    [Tooltip("패배 연출 시작 후 결과창(Results Panel)이 화면에 나타나기까지의 대기 시간(초)")]
    [Range(0.3f, 5.0f)]
    public float resultPanelDelay = 1.2f;

    [Tooltip("3D 프레임 모션 애니메이션 지속 시간 (초)")]
    public float motionDuration = 0.8f;

    [Header("3. 3D 프레임 물리 모션 (Frame Motion)")]
    public Leader3DMotionType motionType = Leader3DMotionType.Sink;

    [Tooltip("모션 시작 시 위치 진동 강도 (DOShakePosition)")]
    public Vector3 shakeStrength = new Vector3(0.4f, 0.2f, 0.4f);

    [Tooltip("모션 완료 시 최종 이동 오프셋 (Sink, Collapse 등에서 활용)")]
    public Vector3 motionOffset = new Vector3(0f, -1.2f, 0f);

    [Tooltip("모션 완료 시 최종 회전 각도 (Collapse, TiltBack 등에서 활용)")]
    public Vector3 rotationOffset = new Vector3(-25f, 0f, 0f);

    [Header("4. 시각 효과 (VFX)")]
    [Tooltip("리더 파괴 시 생성할 파티클/폭발 이펙트 프리팹 (선택)")]
    public GameObject vfxPrefab;
    public Vector3 vfxOffset = Vector3.zero;
    public float vfxDestroyTime = 3.0f;

    [Header("5. 화면 흔들림 (Camera Shake)")]
    [Tooltip("사망 시 카메라 흔들림 발생 여부")]
    public bool enableCameraShake = true;
    [Tooltip("카메라 흔들림 강도 (기본 1.0f, 강한 타격 2.5f)")]
    public float cameraShakeForce = 2.5f;

    [Header("6. 사운드 효과 (SFX)")]
    [Tooltip("사망 시 재생할 사운드 (선택)")]
    public AudioClip defeatSound;
    [Range(0f, 1f)]
    public float soundVolume = 1.0f;

    /// <summary>
    /// 대상 리더 3D 프레임 오브젝트에 이 ScriptableObject에 정의된 파괴 모션을 실행합니다.
    /// </summary>
    public void Play3DMotion(LeaderCardDisplay leaderDisplay)
    {
        if (leaderDisplay == null) return;
        Transform frameTransform = leaderDisplay.transform;

        // 1. 기존 진행 중인 트윈 중단
        frameTransform.DOKill();

        // 2. 사운드 재생
        if (defeatSound != null)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(defeatSound, soundVolume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(defeatSound, frameTransform.position, soundVolume);
            }
        }

        // 3. 카메라 흔들림
        if (enableCameraShake && CameraShakeManager.Instance != null)
        {
            CameraShakeManager.Instance.Shake(cameraShakeForce);
        }

        // 4. 파티클 이펙트(VFX) 생성
        if (vfxPrefab != null)
        {
            Vector3 spawnPos = frameTransform.position + vfxOffset;
            GameObject vfx = Instantiate(vfxPrefab, spawnPos, Quaternion.identity);
            Destroy(vfx, vfxDestroyTime);
        }

        // 5. HP, 데미지 및 힐 UI 비활성화 (깔끔한 연출)
        if (leaderDisplay.HP != null) leaderDisplay.HP.gameObject.SetActive(false);
        if (leaderDisplay.damageIMG != null) leaderDisplay.damageIMG.SetActive(false);
        if (leaderDisplay.healIMG != null) leaderDisplay.healIMG.SetActive(false);

        // 6. 3D 물리 모션 실행
        float d = motionDuration;
        switch (motionType)
        {
            case Leader3DMotionType.Sink:
                // 진동 후 바닥으로 침몰
                frameTransform.DOShakePosition(d * 0.4f, shakeStrength, 15, 90f)
                    .OnComplete(() =>
                    {
                        frameTransform.DOMoveY(frameTransform.position.y + motionOffset.y, d * 0.6f).SetEase(Ease.InQuad);
                    });
                break;

            case Leader3DMotionType.Collapse:
                // 진동하면서 비스듬히 기울어지며 바닥으로 쓰러짐
                frameTransform.DOShakePosition(d * 0.35f, shakeStrength, 15, 90f);
                frameTransform.DORotate(frameTransform.eulerAngles + rotationOffset, d).SetEase(Ease.InBack);
                frameTransform.DOMoveY(frameTransform.position.y + motionOffset.y, d).SetEase(Ease.InQuad);
                break;

            case Leader3DMotionType.TiltBack:
                // 뒤로 젖혀지며 추락
                frameTransform.DORotate(frameTransform.eulerAngles + rotationOffset, d).SetEase(Ease.OutQuad);
                frameTransform.DOMove(frameTransform.position + motionOffset, d).SetEase(Ease.InQuad);
                break;

            case Leader3DMotionType.Shatter:
                // 거세게 떨리다가 산산조각나듯 축소
                frameTransform.DOShakePosition(d * 0.5f, shakeStrength * 1.5f, 25, 90f);
                frameTransform.DOScale(Vector3.zero, d * 0.5f).SetDelay(d * 0.4f).SetEase(Ease.InBack);
                break;

            case Leader3DMotionType.Explode:
                // 위로 살짝 튀어 올랐다가 파괴
                Sequence explodeSeq = DOTween.Sequence();
                explodeSeq.Append(frameTransform.DOMoveY(frameTransform.position.y + 0.8f, d * 0.3f).SetEase(Ease.OutQuad));
                explodeSeq.Join(frameTransform.DOShakeRotation(d * 0.3f, 25f));
                explodeSeq.Append(frameTransform.DOMoveY(frameTransform.position.y + motionOffset.y, d * 0.7f).SetEase(Ease.InQuad));
                break;

            case Leader3DMotionType.Surrender:
                // 조용하게 아래로 살짝 내려앉음
                frameTransform.DOMoveY(frameTransform.position.y + (motionOffset.y * 0.5f), d).SetEase(Ease.InOutSine);
                break;

            case Leader3DMotionType.CustomShake:
            default:
                frameTransform.DOShakePosition(d, shakeStrength, 18, 90f);
                break;
        }
    }
}
