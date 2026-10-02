using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 멤버 스킬 선택 UI(MemberSkillSelect) 내 개별 스킬 항목(SkillPanel)을 제어하는 스크립트입니다.
/// </summary>
public class SkillPanelItemUI : MonoBehaviour
{
    [Header("UI 바인딩")]
    public Button skillButton;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI descriptionText;

    private MemberSkillData _skillData;
    private Action<MemberSkillData> _onClickCallback;

    private void Awake()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        if (skillButton == null)
            skillButton = GetComponent<Button>();

        // 코스트 텍스트 자동 탐색 (자식 Image 안의 Text (TMP))
        if (costText == null)
        {
            Transform imgChild = transform.Find("Image");
            if (imgChild != null)
            {
                costText = imgChild.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        // 설명 텍스트 자동 탐색
        if (descriptionText == null)
        {
            Transform textChild = transform.Find("Text (TMP)");
            if (textChild != null)
            {
                descriptionText = textChild.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                var allTmps = GetComponentsInChildren<TextMeshProUGUI>(true);
                if (allTmps.Length > 1)
                {
                    descriptionText = allTmps[1];
                }
            }
        }

        if (skillButton != null)
        {
            skillButton.onClick.RemoveAllListeners();
            skillButton.onClick.AddListener(OnClick);
        }
    }

    /// <summary>
    /// 스킬 데이터를 바탕으로 UI를 구성합니다.
    /// </summary>
    public void Setup(MemberSkillData skillData, int currentMemberHealth, Action<MemberSkillData> onClickCallback)
    {
        Setup(skillData, currentMemberHealth, false, onClickCallback);
    }

    /// <summary>
    /// 스킬 데이터를 바탕으로 UI를 구성합니다.
    /// 이번 턴에 이미 스킬을 사용했거나 체력이 부족한 경우 버튼을 비활성화(어둡게)합니다.
    /// </summary>
    public void Setup(MemberSkillData skillData, int currentMemberHealth, bool hasUsedSkillThisTurn, Action<MemberSkillData> onClickCallback)
    {
        InitializeComponents();

        _skillData = skillData;
        _onClickCallback = onClickCallback;

        if (skillData == null) return;

        // 1. 코스트 표시: 양수는 "+수치", 음수는 "-수치", 0은 "0"
        if (costText != null)
        {
            if (skillData.healthCost > 0)
            {
                costText.text = $"+{skillData.healthCost}";
            }
            else
            {
                costText.text = skillData.healthCost.ToString();
            }
        }

        // 2. 설명 텍스트 (CardTextFormatter를 통한 키워드/데미지 포맷팅)
        if (descriptionText != null)
        {
            CardTextFormatter.EnsureTextAutoFit(descriptionText, 10f, 22f, true, TextOverflowModes.Ellipsis);
            string desc = skillData.description;
            if (!string.IsNullOrEmpty(desc))
            {
                desc = CardTextFormatter.FormatDescription(desc);
            }
            descriptionText.text = desc;
        }

        // 3. 체력 및 턴당 1회 사용 제한 체크 (이미 스킬을 사용했거나 체력이 부족한 경우 사용 불가)
        bool canUse = true;
        if (hasUsedSkillThisTurn)
        {
            canUse = false;
        }
        else if (skillData.healthCost < 0)
        {
            int requiredHealth = Mathf.Abs(skillData.healthCost);
            if (currentMemberHealth < requiredHealth)
            {
                canUse = false;
            }
        }

        if (skillButton != null)
        {
            skillButton.interactable = canUse;
        }
    }

    private void OnClick()
    {
        if (_skillData != null && (skillButton == null || skillButton.interactable))
        {
            _onClickCallback?.Invoke(_skillData);
        }
    }
}
