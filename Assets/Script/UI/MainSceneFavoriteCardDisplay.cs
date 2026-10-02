using UnityEngine;
using TMPro;

/// <summary>
/// 메인 씬의 좌측에 유저가 지정한 대표(좋아하는) 카드를 연출/표시하는 컴포넌트입니다.
/// </summary>
public class MainSceneFavoriteCardDisplay : MonoBehaviour
{
    [Header("1. 카드 프리팹 및 앵커")]
    [Tooltip("카드 프리팹 (Assets/Prefab/Deck/Card.prefab)")]
    public GameObject cardPrefab;

    [Tooltip("카드가 생성되어 배치될 부모 Transform")]
    public Transform cardAnchor;

    [Tooltip("카드 표시 스케일")]
    public float cardScale = 1.6f;

    [Header("2. UI 텍스트 (선택 사항)")]
    [Tooltip("대표 카드 타이틀/배너 텍스트")]
    public TextMeshProUGUI headerBadgeText;

    [Tooltip("카드 이름 텍스트 (하단 전용 텍스트 필요 시)")]
    public TextMeshProUGUI cardNameText;

    [Header("3. 유휴 애니메이션")]
    [Tooltip("부드러운 상하 부유 애니메이션 활성화")]
    public bool enableFloatingAnimation = true;
    public float floatAmplitude = 8f;
    public float floatSpeed = 1.5f;

    private GameObject spawnedCardObject;
    private DeckCardDisplay cachedCardDisplay;
    private Vector3 initialAnchorLocalPos;

    private void Awake()
    {
        if (cardAnchor != null)
        {
            initialAnchorLocalPos = cardAnchor.localPosition;
        }
    }

    private void OnEnable()
    {
        FavoriteCardManager.OnFavoriteCardChanged += OnFavoriteChanged;
        RefreshDisplay();
    }

    private void OnDisable()
    {
        FavoriteCardManager.OnFavoriteCardChanged -= OnFavoriteChanged;
    }

    private void Update()
    {
        if (enableFloatingAnimation && cardAnchor != null)
        {
            float offset = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            cardAnchor.localPosition = initialAnchorLocalPos + new Vector3(0f, offset, 0f);
        }
    }

    private void OnFavoriteChanged(string newCardId)
    {
        RefreshDisplay();
    }

    /// <summary>
    /// 저장된 대표 카드를 읽어와 좌측 카드 뷰에 반영합니다.
    /// </summary>
    public void RefreshDisplay()
    {
        CardData favCard = FavoriteCardManager.GetFavoriteCard();
        if (favCard == null)
        {
            Debug.LogWarning("[MainSceneFavoriteCardDisplay] 표시할 카드를 찾을 수 없습니다.");
            return;
        }

        // 1. 카드 프리팹 인스턴스 생성 (최초 1회)
        if (spawnedCardObject == null)
        {
            if (cardPrefab != null && cardAnchor != null)
            {
                spawnedCardObject = Instantiate(cardPrefab, cardAnchor);
                spawnedCardObject.transform.localPosition = Vector3.zero;
                spawnedCardObject.transform.localRotation = Quaternion.identity;
                spawnedCardObject.transform.localScale = Vector3.one * cardScale;

                cachedCardDisplay = spawnedCardObject.GetComponent<DeckCardDisplay>();

                // 메인 씬에서는 카드 클릭 인터랙션(덱 추가/상세 팝업 등) 제거
                CardInteraction interaction = spawnedCardObject.GetComponent<CardInteraction>();
                if (interaction != null)
                {
                    Destroy(interaction);
                }
            }
        }

        // 2. 카드 정보 바인딩
        if (spawnedCardObject != null)
        {
            if (cachedCardDisplay == null)
            {
                cachedCardDisplay = spawnedCardObject.GetComponent<DeckCardDisplay>();
            }

            if (cachedCardDisplay != null)
            {
                cachedCardDisplay.Setup(favCard, count: -1);
                cachedCardDisplay.HideCount();

                if (cachedCardDisplay.countText != null)
                {
                    cachedCardDisplay.countText.gameObject.SetActive(false);
                }

                // 어두운 오버레이 제거하여 밝고 선명하게 노출
                if (cachedCardDisplay.disabledOverlayPanel != null)
                {
                    cachedCardDisplay.disabledOverlayPanel.SetActive(false);
                }
            }

            if (cardNameText != null)
            {
                cardNameText.text = favCard.cardName;
            }

            if (headerBadgeText != null)
            {
                headerBadgeText.text = "★ FAVORITE CARD ★";
            }
        }

        Debug.Log($"[MainSceneFavoriteCardDisplay] 메인 씬 대표 카드 표시 완료: '{favCard.cardName}' ({favCard.cardID})");
    }
}
