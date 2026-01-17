using UnityEngine;
using TMPro;

public class GameCardDisplay : MonoBehaviour
{
    [Header("UI 연결")]
    // SpriteRenderer 대신 애니메이션 재생기를 연결합니다.
    public SpriteGifPlayer cardArtAnimator;
    public TextMeshPro nameText;
    public TextMeshPro descriptionText;

    [Header("스탯 UI")]
    public TextMeshPro costText;
    public TextMeshPro attackText;
    public TextMeshPro healthText;

    [Header("데이터")]
    [SerializeField] public CardData _cardData;
    [SerializeField] public CardInfo _cardInfo;

    // (신규) 필드에 소환된 후 서버가 부여한 고유 ID를 저장합니다.
    public int EntityId { get; private set; }

    // 카드 개별 아이디 (손패에 있을 때 사용)
    public string InstanceId => _cardInfo?.instanceId;

    /// <summary>
    /// 카드 데이터를 초기화하고 UI를 갱신합니다. (주로 손패/드로우 시 사용)
    /// </summary>
    /// <param name="data">ScriptableObject 원본 데이터</param>
    /// <param name="info">서버에서 온 현재 상태 데이터</param>
    public void Setup(CardData data, CardInfo info)
    {
        _cardData = data;
        _cardInfo = info;

        if (_cardData == null) return;

        // ---------------------------------------------------------
        // 1. 리소스 (이미지) 설정
        // ---------------------------------------------------------
        if (cardArtAnimator != null)
        {
            // CardData에 있는 스프라이트 배열을 애니메이터에게 전달
            if (_cardData.animationFrames != null && _cardData.animationFrames.Length > 0)
            {
                // 애니메이터에 이미지 리스트 주입 (자동으로 재생됨)
                cardArtAnimator.SetGif(_cardData.animationFrames);
            }
            else
            {
                // 이미지가 없는 경우 처리 (빈칸 등)
                Debug.LogWarning($"카드 ID {_cardData.cardID}에 이미지가 없습니다.");
            }
        }

        // ---------------------------------------------------------
        // 2. 텍스트 및 스탯 설정
        // ---------------------------------------------------------
        if (nameText != null) nameText.text = _cardData.cardName;
        if (descriptionText != null) descriptionText.text = _cardData.description;

        // 서버 정보(info)가 있으면 그것을 쓰고, 없으면 기본 데이터(data) 사용
        int cost = (info != null) ? info.currentCost : _cardData.manaCost;
        int atk = (info != null) ? info.currentAttack : _cardData.attack;
        int hp = (info != null) ? info.currentHealth : _cardData.health;

        if (costText != null) costText.text = cost.ToString();

        // 하수인일 때만 공/체 표시, 주문이면 숨김
        bool isMinion = _cardData.cardType == CardType.하수인;

        if (attackText != null)
        {
            attackText.text = isMinion ? atk.ToString() : "";
            // 공격력 아이콘 배경도 있다면 여기서 같이 끄고 켜주면 좋습니다.
            // attackIconObject.SetActive(isMinion); 
        }

        if (healthText != null)
        {
            healthText.text = isMinion ? hp.ToString() : "";
        }
    }

    /// <summary>
    /// (신규) 필드에 소환된 개체(Entity) 정보를 설정합니다.
    /// </summary>
    public void SetupEntity(EntityData entityData, CardData cardData)
    {
        // 1. 서버가 준 고유 ID 저장 (가장 중요!)
        this.EntityId = entityData.entityId;

        // 2. 카드 데이터 연결 (이미지, 이름 등 표시용)
        _cardData = cardData;

        // 3. UI 설정 (Setup 재활용 가능 - CardInfo를 임시로 만들거나 UI 부분만 추출)
        // 여기서는 간단히 리소스와 텍스트 설정만 다시 수행
        if (cardArtAnimator != null && _cardData.animationFrames != null && _cardData.animationFrames.Length > 0)
        {
            cardArtAnimator.SetGif(_cardData.animationFrames);
        }
        if (nameText != null) nameText.text = _cardData.cardName;
        if (descriptionText != null) descriptionText.text = _cardData.description;

        // 4. 스탯 갱신
        UpdateEntityStats(entityData);
    }

    public void UpdateEntityStats(EntityData entityData)
    {
        // 필드 개체는 cost가 표시되지 않는 경우가 많으므로 생략하거나 필요시 추가
        if (attackText != null) attackText.text = entityData.attack.ToString();
        if (healthText != null) healthText.text = entityData.health.ToString();

        // (선택) 공격 가능 여부 표시 (초록색 테두리 등)
        // if (entityData.canAttack) ShowGreenGlow();
    }
}