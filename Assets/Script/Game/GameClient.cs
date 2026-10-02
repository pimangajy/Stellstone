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
    public event Action<S_ValidMemberSkillTargetsResponse> OnValidMemberSkillTargetsResponseEvent; // 멤버 스킬 조준 대상 목록 패킷
    public event Action<S_UseMemberSkillSuccess> OnUseMemberSkillSuccessEvent;                     // 멤버 스킬 사용 성공 패킷
    public event Action<S_UseMemberSkillFail> OnUseMemberSkillFailEvent;                           // 멤버 스킬 사용 실패 패킷
    public event Action<S_GetCardFromSideDeckSuccess> OnGetCardFromSideDeckSuccessEvent;           // 사이드덱 카드 가져오기 성공 패킷
    public event Action<S_GetCardFromSideDeckFail> OnGetCardFromSideDeckFailEvent;                 // 사이드덱 카드 가져오기 실패 패킷
    public event Action<S_CardCreated> OnCardCreatedEvent;                                         // 카드 생성(손패 획득) 알림 패킷
    public event Action<S_ReceiveEmote> OnReceiveEmoteEvent;                                       // 감정표현 수신 패킷
    public event Action<S_NewLogEvent> OnNewLogEvent;                                             // 실시간 행동 로그 수신 패킷

    // --- 유저 계정 및 아이템 데이터 관리 ---
    public UserData CurrentUser { get; private set; }
    public event Action<UserData> OnUserDataUpdated;
    public List<ProductData> AllLeaderSkins { get; private set; } = new List<ProductData>();

    // --- 유저 닉네임 및 상대방 닉네임 관리 ---
    public string MyUsername => !string.IsNullOrEmpty(CurrentUser?.username) ? CurrentUser.username : (!string.IsNullOrEmpty(SinginManager.CurrentUserData?.username) ? SinginManager.CurrentUserData.username : "나");
    public string OpponentUsername { get; set; } = "상대방";

    private ClientWebSocket _webSocket;
    public bool IsConnected => _webSocket != null && _webSocket.State == System.Net.WebSockets.WebSocketState.Open;
    private CancellationTokenSource _cts;

    // Firebase 인증 정보
    public FirebaseAuth _auth;
    public string UserUid;

    // ★ [최적화 완료] 문자열(string) 대신, 파싱이 끝난 '객체(BaseGameAction)'를 담습니다.
    private ConcurrentQueue<BaseGameAction> _receivedActions = new ConcurrentQueue<BaseGameAction>();
    // [디버그]
    private ConcurrentQueue<BaseDebugAction> _debugdActions = new ConcurrentQueue<BaseDebugAction>();

    [Header("테스트 및 디버그 설정")]
    [Tooltip("true일 경우 직업 제한 및 보유 여부와 관계없이 모든 이모션이 감정표현 창에 표시되며 장착 가능합니다.")]
    public bool unlockAllEmotesForTest = true;

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
            // 수신 패킷 조립용 메모리 스트림 (매 패킷마다 재할당하지 않고 재사용)
            using (var ms = new System.IO.MemoryStream())
            {
                while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    ms.SetLength(0);

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

                    // 모인 전체 바이트 데이터를 하나의 완벽한 JSON 문자열로 변환 (ms.ToArray()의 불필요한 바이트 배열 복사/할당 제거)
                    string receivedJson = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);

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
                                case GameActionType.DRAW_CARD: 
                                    Debug.Log($"<color=cyan>[GameClient] 📥 DRAW_CARD 패킷 수신 (Raw JSON):</color> {receivedJson}");
                                    parsedAction = JsonConvert.DeserializeObject<S_DrawCard>(receivedJson); 
                                    break;
                                case GameActionType.UPDATE_HAND_CARDS: parsedAction = JsonConvert.DeserializeObject<S_UpdateHandCards>(receivedJson); break;
                                case GameActionType.UPDATE_MANA: parsedAction = JsonConvert.DeserializeObject<S_UpdateMana>(receivedJson); break;
                                case GameActionType.UPDATE_ENTITIES: parsedAction = JsonConvert.DeserializeObject<S_UpdateEntities>(receivedJson); break;
                                case GameActionType.OPPONENT_PLAY_CARD: parsedAction = JsonConvert.DeserializeObject<S_OpponentPlayCard>(receivedJson); break;
                                case GameActionType.VALID_TARGETS_RESPONSE: parsedAction = JsonConvert.DeserializeObject<S_ValidTargetResponse>(receivedJson); break;
                                case GameActionType.REQUEST_TARGET_FOR_PLAY: parsedAction = JsonConvert.DeserializeObject<S_RequestTargetForPlay>(receivedJson); break;
                                case GameActionType.VALID_ATTACK_TARGETS_RESPONSE: parsedAction = JsonConvert.DeserializeObject<S_ValidAttackTargetsResponse>(receivedJson); break;
                                case GameActionType.PLAY_CARD_SUCCESS: parsedAction = JsonConvert.DeserializeObject<S_PlayCardSuccess>(receivedJson); break;
                                case GameActionType.PLAY_CARD_FAIL: parsedAction = JsonConvert.DeserializeObject<S_PlayCardFail>(receivedJson); break;
                                case GameActionType.VALID_MEMBER_SKILL_TARGETS_RESPONSE: parsedAction = JsonConvert.DeserializeObject<S_ValidMemberSkillTargetsResponse>(receivedJson); break;
                                case GameActionType.USE_MEMBER_SKILL_SUCCESS: parsedAction = JsonConvert.DeserializeObject<S_UseMemberSkillSuccess>(receivedJson); break;
                                case GameActionType.USE_MEMBER_SKILL_FAIL: parsedAction = JsonConvert.DeserializeObject<S_UseMemberSkillFail>(receivedJson); break;
                                case GameActionType.GAME_OVER: parsedAction = JsonConvert.DeserializeObject<S_GameOver>(receivedJson); break;
                                case GameActionType.ERROR: parsedAction = JsonConvert.DeserializeObject<S_Error>(receivedJson); break;
                                case GameActionType.GET_CARD_FROM_SIDE_DECK_SUCCESS: parsedAction = JsonConvert.DeserializeObject<S_GetCardFromSideDeckSuccess>(receivedJson); break;
                                case GameActionType.GET_CARD_FROM_SIDE_DECK_FAIL: parsedAction = JsonConvert.DeserializeObject<S_GetCardFromSideDeckFail>(receivedJson); break;
                                case GameActionType.CARD_CREATED: parsedAction = JsonConvert.DeserializeObject<S_CardCreated>(receivedJson); break;
                                case GameActionType.RECEIVE_EMOTE: parsedAction = JsonConvert.DeserializeObject<S_ReceiveEmote>(receivedJson); break;
                                case GameActionType.NEW_LOG_EVENT: parsedAction = JsonConvert.DeserializeObject<S_NewLogEvent>(receivedJson); break;
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

    /// <summary>
    /// 멤버 카드 사용(소환) 요청을 서버에 전송합니다.
    /// 멤버 카드는 항상 MemberZone[0] 슬롯에 배치되므로 position은 0으로 고정됩니다.
    /// </summary>
    public void SendPlayMemberCardRequest(string cardInstanceId, int targetEntityId = 0)
    {
        SendPlayCardRequest(cardInstanceId, 0, targetEntityId);
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

    /// <summary>
    /// 멤버 스킬의 유효 타겟 목록을 서버에 요청합니다.
    /// </summary>
    public void SendValidMemberSkillTargetsRequest(int entityId, int skillId)
    {
        var action = new C_ValidMemberSkillTargetsRequest
        {
            entityId = entityId,
            skillId = skillId
        };
        SendMessageAsync(action);
    }

    /// <summary>
    /// 멤버 스킬 시전을 서버에 요청합니다.
    /// </summary>
    public void SendUseMemberSkill(int entityId, int skillId, int targetEntityId = 0)
    {
        var action = new C_UseMemberSkill
        {
            entityId = entityId,
            skillId = skillId,
            targetEntityId = targetEntityId
        };
        SendMessageAsync(action);
    }

    /// <summary>
    /// 사이드덱에서 원하는 카드를 손패로 가져오도록 서버에 요청합니다.
    /// </summary>
    public void SendGetCardFromSideDeck(string cardInstanceId)
    {
        var action = new C_GetCardFromSideDeck
        {
            action = GameActionType.GET_CARD_FROM_SIDE_DECK,
            cardInstanceId = cardInstanceId
        };
        SendMessageAsync(action);
    }

    /// <summary>
    /// 감정표현(Emote)을 서버로 전송합니다.
    /// </summary>
    public void SendEmote(string emoteId, string message = "")
    {
        var action = new C_SendEmote(emoteId, message);
        SendMessageAsync(action);
        Debug.Log($"[GameClient] 💬 감정표현 전송: EmoteId='{emoteId}', Message='{message}'");
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

            case GameActionType.VALID_MEMBER_SKILL_TARGETS_RESPONSE:
                var memberTargetList = (S_ValidMemberSkillTargetsResponse)action;
                OnValidMemberSkillTargetsResponseEvent?.Invoke(memberTargetList);
                break;

            case GameActionType.USE_MEMBER_SKILL_SUCCESS:
                var memberSkillSuccess = (S_UseMemberSkillSuccess)action;
                OnUseMemberSkillSuccessEvent?.Invoke(memberSkillSuccess);
                break;

            case GameActionType.USE_MEMBER_SKILL_FAIL:
                var memberSkillFail = (S_UseMemberSkillFail)action;
                OnUseMemberSkillFailEvent?.Invoke(memberSkillFail);
                Debug.LogWarning($"[GameClient] 멤버 스킬 실패: {memberSkillFail.reason}");
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

            case GameActionType.GET_CARD_FROM_SIDE_DECK_SUCCESS:
                var sideDeckSuccess = (S_GetCardFromSideDeckSuccess)action;
                OnGetCardFromSideDeckSuccessEvent?.Invoke(sideDeckSuccess);
                Debug.Log($"[GameClient] 📥 사이드덱 카드 획득 성공 (소모 마나: {sideDeckSuccess.consumedCost}, 잔여 사이드덱: {sideDeckSuccess.remainingSideDeckCount})");
                break;

            case GameActionType.GET_CARD_FROM_SIDE_DECK_FAIL:
                var sideDeckFail = (S_GetCardFromSideDeckFail)action;
                OnGetCardFromSideDeckFailEvent?.Invoke(sideDeckFail);
                Debug.LogWarning($"[GameClient] ❌ 사이드덱 카드 가져오기 실패: {sideDeckFail.reason}");
                break;

            case GameActionType.CARD_CREATED:
                var cardCreated = (S_CardCreated)action;
                OnCardCreatedEvent?.Invoke(cardCreated);
                OnCardCreated(cardCreated);
                break;

            case GameActionType.RECEIVE_EMOTE:
                var emoteReceived = (S_ReceiveEmote)action;
                OnReceiveEmoteEvent?.Invoke(emoteReceived);
                Debug.Log($"[GameClient] 💬 감정표현 수신: Sender='{emoteReceived.senderUid}', EmoteId='{emoteReceived.emoteId}', Message='{emoteReceived.message}'");
                break;

            case GameActionType.NEW_LOG_EVENT:
                var newLog = (S_NewLogEvent)action;
                OnNewLogEvent?.Invoke(newLog);
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
                ClientDebugAction.Instance.DebugDeckinfo(gameReadyInfo.deckCards, gameReadyInfo.isOpponent);
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
        {
            GameMulliganManager.instance.StartMulliganPhase(info.cardsToMulligan);
        }
        else if (CardDrawManager.Instance != null)
        {
            CardDrawManager.Instance.PerformBatchDraw(info.cardsToMulligan);
        }

        if (OpponentHandVisualizer.Instance != null)
            OpponentHandVisualizer.Instance.PerformBatchDraw(info.cardsToMulligan.Count);

        // 멀리건 시작 시점에 양쪽 리더 설정
        if (GameEntityManager.Instance != null)
            GameEntityManager.Instance.SetReader(info);
    }

    private void OnGameReady(S_GameReady info)
    {
        if (info != null && !string.IsNullOrEmpty(info.opponentName))
        {
            OpponentUsername = info.opponentName;
        }
        StartCoroutine(SyncHandWithServer(info.finalHand));
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
                yield return YieldInstructionCache.WaitForSeconds(0.3f);
            }
        }

        // 멀리건 카드를 전부 뽑은 후 isMulliganPhase를 변경하여 손패각도 정상화 및 멀리건 UI 비활성화
        if (HandCardControllManager.instance != null)
        {
            HandCardControllManager.instance.isMulliganPhase = false;
            HandCardControllManager.instance.isMulligan = false;
            HandCardControllManager.instance.AlignHand();
        }

        if (GameMulliganManager.instance != null)
        {
            if (GameMulliganManager.instance.mulliganImg != null)
            {
                GameMulliganManager.instance.mulliganImg.SetActive(false);
            }
            GameMulliganManager.instance.EndMulliganPhase();
        }
    }

    // 상대 멀리건 돌아가는 함수 수정
    private IEnumerator OnMulliganInfoReceivedenemy(S_OpponentMulliganStatus info)
    {
        Debug.Log($"enamy mulligan : {info.replacedCount}");
        int m = 0;

        // 교체할 인덱스 목록을 큰 숫자부터 역순(내림차순)으로 정렬 (인플레이스 정렬로 LINQ 힙 할당 제거)
        if (info.replacedIndices != null)
        {
            info.replacedIndices.Sort((a, b) => b.CompareTo(a));
            foreach (var mulligan in info.replacedIndices)
            {
                OpponentHandVisualizer.Instance.ReturnCardToDeck(mulligan);
                m++;
                yield return YieldInstructionCache.WaitForSeconds(0.1f);
            }
        }

        yield return YieldInstructionCache.WaitForSeconds(1.5f);

        OpponentHandVisualizer.Instance.PerformBatchDraw(info.replacedCount);
        Debug.Log($"mulligan Count : {m} info.replacedCount : {info.replacedCount}");
    }

    // 상대 멀리건을 뽄는 함수
    private IEnumerator SyncHandWithServerenemy(int count)
    {
        for(int i = 0; i < count; i++) 
        {
            OpponentHandVisualizer.Instance.DrawCard();
            yield return YieldInstructionCache.WaitForSeconds(0.2f);
        }
    }

    private void OnPhaseStart(S_PhaseStart info)
    {
        if (info == null) return;
        // BattleManager가 OnPhaseStartEvent를 구독하여 턴 시작 드로우(PerformDrawAnimation)를 단독 전담하므로,
        // 여기서 중복으로 SendDrawCard를 호출하지 않습니다 (중복 드로우 버그 방지).
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
                    HandCardControllManager.instance.AlignHand();
                    break;

                case GameEventType.ATTACK:
                    //GameEntityManager.Instance.HandleEntitiesUpdated(info);
                    Debug.Log($"{info.eventLog[0].sourceEntityId}이가 {info.eventLog[0].targetEntityId}에게 Card Attack!");
                    break;
            }

        }
        yield return YieldInstructionCache.WaitForSeconds(1.0f);
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
        if (draw_Card == null)
        {
            Debug.LogWarning("[GameClient:SendDrawCard] draw_Card 객체가 null입니다.");
            return;
        }

        string cardDesc = draw_Card.drawnCard != null 
            ? $"'{draw_Card.drawnCard.cardId}' (instanceId: {draw_Card.drawnCard.instanceId}, origin: {draw_Card.drawnCard.origin} [{(int)draw_Card.drawnCard.origin}])" 
            : "null";

        if (draw_Card.playerUid == UserUid)
        {
            if (draw_Card.drawnCard != null && CardDrawManager.Instance != null)
            {
                bool isGenerate = (draw_Card.drawnCard.origin == CardOrigin.Created || draw_Card.drawnCard.origin == CardOrigin.SideDeck);

                if (isGenerate)
                {
                    CardDrawManager.Instance.PerformGenerateAnimation(draw_Card.drawnCard);
                }
                else
                {
                    CardDrawManager.Instance.PerformDrawAnimation(draw_Card.drawnCard);
                }
            }
            else
            {
                Debug.Log($"[GameClient:SendDrawCard] 드로우된 카드 데이터가 없거나 CardDrawManager가 없음 (drawnCard: {draw_Card.drawnCard != null}, CardDrawManager: {CardDrawManager.Instance != null})");
            }
        }
        else
        {
            Debug.Log($"[GameClient:SendDrawCard] 상대방 카드 드로우 처리 (playerUid: '{draw_Card.playerUid}')");
            if (OpponentHandVisualizer.Instance != null)
            {
                OpponentHandVisualizer.Instance.DrawCard();
            }
        }
    }

    /// <summary>
    /// 카드 생성(S_CardCreated) 패킷을 처리합니다.
    /// 본인이면 CardDrawManager에서 생성 연출을, 상대방이면 OpponentHandVisualizer에서 뒷면 생성 연출을 실행합니다.
    /// </summary>
    private void OnCardCreated(S_CardCreated info)
    {
        if (info == null) return;

        if (info.playerUid == UserUid)
        {
            CardInfo cardInfo = info.card ?? info.createdCard;
            if (cardInfo != null && CardDrawManager.Instance != null)
            {
                CardDrawManager.Instance.PerformGenerateAnimation(cardInfo);
            }
            else
            {
                Debug.Log($"[GameClient:OnCardCreated] 본인 카드 정보가 없거나 CardDrawManager가 없습니다.");
            }
        }
        else
        {
            if (OpponentHandVisualizer.Instance != null)
            {
                OpponentHandVisualizer.Instance.GenerateCard();
            }
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
        OpponentUsername = "상대방";
        OnGameOverEvent?.Invoke(info);
        _cts?.Cancel();
    }

    // ==================================================================
    // 유저 계정 및 보유 아이템 관리 기능
    // ==================================================================

    /// <summary>
    /// 로그인 시 또는 서버 동기화 시 유저 데이터 설정
    /// (0001 기본 스킨이 누락되어 있다면 자동으로 보유 목록에 추가)
    /// </summary>
    public void SetUserData(UserData data)
    {
        CurrentUser = data;
        if (data != null)
        {
            if (CurrentUser.ownedSkins == null)
            {
                CurrentUser.ownedSkins = new List<string>();
            }

            // 기본 0001 스킨 자동 보장 (아이템 목록.csv 기준 표준 규격)
            string[] default0001Skins = new string[] { "Skin_Gangzi_0001", "Skin_Yuni_0001", "Skin_Huya_0001" };
            foreach (var skinId in default0001Skins)
            {
                if (!CurrentUser.ownedSkins.Contains(skinId))
                {
                    CurrentUser.ownedSkins.Add(skinId);
                }
            }

            Debug.Log($"[GameClient] ✅ 유저 데이터 갱신 완료: {data.username} (Level: {data.level}, Gold: {data.gold}, Skins: {data.ownedSkins.Count}개)");
        }
        OnUserDataUpdated?.Invoke(CurrentUser);
    }

    /// <summary>
    /// 상점 구매 결과(재화 잔액 및 획득 아이템)를 즉시 로컬 CurrentUser에 반영
    /// </summary>
    public void ApplyPurchaseResult(PurchaseResponse purchase)
    {
        if (CurrentUser == null || purchase == null) return;

        CurrentUser.gold = purchase.remainingGold;
        CurrentUser.stellastone = purchase.remainingStellastone;
        CurrentUser.stardust = purchase.remainingStardust;

        if (CurrentUser.ownedPacks == null) CurrentUser.ownedPacks = new Dictionary<string, int>();
        if (CurrentUser.ownedSkins == null) CurrentUser.ownedSkins = new List<string>();

        // 카드팩 구매인 경우 OwnedPacks 반영
        if (purchase.remainingPacks > 0)
        {
            CurrentUser.ownedPacks[purchase.productId] = purchase.remainingPacks;
            Debug.Log($"[GameClient] 🎁 보유 카드팩 갱신: {purchase.productId} (총 {purchase.remainingPacks}개)");
        }
        else if (!string.IsNullOrEmpty(purchase.obtainedItemId))
        {
            if (!CurrentUser.ownedSkins.Contains(purchase.obtainedItemId))
            {
                CurrentUser.ownedSkins.Add(purchase.obtainedItemId);
                Debug.Log($"[GameClient] 🎁 신규 스킨/아이템 획득 로컬 반영: {purchase.obtainedItemId}");
            }
        }

        // SinginManager의 전역 UserData도 함께 실시간 동기화
        if (SinginManager.CurrentUserData != null)
        {
            SinginManager.CurrentUserData.gold = purchase.remainingGold;
            SinginManager.CurrentUserData.stellastone = purchase.remainingStellastone;
            SinginManager.CurrentUserData.stardust = purchase.remainingStardust;
            SinginManager.CurrentUserData.ownedPacks = CurrentUser.ownedPacks;
            SinginManager.CurrentUserData.ownedSkins = CurrentUser.ownedSkins;
            SinginManager.CurrentUserData.ownedCards = CurrentUser.ownedCards;
        }

        Debug.Log($"[GameClient] 🛒 상점 구매 로컬 반영 완료: 남은 골드={CurrentUser.gold}, 남은 성석={CurrentUser.stellastone}, 보유 스킨 수={CurrentUser.ownedSkins?.Count ?? 0}, 보유 팩 종류={CurrentUser.ownedPacks?.Count ?? 0}");
        OnUserDataUpdated?.Invoke(CurrentUser);
    }

    /// <summary>
    /// 서버(GET /api/users/me)로부터 최신 유저 정보를 다시 불러와 동기화하는 코루틴
    /// </summary>
    public IEnumerator RefreshUserDataAsync(Action<bool> onComplete = null)
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null)
        {
            Debug.LogWarning("[GameClient] 로그인된 사용자가 없어 유저 정보를 갱신할 수 없습니다.");
            onComplete?.Invoke(false);
            yield break;
        }

        var tokenTask = user.TokenAsync(false);
        yield return new WaitUntil(() => tokenTask.IsCompleted);

        if (tokenTask.IsFaulted || tokenTask.IsCanceled)
        {
            Debug.LogError("[GameClient] 인증 토큰을 가져오지 못했습니다.");
            onComplete?.Invoke(false);
            yield break;
        }

        string idToken = tokenTask.Result;
        string requestUrl = $"{BaseApiUrl}/users/me";

        using (UnityEngine.Networking.UnityWebRequest webRequest = UnityEngine.Networking.UnityWebRequest.Get(requestUrl))
        {
            webRequest.SetRequestHeader("Authorization", "Bearer " + idToken);
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    AuthApiResponse response = JsonConvert.DeserializeObject<AuthApiResponse>(webRequest.downloadHandler.text);
                    if (response != null && response.userData != null)
                    {
                        SetUserData(response.userData);
                        onComplete?.Invoke(true);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameClient] 유저 데이터 파싱 오류: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError($"[GameClient] 유저 데이터 갱신 실패: {webRequest.error}");
            }
        }

        onComplete?.Invoke(false);
    }

    /// <summary>
    /// 서버(/api/shop/products?category_id=0)로부터 리더 스킨 목록을 불러와 캐싱합니다.
    /// (이미 캐시된 데이터가 있고 forceReload가 false면 즉시 반환)
    /// </summary>
    public IEnumerator GetLeaderSkinsAsync(Action<List<ProductData>> onComplete = null, bool forceReload = false)
    {
        if (!forceReload && AllLeaderSkins != null && AllLeaderSkins.Count > 0)
        {
            onComplete?.Invoke(AllLeaderSkins);
            yield break;
        }

        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        string idToken = null;
        if (user != null)
        {
            var tokenTask = user.TokenAsync(false);
            yield return new WaitUntil(() => tokenTask.IsCompleted);
            if (!tokenTask.IsFaulted && !tokenTask.IsCanceled)
            {
                idToken = tokenTask.Result;
            }
        }

        string requestUrl = $"{BaseApiUrl}/shop/products?category_id=0";

        using (UnityEngine.Networking.UnityWebRequest webRequest = UnityEngine.Networking.UnityWebRequest.Get(requestUrl))
        {
            if (!string.IsNullOrEmpty(idToken))
            {
                webRequest.SetRequestHeader("Authorization", "Bearer " + idToken);
            }

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    ProductsApiResponse res = JsonUtility.FromJson<ProductsApiResponse>(webRequest.downloadHandler.text);
                    if (res != null && res.status == "success" && res.data != null)
                    {
                        AllLeaderSkins = res.data;
                        Debug.Log($"[GameClient] 🎨 리더 스킨 목록 캐시 완료: {AllLeaderSkins.Count}개 로드됨");

                        // 0001로 끝나는 기본 스킨들을 CurrentUser.ownedSkins에 자동 보유 등록
                        if (CurrentUser != null && CurrentUser.ownedSkins != null)
                        {
                            foreach (var skin in AllLeaderSkins)
                            {
                                if (skin != null && !string.IsNullOrEmpty(skin.productId) && skin.productId.EndsWith("0001"))
                                {
                                    if (!CurrentUser.ownedSkins.Contains(skin.productId))
                                    {
                                        CurrentUser.ownedSkins.Add(skin.productId);
                                        Debug.Log($"[GameClient] 🎁 0001 기본 스킨 자동 보유 등록: {skin.productId}");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameClient] 스킨 목록 파싱 오류: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError($"[GameClient] 스킨 목록 로드 실패: {webRequest.error}");
            }
        }

        onComplete?.Invoke(AllLeaderSkins);
    }
}