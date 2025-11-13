using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase.Auth; // Firebase 사용을 위해 추가
using Firebase.Firestore;
using Firebase.Extensions; // ContinueWithOnMainThread 사용을 위해 필요

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader instance;

    // 씬 간에 전달할 '편집할 덱' 데이터
    public DeckData DeckToEdit { get; private set; }

    public string gameID;

    void Awake()
    {
        // --- 영구 싱글톤 패턴 구현 ---
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {

    }

    /// <summary>
    /// 이름으로 씬을 로드합니다.
    /// </summary>
    public void LoadSceneByName(string sceneName)
    {
        Debug.Log($"{sceneName} 씬으로 이동합니다.");
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// '편집할 덱' 데이터를 저장한 뒤, 덱 빌더 씬으로 이동합니다.
    /// </summary>
    public void LoadDeckEditorScene(DeckData deck, string sceneName)
    {
        Debug.Log($"'{deck.deckName}' 덱 정보를 저장하고 {sceneName} 씬으로 이동합니다.");
        DeckToEdit = deck; // 덱 정보를 임시 저장
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 덱 빌더가 데이터를 사용한 후 호출하여, 임시 데이터를 비웁니다.
    /// (다음에 덱 빌더 씬에 그냥 들어왔을 때 또 로드되는 것을 방지)
    /// </summary>
    public void ClearDeckToEdit()
    {
        DeckToEdit = null;
    }
}
