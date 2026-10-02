using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 필드에 소환된 멤버 카드를 클릭했을 때 열리는 스킬 선택창(MemberSkillSelect) UI 컨트롤러입니다.
/// 좌측에는 멤버 카드의 현재 상태를 표시하고, 우측에는 2~4개의 스킬 목록을 동적으로 생성하여 표시합니다.
/// </summary>
public class MemberSkillSelectUI : MonoBehaviour
{
    private static MemberSkillSelectUI _instance;
    public static MemberSkillSelectUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<MemberSkillSelectUI>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    // 씬 내 MemberSkillSelect 오브젝트 탐색
                    GameObject obj = GameObject.Find("MemberSkillSelect");
                    if (obj == null)
                    {
                        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                        foreach (var c in canvases)
                        {
                            Transform t = c.transform.Find("MemberSkillSelect");
                            if (t != null) { obj = t.gameObject; break; }
                        }
                    }
                    if (obj != null)
                    {
                        _instance = obj.AddComponent<MemberSkillSelectUI>();
                    }
                }
            }
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }

    [Header("UI 바인딩")]
    [Tooltip("전체 팝업 루트 GameObject (MemberSkillSelect)")]
    public GameObject rootPanel;

    [Tooltip("좌측 멤버 카드 표시용 HandCardDisplay")]
    public HandCardDisplay memberCardDisplay;

    [Tooltip("우측 스킬 아이템들이 들어갈 부모 컨테이너 (VerticalLayoutGroup)")]
    public Transform skillListContainer;

    [Tooltip("스킬 항목 프리팹 (SkillPanel)")]
    public GameObject skillItemPrefab;

    [Tooltip("배경 클릭 시 팝업을 닫기 위한 버튼 (미할당 시 자동 생성)")]
    public Button backgroundCloseButton;

    /// <summary>
    /// 현재 스킬창이 활성화되어 열려있는지 여부
    /// </summary>
    public bool IsOpen => rootPanel != null && rootPanel.activeSelf;

    private GameCardDisplay _currentMemberEntity;
    private readonly List<SkillPanelItemUI> _spawnedSkillItems = new List<SkillPanelItemUI>();
    private GameObject _templateItem;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (rootPanel == null)
        {
            rootPanel = gameObject;
        }

        // 좌측 HandCard 자동 바인딩
        if (memberCardDisplay == null)
        {
            Transform cardTransform = transform.Find("HandCard");
            if (cardTransform != null)
            {
                memberCardDisplay = cardTransform.GetComponent<HandCardDisplay>();
            }
        }

        // 우측 Panel 자동 바인딩
        if (skillListContainer == null)
        {
            skillListContainer = transform.Find("Panel");
        }

        // 프리팹 미할당 시 우측 Panel의 첫 자식 오브젝트를 템플릿으로 사용
        if (skillItemPrefab == null && skillListContainer != null && skillListContainer.childCount > 0)
        {
            _templateItem = skillListContainer.GetChild(0).gameObject;
            _templateItem.SetActive(false);
            skillItemPrefab = _templateItem;
        }

        // 배경 클릭 시 팝업 닫기 기능 바인딩
        if (backgroundCloseButton == null && rootPanel != null)
        {
            backgroundCloseButton = rootPanel.GetComponent<Button>();
            if (backgroundCloseButton == null)
            {
                backgroundCloseButton = rootPanel.AddComponent<Button>();
                backgroundCloseButton.transition = Selectable.Transition.None;
            }
        }

        if (backgroundCloseButton != null)
        {
            backgroundCloseButton.onClick.RemoveAllListeners();
            backgroundCloseButton.onClick.AddListener(Close);
        }
    }

    private void OnEnable()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPhaseStartEvent -= OnPhaseStart;
            GameClient.Instance.OnPhaseStartEvent += OnPhaseStart;
        }
    }

    private void OnDisable()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnPhaseStartEvent -= OnPhaseStart;
        }
    }

    private void OnPhaseStart(S_PhaseStart packet)
    {
        if (packet != null && packet.phase == GamePhase.STANDBY)
        {
            Close();
        }
    }

    private void Start()
    {
        // 게임 씬 시작 시 스킬창 닫기
        Close();
    }

    private void Update()
    {
        // 팝업이 열려 있을 때 ESC 또는 우클릭 시 닫기
        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                Close();
            }
        }
    }

    /// <summary>
    /// 필드의 멤버 카드를 클릭했을 때 스킬 선택 팝업을 엽니다.
    /// </summary>
    public void Open(GameCardDisplay memberEntity)
    {
        if (memberEntity == null || memberEntity._cardData == null) return;

        _currentMemberEntity = memberEntity;
        CardData cardData = memberEntity._cardData;

        // 1. 좌측 멤버 카드 뷰 갱신
        if (memberCardDisplay != null)
        {
            memberCardDisplay.Setup(cardData, null);

            // 필드의 현재 체력 반영
            if (memberEntity.CurrentEntityData != null && memberCardDisplay.healthText != null)
            {
                int curHp = memberEntity.CurrentEntityData.health;
                int maxHp = cardData.health;
                memberCardDisplay.healthText.text = curHp.ToString();

                if (curHp < maxHp) memberCardDisplay.healthText.color = memberCardDisplay.debuffColor;
                else if (curHp > maxHp) memberCardDisplay.healthText.color = memberCardDisplay.buffColor;
                else memberCardDisplay.healthText.color = memberCardDisplay.normalColor;
            }
        }

        // 2. 우측 스킬 목록 컨테이너 초기화
        foreach (var item in _spawnedSkillItems)
        {
            if (item != null && item.gameObject != _templateItem)
            {
                Destroy(item.gameObject);
            }
        }
        _spawnedSkillItems.Clear();

        int currentHealth = (memberEntity.CurrentEntityData != null) ? memberEntity.CurrentEntityData.health : cardData.health;
        bool hasUsedSkillThisTurn = (memberEntity.CurrentEntityData != null) && memberEntity.CurrentEntityData.hasUsedSkillThisTurn;

        // 3. 멤버 스킬 목록 생성 (2~4개)
        if (cardData.memberSkills != null && cardData.memberSkills.Count > 0 && skillItemPrefab != null && skillListContainer != null)
        {
            foreach (var skill in cardData.memberSkills)
            {
                GameObject newObj = Instantiate(skillItemPrefab, skillListContainer);
                newObj.SetActive(true);

                SkillPanelItemUI itemUI = newObj.GetComponent<SkillPanelItemUI>();
                if (itemUI == null)
                {
                    itemUI = newObj.AddComponent<SkillPanelItemUI>();
                }

                itemUI.Setup(skill, currentHealth, hasUsedSkillThisTurn, OnSkillSelected);
                _spawnedSkillItems.Add(itemUI);
            }
        }

        if (rootPanel != null)
        {
            rootPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 스킬 선택 팝업을 닫습니다.
    /// </summary>
    public void Close()
    {
        if (rootPanel != null)
        {
            rootPanel.SetActive(false);
        }
        _currentMemberEntity = null;
    }

    /// <summary>
    /// 플레이어가 특정 스킬 버튼을 클릭했을 때 호출됩니다.
    /// </summary>
    private void OnSkillSelected(MemberSkillData skill)
    {
        if (_currentMemberEntity == null || skill == null) return;

        var entity = _currentMemberEntity;
        int entityId = entity.CurrentEntityData != null ? entity.CurrentEntityData.entityId : entity.EntityId;

        // 팝업 닫기
        Close();

        // 타겟팅 여부에 따른 분기
        if (skill.targeting)
        {
            if (GameInputManager.Instance != null)
            {
                GameInputManager.Instance.StartMemberSkillTargeting(entityId, skill, entity.transform);
            }
        }
        else
        {
            Debug.Log($"[MemberSkillSelectUI] 논타겟 멤버 스킬 사용: EntityId={entityId}, SkillId={skill.skillId}");
            if (entity.CurrentEntityData != null)
            {
                entity.CurrentEntityData.hasUsedSkillThisTurn = true;
            }
            GameClient.Instance?.SendUseMemberSkill(entityId, skill.skillId, 0);
        }
    }
}
