using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using Firebase.Auth;
using TMPro;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 카드팩 개봉 요청 DTO (클라이언트 -> 서버)
/// </summary>
[System.Serializable]
public class OpenPackRequest
{
    public string packProductId;
    public int count = 1;
}

/// <summary>
/// 카드팩 개봉 응답 DTO (서버 -> 클라이언트)
/// </summary>
[System.Serializable]
public class OpenPackResponse
{
    public string status;
    public string message;
    public string packProductId;
    public int remainingPacks;
    public List<string> obtainedCardIds;
}

/// <summary>
/// 작업대(Craft Table) 전담 매니저
/// - 작업대 구역 진입 감지 및 계정 보유 카드팩 조회
/// - 상자/컨테이너 안에 3D 카드팩 프리팹 일정한 간격으로 생성 및 스크롤 연동
/// - 카드팩 클릭 시 지정 위치에 복사본 생성 (수량 텍스트 숨김)
/// - 원본 카드팩 수량 차감 (1개면 숨김, 2개 이상이면 1 감소)
/// - 포커스 및 카드 개봉 중에는 상자 스크롤러 입력 차단 (상자 오클릭 방지)
/// - '취소' 클릭 시 복사본 제거 및 원본 수량/표시 복구
/// - '사용' 클릭 시 서버 패킷 전송 및 성공 응답 모의 -> 중앙 복사본 소멸
/// - 5장의 3D 카드가 뒷면으로 카메라 전방 부채꼴(Fan) 형태로 등장
/// - 뒷면 카드 클릭 시 180도 회전(Flip)하며 앞면 노출 (3중 클릭 감지)
/// - 확인 버튼 동적 전환: 5장 확인 전에는 '모두 확인' (클릭 시 순차 플립) -> 5장 모두 앞면일 때 '확인' (창고 복귀)
/// - '다시 사용' 클릭 시 상자 수량 추가 차감 후 새로운 5장 부채꼴 재등장
/// - '확인' 클릭 시 5장 카드 파괴 후 창고 복귀 (상자엔 줄어든 수량 유지)
/// </summary>
public class CraftManager : MonoBehaviour
{
    public static CraftManager Instance { get; private set; }

    /// <summary>
    /// 카드팩 개봉 API 엔드포인트 URL
    /// </summary>
    public string OpenPackApiUrl => (GameClient.Instance != null)
        ? GameClient.Instance.GetApiUrl("inventory/open-pack")
        : "http://175.125.250.226:5123/api/inventory/open-pack";

    [Header("카메라 연동")]
    [Tooltip("구역 진입 이벤트를 수신할 StorageCameraController (비워두면 자동 탐색)")]
    [SerializeField] private StorageCameraController cameraController;

    [Tooltip("작업대 구역 인덱스 (기본: 0)")]
    [SerializeField] private int craftSectionIndex = 0;

    [Header("카드팩 프리팹 및 생성 설정")]
    [Tooltip("생성할 카드팩 프리팹 (인스펙터에서 직접 연결)")]
    [SerializeField] private GameObject cardPackPrefab;

    [Tooltip("카드팩들이 생성될 부모 컨테이너 Transform (스크롤러의 Target Object)")]
    [SerializeField] private Transform packContainer;

    [Tooltip("연동할 CraftTableScroller (비워두면 자동 탐색)")]
    [SerializeField] private CraftTableScroller scroller;

    [Tooltip("카드팩 간의 배치 간격 (m)")]
    [SerializeField] private float packSpacing = 0.35f;

    [Tooltip("카드팩이 나열될 배치 축 (기본: Z축 (0, 0, 1) 또는 X축 (1, 0, 0))")]
    [SerializeField] private Vector3 spawnAxis = new Vector3(0, 0, 1);

    [Tooltip("첫 번째 카드팩이 생성될 기준 로컬 위치")]
    [SerializeField] private Vector3 startLocalPosition = Vector3.zero;

    [Tooltip("카드팩 생성 시 적용할 로컬 회전값 (오일러 각도)")]
    [SerializeField] private Vector3 packLocalRotation = Vector3.zero;

    [Tooltip("카드팩 생성 시 적용할 로컬 크기 배율 (기본: 1, 1, 1)")]
    [SerializeField] private Vector3 packLocalScale = Vector3.one;

    [Tooltip("카드팩 개봉 애니메이션 연출 대기 시간 (초, 모션 완료 후 카드가 펼쳐짐)")]
    [SerializeField] private float packOpenAnimationDuration = 0.8f;

    [Tooltip("true: 보유 수량만큼 각각 생성 / false: 종류당 1개씩만 생성")]
    [SerializeField] private bool spawnPerCount = true;

    [Tooltip("전체 조망으로 나갈 때 생성된 카드팩 삭제 여부")]
    [SerializeField] private bool clearPacksOnReturnOverview = false;

    [Header("카드팩 직업별 3D 머티리얼 (Resources/Items/CardPack/Materials/)")]
    [Tooltip("강지 직업 및 기본 카드팩 머티리얼 (M_CardPack_Gangzi.mat - 맞는 직업이 없을 때 기본 적용)")]
    [SerializeField] private Material gangziPackMaterial;

    [Tooltip("유니 직업 카드팩 머티리얼 (M_CardPack_Yuni.mat)")]
    [SerializeField] private Material yuniPackMaterial;

    [Tooltip("후야 직업 카드팩 머티리얼 (M_CardPack_Huya.mat)")]
    [SerializeField] private Material huyaPackMaterial;

    [Header("카드팩 직업별 3D 전개도 텍스처 (UV Fallback)")]
    [Tooltip("강지 직업 및 기본 카드팩 전개도 텍스처 (gangzi.png - 맞는 직업이 없을 때 기본 적용)")]
    [SerializeField] private Texture2D gangziPackTexture;

    [Tooltip("유니 직업 카드팩 전개도 텍스처 (yuni.png)")]
    [SerializeField] private Texture2D yuniPackTexture;

    [Tooltip("후야 직업 카드팩 전개도 텍스처 (huya.png)")]
    [SerializeField] private Texture2D huyaPackTexture;

    [Header("중앙 배치 및 상호작용 설정")]
    [Tooltip("클릭된 카드팩 복사본이 배치될 지정 위치 Transform (중앙 지점)")]
    [SerializeField] private Transform focusSpot;

    [Tooltip("중앙 복사본 카드팩의 회전 각도 (오일러 각도)")]
    [SerializeField] private Vector3 focusPackRotation = Vector3.zero;

    [Tooltip("중앙 복사본 카드팩의 크기 배율")]
    [SerializeField] private Vector3 focusPackScale = Vector3.one;

    [Tooltip("취소 및 사용 버튼을 포함하는 UI 패널 (선택 시 활성화, 취소 시 비활성화)")]
    [SerializeField] private GameObject focusButtonGroup;

    [Tooltip("중앙 카드팩 취소 버튼")]
    [SerializeField] private Button btnCancel;

    [Tooltip("중앙 카드팩 사용 버튼")]
    [SerializeField] private Button btnUse;

    [Tooltip("중앙 카드팩 정보 패널(Packinfo)의 팩 이름 표시 텍스트")]
    [SerializeField] private TextMeshProUGUI packNameText;

    [Header("개봉 결과 상호작용 설정")]
    [Tooltip("개봉 완료 후 '다시 사용', '모두 확인/확인' 버튼을 포함하는 UI 패널")]
    [SerializeField] private GameObject resultButtonGroup;

    [Tooltip("다시 사용 버튼")]
    [SerializeField] private Button btnUseAgain;

    [Tooltip("확인 버튼 (모두 확인 <-> 확인 동적 전환)")]
    [SerializeField] private Button btnConfirm;

    [Header("카드 개봉 연출 설정")]
    [Tooltip("개봉 시 생성할 3D 카드 프리팹 (Assets/Prefab/Storage/Card.prefab)")]
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("부채꼴 연출 기준 Transform (비워두면 카메라 전방 자동 계산)")]
    [SerializeField] private Transform cardFanAnchor;

    [Tooltip("카메라 전방 거리 (m)")]
    [SerializeField] private float distanceFromCamera = 1.8f;

    [Tooltip("카메라 중심 대비 상하 높이 오프셋 (m, 상자를 가리지 않도록 약간 높게 설정)")]
    [SerializeField] private float heightOffsetFromCamera = 0.05f;

    [Tooltip("카드 간 좌우 간격 (m)")]
    [SerializeField] private float cardSpacing = 0.75f;

    [Tooltip("부채꼴 곡률 - 깊이 (m, 양 끝 카드가 뒤로 빠지는 정도)")]
    [SerializeField] private float cardArcDepth = 0.06f;

    [Tooltip("부채꼴 곡률 - 높이 (m, 양 끝 카드가 아래로 처지는 정도)")]
    [SerializeField] private float cardArcHeight = 0.08f;

    [Tooltip("카드별 Z축 기울기 각도 (도)")]
    [SerializeField] private float cardFanAngle = 4.5f;

    [Tooltip("개봉 카드의 크기 배율 (원본 프리팹 3:4.2 비율 보존)")]
    [SerializeField] private float cardScaleMultiplier = 0.22f;

    [Tooltip("카드 뒤집기 애니메이션 지속 시간 (초)")]
    [SerializeField] private float flipDuration = 0.45f;

    [Tooltip("뒤집히지 않은 카드가 있는 상태에서 '다시 사용' 시, 전원 플립 완료 후 다음 팩으로 넘어가기 전 대기 딜레이 (초)")]
    [SerializeField] private float delayAfterRevealOnUseAgain = 0.8f;

    [Header("로딩 및 네트워크 연출")]
    [Tooltip("서버 요청 중 표시할 로딩 패널 (인스펙터에서 직접 연결)")]
    [SerializeField] private GameObject loadingPanel;

    [Tooltip("10초 이상 응답이 없을 때 표시할 통신 오류 패널 (인스펙터에서 직접 연결)")]
    [SerializeField] private GameObject errorPanel;

    [Tooltip("통신 오류 패널 닫기/확인 버튼 (인스펙터에서 직접 연결)")]
    [SerializeField] private Button btnErrorClose;

    [Tooltip("네트워크 타임아웃 제한 시간 (초, 기본: 10초)")]
    [SerializeField] private float networkTimeoutSeconds = 10.0f;

    [Tooltip("가상 서버 응답 지연 시간 (초, 패킷 시뮬레이션용)")]
    [SerializeField] private float mockResponseDelay = 1.2f;

    [Tooltip("타임아웃 오류 테스트용 플래그 (체크 시 10초 동안 대기 후 통신오류 패널 표시)")]
    [SerializeField] private bool testTimeoutError = false;

    [Header("테스트 설정")]
    [Tooltip("로그인된 데이터가 없을 때 에디터 테스트용 더미 팩 데이터 사용 여부")]
    [SerializeField] private bool useDummyDataIfNoLogin = true;

    // 생성된 카드팩 인스턴스 관리
    private readonly List<GameObject> _spawnedPacks = new List<GameObject>();

    // 중앙 포커스 상태 관리
    private CardPackItem _selectedOriginalPack;
    private GameObject _currentFocusPackObject;
    private bool _isFocusing = false;

    // 5장 카드 개봉 상태 관리
    private readonly List<CardOpeningItem> _activeCards = new List<CardOpeningItem>();
    private bool _isOpeningActive = false;

    // 네트워크 요청 및 다시 사용 코루틴 관리
    private Coroutine _packOpenCoroutine;
    private Coroutine _useAgainCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        CheckInspectorReferences();
        EnsurePackMaterialsLoaded();
        EnsurePackTexturesLoaded();
    }

    private void CheckInspectorReferences()
    {
        if (cameraController == null)
            Debug.LogWarning("[CraftManager] ⚠️ cameraController가 인스펙터에 할당되지 않았습니다.");
        if (scroller == null)
            Debug.LogWarning("[CraftManager] ⚠️ scroller가 인스펙터에 할당되지 않았습니다.");
        if (cardPackPrefab == null)
            Debug.LogWarning("[CraftManager] ⚠️ cardPackPrefab이 인스펙터에 할당되지 않았습니다.");
        if (packContainer == null)
            Debug.LogWarning("[CraftManager] ⚠️ packContainer가 인스펙터에 할당되지 않았습니다.");
        if (focusSpot == null)
            Debug.LogWarning("[CraftManager] ⚠️ focusSpot이 인스펙터에 할당되지 않았습니다.");
        if (focusButtonGroup == null)
            Debug.LogWarning("[CraftManager] ⚠️ focusButtonGroup이 인스펙터에 할당되지 않았습니다.");
        if (btnCancel == null)
            Debug.LogWarning("[CraftManager] ⚠️ btnCancel이 인스펙터에 할당되지 않았습니다.");
        if (btnUse == null)
            Debug.LogWarning("[CraftManager] ⚠️ btnUse가 인스펙터에 할당되지 않았습니다.");
        if (resultButtonGroup == null)
            Debug.LogWarning("[CraftManager] ⚠️ resultButtonGroup이 인스펙터에 할당되지 않았습니다.");
        if (btnUseAgain == null)
            Debug.LogWarning("[CraftManager] ⚠️ btnUseAgain이 인스펙터에 할당되지 않았습니다.");
        if (btnConfirm == null)
            Debug.LogWarning("[CraftManager] ⚠️ btnConfirm이 인스펙터에 할당되지 않았습니다.");
        if (cardPrefab == null)
            Debug.LogWarning("[CraftManager] ⚠️ cardPrefab이 인스펙터에 할당되지 않았습니다.");
        if (loadingPanel == null)
            Debug.LogWarning("[CraftManager] ⚠️ loadingPanel이 인스펙터에 할당되지 않았습니다.");
        if (errorPanel == null)
            Debug.LogWarning("[CraftManager] ⚠️ errorPanel이 인스펙터에 할당되지 않았습니다.");
        if (btnErrorClose == null)
            Debug.LogWarning("[CraftManager] ⚠️ btnErrorClose가 인스펙터에 할당되지 않았습니다.");
    }

    private void Start()
    {
        if (btnCancel != null)
        {
            btnCancel.onClick.AddListener(OnCancelClicked);
        }

        if (btnUse != null)
        {
            btnUse.onClick.AddListener(OnUseClicked);
        }

        if (btnUseAgain != null)
        {
            btnUseAgain.onClick.AddListener(OnUseAgainClicked);
        }

        if (btnConfirm != null)
        {
            btnConfirm.onClick.AddListener(HandleConfirmButtonClicked);
        }

        if (btnErrorClose != null)
        {
            btnErrorClose.onClick.AddListener(OnErrorCloseClicked);
        }

        if (focusButtonGroup != null)
        {
            focusButtonGroup.SetActive(false);
        }

        if (resultButtonGroup != null)
        {
            resultButtonGroup.SetActive(false);
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (cameraController != null)
        {
            cameraController.onSectionEnter.AddListener(OnSectionEnter);
            cameraController.onReturnOverview.AddListener(OnReturnOverview);
        }

        if (scroller != null)
        {
            scroller.OnItemClicked += HandleCardPackClicked;
        }
    }

    private void OnDisable()
    {
        if (cameraController != null)
        {
            cameraController.onSectionEnter.RemoveListener(OnSectionEnter);
            cameraController.onReturnOverview.RemoveListener(OnReturnOverview);
        }

        if (scroller != null)
        {
            scroller.OnItemClicked -= HandleCardPackClicked;
        }

        if (_useAgainCoroutine != null)
        {
            StopCoroutine(_useAgainCoroutine);
            _useAgainCoroutine = null;
        }
    }

    /// <summary>
    /// 구역 진입 시 호출
    /// </summary>
    private void OnSectionEnter(int sectionIndex)
    {
        if (sectionIndex != craftSectionIndex) return;

        Debug.Log("[CraftManager] 🛠️ 작업대 구역에 도착했습니다! 계정 보유 카드팩을 조회합니다.");
        RefreshOwnedPacks();
    }

    /// <summary>
    /// 전체 조망 복귀 시 호출
    /// </summary>
    private void OnReturnOverview()
    {
        if (_packOpenCoroutine != null)
        {
            StopCoroutine(_packOpenCoroutine);
            _packOpenCoroutine = null;
        }

        if (_useAgainCoroutine != null)
        {
            StopCoroutine(_useAgainCoroutine);
            _useAgainCoroutine = null;
        }

        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (errorPanel != null) errorPanel.SetActive(false);

        if (_isOpeningActive)
        {
            ClearActiveCards();
            if (resultButtonGroup != null) resultButtonGroup.SetActive(false);
            if (_currentFocusPackObject != null)
            {
                Destroy(_currentFocusPackObject);
                _currentFocusPackObject = null;
            }
            _isFocusing = false;
            _selectedOriginalPack = null;
        }
        else if (_isFocusing)
        {
            OnCancelClicked();
        }

        if (scroller != null)
        {
            scroller.IsInteractive = true;
        }

        if (clearPacksOnReturnOverview)
        {
            ClearSpawnedPacks();
        }
    }

    // [최적화] 클릭 감지 시 힙 할당(GC Alloc) 방지를 위한 캐시 버퍼
    private static readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>(16);
    private static readonly RaycastHit[] _raycastHitBuffer = new RaycastHit[16];
    private PointerEventData _cachedPointerData;

    private void Update()
    {
        // 1. 중앙에 복사본이 활성화되어 있을 때 인스펙터 실시간 조절 반영
        if (_isFocusing && _currentFocusPackObject != null)
        {
            Quaternion targetRot = (focusSpot != null ? focusSpot.rotation : Quaternion.identity) * Quaternion.Euler(focusPackRotation);
            _currentFocusPackObject.transform.rotation = targetRot;
            _currentFocusPackObject.transform.localScale = focusPackScale;
        }

        // 2. 카드가 열려 있을 때 마우스 클릭으로 카드 뒤집기 감지 (화면 UI 버튼 클릭은 제외)
        if (_isOpeningActive && Input.GetMouseButtonDown(0))
        {
            bool isOverScreenUI = false;
            if (EventSystem.current != null)
            {
                if (_cachedPointerData == null) _cachedPointerData = new PointerEventData(EventSystem.current);
                _cachedPointerData.position = Input.mousePosition;
                _uiRaycastResults.Clear();
                EventSystem.current.RaycastAll(_cachedPointerData, _uiRaycastResults);

                for (int i = 0; i < _uiRaycastResults.Count; i++)
                {
                    Canvas c = _uiRaycastResults[i].gameObject.GetComponentInParent<Canvas>();
                    if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        isOverScreenUI = true;
                        break;
                    }
                }
            }

            if (!isOverScreenUI)
            {
                Camera cam = cameraController != null && cameraController.TargetCamera != null
                    ? cameraController.TargetCamera
                    : Camera.main;

                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                    int hitCount = Physics.RaycastNonAlloc(ray, _raycastHitBuffer, 100f);
                    for (int i = 0; i < hitCount; i++)
                    {
                        CardOpeningItem card = _raycastHitBuffer[i].collider.GetComponentInParent<CardOpeningItem>();
                        if (card != null && !card.IsFlipped)
                        {
                            card.Flip();
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 상자 안의 카드팩 클릭 시 호출 (스크롤러의 OnItemClicked 이벤트 연동)
    /// </summary>
    private void HandleCardPackClicked(Collider hitCollider)
    {
        if (_isFocusing || _isOpeningActive) return; // 이미 진행 중이면 추가 클릭 방지
        if (hitCollider == null) return;

        CardPackItem clickedPack = hitCollider.GetComponentInParent<CardPackItem>();
        if (clickedPack == null)
        {
            Debug.Log($"[CraftManager] ℹ️ 클릭된 오브젝트 '{hitCollider.name}'는 카드팩이 아닙니다. (테이블/상자 빈 공간 클릭)");
            return;
        }

        _selectedOriginalPack = clickedPack;
        _isFocusing = true;

        // 상자 스크롤러 상호작용 잠금 (포커스 중 상자 오클릭 및 스크롤 차단)
        if (scroller != null)
        {
            scroller.IsInteractive = false;
        }

        // 1. 지정 위치에 복사본 생성 (수량 텍스트 비활성화, 지정 각도 및 크기 적용)
        if (cardPackPrefab != null)
        {
            Vector3 spawnPos = focusSpot != null ? focusSpot.position : transform.position;
            Quaternion spawnRot = (focusSpot != null ? focusSpot.rotation : Quaternion.identity) * Quaternion.Euler(focusPackRotation);

            _currentFocusPackObject = Instantiate(cardPackPrefab, spawnPos, spawnRot);
            _currentFocusPackObject.name = $"{clickedPack.PackId}_FocusCopy";
            _currentFocusPackObject.transform.localScale = focusPackScale;

            if (_currentFocusPackObject.TryGetComponent<CardPackItem>(out var copyItem))
            {
                copyItem.Setup(clickedPack.PackId, 1);
                copyItem.SetTextActive(false); // 복사본은 수량 텍스트 숨김!
            }

            if (_currentFocusPackObject.TryGetComponent<Animator>(out var copyAnim))
            {
                copyAnim.Play("Idle", 0, 0f);
            }
        }

        // 2. 상자 안의 원본 카드팩 수량 임시 차감 (중앙으로 이동한 것처럼 연출)
        if (clickedPack.Count <= 1)
        {
            clickedPack.gameObject.SetActive(false);
        }
        else
        {
            clickedPack.UpdateCount(clickedPack.Count - 1);
        }

        // 3. '취소' 및 '사용' 버튼 활성화
        if (focusButtonGroup != null)
        {
            focusButtonGroup.SetActive(true);
        }

        // 4. 팩 이름 텍스트 갱신
        UpdatePackNameText(clickedPack.PackId);

        if (resultButtonGroup != null)
        {
            resultButtonGroup.SetActive(false);
        }

        Debug.Log($"[CraftManager] 🎯 카드팩 선택: [{clickedPack.PackId}] (상자 원본 상태: {(clickedPack.gameObject.activeSelf ? clickedPack.Count + "개 남음" : "숨김(중앙 이동)")})");
    }

    /// <summary>
    /// 카드팩 상품 ID에 해당하는 한글 이름을 반환합니다.
    /// </summary>
    public static string GetPackDisplayName(string packId)
    {
        if (string.IsNullOrEmpty(packId)) return "기본 카드팩";

        string trimmed = packId.Trim();

        // 1. 아이템 목록 마스터 데이터 1:1 매칭
        switch (trimmed)
        {
            case "CardPack_0001":
                return "기본 카드팩";
            case "CardPack_yuni_0001":
                return "유니 기본 카드 확장팩";
            case "Cardpack_Huya_0001":
                return "후야 기본 카드팩";
            case "SpecialCardPack_0001":
                return "출시 기념 카드팩";
            case "SpecialCardPack_0002":
                return "강지 전설 카드팩";
        }

        // 2. 키워드 기반 대소문자 무시 판별
        string lower = trimmed.ToLowerInvariant();
        if (lower.Contains("yuni"))
        {
            return "유니 기본 카드 확장팩";
        }
        if (lower.Contains("huya"))
        {
            return "후야 기본 카드팩";
        }
        if (lower.Contains("special"))
        {
            if (lower.Contains("gangzi")) return "강지 전설 카드팩";
            return "출시 기념 카드팩";
        }
        if (lower.Contains("gangzi"))
        {
            return "강지 기본 카드팩";
        }

        return "기본 카드팩";
    }

    /// <summary>
    /// Packinfo 패널의 카드팩 이름 TextMeshProUGUI를 갱신합니다.
    /// </summary>
    private void UpdatePackNameText(string packId)
    {
        string displayName = GetPackDisplayName(packId);

        // 1. 인스펙터에 연결된 텍스트 컴포넌트 갱신
        if (packNameText != null)
        {
            packNameText.text = displayName;
            return;
        }

        // 2. Fallback: focusButtonGroup(Packinfo) 내부에서 Name 오브젝트의 TextMeshProUGUI 자동 탐색
        if (focusButtonGroup != null)
        {
            var nameTransform = focusButtonGroup.transform.Find("Name");
            if (nameTransform != null)
            {
                var tmp = nameTransform.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                {
                    packNameText = tmp;
                    tmp.text = displayName;
                    return;
                }
            }

            var anyTmp = focusButtonGroup.GetComponentInChildren<TextMeshProUGUI>(true);
            if (anyTmp != null)
            {
                packNameText = anyTmp;
                anyTmp.text = displayName;
            }
        }
    }

    /// <summary>
    /// '취소' 버튼 클릭 시 호출
    /// </summary>
    public void OnCancelClicked()
    {
        if (!_isFocusing || _isOpeningActive) return;

        // 1. 중앙 복사본 파괴
        if (_currentFocusPackObject != null)
        {
            Destroy(_currentFocusPackObject);
            _currentFocusPackObject = null;
        }

        // 2. 상자 안의 원본 카드팩 복구
        if (_selectedOriginalPack != null)
        {
            if (!_selectedOriginalPack.gameObject.activeSelf)
            {
                _selectedOriginalPack.gameObject.SetActive(true);
                _selectedOriginalPack.UpdateCount(1);
            }
            else
            {
                _selectedOriginalPack.UpdateCount(_selectedOriginalPack.Count + 1);
            }

            Debug.Log($"[CraftManager] ↩️ 카드팩 선택 취소: [{_selectedOriginalPack.PackId}] 원상 복구 완료 (수량: {_selectedOriginalPack.Count}개)");
            _selectedOriginalPack = null;
        }

        // 3. 버튼 그룹 비활성화
        if (focusButtonGroup != null)
        {
            focusButtonGroup.SetActive(false);
        }

        _isFocusing = false;

        // 상자 스크롤러 상호작용 복구
        if (scroller != null)
        {
            scroller.IsInteractive = true;
        }
    }

    /// <summary>
    /// '사용' 버튼 클릭 시 호출 -> 로딩 표시 & 서버 패킷 요청 (타임아웃 10초)
    /// </summary>
    public void OnUseClicked()
    {
        if (!_isFocusing || _selectedOriginalPack == null || _isOpeningActive || _packOpenCoroutine != null) return;

        // 취소/사용 버튼 비활성화
        if (focusButtonGroup != null)
        {
            focusButtonGroup.SetActive(false);
        }

        // 상자 스크롤러 잠금 유지
        if (scroller != null)
        {
            scroller.IsInteractive = false;
        }

        // 서버 패킷 요청 코루틴 시작
        _packOpenCoroutine = StartCoroutine(CoRequestPackOpen(isUseAgain: false));
    }

    /// <summary>
    /// 서버 카드팩 사용 요청 및 응답 대기 코루틴 (10초 타임아웃 및 신규 카드 판정)
    /// </summary>
    private System.Collections.IEnumerator CoRequestPackOpen(bool isUseAgain)
    {
        string packId = _selectedOriginalPack != null ? _selectedOriginalPack.PackId : "UnknownPack";

        Debug.Log($"[CraftManager] 🚀 [서버 요청 시작] 카드팩 개봉 요청! (상품 ID: {packId}, 다시 사용 여부: {isUseAgain})");

        // 1. 로딩 패널 활성화
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        // 2. 현재 로그인된 Firebase 유저 확인
        FirebaseUser user = FirebaseAuth.DefaultInstance != null ? FirebaseAuth.DefaultInstance.CurrentUser : null;

        // 개봉 전 기존 보유 카드 ID 목록 캡처 (신규 NEW 카드 판정용)
        HashSet<string> previouslyOwnedCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (GameClient.Instance?.CurrentUser?.ownedCards != null)
        {
            foreach (var kvp in GameClient.Instance.CurrentUser.ownedCards)
            {
                if (kvp.Value > 0) previouslyOwnedCards.Add(kvp.Key);
            }
        }
        else if (SinginManager.CurrentUserData?.ownedCards != null)
        {
            foreach (var kvp in SinginManager.CurrentUserData.ownedCards)
            {
                if (kvp.Value > 0) previouslyOwnedCards.Add(kvp.Key);
            }
        }

        bool isSuccess = false;
        List<string> resultCardIds = new List<string>();
        int updatedRemainingPacks = 0;

        // -------------------------------------------------------------
        // A. 테스트 모드 또는 로그인 유저가 없을 때 (가상 응답 폴백)
        // -------------------------------------------------------------
        if (user == null && useDummyDataIfNoLogin)
        {
            Debug.LogWarning("[CraftManager] ⚠️ Firebase 로그인 정보가 없어 테스트 모드로 가상 서버 응답을 생성합니다.");

            float elapsed = 0f;
            while (elapsed < networkTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;

                if (!testTimeoutError && elapsed >= mockResponseDelay)
                {
                    isSuccess = true;
                    break;
                }
            }

            if (isSuccess)
            {
                // 테스트용 카드 ID 5장 임의 추출
                CardData[] allResCards = Resources.LoadAll<CardData>("CardData");
                if (allResCards != null && allResCards.Length > 0)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        var picked = allResCards[UnityEngine.Random.Range(0, allResCards.Length)];
                        resultCardIds.Add(picked.cardID);
                    }
                }

                updatedRemainingPacks = (_selectedOriginalPack != null) ? Mathf.Max(0, _selectedOriginalPack.Count) : 0;
            }
        }
        // -------------------------------------------------------------
        // B. 실제 Firebase 인증 및 백엔드 서버 HTTP 통신
        // -------------------------------------------------------------
        else if (user != null)
        {
            // 2-1. Firebase ID 토큰 비동기 획득
            var tokenTask = user.TokenAsync(false);
            float tokenElapsed = 0f;
            while (!tokenTask.IsCompleted && tokenElapsed < networkTimeoutSeconds)
            {
                yield return null;
                tokenElapsed += Time.deltaTime;
            }

            if (tokenTask.IsFaulted || tokenTask.IsCanceled || !tokenTask.IsCompleted)
            {
                Debug.LogError("[CraftManager] ❌ Firebase 인증 토큰을 획득하지 못했거나 타임아웃되었습니다.");
            }
            else
            {
                string idToken = tokenTask.Result;

                // 2-2. OpenPackRequest 패킷 JSON 생성
                OpenPackRequest reqDto = new OpenPackRequest
                {
                    packProductId = packId,
                    count = 1
                };
                string jsonBody = JsonUtility.ToJson(reqDto);

                // 2-3. UnityWebRequest POST 전송
                using (UnityWebRequest webRequest = new UnityWebRequest(OpenPackApiUrl, "POST"))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    webRequest.downloadHandler = new DownloadHandlerBuffer();
                    webRequest.SetRequestHeader("Content-Type", "application/json");
                    webRequest.SetRequestHeader("Authorization", "Bearer " + idToken);
                    webRequest.timeout = Mathf.RoundToInt(networkTimeoutSeconds);

                    // 타임아웃 테스트 강제 발생 모드
                    if (testTimeoutError)
                    {
                        yield return YieldInstructionCache.WaitForSeconds(networkTimeoutSeconds);
                    }
                    else
                    {
                        var asyncOp = webRequest.SendWebRequest();
                        float netElapsed = 0f;
                        while (!asyncOp.isDone && netElapsed < networkTimeoutSeconds)
                        {
                            yield return null;
                            netElapsed += Time.deltaTime;
                        }
                    }

                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string responseText = webRequest.downloadHandler.text;
                        Debug.Log($"[CraftManager] 📦 [서버 패킷 수신]: {responseText}");

                        OpenPackResponse res = JsonUtility.FromJson<OpenPackResponse>(responseText);
                        if (res != null && res.status == "success")
                        {
                            isSuccess = true;
                            resultCardIds = res.obtainedCardIds ?? new List<string>();
                            updatedRemainingPacks = res.remainingPacks;
                        }
                        else
                        {
                            Debug.LogError($"[CraftManager] ❌ 카드팩 개봉 서버 실패: {res?.message}");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[CraftManager] ❌ 카드팩 개봉 통신 오류: {webRequest.responseCode} - {webRequest.error} | {webRequest.downloadHandler?.text}");
                    }
                }
            }
        }
        else
        {
            Debug.LogError("[CraftManager] ❌ 로그인된 사용자가 없고 useDummyDataIfNoLogin이 false이므로 통신을 진행할 수 없습니다.");
        }

        // 3. 로딩 패널 비활성화
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        // -------------------------------------------------------------
        // C. 결과 처리
        // -------------------------------------------------------------
        if (isSuccess)
        {
            Debug.Log($"[CraftManager] 🎉 [개봉 성공] 카드팩 '{packId}' 개봉 완료! (남은 팩: {updatedRemainingPacks}개, 획득 카드 수: {resultCardIds.Count})");

            // 1) 정상 패킷 수신 시점에 중앙 복사본 카드팩의 개봉 애니메이션 트리거 및 연출 대기
            if (_currentFocusPackObject != null)
            {
                if (_currentFocusPackObject.TryGetComponent<Animator>(out var openAnim))
                {
                    openAnim.SetTrigger("Open");
                }

                // 개봉 모션 재생 시간만큼 대기하여 팩이 뜯어지는 연출 재생
                if (packOpenAnimationDuration > 0f)
                {
                    yield return YieldInstructionCache.WaitForSeconds(packOpenAnimationDuration);
                }

                Destroy(_currentFocusPackObject);
                _currentFocusPackObject = null;
            }

            // 2) 유저 계정 데이터 실시간 동기화 (보유 팩 수량 및 획득 카드 수량)
            if (GameClient.Instance?.CurrentUser != null)
            {
                if (GameClient.Instance.CurrentUser.ownedPacks == null)
                    GameClient.Instance.CurrentUser.ownedPacks = new Dictionary<string, int>();
                if (GameClient.Instance.CurrentUser.ownedCards == null)
                    GameClient.Instance.CurrentUser.ownedCards = new Dictionary<string, int>();

                GameClient.Instance.CurrentUser.ownedPacks[packId] = updatedRemainingPacks;

                foreach (var cid in resultCardIds)
                {
                    if (!string.IsNullOrEmpty(cid))
                    {
                        GameClient.Instance.CurrentUser.ownedCards[cid] =
                            GameClient.Instance.CurrentUser.ownedCards.GetValueOrDefault(cid, 0) + 1;
                    }
                }
            }

            if (SinginManager.CurrentUserData != null)
            {
                if (SinginManager.CurrentUserData.ownedPacks == null)
                    SinginManager.CurrentUserData.ownedPacks = new Dictionary<string, int>();
                if (SinginManager.CurrentUserData.ownedCards == null)
                    SinginManager.CurrentUserData.ownedCards = new Dictionary<string, int>();

                SinginManager.CurrentUserData.ownedPacks[packId] = updatedRemainingPacks;

                foreach (var cid in resultCardIds)
                {
                    if (!string.IsNullOrEmpty(cid))
                    {
                        SinginManager.CurrentUserData.ownedCards[cid] =
                            SinginManager.CurrentUserData.ownedCards.GetValueOrDefault(cid, 0) + 1;
                    }
                }
            }

            // 3) 상자 속 원본 팩 수량 표시 동기화
            if (_selectedOriginalPack != null)
            {
                _selectedOriginalPack.UpdateCount(updatedRemainingPacks);
                if (updatedRemainingPacks <= 0)
                {
                    _selectedOriginalPack.gameObject.SetActive(false);
                }
            }

            // 4) 신규(NEW) 카드 판정 목록 계산
            List<bool> isNewFlags = new List<bool>();
            foreach (var cid in resultCardIds)
            {
                bool isNew = !previouslyOwnedCards.Contains(cid);
                isNewFlags.Add(isNew);
                if (isNew)
                {
                    previouslyOwnedCards.Add(cid); // 동일 팩 내 중복 획득 시 첫 번째만 NEW 표기
                }
            }

            // 5) 결과 버튼 그룹 활성화 및 '다시 사용' 가능 여부 갱신
            if (resultButtonGroup != null)
            {
                resultButtonGroup.SetActive(true);
            }
            UpdateUseAgainButtonState();

            // 6) 5장 카드 부채꼴 개봉 연출 시작 (서버에서 받은 카드 목록 및 NEW 판정 전달)
            StartOpeningSequence(resultCardIds, isNewFlags);
        }
        else
        {
            // 10초 이상 응답이 없거나 통신 실패 시 -> 오류 패널 표시
            Debug.LogError($"[CraftManager] ⚠️ [통신 오류] 카드팩 개봉 요청이 실패했습니다.");

            if (errorPanel != null)
            {
                errorPanel.SetActive(true);
            }

            // '다시 사용' 중 오류 발생 시 상자 속 수량 롤백 복원
            if (isUseAgain && _selectedOriginalPack != null)
            {
                if (!_selectedOriginalPack.gameObject.activeSelf)
                {
                    _selectedOriginalPack.gameObject.SetActive(true);
                    _selectedOriginalPack.UpdateCount(1);
                }
                else
                {
                    _selectedOriginalPack.UpdateCount(_selectedOriginalPack.Count + 1);
                }
                Debug.Log($"[CraftManager] ↩️ 오류 발생으로 차감되었던 수량 1개 복원 완료 (남은 수량: {_selectedOriginalPack.Count}개)");
            }
        }

        _packOpenCoroutine = null;
    }

    /// <summary>
    /// 통신 오류 패널 닫기 버튼 클릭 시 호출
    /// </summary>
    public void OnErrorCloseClicked()
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }

        // 중앙 복사본이 있다면 제거
        if (_currentFocusPackObject != null)
        {
            Destroy(_currentFocusPackObject);
            _currentFocusPackObject = null;
        }

        // 개봉된 카드가 없는 상태라면 원본 복구 후 취소 처리
        if (!_isOpeningActive)
        {
            OnCancelClicked();
        }
        else
        {
            // 이미 이전에 열린 카드가 있다면 결과 버튼 복원
            if (resultButtonGroup != null)
            {
                resultButtonGroup.SetActive(true);
            }
            UpdateUseAgainButtonState();
        }
    }

    /// <summary>
    /// 5장 카드 부채꼴 배치 및 개봉 연출 시작
    /// (서버에서 받은 cardIds와 각 카드의 신규(NEW) 여부 바인딩)
    /// </summary>
    public void StartOpeningSequence(List<string> cardIds = null, List<bool> isNewFlags = null)
    {
        ClearActiveCards();
        _isOpeningActive = true;

        // 버튼 라벨을 '모두 확인'으로 초기화
        SetConfirmButtonLabel("모두 확인");

        if (cardPrefab == null)
        {
            Debug.LogError("[CraftManager] ⚠️ cardPrefab이 할당되지 않았습니다! (Assets/Prefab/Storage/Card.prefab)");
            return;
        }

        Camera cam = cameraController != null && cameraController.TargetCamera != null
            ? cameraController.TargetCamera
            : Camera.main;

        Transform camT = cardFanAnchor != null ? cardFanAnchor : (cam != null ? cam.transform : transform);
        Vector3 camCenter = camT.position + camT.forward * distanceFromCamera + camT.up * heightOffsetFromCamera;

        // 프리팹의 원래 비율(3 : 0.05 : 4.2)을 유지하며 스케일 계산
        Vector3 basePrefabScale = cardPrefab.transform.localScale;
        Vector3 targetCardScale = new Vector3(
            basePrefabScale.x * cardScaleMultiplier,
            basePrefabScale.y * cardScaleMultiplier,
            basePrefabScale.z * cardScaleMultiplier
        );

        // 전체 카드 리소스 로드 및 ID 매핑 딕셔너리 구축
        CardData[] allCards = Resources.LoadAll<CardData>("CardData");
        Dictionary<string, CardData> cardMap = new Dictionary<string, CardData>(StringComparer.OrdinalIgnoreCase);
        if (allCards != null)
        {
            foreach (var c in allCards)
            {
                if (c != null)
                {
                    if (!string.IsNullOrEmpty(c.cardID)) cardMap[c.cardID] = c;
                    if (!string.IsNullOrEmpty(c.name) && !cardMap.ContainsKey(c.name)) cardMap[c.name] = c;
                }
            }
        }

        // 5장의 카드를 부채꼴로 생성 및 배치
        for (int i = 0; i < 5; i++)
        {
            float offset = i - 2f; // -2, -1, 0, 1, 2

            // 부채꼴 위치 계산 (수평 간격 + 높이 곡률 + 깊이 곡률 + Z파이팅 방지 미세 오프셋)
            Vector3 cardPos = camCenter
                + camT.right * (offset * cardSpacing)
                - camT.up * (offset * offset * cardArcHeight * 0.5f)
                + camT.forward * (Mathf.Abs(offset) * cardArcDepth)
                + camT.forward * (offset * 0.005f);

            // 뒷면(FaceDown) 및 앞면(FaceUp) 회전값 계산
            // Card.prefab 구조: +Y가 Front, -Y가 Back, +Z가 카드 상단
            Quaternion faceDownRot = Quaternion.AngleAxis(-offset * cardFanAngle, camT.forward)
                * Quaternion.LookRotation(camT.up, camT.forward);

            Quaternion faceUpRot = Quaternion.AngleAxis(-offset * cardFanAngle, camT.forward)
                * Quaternion.LookRotation(camT.up, -camT.forward);

            GameObject cardObj = Instantiate(cardPrefab, cardPos, faceDownRot);
            cardObj.name = $"Card_Opened_{i + 1}";
            cardObj.transform.localScale = targetCardScale;

            if (!cardObj.TryGetComponent<CardOpeningItem>(out var cardItem))
            {
                cardItem = cardObj.AddComponent<CardOpeningItem>();
            }

            // 개별 카드 플립 완료 이벤트 구독
            cardItem.OnCardFlipped += OnSingleCardFlipped;

            // 서버 응답 기반 카드 데이터 바인딩 및 신규(NEW) 여부 설정
            CardData chosenData = null;
            bool isNew = false;

            if (cardIds != null && i < cardIds.Count && !string.IsNullOrEmpty(cardIds[i]))
            {
                string targetId = cardIds[i];
                if (cardMap.TryGetValue(targetId, out var foundCard))
                {
                    chosenData = foundCard;
                }
                else
                {
                    chosenData = Resources.Load<CardData>($"CardData/{targetId}");
                }

                if (isNewFlags != null && i < isNewFlags.Count)
                {
                    isNew = isNewFlags[i];
                }
            }

            // 만약 서버 데이터를 못 찾았거나 없는 경우의 안전 폴백
            if (chosenData == null && allCards != null && allCards.Length > 0)
            {
                chosenData = allCards[UnityEngine.Random.Range(0, allCards.Length)];
            }

            cardItem.Setup(chosenData, faceDownRot, faceUpRot, cardPos, camT.forward, flipDuration, isNewCard: isNew);
            _activeCards.Add(cardItem);
        }

        Debug.Log("[CraftManager] 🎴 5장의 카드가 부채꼴로 펼쳐졌습니다! 뒷면 카드를 클릭하거나 '모두 확인'을 누르세요.");
    }

    /// <summary>
    /// 카드가 한 장 뒤집힐 때마다 호출 -> 5장 모두 앞면인지 검사하여 버튼 텍스트 변경
    /// </summary>
    private void OnSingleCardFlipped(CardOpeningItem item)
    {
        if (AreAllCardsFlipped())
        {
            SetConfirmButtonLabel("확인");
            Debug.Log("[CraftManager] 🎴 5장의 카드가 모두 공개되었습니다! 버튼이 '확인'으로 전환됩니다.");
        }
    }

    /// <summary>
    /// 현재 5장의 카드가 모두 앞면으로 뒤집혔는지 검사
    /// </summary>
    public bool AreAllCardsFlipped()
    {
        if (_activeCards == null || _activeCards.Count == 0) return true;

        for (int i = 0; i < _activeCards.Count; i++)
        {
            if (_activeCards[i] != null && !_activeCards[i].IsFlipped)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// '모두 확인 / 확인' 버튼 클릭 통합 처리
    /// - 5장 확인 전: '모두 확인' (0.08초 간격으로 순차 자동 플립)
    /// - 5장 확인 후: '확인' (카드 정리 및 창고 복귀)
    /// </summary>
    public void HandleConfirmButtonClicked()
    {
        if (!AreAllCardsFlipped())
        {
            RevealAllCards();
        }
        else
        {
            OnConfirmClicked();
        }
    }

    /// <summary>
    /// 아직 뒤집히지 않은 뒷면 카드들을 0.08초 시차를 두고 순차적으로 뒤집기
    /// </summary>
    public void RevealAllCards()
    {
        float delay = 0f;
        for (int i = 0; i < _activeCards.Count; i++)
        {
            CardOpeningItem card = _activeCards[i];
            if (card != null && !card.IsFlipped && !card.IsFlipping)
            {
                DOVirtual.DelayedCall(delay, () =>
                {
                    if (card != null && !card.IsFlipped)
                    {
                        card.Flip();
                    }
                });
                delay += 0.08f;
            }
        }
    }

    /// <summary>
    /// '다시 사용' 버튼 클릭 시 호출
    /// - 아직 뒤집히지 않은 카드가 있다면: 모두 뒤집고 delayAfterRevealOnUseAgain초 대기 후 다음 팩 사용
    /// - 이미 5장 모두 확인되었다면: 즉시 다음 팩 사용
    /// 1. 기존 5장 카드가 작아지면서 소멸
    /// 2. 다음 카드팩 복사본이 중앙(focusSpot)에 등장
    /// 3. 상자 속 카드팩 수량 차감
    /// 4. 로딩 패널 표시 및 서버 패킷 요청 -> 성공 시 복사본 소멸 & 새로운 5장 부채꼴 등장
    /// </summary>
    public void OnUseAgainClicked()
    {
        if (_selectedOriginalPack == null || _packOpenCoroutine != null || _useAgainCoroutine != null) return;

        // 1. 상자 안 원본 카드팩의 남은 수량 확인
        if (!_selectedOriginalPack.gameObject.activeSelf || _selectedOriginalPack.Count <= 0)
        {
            Debug.LogWarning("[CraftManager] ⚠️ 상자에 남은 카드팩이 없어 다시 사용할 수 없습니다.");
            UpdateUseAgainButtonState();
            return;
        }

        // 결과 버튼 일시 비활성화 (다시 사용/확인 중복 클릭 방지)
        if (resultButtonGroup != null)
        {
            resultButtonGroup.SetActive(false);
        }

        // 상자 스크롤러 잠금 유지
        if (scroller != null)
        {
            scroller.IsInteractive = false;
        }

        // 미확인(뒷면) 카드가 남아있는 경우: 모두 뒤집고 대기 후 다음 팩 사용
        if (!AreAllCardsFlipped())
        {
            _useAgainCoroutine = StartCoroutine(CoRevealAndUseAgain());
        }
        else
        {
            ExecuteUseAgainTransition();
        }
    }

    /// <summary>
    /// 미공개 카드를 모두 뒤집은 후 일정 시간 대기했다가 다음 팩 개봉으로 넘어가는 코루틴
    /// </summary>
    private IEnumerator CoRevealAndUseAgain()
    {
        // 1. 아직 뒤집히지 않은 카드 모두 순차적으로 뒤집기
        RevealAllCards();

        // 2. 모든 카드가 완전히 뒤집힐 때까지 대기 (최대 3초 안전 타임아웃)
        float elapsed = 0f;
        while (!AreAllCardsFlipped() && elapsed < 3f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. 카드 확인을 위한 약간의 딜레이
        if (delayAfterRevealOnUseAgain > 0f)
        {
            yield return new WaitForSeconds(delayAfterRevealOnUseAgain);
        }

        _useAgainCoroutine = null;

        // 4. 다음 팩 사용 연출 진행
        ExecuteUseAgainTransition();
    }

    /// <summary>
    /// 다음 카드팩 사용 연출 및 서버 개봉 요청 실행
    /// </summary>
    private void ExecuteUseAgainTransition()
    {
        // 2. 기존 5장 카드가 작아지면서 소멸하는 연출
        for (int i = 0; i < _activeCards.Count; i++)
        {
            if (_activeCards[i] != null)
            {
                _activeCards[i].OnCardFlipped -= OnSingleCardFlipped;
                var cardObj = _activeCards[i].gameObject;
                cardObj.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).OnComplete(() =>
                {
                    Destroy(cardObj);
                });
            }
        }
        _activeCards.Clear();
        _isOpeningActive = false;

        // 3. 카드가 작아지는 시간(0.35초) 후 다음 카드팩 복사본 스폰 & 상자 수량 차감 & 서버 패킷 요청
        DOVirtual.DelayedCall(0.35f, () =>
        {
            if (_selectedOriginalPack == null) return;

            // 상자 안의 수량 1 차감
            int newCount = _selectedOriginalPack.Count - 1;
            if (newCount <= 0)
            {
                _selectedOriginalPack.gameObject.SetActive(false);
                Debug.Log($"[CraftManager] 📦 상자 속 원본 수량 0개 소진 (숨김 처리)");
            }
            else
            {
                _selectedOriginalPack.UpdateCount(newCount);
                Debug.Log($"[CraftManager] 📦 상자 속 원본 남은 수량: {newCount}개");
            }

            // 중앙 복사본 새로 생성 및 통통 튀는 팝업 연출
            if (cardPackPrefab != null)
            {
                Vector3 spawnPos = focusSpot != null ? focusSpot.position : transform.position;
                Quaternion spawnRot = (focusSpot != null ? focusSpot.rotation : Quaternion.identity) * Quaternion.Euler(focusPackRotation);

                _currentFocusPackObject = Instantiate(cardPackPrefab, spawnPos, spawnRot);
                _currentFocusPackObject.name = $"{_selectedOriginalPack.PackId}_FocusCopy";
                _currentFocusPackObject.transform.localScale = Vector3.zero;
                _currentFocusPackObject.transform.DOScale(focusPackScale, 0.25f).SetEase(Ease.OutBack);

                if (_currentFocusPackObject.TryGetComponent<CardPackItem>(out var copyItem))
                {
                    copyItem.Setup(_selectedOriginalPack.PackId, 1);
                    copyItem.SetTextActive(false); // 수량 텍스트 숨김
                }

                if (_currentFocusPackObject.TryGetComponent<Animator>(out var copyAnim2))
                {
                    copyAnim2.Play("Idle", 0, 0f);
                }
            }

            _isFocusing = true;

            // 0.3초 후 서버 요청 및 로딩 시작
            DOVirtual.DelayedCall(0.3f, () =>
            {
                _packOpenCoroutine = StartCoroutine(CoRequestPackOpen(isUseAgain: true));
            });
        });
    }

    /// <summary>
    /// '확인' 버튼 클릭 시 호출 (5장 모두 확인 완료 상태에서만 진입)
    /// </summary>
    public void OnConfirmClicked()
    {
        // 1. 5장의 카드 파괴
        ClearActiveCards();

        // 2. 중앙 복사본 카드팩 파괴 (혹시 남아있다면)
        if (_currentFocusPackObject != null)
        {
            Destroy(_currentFocusPackObject);
            _currentFocusPackObject = null;
        }

        // 3. 결과 버튼 숨김
        if (resultButtonGroup != null)
        {
            resultButtonGroup.SetActive(false);
        }

        // 4. 상태 초기화 (상자의 원본은 차감된 수량 또는 숨김 상태 그대로 유지)
        _isFocusing = false;
        _isOpeningActive = false;
        _selectedOriginalPack = null;

        // 상자 스크롤러 상호작용 복구
        if (scroller != null)
        {
            scroller.IsInteractive = true;
        }

        Debug.Log("[CraftManager] ✅ 카드팩 개봉 확인 완료! 창고 상태로 복귀합니다.");
    }

    /// <summary>
    /// 확인 버튼 텍스트 라벨 실시간 변경 ('모두 확인' <-> '확인')
    /// </summary>
    private void SetConfirmButtonLabel(string label)
    {
        if (btnConfirm == null) return;

        if (btnConfirm.TryGetComponent<TMP_Text>(out var tmp))
        {
            tmp.text = label;
        }
        else
        {
            var childTmp = btnConfirm.GetComponentInChildren<TMP_Text>();
            if (childTmp != null)
            {
                childTmp.text = label;
            }
            else
            {
                var legacyText = btnConfirm.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    legacyText.text = label;
                }
            }
        }
    }

    /// <summary>
    /// '다시 사용' 버튼 활성화/비활성화 상태 갱신
    /// </summary>
    private void UpdateUseAgainButtonState()
    {
        if (btnUseAgain == null) return;

        bool hasRemaining = _selectedOriginalPack != null
            && _selectedOriginalPack.gameObject.activeSelf
            && _selectedOriginalPack.Count > 0;

        btnUseAgain.interactable = hasRemaining;
    }

    /// <summary>
    /// 현재 열려 있는 5장 카드 오브젝트 정리
    /// </summary>
    public void ClearActiveCards()
    {
        for (int i = 0; i < _activeCards.Count; i++)
        {
            if (_activeCards[i] != null)
            {
                _activeCards[i].OnCardFlipped -= OnSingleCardFlipped;
                Destroy(_activeCards[i].gameObject);
            }
        }
        _activeCards.Clear();
        _isOpeningActive = false;
    }

    /// <summary>
    /// 계정의 카드팩 정보 조회 및 스폰 트리거
    /// </summary>
    public void RefreshOwnedPacks()
    {
        Dictionary<string, int> ownedPacks = GetOwnedPacks();

        if (ownedPacks == null || ownedPacks.Count == 0)
        {
            Debug.Log("[CraftManager] 📦 현재 계정에 보유 중인 카드팩이 없습니다.");
            ClearSpawnedPacks();
            return;
        }

        Debug.Log($"[CraftManager] 📦 보유 카드팩 총 {ownedPacks.Count}종 확인:");
        foreach (var kvp in ownedPacks)
        {
            Debug.Log($"  ▶ [카드팩 ID: {kvp.Key}] 수량: {kvp.Value}개");
        }

        // 책상 위에 3D 카드팩 프리팹 생성 및 배치
        SpawnCardPacks(ownedPacks);
    }

    /// <summary>
    /// GameClient 또는 SinginManager로부터 실제 보유 팩 데이터 획득
    /// </summary>
    public Dictionary<string, int> GetOwnedPacks()
    {
        if (GameClient.Instance != null && GameClient.Instance.CurrentUser != null)
        {
            if (GameClient.Instance.CurrentUser.ownedPacks != null)
            {
                return GameClient.Instance.CurrentUser.ownedPacks;
            }
        }

        if (SinginManager.CurrentUserData != null && SinginManager.CurrentUserData.ownedPacks != null)
        {
            return SinginManager.CurrentUserData.ownedPacks;
        }

        if (useDummyDataIfNoLogin)
        {
            Debug.LogWarning("[CraftManager] ⚠️ 로그인된 계정 정보(GameClient)가 없어 테스트용 가상 팩 데이터를 사용합니다.");
            return new Dictionary<string, int>
            {
                { "CardPack_0001", 3 },
                { "CardPack_yuni_0001", 1 }
            };
        }

        return new Dictionary<string, int>();
    }

    /// <summary>
    /// 작업대 컨테이너 안에 프리팹을 일정한 간격으로 생성
    /// </summary>
    private void SpawnCardPacks(Dictionary<string, int> packs)
    {
        if (cardPackPrefab == null)
        {
            Debug.LogWarning("[CraftManager] ⚠️ cardPackPrefab이 인스펙터에 할당되지 않아 3D 카드팩 생성을 건너뜁니다. (프리팹을 인스펙터에 연결해 주세요)");
            return;
        }

        Transform parentTransform = packContainer != null ? packContainer : transform;

        // 1. 기존 생성 팩 정리
        ClearSpawnedPacks();

        // 2. 카드팩 인스턴스 생성 및 일정 간격 배치
        int currentIndex = 0;
        foreach (var kvp in packs)
        {
            string packId = kvp.Key;
            int count = kvp.Value;
            if (count <= 0) continue;

            int spawnAmount = spawnPerCount ? count : 1;

            for (int i = 0; i < spawnAmount; i++)
            {
                GameObject packInstance = Instantiate(cardPackPrefab, parentTransform);
                packInstance.name = $"{cardPackPrefab.name}_{packId}_{i + 1}";

                // 간격 위치, 회전 및 크기(사이즈) 설정
                Vector3 localPos = startLocalPosition + spawnAxis.normalized * (currentIndex * packSpacing);
                packInstance.transform.localPosition = localPos;
                packInstance.transform.localRotation = Quaternion.Euler(packLocalRotation);
                packInstance.transform.localScale = packLocalScale;

                // 스폰된 카드팩의 애니메이션은 대기(Idle) 상태 유지
                if (packInstance.TryGetComponent<Animator>(out var anim))
                {
                    anim.Play("Idle", 0, 0f);
                }

                // CardPackItem 컴포넌트 데이터 및 수량 텍스트 자동 초기화
                if (packInstance.TryGetComponent<CardPackItem>(out var packItem))
                {
                    packItem.Setup(packId, count);
                }

                _spawnedPacks.Add(packInstance);
                currentIndex++;
            }
        }

        Debug.Log($"[CraftManager] 📦 총 {currentIndex}개의 카드팩 3D 오브젝트 생성 및 배치 완료!");

        // 3. 스크롤러 연동: 스크롤러 대상 컨테이너 설정 및 경계 한계선 자동 갱신
        if (scroller != null)
        {
            scroller.SetTargetObject(parentTransform);
            scroller.UpdateScrollLimits();
            scroller.IsInteractive = true;
        }
    }

    /// <summary>
    /// 이전에 생성된 카드팩 오브젝트 일괄 제거
    /// </summary>
    public void ClearSpawnedPacks()
    {
        if (_packOpenCoroutine != null)
        {
            StopCoroutine(_packOpenCoroutine);
            _packOpenCoroutine = null;
        }

        if (_useAgainCoroutine != null)
        {
            StopCoroutine(_useAgainCoroutine);
            _useAgainCoroutine = null;
        }

        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (errorPanel != null) errorPanel.SetActive(false);

        ClearActiveCards();

        if (scroller != null)
        {
            scroller.IsInteractive = true;
        }

        if (_isFocusing)
        {
            if (_currentFocusPackObject != null)
            {
                Destroy(_currentFocusPackObject);
                _currentFocusPackObject = null;
            }
            _isFocusing = false;
            _selectedOriginalPack = null;
            if (focusButtonGroup != null)
            {
                focusButtonGroup.SetActive(false);
            }
            if (resultButtonGroup != null)
            {
                resultButtonGroup.SetActive(false);
            }
        }

        for (int i = 0; i < _spawnedPacks.Count; i++)
        {
            if (_spawnedPacks[i] != null)
            {
                Destroy(_spawnedPacks[i]);
            }
        }
        _spawnedPacks.Clear();

        if (packContainer != null)
        {
            for (int i = packContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(packContainer.GetChild(i).gameObject);
            }
        }
    }

    // ==================================================================
    // 카드팩 직업별 3D 머티리얼 및 전개도 텍스처 관리
    // ==================================================================

    /// <summary>
    /// 인스펙터에 머티리얼이 미할당된 경우 Resources/Items/CardPack/Materials/ 경로에서 자동 로드
    /// </summary>
    public void EnsurePackMaterialsLoaded()
    {
        if (gangziPackMaterial == null)
        {
            gangziPackMaterial = Resources.Load<Material>("Items/CardPack/Materials/M_CardPack_Gangzi");
        }
        if (yuniPackMaterial == null)
        {
            yuniPackMaterial = Resources.Load<Material>("Items/CardPack/Materials/M_CardPack_Yuni");
        }
        if (huyaPackMaterial == null)
        {
            huyaPackMaterial = Resources.Load<Material>("Items/CardPack/Materials/M_CardPack_Huya");
        }
    }

    /// <summary>
    /// 카드팩 ID를 분석하여 직업에 맞는 전용 URP Lit 머티리얼 에셋을 반환합니다.
    /// 맞는 직업이 없거나 기본팩인 경우 M_CardPack_Gangzi.mat를 반환합니다.
    /// </summary>
    public Material GetPackMaterial(string packId)
    {
        EnsurePackMaterialsLoaded();

        if (string.IsNullOrEmpty(packId)) return gangziPackMaterial;

        string lower = packId.ToLower();

        if (lower.Contains("yuni"))
        {
            return yuniPackMaterial != null ? yuniPackMaterial : gangziPackMaterial;
        }
        if (lower.Contains("huya"))
        {
            return huyaPackMaterial != null ? huyaPackMaterial : gangziPackMaterial;
        }
        if (lower.Contains("gangzi"))
        {
            return gangziPackMaterial;
        }

        // 맞는 직업이 없는 경우 기본값 (강지 머티리얼)
        return gangziPackMaterial;
    }

    /// <summary>
    /// 인스펙터에 텍스처가 미할당된 경우 Assets/Sprite/Storage/ 경로에서 자동 로드
    /// </summary>
    public void EnsurePackTexturesLoaded()
    {
        if (gangziPackTexture == null)
        {
            gangziPackTexture = LoadTextureFromDiskOrResources("Assets/Sprite/Storage/gangzi.png");
        }
        if (yuniPackTexture == null)
        {
            yuniPackTexture = LoadTextureFromDiskOrResources("Assets/Sprite/Storage/yuni.png");
        }
        if (huyaPackTexture == null)
        {
            huyaPackTexture = LoadTextureFromDiskOrResources("Assets/Sprite/Storage/huya.png");
        }
    }

    /// <summary>
    /// 에셋 경로 또는 로컬 파일에서 Texture2D 로드
    /// </summary>
    private Texture2D LoadTextureFromDiskOrResources(string relativeAssetPath)
    {
#if UNITY_EDITOR
        var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(relativeAssetPath);
        if (tex != null) return tex;
#endif
        string fullPath = System.IO.Path.Combine(Application.dataPath, relativeAssetPath.Replace("Assets/", "").Replace('\\', '/'));
        if (System.IO.File.Exists(fullPath))
        {
            try
            {
                byte[] data = System.IO.File.ReadAllBytes(fullPath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(data))
                {
                    return texture;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CraftManager] 텍스처 파일 로드 실패 ({fullPath}): {ex.Message}");
            }
        }
        return null;
    }

    /// <summary>
    /// 카드팩 ID를 분석하여 직업에 맞는 전개도 텍스처를 반환합니다.
    /// 맞는 직업이 없거나 기본팩인 경우 gangzi.png를 반환합니다.
    /// </summary>
    public Texture2D GetPackTexture(string packId)
    {
        EnsurePackTexturesLoaded();

        if (string.IsNullOrEmpty(packId)) return gangziPackTexture;

        string lower = packId.ToLower();

        if (lower.Contains("yuni"))
        {
            return yuniPackTexture != null ? yuniPackTexture : gangziPackTexture;
        }
        if (lower.Contains("huya"))
        {
            return huyaPackTexture != null ? huyaPackTexture : gangziPackTexture;
        }
        if (lower.Contains("gangzi"))
        {
            return gangziPackTexture;
        }

        // 맞는 직업이 없는 경우 기본값 gangzi.png
        return gangziPackTexture;
    }
}
