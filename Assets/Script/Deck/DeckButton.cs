using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.UI;

/// <summary>
/// 덱 리스트(왼쪽)에 있는 각각의 '덱 버튼'을 관리하는 스크립트입니다.
/// 버튼을 누르면 해당 덱을 편집할 수 있게 해줍니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class DeckButton : MonoBehaviour
{
    // 버튼 위에 표시될 덱 이름 텍스트
    [SerializeField]
    private TextMeshProUGUI deckNameText;

    // 이 버튼이 담고 있는 덱 정보
    public DeckData deckData;

    // Action: 함수를 변수처럼 저장하는 '대리자'입니다.
    // "버튼이 눌리면 이 함수를 실행해줘!"라고 외부에서 주입받습니다.
    private Action<DeckData> onClickAction;

    /// <summary>
    /// 버튼을 초기화하는 함수입니다. (외부에서 호출)
    /// </summary>
    /// <param name="data">이 버튼이 가질 덱 데이터</param>
    /// <param name="onClickCallback">클릭됐을 때 실행할 함수</param>
    public void Setup(DeckData data, Action<DeckData> onClickCallback)
    {
        this.deckData = data;
        this.deckNameText.text = data.deckName; // 버튼 글씨를 덱 이름으로 변경
        this.onClickAction = onClickCallback; // 클릭 행동 저장
    }

    private void Start()
    {
        // 실제 버튼 컴포넌트의 클릭 이벤트에 내 함수(HandleClick)를 연결합니다.
        GetComponent<Button>().onClick.AddListener(HandleClick);
    }

    /// <summary>
    /// 사용자가 버튼을 클릭하면 이 함수가 실행됩니다.
    /// </summary>
    private void HandleClick()
    {
        // ?. : 만약 onClickAction이 연결되어 있다면(null이 아니면) 실행(Invoke)합니다.
        // 이때 내 덱 데이터(deckData)를 매개변수로 넘겨줍니다.
        onClickAction?.Invoke(deckData);
    }
}