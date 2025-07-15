using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class BackgroundClickHandler : MonoBehaviour, IPointerClickHandler
{

    public void OnPointerClick(PointerEventData eventData)
    {
        // 이 오브젝트(배경)가 클릭되었다면, HandManager에게 핸드를 접으라고 요청합니다.
        HandManager.Instance.ToggleHandExpansion(false);
    }
}
