using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FilterToggle : MonoBehaviour
{
    // 예시: 나중에 텍스트 대신 Enum이나 ID로 값을 관리하고 싶을 때 사용합니다.
    // public CardType cardType;
    // public Rarity rarity;

    [Tooltip("이 토글이 나타내는 필터 값입니다. (예: '하수인', '전설')")]
    public string filterValue;

    // 필요에 따라 초기화 코드나 헬퍼 함수를 추가할 수 있습니다.
    void Start()
    {
        if (string.IsNullOrEmpty(filterValue))
        {
            // filterValue가 비어있으면 토글의 자식 Text에서 자동으로 값을 가져오도록 설정할 수 있습니다.
            // filterValue = GetComponentInChildren<UnityEngine.UI.Text>()?.text;
        }
    }
}
