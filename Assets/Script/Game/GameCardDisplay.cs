using UnityEngine;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;
using System.Collections; // 애니메이션

/// <summary>
/// 인게임(필드, 손패)에서 카드의 외형을 표시합니다.
/// [수정됨]
/// - 공격자/대상 연출 (Floating, Glow)
/// - 스탯 변화에 따른 텍스트 색상 변경 (버프: 초록, 너프: 빨강)
/// </summary>
public class GameCardDisplay : MonoBehaviour
{
    [Header("UI 연결")]
    public SpriteGifPlayer cardArtAnimator;
    public TextMeshPro nameText;
    public TextMeshPro descriptionText;

    [Header("스탯 UI")]
    public TextMeshPro costText;
    public TextMeshPro attackText;
    public TextMeshPro healthText;

    [Header("색상 설정")]
    public Color normalColor = Color.white;
    public Color buffColor = Color.green;   // 스탯이 높아졌을 때 (초록색)
    public Color debuffColor = Color.red;   // 스탯이 낮아졌을 때 (빨간색)

    [Header("데이터")]
    public CardData _cardData;
    public CardInfo _cardInfo;
    public EntityData CurrentEntityData { get; private set; }
    public int EntityId { get; private set; }
    public string InstanceId => _cardInfo?.instanceId;

    // --- [연출 설정] ---
    [Header("연출 - 공격자 (Floating)")]
    [Tooltip("공격 시도 시 떠오를 높이")]
    public float floatHeight = 0.5f;
    public float floatDuration = 0.2f;

    [Header("연출 - 대상 (Glow)")]
    [Tooltip("조준당할 때 켜질 하이라이트 오브젝트 (테두리 이미지 등)")]
    public GameObject glowEffectObject;

    [Header("피격 모션")]
    public GameObject damageIMG;

    private Vector3 _basePosition; // 원래 위치
    private bool _isFloating = false; // 현재 떠있는 상태인가?

    private void Awake()
    {
        _basePosition = transform.localPosition;

        // 시작 시 발광 효과는 꺼둡니다.
        if (glowEffectObject != null) glowEffectObject.SetActive(false);
    }

    /// <summary>
    /// [손패용] 카드 데이터를 받아서 화면에 표시합니다.
    /// </summary>
    public void Setup(CardData data, CardInfo info)
    {
        _cardData = data;
        _cardInfo = info;

        if (_cardData == null) return;

        // 1. 이미지 및 텍스트 설정
        if (cardArtAnimator != null && _cardData.animationFrames != null)
            cardArtAnimator.SetGif(_cardData.animationFrames);

        if (nameText != null) nameText.text = _cardData.cardName;
        if (descriptionText != null) descriptionText.text = _cardData.description;

        // 2. 스탯 설정 (서버 정보가 있으면 반영, 없으면 기본값)
        int cost = (info != null) ? info.currentCost : _cardData.manaCost;
        int atk = (info != null) ? info.currentAttack : _cardData.attack;
        int hp = (info != null) ? info.currentHealth : _cardData.health;

        if (costText != null) costText.text = cost.ToString();

        // 하수인만 공/체 표시 및 색상 적용
        bool isMinion = _cardData.cardType == CardType.하수인;

        if (attackText != null)
        {
            if (isMinion) SetStatText(attackText, atk, _cardData.attack);
            else attackText.text = "";
        }

        if (healthText != null)
        {
            if (isMinion) SetStatText(healthText, hp, _cardData.health);
            else healthText.text = "";
        }
    }

    /// <summary>
    /// [필드용] 소환된 개체의 정보를 설정합니다.
    /// </summary>
    public void SetupEntity(EntityData entityData, CardData cardData)
    {
        this.EntityId = entityData.entityId;
        this.CurrentEntityData = entityData;
        _cardData = cardData;
        _basePosition = transform.localPosition;

        if (cardArtAnimator != null && _cardData.animationFrames != null)
            cardArtAnimator.SetGif(_cardData.animationFrames);

        if (nameText != null) nameText.text = _cardData.cardName;
        if (descriptionText != null) descriptionText.text = _cardData.description;

        UpdateEntityStats(entityData);
    }

    /// <summary>
    /// 서버에서 온 최신 상태로 스탯 UI를 갱신합니다.
    /// </summary>
    public void UpdateEntityStats(EntityData entityData)
    {
        this.CurrentEntityData = entityData;

        // 비용은 필드에서 보통 표시 안 하지만, 필요하다면 업데이트
        // if (costText != null) costText.text = entityData.cost.ToString();

        // 공격력과 체력을 갱신하면서 색상도 같이 계산합니다.
        if (attackText != null)
        {
            // 현재 공격력 vs 원래 공격력 비교
            SetStatText(attackText, entityData.attack, _cardData.attack);
        }

        if (healthText != null)
        {
            // 현재 체력 vs 원래 체력 비교
            // (주의: 하스스톤 로직상 '피해를 입은 상태'는 빨강, '최대 체력이 늘어난 상태'는 초록입니다.
            // 여기서는 요청하신 대로 '현재 값 vs 원본 최대값' 기준으로 단순 비교합니다)
            SetStatText(healthText, entityData.health, _cardData.health);
        }
    }

    /// <summary>
    /// [내부 함수] 값에 따라 텍스트 내용과 색상을 변경합니다.
    /// </summary>
    private void SetStatText(TextMeshPro textComp, int currentVal, int originalVal)
    {
        textComp.text = currentVal.ToString();

        if (currentVal > originalVal)
        {
            textComp.color = buffColor; // 버프 (초록)
        }
        else if (currentVal < originalVal)
        {
            textComp.color = debuffColor; // 너프/피해 (빨강)
        }
        else
        {
            textComp.color = normalColor; // 정상 (흰색)
        }
    }

    // --- [연출 기능 1] 공격자용: 공중부양 (Floating) ---
    public void SetFloatingState(bool shouldFloat)
    {
        if (_isFloating == shouldFloat) return;
        _isFloating = shouldFloat;

        transform.DOKill(); // 기존 애니메이션 취소

        if (shouldFloat)
        {
            // 위로 둥실 떠오름
            transform.DOLocalMoveY(_basePosition.y + floatHeight, floatDuration)
                .SetEase(Ease.OutQuad);
        }
        else
        {
            // 원래 자리로 착지
            transform.DOLocalMoveY(_basePosition.y, floatDuration)
                .SetEase(Ease.OutQuad);
        }
    }

    // --- [연출 기능 2] 대상용: 발광 (Glow) ---
    public void SetGlowState(bool shouldGlow)
    {
        if (glowEffectObject != null)
        {
            glowEffectObject.SetActive(shouldGlow);
        }
    }

    // --- [연출 기능 3] 데미지 ---
    public void DamageUI(int damage)
    {
        StartCoroutine(HitUI(damage));
    }

    public IEnumerator HitUI(int damage)
    {
        damageIMG.SetActive(true);
        damageIMG.GetComponent<Text>().text = damage.ToString();

        yield return new WaitForSeconds(1.0f);

        damageIMG.SetActive(false);

    }

    // 위치 재설정 (이동 후 호출)
    public void ResetBasePosition()
    {
        _basePosition = transform.localPosition;
    }
}