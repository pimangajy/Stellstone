using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배틀 씬의 Canvas/SidePannel에 위치하여
/// 게임 시작 시 서버로부터 전달받은 사이드덱(mySideDeck) 카드들을 최대 5개 생성하고,
/// 카드의 마우스 인터랙션 및 서버 획득 요청을 총괄 관리하는 매니저입니다.
/// </summary>
public class SideDeckUIManager : MonoBehaviour
{
    public static SideDeckUIManager Instance { get; private set; }

    [Header("UI 및 컨테이너 연결")]
    [Tooltip("카드가 정렬되어 생성될 부모 Transform (SidePannel/Scroll View/Viewport/Content)")]
    [SerializeField] private Transform cardContainer;

    [Tooltip("사이드 카드로 생성할 HandCard 프리팹 (Assets/Prefab/HandCard.prefab)")]
    [SerializeField] private GameObject handCardPrefab;

    [Header("패널 토글 연동 (선택 사항)")]
    [Tooltip("SidePannel의 최상위 루트 오브젝트")]
    [SerializeField] private GameObject sidePanelRoot;

    [Header("카드 크기 및 레이아웃 조절")]
    [Tooltip("사이드덱 카드들의 스케일 배율 (1.0 = 100%, 0.8 = 80% 등)")]
    [Range(0.2f, 2.0f)]
    [SerializeField] private float cardScale = 1.0f;

    [Tooltip("카드 프리팹의 RectTransform 크기(sizeDelta)를 강제 지정할 때 사용 (0이면 프리팹 원본 유지)")]
    [SerializeField] private Vector2 customCardSize = Vector2.zero;

    [Tooltip("컨테이너 레이아웃 그룹의 카드 간격(spacing)")]
    [SerializeField] private float cardSpacing = 20f;

    // 현재 생성되어 있는 사이드 카드 목록
    private readonly List<SideDeckCardItem> _spawnedCardItems = new List<SideDeckCardItem>();
    private bool _isRequestPending = false;

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

        if (sidePanelRoot == null)
        {
            sidePanelRoot = this.gameObject;
        }

        // 컨테이너가 인스펙터에 미지정된 경우 ScrollRect의 Content 또는 "Content"라는 이름의 자식 Transform 자동 탐색
        if (cardContainer == null)
        {
            ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);
            if (scrollRect != null && scrollRect.content != null)
            {
                cardContainer = scrollRect.content;
            }
            else
            {
                // Content 이름으로 탐색
                Transform found = transform.Find("Scroll View/Viewport/Content");
                if (found != null) cardContainer = found;
            }
        }

        // 프리팹이 인스펙터에 미지정된 경우 에디터/Resources fallback 탐색
        if (handCardPrefab == null)
        {
#if UNITY_EDITOR
            handCardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/HandCard.prefab");
#endif
            if (handCardPrefab == null)
            {
                handCardPrefab = Resources.Load<GameObject>("Prefab/HandCard");
            }
        }
    }

    private void Start()
    {
        // 네트워크 이벤트 구독
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnGameReadyEvent += HandleGameReady;
            GameClient.Instance.OnGetCardFromSideDeckSuccessEvent += HandleGetCardSuccess;
            GameClient.Instance.OnGetCardFromSideDeckFailEvent += HandleGetCardFail;
        }
    }

    private void OnDestroy()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnGameReadyEvent -= HandleGameReady;
            GameClient.Instance.OnGetCardFromSideDeckSuccessEvent -= HandleGetCardSuccess;
            GameClient.Instance.OnGetCardFromSideDeckFailEvent -= HandleGetCardFail;
        }
    }

    /// <summary>
    /// 게임 시작(S_GameReady) 시 내 사이드덱 카드 목록(mySideDeck)을 받아 UI 카드를 생성합니다.
    /// </summary>
    private void HandleGameReady(S_GameReady readyInfo)
    {
        if (readyInfo == null || readyInfo.mySideDeck == null) return;

        Debug.Log($"[SideDeckUIManager] 📥 사이드덱 카드 {readyInfo.mySideDeck.Count}장 수신");
        SetupSideDeckCards(readyInfo.mySideDeck);
    }

    /// <summary>
    /// 사이드덱 카드를 패널에 생성하고 바인딩합니다. (최대 5장)
    /// </summary>
    public void SetupSideDeckCards(List<CardInfo> sideDeckCards)
    {
        ClearCards();

        if (cardContainer == null)
        {
            Debug.LogError("[SideDeckUIManager] ❌ cardContainer가 설정되지 않았습니다.");
            return;
        }

        int count = Mathf.Min(5, sideDeckCards.Count);
        for (int i = 0; i < count; i++)
        {
            CardInfo info = sideDeckCards[i];
            CardData cardData = null;

            if (ResourceManager.Instance != null)
            {
                cardData = ResourceManager.Instance.GetCardData(info.cardId);
            }

            GameObject cardObj = null;
            if (handCardPrefab != null)
            {
                cardObj = Instantiate(handCardPrefab, cardContainer);
            }
            else
            {
                Debug.LogWarning("[SideDeckUIManager] ⚠️ handCardPrefab이 연결되지 않았습니다.");
                break;
            }

            // 손패 디스플레이 셋업
            HandCardDisplay display = cardObj.GetComponent<HandCardDisplay>();
            if (display != null && cardData != null)
            {
                display.Setup(cardData, info);
            }

            // 호버 및 클릭 인터랙션 컴포넌트 연결
            SideDeckCardItem item = cardObj.GetComponent<SideDeckCardItem>();
            if (item == null)
            {
                item = cardObj.AddComponent<SideDeckCardItem>();
            }
            item.Init(cardData, info, OnSideCardClicked);

            // 카드 크기 및 스케일 적용
            ApplyLayoutToCard(cardObj);

            _spawnedCardItems.Add(item);
        }

        // 컨테이너 레이아웃(간격 등) 일괄 갱신
        ApplyCardLayout();

        Debug.Log($"[SideDeckUIManager] ✅ 사이드덱 카드 {count}장 생성 완료 (스케일: {cardScale})");
    }

    /// <summary>
    /// 사이드 카드를 클릭했을 때 호출됩니다.
    /// </summary>
    private void OnSideCardClicked(SideDeckCardItem item)
    {
        if (item == null || item.CardInfo == null) return;
        if (_isRequestPending)
        {
            Debug.Log("[SideDeckUIManager] ⏳ 이전 요청을 처리 중입니다.");
            return;
        }

        // 1. 내 턴 여부 검사
        if (BattleManager.Instance != null && !BattleManager.Instance.IsMyTurn)
        {
            Debug.LogWarning("[SideDeckUIManager] ❌ 자신의 턴에만 사이드덱에서 카드를 가져올 수 있습니다.");
            return;
        }

        // 2. 마나 검사
        if (BattleManager.Instance != null && BattleManager.Instance.MyCurrentMana < item.CardInfo.currentCost)
        {
            Debug.LogWarning($"[SideDeckUIManager] ❌ 마나가 부족합니다. (필요: {item.CardInfo.currentCost}, 현재: {BattleManager.Instance.MyCurrentMana})");
            return;
        }

        // 3. 서버에 가져오기 요청 전송
        _isRequestPending = true;
        Debug.Log($"[SideDeckUIManager] 📤 사이드 카드 가져오기 요청: {item.CardInfo.cardName} (ID: {item.CardInfo.instanceId})");
        GameClient.Instance?.SendGetCardFromSideDeck(item.CardInfo.instanceId);
    }

    /// <summary>
    /// 사이드 카드 획득 성공 응답 수신 시 처리
    /// </summary>
    private void HandleGetCardSuccess(S_GetCardFromSideDeckSuccess res)
    {
        _isRequestPending = false;
        if (res == null) return;

        bool isMine = (GameClient.Instance != null && res.playerUid == GameClient.Instance.UserUid);
        if (!isMine) return;

        // 1. 가져온 카드 UI를 사이드 패널에서 제거
        SideDeckCardItem targetItem = _spawnedCardItems.FirstOrDefault(i =>
            i != null && i.CardInfo != null &&
            (i.CardInfo.instanceId == res.card?.instanceId || i.CardInfo.cardId == res.card?.cardId));

        if (targetItem != null)
        {
            _spawnedCardItems.Remove(targetItem);
            Destroy(targetItem.gameObject);
        }

        // 2. 미리보기 창 및 마나 하이라이트 닫기
        DeckCardPreviewManager.Instance?.HidePreview();
        BattleManager.Instance?.ResetHighlights();

        // 3. 카드 생성 애니메이션(화면 중앙 등장 -> 손패 이동) 연출 실행
        if (res.card != null && CardDrawManager.Instance != null)
        {
            CardDrawManager.Instance.PerformGenerateAnimation(res.card);
        }

        Debug.Log($"[SideDeckUIManager] 🎉 사이드 카드 획득 완료! 남은 사이드 카드: {_spawnedCardItems.Count}장");
    }

    /// <summary>
    /// 사이드 카드 획득 실패 응답 수신 시 처리
    /// </summary>
    private void HandleGetCardFail(S_GetCardFromSideDeckFail fail)
    {
        _isRequestPending = false;
        Debug.LogWarning($"[SideDeckUIManager] ❌ 사이드 카드 가져오기 실패: {fail?.reason}");
    }

    /// <summary>
    /// 기존 카드 UI 오브젝트들을 모두 정리합니다.
    /// </summary>
    private void ClearCards()
    {
        foreach (var item in _spawnedCardItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        _spawnedCardItems.Clear();

        if (cardContainer != null)
        {
            // 컨테이너 안의 기존 더미 자식 오브젝트들도 함께 정리
            foreach (Transform child in cardContainer)
            {
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// 인스펙터에 설정된 크기(cardScale, customCardSize) 및 간격(cardSpacing)을 생성된 카드와 컨테이너에 적용합니다.
    /// </summary>
    public void ApplyCardLayout()
    {
        if (cardContainer != null)
        {
            var hlg = cardContainer.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.spacing = cardSpacing;
            }
            var vlg = cardContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.spacing = cardSpacing;
            }
            var glg = cardContainer.GetComponent<GridLayoutGroup>();
            if (glg != null)
            {
                glg.spacing = new Vector2(cardSpacing, cardSpacing);
            }
        }

        foreach (var item in _spawnedCardItems)
        {
            if (item == null) continue;
            ApplyLayoutToCard(item.gameObject);
        }
    }

    /// <summary>
    /// 개별 카드 게임오브젝트에 스케일 및 크기를 적용합니다.
    /// </summary>
    private void ApplyLayoutToCard(GameObject cardObj)
    {
        if (cardObj == null) return;

        // 1. 스케일 적용
        cardObj.transform.localScale = Vector3.one * cardScale;

        // 2. 커스텀 크기(sizeDelta) 지정 시 적용
        if (customCardSize != Vector2.zero)
        {
            RectTransform rt = cardObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = customCardSize;
            }
        }
    }

    /// <summary>
    /// 사이드덱 카드의 크기(배율)를 런타임에 동적으로 변경합니다.
    /// </summary>
    public void SetCardScale(float scale)
    {
        cardScale = Mathf.Clamp(scale, 0.2f, 2.0f);
        ApplyCardLayout();
    }

    /// <summary>
    /// 사이드덱 카드의 간격을 런타임에 동적으로 변경합니다.
    /// </summary>
    public void SetCardSpacing(float spacing)
    {
        cardSpacing = spacing;
        ApplyCardLayout();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && _spawnedCardItems != null && _spawnedCardItems.Count > 0)
        {
            ApplyCardLayout();
        }
        else if (cardContainer != null)
        {
            var hlg = cardContainer.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) hlg.spacing = cardSpacing;
        }
    }
#endif
}
