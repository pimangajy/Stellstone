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
    public event Action<S_GameReady> OnGameReadyEvent;
    public event Action<S_PhaseStart> OnPhaseStartEvent;
    public event Action<string> OnPlayCardSuccessEvent;
    public event Action<S_UpdateMana> OnUpdateManaEvent;
    public event Action<S_UpdateEntities> OnUpdateEntitiesEvent;
    public event Action<List<EntityData>> OnEntitiesUpdatedEvent;
    public event Action<S_OpponentPlayCard> OnOpponentPlayCardEvent;
    public event Action<string> OnErrorEvent;
    public event Action<string> OnPlayCardFailedEvent;

    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cts;

    // Firebase 인증 정보
    public FirebaseAuth _auth;
    public string UserUid;

    // ★ [최적화 완료] 문자열(string) 대신, 파싱이 끝난 '객체(BaseGameAction)'를 담습니다.
    private ConcurrentQueue<BaseGameAction> _receivedActions = new ConcurrentQueue<BaseGameAction>();

    [Header("서버 주소")]
    [SerializeField] private string serverIp = "175.125.250.226";
    [SerializeField] private string notebookserverIp = "192.168.0.36";
    [SerializeField] private string serverPort = "5123";
    [SerializeField] private bool useHttps = false;
    [SerializeField] private bool notebook = false;

    [Header("서버 주소")]
    public string BaseUrl => $"{(useHttps ? "https" : "http")}://{(notebook ? notebookserverIp : serverIp)}:{serverPort}";

    /// <summary>
    /// API 호출을 위한 기본 경로
    /// </summary>
    public string BaseApiUrl => $"{BaseUrl}/api";
    public string serverAddress => $"ws://{(notebook ? notebookserverIp : serverIp)}:5123/ws/game";
    public string GameId;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        _auth = FirebaseAuth.DefaultInstance;
        FirebaseUser user = _auth.CurrentUser;
        if (user != null) UserUid = user.UserId;
    }

    void Update()
    {
        // ★ [최적화 완료] 메인 스레드는 JSON 파싱을 하지 않고, 이미 완성된 객체만 꺼내서 사용합니다! (프레임 드랍 방지)
        while (_receivedActions.TryDequeue(out BaseGameAction action))
        {
            HandleServerAction(action);
        }
    }

    async void OnDestroy()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            Debug.Log("[GameClient] 연결 종료 중...");
            _cts.Cancel();
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client shutting down", CancellationToken.None);
            _webSocket.Dispose();
        }
    }

    /// <summary>
    /// 특정 엔드포인트에 대한 전체 URL을 반환합니다.
    /// </summary>
    /// <param name="subPath">e.g., "auth/signup"</param>
    public string GetApiUrl(string subPath)
    {
        // subPath의 시작 부분에 '/'가 있으면 제거하여 중복 방지
        string path = subPath.StartsWith("/") ? subPath.Substring(1) : subPath;
        return $"{BaseApiUrl}/{path}";
    }

    public async void ConnectToServerAsync()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            Debug.LogWarning("[GameClient] 이미 연결되어 있습니다.");
            return;
        }

        FirebaseUser user = _auth.CurrentUser;
        if (user == null)
        {
            Debug.LogError("[GameClient] 연결 실패: 로그인 정보가 없습니다.");
            return;
        }

        string idToken;
        try
        {
            idToken = await user.TokenAsync(true);
            Debug.Log("[GameClient] Firebase 토큰 확보 성공!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient] 토큰 오류: {e.Message}");
            return;
        }

        string fullUrl = $"{serverAddress}?token={idToken}&gameId={GameId}";

        _webSocket = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        try
        {
            Debug.Log($"[GameClient] 서버 연결 시도: {fullUrl}");
            await _webSocket.ConnectAsync(new Uri(fullUrl), _cts.Token);
            Debug.Log("[GameClient] 서버 연결 성공!");

            StartReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient] 연결 실패: {e.Message}");
            _webSocket?.Dispose();
        }
    }

    private async void StartReceiveLoop()
    {
        var buffer = new byte[1024 * 4];

        try
        {
            while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close) break;

                string receivedJson = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // ★ [최적화 완료] 무거운 JSON 파싱 작업을 메인 화면(Update)이 아닌 백그라운드 스레드에서 처리합니다.
                try
                {
                    var baseAction = JsonConvert.DeserializeObject<BaseGameAction>(receivedJson);
                    BaseGameAction parsedAction = null;

                    switch (baseAction.action)
                    {
                        case ActionTypes.MulliganInfo: parsedAction = JsonConvert.DeserializeObject<S_MulliganInfo>(receivedJson); break;
                        case ActionTypes.EnemyMulligunInfo: parsedAction = JsonConvert.DeserializeObject<S_OpponentMulliganStatus>(receivedJson); break;
                        case ActionTypes.GameReady: parsedAction = JsonConvert.DeserializeObject<S_GameReady>(receivedJson); break;
                        case ActionTypes.PhaseStart: parsedAction = JsonConvert.DeserializeObject<S_PhaseStart>(receivedJson); break;
                        case ActionTypes.UpdateMana: parsedAction = JsonConvert.DeserializeObject<S_UpdateMana>(receivedJson); break;
                        case ActionTypes.UpdateEntities: parsedAction = JsonConvert.DeserializeObject<S_UpdateEntities>(receivedJson); break;
                        case ActionTypes.OpponentPlayCard: parsedAction = JsonConvert.DeserializeObject<S_OpponentPlayCard>(receivedJson); break;
                        case ActionTypes.PlayCardSuccess: parsedAction = JsonConvert.DeserializeObject<S_PlayCardSuccess>(receivedJson); break;
                        case ActionTypes.PlayCardFail: parsedAction = JsonConvert.DeserializeObject<S_PlayCardFail>(receivedJson); break;
                        case ActionTypes.GameOver: parsedAction = JsonConvert.DeserializeObject<S_GameOver>(receivedJson); break;
                        case ActionTypes.Error: parsedAction = JsonConvert.DeserializeObject<S_Error>(receivedJson); break;
                    }

                    if (parsedAction != null)
                    {
                        // 파싱이 끝난 깨끗한 객체를 큐에 넣습니다.
                        _receivedActions.Enqueue(parsedAction);
                    }
                }
                catch (Exception parseEx)
                {
                    Debug.LogError($"[GameClient] 파싱 에러: {parseEx.Message}");
                }
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

    // 카드 플레이
    public void SendPlayCardRequest(string cardInstanceId, int slotIndex, int targetEntityId = 0)
    {
        C_PlayCard action = new C_PlayCard
        {
            action = ActionTypes.PlayCard,
            handCardInstanceId = cardInstanceId,
            position = slotIndex,
            targetEntityId = targetEntityId
        };
        SendMessageAsync(action);
    }


    // 공격 전송
    public void SendAttackRequest(int attackerId, int defenderId)
    {
        C_Attack action = new C_Attack
        {
            action = ActionTypes.Attack,
            attackerEntityId = attackerId,
            defenderEntityId = defenderId
        };
        SendMessageAsync(action);
    }

    // 턴엔드 전송
    public void RequestEndTurn()
    {
        C_EndTurn action = new C_EndTurn
        {
            action = ActionTypes.EndTurn,
        };
        SendMessageAsync(action);
    }

    // 서버에 메세지를 보내는 함수
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
    /// [처리하기] 큐에서 꺼낸 완성된 객체를 게임에 반영합니다. (타입 캐스팅만 수행)
    /// </summary>
    private void HandleServerAction(BaseGameAction action)
    {
        switch (action.action)
        {
            case ActionTypes.MulliganInfo:
                Debug.Log("MULLIGAN_INFO 발생");
                OnMulliganInfoReceived((S_MulliganInfo)action);
                break;

            case ActionTypes.EnemyMulligunInfo:
                Debug.Log("상대 멀리건 결정");
                StartCoroutine(OnMulliganInfoReceivedenemy((S_OpponentMulliganStatus)action));
                break;

            case ActionTypes.GameReady:
                Debug.Log("GAME_READY 발생");
                var gameReadyInfo = (S_GameReady)action;
                OnGameReadyEvent?.Invoke(gameReadyInfo);
                OnGameReady(gameReadyInfo);
                break;

            case ActionTypes.PhaseStart:
                Debug.Log("PHASE_START 발생");
                var phaseStartInfo = (S_PhaseStart)action;
                OnPhaseStartEvent?.Invoke(phaseStartInfo);
                OnPhaseStart(phaseStartInfo);
                break;

            case ActionTypes.UpdateMana:
                Debug.Log("UPDATE_MANA 발생");
                var updateManaInfo = (S_UpdateMana)action;
                OnUpdateManaEvent?.Invoke(updateManaInfo);
                OnUpdateMana(updateManaInfo);
                break;

            case ActionTypes.UpdateEntities:
                Debug.Log("UPDATE_ENTITIES 발생");
                var updateEntitiesInfo = (S_UpdateEntities)action;
                OnUpdateEntitiesEvent?.Invoke(updateEntitiesInfo);
                OnEntitiesUpdatedEvent?.Invoke(updateEntitiesInfo.updatedEntities);
                break;

            case ActionTypes.OpponentPlayCard:
                Debug.Log("OPPONENT_PLAY_CARD 발생");
                var opponentPlayCardInfo = (S_OpponentPlayCard)action;
                OnOpponentPlayCardEvent?.Invoke(opponentPlayCardInfo);
                OnOpponentPlayCard(opponentPlayCardInfo);
                break;

            case ActionTypes.PlayCardSuccess:
                Debug.Log("PlayCardSuccess 발생");
                var successInfo = (S_PlayCardSuccess)action;
                OnPlayCardSuccessEvent?.Invoke(successInfo.serverInstanceId);
                break;

            case ActionTypes.PlayCardFail:
                Debug.Log("PLAY_CARD_FAIL 발생");
                var playCardFailInfo = (S_PlayCardFail)action;
                OnPlayCardFailedEvent?.Invoke(playCardFailInfo.reason);
                OnPlayCardFail(playCardFailInfo);
                break;

            case ActionTypes.GameOver:
                Debug.Log("GAME_OVER 발생");
                OnGameOver((S_GameOver)action);
                break;

            case ActionTypes.Error:
                Debug.Log("ERROR 발생");
                var errorInfo = (S_Error)action;
                OnErrorEvent?.Invoke(errorInfo.message);
                Debug.LogError($"[GameClient] 서버 오류: {errorInfo.message}");
                break;
        }
    }

    private void OnMulliganInfoReceived(S_MulliganInfo info)
    {
        Debug.Log($"[GameClient] 멀리건 시작! 교체할 카드 {info.cardsToMulligan.Count}장 받음.");
        if (GameMulliganManager.instance != null)
            GameMulliganManager.instance.mulliganImg.SetActive(true);

        if (CardDrawManager.Instance != null)
            CardDrawManager.Instance.PerformBatchDraw(info.cardsToMulligan);
        if (OpponentHandVisualizer.Instance != null)
            OpponentHandVisualizer.Instance.PerformBatchDraw(info.cardsToMulligan.Count);
    }

    private void OnGameReady(S_GameReady info)
    {
        Debug.Log($"[GameClient] 게임 시작! 내 손패: {info.finalHand.Count}장");
        StartCoroutine(SyncHandWithServer(info.finalHand));
    }

    // 멀리건 받은 카드를 뽄는 함수
    private IEnumerator SyncHandWithServer(List<CardInfo> finalHand)
    {
        var handManager = HandInteractionManager.instance;
        if (handManager == null) yield break;

        foreach (var serverCard in finalHand)
        {
            bool isAlreadyInHand = false;
            foreach (var existingCardObj in handManager.handCards)
            {
                var display = existingCardObj.GetComponent<GameCardDisplay>();
                if (display != null && display.InstanceId == serverCard.instanceId)
                {
                    isAlreadyInHand = true;
                    break;
                }
            }

            if (!isAlreadyInHand)
            {
                if (CardDrawManager.Instance != null)
                    CardDrawManager.Instance.PerformDrawAnimation(serverCard);
                yield return new WaitForSeconds(0.3f);
            }
        }
    }

    // 상대 멀리건 돌아가는 함수
    private IEnumerator OnMulliganInfoReceivedenemy(S_OpponentMulliganStatus info)
    {
        foreach (var mulligan in info.replacedIndices)
        {
            OpponentHandVisualizer.Instance.ReturnCardToDeck(mulligan);
            yield return new WaitForSeconds(0.1f);
        }

        yield return new WaitForSeconds(1.5f);

        OpponentHandVisualizer.Instance.PerformBatchDraw(info.replacedCount);
    }

    // 상대 멀리건을 뽄는 함수
    private IEnumerator SyncHandWithServerenemy(int count)
    {
        for(int i = 0; i < count; i++) 
        {
            OpponentHandVisualizer.Instance.DrawCard();
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void OnPhaseStart(S_PhaseStart info)
    {
        Debug.Log($"[GameClient] 페이즈 시작: {info.phase}");
    }

    private void OnUpdateMana(S_UpdateMana info)
    {
        Debug.Log($"[GameClient] 마나 갱신: {info.currentMana}/{info.maxMana}");
    }

    private void OnOpponentPlayCard(S_OpponentPlayCard info)
    {
        Debug.Log($"[GameClient] 상대방이 카드 사용: {info.cardPlayed.cardId}");
    }

    private void OnPlayCardFail(S_PlayCardFail info)
    {
        Debug.LogWarning($"[GameClient] 카드 내기 실패: {info.reason}");
    }

    private void OnGameOver(S_GameOver info)
    {
        Debug.Log($"[GameClient] 게임 종료! 승자: {info.winnerUid}");
        _cts.Cancel();
    }
}