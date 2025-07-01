using UnityEngine;
using UnityEditor; // 에디터 스크립트를 만들기 위해 꼭 필요합니다.

/// <summary>
/// CardData 스크립터블 오브젝트의 인스펙터 창을 커스터마이징합니다.
/// </summary>
[CustomEditor(typeof(CardData))]
public class CardDataEditor : Editor
{
    // OnInspectorGUI 함수는 인스펙터 창이 그려질 때마다 호출됩니다.
    public override void OnInspectorGUI()
    {
        // 1. 기본 인스펙터를 그립니다. (cardType, cardName 등 attack/health를 제외한 모든 변수)
        // base.OnInspectorGUI(); // 이 방법 대신 아래처럼 수동으로 그립니다.

        // 타겟 스크립트의 모든 변수를 가져옵니다.
        serializedObject.Update();

        // 모든 변수를 순회하며 attack과 health를 제외하고 그립니다.
        SerializedProperty prop = serializedObject.GetIterator();
        if (prop.NextVisible(true))
        {
            do
            {
                if (prop.name != "attack" && prop.name != "health" && prop.name != "minionTribe" && prop.name != "spellType" && prop.name != "EquipmentType" 
                    && prop.name != "targetRule")
                {
                    EditorGUILayout.PropertyField(prop, true);
                }
            } while (prop.NextVisible(false));
        }

        // 2. 현재 선택된 CardType을 가져옵니다.
        CardData cardData = (CardData)target;

        // 3. CardType에 따라 보여줄 항목을 결정합니다.
        switch (cardData.cardType)
        {
            case CardType.하수인:
                EditorGUILayout.LabelField("하수인 전용 스탯", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("minionTribe"), new GUIContent("종족"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("attack"), new GUIContent("공격력"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("health"), new GUIContent("체력"));
                break;

            case CardType.주문:
                EditorGUILayout.LabelField("주문 전용 스탯", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("spellType"), new GUIContent("타입"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("targetRule"), new GUIContent("대상"));
                break;
                
            case CardType.장비:
                EditorGUILayout.LabelField("장비 전용 스탯", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("EquipmentType"), new GUIContent("종류"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("attack"), new GUIContent("공격력"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("health"), new GUIContent("내구도"));
                break;
        }

        // 변경된 사항을 저장합니다.
        serializedObject.ApplyModifiedProperties();
    }
}
