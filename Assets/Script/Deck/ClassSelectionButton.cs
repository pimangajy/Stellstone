using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 직업 선택 버튼에 부착하는 스크립트입니다.
/// 버튼 클릭 시 DeckBuilder에 어떤 직업이 선택되었는지 알려주는 역할을 합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class ClassSelectionButton : MonoBehaviour
{
    [Tooltip("이 버튼이 나타내는 직업 이름입니다. CardDataFirebase의 직업 속성과 일치해야 합니다. (예: '마법사', '전사')")]
    public string className;

    // DeckBuilder 스크립트의 참조를 저장할 변수
    private DeckBuilder deckBuilder;

    void Start()
    {
        // 씬에서 DeckBuilder 스크립트를 찾아서 참조를 저장합니다.
        // (더 좋은 방법은 싱글톤이나 직접 참조를 연결하는 것이지만, 지금은 간단한 구현을 위해 이 방식을 사용합니다.)
        deckBuilder = FindObjectOfType<DeckBuilder>();
        if (deckBuilder == null)
        {
            Debug.LogError("씬에서 DeckBuilder 스크립트를 찾을 수 없습니다!");
            return;
        }

        // 이 스크립트가 붙어있는 게임 오브젝트의 Button 컴포넌트를 가져옵니다.
        Button button = GetComponent<Button>();
        // 버튼의 OnClick 이벤트에 OnButtonClick 함수를 동적으로 연결합니다.
        button.onClick.AddListener(OnButtonClick);
    }

    /// <summary>
    /// 버튼이 클릭되었을 때 호출될 함수입니다.
    /// </summary>
    void OnButtonClick()
    {
        // DeckBuilder의 직업 필터 함수를 호출하고, 이 버튼의 직업 이름을 전달합니다.
        if (deckBuilder != null)
        {
            deckBuilder.SetClassFilter(className);
            UIManager.Instance.ClearPopupList();

            // (선택) 직업 선택 창을 닫는 코드를 여기에 추가할 수 있습니다.
            // 예를 들어, transform.parent.parent.gameObject.SetActive(false);
        }
    }
}

