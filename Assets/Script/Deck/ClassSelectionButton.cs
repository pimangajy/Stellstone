using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 직업 선택 버튼(예: 마법사, 사냥꾼 아이콘)에 붙여서 사용하는 스크립트입니다.
/// 버튼을 누르면 "나 이 직업으로 덱 짤래!"라고 DeckBuilder에게 알려줍니다.
/// </summary>
// [RequireComponent]: 이 스크립트는 반드시 Button 컴포넌트가 같이 있어야 한다고 강제합니다.
[RequireComponent(typeof(Button))]
public class ClassSelectionButton : MonoBehaviour
{
    // 유니티 에디터에서 직접 입력해줄 직업 이름입니다. (예: "Mage", "Warrior")
    [Tooltip("이 버튼이 나타내는 직업 이름입니다. CardDataFirebase의 직업 속성과 일치해야 합니다.")]
    public string className;

    // DeckBuilder 스크립트에게 연락하기 위해 주소를 저장할 변수
    private DeckBuilder deckBuilder;

    void Start()
    {
        // 화면(Scene) 전체를 뒤져서 DeckBuilder 스크립트를 찾아냅니다.
        deckBuilder = FindObjectOfType<DeckBuilder>();

        // 못 찾았으면 에러 메시지를 띄웁니다.
        if (deckBuilder == null)
        {
            Debug.LogError("씬에서 DeckBuilder 스크립트를 찾을 수 없습니다!");
            return;
        }

        // 내 몸에 붙어있는 버튼 컴포넌트를 가져옵니다.
        Button button = GetComponent<Button>();

        // 버튼이 클릭되면(onClick) -> OnButtonClick 함수를 실행하라고 연결(AddListener)합니다.
        button.onClick.AddListener(OnButtonClick);
    }

    /// <summary>
    /// 실제 버튼이 클릭되었을 때 실행되는 함수입니다.
    /// </summary>
    void OnButtonClick()
    {
        // 덱 빌더가 존재한다면
        if (deckBuilder != null)
        {
            // 1. 덱 빌더에게 "이 직업(className)으로 필터링해줘"라고 요청합니다.
            deckBuilder.SetClassFilter(className);

            // 2. UI 매니저에게 팝업창(직업 선택창)을 닫아달라고 요청합니다.
            UIManager.Instance.ClearPopupList();
        }
    }
}