using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("카드 생성 관련")]
    [Tooltip("복제해서 생성할 카드 프리팹입니다.")]
    public GameObject cardPrefab; // 인스펙터에서 카드 프리팹을 연결해 주세요.

    [Tooltip("카드가 추가될 플레이어의 손(Hand) 패널입니다.")]
    public Transform playerHandTransform; // 인스펙터에서 핸드 패널을 연결해 주세요.

    private HandManager handManager; // 핸드 정렬을 위한 스크립트 참조

    void Start()
    {
        // playerHandTransform에 HandArranger 스크립트가 있는지 확인하고 가져옵니다.
        if (playerHandTransform != null)
        {
            handManager = playerHandTransform.GetComponent<HandManager>();
        }
    }

    // 이 함수를 UI 버튼의 OnClick 이벤트에 연결할 것입니다.
    public void DrawCardToHand()
    {
        // 필요한 프리팹이나 핸드 정보가 없으면 에러 메시지를 출력하고 함수를 종료합니다.
        if (cardPrefab == null)
        {
            Debug.LogError("Card Prefab이 지정되지 않았습니다!");
            return;
        }
        if (playerHandTransform == null)
        {
            Debug.LogError("Player Hand Transform이 지정되지 않았습니다!");
            return;
        }

        // 1. 카드 프리팹을 복제하여 생성하고, playerHandTransform의 자식으로 만듭니다.
        GameObject newCard = Instantiate(cardPrefab, playerHandTransform);
        // 램덤 색 부여
        newCard.GetComponent<Image>().color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
        newCard.GetComponent<CardInHandController>().Initialize();
        Debug.Log(newCard.name + " 카드를 핸드에 추가했습니다.");

        // 2. 카드가 추가되었으므로, 핸드 정렬 스크립트를 호출하여 카드 위치를 다시 정렬합니다.
        if (handManager != null)
        {
            handManager.ArrangeCards();
        }
    }
}
