using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리를 위해 필요
using System.Threading.Tasks; // Task를 사용하기 위해 필요
using TMPro; // (선택 사항) 로딩 상태 텍스트

/// <summary>
/// 게임 시작 시 필요한 모든 데이터를 로드하고 메인 씬으로 이동시킵니다.
/// 로딩 씬(0번 씬)에 배치되어야 합니다.
/// </summary>
public class LoadingSceneManager : MonoBehaviour
{
    [Header("씬 설정")]
    [Tooltip("로딩이 완료된 후 이동할 씬의 이름")]
    [SerializeField] private string nextSceneName = "1. MainMenuScene"; // 로비 씬 이름으로 변경하세요

    [Header("UI (선택 사항)")]
    [Tooltip("현재 로딩 상태를 표시할 텍스트")]
    [SerializeField] private TextMeshProUGUI loadingStatusText;


    private void Awake()
    {
        // [최적화] 기기 과열 방지 및 부드러운 프레임 유지를 위해 60 FPS 고정
        Application.targetFrameRate = 60;
    }

    // Start() 함수를 async void로 선언하여 비동기 작업을 await할 수 있게 합니다.
    async void Start()
    {
        // 영구 매니저들이 DontDestroyOnLoad로 등록될 시간을 벌어줍니다.
        // (보통 필요 없지만, 안전을 위해 첫 프레임 대기)
        await Task.Yield();

        try
        {
            // --- 1. 모든 카드 정보 로드 ---
            UpdateStatus("모든 카드 정보를 불러오는 중...");

            // 텍스트가 바뀔 시간을 아주 잠깐 줍니다 (0.1초)
            await Task.Delay(100);
            // ResourceManager에게 로딩 지시
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.LoadAllCards();
            }
            else
            {
                Debug.LogError("ResourceManager가 씬에 없습니다!");
            }

            // --- 2. 유저 덱 정보 로드 ---
            UpdateStatus("유저 덱 목록을 불러오는 중...");


            // DeckSaveManager의 인스턴스를 찾아 새로 만든 InitializeAsync를 호출합니다.
            //await DeckSaveManager_Firebase.instance.InitializeAsync(currentUser.UserId);

            // --- 3. 모든 로딩 완료 ---
            UpdateStatus("로드 완료!");

            // 모든 작업이 성공적으로 끝나면 다음 씬으로 이동합니다.
            SceneManager.LoadScene(nextSceneName);
        }
        catch (System.Exception e)
        {
            // 로딩 중 오류 발생 시 (인터넷 연결 끊김, 서버 오류 등)
            Debug.LogError($"필수 데이터 로딩 실패: {e.Message}");
            UpdateStatus($"오류 발생: {e.Message}\n앱을 재시작하세요.");
            // TODO: 여기에 "재시도" 버튼이나 "종료" 버튼을 활성화하는 UI 로직을 넣으면 좋습니다.
        }
    }

    /// <summary>
    /// (선택 사항) 로딩 상태 텍스트 UI를 업데이트합니다.
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (loadingStatusText != null)
        {
            loadingStatusText.text = message;
        }
    }
}
