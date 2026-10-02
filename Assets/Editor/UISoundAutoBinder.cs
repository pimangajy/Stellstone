using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class UISoundAutoBinder
{
    [MenuItem("Tools/Stellar Duel/UI/씬 내 모든 Button에 UIButtonSound 자동 부착")]
    public static void AddSoundToAllButtonsInActiveScene()
    {
        var buttons = GameObject.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int addedCount = 0;

        foreach (var btn in buttons)
        {
            if (btn.GetComponent<UIButtonSound>() == null)
            {
                Undo.AddComponent<UIButtonSound>(btn.gameObject);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"<color=#00FF88>[UI Sound] 현재 씬의 {addedCount}개 Button에 'UIButtonSound' 컴포넌트가 추가되었습니다!</color>");
            EditorUtility.DisplayDialog("UI 사운드 자동 부착 완료", $"현재 씬의 총 {addedCount}개 버튼에 UIButtonSound 컴포넌트가 부착되었습니다.\n(인스펙터에서 각 버튼의 Button Type을 변경할 수 있습니다)", "확인");
        }
        else
        {
            Debug.Log("<color=#FFFF00>[UI Sound] 이미 모든 Button에 'UIButtonSound' 컴포넌트가 부착되어 있습니다.</color>");
            EditorUtility.DisplayDialog("알림", "이미 씬 내 모든 버튼에 UIButtonSound 컴포넌트가 연결되어 있습니다.", "확인");
        }
    }
}
