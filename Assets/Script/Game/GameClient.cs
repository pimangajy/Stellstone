using System;
using System.Collections.Concurrent; // (중요) 스레드 안전 큐
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json; // 2단계에서 설치한 라이브러리
using Firebase.Auth;

// (중요) GameActionModels.cs 파일의 클래스 정의가 필요합니다.
// 서버의 GameActionModels.cs 파일을 유니티 프로젝트에 그대로 복사해오세요.
// using GameServer; // <- 네임스페이스가 동일하다면 이렇게 사용

/// <summary>
/// 유니티에서 C# 게임 서버와 통신하는 메인 클라이언트입니다.
/// </summary>
public class GameClient : MonoBehaviour
{
    // C# 기본 WebSocket 클라이언트
    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cts;

    // Firebase 인증 인스턴스
    private FirebaseAuth _auth;

    // (핵심)
    // WebSocket 메시지는 '백그라운드 스레드'에서 수신됩니다.
    // 하지만 유니티의 오브젝트(GameObject, UI)는 '메인 스레드'에서만 접근할 수 있습니다.
    // 따라서, 수신 스레드는 이 큐(Queue)에 메시지를 넣기만 하고,
    // 유니티의 Update() 함수(메인 스레드)가 큐에서 메시지를 꺼내 처리합니다.
    private ConcurrentQueue<string> _receivedMessages = new ConcurrentQueue<string>();

    [Header("Test Connection")]
    public string serverAddress = "ws://localhost:5123/ws/game";

    // (테스트용) 실제로는 매치메이킹 후 받아와야 합니다.
    public string GameId;

    // --- 1. 유니티 생명주기 (메인 스레드) ---

    void Start()
    {
        // (신규) Firebase Auth 인스턴스를 가져옵니다.
        _auth = FirebaseAuth.DefaultInstance;

        GameId = SceneLoader.instance.gameID;

        // (테스트용) 
        // 이 스크립트가 시작되면 바로 서버에 연결을 시도합니다.
        // 실제 게임에서는 "게임 시작" 버튼을 눌렀을 때 호출해야 합니다.
        ConnectToServerAsync();
    }

    void Update()
    {
        // (핵심) 메인 스레드에서만 실행됨
        // 큐에 처리할 메시지가 있는지 확인합니다.
        while (_receivedMessages.TryDequeue(out string message))
        {
            Debug.Log($"[GameClient]  S -> C 수신 (처리): {message}");
            HandleServerMessage(message);
        }
    }

    async void OnDestroy()
    {
        // 유니티가 종료될 때 WebSocket 연결을 안전하게 닫습니다.
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            Debug.Log("[GameClient] 연결 종료 중...");
            _cts.Cancel(); // 수신 루프 중단
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client shutting down", CancellationToken.None);
            _webSocket.Dispose();
        }
    }

    // --- 2. WebSocket 연결 및 수신 (백그라운드 스레드) ---

    /// <summary>
    /// 서버에 WebSocket 연결을 시도합니다.
    /// </summary>
    public async void ConnectToServerAsync()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            Debug.LogWarning("[GameClient] 이미 연결되어 있습니다.");
            return;
        }

        string idToken;
        FirebaseUser user = _auth.CurrentUser;

        if (user == null)
        {
            Debug.LogError("[GameClient]  연결 실패: Firebase에 로그인한 유저가 없습니다.");
            // TODO: 여기서 로그인 씬으로 돌려보내는 로직이 필요합니다.
            return;
        }

        try
        {
            // (중요) 토큰을 비동기로 가져옵니다. true는 만료 시 강제 갱신을 의미합니다.
            idToken = await user.TokenAsync(true);
            Debug.Log("[GameClient]  Firebase ID 토큰 가져오기 성공!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient]  토큰 가져오기 실패: {e.Message}");
            return;
        }
        // --- (핵심 수정 끝) ---

        // (중요) 서버가 요구하는 인증 파라미터를 URL에 추가합니다.
        string fullUrl = $"{serverAddress}?token={idToken}&gameId={GameId}";

        _webSocket = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        try
        {
            Debug.Log($"[GameClient] 서버 연결 시도: {fullUrl}");
            await _webSocket.ConnectAsync(new Uri(fullUrl), _cts.Token);
            Debug.Log("[GameClient]  서버 연결 성공!");

            // (핵심) 연결 성공 시, 즉시 '메시지 수신' 루프를 시작합니다.
            StartReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient]  연결 실패: {e.Message}");
            _webSocket?.Dispose();
        }
    }

    /// <summary>
    /// 메시지를 '지속적으로' 수신하는 비동기 루프를 시작합니다.
    /// (이 함수는 백그라운드 스레드에서 실행됩니다)
    /// </summary>
    private async void StartReceiveLoop()
    {
        var buffer = new byte[1024 * 4];

        try
        {
            // 연결이 열려있는 동안 계속 수신 대기
            while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.LogWarning("[GameClient]  서버가 연결을 닫았습니다.");
                    break;
                }

                string receivedJson = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // (핵심) 메시지를 즉시 처리하지 않고, 메인 스레드용 큐에 넣습니다.
                _receivedMessages.Enqueue(receivedJson);
            }
        }
        catch (OperationCanceledException)
        {
            // (정상) _cts.Cancel() 호출 시
            Debug.Log("[GameClient] 수신 루프가 정상적으로 중단되었습니다.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient]  수신 루프 오류: {e.Message}");
        }
        finally
        {
            _webSocket?.Dispose();
        }
    }

    // --- 3. 메시지 전송 및 처리 (C -> S / S -> C) ---

    /// <summary>
    /// (메인 스레드 -> 백그라운드) 서버로 JSON 메시지를 전송합니다.
    /// (주의: BaseGameAction을 상속받는 C_MulliganDecision 등)
    /// </summary>
    public async void SendMessageAsync(BaseGameAction actionMessage)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            Debug.LogError("[GameClient]  메시지를 보내려 했으나, 연결이 끊겨있습니다.");
            return;
        }

        try
        {
            string jsonMessage = JsonConvert.SerializeObject(actionMessage);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonMessage);

            await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            Debug.Log($"[GameClient] C -> S 전송: {jsonMessage}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient]  메시지 전송 실패: {e.Message}");
        }
    }

    /// <summary>
    /// (메인 스레드) 큐에서 꺼내온 메시지를 실제 '처리'하는 함수입니다.
    /// </summary>
    private void HandleServerMessage(string jsonMessage)
    {
        // 1. 먼저 'action' 필드만 확인 (어떤 종류의 메시지인지)
        var baseAction = JsonConvert.DeserializeObject<BaseGameAction>(jsonMessage);

        // 2. 'action'에 따라 전체 메시지를 다시 파싱
        switch (baseAction.action)
        {
            case "MULLIGAN_INFO":
                var mulliganInfo = JsonConvert.DeserializeObject<S_MulliganInfo>(jsonMessage);
                OnMulliganInfoReceived(mulliganInfo);
                break;

            case "GAME_READY":
                var gameReadyInfo = JsonConvert.DeserializeObject<S_GameReady>(jsonMessage);
                OnGameReady(gameReadyInfo);
                break;

            case "PHASE_START":
                var phaseStartInfo = JsonConvert.DeserializeObject<S_PhaseStart>(jsonMessage);
                OnPhaseStart(phaseStartInfo);
                break;

            case "UPDATE_MANA":
                var updateManaInfo = JsonConvert.DeserializeObject<S_UpdateMana>(jsonMessage);
                OnUpdateMana(updateManaInfo);
                break;

            case "UPDATE_ENTITIES":
                var updateEntitiesInfo = JsonConvert.DeserializeObject<S_UpdateEntities>(jsonMessage);
                OnUpdateEntities(updateEntitiesInfo);
                break;

            case "OPPONENT_PLAY_CARD":
                var opponentPlayCardInfo = JsonConvert.DeserializeObject<S_OpponentPlayCard>(jsonMessage);
                OnOpponentPlayCard(opponentPlayCardInfo);
                break;

            case "PLAY_CARD_FAIL":
                var playCardFailInfo = JsonConvert.DeserializeObject<S_PlayCardFail>(jsonMessage);
                OnPlayCardFail(playCardFailInfo);
                break;

            case "GAME_OVER":
                var gameOverInfo = JsonConvert.DeserializeObject<S_GameOver>(jsonMessage);
                OnGameOver(gameOverInfo);
                break;

            case "ERROR":
                var errorInfo = JsonConvert.DeserializeObject<S_Error>(jsonMessage);
                Debug.LogError($"[GameClient]  서버 오류: {errorInfo.message}");
                break;

            default:
                Debug.LogWarning($"[GameClient]  알 수 없는 action 수신: {baseAction.action}");
                break;
        }
    }

    // --- 4. (유니티) 실제 게임 로직 연동 ---
    // (여기서부터 실제 유니티의 카드 UI, 필드 등을 조작합니다)

    private void OnMulliganInfoReceived(S_MulliganInfo info)
    {
        // TODO: 멀리건 UI를 띄웁니다.
        // info.cardsToMulligan 리스트를 사용해 5장의 카드를 화면에 그립니다.
        Debug.Log($"[GameClient] 멀리건 시작! 교체할 카드 {info.cardsToMulligan.Count}장 받음.");

        // (테스트용) 1초 후, 아무것도 교체하지 않음
        Invoke("SendMulliganDecision", 1.0f);
    }

    // (테스트용)
    void SendMulliganDecision()
    {
        var decision = new C_MulliganDecision
        {
            action = "MULLIGAN_DECISION",
            cardInstanceIdsToReplace = new System.Collections.Generic.List<string>() // 아무것도 바꾸지 않음
        };
        SendMessageAsync(decision);
    }

    private void OnGameReady(S_GameReady info)
    {
        // TODO: 멀리건 UI를 닫고, 게임 보드를 활성화합니다.
        // info.finalHand 리스트를 사용해 내 손패를 그립니다.
        Debug.Log($"[GameClient] 게임 준비 완료! 선공: {info.firstPlayerUid}. 내 최종 손패: {info.finalHand.Count}장");
    }

    private void OnPhaseStart(S_PhaseStart info)
    {
        Debug.Log($"[GameClient] 페이즈 시작: {info.phase}");
        if (info.phase == "Draw" && info.drawnCard != null)
        {
            // TODO: 내 덱에서 info.drawnCard를 내 손으로 가져오는 애니메이션
            Debug.Log($"[GameClient] {info.drawnCard.cardId} ({info.drawnCard.instanceId}) 카드를 뽑았습니다.");
        }
        if (info.phase == "Main")
        {
            // TODO: 턴 종료 버튼 활성화
            Debug.Log($"[GameClient] 메인 페이즈 시작. 턴 종료 시간: {info.turnEndTime}");
        }
    }

    private void OnUpdateMana(S_UpdateMana info)
    {
        // TODO: 내 마나 UI 갱신
        Debug.Log($"[GameClient] 마나 갱신: {info.currentMana}/{info.maxMana}");
    }

    private void OnUpdateEntities(S_UpdateEntities info)
    {
        // (가장 중요)
        // TODO: 필드 위의 개체(하수인, 리더, 멤버) 상태를 갱신합니다.
        // info.updatedEntities 리스트를 순회하며
        // - ID가 없으면 -> 새로 소환
        // - ID가 있는데 health <= 0 이면 -> 죽음 처리
        // - ID가 있으면 -> 체력/공격력/상태(CanAttack) 갱신
        Debug.Log($"[GameClient] {info.updatedEntities.Count}개의 개체 상태 갱신됨.");
    }

    private void OnOpponentPlayCard(S_OpponentPlayCard info)
    {
        // TODO: 상대방이 info.cardPlayed를 낸 것처럼 애니메이션 처리
        Debug.Log($"[GameClient] 상대가 {info.cardPlayed.cardId} 카드를 냈습니다.");
    }

    private void OnPlayCardFail(S_PlayCardFail info)
    {
        // TODO: 카드 내기 실패 UI (예: 카드 붉은색으로 반짝)
        Debug.LogWarning($"[GameClient]  카드 내기 실패: {info.reason}");
    }

    private void OnGameOver(S_GameOver info)
    {
        // TODO: 게임 종료 UI (승리/패배) 표시
        Debug.Log($"[GameClient]  게임 종료! 승자: {info.winnerUid}. 사유: {info.reason}");
        // (연결 자동 종료)
        _cts.Cancel();
    }
}
