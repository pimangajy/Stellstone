#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CardPackAnimationTimingSetup
{
    private const string PREFAB_PATH = "Assets/Prefab/Storage/Craft/CardPack.prefab";
    private const string CONTROLLER_PATH = "Assets/Prefab/Storage/Craft/New Animator Controller.controller";

    [MenuItem("StellarDuel/Setup CardPack Idle and Open Animation")]
    public static void SetupAnimator()
    {
        // 1. AnimatorController 로드
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
        if (controller == null)
        {
            Debug.LogError($"[CardPackAnimationTimingSetup] ❌ {CONTROLLER_PATH}를 찾을 수 없습니다!");
            return;
        }

        // 2. 파라미터 "Open" 추가 (없을 경우)
        bool hasOpenParam = false;
        foreach (var p in controller.parameters)
        {
            if (p.name == "Open") { hasOpenParam = true; break; }
        }
        if (!hasOpenParam)
        {
            controller.AddParameter("Open", AnimatorControllerParameterType.Trigger);
        }

        // 3. 첫 번째 레이어의 StateMachine 정리
        var rootStateMachine = controller.layers[0].stateMachine;

        // 기존 개봉 모션 클립 추출
        Motion openMotion = null;
        foreach (var childState in rootStateMachine.states)
        {
            if (childState.state.motion != null)
            {
                openMotion = childState.state.motion;
                break;
            }
        }

        // 기존 상태들 정리
        for (int i = rootStateMachine.states.Length - 1; i >= 0; i--)
        {
            rootStateMachine.RemoveState(rootStateMachine.states[i].state);
        }

        // 4. Idle 상태 (기본 대기 상태, 모션 없음) 생성
        var idleState = rootStateMachine.AddState("Idle");
        idleState.motion = null;

        // 5. Open 상태 생성 및 모션 연결
        var openState = rootStateMachine.AddState("Open");
        openState.motion = openMotion;

        // 6. Idle -> Open 트랜지션 생성 (Open 트리거 발동 시)
        var transition = idleState.AddTransition(openState);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.05f;
        transition.AddCondition(AnimatorConditionMode.If, 0, "Open");

        // 기본 상태를 Idle로 지정!
        rootStateMachine.defaultState = idleState;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("[CardPackAnimationTimingSetup] ✨ AnimatorController에 'Idle'(기본) 및 'Open'(트리거) 상태가 성공적으로 구성되었습니다!");
    }
}
#endif
