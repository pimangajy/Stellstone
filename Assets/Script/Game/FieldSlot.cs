using UnityEngine;

/// <summary>
/// 하수인이 놓일 수 있는 '자리(Slot)' 하나를 나타냅니다.
/// </summary>
public class FieldSlot : MonoBehaviour
{
    [Tooltip("몇 번째 자리인가요? (0~6)")]
    public int slotIndex;

    [Tooltip("누군가 이 자리에 있나요?")]
    public bool IsOccupied = false;

    // (선택) 현재 이 자리에 있는 카드 정보
    public CardData cardData;
}