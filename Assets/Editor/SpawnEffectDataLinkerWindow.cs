using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SpawnEffectDataLinkerWindow : EditorWindow
{
    // 공통으로 적용할 스폰 이펙트 스크립터블 오브젝트
    public SpawnEffectData targetSpawnEffect;
    // 적용할 카드 리스트
    public List<CardData> targetCards = new List<CardData>();

    private SerializedObject so;
    private SerializedProperty propSpawnEffect;
    private SerializedProperty propCards;

    [MenuItem("Tools/Link Card Resources (Spawn Effect)")]
    public static void ShowWindow()
    {
        GetWindow<SpawnEffectDataLinkerWindow>("Spawn Effect Linker");
    }

    private void OnEnable()
    {
        so = new SerializedObject(this);
        propSpawnEffect = so.FindProperty("targetSpawnEffect");
        propCards = so.FindProperty("targetCards");
    }

    private void OnGUI()
    {
        GUILayout.Label("스폰 이펙트(Spawn Effect) 일괄 할당기", EditorStyles.boldLabel);
        GUILayout.Space(10);

        so.Update();

        // 스폰 이펙트 데이터 변수 필드
        EditorGUILayout.PropertyField(propSpawnEffect, new GUIContent("공통 스폰 이펙트 데이터"));
        GUILayout.Space(10);

        // 적용할 카드 리스트 필드
        EditorGUILayout.PropertyField(propCards, new GUIContent("적용 대상 카드 리스트"), true);

        so.ApplyModifiedProperties();

        GUILayout.Space(20);

        if (GUILayout.Button("목록의 모든 카드에 스폰 이펙트 할당", GUILayout.Height(40)))
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
                card.spawnEffectData = targetSpawnEffect; // SpawnEffectData 스크립터블 오브젝트 할당
                EditorUtility.SetDirty(card); // 변경 사항 적용 알림
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"스폰 이펙트 데이터 일괄 할당 완료! 총 {count}개의 카드에 적용되었습니다.");
    }
}
