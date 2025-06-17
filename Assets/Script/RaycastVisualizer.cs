using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaycastVisualizer : MonoBehaviour
{
    // 광선의 최대 길이를 설정합니다.
    public float rayLength = 100f;

    void Update()
    {
        // 메인 카메라에서 현재 마우스 위치로 향하는 Ray(광선)를 생성합니다.
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Scene 뷰에 광선을 시각적으로 그립니다.
        // ray.origin: 광선의 시작점 (카메라 위치)
        // ray.direction * rayLength: 광선의 방향과 길이
        // Color.red: 광선의 색상
        Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.red);
    }
}
