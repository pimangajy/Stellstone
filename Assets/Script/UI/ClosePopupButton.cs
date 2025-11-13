using UnityEngine;
using UnityEngine.UI; // Button 컴포넌트를 사용하기 위해 추가

/// <summary>
/// UI 버튼에 추가하여 UIManager의 CloseSpecificPopup 함수를 호출해주는 헬퍼 스크립트입니다.
/// </summary>
[RequireComponent(typeof(Button))] // 이 스크립트는 Button 컴포넌트가 있는 곳에만 추가되도록 강제합니다.
public class ClosePopupButton : MonoBehaviour
{
    /// <summary>
    /// 이 버튼을 눌렀을 때 닫고 싶은 팝업 GameObject입니다.
    /// 인스펙터에서 직접 할당해주세요.
    /// </summary>
    public GameObject popupToClose;

    private Button closeButton;

    private void Start()
    {
        // 버튼 컴포넌트를 가져옵니다.
        closeButton = GetComponent<Button>();

        // 버튼 클릭 이벤트에 우리가 만든 함수를 등록(연결)합니다.
        // (인스펙터에서 수동으로 연결하는 대신 코드로 자동 연결)
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseTargetPopup);
        }

        // 만약 팝업이 할당되지 않았다면 경고를 출력합니다.
        if (popupToClose == null)
        {
            Debug.LogWarning(gameObject.name + " 버튼에 닫을 팝업(Popup To Close)이 할당되지 않았습니다. 인스펙터에서 설정해주세요.", this);
        }
    }

    /// <summary>
    /// UIManager의 싱글톤 인스턴스를 찾아 할당된 팝업을 닫도록 요청합니다.
    /// </summary>
    public void CloseTargetPopup()
    {
        // UIManager 인스턴스가 있는지 확인합니다.
        if (UIManager.Instance == null)
        {
            Debug.LogError("UIManager 인스턴스를 찾을 수 없습니다! 씬에 UIManager가 있는지 확인해주세요.");
            return;
        }

        // 닫을 팝업이 할당되었는지 확인합니다.
        if (popupToClose == null)
        {
            Debug.LogError(gameObject.name + " 버튼에 닫을 팝업이 할당되지 않았습니다. 인스펙터에서 설정이 필요합니다.", this);
            return;
        }

        // UIManager에게 특정 팝업을 닫으라고 명령합니다.
        UIManager.Instance.CloseSpecificPopup(popupToClose);
    }

    // 오브젝트가 파괴될 때 이벤트 리스너를 정리합니다. (메모리 누수 방지)
    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseTargetPopup);
        }
    }
}
