using UnityEngine;
using TMPro;
using System.Collections.Generic; // 마나 UI 텍스트를 업데이트하기 위해 필요합니다.

/// <summary>
/// 플레이어의 마나를 관리하는 스크립트입니다. (최대치, 현재량, 회복, 소모)
/// </summary>
public class PlayerManaManager : MonoBehaviour
{
    #region Singleton
    public static PlayerManaManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }
    #endregion

    [Header("마나 설정")]
    [Tooltip("최대 마나의 한도입니다.")]
    public int maxManaCap = 10;

    [Header("현재 상태")]
    [SerializeField] private int currentMaxMana = 0; // 현재 턴의 최대 마나
    [SerializeField] private int currentMana = 0;    // 현재 보유 마나

    // ★★★ 핵심 수정: Text 대신 GameObject 리스트를 사용합니다. ★★★
    [Header("UI 연결")]
    [Tooltip("마나 슬롯 게임 오브젝트들을 순서대로 10개 연결해주세요.")]
    public List<GameObject> manaSlots;

    /// <summary>
    /// 게임 시작 시 마나를 초기화합니다.
    /// </summary>
    void Start()
    {
        currentMaxMana = 1;
        currentMana = 1;
        UpdateManaUI();
    }

    /// <summary>
    /// TurnManager의 OnPlayerTurnStart 이벤트에 연결될 함수입니다.
    /// </summary>
    public void OnTurnStart()
    {
        if (currentMaxMana < maxManaCap)
        {
            currentMaxMana++;
        }
        currentMana = currentMaxMana;
        UpdateManaUI();
    }

    /// <summary>
    /// 특정 양의 마나를 소모할 수 있는지 확인합니다.
    /// </summary>
    public bool CanSpendMana(int cost)
    {
        return currentMana >= cost;
    }

    /// <summary>
    /// 특정 양의 마나를 소모합니다.
    /// </summary>
    public void SpendMana(int cost)
    {
        currentMana -= cost;
        if (currentMana < 0) currentMana = 0;
        UpdateManaUI();
    }

    /// <summary>
    /// ★★★ 핵심 수정: 마나 이미지 UI를 현재 상태에 맞게 업데이트합니다. ★★★
    /// </summary>
    private void UpdateManaUI()
    {
        // 연결된 모든 마나 슬롯을 순회합니다.
        for (int i = 0; i < manaSlots.Count; i++)
        {
            // i가 현재 최대 마나보다 작으면, 해당 슬롯을 활성화합니다. (연한 푸른색 배경 보이기)
            if (i < currentMaxMana)
            {
                manaSlots[i].SetActive(true);

                // 슬롯의 자식인 크리스탈 오브젝트를 찾습니다.
                Transform crystal = manaSlots[i].transform.GetChild(0);
                if (crystal != null)
                {
                    // i가 현재 보유 마나보다 작으면, 크리스탈을 활성화합니다. (진한 푸른색 보석 보이기)
                    crystal.gameObject.SetActive(i < currentMana);
                }
            }
            // i가 현재 최대 마나보다 크거나 같으면, 슬롯 자체를 비활성화합니다.
            else
            {
                manaSlots[i].SetActive(false);
            }
        }
    }
}
