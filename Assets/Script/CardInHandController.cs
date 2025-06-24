using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class CardInHandController : MonoBehaviour
{
    [Header("데이터 및 참조")]
    public CardData cardData; // 원본 데이터
    private CardDisplay cardDisplay; // 시각적 표현을 담당하는 스크립트 참조

    [Header("현재 상태 (수정치)")]
    public int attackModifier = 0;
    public int healthModifier = 0;
    public int manaModifier = 0;

    // --- 계산된 최종 스탯 ---
    public int CurrentMana => cardData.manaCost + manaModifier;
    public int CurrentAttack => cardData.attack + attackModifier;
    public int CurrentHealth => cardData.health + healthModifier;

    void Awake()
    {
        // CardDisplay 스크립트의 참조를 미리 가져옵니다.
        cardDisplay = GetComponent<CardDisplay>();
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
}
