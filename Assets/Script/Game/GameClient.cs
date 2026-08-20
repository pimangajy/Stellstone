using System;
using System.Collections;
using System.Collections.Concurrent; // (중요) 여러 스레드가 동시에 접근해도 안전한 큐(Queue)를 씁니다.
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Auth;
using Newtonsoft.Json; // JSON 데이터를 다루기 위한 도구
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

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
    public event Action<S_GameReady> OnGameReadyEvent;                                   // 게임 시작 패킷
    public event Action<S_PhaseStart> OnPhaseStartEvent;                                 // 페이즈 시작 패킷
    public event Action<string> OnPlayCardSuccessEvent;                                  // 카드 사용 성공 패킷
    public event Action<S_ActionResolution> OnActionResolutionEvent;                     // 카드 사용 효과 패킷
    public event Action<S_UpdateMana> OnUpdateManaEvent;                                 // 마나 없데이트 패킷
    public event Action<S_UpdateHandCards> OnUpdateHandCardsEvent;                       // 손패 업데이트 패킷
    public event Action<S_ValidTargetResponse> validTargetResponse;                      // 효과 타겟 패킷
    public event Action<S_ValidAttackTargetsResponse> validAttackTargetsResponse;        // 공격 타겟 패킷 
    public event Action<S_UpdateEntities> OnUpdateEntitiesEvent;                         // 필드 상태 패킷
    public event Action<S_OpponentPlayCard> OnOpponentPlayCardEvent;                     // 상대 카드 플레이 패킷
    public event Action<string> OnErrorEvent;                                            // 에러 패킷
    public event Action<string> OnPlayCardFailedEvent;                                   // 카드 사용 실패 패킷
    public event Action<S_GameOver> OnGameOverEvent;                                     // 게임 종료 패킷 (승자/패배/항복)

    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cts;

    // Firebase 인증 정보
    public FirebaseAuth _auth;
    public string UserUid;

    // ★ [최적화 완료] 문자열(string) 대신, 파싱이 끝난 '객체(BaseGameAction)'를 담습니다.
    private ConcurrentQueue<BaseGameAction> _receivedActions = new ConcurrentQueue<BaseGameAction>();
    // [디버그]
    private ConcurrentQueue<BaseDebugAction> _debugdActions = new ConcurrentQueue<BaseDebugAction>();

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

        // [디버그]
        while (_debugdActions.TryDequeue(out BaseDebugAction action))
        {
            HandleDebugAction(action);
        }
    }

    public void DebugCheckSubscribers()
    {
        if (OnActionResolutionEvent == null)
        {
            Debug.Log("<color=yellow> OnActionResolutionEvent 구독된 함수가 없습니다.</color>");
            return;
        }

        // event 키워드가 있어도 클래스 내부에서는 GetInvocationList() 사용 가능!
        Delegate[] subscribers = OnActionResolutionEvent.GetInvocationList();

        Debug.Log($"<color=cyan>=== OnActionResolutionEvent 구독 목록 ({subscribers.Length}개) ===</color>");
        foreach (Delegate d in subscribers)
        {
            // d.Target: 함수가 속한 스크립트/클래스의 인스턴스
            // d.Method.Name: 구독된 실제 함수 이름
            Debug.Log($"[오브젝트]: {d.Target} | [함수명]: {d.Method.Name}");
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
            Debug.LogWarning("[GameClient] 이미 서버에 연결되어 있습니다.");
            return;
        }

        // 이전 웹소켓 정리
        try
        {
            _webSocket?.Dispose();
            _webSocket = null;
        }
        catch { }

        if (_auth == null)
        {
            _auth = FirebaseAuth.DefaultInstance;
        }

        FirebaseUser user = _auth?.CurrentUser;
        if (user == null)
        {
            Debug.LogError("[GameClient] ❌ 연결 실패: 로그인된 Firebase 사용자 정보가 없습니다.");
            return;
        }

        UserUid = user.UserId;

        string idToken;
        try
        {
            idToken = await user.TokenAsync(true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient] ❌ 토큰 획득 오류: {e.Message}");
            return;
        }

        string fullUrl = $"{serverAddress}?token={idToken}&gameId={GameId}";
        Debug.Log($"[GameClient] 🌐 서버 웹소켓 연결 시도... (GameId: {GameId})\nURL: {fullUrl}");

        _webSocket = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        try
        {
            await _webSocket.ConnectAsync(new Uri(fullUrl), _cts.Token);
            Debug.Log("[GameClient] ✅ 서버 웹소켓 연결 성공! 게임 메시지 수신을 시작합니다.");
            StartReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameClient] ❌ 웹소켓 연결 실패 ({fullUrl}): {e.Message}");
            _webSocket?.Dispose();
            _webSocket = null;
        }
    }

    private async void StartReceiveLoop()
    {
        var buffer = new byte[1024 * 4]; // 4KB 청크 버퍼

        try
        {
            while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                // 동적 메모리 스트림을 생성하여 조각난 패킷들을 한데 모읍니다.
                using (var ms = new System.IO.MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

                        // 버퍼에 담긴 조각 데이터를 메모리 스트림에 계속 씁니다.
                        ms.Write(buffer, 0, result.Count);

                    } while (!result.EndOfMessage); // ★ 하나의 메시지가 끝날 때까지 반복해서 수신합니다!

                    if (result.MessageType == WebSocketMessageType.Close) break;

                    // 모인 전체 바이트 배열을 하나의 완벽한 JSON 문자열로 변환합니다.
                    string receivedJson = Encoding.UTF8.GetString(ms.ToArray());

                    // 이제 잘림 없이 온전하게 합쳐진 JSON 전체 데이터가 확보되었습니다!
                    try
                    {
                        // 1. 디버그 액션 처리
                        if (receivedJson.Contains("\"debugAction\""))
                        {
                            // 접두사('d' 등)가 붙어있을 때를 대비한 안전 장치
                            int jsonStartIndex = receivedJson.IndexOfAny(new char[] { '{', '[' });
                            string cleanJson = (jsonStartIndex >= 0) ? receivedJson.Substring(jsonStartIndex) : receivedJson;

                            var baseDebugAction = JsonConvert.DeserializeObject<BaseDebugAction>(cleanJson);
                            BaseDebugAction parsedDebugAction = null;

                            switch (baseDebugAction.debugAction)
                            {
                                case DebugAction.ResponseDeckInfo:
                                    parsedDebugAction = JsonConvert.DeserializeObject<S_DebugResponseDeckInfo>(cleanJson);
                                    break;
                            }

                            if (parsedDebugAction != null)
                            {
                                _debugdActions.Enqueue(parsedDebugAction);
                            }
                        }
                        // 2. 일반 게임 액션 처리
                        else if (receivedJson.Contains("\"action\""))
                        {
                            var baseAction = JsonConvert.DeserializeObject<BaseGameAction>(receivedJson);
                            BaseGameAction parsedAction = null;

                            switch (baseAction.action)
                            {
                                case GameActionType.ACTION_RESOLUTION: parsedAction = JsonConvert.DeserializeObject<S_ActionResolution>(receivedJson); break;
                                case GameActionType.REQUEST_CHOICE: parsedAction = JsonConvert.DeserializeObject<S_RequestChoice>(receivedJson); break;
                                case GameActionType.MULLIGAN_INFO: parsedAction = JsonConvert.DeserializeObject<S_MulliganInfo>(receivedJson); break;
                                case GameActionType.OPPONENT_MULLIGAN_STATUS: parsedAction = JsonConvert.DeserializeObject<S_OpponentMulliganStatus>(receivedJson); break;
                                case GameActionType.GAME_READY: parsedAction = JsonConvert.DeserializeObject<S_GameReady>(receivedJson); break;
                                case GameActionType.PHASE_START: parsedAction = JsonConvert.DeserializeObject<S_PhaseStart>(receivedJson); break;
                                case GameActionType.DRAW_CARD: parsedAction = JsonConvert.DeserializeObject<S_DrawCard>(receivedJson); break;
                                case GameActionType.UPDATE_HAND_CARDS: parsedAction = JsonConvert.DeserializeObject<S_UpdateHandCards>(receivedJson); break;
                                case GameActionType.UPDATE_MANA: parsedAction = JsonConvert.DeserializeObject<S_UpdateMana>(receivedJson); break;
                                case GameActionType.UPDATE_ENTITIES: parsedAction = JsonConvert.DeserializeObject<S_UpdateEntities>(receivedJson); break;
                                case GameActionType.OPPONENT_PLAY_CARD: parsedAction = JsonConvert.DeserializeObject<S_OpponentPlayCard>(receivedJson); break;
                                case GameActionType.VALID_TARGETS_RESPONSE: parsedAction = JsonConvert.DeserializeObject<S_ValidTargetResponse>(receivedJson); break;
                                case GameActionType.REQUEST_TARGET_FOR_PLAY: parsedAction = JsonConvert.DeserializeObject<S_RequestTargetForPlay>(receivedJson); break;
                                case GameActionType.VALID_ATTACK_TARGETS_RESPONSE: parsedAction = JsonConvert.DeserializeObject<S_ValidAttackTargetsResponse>(receivedJson); break;
                                case GameActionType.PLAY_CARD_SUCCESS: parsedAction = JsonConvert.DeserializeObject<S_PlayCardSuccess>(receivedJson); break;
                                case GameActionType.PLAY_CARD_FAIL: parsedAction = JsonConvert.DeserializeObject<S_PlayCardFail>(receivedJson); break;
                                case GameActionType.GAME_OVER: parsedAction = JsonConvert.DeserializeObject<S_GameOver>(receivedJson); break;
                                case GameActionType.ERROR: parsedAction = JsonConvert.DeserializeObject<S_Error>(receivedJson); break;
                            }

                            if (parsedAction != null)
                            {
                                _receivedActions.Enqueue(parsedAction);
                            }
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Debug.LogError($"[GameClient] 파싱 에러: {parseEx.Message}\n에러 발생 원본 문자열: {receivedJson}");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[GameClient] 웹소켓 수신 루프가 정상 종료되었습니다.");
        }
        catch (Exception e)
        {
            if (_cts != null && _cts.IsCancellationRequested)
            {
                Debug.Log("[GameClient] 게임 종료로 인해 웹소켓 수신이 안전하게 중단되었습니다.");
            }
            else
            {
                Debug.LogError($"[GameClient] 수신 오류: {e.Message}");
            }
        }
        finally
        {
            _webSocket?.Dispose();
        }
    }

    // 디버그 전송
    public void SendDebugRequest(int userId, int cardId)
    {
        C_Attack action = new C_Attack
        {
            action = GameActionType.ATTACK,
            attackerEntityId = userId,
            defenderEntityId = cardId
        };
        SendMessageAsync(action);
    }

    // 카드 플레이
    public void SendPlayCardRequest(string cardInstanceId, int slotIndex, int targetEntityId = 0)
    {
        C_PlayCard action = new C_PlayCard
        {
            action = GameActionType.PLAY_CARD,
            handCardInstanceId = cardInstanceId,
            position = slotIndex,
            targetEntityId = targetEntityId
        };
        SendMessageAsync(action);
    }

    // 타겟 목록 요청
    public void SendValidTargetResponse(string cardInstanceId)
    {
        C_ValidTargetRequest action = new C_ValidTargetRequest
        {
            action = GameActionType.VALID_TARGETS_REQUEST,
            CardEntityId = cardInstanceId,
        };
        SendMessageAsync(action);
    }

    // 추가 타겟 요청
    public void SendTargetReauest(string cardInstanceId, int targetEntityid)
    {
        Debug.Log($"{cardInstanceId}카드 사용을 위해 {targetEntityid}을 대상으로 고름");

        C_SelectTargetForPlay action = new C_SelectTargetForPlay
        {
            action = GameActionType.SELECT_TARGET_FOR_PLAY,
            CardEntityId = cardInstanceId,
            selectedEntityId = targetEntityid,
        };
        SendMessageAsync(action);
    }


    // 공격 전송
    public void SendAttackRequest(int attackerId, int defenderId)
    {
        Debug.Log($"{attackerId}이가 {defenderId}을 공격함");
        C_Attack action = new C_Attack
        {
            action = GameActionType.ATTACK,
            attackerEntityId = attackerId,
            defenderEntityId = defenderId
        };
        SendMessageAsync(action);
    }

    // 공격가능한 대상 요청
    public void SendValidAttackTargetsRequest(int attackerId)
    {
        Debug.Log("공격 가능한 대상 요청");
        C_ValidAttackTargetsRequest action = new C_ValidAttackTargetsRequest
        {
            action = GameActionType.VALID_ATTACK_TARGETS_REQUEST,
            attackerEntityId = attackerId,
        };
        SendMessageAsync(action);
    }

    // 턴엔드 전송
    public void RequestEndTurn()
    {
        C_EndTurn action = new C_EndTurn
        {
            action = GameActionType.END_TURN,
        };
        SendMessageAsync(action);
    }

    /// <summary>
    /// 서버에 항복 요청(CONCEDE)을 전송합니다. (인게임 항복 버튼 OnClick에 직접 연결 가능)
    /// </summary>
    public void SendConcedeRequest()
    {
        Debug.Log("[GameClient] 항복 요청(CONCEDE) 전송");
        C_Concede action = new C_Concede
        {
            action = GameActionType.CONCEDE
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

    // 서버에 디버그 메세지를 보내는 함수
    public async void SendDebugMessageAsync(BaseDebugAction actionMessage)
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
            case GameActionType.MULLIGAN_INFO:
                OnMulliganInfoReceived((S_MulliganInfo)action);
                break;

            case GameActionType.OPPONENT_MULLIGAN_STATUS:
                StartCoroutine(OnMulliganInfoReceivedenemy((S_OpponentMulliganStatus)action));
                break;

            case GameActionType.GAME_READY:
                var gameReadyInfo = (S_GameReady)action;
                OnGameReadyEvent?.Invoke(gameReadyInfo);
                OnGameReady(gameReadyInfo);
                break;

            case GameActionType.PHASE_START:
                var phaseStartInfo = (S_PhaseStart)action;
                OnPhaseStartEvent?.Invoke(phaseStartInfo);
                OnPhaseStart(phaseStartInfo);
                break;

            case GameActionType.ACTION_RESOLUTION:
                var actionInfo = (S_ActionResolution)action;
                OnActionResolutionEvent?.Invoke(actionInfo);
                OnActionResolution(actionInfo);
                break;

            case GameActionType.REQUEST_CHOICE:
                var requestInfo = (S_RequestChoice)action;
                OnReqeustChoice(requestInfo);
                break;

            case GameActionType.REQUEST_TARGET_FOR_PLAY:
                var select_Target_For_Play = (S_RequestTargetForPlay)action;
                GameInputManager.Instance.HandleRequestTargetForPlay(select_Target_For_Play);
                break;

            case GameActionType.DRAW_CARD:
                var draw_Card = (S_DrawCard)action;
                SendDrawCard(draw_Card);
                break;

            case GameActionType.UPDATE_HAND_CARDS:
                var updateHandCards = (S_UpdateHandCards)action;
                OnUpdateHandCardsEvent?.Invoke(updateHandCards);
                if (HandCardControllManager.instance != null)
                {
                    HandCardControllManager.instance.UpdateHandCards(updateHandCards.updatedCards);
                }
                break;

            case GameActionType.UPDATE_MANA:
                var updateManaInfo = (S_UpdateMana)action;
                OnUpdateManaEvent?.Invoke(updateManaInfo);
                OnUpdateMana(updateManaInfo);
                break;

            case GameActionType.UPDATE_ENTITIES:
                var updateEntitiesInfo = (S_UpdateEntities)action;
                OnUpdateEntitiesEvent?.Invoke(updateEntitiesInfo);
                break;

            case GameActionType.OPPONENT_PLAY_CARD:
                var opponentPlayCardInfo = (S_OpponentPlayCard)action;
                OnOpponentPlayCardEvent?.Invoke(opponentPlayCardInfo);
                OnOpponentPlayCard(opponentPlayCardInfo);
                break;

            case GameActionType.VALID_TARGETS_RESPONSE:
                var targetList = (S_ValidTargetResponse)action;
                validTargetResponse?.Invoke(targetList);
                break;

            case GameActionType.VALID_ATTACK_TARGETS_RESPONSE:
                var attackTargetList = (S_ValidAttackTargetsResponse)action;
                validAttackTargetsResponse.Invoke(attackTargetList);
                break;

            case GameActionType.PLAY_CARD_SUCCESS:
                var successInfo = (S_PlayCardSuccess)action;
                OnPlayCardSuccessEvent?.Invoke(successInfo.serverInstanceId);
                break;

            case GameActionType.PLAY_CARD_FAIL:
                var playCardFailInfo = (S_PlayCardFail)action;
                OnPlayCardFailedEvent?.Invoke(playCardFailInfo.reason);
                Debug.Log($"{playCardFailInfo.reason}");
                OnPlayCardFail(playCardFailInfo);
                break;

            case GameActionType.GAME_OVER:
                OnGameOver((S_GameOver)action);
                break;

            case GameActionType.ERROR:
                Debug.Log("ERROR 발생");
                var errorInfo = (S_Error)action;
                OnErrorEvent?.Invoke(errorInfo.message);
                Debug.LogError($"[GameClient] 서버 오류: {errorInfo.message}");
                break;
        }
    }

    /// <summary>
    /// [처리하기] 큐에서 꺼낸 완성된 객체를 게임에 반영합니다. (타입 캐스팅만 수행)
    /// </summary>
    private void HandleDebugAction(BaseDebugAction action)
    {
        switch (action.debugAction)
        {
            case DebugAction.ResponseDeckInfo:
                var gameReadyInfo = (S_DebugResponseDeckInfo)action;
                ClientDebugAction.Instance.DebugDeckinfo(gameReadyInfo.deckCards);
                break;

            case DebugAction.SpecificCardDraw:
                
                break;

            

            case DebugAction.NONE:
                Debug.Log("ERROR 발생");
                break;
        }
    }

    private void OnMulliganInfoReceived(S_MulliganInfo info)
    {
        if (GameMulliganManager.instance != null)
            GameMulliganManager.instance.mulliganImg.SetActive(true);

        if (CardDrawManager.Instance != null)
            CardDrawManager.Instance.PerformBatchDraw(info.cardsToMulligan);
        if (OpponentHandVisualizer.Instance != null)
            OpponentHandVisualizer.Instance.PerformBatchDraw(info.cardsToMulligan.Count);
    }

    private void OnGameReady(S_GameReady info)
    {
        StartCoroutine(SyncHandWithServer(info.finalHand));
        GameEntityManager.Instance.SetReader(info);
    }

    // 멀리건 받은 카드를 뽑는 함수
    private IEnumerator SyncHandWithServer(List<CardInfo> finalHand)
    {
        var handManager = HandCardControllManager.instance;
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

        // 멀리건 카드를 전부 뽑은 후 isMulliganPhase를 변경하여 손패각도 정상화
        HandCardControllManager.instance.isMulliganPhase = false;
        HandCardControllManager.instance.isMulligan = false;
    }

    // 상대 멀리건 돌아가는 함수 수정
    private IEnumerator OnMulliganInfoReceivedenemy(S_OpponentMulliganStatus info)
    {
        Debug.Log($"enamy mulligan : {info.replacedCount}");
        int m = 0;

        // 교체할 인덱스 목록을 큰 숫자부터 역순(내림차순)으로 정렬합니다!
        var sortedIndices = info.replacedIndices.OrderByDescending(x => x).ToList();

        foreach (var mulligan in sortedIndices)
        {
            OpponentHandVisualizer.Instance.ReturnCardToDeck(mulligan);
            m++;
            yield return new WaitForSeconds(0.1f);
        }

        yield return new WaitForSeconds(1.5f);

        OpponentHandVisualizer.Instance.PerformBatchDraw(info.replacedCount);
        Debug.Log($"mulligan Count : {m} info.replacedCount : {info.replacedCount}");
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

    }

    private void OnActionResolution(S_ActionResolution info)
    {
        // GameClient에서 코루틴을 돌리지 않고 GameEntityManager로 패킷을 넘깁니다.
        if (GameEntityManager.Instance != null)
        {
            GameEntityManager.Instance.ResolveActionSequence(info);
        }

        // StartCoroutine(SyncActioninfo(info));
    }

    private IEnumerator SyncActioninfo(S_ActionResolution info)
    {
        foreach(var log in info.eventLog)
        {
            switch(log.eventType)
            {
                case GameEventType.SUMMON:
                    if(log.entityData == null)
                    {
                        Debug.Log("EntityData Null!");
                        break;
                    }
                    CardActionQueueManager.Instance.ResolvePlay(log.entityData);
                    HandInteractionManager.instance.AlignHand();
                    break;

                case GameEventType.ATTACK:
                    //GameEntityManager.Instance.HandleEntitiesUpdated(info);
                    Debug.Log($"{info.eventLog[0].sourceEntityId}이가 {info.eventLog[0].targetEntityId}에게 Card Attack!");
                    break;
            }

        }
        yield return new WaitForSeconds(1.0f);
    }

    public void OnReqeustChoice(S_RequestChoice info)
    {
        GameInputManager.Instance.StartChoiceMode(info.choiceType, info.sourceEntityId);
    }

    /// <summary>
    /// 토큰 소환 위치나 대상을 선택하며 C_MakeChoice 패킷으로 만들어 서버에 전송합니다.
    /// </summary>
    public void SendMakeChoiceRequest(int position, string cardId, int entityId)
    {
        // (GameActionModels.cs에 C_MakeChoice가 정의되어 있다고 가정)
        C_MakeChoice request = new C_MakeChoice
        {
            action = GameActionType.MAKE_CHOICE,
            selectedPosition = position,
            selectedCardId = cardId,
            selectedEntityId = entityId
        };

        if (GameClient.Instance != null)
        {
            GameClient.Instance.SendMessageAsync(request);
        }
    }

    /// <summary>
    /// 덱에서 카드를 뽑습니다.
    /// </summary>
    public void SendDrawCard(S_DrawCard draw_Card)
    {
        if(draw_Card.playerUid == UserUid)
        {
            CardDrawManager.Instance.PerformDrawAnimation(draw_Card.drawnCard);
        }else
        {
            OpponentHandVisualizer.Instance.DrawCard();
        }
    }

    private void OnUpdateMana(S_UpdateMana info)
    {

    }

    private void OnOpponentPlayCard(S_OpponentPlayCard info)
    {

    }

    private void OnPlayCardFail(S_PlayCardFail info)
    {

    }

    private void OnGameOver(S_GameOver info)
    {
        Debug.Log($"[GameClient] 🏆 게임 종료 수신! 승자: {info.winnerUid} (종료 사유: {info.reason})");
        OnGameOverEvent?.Invoke(info);
        _cts?.Cancel();
    }
}