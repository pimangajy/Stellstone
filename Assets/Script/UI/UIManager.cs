using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리 API 사용을 위해 추가

public class UIManager : MonoBehaviour
{
    // --- 싱글톤 인스턴스 ---
    public static UIManager Instance { get; private set; }

    // --- 내부 변수 ---
    // [수정] Stack -> List로 변경하여 특정 항목을 제거할 수 있도록 합니다.
    // 열려있는 팝업들을 관리할 리스트
    private List<GameObject> openPopups = new List<GameObject>();

    private void Awake()
    {
        // --- 싱글톤 패턴 구현 ---
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 새로운 씬이 로드되었을 때 자동으로 호출될 함수입니다.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 새로 로드되었으므로, 기존 팝업 리스트를 깨끗하게 비웁니다.
        ClearPopupList();
    }

    private void Update()
    {
        // ESC 키가 눌렸는지 확인합니다.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 닫을 팝업이 있다면
            if (openPopups.Count > 0)
            {
                // 가장 마지막에 열린 팝업을 닫습니다. (기존 기능 유지)
                ClosePopup();
            }
        }
    }

    /// <summary>
    /// 팝업을 열고 리스트에 추가합니다.
    /// </summary>
    /// <param name="popupObject">열고자 하는 팝업의 GameObject</param>
    public void OpenPopup(GameObject popupObject)
    {
        if (popupObject == null) return;

        popupObject.SetActive(true);
        // [수정] Push -> Add
        openPopups.Add(popupObject);
    }

    /// <summary>
    /// (ESC 키용) 가장 마지막에 연 팝업을 리스트에서 제거하고 닫습니다.
    /// </summary>
    public void ClosePopup()
    {
        if (openPopups.Count == 0)
        {
            Debug.Log("열려있는 팝업창이 없습니다.");
            return;
        }

        // [수정] 스택의 Pop 대신 리스트의 마지막 항목을 가져옵니다.
        int lastIndex = openPopups.Count - 1;
        GameObject popupToClose = openPopups[lastIndex];

        // [수정] 리스트에서 마지막 항목을 제거합니다.
        openPopups.RemoveAt(lastIndex);

        // 팝업 닫기 로직 (UIPanelToggler 또는 SetActive)
        ClosePopupObject(popupToClose);
    }

    /// <summary>
    /// [추가] 특정 팝업을 닫고 리스트에서 제거합니다.
    /// 이 함수를 팝업의 '닫기' 버튼 OnClick에 연결하세요.
    /// </summary>
    /// <param name="popupToClose">닫고자 하는 특정 팝업의 GameObject</param>
    public void CloseSpecificPopup(GameObject popupToClose)
    {
        if (popupToClose == null) return;

        // [추가] 리스트에 해당 팝업이 있는지 확인하고 제거합니다.
        if (openPopups.Contains(popupToClose))
        {
            openPopups.Remove(popupToClose);
        }
        else
        {
            // 리스트에 없더라도(예: UIManager로 열지 않은 경우) 닫기를 시도합니다.
            Debug.LogWarning(popupToClose.name + " 팝업이 UIManager 리스트에 없습니다. 닫기만 시도합니다.");
        }

        // 팝업 닫기 로직
        ClosePopupObject(popupToClose);
    }


    /// <summary>
    /// 리스트에 있는 모든 팝업을 강제로 닫고 리스트를 비웁니다.
    /// (이름 변경: ClearPopupStack -> ClearPopupList)
    /// </summary>
    public void ClearPopupList()
    {
        // 리스트에 있는 모든 팝업을 비활성화합니다.
        foreach (var popup in openPopups)
        {
            if (popup != null)
            {
                popup.SetActive(false);
            }
        }
        // 리스트를 완전히 비웁니다.
        openPopups.Clear();
    }

    /// <summary>
    /// [추가] 팝업 닫기 공통 로직 (중복 제거용)
    /// </summary>
    /// <param name="popupObject">닫을 팝업</param>
    private void ClosePopupObject(GameObject popupObject)
    {
        if (popupObject == null) return;

        // UIPanelToggler에게 애니메이션과 함께 닫으라고 명령합니다.
        UIPanelToggler toggler = popupObject.GetComponent<UIPanelToggler>();
        if (toggler != null)
        {
            toggler.HidePanel(); // HidePanel은 애니메이션 실행 후 비활성화를 처리해야 합니다.
        }
        else
        {
            // UIPanelToggler가 없는 UI라면 즉시 비활성화합니다.
            popupObject.SetActive(false);
        }
    }
}
