using UnityEngine;
using UnityEditor;
using System.IO;

public class CardResourceLinker : EditorWindow
{
    // 이미지가 들어있는 최상위 폴더 경로
    private string baseImagePath = "Assets/Resources/Cardsimg";
    // 카드 데이터가 있는 폴더 경로
    private string cardDataPath = "Assets/Resources/CardData";

    [MenuItem("Tools/Link Card Resources (Images, etc)")]
    public static void ShowWindow()
    {
        GetWindow<CardResourceLinker>("Card Resource Linker");
    }

    private void OnGUI()
    {
        GUILayout.Label("스크립터블 오브젝트 리소스 자동 연결기", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("이미지 및 리소스 자동 연결 실행", GUILayout.Height(40)))
        {
            LinkResources();
        }
    }

    private void LinkResources()
    {
        // 1. 지정된 폴더 내의 모든 CardData 에셋을 찾습니다.
        string[] guids = AssetDatabase.FindAssets("t:CardData", new[] { cardDataPath });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);

            if (card != null && !string.IsNullOrEmpty(card.cardID))
            {
                // 2. ID 분리: "cards-gangzi-001" -> "001" 추출
                string[] idParts = card.cardID.Split('-');
                string imageNumber = idParts[idParts.Length - 1]; // 마지막 부분 (001)

                // 3. 직업명 가져오기 (Gangzi, Yuni 등)
                string className = card.cardClass.ToString();

                // 4. 찾을 이미지의 폴더 경로 조합 (예: Assets/Resources/Cardsimg/Gangzi/Minion)
                // (추후 card.cardType을 확인하여 '주문(Spell)' 폴더를 따로 분기할 수도 있습니다.)
                string searchFolder = $"{baseImagePath}/{className}/Minion";

                // 해당 폴더가 존재하는지 확인
                if (AssetDatabase.IsValidFolder(searchFolder))
                {
                    // 5. 해당 폴더에서 이름이 일치하는 Sprite 에셋 찾기 (확장자 무관)
                    string[] spriteGuids = AssetDatabase.FindAssets($"{imageNumber} t:Sprite", new[] { searchFolder });

                    if (spriteGuids.Length > 0)
                    {
                        // 찾은 첫 번째 이미지의 경로를 가져옵니다.
                        string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuids[0]);
                        Sprite foundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

                        if (foundSprite != null)
                        {
                            // 6. animationFrames 배열이 비어있다면 새로 1칸 생성
                            if (card.animationFrames == null || card.animationFrames.Length == 0)
                            {
                                card.animationFrames = new Sprite[3];
                            }

                            // 배열의 첫 번째(인덱스 0)에 이미지 할당
                            card.animationFrames[0] = foundSprite;

                            // 변경 사항 적용
                            EditorUtility.SetDirty(card);
                            count++;
                        }
                    }
                }

                // 차후 여기에 다른 스크립터블 오브젝트나 프리팹을 연결하는 코드를 추가하시면 됩니다!
                // 예: card.spawnEffect = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Effects/{imageNumber}.prefab");
            }
        }

        // 전체 저장 및 새로고침
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"리소스 연결 완료! 총 {count}개의 카드에 이미지가 할당되었습니다.");
    }
}
