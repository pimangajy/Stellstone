using System;
using System.Collections.Concurrent; // (중요) 여러 스레드가 동시에 접근해도 안전한 큐(Queue)를 씁니다.
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json; // JSON 데이터를 다루기 위한 도구
using Firebase.Auth;
using System.Collections.Generic;
using System.Collections;

// 서버와 주고받을 메시지 규격(모델)을 가져옵니다.
// using GameServer; 

/// <summary>
/// 유니티(클라이언트)와 게임 서버 간의 대화를 담당하는 통역사입니다.
/// "카드 냈어", "공격해" 같은 메시지를 보내고, 서버의 응답을 받아서 게임에 반영합니다.
/// </summary>
public class GameClient : MonoBehaviour
{
    // 싱글톤 패턴: 이 클래스는 게임 내에 단 하나만 존재해야 합니다.
    public static GameClient Instance { get; private set; }

    // --- 이벤트 정의 (방송국) ---
    // 서버에서 무슨 일이 생기면 이 이벤트들을 통해 다른 스크립트들에게 알립니다.
    // 예: "게임 시작한대!", "네 턴이야!", "상대가 카드 냈어!"
    public event Action<S_GameReady> OnGameReadyEvent;
    public event Action<S_PhaseStart> OnPhaseStartEvent;
    public event Action<S_UpdateMana> OnUpdateManaEvent;
    public event Action<List<EntityData>> OnEntitiesUpdatedEvent;
    public event Action<S_OpponentPlayCard> OnOpponentPlayCardEvent;
    public event Action<string> OnErrorEvent;

    // WebSocket: 서버와 연결된 전화기 같은 것입니다.
    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cts; // 연결을 취소하거나 끊을 때 사용하는 신호

    // Firebase 인증 정보 (로그인한 유저 정보)
    private FirebaseAuth _auth;

    // (핵심) 메시지 보관함 (큐)
    // 서버에서 오는 메시지는 '별도의 스레드(일꾼)'가 받습니다.
    // 하지만 유니티 화면(UI 등)은 '메인 스레드'만 건드릴 수 있습니다.
    // 그래서 받은 메시지를 이 상자에 잠시 넣어두고, 메인 스레드가 나중에 꺼내서 처리합니다.
    private ConcurrentQueue<string> _receivedMessages = new ConcurrentQueue<string>();

    [Header("서버 주소")]
    public string serverAddress = "ws://175.125.250.226:5123/ws/game";

    // 매칭된 게임 방 번호
    public string GameId;

    void Awake()
    {
        // 싱글톤 초기화: 나 말고 다른 GameClient가 있으면 그 녀석을 없앱니다.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않게 설정
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // --- 1. 유니티 생명주기 (메인 스레드) ---

    void Start()
    {
        // Firebase 로그인 도구를 준비합니다.
        _auth = FirebaseAuth.DefaultInstance;
    }

    void Update()
    {
        // (핵심) 매 프레임마다 "서버에서 온 메시지 있나?" 확인합니다.
        // 큐(보관함)에서 메시지를 하나씩 꺼내서 처리(HandleServerMessage)합니다.
        while (_receivedMessages.TryDequeue(out string message))
        {
            HandleServerMessage(message);
        }
    }

    // 게임이 꺼지거나 이 오브젝트가 사라질 때 연결을 끊습니다.
    async void OnDestroy()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            Debug.Log("[GameClient] 연결 종료 중...");
            _cts.Cancel(); // 수신 중단 신호
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client shutting down", CancellationToken.None);
            _webSocket.Dispose(); // 정리
        }
    }

    // --- 2. 서버 연결 및 수신 (백그라운드 작업) ---

    /// <summary>
    /// 서버에 접속을 시도합니다.
    /// </summary>
    public async void ConnectToServerAsync()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            Debug.LogWarning("[GameClient] 이미 연결되어 있습니다.");
            return;
        }

        // 로그인한 유저인지 확인
        FirebaseUser user = _auth.CurrentUser;
        if (user == null)
        {
            Debug.LogError("[GameClient] 연결 실패: 로그인 정보가 없습니다.");
            return;
        }

        string idToken;
        try
        {
            // 서버 입장권(토큰)을 발급받습니다.
            idToken = await user.TokenAsync(true);
            Debug.Log("[GameClient] Firebase 토큰 확보 성공!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient] 토큰 오류: {e.Message}");
            return;
        }

        // 주소 뒤에 토큰과 방 번호를 붙여서 접속 요청을 보냅니다.
        string fullUrl = $"{serverAddress}?token={idToken}&gameId={GameId}";

        _webSocket = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        try
        {
            Debug.Log($"[GameClient] 서버 연결 시도: {fullUrl}");
            await _webSocket.ConnectAsync(new Uri(fullUrl), _cts.Token);
            Debug.Log("[GameClient] 서버 연결 성공!");

            // 연결되자마자 메시지 수신 대기 상태로 들어갑니다.
            StartReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient] 연결 실패: {e.Message}");
            _webSocket?.Dispose();
        }
    }

    /// <summary>
    /// 서버에서 오는 메시지를 계속해서 듣는 귀(Loop)입니다.
    /// </summary>
    private async void StartReceiveLoop()
    {
        var buffer = new byte[1024 * 4]; // 메시지를 담을 그릇 (4KB)

        try
        {
            // 연결이 끊기지 않는 한 계속 반복합니다.
            while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                // 메시지가 올 때까지 여기서 대기합니다 (await)
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                // 서버가 "끊자"고 하면 루프 종료
                if (result.MessageType == WebSocketMessageType.Close) break;

                // 받은 바이트(byte) 데이터를 문자열(string)로 변환
                string receivedJson = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // (중요) 바로 처리하지 않고 큐(보관함)에 넣습니다. (Update 함수가 처리하도록)
                _receivedMessages.Enqueue(receivedJson);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient] 수신 오류: {e.Message}");
        }
        finally
        {
            _webSocket?.Dispose();
        }
    }

    // --- 3. 메시지 보내기 및 처리 ---

    /// <summary>
    /// [보내기] "나 이 카드 낼래!" 라고 서버에 요청합니다.
    /// </summary>
    public void SendPlayCardRequest(string cardInstanceId, int slotIndex, int targetEntityId = 0)
    {
        C_PlayCard action = new C_PlayCard
        {
            action = "PLAY_CARD",
            handCardInstanceId = cardInstanceId,
            position = slotIndex,
            targetEntityId = targetEntityId
        };

        SendMessageAsync(action);
    }

    /// <summary>
    /// [보내기] "공격해!" 라고 서버에 요청합니다.
    /// </summary>
    public void SendAttackRequest(int attackerId, int defenderId)
    {
        C_Attack action = new C_Attack
        {
            action = "ATTACK",
            attackerEntityId = attackerId,
            defenderEntityId = defenderId
        };
        SendMessageAsync(action);
    }

    /// <summary>
    /// 실제 메시지 전송 함수 (JSON으로 변환해서 보냄)
    /// </summary>
    public async void SendMessageAsync(BaseGameAction actionMessage)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;

        try
        {
            string jsonMessage = JsonConvert.SerializeObject(actionMessage);
            byte[] buffer = Encoding.UTF8.GetBytes(jsonMessage);

            await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient] 전송 실패: {e.Message}");
        }
    }

    /// <summary>
    /// [처리하기] 큐에서 꺼낸 메시지를 분석해서 게임에 반영합니다.
    /// </summary>
    private void HandleServerMessage(string jsonMessage)
    {
        // 1. 일단 '어떤 종류의 행동(action)'인지 확인하기 위해 기본 형태로 풉니다.
        var baseAction = JsonConvert.DeserializeObject<BaseGameAction>(jsonMessage);

        // 2. 종류에 따라 제대로 된 클래스로 다시 풀어서 처리합니다.
        switch (baseAction.action)
        {
            case "MULLIGAN_INFO": // 멀리건(첫 패 교환) 시작
                var mulliganInfo = JsonConvert.DeserializeObject<S_MulliganInfo>(jsonMessage);
                OnMulliganInfoReceived(mulliganInfo);
                break;

            case "GAME_READY": // 게임 준비 완료 (멀리건 끝)
                var gameReadyInfo = JsonConvert.DeserializeObject<S_GameReady>(jsonMessage);
                OnGameReadyEvent?.Invoke(gameReadyInfo); // 이벤트 방송
                OnGameReady(gameReadyInfo);
                break;

            case "PHASE_START": // 턴/페이즈 시작
                var phaseStartInfo = JsonConvert.DeserializeObject<S_PhaseStart>(jsonMessage);
                OnPhaseStartEvent?.Invoke(phaseStartInfo);
                OnPhaseStart(phaseStartInfo);
                break;

            case "UPDATE_MANA": // 마나 정보 갱신
                var updateManaInfo = JsonConvert.DeserializeObject<S_UpdateMana>(jsonMessage);
                OnUpdateManaEvent?.Invoke(updateManaInfo);
                OnUpdateMana(updateManaInfo);
                break;

            case "UPDATE_ENTITIES": // 필드 하수인/영웅 상태 변경
                var updateEntitiesInfo = JsonConvert.DeserializeObject<S_UpdateEntities>(jsonMessage);
                OnEntitiesUpdatedEvent?.Invoke(updateEntitiesInfo.updatedEntities);
                OnUpdateEntities(updateEntitiesInfo);
                break;

            case "OPPONENT_PLAY_CARD": // 상대가 카드 냄
                var opponentPlayCardInfo = JsonConvert.DeserializeObject<S_OpponentPlayCard>(jsonMessage);
                OnOpponentPlayCardEvent?.Invoke(opponentPlayCardInfo);
                OnOpponentPlayCard(opponentPlayCardInfo);
                break;

            case "PLAY_CARD_FAIL": // 내가 낸 카드 실패 (마나 부족 등)
                var playCardFailInfo = JsonConvert.DeserializeObject<S_PlayCardFail>(jsonMessage);
                OnPlayCardFail(playCardFailInfo);
                break;

            case "GAME_OVER": // 게임 끝
                var gameOverInfo = JsonConvert.DeserializeObject<S_GameOver>(jsonMessage);
                OnGameOver(gameOverInfo);
                break;

            case "ERROR": // 서버 에러
                var errorInfo = JsonConvert.DeserializeObject<S_Error>(jsonMessage);
                Debug.LogError($"[GameClient] 서버 오류: {errorInfo.message}");
                break;
        }
    }

    // --- 4. 게임 로직 반영 (실제 UI/연출 호출) ---

    private void OnMulliganInfoReceived(S_MulliganInfo info)
    {
        Debug.Log($"[GameClient] 멀리건 시작! 교체할 카드 {info.cardsToMulligan.Count}장 받음.");

        // 멀리건 UI를 켜줍니다.
        if (GameMulliganManager.instance != null)
        {
            GameMulliganManager.instance.mulliganImg.SetActive(true);
        }

        // 카드를 화면에 그려줍니다.
        if (CardDrawManager.Instance != null)
        {
            CardDrawManager.Instance.PerformBatchDraw(info.cardsToMulligan);
        }
    }

    private void OnGameReady(S_GameReady info)
    {
        Debug.Log($"[GameClient] 게임 시작! 내 손패: {info.finalHand.Count}장");
        // 서버의 확정된 손패와 내 화면의 손패를 맞추는 작업(동기화)을 합니다.
        StartCoroutine(SyncHandWithServer(info.finalHand));
    }

    // 서버 손패 정보와 내 화면을 맞추는 함수
    private IEnumerator SyncHandWithServer(List<CardInfo> finalHand)
    {
        var handManager = HandInteractionManager.instance;
        if (handManager == null) yield break;

        foreach (var serverCard in finalHand)
        {
            bool isAlreadyInHand = false;

            // 이미 내 손에 있는 카드인지 확인
            foreach (var existingCardObj in handManager.handCards)
            {
                var display = existingCardObj.GetComponent<GameCardDisplay>();
                if (display != null && display.InstanceId == serverCard.instanceId)
                {
                    isAlreadyInHand = true;
                    break;
                }
            }

            // 내 손에 없으면 새로 뽑는 연출 실행
            if (!isAlreadyInHand)
            {
                if (CardDrawManager.Instance != null)
                {
                    CardDrawManager.Instance.PerformDrawAnimation(serverCard);
                }
                yield return new WaitForSeconds(0.3f); // 드로우 간격
            }
        }
    }

    private void OnPhaseStart(S_PhaseStart info)
    {
        Debug.Log($"[GameClient] 페이즈 시작: {info.phase}");
        // 드로우 페이즈면 카드 뽑기
        if (info.phase == "Draw" && info.drawnCard != null)
        {
            Debug.Log($"[GameClient] 카드 드로우: {info.drawnCard.cardId}");
            // 실제 드로우 연출은 여기서 추가해야 함
        }
    }

    private void OnUpdateMana(S_UpdateMana info)
    {
        Debug.Log($"[GameClient] 마나 갱신: {info.currentMana}/{info.maxMana}");
        // TODO: 마나 수정(Crystal) UI 갱신 코드 추가 필요
    }

    private void OnUpdateEntities(S_UpdateEntities info)
    {
        // 필드 상황(하수인 등장/사망/체력변경)을 매니저에게 전달
        if (GameEntityManager.Instance == null) return;

        foreach (var entityData in info.updatedEntities)
        {
            // 이 개체가 '내 것'인지 확인 (내 UID와 비교)
            bool isMine = (entityData.ownerUid == _auth.CurrentUser.UserId);

            // 엔티티 매니저에게 처리를 위임
            GameEntityManager.Instance.HandleEntityUpdate(entityData, isMine);
        }
    }

    private void OnOpponentPlayCard(S_OpponentPlayCard info)
    {
        Debug.Log($"[GameClient] 상대방이 카드 사용: {info.cardPlayed.cardId}");
        // TODO: 상대방 손에서 카드가 날아와서 필드에 놓이는 연출 추가 필요
    }

    private void OnPlayCardFail(S_PlayCardFail info)
    {
        Debug.LogWarning($"[GameClient] 카드 내기 실패: {info.reason}");
        // TODO: 실패 알림 메시지나 효과음 재생
    }

    private void OnGameOver(S_GameOver info)
    {
        Debug.Log($"[GameClient] 게임 종료! 승자: {info.winnerUid}");
        // 연결 종료
        _cts.Cancel();
        // TODO: 승리/패배 팝업 띄우기
    }
}