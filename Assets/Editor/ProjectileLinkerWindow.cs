using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ProjectileLinkerWindow : EditorWindow
{
    // 공통으로 적용할 투사체 프리팹
    public GameObject targetProjectile;
    // 투사체를 적용할 카드 리스트
    public List<CardData> targetCards = new List<CardData>();

    private SerializedObject so;
    private SerializedProperty propProjectile;
    private SerializedProperty propCards;

    [MenuItem("Tools/Link Card Resources (Projectile)")]
    public static void ShowWindow()
    {
        GetWindow<ProjectileLinkerWindow>("Projectile Linker");
    }

    private void OnEnable()
    {
        // 현재 에디터 창 자체를 직렬화(Serialize)하여 리스트 UI를 쉽게 그릴 수 있게 합니다.
        so = new SerializedObject(this);
        propProjectile = so.FindProperty("targetProjectile");
        propCards = so.FindProperty("targetCards");
    }

    private void OnGUI()
    {
        GUILayout.Label("투사체(Projectile) 일괄 할당기", EditorStyles.boldLabel);
        GUILayout.Space(10);

        so.Update();

        // 투사체 프리팹 변수 필드
        EditorGUILayout.PropertyField(propProjectile, new GUIContent("공통 투사체 프리팹"));
        GUILayout.Space(10);

        // 적용할 카드 리스트 필드 (true 옵션으로 하위 요소 펼치기 기능 활성화)
        EditorGUILayout.PropertyField(propCards, new GUIContent("적용 대상 카드 리스트"), true);

        so.ApplyModifiedProperties();

        GUILayout.Space(20);

        if (GUILayout.Button("목록의 모든 카드에 투사체 할당", GUILayout.Height(40)))
        {
            ApplyToCards();
        }
    }

    private void ApplyToCards()
    {
        if (targetCards.Count == 0)
        {
            Debug.LogWarning("카드가 목록에 없습니다. 적용할 카드를 추가해 주세요.");
            return;
        }

        int count = 0;
        foreach (CardData card in targetCards)
        {
            if (card != null)
            {
                card.projectilePrefab = targetProjectile; // GameObject 할당
                EditorUtility.SetDirty(card); // 변경 사항 적용 알림
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"투사체 일괄 할당 완료! 총 {count}개의 카드에 적용되었습니다.");
    }
}
