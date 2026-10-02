using System;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// 개별 3D 카드 개봉 아이템
/// - 개봉 연출 시 뒷면으로 등장
/// - 클릭 시 180도 회전(Flip)하며 앞면 공개 (IPointerClickHandler, OnMouseDown, Raycast 3중 지원)
/// - HandCardDisplay 컴포넌트를 통한 카드 정보 바인딩
/// - 인스펙터 미할당 시 자동 탐색 없이 경고 로그 출력
/// </summary>
public class CardOpeningItem : MonoBehaviour, IPointerClickHandler
{
    [Header("카드 면 오브젝트")]
    [Tooltip("카드 앞면 오브젝트 (Canvas)")]
    [SerializeField] private GameObject frontObj;

    [Tooltip("카드 뒷면 오브젝트 (Canvas)")]
    [SerializeField] private GameObject backObj;

    [Tooltip("카드 데이터 표시 컴포넌트")]
    [SerializeField] private HandCardDisplay cardDisplay;

    [Tooltip("카드 충돌체 (BoxCollider 등)")]
    [SerializeField] private Collider cardCollider;

    [Header("신규 카드 (NEW) 설정")]
    [Tooltip("신규 획득 카드일 때 활성화할 NEW 이미지 오브젝트 (인스펙터에서 직접 연결)")]
    [SerializeField] private GameObject newCardImage;

    [Header("플립 연출 설정")]
    [Tooltip("180도 회전 시간 (초)")]
    [SerializeField] private float flipDuration = 0.45f;

    [Tooltip("플립 시 카메라 쪽으로 들리는 거리 (m)")]
    [SerializeField] private float liftDistance = 0.12f;

    [Tooltip("플립 이징 효과")]
    [SerializeField] private Ease flipEase = Ease.OutBack;

    [Header("상태")]
    public bool IsFlipped { get; private set; } = false;
    public bool IsFlipping { get; private set; } = false;
    public bool IsNewCard => _isNewCard;

    // 이벤트
    public event Action<CardOpeningItem> OnCardFlipped;

    private Quaternion _faceDownRot;
    private Quaternion _faceUpRot;
    private Vector3 _basePos;
    private Vector3 _camForward;
    private bool _isNewCard = false;
    private Tween _moveTween;
    private Tween _rotTween;

    private void Awake()
    {
        CheckInspectorReferences();
    }

    /// <summary>
    /// 인스펙터 참조 누락 검사 (자동 채우기 없이 경고 로그 출력)
    /// </summary>
    private void CheckInspectorReferences()
    {
        if (frontObj == null)
        {
            Debug.LogWarning($"[CardOpeningItem] ⚠️ {name}: frontObj가 인스펙터에 연결되지 않았습니다.");
        }

        if (backObj == null)
        {
            Debug.LogWarning($"[CardOpeningItem] ⚠️ {name}: backObj가 인스펙터에 연결되지 않았습니다.");
        }

        if (cardDisplay == null)
        {
            Debug.LogWarning($"[CardOpeningItem] ⚠️ {name}: cardDisplay가 인스펙터에 연결되지 않았습니다.");
        }

        if (cardCollider == null)
        {
            Debug.LogWarning($"[CardOpeningItem] ⚠️ {name}: cardCollider가 인스펙터에 연결되지 않았습니다.");
        }
        else if (!cardCollider.enabled)
        {
            cardCollider.enabled = true;
        }

        if (newCardImage == null)
        {
            Debug.LogWarning($"[CardOpeningItem] ⚠️ {name}: newCardImage가 인스펙터에 연결되지 않았습니다.");
        }
    }

    /// <summary>
    /// 카드 초기 설정 (위치, 회전, 데이터 바인딩, 신규 카드 여부)
    /// </summary>
    public void Setup(CardData data, Quaternion faceDownRot, Quaternion faceUpRot, Vector3 basePos, Vector3 camForward, float customFlipDuration = 0.45f, bool isNewCard = false)
    {
        _faceDownRot = faceDownRot;
        _faceUpRot = faceUpRot;
        _basePos = basePos;
        _camForward = camForward;
        flipDuration = customFlipDuration;
        _isNewCard = isNewCard;

        IsFlipped = false;
        IsFlipping = false;

        transform.position = basePos;
        transform.rotation = faceDownRot;

        // Front와 Back의 로컬 트랜스폼 및 렌더링 방향 보정
        if (frontObj != null)
        {
            frontObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            frontObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            frontObj.SetActive(true);
        }

        if (backObj != null)
        {
            // Back이 카드의 반대쪽(-Y 방향)을 향하도록 보정
            backObj.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);
            backObj.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            backObj.SetActive(true);
        }

        // 초기에는 NEW 이미지를 꺼둠 (뒤집었을 때 신규 카드만 켜짐)
        if (newCardImage != null)
        {
            newCardImage.SetActive(false);
        }

        // 카드 데이터 바인딩
        if (data != null && cardDisplay != null)
        {
            cardDisplay.Setup(data, null);
        }
    }

    /// <summary>
    /// EventSystem UI 그래픽 클릭 수신 (GraphicRaycaster 연동)
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        Flip();
    }

    /// <summary>
    /// Unity 3D 물리 충돌체 클릭 수신 (Physics Collider 연동)
    /// </summary>
    private void OnMouseDown()
    {
        Flip();
    }

    /// <summary>
    /// 카드 180도 뒤집기 연출
    /// </summary>
    public void Flip()
    {
        if (IsFlipped || IsFlipping) return;

        IsFlipping = true;

        _moveTween?.Kill();
        _rotTween?.Kill();

        // 1. 카메라 방향으로 살짝 떠오르는 연출 (Pop & Return)
        Vector3 liftPos = _basePos - _camForward * liftDistance;
        Sequence moveSeq = DOTween.Sequence();
        moveSeq.Append(transform.DOMove(liftPos, flipDuration * 0.45f).SetEase(Ease.OutQuad));
        moveSeq.Append(transform.DOMove(_basePos, flipDuration * 0.55f).SetEase(Ease.InQuad));
        _moveTween = moveSeq;

        // 2. 180도 회전하여 앞면 노출
        _rotTween = transform.DORotateQuaternion(_faceUpRot, flipDuration)
            .SetEase(flipEase)
            .OnComplete(() =>
            {
                IsFlipped = true;
                IsFlipping = false;

                // 신규 카드라면 NEW 이미지를 켜고, 이미 있는 카드라면 켜지 않음!
                if (newCardImage != null)
                {
                    newCardImage.SetActive(_isNewCard);
                }

                OnCardFlipped?.Invoke(this);
            });

        Debug.Log($"[CardOpeningItem] 🃏 카드 앞면 공개! ({name}) {(_isNewCard ? "✨ [NEW!]" : "")}");
    }

    private void OnDestroy()
    {
        _moveTween?.Kill();
        _rotTween?.Kill();
    }
}
