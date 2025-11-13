using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro를 사용하기 위해 필요
using System;
using UnityEngine.UI; // Action을 사용하기 위해 필요

/// <summary>
/// 덱 목록에 표시되는 개별 덱 버튼 UI를 제어합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class DeckButton : MonoBehaviour
{
    [SerializeField] 
    private TextMeshProUGUI deckNameText;

    public DeckData deckData;
    private Action<DeckData> onClickAction;

    /// <summary>
    /// 이 버튼을 설정하고, 어떤 덱 데이터를 표시할지, 클릭 시 어떤 행동을 할지 결정합니다.
    /// </summary>
    public void Setup(DeckData data, Action<DeckData> onClickCallback)
    {
        this.deckData = data;
        this.deckNameText.text = data.deckName;
        this.onClickAction = onClickCallback;
    }

    private void Start()
    {
        // 버튼 컴포넌트를 가져와 OnClick 이벤트를 연결합니다.
        GetComponent<Button>().onClick.AddListener(HandleClick);
    }

    /// <summary>
    /// 버튼이 클릭되었을 때 호출될 함수입니다.
    /// </summary>
    private void HandleClick()
    {
        // Setup에서 등록한 onClickAction을 실행하고, 자신의 덱 데이터를 전달합니다.
        onClickAction?.Invoke(deckData);
    }
}
