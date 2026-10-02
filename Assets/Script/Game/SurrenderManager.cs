using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SurrenderManager : MonoBehaviour
{
    [Header("Buttons & Panels")]
    public Button surrenderbtn;
    public UIPanelToggler resultsPanel;
    public TextMeshProUGUI resultsText;

    [Header("Results Panel Rewards UI")]
    public TextMeshProUGUI goldValueText;
    public TextMeshProUGUI goldPlusText;
    public TextMeshProUGUI expValueText;
    public TextMeshProUGUI expPlusText;

    [Header("Default Rewards (Fallback)")]
    public int winDefaultGold = 100;
    public int winDefaultExp = 100;
    public int lossDefaultGold = 50;
    public int lossDefaultExp = 50;

    [Header("Animation Settings")]
    public float countDuration = 1.2f;

    private Coroutine _resultsRoutine;

    private void Awake()
    {
        // UI 컴포넌트 자동 탐색 (인스펙터 미할당 시 대비)
        AutoBindUI();
    }

    private void Start()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnGameOverEvent += Results_Window;
        }

        if (surrenderbtn != null)
        {
            surrenderbtn.onClick.AddListener(Surrender);
        }
    }

    private void OnDestroy()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.OnGameOverEvent -= Results_Window;
        }
    }

    private void AutoBindUI()
    {
        if (resultsPanel == null)
        {
            var panelObj = GameObject.Find("Results Panel");
            if (panelObj != null) resultsPanel = panelObj.GetComponent<UIPanelToggler>();
        }

        if (resultsPanel != null)
        {
            if (resultsText == null)
            {
                var t = resultsPanel.transform.Find("Results/ResultsText");
                if (t != null) resultsText = t.GetComponent<TextMeshProUGUI>();
            }
            if (goldValueText == null)
            {
                var t = resultsPanel.transform.Find("Results/Gold/value");
                if (t != null) goldValueText = t.GetComponent<TextMeshProUGUI>();
            }
            if (goldPlusText == null)
            {
                var t = resultsPanel.transform.Find("Results/Gold/plus");
                if (t != null) goldPlusText = t.GetComponent<TextMeshProUGUI>();
            }
            if (expValueText == null)
            {
                var t = resultsPanel.transform.Find("Results/Exp/value");
                if (t != null) expValueText = t.GetComponent<TextMeshProUGUI>();
            }
            if (expPlusText == null)
            {
                var t = resultsPanel.transform.Find("Results/Exp/plus");
                if (t != null) expPlusText = t.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    public void Surrender()
    {
        if (GameClient.Instance != null)
        {
            GameClient.Instance.SendConcedeRequest();
        }
    }

    public void Results_Window(S_GameOver over)
    {
        if (_resultsRoutine != null) StopCoroutine(_resultsRoutine);
        _resultsRoutine = StartCoroutine(ShowResultsRoutine(over));
    }

    private IEnumerator ShowResultsRoutine(S_GameOver over)
    {
        Debug.Log($"[SurrenderManager] 🏁 게임 종료 처리 시작 (Winner: {over.winnerUid}, Reason: {over.reason})");

        // 1. 공격/스킬 패킷이 다음 프레임에 큐에 들어올 수 있으므로 잠시 대기
        yield return null;

        // 2. GameEntityManager의 모든 액션(하수인 공격 투사체, 데미지 계산, 카메라 흔들림 등)이 완료될 때까지 대기
        if (GameEntityManager.Instance != null)
        {
            while (GameEntityManager.Instance.IsProcessingQueue)
            {
                yield return null;
            }
        }

        // 2-1. 투사체 적중 및 데미지 표기 연출이 안정적으로 끝난 후 패배 모션으로 넘어가도록 짧은 호흡 대기
        yield return YieldInstructionCache.WaitForSeconds(0.15f);

        // 3. 승리 / 무승부 / 패배 판정
        bool isDraw = (over.winnerUid == "DRAW");
        bool isWin = false;
        string myUid = GameClient.Instance?.UserUid;
        if (!string.IsNullOrEmpty(myUid))
        {
            isWin = (over.winnerUid == myUid);
        }
        else if (GameEntityManager.Instance != null)
        {
            isWin = (over.winnerUid == GameEntityManager.Instance.MyUid);
        }

        // 4. 패배한 리더에 피격/패배 흔들림 연출 (무승부가 아닐 때)
        LeaderCardDisplay losingLeader = null;
        if (GameEntityManager.Instance != null && !isDraw)
        {
            losingLeader = isWin ? GameEntityManager.Instance.opponentLeader : GameEntityManager.Instance.myLeader;
        }

        float defeatDelay = 1.2f;
        if (losingLeader != null)
        {
            string attackKey = GameEntityManager.Instance?.LastKillingAttackTypeKey;
            LeaderDefeatData killingData = GameEntityManager.Instance?.LastKillingDefeatData;

            // CardVFXData에 등록된 전용 모션(killingData)이 있으면 최우선 실행, 없으면 attackKey/기본 모션으로 실행!
            defeatDelay = losingLeader.PlayDefeatSequence(over.reason, killingData, attackKey);
        }

        // 패배/승리 체감 및 LeaderDefeatData에 설정된 결과창 등장 지연 대기
        yield return YieldInstructionCache.WaitForSeconds(defeatDelay);

        // 5. 결과창 텍스트 설정
        if (resultsText != null)
        {
            if (isDraw)
            {
                resultsText.text = "무승부";
                resultsText.color = new Color(0.85f, 0.85f, 0.85f);
            }
            else
            {
                resultsText.text = isWin ? "승리" : "패배";
                resultsText.color = isWin ? new Color(1f, 0.85f, 0.2f) : new Color(0.9f, 0.25f, 0.25f);
            }
        }

        // 6. 보상 수치 및 UI 초기 세팅
        int earnedGold = over.earnedGold;
        int earnedExp = over.earnedExp;

        // 서버에서 보상 필드를 안 보냈거나 0인 경우 기본값 사용
        if (earnedGold <= 0) earnedGold = isWin ? winDefaultGold : (isDraw ? (winDefaultGold + lossDefaultGold) / 2 : lossDefaultGold);
        if (earnedExp <= 0) earnedExp = isWin ? winDefaultExp : (isDraw ? (winDefaultExp + lossDefaultExp) / 2 : lossDefaultExp);

        // 목표 수치: 서버가 currentGold / currentExp를 보냈다면 우선 적용
        int targetGold = (over.currentGold > 0) ? over.currentGold : ((GameClient.Instance?.CurrentUser?.gold ?? SinginManager.CurrentUserData?.gold ?? 0) + earnedGold);
        int initialGold = (GameClient.Instance?.CurrentUser != null && GameClient.Instance.CurrentUser.gold > 0)
                          ? GameClient.Instance.CurrentUser.gold 
                          : Mathf.Max(0, targetGold - earnedGold);

        int targetExp = (over.currentExp > 0 || over.maxExp > 0) ? over.currentExp : ((GameClient.Instance?.CurrentUser?.exp ?? SinginManager.CurrentUserData?.exp ?? 0) + earnedExp);
        int initialExp;
        if (over.isLevelUp)
        {
            initialExp = 0; // 레벨업 시 0에서부터 차오르는 시각적 연출
        }
        else
        {
            initialExp = (GameClient.Instance?.CurrentUser != null && GameClient.Instance.CurrentUser.exp > 0)
                         ? GameClient.Instance.CurrentUser.exp
                         : Mathf.Max(0, targetExp - earnedExp);
        }

        if (goldValueText != null) goldValueText.text = initialGold.ToString("N0");
        if (expValueText != null) expValueText.text = initialExp.ToString("N0");

        if (goldPlusText != null)
        {
            goldPlusText.text = $"+ {earnedGold:N0}";
            goldPlusText.gameObject.SetActive(false);
            goldPlusText.alpha = 1f;
        }
        if (expPlusText != null)
        {
            expPlusText.text = $"+ {earnedExp:N0}";
            expPlusText.gameObject.SetActive(false);
            expPlusText.alpha = 1f;
        }

        // 7. 결과 패널 오픈
        if (resultsPanel != null)
        {
            Debug.Log("[SurrenderManager] 🏆 결과창(Results Panel) 오픈!");
            resultsPanel.ShowPanel();
        }

        // 8. 골드 및 경험치 카운팅 애니메이션 실행
        yield return StartCoroutine(PlayRewardsAnimationRoutine(initialGold, targetGold, earnedGold, initialExp, targetExp, earnedExp, over));

        _resultsRoutine = null;
    }

    private IEnumerator PlayRewardsAnimationRoutine(int initialGold, int targetGold, int earnedGold, 
                                                    int initialExp, int targetExp, int earnedExp, 
                                                    S_GameOver over)
    {
        // 결과창 패널 등장 애니메이션 대기
        float panelOpenTime = resultsPanel != null ? resultsPanel.animationDuration + 0.15f : 0.45f;
        yield return YieldInstructionCache.WaitForSeconds(panelOpenTime);

        // 1. + 획득량 텍스트 팝업 (통통 튀는 OutBack 효과)
        if (goldPlusText != null)
        {
            goldPlusText.gameObject.SetActive(true);
            goldPlusText.transform.localScale = Vector3.zero;
            goldPlusText.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }
        if (expPlusText != null)
        {
            expPlusText.gameObject.SetActive(true);
            expPlusText.transform.localScale = Vector3.zero;
            expPlusText.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }

        yield return YieldInstructionCache.WaitForSeconds(0.4f);

        // 2. 기존 수치에서 획득량을 더하는 롤링 카운트업 애니메이션
        int displayGold = initialGold;
        int displayExp = initialExp;

        Sequence countSeq = DOTween.Sequence();
        if (goldValueText != null)
        {
            countSeq.Join(DOTween.To(() => displayGold, x =>
            {
                displayGold = x;
                goldValueText.text = displayGold.ToString("N0");
            }, targetGold, countDuration).SetEase(Ease.OutQuad));
            goldValueText.transform.DOPunchScale(Vector3.one * 0.15f, countDuration, 5, 0.5f);
        }

        if (expValueText != null)
        {
            countSeq.Join(DOTween.To(() => displayExp, x =>
            {
                displayExp = x;
                expValueText.text = displayExp.ToString("N0");
            }, targetExp, countDuration).SetEase(Ease.OutQuad));
            expValueText.transform.DOPunchScale(Vector3.one * 0.15f, countDuration, 5, 0.5f);
        }

        yield return countSeq.WaitForCompletion();

        // 3. 카운팅 완료 후 plus 텍스트 축소 및 페이드아웃 (합산 흡수 연출)
        if (goldPlusText != null)
        {
            goldPlusText.transform.DOScale(Vector3.one * 0.6f, 0.4f);
            DOTween.To(() => goldPlusText.alpha, x => goldPlusText.alpha = x, 0f, 0.4f);
        }
        if (expPlusText != null)
        {
            expPlusText.transform.DOScale(Vector3.one * 0.6f, 0.4f);
            DOTween.To(() => expPlusText.alpha, x => expPlusText.alpha = x, 0f, 0.4f);
        }

        // 4. 로컬 유저 데이터 반영 (골드, 경험치, 레벨, 점수)
        if (GameClient.Instance?.CurrentUser != null)
        {
            GameClient.Instance.CurrentUser.gold = targetGold;
            GameClient.Instance.CurrentUser.exp = targetExp;
            if (over.currentLevel > 0) GameClient.Instance.CurrentUser.level = over.currentLevel;
            if (over.currentScore > 0) GameClient.Instance.CurrentUser.score = over.currentScore;
        }
        if (SinginManager.CurrentUserData != null)
        {
            SinginManager.CurrentUserData.gold = targetGold;
            SinginManager.CurrentUserData.exp = targetExp;
            if (over.currentLevel > 0) SinginManager.CurrentUserData.level = over.currentLevel;
            if (over.currentScore > 0) SinginManager.CurrentUserData.score = over.currentScore;
        }
        if (UserCurrencyDisplay.Instance != null && GameClient.Instance?.CurrentUser != null)
        {
            UserCurrencyDisplay.Instance.SetCurrency(targetGold, GameClient.Instance.CurrentUser.stellastone, GameClient.Instance.CurrentUser.stardust);
        }
    }

    public void SceneMove(string sceneName)
    {
        if (SceneLoader.instance != null)
        {
            SceneLoader.instance.LoadSceneByName(sceneName);
        }
    }
}
