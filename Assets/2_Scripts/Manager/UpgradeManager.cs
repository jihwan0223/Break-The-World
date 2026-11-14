using System;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    public enum UpgradeEffect
    {
        [InspectorName("클릭 데미지")] ClickDamage, // +값, 무기 이름을 비우면 전역
        [InspectorName("치명타 확률")] CritChance, // +값 %
        [InspectorName("치명타 데미지")] CritDamage, // +값 %
        [InspectorName("자동클릭 해금")] AutoClickUnlock,
        [InspectorName("자동클릭 주기 단축")] AutoClickSpeed, // -값 초
        [InspectorName("자동클릭 횟수 증가")] AutoClickCount, // +값 번
        [InspectorName("(사용 안 함) 콤보 해금")] ComboUnlock, // 사용 안 함 - 번호 유지용 자리
        [InspectorName("(사용 안 함) 콤보 쿨타임")] ComboCooldown,
        [InspectorName("(사용 안 함) 콤보 지속시간")] ComboDuration,
        [InspectorName("슈퍼 크리티컬 해금")] LuckyClick,
        [InspectorName("더블 클릭 해금")] DoubleClick,
        [InspectorName("자동 채굴 해금")] AutoMineUnlock,
        [InspectorName("자동 채굴 주기 단축")] AutoMineSpeed, // -값 초
        // enum은 int로 저장되니 새 값은 맨 뒤에만 추가
        [InspectorName("자동 채굴 획득량 증가")] AutoMineYield, // +값 배
        [InspectorName("(사용 안 함) 콤보 배율")] ComboMultiplier,
        [InspectorName("슈퍼 크리티컬 확률 증가")] LuckyClickChanceUp, // +값 %
        [InspectorName("더블클릭 확률 증가")] DoubleClickChanceUp, // +값 %
        [InspectorName("전역 조각 획득 증가")] PieceGainGlobal, // +값 %
        [InspectorName("오브젝트 조각 획득 증가")] PieceGainObject, // +기본 보상의 값 %, 최소 1개
        [InspectorName("오브젝트 2배 드랍 확률")] ObjectDoubleDrop, // +값 %
        [InspectorName("무기 처치 보너스 조각")] WeaponKillBonus, // +값 개
        [InspectorName("바닥 파편 최대 개수 증가")] GroundPieceCap, // +값 개
        [InspectorName("결정 확률 증가")] CrystalChance, // +값 %
        [InspectorName("결정 드랍량 증가")] CrystalAmount, // x2^값
        [InspectorName("자동 줍기 해금")] AutoSweepUnlock,
        [InspectorName("자동 줍기 주기 단축")] AutoSweepSpeed, // -값 초
        [InspectorName("자동 줍기 양 증가")] AutoSweepLayers, // +값 층 (화면엔 "양")
        [InspectorName("호버 줍기 해금")] HoverPickupUnlock,
        [InspectorName("존 자동 파괴 해금")] ZoneAutoBreakUnlock,
        [InspectorName("오브젝트 추가 배치")] ObjectExtraCopies, // +값 개, 대상을 비우면 전체
        [InspectorName("커서 범위 증가")] CursorRange, // +값 월드 유닛
        [InspectorName("커서 타격 해금")] HoverClickUnlock,
        [InspectorName("조각 합치기 (표시용)")] PieceMergeDecoy, // 효과 없음 (표시용)
        [InspectorName("왁스 코팅 해금")] WaxCoatUnlock,
        [InspectorName("커서 줍기 양 증가")] HoverPickupLayers, // +값 층 (화면엔 "양")
        [InspectorName("통계 창 해금")] StatsUnlock,
        // 환생 창 (별조각)
        [InspectorName("환생: 모든 업그레이드 유지")] RebirthKeepAllUpgrades, // 환생해도 업그레이드/오브젝트 해금/획득량 유지
        [InspectorName("환생: 보스 클리어 유지")] RebirthKeepBosses,
        [InspectorName("환생: 무기 유지")] RebirthKeepWeapons,
        [InspectorName("(사용 안 함) 환생: 업그레이드 할인")] UnusedUpgradeDiscount, // 사용 안 함 - 번호 유지용 자리
        [InspectorName("환생: 별조각 획득 증가 (+n)")] StarShardGain, // +값 개
        [InspectorName("(사용 안 함) 환생: 시작 조각")] UnusedRebirthStartPieces, // 사용 안 함 - 번호 유지용 자리
        [InspectorName("환생: 오브젝트 해금 유지")] RebirthKeepObjects,
        [InspectorName("결정 획득량 증가 (+n)")] CrystalGainFlat, // +값 개
        [InspectorName("(사용 안 함) 한 번에 최대 레벨 구매 해금")] UnusedBulkBuyUnlock, // 사용 안 함 - 번호 유지용 자리
        [InspectorName("환생: 창 열어도 게임 진행")] WindowsKeepRunning,
        [InspectorName("환생: 무기 데미지 증가 (%)")] RebirthClickDamagePercent, // +값 %
        [InspectorName("환생: 자동클릭 속도 증가 (%)")] RebirthAutoClickSpeedPercent, // 주기 -값 %
        [InspectorName("환생: 보스 제한 시간 증가")] RebirthBossTime, // +값 초
        [InspectorName("(사용 안 함) 환생: 시작 무기")] UnusedRebirthStartWeapon, // 사용 안 함 - 번호 유지용 자리
        [InspectorName("환생: 무기 데미지 2배")] RebirthClickDamageDouble,
        [InspectorName("환생: 자동클릭 데미지 증가 (%)")] RebirthAutoClickDamagePercent, // +값 %
        [InspectorName("환생: 자동클릭 데미지 2배")] RebirthAutoClickDamageDouble,
        [InspectorName("환생: 줍는 조각 증가 (+n)")] PickupBonusPieces, // 바닥 조각 1개 주울 때마다 +값 개
        [InspectorName("자동클릭 데미지 (+n)")] AutoClickDamageFlat, // +값, 장착 무기 티어 배율 곱함
    }

    public enum LinkRouting
    {
        Straight,
        ElbowVerticalFirst,
        ElbowHorizontalFirst,
        Stepped,
    }

    [Header("기준값")]
    [SerializeField] private float baseCritChance = 0.1f; // 치명타 데미지를 1레벨 이상 사야 적용
    [SerializeField] private float baseCritMultiplier = 1.5f;
    [SerializeField] private float baseAutoClickInterval = 7f; // 초
    [SerializeField] private float minAutoClickInterval = 2.5f; // 초
    [SerializeField] private float autoClickIntervalGrowthPerObject = 1f;
    [SerializeField] private int baseAutoClickCount = 1;
    [SerializeField] private float luckyClickChanceValue = 0.001f; // 0.1%
    [SerializeField] private float baseDoubleClickChance = 0.25f;
    [SerializeField] private float baseAutoMineInterval = 8f; // 초
    [SerializeField] private float minAutoMineInterval = 2f; // 초
    [SerializeField] private int autoMineTierOffset = 3;
    [SerializeField] private int baseMaxGroundPieces = 400;
    [SerializeField] private float baseCrystalChance = 0.05f;
    [SerializeField] private int baseCrystalAmount = 1;
    [SerializeField] private float hoverPickupBaseRadius = 0.2f; // 월드 유닛
    [SerializeField] private float baseAutoSweepInterval = 20f; // 초
    [SerializeField] private float minAutoSweepInterval = 3f; // 초
    [SerializeField] private int baseAutoSweepLayers = 1;
    [SerializeField] private float hoverClickInterval = 0.7f; // 초

    public class UpgradeNode
    {
        public string id;
        public string displayName;
        public string description;
        public UpgradeEffect effect;
        public int targetObjectIndex; // -1 = 전역
        public int gateObjectIndex = -1; // -1 = 관문 없음
        public int targetWeaponIndex; // -1 = 전역
        public bool fullVersionOnly; // 데모에선 숨김
        public int maxLevel;
        public string prerequisiteId;
        public System.Func<bool> prerequisiteSatisfied; // null = 루트
        public float[] valuePerLevel;
        public (bool isCrystal, long baseAmount, float levelGrowth, long firstLevelAmount, long[] levelAmounts, bool isStarShard)[] costs;
        public float fullVersionCostMultiplier = 1f; // 풀버전에서만 비용에 곱함

        public float ValueAtLevel(int level)
        {
            if (valuePerLevel == null || valuePerLevel.Length == 0) return 0f;
            int idx = Mathf.Clamp(level - 1, 0, valuePerLevel.Length - 1); // 배열이 짧으면 마지막 값 반복
            return valuePerLevel[idx];
        }

        public PieceCost[] CostForLevel(int currentLevel)
        {
            if (currentLevel >= maxLevel) return null;
            if (costs == null || costs.Length == 0) return System.Array.Empty<PieceCost>();

            var result = new PieceCost[costs.Length];
            for (int i = 0; i < costs.Length; i++)
            {
                // long을 넘으면 음수가 되니 double로 계산 후 자름
                long[] fixedAmounts = costs[i].levelAmounts;
                double raw = fixedAmounts != null && fixedAmounts.Length > 0
                    ? fixedAmounts[Mathf.Min(currentLevel, fixedAmounts.Length - 1)]
                    : currentLevel == 0 && costs[i].firstLevelAmount > 0
                    ? costs[i].firstLevelAmount
                    : costs[i].baseAmount * System.Math.Pow(costs[i].levelGrowth, currentLevel);
                if (!GameSettings.IsDemo) raw *= fullVersionCostMultiplier;
                long amount = raw >= long.MaxValue ? long.MaxValue : (long)System.Math.Max(1d, raw);
                if (!costs[i].isCrystal && !costs[i].isStarShard) amount = NumberFormatUtil.RoundPrice(amount); // 결정, 별조각은 그대로
                result[i] = new PieceCost(amount, costs[i].isCrystal, costs[i].isStarShard);
            }
            return result;
        }
    }

    private readonly List<UpgradeNode> _nodes = new List<UpgradeNode>();
    private readonly Dictionary<string, UpgradeNode> _nodeById = new Dictionary<string, UpgradeNode>();
    private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();
    private ObjectEconomyNodeUI[] _economyNodes = System.Array.Empty<ObjectEconomyNodeUI>();

    public event Action<string, int> OnUpgradeChanged;

    public IReadOnlyList<UpgradeNode> Nodes => _nodes;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildNodes();
    }

    private void BuildNodes()
    {
        _nodes.Clear();
        _nodeById.Clear();

        var nodeUis = FindObjectsByType<UpgradeNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var links = FindObjectsByType<UpgradeTreeLink>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _economyNodes = FindObjectsByType<ObjectEconomyNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var prereqsByToRect = new Dictionary<RectTransform, List<(string id, System.Func<bool> satisfied)>>();
        foreach (UpgradeTreeLink link in links)
        {
            if (link.ToNode == null) continue;

            if (!prereqsByToRect.TryGetValue(link.ToNode, out List<(string id, System.Func<bool> satisfied)> list))
                prereqsByToRect[link.ToNode] = list = new List<(string, System.Func<bool>)>();

            foreach (RectTransform from in link.FromNodes)
            {
                UpgradeNodeUI fromUi = from.GetComponent<UpgradeNodeUI>();
                if (fromUi != null)
                {
                    string fromId = fromUi.Id;
                    list.Add((fromId, () => GetLevel(fromId) >= 1 && IsRevealed(fromId))); // 부모가 숨어 있으면 (환생 후 남은 노드 등) 같이 숨김
                    continue;
                }

                ObjectEconomyNodeUI fromEconomy = from.GetComponent<ObjectEconomyNodeUI>();
                if (fromEconomy != null)
                    list.Add((fromEconomy.name, () => fromEconomy.IsLeveled() && fromEconomy.IsRevealed())); // 부모가 숨어 있으면 같이 숨김
            }
        }

        foreach (UpgradeNodeUI ui in nodeUis)
        {
            prereqsByToRect.TryGetValue(ui.Rect, out List<(string id, System.Func<bool> satisfied)> prereqs);

            int targetObjectIndex = ObjectManager.StaticIndexOfName(ui.TargetObjectName);

            var node = new UpgradeNode
            {
                id = ui.Id,
                displayName = ui.DisplayName,
                description = ui.Description,
                effect = ui.Effect,
                targetObjectIndex = targetObjectIndex,
                gateObjectIndex = string.IsNullOrEmpty(ui.GateObjectName) ? -1 : ObjectManager.StaticIndexOfName(ui.GateObjectName),
                targetWeaponIndex = WeaponManager.StaticIndexOfWeaponName(ui.TargetWeaponName),
                fullVersionOnly = ui.FullVersionOnly,
                maxLevel = Mathf.Max(1, ui.MaxLevel),
                prerequisiteId = prereqs != null ? string.Join(", ", prereqs.ConvertAll(p => p.id)) : null,
                prerequisiteSatisfied = prereqs == null || prereqs.Count == 0 ? null
                    : ui.RequireAllPrereqs ? () => prereqs.TrueForAll(p => p.satisfied()) // AND
                    : () => prereqs.Exists(p => p.satisfied()), // OR
                valuePerLevel = ui.ValuePerLevel,
                costs = ResolveCosts(ui.Costs),
                fullVersionCostMultiplier = ui.FullVersionCostMultiplier,
            };

            _nodes.Add(node);
            _nodeById[node.id] = node;
        }
    }

    private static (bool, long, float, long, long[], bool)[] ResolveCosts(UpgradeNodeUI.NodeCost[] uiCosts)
    {
        if (uiCosts == null || uiCosts.Length == 0) return System.Array.Empty<(bool, long, float, long, long[], bool)>();

        var result = new (bool, long, float, long, long[], bool)[uiCosts.Length];
        for (int i = 0; i < uiCosts.Length; i++)
            result[i] = (uiCosts[i].useCrystals && !uiCosts[i].useStarShards, System.Math.Max(1L, uiCosts[i].baseAmount), uiCosts[i].levelGrowth, uiCosts[i].firstLevelAmount, uiCosts[i].levelAmounts, uiCosts[i].useStarShards);
        return result;
    }

    public UpgradeNode GetNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;
        return _nodeById.TryGetValue(nodeId, out UpgradeNode node) ? node : null;
    }

    public int GetLevel(string nodeId) => _levels.TryGetValue(nodeId, out int level) ? level : 0;
    public const string FirstNodeId = "0_달걀_드랍량_강화"; // 트리 가운데 첫 노드 (튜토리얼 / 첫 결정 확정 기준)
    public bool FirstUpgradeBought => GetLevel(FirstNodeId) >= 1; // 첫 업그레이드를 샀는지

    public PieceCost[] GetNextCost(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        return node?.CostForLevel(GetLevel(nodeId));
    }

    private static bool IsDemoHidden(UpgradeNode node) =>
        GameSettings.IsDemo && (node.fullVersionOnly || GameSettings.IsDemoLockedObject(node.targetObjectIndex) || GameSettings.IsDemoLockedWeapon(node.targetWeaponIndex));

    // 이번 버전(데모/풀)에 나오는 노드인지 (환생 조건 셀 때)
    public bool IsInThisVersion(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        return node != null && !IsDemoHidden(node);
    }

    public bool IsRevealed(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        if (node == null) return false;
        if (IsDemoHidden(node)) return false; // 데모에선 풀버전 내용 숨김
        if (node.prerequisiteSatisfied == null) return true;
        if (_revealDepth > 64) return true; // 링크가 고리로 이어져도 무한 재귀 안 함

        _revealDepth++;
        bool revealed = node.prerequisiteSatisfied();
        _revealDepth--;
        return revealed;
    }
    private int _revealDepth; // IsRevealed 재귀 깊이

    private bool IsTargetObjectLocked(UpgradeNode node) =>
        node.targetObjectIndex >= 0 && ObjectManager.Instance != null && !ObjectManager.Instance.IsUnlocked(node.targetObjectIndex);

    private bool IsTargetWeaponLocked(UpgradeNode node) =>
        node.targetWeaponIndex >= 0 && WeaponManager.Instance != null && !WeaponManager.Instance.IsUnlocked(node.targetWeaponIndex);

    public bool IsTargetLocked(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        if (node != null && node.gateObjectIndex >= 0) // 관문 노드는 관문 오브젝트만 봄
            return ObjectManager.Instance != null && !ObjectManager.Instance.IsUnlocked(node.gateObjectIndex);
        return node != null && (IsTargetObjectLocked(node) || IsTargetWeaponLocked(node));
    }

    public string LockedTargetHintText(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        if (node == null) return Loc.T("아직 해금할 수 없습니다");
        if (node.gateObjectIndex >= 0) return "???";

        if (IsTargetObjectLocked(node))
            return ObjectManager.Instance.NextRequiredUnlockHintText(node.targetObjectIndex);

        return Loc.T("{0} 무기 해금이 필요합니다", WeaponManager.StaticWeaponNameAt(node.targetWeaponIndex));
    }

    public bool CanUpgrade(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        if (node == null || CurrencyManager.Instance == null) return false;

        int level = GetLevel(nodeId);
        return level < node.maxLevel && IsRevealed(nodeId) && !IsTargetLocked(nodeId)
            && CurrencyManager.Instance.CanAfford(node.CostForLevel(level));
    }

    public bool AnyUpgradeAvailable(bool crystal)
    {
        foreach (UpgradeNode node in _nodes)
            if (!UsesStarShards(node.id) && UsesCrystals(node.id) == crystal && CanUpgrade(node.id)) return true; // 환생 노드는 업그레이드 버튼 알림에서 뺌

        if (!crystal)
            foreach (ObjectEconomyNodeUI node in _economyNodes)
                if (node != null && node.CanBuy()) return true;

        return false;
    }

    public bool UsesCrystals(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        if (node == null || node.costs == null) return false;
        foreach (var cost in node.costs)
            if (cost.isCrystal) return true;
        return false;
    }

    // 환생 창 노드 (별조각으로 삼)
    public bool UsesStarShards(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        if (node == null || node.costs == null) return false;
        foreach (var cost in node.costs)
            if (cost.isStarShard) return true;
        return false;
    }

    public bool TryUpgrade(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        if (node == null) return false;

        if (!IsRevealed(nodeId))
        {
            Debug.Log($"{nodeId} 업그레이드는 아직 잠겨있음 (선행 조건 미충족)");
            return false;
        }

        if (IsTargetLocked(nodeId))
        {
            Debug.Log($"{nodeId} 업그레이드는 대상 오브젝트/무기가 아직 해금되지 않아 구매 불가");
            return false;
        }

        int level = GetLevel(nodeId);
        if (level >= node.maxLevel)
        {
            Debug.Log($"{nodeId} 업그레이드는 이미 최대 레벨");
            return false;
        }

        PieceCost[] cost = node.CostForLevel(level);
        if (cost != null && cost.Length > 0)
        {
            if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendPieces(cost))
            {
                Debug.Log($"{nodeId} 업그레이드 조각 부족");
                return false;
            }
        }

        SetLevelInternal(nodeId, level + 1);
        return true;
    }

    public void SetLevel(string nodeId, int level)
    {
        UpgradeNode node = GetNode(nodeId);
        if (node == null) return;
        SetLevelInternal(nodeId, Mathf.Clamp(level, 0, node.maxLevel));
    }

    private void SetLevelInternal(string nodeId, int level)
    {
        _levels[nodeId] = level;
        OnUpgradeChanged?.Invoke(nodeId, level);
    }

    public void UnlockAllDebug()
    {
        foreach (UpgradeNode node in _nodes)
            SetLevelInternal(node.id, 1);
    }

    public void MaxAllDebug()
    {
        foreach (UpgradeNode node in _nodes)
            SetLevelInternal(node.id, node.maxLevel);
    }

    public void ResetAll()
    {
        foreach (UpgradeNode node in _nodes)
        {
            _levels[node.id] = 0;
            OnUpgradeChanged?.Invoke(node.id, 0);
        }
    }

    private float SumEffect(UpgradeEffect effect, int objectIndexFilter = int.MinValue)
    {
        float total = 0f;
        foreach (UpgradeNode node in _nodes)
        {
            if (node.effect != effect) continue;
            if (objectIndexFilter != int.MinValue && node.targetObjectIndex != objectIndexFilter) continue;

            int level = GetLevel(node.id);
            for (int L = 1; L <= level; L++)
                total += node.ValueAtLevel(L);
        }
        return total;
    }

    private bool AnyUnlocked(UpgradeEffect effect, int objectIndexFilter = int.MinValue)
    {
        foreach (UpgradeNode node in _nodes)
        {
            if (node.effect != effect) continue;
            if (objectIndexFilter != int.MinValue && node.targetObjectIndex != objectIndexFilter) continue;
            if (GetLevel(node.id) >= 1) return true;
        }
        return false;
    }

    public int GetClickDamageBonus(int equippedWeaponIndex)
    {
        float total = 0f;
        foreach (UpgradeNode node in _nodes)
        {
            if (node.effect != UpgradeEffect.ClickDamage) continue;
            if (node.targetWeaponIndex >= 0 && node.targetWeaponIndex != equippedWeaponIndex) continue;

            int level = GetLevel(node.id);
            for (int L = 1; L <= level; L++)
                total += node.ValueAtLevel(L);
        }
        // 노드 값에 장착 무기 티어 배율을 곱함
        return ClampToInt((double)total * ObjectHealthCalculator.ScaleOfTier(equippedWeaponIndex + 1));
    }

    public float CritChanceValue
    {
        get
        {
            if (SumEffectRawCount(UpgradeEffect.CritDamage) <= 0) return 0f; // 치명타 데미지를 한 번도 안 샀으면 치명타 없음
            float bonus = SumEffect(UpgradeEffect.CritChance) / 100f;
            return Mathf.Clamp01(baseCritChance + bonus);
        }
    }

    public float CritMultiplierValue
    {
        get
        {
            if (SumEffectRawCount(UpgradeEffect.CritDamage) <= 0) return 1f;
            return baseCritMultiplier + SumEffect(UpgradeEffect.CritDamage) / 100f;
        }
    }

    private int SumEffectRawCount(UpgradeEffect effect)
    {
        int total = 0;
        foreach (UpgradeNode node in _nodes)
            if (node.effect == effect) total += GetLevel(node.id);
        return total;
    }

    public bool AutoClickIsUnlockedFor(int objectIndex) => AnyUnlocked(UpgradeEffect.AutoClickUnlock, objectIndex);

    public float AutoClickIntervalSecondsFor(int objectIndex) =>
        Mathf.Max(minAutoClickInterval, baseAutoClickInterval - SumEffect(UpgradeEffect.AutoClickSpeed, objectIndex))
        * Mathf.Pow(autoClickIntervalGrowthPerObject, Mathf.Max(0, objectIndex))
        * (1f - Mathf.Clamp(SumEffect(UpgradeEffect.RebirthAutoClickSpeedPercent), 0f, 90f) / 100f); // 환생 노드는 최소 주기 아래로도 줄임

    public int AutoClickClicksPerTriggerFor(int objectIndex) =>
        baseAutoClickCount + Mathf.RoundToInt(SumEffect(UpgradeEffect.AutoClickCount, objectIndex));

    public bool LuckyClickIsUnlocked => AnyUnlocked(UpgradeEffect.LuckyClick);
    private const float MaxLuckyClickChance = 0.9f; // 슈퍼크리가 터지면 일반 치명타는 안 굴리니 10%는 남김
    // 확률 노드만 사도 바로 적용 (해금 노드 없이 확률 노드끼리 트리 아무 데나 둘 수 있게)
    public float LuckyClickChance =>
        Mathf.Min(MaxLuckyClickChance, (LuckyClickIsUnlocked ? luckyClickChanceValue : 0f) + SumEffect(UpgradeEffect.LuckyClickChanceUp) / 100f);

    public bool DoubleClickIsUnlocked => AnyUnlocked(UpgradeEffect.DoubleClick);
    public float DoubleClickChanceValue =>
        DoubleClickIsUnlocked ? Mathf.Clamp01(baseDoubleClickChance + SumEffect(UpgradeEffect.DoubleClickChanceUp) / 100f) : 0f;

    public bool AutoMineIsUnlocked => AnyUnlocked(UpgradeEffect.AutoMineUnlock);
    public int AutoMineTierOffset => autoMineTierOffset;

    public float AutoMineIntervalSeconds =>
        Mathf.Max(minAutoMineInterval, baseAutoMineInterval - SumEffect(UpgradeEffect.AutoMineSpeed));

    public int AutoMineYieldBonus => Mathf.RoundToInt(SumEffect(UpgradeEffect.AutoMineYield));

    public float PieceGainMultiplier() => 1f + SumEffect(UpgradeEffect.PieceGainGlobal) / 100f;

    // 기본 보상의 value%, 최소 1개
    public static double ObjectPieceBonusPerLevel(long baseReward, float valuePercent) =>
        System.Math.Max(1.0, baseReward * (double)valuePercent / 100.0);

    public double ObjectPieceBonus(int objectIndex, long baseReward)
    {
        double total = 0;
        foreach (UpgradeNode node in _nodes)
        {
            if (node.effect != UpgradeEffect.PieceGainObject || node.targetObjectIndex != objectIndex) continue;

            int level = GetLevel(node.id);
            for (int L = 1; L <= level; L++)
                total += ObjectPieceBonusPerLevel(baseReward, node.ValueAtLevel(L));
        }
        return total;
    }

    public float ObjectDoubleDropChance(int objectIndex) =>
        Mathf.Clamp01(SumEffect(UpgradeEffect.ObjectDoubleDrop, objectIndex) / 100f);

    public int MaxGroundPieces => baseMaxGroundPieces + Mathf.RoundToInt(SumEffect(UpgradeEffect.GroundPieceCap));

    public float CrystalDropChance => Mathf.Clamp01(baseCrystalChance + SumEffect(UpgradeEffect.CrystalChance) / 100f);

    public long CrystalDropAmount => (long)baseCrystalAmount << Mathf.Clamp(Mathf.RoundToInt(SumEffect(UpgradeEffect.CrystalAmount)), 0, 20); // 레벨마다 2배

    private static readonly int[] CrystalZoneMultipliers = { 1, 10, 100, 1000, 10000 }; // 맵1 x1 ~ 맵5 x10000
    public static int CrystalZoneMultiplier(int zone) => CrystalZoneMultipliers[Mathf.Clamp(zone, 0, CrystalZoneMultipliers.Length - 1)];

    // zone -1 = 지금 보는 존
    public long CrystalDropAmountIn(int zone)
    {
        if (zone < 0) zone = ZoneManager.Instance != null ? ZoneManager.Instance.CurrentZone : 0;
        long amount = CrystalDropAmount * CrystalZoneMultipliers[Mathf.Clamp(zone, 0, CrystalZoneMultipliers.Length - 1)]; // 결정 개수 (보너스 전)
        return amount + Mathf.RoundToInt(SumEffect(UpgradeEffect.CrystalGainFlat)); // 환생 노드: +n개
    }

    public bool AutoSweepIsUnlocked => AnyUnlocked(UpgradeEffect.AutoSweepUnlock);
    public bool ZoneAutoBreakIsUnlockedFor(int objectIndex) => AnyUnlocked(UpgradeEffect.ZoneAutoBreakUnlock, objectIndex);
    public bool WaxIsUnlockedFor(int objectIndex) => AnyUnlocked(UpgradeEffect.WaxCoatUnlock, objectIndex);
    public int ExtraObjectCopies(int objectIndex) =>
        Mathf.RoundToInt(SumEffect(UpgradeEffect.ObjectExtraCopies, -1) + SumEffect(UpgradeEffect.ObjectExtraCopies, objectIndex));
    public bool HoverPickupIsUnlocked => AnyUnlocked(UpgradeEffect.HoverPickupUnlock);
    // 월드 유닛, 0이면 커서 한 점
    public float CursorRadius => SumEffect(UpgradeEffect.CursorRange) + (HoverPickupIsUnlocked ? hoverPickupBaseRadius : 0f);
    public bool HoverClickIsUnlocked => AnyUnlocked(UpgradeEffect.HoverClickUnlock);
    public bool StatsIsUnlocked => AnyUnlocked(UpgradeEffect.StatsUnlock);
    public bool WindowsKeepRunningIsUnlocked => AnyUnlocked(UpgradeEffect.WindowsKeepRunning); // 설정·도감·무기 창을 열어도 안 멈춤
    public float HoverClickIntervalSeconds => Mathf.Max(0.02f, hoverClickInterval); // 0 이하로 넣어도 매 프레임은 안 때리게

    public float AutoSweepIntervalSeconds =>
        Mathf.Max(minAutoSweepInterval, baseAutoSweepInterval - SumEffect(UpgradeEffect.AutoSweepSpeed));

    // 1 = 맨 위층만
    // 0 = 맨 위 1개만
    public int HoverPickupLayerCount => Mathf.Clamp(Mathf.RoundToInt(SumEffect(UpgradeEffect.HoverPickupLayers)), 0, DebrisPool.LayerCount);

    public int AutoSweepLayers =>
        Mathf.Clamp(baseAutoSweepLayers + Mathf.RoundToInt(SumEffect(UpgradeEffect.AutoSweepLayers)), 1, DebrisPool.LayerCount);

    // 환생 창 효과
    // 무기 데미지 배율 (직접/자동 클릭 모두): % 증가 x 2배 노드
    public float ClickDamageMultiplier =>
        (1f + SumEffect(UpgradeEffect.RebirthClickDamagePercent) / 100f) * (AnyUnlocked(UpgradeEffect.RebirthClickDamageDouble) ? 2f : 1f);
    // 자동클릭에만 붙는 배율: % 증가 x 2배 노드
    public float AutoClickDamageMultiplier =>
        (1f + SumEffect(UpgradeEffect.RebirthAutoClickDamagePercent) / 100f) * (AnyUnlocked(UpgradeEffect.RebirthAutoClickDamageDouble) ? 2f : 1f);
    // 자동클릭 데미지 +n (클릭 데미지처럼 장착 무기 티어 배율을 곱함)
    public int GetAutoClickDamageBonus(int equippedWeaponIndex) =>
        ClampToInt((double)SumEffect(UpgradeEffect.AutoClickDamageFlat) * ObjectHealthCalculator.ScaleOfTier(equippedWeaponIndex + 1));
    // 데미지 계산용: int를 넘으면 음수로 뒤집히지 않고 int 최대값에서 멈춤
    public static int ClampToInt(double value) => value >= int.MaxValue ? int.MaxValue : value <= 0 ? 0 : (int)System.Math.Round(value);
    public float BossTimeBonus => SumEffect(UpgradeEffect.RebirthBossTime); // 초
    public long StarShardBonus => Mathf.RoundToInt(SumEffect(UpgradeEffect.StarShardGain)); // 환생할 때 별조각 +n
    public long PickupBonusPieces => Mathf.RoundToInt(SumEffect(UpgradeEffect.PickupBonusPieces)); // 바닥 조각 1개 주울 때 +n
    public bool KeepsAllUpgradesOnRebirth => AnyUnlocked(UpgradeEffect.RebirthKeepAllUpgrades);
    public bool KeepsObjectsOnRebirth => AnyUnlocked(UpgradeEffect.RebirthKeepObjects);
    public bool KeepsBossesOnRebirth => AnyUnlocked(UpgradeEffect.RebirthKeepBosses);
    public bool KeepsWeaponsOnRebirth => AnyUnlocked(UpgradeEffect.RebirthKeepWeapons);

    // 환생: 별조각 노드는 남기고 나머지 레벨을 0으로 (모든 업그레이드 유지면 그대로)
    public void ResetForRebirth()
    {
        if (KeepsAllUpgradesOnRebirth) return;
        foreach (UpgradeNode node in _nodes)
        {
            if (UsesStarShards(node.id) || GetLevel(node.id) == 0) continue;
            _levels[node.id] = 0;
            OnUpgradeChanged?.Invoke(node.id, 0);
        }
    }

    public long WeaponKillBonusPieces(int equippedWeaponIndex)
    {
        float total = 0f;
        foreach (UpgradeNode node in _nodes)
        {
            if (node.effect != UpgradeEffect.WeaponKillBonus) continue;
            if (node.targetWeaponIndex >= 0 && node.targetWeaponIndex != equippedWeaponIndex) continue;

            int level = GetLevel(node.id);
            for (int L = 1; L <= level; L++)
                total += node.ValueAtLevel(L);
        }
        return (long)total;
    }
}
