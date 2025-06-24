using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 필드 카드에 부착되어 플레이어의 조준 타겟이 될 수 있도록 합니다.
/// 이 스크립트는 플레이어가 조종할 수 없는 카드에 사용됩니다.
/// </summary>
public class EnemyCardTarget : MonoBehaviour
{
    [Header("하이라이트 효과")]
    [Tooltip("타겟팅되었을 때 활성화될 하이라이트 오브젝트입니다. (예: 빛나는 테두리, 파티클 등)")]
    public GameObject highlightEffect;

    [Header("태그 설정")]
    [Tooltip("플레이어의 커서로 인식할 오브젝트의 태그입니다.")]
    public string cursorTag = "PlayerCursor";

    void Awake()
    {
        // 게임 시작 시 하이라이트 효과를 확실히 비활성화합니다.
        if (highlightEffect != null)
        {
            highlightEffect.SetActive(false);
        }
    }

    /// <summary>
    /// 다른 Collider가 이 카드의 Collider 영역 안으로 들어왔을 때 호출됩니다.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // 들어온 오브젝트의 태그가 지정된 커서 태그와 일치하는지 확인합니다.
        if (other.CompareTag(cursorTag))
        {
            Debug.Log(gameObject.name + " 조준됨!");
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 다른 Collider가 이 카드의 Collider 영역에서 나갔을 때 호출됩니다.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        // 나간 오브젝트의 태그가 지정된 커서 태그와 일치하는지 확인합니다.
        if (other.CompareTag(cursorTag))
        {
            Debug.Log(gameObject.name + " 조준 해제됨!");
            if (highlightEffect != null)
            {
                highlightEffect.SetActive(false);
            }
        }
    }
}
