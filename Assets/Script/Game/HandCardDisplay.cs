using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AYellowpaper.SerializedCollections;

/// <summary>
/// 인게임 배틀에서 유저의 [손패(Hand Card)] UI 표시를 전담하는 컴포넌트입니다.
/// 2D Canvas 기반의 텍스트, GIF 애니메이션, 레어도 테두리를 관리합니다.
/// </summary>
public class HandCardDisplay : GameCardDisplay
{
    [Header("손패 UI 요소")]
    public SpriteGifPlayer cardArtAnimator;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    [Header("손패 스탯 UI")]
    public TextMeshProUGUI costText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI healthText;

    [Header("레어도별 카드 테두리 (Border)")]
    [Tooltip("카드 테두리 Image 컴포넌트")]
    public Image borderImage;

    [Tooltip("레어도별 테두리 스프라이트 매핑 (Common, Rare, Epic, Legendary)")]
    [SerializedDictionary("레어도", "테두리 스프라이트")]
    public SerializedDictionary<CardRarity, Sprite> rarityBorders = new SerializedDictionary<CardRarity, Sprite>();

    /// <summary>
    /// 손패 카드 데이터를 받아 UI 및 테두리를 갱신합니다.
    /// </summary>
    public override void Setup(CardData data, CardInfo info)
    {
        base.Setup(data, info);

        if (_cardData == null) return;

        // 1. 일러스트(GIF 애니메이션) 설정
        if (cardArtAnimator != null)
        {
            if (_cardData.animationFrames != null && _cardData.animationFrames.Length > 0)
            {
                cardArtAnimator.SetGif(_cardData.animationFrames);
            }
            else if (_cardData.thumbnail != null)
            {
                cardArtAnimator.SetGif(new Sprite[] { _cardData.thumbnail });
            }
        }

        // 2. 텍스트 정보 설정
        if (nameText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(nameText, minSize: 11f, maxSize: 24f, wordWrap: false, overflowMode: TextOverflowModes.Ellipsis);
            nameText.text = _cardData.cardName;
        }
        if (descriptionText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(descriptionText, minSize: 9f, maxSize: 22f, wordWrap: true, overflowMode: TextOverflowModes.Ellipsis);
            CardTextFormatter.FormatAndBind(descriptionText, _cardData.description, _cardData, info);
        }

        // 3. 레어도별 테두리 적용
        SetRarityBorder(_cardData.rarity);

        // 4. 스탯 설정 (서버 정보가 있으면 반영, 없으면 기본 데이터)
        int cost = (info != null) ? info.currentCost : _cardData.manaCost;
        int atk = (info != null) ? info.currentAttack : _cardData.attack;
        int hp = (info != null) ? info.currentHealth : _cardData.health;

        if (costText != null) SetStatText(costText, cost, _cardData.manaCost, isCost: true);

        bool isMinion = _cardData.cardType == CardType.하수인;
        bool isMember = _cardData.cardType == CardType.멤버;

        if (attackText != null)
        {
            if (isMinion) SetStatText(attackText, atk, _cardData.attack);
            else attackText.text = "";
        }

        if (healthText != null)
        {
            if (isMinion || isMember) SetStatText(healthText, hp, _cardData.health);
            else healthText.text = "";
        }
    }

    /// <summary>
    /// 서버로부터 손패 카드의 스탯 변화 패킷을 받았을 때 갱신합니다.
    /// </summary>
    public override void UpdateCardStats(CardInfo info)
    {
        base.UpdateCardStats(info);

        if (info == null || _cardData == null) return;

        if (costText != null)
        {
            SetStatText(costText, info.currentCost, _cardData.manaCost, isCost: true);
        }

        bool isMinion = _cardData.cardType == CardType.하수인;
        bool isMember = _cardData.cardType == CardType.멤버;

        if (isMinion)
        {
            if (attackText != null) SetStatText(attackText, info.currentAttack, _cardData.attack);
            if (healthText != null) SetStatText(healthText, info.currentHealth, _cardData.health);
        }
        else if (isMember)
        {
            if (attackText != null) attackText.text = "";
            if (healthText != null) SetStatText(healthText, info.currentHealth, _cardData.health);
        }

        // 효과 텍스트 내 동적 수치(피해량, 누적 스택, 버프 등) 갱신
        if (descriptionText != null)
        {
            CardTextFormatter.FormatAndBind(descriptionText, _cardData.description, _cardData, info);
        }
    }

    /// <summary>
    /// 카드의 레어도에 맞춰 테두리 스프라이트를 교체합니다.
    /// </summary>
    public void SetRarityBorder(CardRarity rarity)
    {
        if (borderImage == null) return;

        if (rarityBorders != null && rarityBorders.TryGetValue(rarity, out Sprite borderSprite) && borderSprite != null)
        {
            borderImage.sprite = borderSprite;
            borderImage.gameObject.SetActive(true);
        }
    }
}
