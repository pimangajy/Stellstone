using UnityEngine;
using System.Collections;

/// <summary>
/// 대전 씬(Battle Scene)의 라이프사이클을 관리하는 매니저입니다.
/// 씬이 로드된 직후 서버에 연결을 요청합니다.
/// </summary>
public class BattleManager : MonoBehaviour
{
    // (선택) 연결 상태를 UI에 표시하고 싶다면 추가
    // public GameObject loadingPanel; 

    IEnumerator Start()
    {
        // 1. 씬이 로드되고 프레임이 안정화될 때까지 잠시 대기 (선택 사항이지만 권장)
        yield return null;

        Debug.Log("[BattleManager] 게임 씬 로드 완료. 서버 연결을 시작합니다.");

        // 2. GameClient 인스턴스가 존재하는지 확인 후 연결 시도
        if (GameClient.Instance != null)
        {
            // 게임 ID가 설정되어 있는지 확인 (안전장치)
            if (string.IsNullOrEmpty(GameClient.Instance.GameId))
            {
                Debug.LogError("[BattleManager] GameId가 없습니다! 로비에서 매칭 과정을 거치지 않았을 수 있습니다.");
                // 예: 로비로 강제 이동
                // SceneLoader.instance.LoadSceneByName("LobbyScene");
            }
            else
            {
                // 핵심: 씬 로딩이 끝난 이 시점에 서버 연결 요청
                GameClient.Instance.ConnectToServerAsync();
            }
        }
        else
        {
            Debug.LogError("[BattleManager] GameClient 인스턴스를 찾을 수 없습니다.");
        }
    }
}