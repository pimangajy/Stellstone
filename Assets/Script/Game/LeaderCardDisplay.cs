using DG.Tweening;
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// 인게임 배틀에서 [리더(Leader)] 오브젝트의 2D 스킨 스프라이트, 체력(HP), 조준 하이라이트 및 피격 데미지 연출을 전담하는 컴포넌트입니다.
/// </summary>
public class LeaderCardDisplay : GameCardDisplay
{
    [Header("리더 UI 요소 (3D 메쉬 및 2D 월드 오브젝트)")]
    public TextMeshPro HP;
    public MeshRenderer leaderMeshRenderer;
    public SpriteRenderer leaderSpriteRenderer;

    [Header("리더 하이라이트 (Glow/조준 연출)")]
    [Tooltip("조준 대상이 되었을 때 켜질 하이라이트 테두리/오브젝트")]
    public GameObject glowEffectObject;

    [Header("피격 데미지 UI")]
    public GameObject damageIMG;

    [Header("회복 힐 UI")]
    public GameObject healIMG;

    [Header("리더 패배 연출 데이터 (ScriptableObjects)")]
    [Tooltip("체력 0으로 파괴 시 사용할 기본 3D 패배 모션 데이터")]
    public LeaderDefeatData defaultDefeatData;

    [Tooltip("항복 시 사용할 3D 패배 모션 데이터")]
    public LeaderDefeatData surrenderDefeatData;

    [Tooltip("공격 유형별(속성, 위력 등) 3D 패배 모션 등록 목록")]
    public System.Collections.Generic.List<LeaderDefeatData> defeatMotionList = new System.Collections.Generic.List<LeaderDefeatData>();

    /// <summary>
    /// 현재 리더에 적용된 스킨 ID
    /// </summary>
    public string CurrentSkinId { get; private set; }

    /// <summary>
    /// 현재 리더에 적용된 SkinData 에셋 (독립형 스킨 데이터)
    /// </summary>
    public SkinData CurrentSkinData { get; private set; }

    /// <summary>
    /// 현재 리더의 직업
    /// </summary>
    public CardClass CurrentCardClass { get; private set; } = CardClass.Gangzi;

    private Coroutine _hitUICoroutine;
    private Coroutine _healUICoroutine;

    private void Awake()
    {
        if (glowEffectObject != null)
        {
            glowEffectObject.SetActive(false);
        }

        // 3D 메쉬 렌더러(Image) 자동 탐색
        if (leaderMeshRenderer == null)
        {
            var imgTrans = transform.Find("Image");
            if (imgTrans != null)
            {
                leaderMeshRenderer = imgTrans.GetComponent<MeshRenderer>();
            }
        }

        // 3D 메쉬(Image)가 활성화되어 있으면 불필요한 2D Square(ReaderIImage) 및 마스크 비활성화
        if (leaderMeshRenderer != null)
        {
            leaderMeshRenderer.gameObject.SetActive(true);

            var readerImg = transform.Find("ReaderIImage");
            if (readerImg != null) readerImg.gameObject.SetActive(false);

            var spriteMask = transform.Find("Sprite Mask");
            if (spriteMask != null) spriteMask.gameObject.SetActive(false);
        }

        if (healIMG != null) healIMG.SetActive(false);
    }

    /// <summary>
    /// 리더 UI 및 데이터를 갱신합니다. (3D MeshRenderer 및 2D SpriteRenderer 스킨 적용)
    /// </summary>
    public override void SetLeader(EntityData leaderData)
    {
        base.SetLeader(leaderData);

        if (leaderData == null) return;

        // 1. 내가 조작하는 리더인지 확인
        bool isMyLeader = (GameEntityManager.Instance != null && GameEntityManager.Instance.myLeader == this) ||
                          (GameClient.Instance != null && !string.IsNullOrEmpty(GameClient.Instance.UserUid) && leaderData.ownerUid == GameClient.Instance.UserUid);

        // 2. 장착할 스킨 ID 결정
        string skinId = null;

        // [내 리더인 경우] -> 계정 정보(CurrentUser.selectDeck)를 통해 현재 선택된 덱의 스킨 ID 조회
        if (isMyLeader && GameClient.Instance?.CurrentUser != null)
        {
            string selectedDeckId = GameClient.Instance.CurrentUser.selectDeck;
            if (!string.IsNullOrEmpty(selectedDeckId) && DeckSaveManager_Firebase.instance != null)
            {
                var myDeck = DeckSaveManager_Firebase.instance.GetAllDecks()?.FirstOrDefault(d => d.deckId == selectedDeckId);
                if (myDeck != null)
                {
                    skinId = myDeck.GetEquippedSkinId();
                }
            }
        }

        // [상대 리더이거나 계정 정보에서 못 찾은 경우] -> 서버 패킷의 leaderData.cardId를 스킨 ID로 사용
        if (string.IsNullOrEmpty(skinId))
        {
            skinId = leaderData.cardId;
        }

        // 3. 직업 문자열 판별 (기본값 Gangzi)
        string cardClass = "Gangzi";
        if (!string.IsNullOrEmpty(skinId))
        {
            if (skinId.IndexOf("Yuni", StringComparison.OrdinalIgnoreCase) >= 0) cardClass = "Yuni";
            else if (skinId.IndexOf("Huya", StringComparison.OrdinalIgnoreCase) >= 0) cardClass = "Huya";
            else if (skinId.IndexOf("Gangzi", StringComparison.OrdinalIgnoreCase) >= 0) cardClass = "Gangzi";
        }

        // 4. 독립형 SkinData 에셋 우선 로드
        CurrentSkinData = LoadSkinData(skinId, cardClass);
        if (CurrentSkinData != null)
        {
            CurrentSkinId = CurrentSkinData.skinId;
            CurrentCardClass = CurrentSkinData.targetClass;
        }
        else
        {
            CurrentSkinId = skinId;
            if (cardClass == "Yuni") CurrentCardClass = CardClass.Yuni;
            else if (cardClass == "Huya") CurrentCardClass = CardClass.Huya;
            else CurrentCardClass = CardClass.Gangzi;
        }

        // 5. 스킨 스프라이트 로드 (SkinData 우선, 없을 경우 기존 URL/경로 fallback)
        Sprite skinSprite = null;
        if (CurrentSkinData != null && CurrentSkinData.skinSprite != null)
        {
            skinSprite = CurrentSkinData.skinSprite;
        }
        else
        {
            string imageUrl = null;
            if (GameClient.Instance != null && GameClient.Instance.AllLeaderSkins != null && !string.IsNullOrEmpty(skinId))
            {
                var skinProduct = GameClient.Instance.AllLeaderSkins.FirstOrDefault(s => s.productId == skinId);
                if (skinProduct != null && !string.IsNullOrEmpty(skinProduct.image_url))
                {
                    imageUrl = skinProduct.image_url;
                }
            }

            if (string.IsNullOrEmpty(imageUrl))
            {
                imageUrl = $"Items/Skins/{cardClass}_Skin_0001.png";
            }

            skinSprite = LoadSkinSprite(imageUrl);
        }

        // 6. 내 리더인 경우: 조준선 및 감정표현에 스킨 적용
        if (isMyLeader)
        {
            // 스킨 조준선 적용 (미지정 시 기본 조준선 자동 폴백)
            if (TargetingReticle.Instance != null)
            {
                TargetingReticle.Instance.ApplySkin(CurrentSkinData);
            }

            // 스킨 감정표현 자동 장착
            if (EmotionManager.Instance != null && CurrentSkinData != null)
            {
                EmotionManager.Instance.ApplySkin(CurrentSkinData);
            }
        }

        // 5-1. 3D MeshRenderer(Image) 적용
        if (leaderMeshRenderer != null)
        {
            #if UNITY_EDITOR
            Material classMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Sprite/UI/DeckBuilding/ClassSelection/{cardClass.ToLower()}_reader.mat");
            if (classMat != null)
            {
                leaderMeshRenderer.sharedMaterial = classMat;
            }
            #endif

            if (skinSprite != null && skinSprite.texture != null)
            {
                leaderMeshRenderer.material.mainTexture = skinSprite.texture;
            }

            leaderMeshRenderer.gameObject.SetActive(true);
        }

        // 5-2. 2D SpriteRenderer 적용 (leaderMeshRenderer가 없거나 별도 필요 시)
        if (leaderSpriteRenderer != null)
        {
            if (skinSprite != null)
            {
                leaderSpriteRenderer.sprite = skinSprite;
            }

            if (leaderMeshRenderer != null)
            {
                leaderSpriteRenderer.gameObject.SetActive(false);
            }
        }

        // 6. 리더 체력 갱신
        if (HP != null)
        {
            HP.text = leaderData.health.ToString();
            HP.color = normalColor;
        }
    }

    /// <summary>
    /// 리더가 피해를 입거나 체력이 변경되었을 때 HP UI를 갱신합니다.
    /// </summary>
    public override void UpdateEntityStats(EntityData entityData)
    {
        base.UpdateEntityStats(entityData);

        if (entityData == null) return;

        if (HP != null)
        {
            HP.text = entityData.health.ToString();

            int maxHp = entityData.maxHealth > 0 ? entityData.maxHealth : 30;
            if (entityData.health < maxHp)
            {
                HP.color = debuffColor; // 피해 시 빨간색
            }
            else if (entityData.health > maxHp)
            {
                HP.color = buffColor;   // 추가 체력/버프 시 초록색
            }
            else
            {
                HP.color = normalColor; // 정상이면 흰색
            }
        }
    }

    /// <summary>
    /// 조준 타겟으로 지정되었을 때 하이라이트(Glow) 효과를 켜거나 끕니다.
    /// </summary>
    public override void SetGlowState(bool shouldGlow)
    {
        if (glowEffectObject != null)
        {
            glowEffectObject.SetActive(shouldGlow);
        }
    }

    /// <summary>
    /// 리더가 피해를 입었을 때 데미지 수치와 펀치 스케일 효과를 표시합니다.
    /// </summary>
    public override void DamageUI(int damage)
    {
        if (_hitUICoroutine != null) StopCoroutine(_hitUICoroutine);
        _hitUICoroutine = StartCoroutine(HitUI(damage));
    }

    private IEnumerator HitUI(int damage)
    {
        if (damageIMG != null)
        {
            damageIMG.SetActive(true);
            var textMesh = damageIMG.GetComponent<TextMeshPro>();
            if (textMesh != null) textMesh.text = damage.ToString();

            damageIMG.transform.DOKill();
            damageIMG.transform.localScale = Vector3.one;
            damageIMG.transform.DOPunchScale(Vector3.one * 0.35f, 0.25f, 6, 0.5f);

            yield return YieldInstructionCache.WaitForSeconds(0.7f);

            damageIMG.SetActive(false);
        }

        _hitUICoroutine = null;
    }

    /// <summary>
    /// 리더가 회복을 받았을 때 'HEAL' 텍스트와 펀치 스케일 효과를 표시합니다.
    /// </summary>
    public override void HealUI(int healAmount)
    {
        if (_healUICoroutine != null) StopCoroutine(_healUICoroutine);
        _healUICoroutine = StartCoroutine(HealUIRoutine(healAmount));
    }

    private IEnumerator HealUIRoutine(int healAmount)
    {
        if (healIMG != null)
        {
            healIMG.SetActive(true);
            var textMesh = healIMG.GetComponent<TextMeshPro>();
            if (textMesh == null) textMesh = healIMG.GetComponentInChildren<TextMeshPro>();
            if (textMesh != null)
            {
                textMesh.text = $"+{healAmount}";
                textMesh.color = new Color(0.2f, 0.95f, 0.2f, 1f);
            }

            var textMeshUGUI = healIMG.GetComponent<TextMeshProUGUI>();
            if (textMeshUGUI == null) textMeshUGUI = healIMG.GetComponentInChildren<TextMeshProUGUI>();
            if (textMeshUGUI != null)
            {
                textMeshUGUI.text = $"+{healAmount}";
                textMeshUGUI.color = new Color(0.2f, 0.95f, 0.2f, 1f);
            }

            healIMG.transform.DOKill();
            healIMG.transform.localScale = Vector3.one;
            healIMG.transform.DOPunchScale(Vector3.one * 0.35f, 0.25f, 6, 0.5f);

            yield return YieldInstructionCache.WaitForSeconds(0.7f);

            healIMG.SetActive(false);
        }

        _healUICoroutine = null;
    }

    /// <summary>
    /// [Sprite 레이어 1] 체력 0 / KO 패배 시 캐릭터 일러스트 연출 (적색 플래시, 충격 펀치, 다운)
    /// </summary>
    public void PlaySpriteDefeat()
    {
        if (leaderMeshRenderer != null)
        {
            var t = leaderMeshRenderer.transform;
            t.DOKill();
            t.DOPunchScale(Vector3.one * 0.25f, 0.4f, 6, 0.5f);

            if (leaderMeshRenderer.material != null)
            {
                leaderMeshRenderer.material.DOKill();
                // 적색 타격 플래시 후 어두워짐
                leaderMeshRenderer.material.DOColor(new Color(1f, 0.3f, 0.3f, 1f), 0.15f)
                    .OnComplete(() => leaderMeshRenderer.material.DOColor(new Color(0.35f, 0.35f, 0.35f, 1f), 0.7f));
            }
        }
        else if (leaderSpriteRenderer != null)
        {
            var t = leaderSpriteRenderer.transform;
            t.DOKill();
            t.DOPunchScale(Vector3.one * 0.25f, 0.4f, 6, 0.5f);
            leaderSpriteRenderer.DOKill();
            leaderSpriteRenderer.DOColor(new Color(0.35f, 0.35f, 0.35f, 1f), 0.7f);
        }
    }

    /// <summary>
    /// [Sprite 레이어 2] 항복 시 캐릭터 일러스트 연출 (고개 숙임, 서서히 어두워짐)
    /// </summary>
    public void PlaySpriteSurrender()
    {
        if (leaderMeshRenderer != null)
        {
            var t = leaderMeshRenderer.transform;
            t.DOKill();
            t.DOLocalMoveY(t.localPosition.y - 0.2f, 0.8f).SetEase(Ease.InOutSine);

            if (leaderMeshRenderer.material != null)
            {
                leaderMeshRenderer.material.DOKill();
                leaderMeshRenderer.material.DOColor(new Color(0.45f, 0.45f, 0.45f, 0.85f), 0.8f);
            }
        }
        else if (leaderSpriteRenderer != null)
        {
            var t = leaderSpriteRenderer.transform;
            t.DOKill();
            t.DOLocalMoveY(t.localPosition.y - 0.2f, 0.8f).SetEase(Ease.InOutSine);
            leaderSpriteRenderer.DOKill();
            leaderSpriteRenderer.DOColor(new Color(0.45f, 0.45f, 0.45f, 0.85f), 0.8f);
        }
    }

    /// <summary>
    /// Sprite 레이어(2개 고정)와 3D Object 레이어(LeaderDefeatData)를 결합하여 통합 패배 연출을 실행하고,
    /// 해당 모션에 설정된 결과창 지연 시간(resultPanelDelay)을 반환합니다.
    /// </summary>
    public float PlayDefeatSequence(string reason, LeaderDefeatData customData = null, string attackTypeKey = null)
    {
        bool isSurrender = (reason == "SURRENDER" || reason == "항복");

        // 1. Sprite 레이어 실행 (항복 vs 패배 2개 고정)
        if (isSurrender)
        {
            PlaySpriteSurrender();
        }
        else
        {
            PlaySpriteDefeat();
        }

        // 2. 3D Object 레이어 결정 (ScriptableObject)
        LeaderDefeatData targetMotion = customData;

        if (targetMotion == null)
        {
            if (isSurrender)
            {
                targetMotion = surrenderDefeatData ?? Resources.Load<LeaderDefeatData>("DefeatMotions/DefeatMotion_Surrender") ?? defaultDefeatData;
            }
            else if (!string.IsNullOrEmpty(attackTypeKey))
            {
                if (defeatMotionList != null && defeatMotionList.Count > 0)
                {
                    targetMotion = defeatMotionList.Find(d => d != null && d.defeatTypeKey.Equals(attackTypeKey, System.StringComparison.OrdinalIgnoreCase));
                }

                if (targetMotion == null)
                {
                    targetMotion = Resources.Load<LeaderDefeatData>($"DefeatMotions/DefeatMotion_{attackTypeKey}");
                }
            }
            
            if (targetMotion == null)
            {
                targetMotion = defaultDefeatData ?? Resources.Load<LeaderDefeatData>("DefeatMotions/DefeatMotion_Default");
            }
        }

        // 3. 3D Object 레이어 실행
        if (targetMotion != null)
        {
            targetMotion.Play3DMotion(this);
            return targetMotion.resultPanelDelay;
        }
        else
        {
            // Fallback 기본 스크립트 모션 (0.7초 진동)
            transform.DOKill();
            transform.DOShakePosition(0.7f, new Vector3(0.4f, 0, 0.4f), 18, 90f);
            if (damageIMG != null) damageIMG.SetActive(false);
            if (healIMG != null) healIMG.SetActive(false);
            return 1.2f;
        }
    }

    /// <summary>
    /// 기존 호환용 단발성 패배 연출 (PlayDefeatSequence로 위임)
    /// </summary>
    public void PlayDefeatEffect()
    {
        PlayDefeatSequence("LEADER_KILLED");
    }

    /// <summary>
    /// 스킨 이미지 경로로부터 스프라이트를 로드합니다.
    /// </summary>
    private Sprite LoadSkinSprite(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return null;

        // 1. Resources 로드 시도 (확장자 및 기본 접두사 제거)
        string cleanPath = imagePath.Replace('\\', '/').TrimStart('/');
        if (cleanPath.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
            cleanPath = cleanPath.Substring("Assets/Resources/".Length);

        string resPath = cleanPath;
        int dotIndex = resPath.LastIndexOf('.');
        if (dotIndex > 0) resPath = resPath.Substring(0, dotIndex);

        Sprite loadedSprite = Resources.Load<Sprite>(resPath);
        if (loadedSprite != null) return loadedSprite;

        // 1-2. Items/ 누락 또는 중복 시도
        if (!resPath.StartsWith("Items/", StringComparison.OrdinalIgnoreCase))
        {
            loadedSprite = Resources.Load<Sprite>("Items/" + resPath);
            if (loadedSprite != null) return loadedSprite;
        }

        // 2. Texture2D fallback 시도
        Texture2D tex = Resources.Load<Texture2D>(resPath);
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        #if UNITY_EDITOR
        Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
        if (editorSprite != null) return editorSprite;
        #endif

        Debug.LogWarning($"[LeaderCardDisplay] 리더 스킨 스프라이트 로드 실패: {imagePath}");
        return null;
    }

    /// <summary>
    /// 스킨 ID 또는 직업을 기반으로 Resources/Skins에서 해당하는 SkinData 에셋을 로드합니다.
    /// fallbackToDefault: true일 경우 일치하는 스킨이 없을 때 해당 직업의 0001 기본 스킨을 대체 반환합니다. (인게임 전투용)
    /// false일 경우 일치하는 스킨이 없으면 기본 스킨으로 대체하지 않고 null을 반환합니다. (상점 상품 검증용)
    /// </summary>
    public static SkinData LoadSkinData(string skinId, string cardClass, bool fallbackToDefault = true)
    {
        var allSkins = Resources.LoadAll<SkinData>("Skins");
        if (allSkins == null || allSkins.Length == 0) return null;

        // 1. skinId가 유효하게 전달된 경우
        if (!string.IsNullOrEmpty(skinId))
        {
            // 1-1. skinId 정확 일치 검색 (예: Gangzi_Skin_0001, Skin_Gangzi_0001)
            var exactMatch = Array.Find(allSkins, s => s != null && (
                string.Equals(s.skinId, skinId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.name, skinId, StringComparison.OrdinalIgnoreCase)
            ));
            if (exactMatch != null) return exactMatch;

            // 1-2. Skin_ 접두사 차이 검색
            string altId = skinId.StartsWith("Skin_", StringComparison.OrdinalIgnoreCase)
                ? skinId.Substring(5)
                : "Skin_" + skinId;
            var altMatch = Array.Find(allSkins, s => s != null && (
                string.Equals(s.skinId, altId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.name, altId, StringComparison.OrdinalIgnoreCase)
            ));
            if (altMatch != null) return altMatch;

            // fallbackToDefault가 false인 경우, 일치하는 스킨을 찾지 못했으면 기본 스킨으로 대체하지 않고 즉시 null 반환
            if (!fallbackToDefault)
            {
                return null;
            }

            // skinId가 명시적으로 전달되었으나 일치하는 스킨이 없고,
            // skinId가 스킨 ID 패턴이 아닌 경우(예: CardPack_, Item_ 등) 스킨이 아니므로 null 반환
            if (!skinId.Contains("Skin", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        // 2. 직업 기본 스킨 검색 (skinId가 없거나, fallbackToDefault가 true일 때 리소스 누락으로 기본 스킨을 요구할 때)
        if (fallbackToDefault && !string.IsNullOrEmpty(cardClass))
        {
            var classDefault = Array.Find(allSkins, s => s != null && 
                s.targetClass.ToString().Equals(cardClass, StringComparison.OrdinalIgnoreCase) &&
                (s.skinId.EndsWith("0001") || s.skinId.EndsWith("_Default")));
            if (classDefault != null) return classDefault;
        }

        return null;
    }
}
