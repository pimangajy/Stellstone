using UnityEngine;

/// <summary>
/// 창고 관리 매니저 (기본 참조용 스크립트)
/// 다른 스크립트(상점 등)에서의 참조를 지원하며,
/// 씬에 배치되었을 때만 Instance를 제공합니다. (자동 생성 및 DontDestroyOnLoad 제거)
/// </summary>
public class StorageManager : MonoBehaviour
{
    public static StorageManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 상점 구매 결과 반영 (외부 참조용 Stub)
    /// </summary>
    public void ApplyPurchaseResult(PurchaseResponse response)
    {
        // 필요 시 창고 인벤토리 갱신 로직 구현
    }
}
