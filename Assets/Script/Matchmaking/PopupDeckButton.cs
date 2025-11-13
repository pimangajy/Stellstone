using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System; // Action을 사용하기 위해 필요

/// <summary>
/// 덱 선택 팝업의 스크롤 뷰에 들어갈 개별 덱 버튼 프리팹에 부착됩니다.
/// </summary>
public class PopupDeckButton : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private TextMeshProUGUI deckNameText;
    [SerializeField] private Button button;
    [SerializeField] private Image classIconImage; // 직업 아이콘 (선택 사항)

    private DeckData heldDeck; // 이 버튼이 표시하는 덱의 데이터
    private Action<DeckData> onClickCallback; // 클릭 시 호출될 함수 (DeckSelectPopup의 ShowDeckDetails)

    /// <summary>
    /// DeckSelectPopup이 이 버튼을 생성할 때 호출합니다.
    /// </summary>
    /// <param name="deck">표시할 덱의 데이터</param>
    /// <param name="callback">이 버튼이 클릭되었을 때 실행할 함수</param>
    public void Setup(DeckData deck, Action<DeckData> callback)
    {
        heldDeck = deck;
        onClickCallback = callback;

        deckNameText.text = deck.deckName;

        // TODO: 덱의 직업(deck.deckClass)에 맞는 아이콘을 classIconImage.sprite에 설정
        // 예: classIconImage.sprite = ClassIconManager.instance.GetIcon(deck.deckClass);

        // 이 버튼의 OnClick 이벤트에 HandleClick 함수를 등록
        button.onClick.AddListener(HandleClick);
    }

    /// <summary>
    /// 버튼이 클릭되면, Setup에서 등록한 콜백 함수를 실행합니다.
    /// </summary>
    private void HandleClick()
    {
        // DeckSelectPopup.ShowDeckDetails(heldDeck)를 호출하게 됨
        onClickCallback?.Invoke(heldDeck);
    }
}
