using UnityEngine;

[CreateAssetMenu(fileName = "NewDissolveEffect", menuName = "CardGame/Dissolven Effect Data")]
public class DissolveEffect : ScriptableObject
{
    [Header("1. 사용 연출")]
    [Tooltip("사용 연출 애니메이션")]
    public GameObject dissolveEfectObject;

    [Header("2. 사운드 효과 (SFX)")]
    [Tooltip("등장할 때 재생할 효과음입니다.")]
    public AudioClip spawnSound;
    [Range(0f, 1f)]
    public float soundVolume = 1.0f;

    [Header("3. 사용 연출 시간")]
    [Tooltip("사용 연출 시간")]
    public float dissolveTime;

    public void PlayCard(Transform card)
    {
        if (dissolveEfectObject == null) return;

        GameObject dissolve = Instantiate(dissolveEfectObject, card.position, Quaternion.identity);

        Destroy(dissolve, dissolveTime);
    }
}
