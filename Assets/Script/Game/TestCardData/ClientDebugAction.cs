using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ClientDebugAction : MonoBehaviour
{
    public static ClientDebugAction Instance { get; private set; }

    [Header("디버그 변수")]
    public Transform deckInfoList;
    public GameObject infoPanel;

    private List<CardInfo> deckList = new List<CardInfo>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SpecificCardDraw()
    {

    }

    public void DeckInfoRequest()
    {
        // 내 덱 정보 요청
        C_DebugRequestDeckInfo action = new C_DebugRequestDeckInfo
        {
            debugAction = DebugAction.RequestDeckInfo, 
            isOpponent = false
        };
        GameClient.Instance.SendDebugMessageAsync(action);
    }

    public void OpponentDeckInfoRequest()
    {
        // 상대방 덱 정보 요청
        C_DebugRequestDeckInfo action = new C_DebugRequestDeckInfo
        {
            debugAction = DebugAction.RequestDeckInfo, 
            isOpponent = true
        };
        GameClient.Instance.SendDebugMessageAsync(action);
    }

    public void ASDF()
    {
        List<CardInfo> infoList = new List<CardInfo>();

        for(int i = 0; i < 10; i++)
        {
            CardInfo cardInfo = new CardInfo
            {
                cardId = Random.Range(0, 1000).ToString(),

            };
            infoList.Add(cardInfo);
        }
        DebugDeckinfo(infoList);
    }

    // 서버에서 덱의 정보를 받아 리스트 생성
    public void DebugDeckinfo(List<CardInfo> infoList, bool isOpponentDeck = false)
    {
        deckList = infoList;

        foreach (Transform child in deckInfoList)
        {
            Destroy(child.gameObject);
        }

        foreach (CardInfo info in infoList)
        {
            GameObject newObj = Instantiate(infoPanel, deckInfoList);
            newObj.GetComponent<SpecificCardDraw>().DeckInfo(info, isOpponentDeck);
        }
    }
}
