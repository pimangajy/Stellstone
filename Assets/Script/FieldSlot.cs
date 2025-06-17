using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FieldSlot : MonoBehaviour
{
    // private으로 선언하여 외부에서 직접 수정하는 것을 막고,
    // [SerializeField]를 사용하여 인스펙터에서만 확인 가능하게 합니다.
    [SerializeField] private bool isOccupied = false;

    /// <summary>
    /// 이 슬롯에 카드를 놓을 수 있는지 여부를 반환합니다.
    /// </summary>
    public bool IsAvailable()
    {
        return !isOccupied;
    }

    /// <summary>
    /// 이 슬롯을 점유 상태로 변경합니다.
    /// </summary>
    public void OccupySlot()
    {
        isOccupied = true;

    }

    /// <summary>
    /// 이 슬롯을 빈 상태로 변경합니다. (카드가 파괴되거나 이동했을 때 사용)
    /// </summary>
    public void VacateSlot()
    {
        isOccupied = false;
    }
}
