using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

// 카드가 가질 수 있는 모든 상태를 정의합니다.
public enum CardState
{
    Idle,       // 아무것도 안 하는 기본 상태
    Arranging,  // HandManager에 의해 정렬되는 중인 상태
    Hovering,   // 플레이어의 마우스가 올라와 있는 상태
    Dragging    // 플레이어에 의해 드래그되는 중인 상태
}
public class CardInHandController : MonoBehaviour
{
    [Header("데이터 및 참조")]
    public CardData cardData; // 원본 데이터
    private CardDisplay cardDisplay; // 시각적 표현을 담당하는 스크립트 참조
    private RectTransform rectTransform;

    [Header("현재 상태 (수정치)")]
    public int attackModifier = 0;
    public int healthModifier = 0;
    public int manaModifier = 0;

    public CardState currentState = CardState.Idle;

    // --- 계산된 최종 스탯 ---
    public int CurrentMana => cardData.manaCost + manaModifier;
    public int CurrentAttack => cardData.attack + attackModifier;
    public int CurrentHealth => cardData.health + healthModifier;

    void Awake()
    {
        // CardDisplay 스크립트의 참조를 미리 가져옵니다.
        cardDisplay = GetComponent<CardDisplay>();
        rectTransform = GetComponent<RectTransform>();
    }

    // 카드가 생성되거나 데이터가 할당될 때 호출될 함수
    public void Initialize()
    {
        // 수정치를 0으로 초기화
        attackModifier = 0;
        healthModifier = 0;
        manaModifier = 0;

        // 화면에 기본 스탯을 표시
        UpdateDisplay();
    }

    // 공격력과 체력 버프를 적용하는 함수
    public void ApplyStatBuff(int attack, int health)
    {
        attackModifier += attack;
        healthModifier += health;

        // 스탯이 변경되었으니 화면을 갱신합니다.
        UpdateDisplay();
    }

    // 비용(마나)을 변경하는 함수
    public void ModifyCost(int amount)
    {
        manaModifier += amount;

        // 스탯이 변경되었으니 화면을 갱신합니다.
        UpdateDisplay();
    }

    /// <summary>
    /// 현재 스탯을 기반으로 CardDisplay를 업데이트하여 화면에 보여줍니다.
    /// </summary>
    private void UpdateDisplay()
    {
        if (cardDisplay == null) return;

        // CardDisplay에게 현재 최종 스탯 정보를 전달하여 화면을 그리도록 요청합니다.
        // (이 기능을 위해 CardDisplay 스크립트도 약간의 수정이 필요합니다.)
        // cardDisplay.UpdateStatDisplay(CurrentMana, CurrentAttack, CurrentHealth, manaModifier, attackModifier, healthModifier);

        // 임시로, 스탯 텍스트만 직접 변경하는 코드
        if (cardDisplay.manaText_UI != null) cardDisplay.manaText_UI.text = CurrentMana.ToString();
        if (cardDisplay.attackText_UI != null) cardDisplay.attackText_UI.text = CurrentAttack.ToString();
        if (cardDisplay.healthText_UI != null) cardDisplay.healthText_UI.text = CurrentHealth.ToString();

        // 여기에 추가로, 수정치 값에 따라 텍스트 색상을 바꾸는 로직을 넣을 수 있습니다.
        // 예: attackModifier > 0 이면 attackText_UI.color = Color.green;
    }


    /// <summary>
    /// 외부(주로 CardHoverEffect)에서 호출하여 현재 상태를 변경합니다.
    /// </summary>
    public void SetState(CardState newState)
    {
        currentState = newState;
    }

    /// <summary>
    /// 외부에서 현재 상태를 확인할 수 있는 함수입니다.
    /// </summary>
    public CardState GetState()
    {
        return currentState;
    }
}
