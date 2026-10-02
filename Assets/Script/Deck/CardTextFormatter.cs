using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

/// <summary>
/// 카드 설명문(description) 속 [] 및 {}로 묶인 키워드나 카드 ID를 감지하여,
/// 굵은 글씨와 색상, 마우스 클릭이 가능한 Link 태그로 변환해 주는 포매터입니다.
/// </summary>
public static class CardTextFormatter
{
    // 카드 캐시 (ID로 빠른 카드 검색)
    private static readonly Dictionary<string, CardData> CardCache = new Dictionary<string, CardData>(StringComparer.OrdinalIgnoreCase);

    // 키워드 데이터베이스 (영문 및 한글 명칭 매핑 -> (한글 명칭, 상세 설명))
    private static readonly Dictionary<string, (string title, string desc)> KeywordMap = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
    {
        { "Charge", ("돌진", "소환된 턴에 즉시 공격할 수 있습니다.") },
        { "돌진", ("돌진", "소환된 턴에 즉시 공격할 수 있습니다.") },

        { "Rush", ("속공", "소환된 턴에 하수인을 공격할 수 있습니다.") },
        { "속공", ("속공", "소환된 턴에 하수인을 공격할 수 있습니다.") },

        { "Taunt", ("도발", "적이 이 하수인을 먼저 공격해야 합니다.") },
        { "도발", ("도발", "적이 이 하수인을 먼저 공격해야 합니다.") },

        { "DivineShield", ("천상의 보호막", "처음 받는 피해를 1회 막아냅니다.") },
        { "천보", ("천상의 보호막", "처음 받는 피해를 1회 막아냅니다.") },
        { "천상의 보호막", ("천상의 보호막", "처음 받는 피해를 1회 막아냅니다.") },
        { "천상의보호막", ("천상의 보호막", "처음 받는 피해를 1회 막아냅니다.") },

        { "Poisonous", ("독성", "이 하수인에게 피해를 받은 하수인은 즉시 처치됩니다.") },
        { "독성", ("독성", "이 하수인에게 피해를 받은 하수인은 즉시 처치됩니다.") },

        { "Stealth", ("은신", "공격하기 전까지 적의 공격이나 대상 지정이 불가합니다.") },
        { "은신", ("은신", "공격하기 전까지 적의 공격이나 대상 지정이 불가합니다.") },

        { "Lifesteal", ("생명력 흡수", "이 카드가 피해를 줄 때마다 내 영웅의 생명력을 그만큼 회복합니다.") },
        { "생흡", ("생명력 흡수", "이 카드가 피해를 줄 때마다 내 영웅의 생명력을 그만큼 회복합니다.") },
        { "생명력 흡수", ("생명력 흡수", "이 카드가 피해를 줄 때마다 내 영웅의 생명력을 그만큼 회복합니다.") },
        { "생명력흡수", ("생명력 흡수", "이 카드가 피해를 줄 때마다 내 영웅의 생명력을 그만큼 회복합니다.") },

        { "Windfury", ("질풍", "한 턴에 2번 공격할 수 있습니다.") },
        { "질풍", ("질풍", "한 턴에 2번 공격할 수 있습니다.") },

        { "Bind", ("속박", "다음 턴 동안 공격할 수 없습니다.") },
        { "속박", ("속박", "다음 턴 동안 공격할 수 없습니다.") },

        { "Silence", ("침묵", "부여된 모든 효과와 스탯 버프를 제거합니다.") },
        { "침묵", ("침묵", "부여된 모든 효과와 스탯 버프를 제거합니다.") },

        { "Elusive", ("주문 면역", "주문 카드의 단일 타겟팅 대상이 되지 않습니다.") },
        { "주문 면역", ("주문 면역", "주문 카드의 단일 타겟팅 대상이 되지 않습니다.") },
        { "주문면역", ("주문 면역", "주문 카드의 단일 타겟팅 대상이 되지 않습니다.") },

        { "Bodyguard", ("리더 수호", "내 영웅(리더)이 피해를 입을 때 이 하수인이 대신 피해를 받습니다.") },
        { "리더 수호", ("리더 수호", "내 영웅(리더)이 피해를 입을 때 이 하수인이 대신 피해를 받습니다.") },
        { "리더수호", ("리더 수호", "내 영웅(리더)이 피해를 입을 때 이 하수인이 대신 피해를 받습니다.") },
        { "보디가드", ("리더 수호", "내 영웅(리더)이 피해를 입을 때 이 하수인이 대신 피해를 받습니다.") },

        { "등장", ("등장", "손에서 필드로 냈을 때 발동합니다.") },
        { "전투의 함성", ("등장", "손에서 필드로 냈을 때 발동합니다.") },
        { "전투의함성", ("등장", "손에서 필드로 냈을 때 발동합니다.") },
        { "Battlecry", ("등장", "손에서 필드로 냈을 때 발동합니다.") },

        { "퇴장", ("퇴장", "필드에서 파괴되었을 때 발동합니다.") },
        { "죽음의 메아리", ("퇴장", "필드에서 파괴되었을 때 발동합니다.") },
        { "죽음의메아리", ("퇴장", "필드에서 파괴되었을 때 발동합니다.") },
        { "Deathrattle", ("퇴장", "필드에서 파괴되었을 때 발동합니다.") },

        // 특수 오라 및 스탯 키워드 (SpellAmp, SpellWeakness, DrawSeal, BuffAmp)
        { "SpellAmp", ("주문 공격력", "내가 시전하는 주문의 피해량이 증가합니다.") },
        { "주문 증폭", ("주문 공격력", "내가 시전하는 주문의 피해량이 증가합니다.") },
        { "주문증폭", ("주문 공격력", "내가 시전하는 주문의 피해량이 증가합니다.") },
        { "주문 공격력", ("주문 공격력", "내가 시전하는 주문의 피해량이 증가합니다.") },
        { "주문공격력", ("주문 공격력", "내가 시전하는 주문의 피해량이 증가합니다.") },

        { "SpellWeakness", ("주문 약화", "받는 주문의 피해량이 감소합니다.") },
        { "주문 약화", ("주문 약화", "받는 주문의 피해량이 감소합니다.") },
        { "주문약화", ("주문 약화", "받는 주문의 피해량이 감소합니다.") },

        { "DrawSeal", ("드로우 봉인", "효과가 지속되는 동안 카드를 뽑을 수 없습니다.") },
        { "드로우 봉인", ("드로우 봉인", "효과가 지속되는 동안 카드를 뽑을 수 없습니다.") },
        { "드로우봉인", ("드로우 봉인", "효과가 지속되는 동안 카드를 뽑을 수 없습니다.") },

        { "BuffAmpAttack", ("공격력 버프 증폭", "내가 시전하는 공격력 버프량이 추가로 증가합니다.") },
        { "BuffAmpHealth", ("체력 버프 증폭", "내가 시전하는 체력 버프량이 추가로 증가합니다.") },
        { "BuffAmp", ("버프 증폭", "내가 시전하는 아군 버프량이 추가로 증가합니다.") },
        { "버프 증폭", ("버프 증폭", "내가 시전하는 아군 버프량이 추가로 증가합니다.") },
        { "버프증폭", ("버프 증폭", "내가 시전하는 아군 버프량이 추가로 증가합니다.") },
    };

    // =========================================================================
    // 정적 컴파일된 정규식 캐시 (런타임 GC Alloc 및 동적 파싱 오버헤드 0 최적화)
    // =========================================================================
    private static readonly Regex BracketRegex = new Regex(@"\[([^\]]+)\]|\{([^}]+)\}", RegexOptions.Compiled);
    private static readonly Regex DamageRegex = new Regex(@"\{damage(?::(\d+))?\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CustomValueRegex = new Regex(@"\{customValue(?::(\d+))?\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BuffAtkRegex = new Regex(@"\{buffAtk(?::(\d+))?\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BuffHpRegex = new Regex(@"\{buffHp(?::(\d+))?\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LegacyNRegex = new Regex(@"\{n\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LegacyNMatch1Regex = new Regex(@"\bn(?=만큼\s*피해)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LegacyNMatch2Regex = new Regex(@"\bn(?=의\s*피해)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LegacyDamageSuffixRegex = new Regex(@"(\d+)(의\s*피해)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LegacyDamagePrefixRegex = new Regex(@"(피해를\s*)(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BuffAmpAtkRegex = new Regex(@"BuffAmpAttack\s*:\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BuffAmpHpRegex = new Regex(@"BuffAmpHealth\s*:\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // [최적화] 정적 카드 설명문(도감, 덱 빌더 등 실시간 전투 버프가 없는 경우) 파싱 결과 캐시
    private static readonly Dictionary<string, string> _staticDescriptionCache = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// description 텍스트 내의 [] 및 {} 태그를 강조 스타일(굵은 글씨 + 색상)과 클릭 링크 태그로 변환합니다.
    /// 카드 ID가 들어있는 경우 실제 카드의 한국어 이름으로 자동 치환하며,
    /// cardInfo가 제공되면 텍스트 내 동적 수치(피해량, 누적 스택 등)를 실시간으로 반영합니다.
    /// </summary>
    public static string FormatDescription(string rawDescription, CardData sourceCard = null, CardInfo cardInfo = null)
    {
        if (string.IsNullOrEmpty(rawDescription)) return string.Empty;

        string cacheKey = null;
        if (cardInfo == null)
        {
            cacheKey = sourceCard != null ? $"{sourceCard.cardID}_{rawDescription}" : rawDescription;
            if (_staticDescriptionCache.TryGetValue(cacheKey, out string cached))
            {
                return cached;
            }
        }

        // 1. 실시간 가변 수치 및 태그 치환 (cardInfo == null일 때는 기본값 숫자로 치환)
        string processed = ApplyDynamicValues(rawDescription, sourceCard, cardInfo);

        string sourceCardInfo = sourceCard != null ? $"{sourceCard.cardName} ({sourceCard.cardID})" : null;

        string formatted = BracketRegex.Replace(processed, match =>
        {
            string token = !string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[2].Value;
            token = token.Trim();

            // 쉼표로 연결된 다중 키워드 (예: [Taunt, DivineShield]) 개별 분리 지원
            if (token.Contains(","))
            {
                string[] subTokens = token.Split(',');
                List<string> formattedSubTokens = new List<string>();
                foreach (string sub in subTokens)
                {
                    string trimmedSub = sub.Trim();
                    if (string.IsNullOrEmpty(trimmedSub)) continue;
                    formattedSubTokens.Add(FormatSingleToken(trimmedSub, sourceCardInfo));
                }
                return string.Join(" ", formattedSubTokens);
            }

            return FormatSingleToken(token, sourceCardInfo);
        });

        if (cacheKey != null)
        {
            _staticDescriptionCache[cacheKey] = formatted;
        }

        return formatted;
    }

    private static string FormatSingleToken(string token, string sourceCardInfo)
    {
        // 1. 키워드 또는 특수 속성(SpellAmp: 1 등)인지 확인
        if (TryGetKeyword(token, out var kw))
        {
            // 황금색 볼드 링크 (#FF9900)
            return $"<link=\"KW:{token}\"><b><color=#FF9900>[{kw.title}]</color></b></link>";
        }

        // 2. 카드 ID인지 확인
        CardData card = FindCardData(token, sourceCardInfo);
        if (card != null)
        {
            // 청록색 볼드 링크 (#00E5FF) -> 실제 카드 이름으로 표시!
            return $"<link=\"CARD:{card.cardID}\"><b><color=#00E5FF>[{card.cardName}]</color></b></link>";
        }

        // 만약 cards- 로 시작하지만 아직 카드를 못 찾은 경우 -> 어떤 카드에서 참조했는지 상세 에러 로그 출력!
        if (token.StartsWith("cards-", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(sourceCardInfo))
            {
                Debug.LogError($"[CardTextFormatter] 카드 참조 오류! '{sourceCardInfo}' 카드의 설명문에서 참조한 카드 ID '{token}'를 ScriptableObject(리소스)에서 찾을 수 없습니다.");
            }
            else
            {
                Debug.LogError($"[CardTextFormatter] 카드 참조 오류! 설명문에서 참조한 카드 ID '{token}'를 ScriptableObject(리소스)에서 찾을 수 없습니다.");
            }

            return $"<link=\"CARD:{token}\"><b><color=#00E5FF>[{token}]</color></b></link>";
        }

        // 3. 기타 미등록 키워드/단어
        return $"<link=\"KW:{token}\"><b><color=#FFD700>[{token}]</color></b></link>";
    }

    /// <summary>
    /// description 텍스트 내의 동적 수치 태그({damage:3}, {customValue:0}, {buffAtk:2}, {buffHP:2})를
    /// 실시간 카드 정보(CardInfo)에 따라 계산 및 치환합니다.
    /// cardInfo가 null(도감, 덱 빌더 등)인 경우 기본값 숫자로 깔끔하게 치환합니다.
    /// </summary>
    public static string ApplyDynamicValues(string rawDescription, CardData sourceCard, CardInfo cardInfo)
    {
        if (string.IsNullOrEmpty(rawDescription)) return string.Empty;

        string result = rawDescription;
        bool isSpell = (sourceCard != null && sourceCard.cardType == CardType.주문);

        // -------------------------------------------------------------
        // 1. {damage:기본값} 태그 치환
        // - 주문(Spell) 카드인 경우: 주문 증폭(spellAmpBonus)을 합산하여 표시
        // - 하수인(Minion) 카드인 경우: 주문 증폭(SpellAmp)은 제외하되, 추후 다른 카드의 효과(minionDamageBonus 또는 서버 dynamicDamage)는 정상 반영
        // -------------------------------------------------------------
        result = DamageRegex.Replace(result, m =>
        {
            int baseVal = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
            if (cardInfo == null) return baseVal.ToString();

            int targetDmg = baseVal;
            if (isSpell)
            {
                // 주문 카드: 주문 증폭(SpellAmp) 적용
                int spellBonus = cardInfo.spellAmpBonus;
                targetDmg = (cardInfo.dynamicDamage > 0) ? cardInfo.dynamicDamage : (baseVal + spellBonus);
            }
            else
            {
                // 하수인 카드: 주문 증폭(SpellAmp)은 미적용!
                // 단, 추후 다른 카드의 효과(하수인 효과 피해 증폭 minionDamageBonus 또는 dynamicDamage)는 온전히 반영
                if (cardInfo.dynamicDamage > 0 && cardInfo.dynamicDamage != cardInfo.baseDamage)
                {
                    targetDmg = cardInfo.dynamicDamage;
                }
                else
                {
                    targetDmg = baseVal + cardInfo.minionDamageBonus;
                }
            }

            if (targetDmg > baseVal)
                return $"<b><color=#00FF66>{targetDmg}</color></b>";
            else if (targetDmg < baseVal)
                return $"<b><color=#FF5555>{targetDmg}</color></b>";
            else
                return targetDmg.ToString();
        });

        // -------------------------------------------------------------
        // 2. {customValue:기본값} 태그 치환
        // - 주문(Spell) 카드인 경우: customValue(스택) + 주문 증폭(spellAmpBonus) 동시 합산
        // - 하수인(Minion) 카드인 경우: customValue(스택) + 하수인 피해 보너스(minionDamageBonus) 합산 (주문 증폭은 제외)
        // -------------------------------------------------------------
        result = CustomValueRegex.Replace(result, m =>
        {
            int baseVal = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
            if (cardInfo == null) return baseVal.ToString();

            // 누적 수치 계산 (서버가 누적 스택을 보내거나 최종값을 보내는 경우 모두 안전 지원)
            int currentVal = (cardInfo.customValue >= baseVal) ? cardInfo.customValue : (baseVal + cardInfo.customValue);

            if (isSpell)
            {
                // 주문 카드라면 주문 증폭(SpellAmp) 추가 합산!
                if (cardInfo.spellAmpBonus > 0)
                {
                    currentVal += cardInfo.spellAmpBonus;
                }
            }
            else
            {
                // 하수인 카드라도 다른 카드의 효과(하수인 피해 증폭)가 있다면 추가 합산 가능
                if (cardInfo.minionDamageBonus > 0)
                {
                    currentVal += cardInfo.minionDamageBonus;
                }
            }

            if (currentVal > baseVal)
                return $"<b><color=#00FF66>{currentVal}</color></b>";
            else if (currentVal < baseVal)
                return $"<b><color=#FF5555>{currentVal}</color></b>";
            else
                return currentVal.ToString();
        });

        // -------------------------------------------------------------
        // 3. {buffAtk:기본값} 공격력 버프 태그 치환 (대소문자 무관)
        // -------------------------------------------------------------
        result = BuffAtkRegex.Replace(result, m =>
        {
            int baseVal = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
            if (cardInfo == null) return baseVal.ToString();

            int currentAtk = (cardInfo.dynamicBuffAttack > 0) ? cardInfo.dynamicBuffAttack : (baseVal + cardInfo.buffAmpAtkBonus);

            if (currentAtk > baseVal)
                return $"<b><color=#00FF66>{currentAtk}</color></b>";
            else if (currentAtk < baseVal)
                return $"<b><color=#FF5555>{currentAtk}</color></b>";
            else
                return currentAtk.ToString();
        });

        // -------------------------------------------------------------
        // 4. {buffHp:기본값} 또는 {buffHP:기본값} 체력 버프 태그 치환 (대소문자 무관)
        // -------------------------------------------------------------
        result = BuffHpRegex.Replace(result, m =>
        {
            int baseVal = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
            if (cardInfo == null) return baseVal.ToString();

            int currentHp = (cardInfo.dynamicBuffHealth > 0) ? cardInfo.dynamicBuffHealth : (baseVal + cardInfo.buffAmpHpBonus);

            if (currentHp > baseVal)
                return $"<b><color=#00FF66>{currentHp}</color></b>";
            else if (currentHp < baseVal)
                return $"<b><color=#FF5555>{currentHp}</color></b>";
            else
                return currentHp.ToString();
        });

        // -------------------------------------------------------------
        // 5. 하위 호환 레거시 패턴 처리 (태그가 아직 없는 기존 카드 지원)
        // -------------------------------------------------------------
        if (cardInfo != null)
        {
            // 'n만큼 피해' / 'n의 피해' / '{n}' 형태 치환
            int effectiveDynamicValue = cardInfo.dynamicDamage > 0 ? cardInfo.dynamicDamage : cardInfo.customValue;
            if (effectiveDynamicValue > 0)
            {
                result = LegacyNRegex.Replace(result, $"<b><color=#00FF66>{effectiveDynamicValue}</color></b>");
                result = LegacyNMatch1Regex.Replace(result, $"<b><color=#00FF66>{effectiveDynamicValue}</color></b>");
                result = LegacyNMatch2Regex.Replace(result, $"<b><color=#00FF66>{effectiveDynamicValue}</color></b>");
            }

            // 만약 태그가 전혀 쓰이지 않은 일반 텍스트 카드라면 한국어 피해량/버프 정규식 치환 fallback 적용
            bool hasExplicitTags = rawDescription.Contains("{damage") || rawDescription.Contains("{customValue") || rawDescription.Contains("{buffAtk") || rawDescription.Contains("{buffHp") || rawDescription.Contains("{buffHP");
            if (!hasExplicitTags && isSpell && (cardInfo.dynamicDamage > 0 || cardInfo.spellAmpBonus > 0))
            {
                int targetDmg = (cardInfo.dynamicDamage > 0) ? cardInfo.dynamicDamage : (cardInfo.baseDamage + cardInfo.spellAmpBonus);
                result = LegacyDamageSuffixRegex.Replace(result, m =>
                {
                    int originalNum = int.Parse(m.Groups[1].Value);
                    if (targetDmg > originalNum)
                        return $"<b><color=#00FF66>{targetDmg}</color></b>{m.Groups[2].Value}";
                    return m.Value;
                });
                result = LegacyDamagePrefixRegex.Replace(result, m =>
                {
                    int originalNum = int.Parse(m.Groups[2].Value);
                    if (targetDmg > originalNum)
                        return $"{m.Groups[1].Value}<b><color=#00FF66>{targetDmg}</color></b>";
                    return m.Value;
                });
            }
        }

        return result;
    }

    /// <summary>
    /// 카드 ID로 CardData를 안전하게 검색합니다. (ResourceManager 및 Resources 폴더 지원)
    /// </summary>
    public static CardData FindCardData(string cardId, string sourceCardInfo = null)
    {
        if (string.IsNullOrEmpty(cardId)) return null;

        cardId = cardId.Trim();

        // 키워드 명칭인 경우 카드 검색 제외
        if (KeywordMap.ContainsKey(cardId)) return null;

        if (CardCache.TryGetValue(cardId, out var cached) && cached != null)
        {
            return cached;
        }

        // 1. ResourceManager 인스턴스 확인 (TryGetCardData 사용하여 불필요한 generic 에러 방지)
        if (ResourceManager.Instance != null && cardId.StartsWith("cards-", StringComparison.OrdinalIgnoreCase))
        {
            if (ResourceManager.Instance.TryGetCardData(cardId, out var resCard) && resCard != null)
            {
                CardCache[cardId] = resCard;
                return resCard;
            }
        }

        // 2. Resources 폴더 전체에서 로드 및 캐싱 (ResourceManager 초기화 전이거나 미등록 시 대비)
        var allCards = Resources.LoadAll<CardData>("CardData");
        if (allCards != null && allCards.Length > 0)
        {
            foreach (var c in allCards)
            {
                if (c != null && !string.IsNullOrEmpty(c.cardID))
                {
                    if (!CardCache.ContainsKey(c.cardID))
                    {
                        CardCache[c.cardID] = c;
                    }
                }
            }
        }

        if (CardCache.TryGetValue(cardId, out var loaded))
        {
            return loaded;
        }

        return null;
    }

    /// <summary>
    /// TextMeshPro 텍스트가 지정 영역을 벗어나지 않도록 자동 크기 조절(Auto-sizing) 및 오버플로우 방지를 설정합니다.
    /// </summary>
    public static void EnsureTextAutoFit(TMP_Text textComp, float minSize = 9f, float maxSize = 20f, bool wordWrap = true, TextOverflowModes overflowMode = TextOverflowModes.Ellipsis)
    {
        if (textComp == null) return;

        textComp.enableAutoSizing = true;
        textComp.fontSizeMin = minSize;
        textComp.fontSizeMax = Mathf.Max(minSize, maxSize > 0 ? maxSize : textComp.fontSize);
        textComp.textWrappingMode = wordWrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        textComp.overflowMode = overflowMode;
    }

    /// <summary>
    /// TextMeshProUGUI에 서식 적용된 텍스트를 대입하고, 클릭 감지 핸들러(TMPLinkClickHandler)를 자동으로 부착합니다.
    /// cardInfo가 제공되면 실시간 동적 수치(피해량 등)가 함께 반영됩니다.
    /// </summary>
    public static void FormatAndBind(TextMeshProUGUI textComp, string rawDescription, CardData sourceCard = null, CardInfo cardInfo = null)
    {
        if (textComp == null) return;

        // 텍스트가 카드/패널 영역 밖으로 튀어나가지 않도록 자동 크기 조절 및 말줄임표 보장
        float currentMax = textComp.enableAutoSizing ? textComp.fontSizeMax : textComp.fontSize;
        if (currentMax <= 0) currentMax = 20f;
        EnsureTextAutoFit(textComp, minSize: 8f, maxSize: currentMax, wordWrap: true, overflowMode: TextOverflowModes.Ellipsis);

        textComp.text = FormatDescription(rawDescription, sourceCard, cardInfo);
        textComp.raycastTarget = true;

        if (textComp.GetComponent<TMPLinkClickHandler>() == null)
        {
            textComp.gameObject.AddComponent<TMPLinkClickHandler>();
        }
    }

    /// <summary>
    /// 키워드 및 특수 오라/속성(SpellAmp: 1, BuffAmp 등)의 설명을 검색합니다.
    /// </summary>
    public static bool TryGetKeyword(string token, out (string title, string desc) info)
    {
        if (string.IsNullOrEmpty(token))
        {
            info = default;
            return false;
        }

        // 1. 기본 키워드 맵 직접 매칭
        if (KeywordMap.TryGetValue(token, out info))
        {
            return true;
        }

        // 2. 콜론(:) 또는 슬래시(/)가 포함된 파라미터형 특수 속성 토큰 처리
        if (token.IndexOf(':') != -1 || token.IndexOf('/') != -1)
        {
            // 2-1. [BuffAmpAttack: X / BuffAmpHealth: Y] 복합 형태
            if (token.IndexOf("BuffAmpAttack", StringComparison.OrdinalIgnoreCase) != -1 ||
                token.IndexOf("BuffAmpHealth", StringComparison.OrdinalIgnoreCase) != -1)
            {
                var matchAtk = BuffAmpAtkRegex.Match(token);
                var matchHp = BuffAmpHpRegex.Match(token);
                string atkVal = matchAtk.Success ? matchAtk.Groups[1].Value : "0";
                string hpVal = matchHp.Success ? matchHp.Groups[1].Value : "0";

                info = ($"버프 증폭 (+{atkVal}/+{hpVal})", $"내가 부여하는 아군 버프 주문의 공격력이 +{atkVal}, 체력이 +{hpVal}만큼 추가로 증가합니다.");
                return true;
            }

            // 2-2. 단일 [속성: 수치] 형태 (예: SpellAmp: 1, SpellWeakness: 1 등)
            string[] parts = token.Split(':');
            if (parts.Length == 2)
            {
                string key = parts[0].Trim();
                string val = parts[1].Trim();

                if (string.Equals(key, "SpellAmp", StringComparison.OrdinalIgnoreCase))
                {
                    info = ($"주문 공격력 +{val}", $"내가 시전하는 주문 카드의 피해량이 {val}만큼 증가합니다.");
                    return true;
                }
                if (string.Equals(key, "SpellWeakness", StringComparison.OrdinalIgnoreCase))
                {
                    info = ($"주문 약화 +{val}", $"받는 주문 카드의 피해량이 {val}만큼 감소합니다.");
                    return true;
                }
                if (string.Equals(key, "BuffAmpAttack", StringComparison.OrdinalIgnoreCase))
                {
                    info = ($"공격력 버프 증폭 +{val}", $"내가 시전하는 아군 공격력 버프량이 {val}만큼 추가로 증가합니다.");
                    return true;
                }
                if (string.Equals(key, "BuffAmpHealth", StringComparison.OrdinalIgnoreCase))
                {
                    info = ($"체력 버프 증폭 +{val}", $"내가 시전하는 아군 체력 버프량이 {val}만큼 추가로 증가합니다.");
                    return true;
                }
            }
        }

        info = default;
        return false;
    }

    /// <summary>
    /// 카드 데이터와 설명문에서 키워드 및 고유 단어(등장, 퇴장, SpellAmp 등)의 설명 문자열 목록을 추출합니다.
    /// 포맷: '단어: 설명'
    /// </summary>
    public static List<string> GetKeywordAndTermTooltips(CardData data)
    {
        List<string> tooltips = new List<string>();
        HashSet<string> addedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (data == null) return tooltips;

        void AddTerm(string title, string desc)
        {
            if (addedTitles.Add(title))
            {
                tooltips.Add($"{title}: {desc}");
            }
        }

        // 1. data.keyward 에 있는 enum 키워드들 추가
        if (data.keyward != null)
        {
            foreach (var kw in data.keyward)
            {
                string kwName = kw.ToString();
                if (TryGetKeyword(kwName, out var info))
                {
                    AddTerm(info.title, info.desc);
                }
            }
        }

        // 2. description 텍스트 분석
        if (!string.IsNullOrEmpty(data.description))
        {
            // (1) 괄호 안의 단어들 확인
            var matches = BracketRegex.Matches(data.description);
            foreach (Match match in matches)
            {
                string token = !string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[2].Value;
                token = token.Trim();

                if (token.Contains(","))
                {
                    foreach (var sub in token.Split(','))
                    {
                        string trimmed = sub.Trim();
                        if (TryGetKeyword(trimmed, out var subInfo))
                        {
                            AddTerm(subInfo.title, subInfo.desc);
                        }
                    }
                }
                else if (TryGetKeyword(token, out var info))
                {
                    AddTerm(info.title, info.desc);
                }
            }

            // (2) 괄호 없이 적혀 있을 수 있는 '등장', '퇴장' 등 핵심 단어 확인
            if (data.description.Contains("등장") && TryGetKeyword("등장", out var onPlayInfo))
            {
                AddTerm(onPlayInfo.title, onPlayInfo.desc);
            }
            if (data.description.Contains("퇴장") && TryGetKeyword("퇴장", out var onDeathInfo))
            {
                AddTerm(onDeathInfo.title, onDeathInfo.desc);
            }
        }

        // 3. CardData의 특수 오라/스탯 속성 확인 (SpellAmp, SpellWeakness, DrawSeal, BuffAmp)
        if (data.spellAmp > 0)
        {
            AddTerm($"주문 공격력 +{data.spellAmp}", $"내가 시전하는 주문 카드의 피해량이 {data.spellAmp}만큼 증가합니다.");
        }
        if (data.spellWeakness > 0)
        {
            AddTerm($"주문 약화 +{data.spellWeakness}", $"받는 주문 카드의 피해량이 {data.spellWeakness}만큼 감소합니다.");
        }
        if (data.drawSeal)
        {
            AddTerm("드로우 봉인", "효과가 지속되는 동안 카드를 뽑을 수 없습니다.");
        }
        if (data.buffAmpAttack > 0 || data.buffAmpHealth > 0)
        {
            AddTerm($"버프 증폭 (+{data.buffAmpAttack}/+{data.buffAmpHealth})", $"내가 시전하는 아군 버프의 공격력이 +{data.buffAmpAttack}, 체력이 +{data.buffAmpHealth}만큼 추가로 증가합니다.");
        }

        return tooltips;
    }

    /// <summary>
    /// description 텍스트에서 참조된 카드 ID 목록을 추출합니다.
    /// </summary>
    public static List<string> GetReferencedCardIds(CardData data)
    {
        List<string> refCardIds = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (data == null || string.IsNullOrEmpty(data.description)) return refCardIds;

        var matches = BracketRegex.Matches(data.description);
        foreach (Match match in matches)
        {
            string token = !string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[2].Value;
            token = token.Trim();

            // 키워드인 경우 참조 카드 검색에서 즉시 제외
            if (KeywordMap.ContainsKey(token)) continue;

            // cards- 로 시작하는 카드 ID만 참조 카드로 등록
            if (token.StartsWith("cards-", StringComparison.OrdinalIgnoreCase))
            {
                if (seen.Add(token))
                {
                    refCardIds.Add(token);
                }
            }
        }

        return refCardIds;
    }
}
