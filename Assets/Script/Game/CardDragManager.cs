using UnityEngine;
using DG.Tweening;
using System.Collections;

/// <summary>
/// (수정) 카드의 드래그, 드랍, 타겟팅, 틸트 효과 및 '소환 연출'을 담당합니다.
/// 사용자 요청 반영: 소환된 하수인에게 손패 카드의 데이터를 전달하여 동일한 정보를 가지게 함.
/// </summary>
public class CardDragManager : MonoBehaviour
{
    public static CardDragManager instance;

    [Header("연결")]
    public HandInteractionManager handManager;
    public Camera mainCamera;

    [Header("드래그 설정")]
    public float dragHeight = 0.5f;
    public float dragFollowSpeed = 10f;

    [Header("틸트 효과")]
    public float tiltStrength = 20f;
    public float maxTiltAngle = 20f;
    public float tiltReturnSpeed = 5f;

    [Header("영역 및 레이어")]
    public float handZoneHeightRatio = 0.35f;
    public LayerMask cardLayer;
    public LayerMask handHoverPlaneLayer;
    public LayerMask fieldSlotLayer;

    [Header("타겟팅")]
    public bool temp_CardIsTargeted = true;
    public Transform targetingSourceTransform;

    [Header("소환 테스트 (TEMP)")]
    [Tooltip("테스트용 하수인 3D 모델 프리팹 (CardDisplay 컴포넌트가 있어야 함)")]
    public GameObject testMinionPrefab;

    // 내부 상태 변수
    private GameObject _currentCard;
    private bool _isDragging = false;
    private Plane _handMathPlane;
    private Plane _playfieldMathPlane;
    private Vector3 _lastPosition;

    private bool _isSpawning = false;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        _playfieldMathPlane = new Plane(Vector3.up, Vector3.zero);
        if (targetingSourceTransform == null && handManager != null)
            targetingSourceTransform = handManager.handAnchor;
    }

    private void Update()
    {
        if (handManager == null) return;

        _handMathPlane = new Plane(handManager.handAnchor.up, handManager.handAnchor.position);

        HandleInput();

        if (_isDragging && _currentCard != null)
        {
            UpdateCardPositionAndTilt();
        }
    }

    private void HandleInput()
    {
        if (_isSpawning) return;

        if (Input.GetMouseButtonDown(0))
        {
            GameObject hoveredCard = handManager.GetHoveredCard();
            if (handManager.IsHandStable && hoveredCard != null)
            {
                StartDrag(hoveredCard);
            }
        }

        if (Input.GetMouseButton(0) && _isDragging)
        {
            CheckZoneAndToggleTargeting();
        }

        if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            EndDrag();
        }
    }

    private void StartDrag(GameObject card)
    {
        _currentCard = card;
        _isDragging = true;
        _lastPosition = card.transform.position;

        handManager.SetDraggedCard(_currentCard);

        _currentCard.transform.DOKill();
        _currentCard.transform.DOScale(handManager.OriginalCardScale, 0.2f).SetEase(Ease.OutQuad);
        _currentCard.transform.rotation = handManager.handAnchor.rotation;
    }

    private void UpdateCardPositionAndTilt()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        float enter;
        Vector3 targetPos = _currentCard.transform.position;

        if (IsMouseInHandZone())
        {
            if (_handMathPlane.Raycast(ray, out enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 localHit = handManager.handAnchor.InverseTransformPoint(hitPoint);
                Vector3 targetLocal = new Vector3(localHit.x, dragHeight, localHit.z);
                targetPos = handManager.handAnchor.TransformPoint(targetLocal);
            }
        }
        else
        {
            if (_playfieldMathPlane.Raycast(ray, out enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 localHit = handManager.handAnchor.InverseTransformPoint(hitPoint);
                Vector3 targetLocal = new Vector3(localHit.x, dragHeight, localHit.z);
                targetPos = handManager.handAnchor.TransformPoint(targetLocal);
            }
        }

        _currentCard.transform.position = Vector3.Lerp(_currentCard.transform.position, targetPos, Time.deltaTime * dragFollowSpeed);
        ApplyDragTilt();
        _lastPosition = _currentCard.transform.position;
    }

    private void ApplyDragTilt()
    {
        Vector3 velocity = (_currentCard.transform.position - _lastPosition) / Time.deltaTime;
        Vector3 localVelocity = handManager.handAnchor.InverseTransformDirection(velocity);

        float targetRotX = localVelocity.z * tiltStrength;
        float targetRotZ = -localVelocity.x * tiltStrength;

        targetRotX = Mathf.Clamp(targetRotX, -maxTiltAngle, maxTiltAngle);
        targetRotZ = Mathf.Clamp(targetRotZ, -maxTiltAngle, maxTiltAngle);

        Quaternion targetRotation = handManager.handAnchor.rotation * Quaternion.Euler(targetRotX, 0, targetRotZ);
        _currentCard.transform.rotation = Quaternion.Slerp(_currentCard.transform.rotation, targetRotation, Time.deltaTime * tiltReturnSpeed);
    }

    private void CheckZoneAndToggleTargeting()
    {
        bool inHandZone = IsMouseInHandZone();

        if (inHandZone)
        {
            if (!_currentCard.activeSelf) _currentCard.SetActive(true);
            if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();
        }
        else
        {
            if (temp_CardIsTargeted)
            {
                if (_currentCard.activeSelf) _currentCard.SetActive(false);
                if (TargetingReticle.Instance != null)
                    TargetingReticle.Instance.StartTargeting(targetingSourceTransform);
            }
            else
            {
                if (!_currentCard.activeSelf) _currentCard.SetActive(true);
                if (TargetingReticle.Instance != null) TargetingReticle.Instance.StopTargeting();
            }
        }
    }

    private void EndDrag()
    {
        bool inHandZone = IsMouseInHandZone();
        bool requestSent = false;

        if (!inHandZone)
        {
            // 필드 슬롯 감지
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, fieldSlotLayer))
            {
                FieldSlot slot = hit.collider.GetComponent<FieldSlot>();
                if (slot != null)
                {
                    // [수정됨] 직접 패킷을 만들지 않고 GameClient 함수 호출
                    SendPlayRequestToClient(_currentCard, slot.slotIndex);
                    requestSent = true;
                }
            }
        }

        if (requestSent)
        {
            // 서버 응답 대기 (카드 숨김)
            handManager.SetDraggedCard(null);
            _currentCard.SetActive(false);
        }
        else
        {
            // 취소 (손패 복귀)
            if (!_currentCard.activeSelf) _currentCard.SetActive(true);
            handManager.SetDraggedCard(null);
            handManager.AlignHand();
        }

        _currentCard = null;
        _isDragging = false;
    }

    /// <summary>
    /// (수정) 카드를 숨기고, 하수인 모델을 생성하여 소환 애니메이션을 재생합니다.
    /// 생성된 하수인에게 카드 데이터를 주입합니다.
    /// </summary>
    private IEnumerator PlaySpawnSequence(GameObject cardObj, Vector3 slotPos, FieldSlot slot)
    {
        _isSpawning = true;

        // 1. 카드 데이터 가져오기 (이 데이터를 하수인에게 넘겨줄 것임)
        GameCardDisplay handCardDisplay = cardObj.GetComponent<GameCardDisplay>();
        SpawnEffectData effectData = null;
        // if (handCardDisplay != null && handCardDisplay.Data != null) effectData = handCardDisplay.Data.spawnEffectData;

        SpawnMotionType motionType = (effectData != null) ? effectData.motionType : SpawnMotionType.Normal;
        float duration = (effectData != null) ? effectData.duration : 0.5f;

        // 2. 카드 정리
        handManager.SetDraggedCard(null);
        handManager.RemoveCardFromHandListOnly(cardObj);
        handManager.AlignHand();
        cardObj.SetActive(false);

        // 3. 하수인 모델 생성
        GameObject minionObj = null;
        if (testMinionPrefab != null)
        {
            // 중앙 정렬을 위해 slot.transform.position 사용
            minionObj = Instantiate(testMinionPrefab, slot.transform.position, slot.transform.rotation);

            // --- (신규) 데이터 주입 ---
            // 하수인 프리팹에도 GameCardDisplay(또는 MinionDisplay)가 붙어있어야 합니다.
            GameCardDisplay minionDisplay = minionObj.GetComponent<GameCardDisplay>();
            if (minionDisplay != null && handCardDisplay != null)
            {
                // 손패 카드의 데이터를 그대로 하수인에게 전달
                // (주의: _cardInfo는 나중에 서버에서 받은 '필드 엔티티 정보'로 업데이트될 수 있음)
                minionDisplay.Setup(handCardDisplay._cardData, handCardDisplay._cardInfo);
            }
            else
            {
                Debug.LogWarning("[CardDrag] 하수인 프리팹이나 손패 카드에 GameCardDisplay가 없습니다.");
            }
            // ------------------------
        }
        else
        {
            Debug.LogError("CardDragManager에 'Test Minion Prefab'이 연결되지 않았습니다!");
            cardObj.SetActive(true);
            Destroy(cardObj);
            _isSpawning = false;
            yield break;
        }

        // 4. 하수인 모델 애니메이션 (DOTween)
        Sequence seq = DOTween.Sequence();

        switch (motionType)
        {
            case SpawnMotionType.SkyDrop:
                minionObj.transform.position = slot.transform.position + Vector3.up * 10f;
                seq.Append(minionObj.transform.DOMove(slot.transform.position, 0.4f).SetEase(Ease.OutBounce));
                if (effectData != null && effectData.cameraShakeStrength > 0)
                    seq.AppendCallback(() => mainCamera.DOShakePosition(0.2f, effectData.cameraShakeStrength));
                break;

            case SpawnMotionType.PopUp:
                minionObj.transform.position = slot.transform.position + Vector3.down * 3f;
                seq.Append(minionObj.transform.DOMove(slot.transform.position, duration).SetEase(Ease.OutBack));
                break;

            case SpawnMotionType.Normal:
            default:
                minionObj.transform.localScale = Vector3.zero;
                seq.Append(minionObj.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));
                break;
        }

        if (effectData != null && effectData.spawnVFXPrefab != null)
        {
            seq.InsertCallback(effectData.vfxDelay, () =>
            {
                Instantiate(effectData.spawnVFXPrefab, slot.transform.position + effectData.vfxOffset, Quaternion.identity);
            });
        }

        // 5. 종료
        yield return seq.WaitForCompletion();

        Debug.Log("소환 연출 종료. (하수인은 필드에 남음)");

        Destroy(cardObj);
        _isSpawning = false;
    }

    private void SendPlayRequestToClient(GameObject cardObj, int slotIndex)
    {
        GameCardDisplay cardDisplay = cardObj.GetComponent<GameCardDisplay>();
        if (cardDisplay != null && GameClient.Instance != null)
        {
            // [핵심] GameClient에게 "이거 처리해줘"라고 위임
            GameClient.Instance.SendPlayCardRequest(cardDisplay.InstanceId, slotIndex);
        }
    }

    private bool IsMouseInHandZone()
    {
        return (Input.mousePosition.y / Screen.height) <= handZoneHeightRatio;
    }
}