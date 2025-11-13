using UnityEngine;
using UnityEngine.UI;

// 이 스크립트는 반드시 Button 컴포넌트가 있는 오브젝트에 붙여야 합니다.
[RequireComponent(typeof(Button))]
public class SceneLoadButton : MonoBehaviour
{
    [Tooltip("이 버튼을 눌렀을 때 이동할 씬의 이름을 입력하세요.")]
    public string sceneName;

    private Button button;

    void Start()
    {
        // 1. 이 스크립트가 붙어있는 오브젝트의 Button 컴포넌트를 가져옵니다.
        button = GetComponent<Button>();

        // 2. 기존에 인스펙터로 연결된 리스너가 있다면 모두 제거합니다. (선택 사항이지만, 중복 실행 방지를 위해 권장)
        button.onClick.RemoveAllListeners();

        // 3. 버튼 클릭 시 실행할 함수를 '동적'으로 연결합니다.
        button.onClick.AddListener(LoadScene);
    }

    void LoadScene()
    {
        // 4. UIManager 싱글톤 인스턴스가 존재하는지 확인합니다.
        if (UIManager.Instance != null)
        {
            // 5. UIManager 인스턴스에 붙어있는 SceneLoader 컴포넌트를 찾습니다.
            SceneLoader loader = UIManager.Instance.GetComponent<SceneLoader>();

            if (loader != null)
            {
                // 6. SceneLoader의 함수를 호출합니다.
                loader.LoadSceneByName(sceneName);
            }
            else
            {
                Debug.LogError("UIManager 오브젝트에 SceneLoader 컴포넌트가 없습니다!");
            }
        }
        else
        {
            Debug.LogError("UIManager.Instance를 찾을 수 없습니다! 씬에 UIManager가 배치되어 있는지 확인하세요.");
        }
    }
}
