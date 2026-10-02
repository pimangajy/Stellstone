using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 인게임 감정표현(Emote) 시스템의 전체 흐름을 총괄하는 싱글톤 매니저입니다.
/// - 리더 클릭 시 주위 원형(Radial) 버튼 소환 및 퇴장
/// - 전역 공용 쿨다운(Global Cooldown) 관리
/// - 감정표현 버튼(EmotionButton) 및 말풍선(EmotionBubble) 오브젝트 풀링
/// - 자신 및 상대방의 감정표현 수신 시 해당 리더 옆에 띠용 말풍선 출력
/// </summary>
public class EmotionManager : MonoBehaviour
{
    private static EmotionManager _instance;
    public static EmotionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<EmotionManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("[EmotionManager]");
                    _instance = obj.AddComponent<EmotionManager>();
                }
            }
            return _instance;
        }
    }

    [System.Serializable]
    public class EmoteConfig
    {
        public string emoteId;
        public string message;
    }

    [Header("프리팹 설정")]
    [Tooltip("감정표현 선택 버튼 프리팹 (emotionButton.prefab)")]
    public GameObject emotionButtonPrefab;

    [Tooltip("말풍선 연출 프리팹 (emotion.prefab)")]
    public GameObject emotionBubblePrefab;

    [Header("감정표현 목록")]
    public List<EmoteConfig> emoteList = new List<EmoteConfig>
    {
        new EmoteConfig { emoteId = "GREETING", message = "안녕하세요!" },
        new EmoteConfig { emoteId = "THANKS", message = "감사합니다!" },
        new EmoteConfig { emoteId = "SORRY", message = "죄송합니다..." },
        new EmoteConfig { emoteId = "WELL_PLAYED", message = "잘 하셨네요!" },
    };

    [Header("현재 장착된 감정표현 (최대 4개)")]
    [Tooltip("인게임에서 사용할 EmoteData 에셋 목록입니다. (비어있으면 리더 직업에 맞는 기본 4개 자동 추출)")]
    public List<EmoteData> equippedEmotes = new List<EmoteData>();

    [Header("전체 감정표현 카탈로그 (Database)")]
    [Tooltip("프로젝트에 등록된 모든 EmoteData 목록입니다. (미할당 시 Resources/Emotes에서 자동 로드)")]
    public List<EmoteData> emoteDatabase = new List<EmoteData>();

    [Header("버튼 고정 배치 위치 설정 (지정 위치)")]
    [Tooltip("씬에 직접 배치한 고정 위치 Transform 목록. 비어있으면 myLeader 하위의 Imoticon 오브젝트를 자동 탐색합니다.")]
    public List<Transform> designatedPositions = new List<Transform>();

    [Tooltip("지정 위치의 플레이스홀더 오브젝트(Imoticon 등)를 게임 시작 시 자동으로 숨길지 여부")]
    public bool autoHidePlaceholderVisuals = true;

    [Header("공용 쿨다운 설정 (내부 관리)")]
    [Tooltip("모든 감정표현이 공유하는 쿨다운 시간 (초)")]
    public float globalCooldown = 3.0f;

    [Header("리더 주위 타원형 배치 설정 (지정 위치 미사용 시 폴백)")]
    [Tooltip("리더 중심으로부터 버튼까지의 가로(X) 반경")]
    public float radialRadiusX = 10.2f;

    [Tooltip("리더 중심으로부터 버튼까지의 세로(Z) 반경")]
    public float radialRadiusZ = 4.8f;

    [Tooltip("원형 호(Arc) 시작 각도 (도)")]
    public float arcStartAngle = 10f;

    [Tooltip("원형 호(Arc) 종료 각도 (도)")]
    public float arcEndAngle = 170f;

    [Tooltip("버튼의 높이(Y) 오프셋")]
    public float heightOffset = 0.2f;

    [Header("말풍선 위치 오프셋")]
    [Tooltip("내 리더 기준 말풍선 상대 위치")]
    public Vector3 myBubbleOffset = new Vector3(3.5f, 0.5f, 0.8f);

    [Tooltip("상대 리더 기준 말풍선 상대 위치")]
    public Vector3 oppBubbleOffset = new Vector3(3.5f, 0.5f, -0.8f);

    [Tooltip("말풍선이 떠 있는 지속 시간 (초)")]
    public float bubbleDuration = 3.0f;

    /// <summary>
    /// 현재 감정표현 메뉴(버튼들)가 열려있는지 여부
    /// </summary>
    public bool IsMenuOpen { get; private set; }

    /// <summary>
    /// 전역 공용 쿨다운 진행 중인지 여부
    /// </summary>
    public bool IsOnGlobalCooldown => Time.time < _lastGlobalEmoteTime + globalCooldown;

    private float _lastGlobalEmoteTime = -999f;
    private readonly Queue<EmotionButton> _buttonPool = new Queue<EmotionButton>();
    private readonly Queue<EmotionBubble> _bubblePool = new Queue<EmotionBubble>();
    private readonly List<EmotionButton> _activeButtons = new List<EmotionButton>();
    private EmotionBubble _myActiveBubble;
    private EmotionBubble _oppActiveBubble;
    private Transform _poolContainer;
    private int _openedFrame = -1;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(this);
            return;
        }

        // 풀링 오브젝트 부모 생성
        GameObject containerObj = new GameObject("EmotionPoolContainer");
        containerObj.transform.SetParent(transform);
        _poolContainer = containerObj.transform;

        EnsurePrefabsLoaded();
    }

    private void Start()
    {
        EnsurePrefabsLoaded();
        HidePlaceholderVisuals();
    }

    /// <summary>
    /// 지정된 위치 Transform 목록을 가져옵니다.
    /// 1. 인스펙터의 designatedPositions 우선 확인
    /// 2. 없으면 myLeader 하위의 Imoticon 오브젝트 내 자식들을 자동 탐색
    /// 3. 둘 다 없으면 null 반환 (원형 배치 폴백)
    /// </summary>
    private LeaderCardDisplay GetMyLeader()
    {
        if (GameEntityManager.Instance != null && GameEntityManager.Instance.myLeader != null)
        {
            return GameEntityManager.Instance.myLeader;
        }

        var gem = FindFirstObjectByType<GameEntityManager>();
        if (gem != null && gem.myLeader != null)
        {
            return gem.myLeader;
        }

        var rf = GameObject.Find("/Ground/PlayerField_1/Reader_Frame");
        if (rf != null)
        {
            return rf.GetComponent<LeaderCardDisplay>();
        }

        return null;
    }

    /// <summary>
    /// Resources/Emotes 폴더에서 등록된 모든 EmoteData 에셋을 자동으로 로드합니다.
    /// </summary>
    public void EnsureEmoteDatabaseLoaded()
    {
        if (emoteDatabase == null || emoteDatabase.Count == 0)
        {
            var loaded = Resources.LoadAll<EmoteData>("Emotes");
            if (loaded != null && loaded.Length > 0)
            {
                emoteDatabase = new List<EmoteData>(loaded);
            }
        }
    }

    /// <summary>
    /// 특정 emoteId에 해당하는 EmoteData 에셋을 검색합니다.
    /// </summary>
    public EmoteData GetEmoteById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        // 1. 현재 장착된 목록 우선 검색
        if (equippedEmotes != null)
        {
            var match = equippedEmotes.Find(e => e != null && string.Equals(e.emoteId, id, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        // 2. 전체 카탈로그 데이터베이스 검색
        EnsureEmoteDatabaseLoaded();
        if (emoteDatabase != null)
        {
            return emoteDatabase.Find(e => e != null && string.Equals(e.emoteId, id, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    /// <summary>
    /// 리더의 스킨 데이터를 받아 해당 스킨에 설정된 4개의 감정표현을 장착 목록(equippedEmotes)으로 적용합니다.
    /// </summary>
    public void ApplySkin(SkinData skinData)
    {
        if (skinData == null) return;

        EnsureEmoteDatabaseLoaded();

        // 1. 유저가 커스텀 장착한 감정표현 확인
        if (SkinCustomSettingManager.Instance != null)
        {
            var customEmotes = SkinCustomSettingManager.Instance.GetEquippedEmotes(skinData.skinId, skinData);
            if (customEmotes != null && customEmotes.Count > 0)
            {
                equippedEmotes = new List<EmoteData>(customEmotes);
                Debug.Log($"[EmotionManager] 🎭 리더 스킨 '{skinData.skinName}'({skinData.skinId})의 커스텀 감정표현 {equippedEmotes.Count}종 장착 완료!");
                return;
            }
        }

        var validEmotes = skinData.GetValidEmotes();
        if (validEmotes != null && validEmotes.Count > 0)
        {
            equippedEmotes = new List<EmoteData>(validEmotes);
            Debug.Log($"[EmotionManager] 🎭 리더 스킨 '{skinData.skinName}'({skinData.skinId})의 감정표현 {equippedEmotes.Count}종 장착 완료!");
        }
    }

    /// <summary>
    /// 플레이어 리더의 직업/스킨에 맞춰 인게임에서 사용할 4개의 EmoteData 목록을 가져옵니다.
    /// 1. 리더 스킨(CurrentSkinData)에 설정된 감정표현 최우선 반환
    /// 2. 런타임에 직접 장착된 equippedEmotes 반환
    /// 3. 장착 목록이 없으면 리더 직업에 맞는 기본 4개 자동 추출
    /// </summary>
    public List<EmoteData> GetActiveEmotes(LeaderCardDisplay leader)
    {
        // 1. 리더 스킨 데이터에 설정된 감정표현 최우선 사용 (커스텀 설정 우선)
        if (leader != null && leader.CurrentSkinData != null)
        {
            if (SkinCustomSettingManager.Instance != null)
            {
                var customEmotes = SkinCustomSettingManager.Instance.GetEquippedEmotes(leader.CurrentSkinData.skinId, leader.CurrentSkinData);
                if (customEmotes != null && customEmotes.Count > 0)
                {
                    return customEmotes;
                }
            }

            var skinEmotes = leader.CurrentSkinData.GetValidEmotes();
            if (skinEmotes != null && skinEmotes.Count > 0)
            {
                return skinEmotes;
            }
        }

        // 2. 인스펙터나 런타임에 직접 장착한 에셋 목록 확인
        if (equippedEmotes != null && equippedEmotes.Count > 0)
        {
            var valid = equippedEmotes.FindAll(e => e != null);
            if (valid.Count > 0) return valid;
        }

        // 3. 장착 목록이 비어있을 때: 전체 카탈로그에서 직업에 호환되는 기본 에셋 4개 자동 추출
        EnsureEmoteDatabaseLoaded();
        CardClass leaderClass = leader != null ? leader.CurrentCardClass : CardClass.Gangzi;

        var result = new List<EmoteData>();
        if (emoteDatabase != null)
        {
            foreach (var e in emoteDatabase)
            {
                if (e != null && e.IsCompatibleWith(leaderClass))
                {
                    result.Add(e);
                    if (result.Count >= 4) break;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// 지정된 위치 Transform 목록을 가져옵니다.
    /// 1. 인스펙터의 designatedPositions 우선 확인
    /// 2. 없으면 myLeader 하위의 Imoticon 오브젝트 내 자식들을 자동 탐색
    /// 3. 둘 다 없으면 null 반환 (원형 배치 폴백)
    /// </summary>
    public List<Transform> GetDesignatedPositions()
    {
        if (designatedPositions != null && designatedPositions.Count > 0)
        {
            var validList = designatedPositions.FindAll(t => t != null);
            if (validList.Count > 0) return validList;
        }

        LeaderCardDisplay myLeader = GetMyLeader();
        if (myLeader != null)
        {
            Transform imoticonRoot = myLeader.transform.Find("Imoticon");
            if (imoticonRoot != null && imoticonRoot.childCount > 0)
            {
                var list = new List<Transform>();
                for (int i = 0; i < imoticonRoot.childCount; i++)
                {
                    Transform child = imoticonRoot.GetChild(i);
                    list.Add(child);
                }
                return list;
            }
        }

        return null;
    }

    /// <summary>
    /// 씬에 배치된 감정표현 플레이스홀더 오브젝트들을 게임 시작 시 비활성화하여
    /// 평상시 중복 노출을 방지합니다. (비활성화되어도 Transform 좌표는 그대로 유지/참조됩니다)
    /// </summary>
    public void HidePlaceholderVisuals()
    {
        if (!autoHidePlaceholderVisuals) return;

        LeaderCardDisplay myLeader = GetMyLeader();
        if (myLeader != null)
        {
            Transform imoticonRoot = myLeader.transform.Find("Imoticon");
            if (imoticonRoot != null)
            {
                for (int i = 0; i < imoticonRoot.childCount; i++)
                {
                    Transform child = imoticonRoot.GetChild(i);
                    if (child != null)
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }
        }

        if (designatedPositions != null)
        {
            foreach (var t in designatedPositions)
            {
                if (t != null && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                }
            }
        }
    }

    private void OnEnable()
    {
        SubscribeNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeNetworkEvents();
    }

    private void EnsurePrefabsLoaded()
    {
#if UNITY_EDITOR
        if (emotionButtonPrefab == null)
        {
            emotionButtonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/TestRoom/Field/emotionButton.prefab");
        }
        if (emotionBubblePrefab == null)
        {
            emotionBubblePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/TestRoom/Field/emotion.prefab");
        }
#endif
    }

    private void SubscribeNetworkEvents()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnReceiveEmoteEvent -= OnReceiveEmotePacket;
            GameClient.Instance.OnReceiveEmoteEvent += OnReceiveEmotePacket;
        }
    }

    private void UnsubscribeNetworkEvents()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnReceiveEmoteEvent -= OnReceiveEmotePacket;
        }
    }

    private void Update()
    {
        // 감정표현 메뉴가 열려있을 때 외부 클릭(빈 필드, 빈 슬롯, 배경, 다른 유닛 등) 시 메뉴 닫기
        if (IsMenuOpen)
        {
            // 메뉴가 열린 당일 프레임에는 외부 클릭으로 간주하지 않음
            if (Time.frameCount == _openedFrame) return;

            // 우클릭 또는 ESC 시 즉시 닫기
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                HideEmoteMenu();
                return;
            }

            // 좌클릭 시
            if (Input.GetMouseButtonDown(0))
            {
                // 감정표현 버튼 자체를 클릭한 경우 버튼 내부 OnClick 처리 유지
                if (IsPointerOverEmotionButton()) return;

                // 아군 리더를 클릭한 경우 GameInputManager의 토글 처리에 맡김
                if (IsPointerOverFriendlyLeader()) return;

                // 그 외 빈 필드, 슬롯, 배경 등 어디든 외부를 클릭하면 닫기
                HideEmoteMenu();
            }
        }
    }

    private bool IsPointerOverEmotionButton()
    {
        if (EventSystem.current == null) return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject != null && results[i].gameObject.GetComponentInParent<EmotionButton>() != null)
                return true;
        }
        return false;
    }

    private bool IsPointerOverFriendlyLeader()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            LeaderCardDisplay myLeader = GetMyLeader();
            if (myLeader != null)
            {
                if (hit.collider != null && (hit.collider.gameObject == myLeader.gameObject || hit.collider.transform.IsChildOf(myLeader.transform)))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 내 리더를 클릭했을 때 감정표현 메뉴를 열거나 닫습니다.
    /// </summary>
    public void ToggleEmoteMenu()
    {
        if (IsMenuOpen)
        {
            HideEmoteMenu();
        }
        else
        {
            ShowEmoteMenu();
        }
    }

    /// <summary>
    /// 감정표현 메뉴를 소환합니다.
    /// 지정 위치(designatedPositions 또는 myLeader 하위 Imoticon)가 있으면 해당 위치에,
    /// 없으면 리더 주위 원형(타원형)으로 배치합니다.
    /// </summary>
    public void ShowEmoteMenu()
    {
        _openedFrame = Time.frameCount;

        // 1. 대사 진행 중에 다시 열면 기존 말풍선 즉시 제거
        if (_myActiveBubble != null && _myActiveBubble.gameObject.activeSelf)
        {
            _myActiveBubble.HideImmediate();
        }

        // 기존 활성화 버튼 정리
        HideButtonsImmediate();

        LeaderCardDisplay myLeader = GetMyLeader();
        if (myLeader == null)
        {
            Debug.LogWarning("[EmotionManager] ⚠️ myLeader를 찾을 수 없어 감정표현 메뉴를 열 수 없습니다.");
            return;
        }

        EnsurePrefabsLoaded();

        // 2. 내 리더의 감정표현 목록 조회 (장착 목록 또는 직업 호환 기본 목록)
        List<EmoteData> displayEmotes = GetActiveEmotes(myLeader);

        int totalEmotes = (displayEmotes != null && displayEmotes.Count > 0)
            ? displayEmotes.Count
            : ((emoteList != null && emoteList.Count > 0) ? emoteList.Count : 0);

        if (totalEmotes == 0) return;

        List<Transform> customPositions = GetDesignatedPositions();
        bool useCustomPositions = (customPositions != null && customPositions.Count > 0);

        int count = useCustomPositions ? Mathf.Min(totalEmotes, customPositions.Count) : totalEmotes;
        Vector3 centerPos = myLeader.transform.position;

        // 3. 버튼 배치 및 소환 애니메이션
        for (int i = 0; i < count; i++)
        {
            EmotionButton btn = GetButtonFromPool();

            string emoteId;
            string message;
            string label;

            if (displayEmotes != null && i < displayEmotes.Count)
            {
                var emote = displayEmotes[i];
                emoteId = emote.emoteId;
                message = emote.speechMessage;
                label = !string.IsNullOrEmpty(emote.buttonLabel) ? emote.buttonLabel : emote.emoteId;
            }
            else
            {
                EmoteConfig cfg = emoteList[i];
                emoteId = cfg.emoteId;
                message = cfg.message;
                label = cfg.message;
            }

            btn.Setup(emoteId, message, label, OnEmotionButtonClicked);

            Vector3 targetPos;
            Quaternion targetRot;

            if (useCustomPositions)
            {
                targetPos = customPositions[i].position;
                targetRot = customPositions[i].rotation;
            }
            else
            {
                // 원형 배치 (폴백)
                float angle = (count > 1) ? Mathf.Lerp(arcStartAngle, arcEndAngle, (float)i / (count - 1)) : 90f;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(rad) * radialRadiusX, heightOffset, Mathf.Sin(rad) * radialRadiusZ);
                targetPos = centerPos + offset;
                targetRot = Quaternion.Euler(90f, 0f, 0f);
            }

            btn.PlaySpawnAnimation(targetPos, targetRot, i * 0.035f);
            _activeButtons.Add(btn);
        }

        IsMenuOpen = true;
    }

    /// <summary>
    /// 감정표현 버튼들을 축소 애니메이션과 함께 숨깁니다.
    /// </summary>
    public void HideEmoteMenu()
    {
        if (!IsMenuOpen && _activeButtons.Count == 0) return;

        for (int i = 0; i < _activeButtons.Count; i++)
        {
            EmotionButton b = _activeButtons[i];
            b.PlayDespawnAnimation(() => ReturnButtonToPool(b));
        }

        _activeButtons.Clear();
        IsMenuOpen = false;
    }

    private void HideButtonsImmediate()
    {
        for (int i = 0; i < _activeButtons.Count; i++)
        {
            EmotionButton b = _activeButtons[i];
            b.HideImmediate();
            ReturnButtonToPool(b);
        }
        _activeButtons.Clear();
        IsMenuOpen = false;
    }

    /// <summary>
    /// 플레이어가 특정 감정표현 버튼을 클릭했을 때 호출됩니다.
    /// </summary>
    public void OnEmotionButtonClicked(EmotionButton btn)
    {
        if (btn == null) return;

        // 공용 쿨타임 검사 (내부 쿨타임)
        if (IsOnGlobalCooldown)
        {
            float remaining = (_lastGlobalEmoteTime + globalCooldown) - Time.time;
            Debug.Log($"[EmotionManager] ⏳ 감정표현 쿨다운 중입니다. (남은 시간: {remaining:F1}초)");
            return;
        }

        _lastGlobalEmoteTime = Time.time;

        string emoteId = btn.emoteId;
        string message = btn.emoteMessage;

        // 1. 버튼들 사라지기
        HideEmoteMenu();

        // 2. 내 리더 옆에 말풍선(emotion) 띠용 등장 및 보이스 재생
        LeaderCardDisplay myLeader = GetMyLeader();
        if (myLeader != null)
        {
            var emote = GetEmoteById(emoteId);
            if (emote != null)
            {
                message = emote.speechMessage;
                if (emote.voiceClip != null && SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlayVoice(emote.voiceClip);
                }
            }

            ShowSpeechBubble(myLeader, message, isMine: true);
        }

        // 3. 서버로 패킷 전송
        if (GameClient.Instance != null && GameClient.Instance.IsConnected)
        {
            GameClient.Instance.SendEmote(emoteId, message);
        }
        else
        {
            Debug.Log($"[EmotionManager] 💬 (테스트/오프라인) 감정표현 전송: EmoteId='{emoteId}', Message='{message}'");
        }
    }

    /// <summary>
    /// 특정 리더 오브젝트 옆에 감정표현 말풍선을 띄웁니다.
    /// </summary>
    public void ShowSpeechBubble(LeaderCardDisplay leader, string message, bool isMine)
    {
        if (leader == null) return;

        EnsurePrefabsLoaded();

        // 이전 활성화된 말풍선이 있다면 즉시 닫기
        if (isMine && _myActiveBubble != null)
        {
            _myActiveBubble.HideImmediate();
        }
        else if (!isMine && _oppActiveBubble != null)
        {
            _oppActiveBubble.HideImmediate();
        }

        EmotionBubble bubble = GetBubbleFromPool();
        if (isMine) _myActiveBubble = bubble;
        else _oppActiveBubble = bubble;

        Vector3 offset = isMine ? myBubbleOffset : oppBubbleOffset;
        Vector3 spawnPos = leader.transform.position + offset;

        bubble.Show(message, spawnPos, bubbleDuration, onHide: () =>
        {
            if (isMine && _myActiveBubble == bubble) _myActiveBubble = null;
            if (!isMine && _oppActiveBubble == bubble) _oppActiveBubble = null;
            ReturnBubbleToPool(bubble);
        });
    }

    /// <summary>
    /// 서버로부터 상대방(또는 자신)의 감정표현 브로드캐스트를 수신했을 때 호출됩니다.
    /// </summary>
    private void OnReceiveEmotePacket(S_ReceiveEmote packet)
    {
        if (packet == null) return;

        bool isMine = (GameEntityManager.Instance != null && packet.senderUid == GameEntityManager.Instance.MyUid);

        // 내가 보낸 것은 OnEmotionButtonClicked에서 즉각 로컬 반응으로 이미 띄웠으므로 상대방 것만 처리
        if (!isMine)
        {
            LeaderCardDisplay oppLeader = GameEntityManager.Instance != null ? GameEntityManager.Instance.opponentLeader : null;
            if (oppLeader != null)
            {
                string message = packet.message;

                // 상대방 감정표현에 해당하는 대사 및 보이스 조회 (상대 리더 스킨 우선 조회)
                EmoteData oppEmote = null;
                if (oppLeader.CurrentSkinData != null && oppLeader.CurrentSkinData.defaultEmotes != null)
                {
                    oppEmote = oppLeader.CurrentSkinData.defaultEmotes.Find(e => e != null && string.Equals(e.emoteId, packet.emoteId, StringComparison.OrdinalIgnoreCase));
                }

                if (oppEmote == null)
                {
                    oppEmote = GetEmoteById(packet.emoteId);
                }

                if (oppEmote != null)
                {
                    message = oppEmote.speechMessage;
                    if (oppEmote.voiceClip != null && SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlayVoice(oppEmote.voiceClip);
                    }
                }

                ShowSpeechBubble(oppLeader, message, isMine: false);
            }
        }
    }

    // =========================================================================
    // 오브젝트 풀링 (Object Pooling)
    // =========================================================================

    private EmotionButton GetButtonFromPool()
    {
        if (_buttonPool.Count > 0)
        {
            EmotionButton btn = _buttonPool.Dequeue();
            if (btn != null)
            {
                btn.gameObject.SetActive(true);
                return btn;
            }
        }

        if (emotionButtonPrefab != null)
        {
            GameObject newObj = Instantiate(emotionButtonPrefab, _poolContainer);
            EmotionButton btn = newObj.GetComponent<EmotionButton>();
            if (btn == null) btn = newObj.AddComponent<EmotionButton>();
            return btn;
        }

        Debug.LogError("[EmotionManager] ❌ emotionButtonPrefab이 할당되지 않았습니다!");
        return null;
    }

    private void ReturnButtonToPool(EmotionButton btn)
    {
        if (btn == null) return;
        btn.HideImmediate();
        if (_poolContainer != null) btn.transform.SetParent(_poolContainer);
        _buttonPool.Enqueue(btn);
    }

    private EmotionBubble GetBubbleFromPool()
    {
        if (_bubblePool.Count > 0)
        {
            EmotionBubble bubble = _bubblePool.Dequeue();
            if (bubble != null)
            {
                bubble.gameObject.SetActive(true);
                return bubble;
            }
        }

        if (emotionBubblePrefab != null)
        {
            GameObject newObj = Instantiate(emotionBubblePrefab, _poolContainer);
            EmotionBubble bubble = newObj.GetComponent<EmotionBubble>();
            if (bubble == null) bubble = newObj.AddComponent<EmotionBubble>();
            return bubble;
        }

        Debug.LogError("[EmotionManager] ❌ emotionBubblePrefab이 할당되지 않았습니다!");
        return null;
    }

    private void ReturnBubbleToPool(EmotionBubble bubble)
    {
        if (bubble == null) return;
        bubble.HideImmediate();
        if (_poolContainer != null) bubble.transform.SetParent(_poolContainer);
        _bubblePool.Enqueue(bubble);
    }
}
