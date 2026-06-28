using UnityEngine;

// 우클릭 -> Create 메뉴에서 쉽게 생성할 수 있도록 속성 추가
[CreateAssetMenu(fileName = "New VFX Data", menuName = "Card Game/Trigger VFX Data")]
public class CardVFXData : ScriptableObject
{
    public EffectTriggerType triggerType; // ON_PLAY, ON_DEATH 등
    public GameObject vfxPrefab;          // 실행할 파티클/애니메이션 프리팹
    public AudioClip soundEffect;         // 실행할 효과음

    // 이펙트 유지 시간이나 크기 등 공통으로 쓸 설정이 있다면 여기에 추가해도 좋습니다.
    // public float duration = 1.0f;
}
