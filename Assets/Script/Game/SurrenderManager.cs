using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SurrenderManager : MonoBehaviour
{
    public Button surrenderbtn;

    public UIPanelToggler resultsPanel;
    public TextMeshProUGUI resultsText;

    void Start()
    {
        if(GameClient.Instance  != null)
        {
            GameClient.Instance.OnGameOverEvent += Results_Window;
        }

        if (surrenderbtn != null)
        {
            surrenderbtn.onClick.AddListener(Surrender);
        }
    }

    public void Surrender()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.SendConcedeRequest();
        }
    }

    public void Results_Window(S_GameOver over)
    {
        if (resultsPanel != null)
        {
            Debug.Log("결과창 오픈");
            resultsPanel.ShowPanel();
            resultsText.text = over.reason;
        }
    }

    public void SceneMove(string sceneName)
    {
        if(SceneLoader.instance != null)
        {
            SceneLoader.instance.LoadSceneByName(sceneName);
        }
    }
}
