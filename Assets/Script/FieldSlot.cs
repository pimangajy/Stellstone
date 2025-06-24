using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FieldSlot : MonoBehaviour
{
    [SerializeField] private FieldCardController occupiedCard = null;

    /// <summary>
    /// 이 슬롯에 카드를 놓을 수 있는지 여부를 반환합니다.
    /// </summary>
    public bool IsAvailable()
    {
        return occupiedCard == null;
    }

    /// <summary>
    /// 이 슬롯을 점유 상태로 변경하고, 어떤 카드가 차지했는지 기록합니다.
    /// </summary>
    public void OccupySlot(FieldCardController card)
    {
        occupiedCard = card;
    }

    /// <summary>
    /// 이 슬롯을 빈 상태로 변경합니다. (카드가 파괴되거나 이동했을 때 사용)
    /// </summary>
    public void VacateSlot()
    {
        occupiedCard = null;
    }

    /// <summary>
    /// 이 슬롯에 있는 카드의 컨트롤러를 반환합니다. 카드가 없으면 null을 반환합니다.
    /// </summary>
    public FieldCardController GetOccupiedCard()
    {
        return occupiedCard;
    }
}
