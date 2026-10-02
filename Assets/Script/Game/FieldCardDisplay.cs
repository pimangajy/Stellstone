using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using AYellowpaper.SerializedCollections;

/// <summary>
/// 인게임 배틀에서 [필드에 소환된 하수인(Minion)]의 외형 및 전투 연출을 전담하는 컴포넌트입니다.
/// 3D 월드 텍스트, 애니메이션, 필드 레어도 테두리, 피격/부유/발광 연출, 키워드/트리거 프레임을 관리합니다.
/// </summary>
public class FieldCardDisplay : GameCardDisplay
{
    [Header("필드 하수인 애니메이션 및 텍스트")]
    public SpriteGifPlayer cardArtAnimator;
    public TextMeshPro entityNameText;

    [Header("필드 하수인 스탯 UI")]
    public TextMeshPro entityAttackText;
    public TextMeshPro entityHealthText;
    public GameObject minionAtkObject; // atk_img
    public GameObject minionHpObject;  // hp_img

    [Header("멤버 전용 스탯 UI")]
    public GameObject memberHpObject;    // member_hp_img
    public TextMeshPro memberHealthText; // member_hp_img 밑의 hp 텍스트

    [Header("필드 레어도별 테두리 (Border)")]
    [Tooltip("필드 하수인 테두리 SpriteRenderer 컴포넌트")]
    public SpriteRenderer fieldBorderRenderer;

    [Tooltip("레어도별 테두리 스프라이트 매핑 (Common, Rare, Epic, Legendary)")]
    [SerializedDictionary("레어도", "테두리 스프라이트")]
    public SerializedDictionary<CardRarity, Sprite> fieldRarityBorders = new SerializedDictionary<CardRarity, Sprite>();

    [Header("필드 하수인 3D 프레임 메쉬 및 레어도 머티리얼 (Unlit)")]
    [Tooltip("레어도 머티리얼이 적용될 3D 프레임 MeshRenderer 목록 (비워두면 Awake 시 자동 수집)")]
    public MeshRenderer[] frameMeshRenderers;

    [Tooltip("일반 (Common) 머티리얼 - 브론즈")]
    public Material commonMaterial;

    [Tooltip("레어 (Rare) 머티리얼 - 실버")]
    public Material rareMaterial;

    [Tooltip("희귀 (Epic) 머티리얼 - 골드")]
    public Material epicMaterial;

    [Tooltip("전설 (Legendary) 머티리얼 - 다이아")]
    public Material legendaryMaterial;

    [Header("키워드 연출 오브젝트 (인스펙터 매핑)")]
    [SerializedDictionary("키워드", "연출 오브젝트")]
    public SerializedDictionary<CardKeywords, GameObject> keywordObjects = new SerializedDictionary<CardKeywords, GameObject>();

    [Header("트리거(효과)별 상시 연출 오브젝트 매핑")]
    public SerializedDictionary<EffectTriggerType, GameObject> triggerObjects = new SerializedDictionary<EffectTriggerType, GameObject>();

    [Header("공격 상태 연출 오브젝트 (인스펙터 매핑)")]
    [Tooltip("소환된 턴에 공격 불가(수면/ZZZ) 상태일 때 활성화되는 오브젝트 (공격 완료 후나 속박 상태에서는 미표시)")]
    public GameObject cannotAttackObject;

    [Tooltip("현재 내 턴이고 실제로 공격 가능한 상태일 때 활성화되는 오브젝트 (예: 녹색 외곽선/하이라이트)")]
    public GameObject canAttackObject;


    // --- [필드 전투 연출 설정] ---
    [Header("연출 - 공격자 (Floating)")]
    [Tooltip("공격 시도 시 떠오를 높이")]
    public float floatHeight = 0.5f;
    public float floatDuration = 0.2f;

    [Header("연출 - 대상 (Glow)")]
    [Tooltip("조준당할 때 켜질 하이라이트 오브젝트 (테두리 이미지 등)")]
    public GameObject glowEffectObject;

    [Header("피격 모션 및 데미지 UI")]
    public GameObject damageIMG;


    private Vector3 _basePosition;
    private bool _isFloating = false;
    private Coroutine _hitUICoroutine;

    private void OnEnable()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnTurnChanged += HandleTurnChanged;
        }
    }

    private void OnDisable()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnTurnChanged -= HandleTurnChanged;
        }
    }

    private void HandleTurnChanged(bool isMyTurn)
    {
        UpdateAttackVisuals(CurrentEntityData);
    }

    private void Awake()
    {
        _basePosition = transform.localPosition;

        if (glowEffectObject != null)
        {
            glowEffectObject.SetActive(false);
        }

        // 인스펙터 미할당 시 프리팹 자식 자동 탐색 및 바인딩
        if (memberHpObject == null)
        {
            Transform t = transform.Find("member_hp_img");
            if (t != null) memberHpObject = t.gameObject;
        }
        if (memberHealthText == null && memberHpObject != null)
        {
            memberHealthText = memberHpObject.GetComponentInChildren<TextMeshPro>(true);
        }
        if (minionAtkObject == null)
        {
            Transform t = transform.Find("atk_img");
            if (t != null) minionAtkObject = t.gameObject;
            else if (entityAttackText != null) minionAtkObject = entityAttackText.gameObject;
        }
        if (minionHpObject == null)
        {
            Transform t = transform.Find("hp_img");
            if (t != null) minionHpObject = t.gameObject;
            else if (entityHealthText != null) minionHpObject = entityHealthText.gameObject;
        }

        if (cannotAttackObject == null)
        {
            Transform t = transform.Find("CannotAttack");
            if (t == null) t = transform.Find("Sleep");
            if (t == null) t = transform.Find("ZZZ");
            if (t == null) t = transform.Find("SleepZZZ");
            if (t != null) cannotAttackObject = t.gameObject;
        }
        if (canAttackObject == null)
        {
            Transform t = transform.Find("CanAttack");
            if (t == null) t = transform.Find("ReadyToAttack");
            if (t != null) canAttackObject = t.gameObject;
        }

        // 3D 프레임 메쉬 렌더러 자동 수집 및 레어도 머티리얼 로드
        InitFrameMeshRenderers();
        EnsureRarityMaterialsLoaded();
    }

    /// <summary>
    /// 필드에 소환될 때 개체 정보 및 카드 데이터를 설정합니다.
    /// </summary>
    public override void SetupEntity(EntityData entityData, CardData cardData)
    {
        base.SetupEntity(entityData, cardData);

        if (_cardData == null || entityData == null) { Debug.Log("FieldCardDisplay.SetupEntity: _cardData or entityData is null"); return; }

        _basePosition = transform.localPosition;

        // 1. 일러스트 스프라이트 설정
        if (cardArtAnimator != null)
        {
            if (_cardData.animationFrames != null && _cardData.animationFrames.Length > 0)
            {
                cardArtAnimator.SetSpriteRender(_cardData.animationFrames);
            }
            else if (_cardData.thumbnail != null)
            {
                cardArtAnimator.SetSpriteRender(new Sprite[] { _cardData.thumbnail });
            }
            else if (_cardData.memberIcon != null)
            {
                cardArtAnimator.SetSpriteRender(new Sprite[] { _cardData.memberIcon });
            }
        }

        // 2. 텍스트 설정
        if (entityNameText != null) entityNameText.text = _cardData.cardName;

        // 3. 레어도별 테두리 적용
        SetRarityBorder(_cardData.rarity);

        // 4. 스탯 설정 (하수인 vs 멤버 분기)
        bool isMember = (entityData != null && entityData.isMember) || (_cardData != null && _cardData.cardType == CardType.멤버);

        if (isMember)
        {
            // 하수인 공격력/체력 오브젝트 비활성화
            if (minionAtkObject != null) minionAtkObject.SetActive(false);
            if (minionHpObject != null) minionHpObject.SetActive(false);

            // 멤버 체력 오브젝트 활성화 및 수치 설정
            if (memberHpObject != null) memberHpObject.SetActive(true);
            if (memberHealthText != null) EntitySetStatText(memberHealthText, entityData.health, _cardData.health);
        }
        else
        {
            // 하수인 공격력/체력 오브젝트 활성화 및 수치 설정
            if (minionAtkObject != null) minionAtkObject.SetActive(true);
            if (minionHpObject != null) minionHpObject.SetActive(true);
            if (entityAttackText != null) EntitySetStatText(entityAttackText, entityData.attack, _cardData.attack);
            if (entityHealthText != null) EntitySetStatText(entityHealthText, entityData.health, _cardData.health);

            // 멤버 체력 오브젝트 비활성화
            if (memberHpObject != null) memberHpObject.SetActive(false);
        }

        // 5. 키워드 연출
        UpdateKeywordFrames(entityData.keywords != null && entityData.keywords.Count > 0 ? entityData.keywords : _cardData?.keyward);

        // 6. 공격 상태(수면 ZZZ / 공격 가능) 연출
        UpdateAttackVisuals(entityData);
    }

    /// <summary>
    /// 필드 하수인의 스탯/키워드 변화 패킷을 받았을 때 갱신합니다.
    /// </summary>
    public override void UpdateEntityStats(EntityData entityData)
    {
        base.UpdateEntityStats(entityData);

        if (entityData == null || _cardData == null) return;

        bool isMember = (entityData != null && entityData.isMember) || (_cardData != null && _cardData.cardType == CardType.멤버);

        if (isMember)
        {
            if (minionAtkObject != null && minionAtkObject.activeSelf) minionAtkObject.SetActive(false);
            if (minionHpObject != null && minionHpObject.activeSelf) minionHpObject.SetActive(false);
            if (memberHpObject != null && !memberHpObject.activeSelf) memberHpObject.SetActive(true);
            if (memberHealthText != null) EntitySetStatText(memberHealthText, entityData.health, _cardData.health);
        }
        else
        {
            if (minionAtkObject != null && !minionAtkObject.activeSelf) minionAtkObject.SetActive(true);
            if (minionHpObject != null && !minionHpObject.activeSelf) minionHpObject.SetActive(true);
            if (memberHpObject != null && memberHpObject.activeSelf) memberHpObject.SetActive(false);
            if (entityAttackText != null) EntitySetStatText(entityAttackText, entityData.attack, _cardData.attack);
            if (entityHealthText != null) EntitySetStatText(entityHealthText, entityData.health, _cardData.health);
        }

        // 침묵 및 키워드 프레임 갱신
        bool isSilenced = entityData.keywords != null && entityData.keywords.Contains(CardKeywords.Silence);

        if (entityData.keywords != null)
        {
            UpdateKeywordFrames(entityData.keywords);
        }

        UpdateTriggerFrames(entityData.activeTriggers, isSilenced);

        // 공격 상태(수면 ZZZ / 공격 가능) 연출 갱신
        UpdateAttackVisuals(entityData);
    }

    /// <summary>
    /// 하수인 3D 프레임 메쉬 렌더러들을 자동으로 수집합니다.
    /// </summary>
    private void InitFrameMeshRenderers()
    {
        if (frameMeshRenderers == null || frameMeshRenderers.Length == 0)
        {
            var list = new List<MeshRenderer>();
            var rootRenderer = GetComponent<MeshRenderer>();
            if (rootRenderer != null)
            {
                list.Add(rootRenderer);
            }

            var allRenderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in allRenderers)
            {
                if (mr == null || mr == rootRenderer) continue;
                // 연출/이펙트 오브젝트 제외 (DMG, HighLight, Silence, Silent, Trigger, Chain, ZZZ, CanAttack 등)
                Transform p = mr.transform;
                bool isEffect = false;
                while (p != null && p != transform)
                {
                    string pName = p.name;
                    if (pName == "DMG" || pName == "HighLight" || pName == "Silence" || pName == "Silent" || 
                        pName == "Trigger" || pName == "Chain" || pName == "Shield" || pName == "Rush" || pName == "Death" ||
                        pName == "CannotAttack" || pName == "Sleep" || pName == "ZZZ" || pName == "CanAttack" || pName == "ReadyToAttack" ||
                        pName == "SleepZZZ" || pName == "ZSpawner" || pName == "Z_Prefab" || pName == "Z_Particle_Pooled")
                    {
                        isEffect = true;
                        break;
                    }
                    p = p.parent;
                }
                if (!isEffect && mr.name.StartsWith("Cube", System.StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(mr);
                }
            }
            frameMeshRenderers = list.ToArray();
        }
    }

    /// <summary>
    /// 인스펙터에 머티리얼이 미할당된 경우 Resources/Materials/Minion/ 경로에서 자동 로드
    /// </summary>
    public void EnsureRarityMaterialsLoaded()
    {
        if (commonMaterial == null) commonMaterial = Resources.Load<Material>("Materials/Minion/M_Minion_Common");
        if (rareMaterial == null) rareMaterial = Resources.Load<Material>("Materials/Minion/M_Minion_Rare");
        if (epicMaterial == null) epicMaterial = Resources.Load<Material>("Materials/Minion/M_Minion_Epic");
        if (legendaryMaterial == null) legendaryMaterial = Resources.Load<Material>("Materials/Minion/M_Minion_Legendary");
    }

    /// <summary>
    /// 레어도에 해당하는 Unlit 머티리얼을 반환합니다.
    /// (일반: 브론즈, 레어: 실버, 희귀: 골드, 전설: 다이아)
    /// </summary>
    public Material GetRarityMaterial(CardRarity rarity)
    {
        EnsureRarityMaterialsLoaded();

        switch (rarity)
        {
            case CardRarity.common: return commonMaterial;
            case CardRarity.rare: return rareMaterial;
            case CardRarity.epic: return epicMaterial;
            case CardRarity.legendary: return legendaryMaterial;
            default: return commonMaterial;
        }
    }

    /// <summary>
    /// 카드의 레어도에 맞춰 테두리 스프라이트 및 3D 프레임 머티리얼을 교체합니다.
    /// (일반: 브론즈, 레어: 실버, 희귀: 골드, 전설: 다이아)
    /// </summary>
    public void SetRarityBorder(CardRarity rarity)
    {
        // 1. 2D 테두리 스프라이트 교체
        if (fieldBorderRenderer != null)
        {
            if (fieldRarityBorders != null && fieldRarityBorders.TryGetValue(rarity, out Sprite borderSprite) && borderSprite != null)
            {
                fieldBorderRenderer.sprite = borderSprite;
                fieldBorderRenderer.gameObject.SetActive(true);
            }
        }

        // 2. 3D 프레임 메쉬 Unlit 머티리얼 일괄 적용 (빛 영향 제거 및 레어도 색상 표현)
        InitFrameMeshRenderers();
        Material rarityMat = GetRarityMaterial(rarity);
        if (rarityMat != null && frameMeshRenderers != null && frameMeshRenderers.Length > 0)
        {
            for (int i = 0; i < frameMeshRenderers.Length; i++)
            {
                if (frameMeshRenderers[i] != null)
                {
                    frameMeshRenderers[i].sharedMaterial = rarityMat;
                }
            }
        }
    }

    /// <summary>
    /// 서버/데이터의 키워드 목록을 기반으로 매핑된 연출 오브젝트들을 켜고 끕니다.
    /// </summary>
    public override void UpdateKeywordFrames(List<CardKeywords> keywords)
    {
        if (keywordObjects == null || keywordObjects.Count == 0) return;

        bool isSilenced = keywords != null && keywords.Contains(CardKeywords.Silence);

        foreach (var kvp in keywordObjects)
        {
            CardKeywords kw = kvp.Key;
            GameObject frameObj = kvp.Value;
            if (frameObj == null) continue;

            bool shouldActive = false;
            if (kw == CardKeywords.Silence)
            {
                shouldActive = isSilenced;
            }
            else
            {
                shouldActive = !isSilenced && keywords != null && keywords.Contains(kw);
            }

            if (frameObj.activeSelf != shouldActive)
            {
                frameObj.SetActive(shouldActive);
            }
        }
    }

    /// <summary>
    /// 서버/데이터의 활성 트리거 효과 목록을 기반으로 매핑된 연출 오브젝트(파티클, 룬 등)를 켜고 끕니다.
    /// </summary>
    public override void UpdateTriggerFrames(List<EffectTriggerType> triggers, bool isSilenced)
    {
        if (triggerObjects == null || triggerObjects.Count == 0) return;

        foreach (var kvp in triggerObjects)
        {
            EffectTriggerType triggerType = kvp.Key;
            GameObject triggerObj = kvp.Value;
            if (triggerObj == null) continue;

            bool shouldActive = !isSilenced && triggers != null && triggers.Contains(triggerType);
            if (triggerObj.activeSelf != shouldActive)
            {
                triggerObj.SetActive(shouldActive);
            }
        }
    }

    public override bool IsFloating => _isFloating;

    // --- [연출 기능 1] 공격자용: 공중부양 (Floating) ---
    public override void SetFloatingState(bool shouldFloat)
    {
        if (_isFloating == shouldFloat) return;
        _isFloating = shouldFloat;

        transform.DOKill();

        if (shouldFloat)
        {
            transform.DOLocalMoveY(_basePosition.y + floatHeight, floatDuration).SetEase(Ease.OutQuad);
        }
        else
        {
            transform.DOLocalMoveY(_basePosition.y, floatDuration).SetEase(Ease.OutQuad);
        }
    }

    // --- [연출 기능 2] 대상용: 발광 (Glow) ---
    public override void SetGlowState(bool shouldGlow)
    {
        if (glowEffectObject != null)
        {
            glowEffectObject.SetActive(shouldGlow);
        }
    }

    /// <summary>
    /// 서버에서 사망이 확정되어 연출 대기 중일 때 호출되어 타겟팅 방지 및 시각 피드백을 적용합니다.
    /// </summary>
    public override void SetPendingDeadVisual()
    {
        SetGlowState(false);

        // 콜라이더를 비활성화하여 마우스 레이캐스트 조준 원천 차단
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = false;
        }

        // 반투명 처리 (사망 예정 표시)
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            if (sr != null)
            {
                Color c = sr.color;
                c.a *= 0.5f;
                sr.color = c;
            }
        }
    }

    // --- [연출 기능 3] 피격 데미지 UI ---
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
    /// 하수인이 회복(Heal)을 받았을 때 버프 VFX 및 펀치 스케일을 재생합니다.
    /// </summary>
    public override void HealUI(int healAmount)
    {
        PlayBuffVFX();
        transform.DOKill();
        transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 5, 0.5f);
    }


    /// <summary>
    /// 트리거 효과 발동 시 상시 연출 오브젝트 펄스 및 이펙트/사운드를 재생합니다.
    /// </summary>
    public override void PlayTriggerAnimation(EffectTriggerType triggerType)
    {
        if (triggerObjects != null && triggerObjects.TryGetValue(triggerType, out var triggerObj) && triggerObj != null)
        {
            triggerObj.SetActive(true);
            triggerObj.transform.DOKill();
            triggerObj.transform.DOPunchScale(Vector3.one * 0.4f, 0.4f, 8, 0.5f);
        }

        CardVFXData vfxData = _cardData != null ? _cardData.GetTriggerVFX(triggerType) : null;

        if (vfxData != null)
        {
            // AreaField, AreaFromCaster, ImpactOnTarget, Projectile 등 대상/필드 지향 연출은 GameEntityManager의 액션 연출에서 전담하므로
            // 시전자 본체 위치에서의 중복 소환을 방지합니다.
            if (vfxData.vfxPrefab != null && 
                vfxData.playType != VFXPlayType.AreaField && 
                vfxData.playType != VFXPlayType.AreaFromCaster && 
                vfxData.playType != VFXPlayType.ImpactOnTarget && 
                vfxData.playType != VFXPlayType.Projectile)
            {
                GameObject vfx = Instantiate(vfxData.vfxPrefab, transform.position, Quaternion.identity);
                Destroy(vfx, vfxData.vfxDestroyTime > 0f ? vfxData.vfxDestroyTime : 2.0f);
            }

            if (vfxData.soundEffect != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX3D(vfxData.soundEffect, transform.position);
            }
        }
    }

    /// <summary>
    /// 하수인의 공격 가능/불가(수면 ZZZ) 상태 연출 오브젝트를 갱신합니다.
    /// - 소환된 턴에 질풍/속공 등이 없어 공격할 수 없는 상태: cannotAttackObject (ZZZ) 활성화
    /// - 속박(Bind) 상태이상이나 공격 완료(hasAttacked) 상태인 경우: cannotAttackObject 비활성화
    /// - 내 턴에 공격 가능한 상태(공격력 > 0, canAttack == true): canAttackObject 활성화
    /// </summary>
    public override void UpdateAttackVisuals(EntityData entityData)
    {
        if (entityData == null || entityData.isMember || entityData.isLeader)
        {
            if (cannotAttackObject != null && cannotAttackObject.activeSelf) cannotAttackObject.SetActive(false);
            if (canAttackObject != null && canAttackObject.activeSelf) canAttackObject.SetActive(false);
            return;
        }

        bool isSilenced = entityData.keywords != null && entityData.keywords.Contains(CardKeywords.Silence);
        bool isBound = !isSilenced && entityData.keywords != null && entityData.keywords.Contains(CardKeywords.Bind);
        bool hasAttacked = entityData.hasAttacked;
        bool canAttack = entityData.canAttack;

        // 1. 공격 불가 (소환 후유증 / ZZZ) 표시 조건:
        // 공격 불가(!canAttack)이면서 속박 등 전용 상태이상이 아니고(!isBound), 이번 턴 공격을 마친 상태가 아닐 때(!hasAttacked)
        bool shouldShowSleep = !canAttack && !isBound && !hasAttacked;
        if (cannotAttackObject != null && cannotAttackObject.activeSelf != shouldShowSleep)
        {
            cannotAttackObject.SetActive(shouldShowSleep);
        }

        // 2. 공격 가능 (Ready to Attack) 표시 조건:
        // 내 하수인이고, 내 턴이며, 서버 판정 상 공격 가능하고(canAttack), 공격력이 0보다 크며, 사망 대기 중이 아닐 때
        bool isMyMinion = string.IsNullOrEmpty(entityData.ownerUid) || 
                          (GameClient.Instance != null && entityData.ownerUid == GameClient.Instance.UserUid);
        bool isMyTurn = BattleManager.Instance == null || BattleManager.Instance.IsMyTurn || 
                        (GameEntityManager.Instance != null && GameEntityManager.Instance.test);
        bool isPendingDead = GameEntityManager.Instance != null && GameEntityManager.Instance.IsPendingDead(EntityId);

        bool shouldShowCanAttack = isMyMinion && isMyTurn && canAttack && (entityData.attack > 0) && !isPendingDead;
        if (canAttackObject != null && canAttackObject.activeSelf != shouldShowCanAttack)
        {
            canAttackObject.SetActive(shouldShowCanAttack);
        }
    }

    public override void ResetBasePosition()
    {
        _basePosition = transform.localPosition;
    }
}
