using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class testui : MonoBehaviour
{
    private Vector2 _mouseDownPos;

    private List<RaycastResult> GetUIElementsUnderPointer()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        return results;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            _mouseDownPos = Input.mousePosition;

            // =======================================================
            // 1단계: UI (2D 캔버스 - 손패 카드) 우선 판정
            // =======================================================
            List<RaycastResult> uiHits = GetUIElementsUnderPointer();

            Debug.Log($"클릭된 UI 개수: {uiHits.Count}");
            if (uiHits.Count > 0) Debug.Log($"가장 위에 있는 UI: {uiHits[0].gameObject.name}");
        }
    }
}
