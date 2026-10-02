using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 인게임 배틀에서 카드의 공통 데이터 및 인터페이스를 제공하는 기본(Base) 클래스입니다.
/// 손패(HandCardDisplay), 필드 하수인(FieldCardDisplay), 리더(LeaderCardDisplay)가 이를 상속받습니다.
/// </summary>
public class GameCardDisplay : MonoBehaviour
{
    [Header("스탯 텍스트 색상 설정")]
    public Color normalColor = Color.white;
    public Color buffColor = Color.green;   // 스탯이 높아졌을 때 (초록색)
    public Color debuffColor = Color.red;   // 스탯이 낮아졌을 때 (빨간색)

    [Header("공통 데이터")]
    public CardData _cardData;
    public CardInfo _cardInfo;
    public EntityData CurrentEntityData;
    public int EntityId { get; protected set; }
    public string InstanceId => _cardInfo?.instanceId;
    public bool HasUsedSkillThisTurn
    {
        get => CurrentEntityData != null && CurrentEntityData.hasUsedSkillThisTurn;
        set
        {
            if (CurrentEntityData != null)
            {
                CurrentEntityData.hasUsedSkillThisTurn = value;
            }
        }
    }

    /// <summary>
    /// [손패용] 카드 데이터 설정 (HandCardDisplay에서 오버라이드)
    /// </summary>
    public virtual void Setup(CardData data, CardInfo info)
    {
        _cardData = data;
        _cardInfo = info;
    }

    /// <summary>
    /// [필드용] 소환된 개체 정보 설정 (FieldCardDisplay에서 오버라이드)
    /// </summary>
    public virtual void SetupEntity(EntityData entityData, CardData cardData)
    {
        this.EntityId = entityData != null ? entityData.entityId : 0;
        this.CurrentEntityData = entityData;
        this._cardData = cardData;
    }

    /// <summary>
    /// [리더용] 리더 UI 및 데이터 설정 (LeaderCardDisplay에서 오버라이드)
    /// </summary>
    public virtual void SetLeader(EntityData leaderData)
    {
        if (leaderData != null)
        {
            this.EntityId = leaderData.entityId;
            this.CurrentEntityData = leaderData;
        }
    }

    /// <summary>
    /// [손패용] 스탯 갱신 (HandCardDisplay에서 오버라이드)
    /// </summary>
    public virtual void UpdateCardStats(CardInfo info)
    {
        _cardInfo = info;
    }

    /// <summary>
    /// [필드용] 스탯 갱신 (FieldCardDisplay에서 오버라이드)
    /// </summary>
    public virtual void UpdateEntityStats(EntityData entityData)
    {
        this.CurrentEntityData = entityData;
    }

    // --- [필드/전투 연출 가상 함수 - FieldCardDisplay에서 구현] ---
    public virtual bool IsFloating => false;
    public virtual void SetFloatingState(bool shouldFloat) { }
    public virtual void SetGlowState(bool shouldGlow) { }
    public virtual void SetPendingDeadVisual() { }
    public virtual void DamageUI(int damage) { }
    public virtual void HealUI(int healAmount) { }
    public virtual void PlayBuffVFX() { }
    public virtual void PlayTriggerAnimation(EffectTriggerType triggerType) { }
    public virtual void UpdateKeywordFrames(List<CardKeywords> keywords) { }
    public virtual void UpdateTriggerFrames(List<EffectTriggerType> triggers, bool isSilenced) { }
    public virtual void UpdateAttackVisuals(EntityData entityData) { }
    public virtual void ResetBasePosition() { }

    /// <summary>
    /// [내부 헬퍼] UI 텍스트 내용과 색상을 값에 따라 변경합니다.
    /// </summary>
    protected void SetStatText(TextMeshProUGUI textComp, int currentVal, int originalVal, bool isCost = false)
    {
        if (textComp == null) return;
        textComp.text = currentVal.ToString();

        if (isCost)
        {
            if (currentVal < originalVal) textComp.color = buffColor;       // 코스트 감소 (이득: 초록)
            else if (currentVal > originalVal) textComp.color = debuffColor; // 코스트 증가 (패널티: 빨강)
            else textComp.color = normalColor;                              // 정상 (흰색)
        }
        else
        {
            if (currentVal > originalVal) textComp.color = buffColor;       // 버프 (초록)
            else if (currentVal < originalVal) textComp.color = debuffColor; // 너프/피해 (빨강)
            else textComp.color = normalColor;                              // 정상 (흰색)
        }
    }

    /// <summary>
    /// [내부 헬퍼] 3D 월드 텍스트 내용과 색상을 값에 따라 변경합니다.
    /// </summary>
    protected void EntitySetStatText(TextMeshPro textComp, int currentVal, int originalVal)
    {
        if (textComp == null) return;
        textComp.text = currentVal.ToString();

        if (currentVal > originalVal) textComp.color = buffColor;       // 버프 (초록)
        else if (currentVal < originalVal) textComp.color = debuffColor; // 너프/피해 (빨강)
        else textComp.color = normalColor;                              // 정상 (흰색)
    }

    protected virtual void OnDestroy()
    {
        transform.DOKill();
    }
}
