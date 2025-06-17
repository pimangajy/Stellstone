using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MousePoint : MonoBehaviour
{
    void Update()
    {
        // 마우스 왼쪽 버튼을 클릭했을 때
        if (Input.GetMouseButtonDown(0))
        {
            // 카메라에서 마우스 위치로 쏘는 Ray(광선) 생성
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // RaycastHit 변수는 광선에 맞은 오브젝트의 정보를 담는 역할을 합니다.
            RaycastHit hit;

            // 실제로 Ray를 쏴서 어떤 Collider와 부딪혔는지 확인합니다.
            // Physics.Raycast가 true를 반환하면 무언가에 맞았다는 의미입니다.
            if (Physics.Raycast(ray, out hit))
            {
                // 광선에 맞은 오브젝트의 이름을 콘솔에 출력
                Debug.Log("클릭한 오브젝트: " + hit.collider.gameObject.name);

                // 여기서부터 원하는 로직을 추가할 수 있습니다.
                // 예: 카드를 클릭했다면 해당 카드 정보를 가져오거나, 선택 효과를 주는 등
            }
        }
    }
}
