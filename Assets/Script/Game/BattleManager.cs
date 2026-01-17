using UnityEngine;
using System.Collections;

/// <summary>
/// 대전(Battle) 씬이 시작될 때 가장 먼저 움직이는 관리자입니다.
/// 게임 화면이 켜지면 서버에 "나 들어왔어!"라고 연결을 시도하는 역할을 합니다.
/// </summary>
public class BattleManager : MonoBehaviour
{
    // (선택 사항) 로딩 중일 때 보여줄 UI 패널 등을 연결할 수 있습니다.
    // public GameObject loadingPanel; 

    // Start 함수는 이 스크립트가 켜질 때 딱 한 번 실행됩니다.
    // IEnumerator를 쓴 이유는 '잠깐 대기'하는 기능을 쓰기 위해서입니다.
    IEnumerator Start()
    {
        // 1. 씬이 완전히 로드되고 화면이 안정될 때까지 한 프레임 쉽니다.
        // (급하게 실행하다가 에러 나는 것을 방지합니다)
        yield return null;

        Debug.Log("[BattleManager] 게임 씬 로드 완료. 서버 연결을 시작합니다.");

        // 2. 서버 통신 담당자(GameClient)가 잘 살아있는지 확인합니다.
        if (GameClient.Instance != null)
        {
            // 게임방 번호(GameId)가 있는지 확인합니다.
            // 로비에서 매칭을 안 하고 바로 이 씬을 켜면 GameId가 없을 수 있습니다.
            if (string.IsNullOrEmpty(GameClient.Instance.GameId))
            {
                Debug.LogError("[BattleManager] GameId가 없습니다! 로비에서 매칭 과정을 거치지 않았을 수 있습니다.");
                // 필요하다면 여기서 로비 씬으로 쫓아내는 코드를 넣을 수도 있습니다.
            }
            else
            {
                // 핵심: 모든 준비가 끝났으니 서버에 접속을 시도합니다.
                GameClient.Instance.ConnectToServerAsync();
            }
        }
        else
        {
            Debug.LogError("[BattleManager] GameClient 인스턴스를 찾을 수 없습니다.");
        }
    }
}