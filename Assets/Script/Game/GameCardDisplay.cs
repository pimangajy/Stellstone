using UnityEngine;
using TMPro;

/// <summary>
/// 인게임(필드, 손패)에서 카드의 외형(이미지, 텍스트, 스탯)을 표시하는 스크립트입니다.
/// </summary>
public class GameCardDisplay : MonoBehaviour
{
    [Header("UI 연결")]
    // 정지 이미지 대신, 움직이는 GIF를 재생해주는 플레이어 스크립트
    public SpriteGifPlayer cardArtAnimator;
    public TextMeshPro nameText;
    public TextMeshPro descriptionText;

    [Header("스탯 UI")]
    public TextMeshPro costText;
    public TextMeshPro attackText;
    public TextMeshPro healthText;

    [Header("데이터")]
    public CardData _cardData; // 원본 데이터 (변하지 않는 정보)
    public CardInfo _cardInfo; // 게임 중 데이터 (변하는 정보)

    // 필드에 소환된 개체일 때 부여되는 ID
    public int EntityId { get; private set; }

    // 손패에 있을 때 부여되는 ID
    public string InstanceId => _cardInfo?.instanceId;

    /// <summary>
    /// [손패용] 카드 데이터를 받아서 화면에 표시합니다.
    /// </summary>
    public void Setup(CardData data, CardInfo info)
    {
        _cardData = data;
        _cardInfo = info;

        if (_cardData == null) return;

        // 1. 이미지(GIF) 설정
        if (cardArtAnimator != null && _cardData.animationFrames != null && _cardData.animationFrames.Length > 0)
        {
            cardArtAnimator.SetGif(_cardData.animationFrames);
        }

        // 2. 텍스트 설정
        if (nameText != null) nameText.text = _cardData.cardName;
        if (descriptionText != null) descriptionText.text = _cardData.description;

        // 3. 스탯(비용, 공격력, 체력) 설정
        // 서버 정보(info)가 있으면 그걸 쓰고, 없으면 기본값(_cardData) 사용
        int cost = (info != null) ? info.currentCost : _cardData.manaCost;
        int atk = (info != null) ? info.currentAttack : _cardData.attack;
        int hp = (info != null) ? info.currentHealth : _cardData.health;

        if (costText != null) costText.text = cost.ToString();

        // 하수인만 공/체 표시
        bool isMinion = _cardData.cardType == CardType.하수인;
        if (attackText != null) attackText.text = isMinion ? atk.ToString() : "";
        if (healthText != null) healthText.text = isMinion ? hp.ToString() : "";
    }

    /// <summary>
    /// [필드용] 소환된 개체의 정보를 설정합니다.
    /// </summary>
    public void SetupEntity(EntityData entityData, CardData cardData)
    {
        this.EntityId = entityData.entityId; // 서버 ID 저장
        _cardData = cardData;

        // 이미지 및 텍스트 설정 (재사용)
        if (cardArtAnimator != null && _cardData.animationFrames != null)
            cardArtAnimator.SetGif(_cardData.animationFrames);

        if (nameText != null) nameText.text = _cardData.cardName;
        if (descriptionText != null) descriptionText.text = _cardData.description;

        // 스탯 갱신
        UpdateEntityStats(entityData);
    }

    public void UpdateEntityStats(EntityData entityData)
    {
        if (attackText != null) attackText.text = entityData.attack.ToString();
        if (healthText != null) healthText.text = entityData.health.ToString();
    }
}