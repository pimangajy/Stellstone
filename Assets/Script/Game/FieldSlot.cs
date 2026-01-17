using UnityEngine;

/// <summary>
/// 필드 슬롯 오브젝트에 부착되어 슬롯의 정보를 담습니다.
/// </summary>
public class FieldSlot : MonoBehaviour
{
    [Tooltip("슬롯의 고유 인덱스 (0부터 시작)")]
    public int slotIndex;
    public bool IsOccupied = false; 
    public CardData cardData;
}