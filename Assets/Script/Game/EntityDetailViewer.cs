using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 카드의 상세 정보를 화면에 띄워주는 UI 관리자입니다.
/// 이미지, 이름, 설명, 종족, 버프 목록 등을 표시합니다.
/// </summary>
public class EntityDetailViewer : MonoBehaviour
{
    public static EntityDetailViewer Instance;

    [Header("UI 연결")]
    [Tooltip("상세 정보창 전체를 껐다 켤 부모 패널")]
    public GameObject viewerPanel;

    [Header("텍스트 및 이미지 연결")]
    public Image cardPortrait;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI tribeText;
    public TextMeshProUGUI buffListText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 시작할 때는 숨겨둡니다.
        HideDetail();
    }

    /// <summary>
    /// 특정 카드의 정보를 읽어와 UI에 채우고 창을 띄웁니다.
    /// </summary>
    public void ShowDetail(GameCardDisplay cardDisplay)
    {
        if (cardDisplay == null || cardDisplay._cardData == null) return;

        CardData data = cardDisplay._cardData;
        EntityData entity = cardDisplay.CurrentEntityData;

        // 1. 패널 켜기
        viewerPanel.SetActive(true);

        // 2. 이미지 & 기본 텍스트 설정
        if (cardPortrait != null)
            cardPortrait.sprite = data.memberIcon;

        if (nameText != null) nameText.text = data.cardName;
        if (descriptionText != null) descriptionText.text = data.description;

        // 3. 종족 텍스트 설정 (없음이 아닐 때만 표시)
        if (tribeText != null)
        {
            if (data.minionTribe != MinionTribe.없음)
                tribeText.text = $"종족: {data.minionTribe}";
            else
                tribeText.text = ""; // 종족 없음
        }

        // 4. 버프 목록 설정 (임시 구현)
        if (buffListText != null)
        {
            // TODO: 추후 EntityData에 List<Buff> 데이터가 생기면 루프를 돌며 출력합니다.
            // 현재는 공격력이 원본보다 높으면 버프를 받은 것으로 간주하여 임시 출력합니다.
            if (entity != null && entity.attack > data.attack)
            {
                buffListText.text = "<color=green>• 공격력 증가 버프</color>";
            }
            else
            {
                buffListText.text = "<color=#888888>적용된 효과 없음</color>";
            }
        }
    }

    /// <summary>
    /// 상세 정보 창을 닫습니다.
    /// </summary>
    public void HideDetail()
    {
        if (viewerPanel != null)
        {
            viewerPanel.SetActive(false);
        }
    }
}