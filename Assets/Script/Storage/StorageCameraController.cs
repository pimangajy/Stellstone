using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using DG.Tweening;

/// <summary>
/// 개별 구역 카메라 설정
/// </summary>
[System.Serializable]
public class StorageSection
{
    [Tooltip("구역 이름 (예: Craft, Vanity, Wardrobe)")]
    public string sectionName = "Section";

    [Tooltip("클릭할 Canvas 상의 판넬/버튼 오브젝트")]
    public GameObject clickPanel;

    [Tooltip("판넬에 부착된 Button 컴포넌트 (비워둘 시 clickPanel에서 자동 탐색)")]
    public Button panelButton;

    [Tooltip("카메라가 이동할 3D 위치 Transform")]
    public Transform cameraTarget;
}

/// <summary>
/// 창고 씬 전용 카메라 이동 컨트롤러
/// - Canvas 버튼 클릭 시 목표 구역(Transform)으로 카메라 부드럽게 이동/회전
/// - Back 버튼 또는 ESC 키 입력 시 전체 조망(Overview)으로 복귀
/// </summary>
public class StorageCameraController : MonoBehaviour
{
    [Header("카메라 설정")]
    [Tooltip("이동시킬 카메라 (비워두면 Main Camera 자동 사용)")]
    [SerializeField] private Camera targetCamera;
    public Camera TargetCamera => targetCamera;

    [Tooltip("기본 전체 조망(Overview) 위치 Transform")]
    [SerializeField] private Transform overviewTarget;

    [Header("구역 목록")]
    [SerializeField] private List<StorageSection> sections = new List<StorageSection>();

    [Header("UI 설정")]
    [Tooltip("전체 조망 상태에서 3개 구역 선택 판넬이 포함된 그룹 (구역 진입 시 비활성화)")]
    [SerializeField] private GameObject overviewPanelGroup;

    [Tooltip("전체 조망 복귀용 뒤로가기 버튼")]
    [SerializeField] private Button backButton;

    [Tooltip("ESC 키로 전체 조망 복귀 허용 여부")]
    [SerializeField] private bool allowEscapeKeyToReturn = true;

    [Header("카메라 이동 연출")]
    [Tooltip("카메라 이동 시간 (초)")]
    [SerializeField] private float moveDuration = 1.0f;

    [Tooltip("카메라 이동 이징 곡선")]
    [SerializeField] private Ease moveEase = Ease.InOutCubic;

    [Header("이벤트 (선택사항)")]
    public UnityEvent<int> onSectionEnter;
    public UnityEvent onReturnOverview;

    private int _currentSectionIndex = -1; // -1: Overview 상태
    private bool _isTransitioning = false;
    private Tween _moveTween;
    private Tween _rotTween;

    private void Awake()
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("[StorageCameraController] ⚠️ targetCamera가 인스펙터에 할당되지 않았습니다.");
        }
        if (overviewTarget == null)
        {
            Debug.LogWarning("[StorageCameraController] ⚠️ overviewTarget이 인스펙터에 할당되지 않았습니다.");
        }
    }

    private void Start()
    {
        // 1. 구역별 클릭 버튼 바인딩
        for (int i = 0; i < sections.Count; i++)
        {
            int index = i;
            var sec = sections[i];

            if (sec.panelButton == null)
            {
                Debug.LogWarning($"[StorageCameraController] ⚠️ sections[{i}] ({sec.sectionName})의 panelButton이 인스펙터에 할당되지 않았습니다.");
            }
            else
            {
                sec.panelButton.onClick.AddListener(() => MoveToSection(index));
            }

            if (sec.cameraTarget == null)
            {
                Debug.LogWarning($"[StorageCameraController] ⚠️ sections[{i}] ({sec.sectionName})의 cameraTarget이 인스펙터에 할당되지 않았습니다.");
            }
        }

        // 2. 뒤로가기 버튼 바인딩
        if (backButton != null)
        {
            backButton.onClick.AddListener(MoveToOverview);
            backButton.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[StorageCameraController] ⚠️ backButton이 인스펙터에 할당되지 않았습니다.");
        }

        // 3. 초기 상태: 전체 조망 UI 활성화
        if (overviewPanelGroup != null)
        {
            overviewPanelGroup.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[StorageCameraController] ⚠️ overviewPanelGroup이 인스펙터에 할당되지 않았습니다.");
        }
    }

    private void Update()
    {
        if (allowEscapeKeyToReturn && Input.GetKeyDown(KeyCode.Escape))
        {
            MoveToOverview();
        }
    }

    private void OnDestroy()
    {
        _moveTween?.Kill();
        _rotTween?.Kill();
    }

    /// <summary>
    /// 지정된 인덱스의 구역으로 카메라 이동
    /// </summary>
    public void MoveToSection(int sectionIndex)
    {
        if (_isTransitioning) return;
        if (sectionIndex < 0 || sectionIndex >= sections.Count) return;

        StorageSection targetSection = sections[sectionIndex];
        if (targetSection.cameraTarget == null) return;

        _isTransitioning = true;
        _currentSectionIndex = sectionIndex;

        // 전체 판넬 비활성화 및 뒤로가기 버튼 임시 숨김
        if (overviewPanelGroup != null) overviewPanelGroup.SetActive(false);
        if (backButton != null) backButton.gameObject.SetActive(false);

        // 카메라 이동
        AnimateCamera(targetSection.cameraTarget, () =>
        {
            _isTransitioning = false;
            if (backButton != null) backButton.gameObject.SetActive(true);
            onSectionEnter?.Invoke(sectionIndex);
            Debug.Log($"[StorageCameraController] 구역 진입 완료: {targetSection.sectionName}");
        });
    }

    /// <summary>
    /// 전체 조망(Overview) 위치로 카메라 복귀
    /// </summary>
    public void MoveToOverview()
    {
        if (_isTransitioning || _currentSectionIndex == -1) return;
        if (overviewTarget == null) return;

        _isTransitioning = true;

        if (backButton != null) backButton.gameObject.SetActive(false);

        AnimateCamera(overviewTarget, () =>
        {
            _isTransitioning = false;
            _currentSectionIndex = -1;

            if (overviewPanelGroup != null) overviewPanelGroup.SetActive(true);
            onReturnOverview?.Invoke();
            Debug.Log("[StorageCameraController] 전체 조망(Overview) 복귀 완료");
        });
    }

    /// <summary>
    /// 목표 Transform으로 카메라 위치/회전 보간 이동 (DOTween)
    /// </summary>
    private void AnimateCamera(Transform target, Action onComplete)
    {
        if (targetCamera == null || target == null)
        {
            _isTransitioning = false;
            onComplete?.Invoke();
            return;
        }

        _moveTween?.Kill();
        _rotTween?.Kill();

        Transform camTransform = targetCamera.transform;

        _moveTween = camTransform.DOMove(target.position, moveDuration)
            .SetEase(moveEase);

        _rotTween = camTransform.DORotate(target.rotation.eulerAngles, moveDuration)
            .SetEase(moveEase)
            .OnComplete(() => onComplete?.Invoke());
    }
}
