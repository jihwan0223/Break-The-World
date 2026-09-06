using System;
using System.Collections.Generic;
using UnityEngine;

// 업그레이드 데이터(이름/설명/횟수/효과/비용)는 각 노드의 UpgradeNodeUI 컴포넌트에 직접 적어둠 - 이 매니저는
// 씬에 있는 UpgradeNodeUI들을 전부 찾아 읽어서 레벨/구매 로직만 담당함. 선행조건은 UpgradeTreeLink가
// 어떤 노드에서 어떤 노드로 이어지는지를 보고 자동으로 정해짐 (선 하나 그으면 그게 곧 선행 설정).
public class UpgradeManager : MonoBehaviour
{
    // 씬 어디서든 UpgradeManager.Instance로 접근하기 위한 싱글톤
    public static UpgradeManager Instance { get; private set; }

    // 업그레이드가 실제로 건드리는 게임 수치의 종류
    public enum UpgradeEffect
    {
        [InspectorName("클릭 데미지")] ClickDamage,               // 클릭 데미지 +값. Target Weapon Name 비우면 전역, 채우면 그 무기 장착 중일 때만
        [InspectorName("치명타 확률")] CritChance,                 // 크리티컬 확률 +값 % (기본 10% 위에 더함)
        [InspectorName("치명타 데미지")] CritDamage,               // 크리티컬 배율 +값 % (기본 150% 위에 더함)
        [InspectorName("자동클릭 해금")] AutoClickUnlock,          // 자동 클릭 기능 해금 (레벨 1이면 켜짐)
        [InspectorName("자동클릭 주기 단축")] AutoClickSpeed,      // 자동 클릭 주기 -값 초
        [InspectorName("자동클릭 횟수 증가")] AutoClickCount,      // 자동 클릭 1회당 +값 번
        [InspectorName("콤보 해금")] ComboUnlock,                  // 콤보 기능 해금
        [InspectorName("콤보 쿨타임 감소")] ComboCooldown,         // 콤보 쿨타임 -값 초
        [InspectorName("콤보 지속시간 증가")] ComboDuration,       // 콤보 지속시간 +값 초
        [InspectorName("럭키 클릭 해금")] LuckyClick,              // 럭키 클릭 해금
        [InspectorName("더블 클릭 해금")] DoubleClick,             // 더블 클릭 해금
        [InspectorName("자동 채굴 해금")] AutoMineUnlock,          // 자동 채굴 해금
        [InspectorName("자동 채굴 주기 단축")] AutoMineSpeed,      // 자동 채굴 주기 -값 초
    }

    // 선행 노드와 잇는 선의 모양 (UpgradeTreeLink가 이 값을 보고 세그먼트를 배치함)
    public enum LinkRouting
    {
        Straight,            // 직선
        ElbowVerticalFirst,  // ㄱ자: 세로로 내려간 뒤 가로
        ElbowHorizontalFirst,// ㄴ자: 가로로 간 뒤 세로
        Stepped,             // 계단(Z): 중간에서 한 번 꺾어 3세그먼트
    }

    // 효과 계산에 쓰는 기준값들
    [Header("기준값")]
    [SerializeField] private float baseCritChance = 0.1f;        // 크리티컬 기본 확률 (CritDamage 노드가 1레벨 이상일 때만 적용)
    [SerializeField] private float baseCritMultiplier = 1.5f;    // 크리티컬 기본 배율
    [SerializeField] private float baseAutoClickInterval = 5f;   // 자동 클릭 기본 주기(초)
    [SerializeField] private float minAutoClickInterval = 1.5f;  // 자동 클릭 주기 하한
    [SerializeField] private int baseAutoClickCount = 1;         // 자동 클릭 1회당 기본 클릭 수
    [SerializeField] private float baseComboCooldown = 30f;      // 콤보 기본 쿨타임(초)
    [SerializeField] private float minComboCooldown = 10f;       // 콤보 쿨타임 하한
    [SerializeField] private float baseComboDuration = 5f;       // 콤보 기본 지속시간(초)
    [SerializeField] private float luckyClickChanceValue = 0.001f; // 럭키 클릭 확률 (0.1%)
    [SerializeField] private float baseAutoMineInterval = 8f;    // 자동 채굴 기본 주기(초)
    [SerializeField] private float minAutoMineInterval = 2f;     // 자동 채굴 주기 하한
    [SerializeField] private int autoMineTierOffset = 3;         // 지금 캐는 오브젝트보다 몇 단계 전을 자동으로 캘지

    // ---- 씬의 UpgradeNodeUI를 읽어 구성한 실제 노드 ----

    // 노드 하나 = UpgradeNodeUI 하나. 세이브/트리/UI가 node.id(=오브젝트 이름)로 다룬다
    public class UpgradeNode
    {
        public string id;                 // UpgradeNodeUI.Id (오브젝트 이름) - 세이브 키
        public string displayName;
        public string description;
        public UpgradeEffect effect;
        public int targetObjectIndex;     // AutoClick 계열이 씀 - -1이면 전역
        public int targetWeaponIndex;     // ClickDamage가 씀 - -1이면 전역
        public int maxLevel;
        public string prerequisiteId;     // 이 노드가 공개되려면 필요한 다른 노드 id (빈칸 = 루트) - 들어오는 링크로 결정됨
        public float[] valuePerLevel;
        public (int objectIndex, long baseAmount, float levelGrowth)[] costs; // 조각 종류별 비용 공식 (여러 종류 동시 가능)

        // 이 노드의 level(1..maxLevel)에서 "그 레벨을 올렸을 때 추가되는" 효과 값
        public float ValueAtLevel(int level)
        {
            if (valuePerLevel == null || valuePerLevel.Length == 0) return 0f;
            int idx = Mathf.Clamp(level - 1, 0, valuePerLevel.Length - 1); // 배열이 짧으면 마지막 값 반복
            return valuePerLevel[idx];
        }

        // currentLevel(0..maxLevel-1)에서 다음 레벨로 올리는 비용. 이미 최대면 null.
        // 조각 종류마다 한 항목씩 - CurrencyManager가 전부 있는지 확인하고 한번에 차감함
        public PieceCost[] CostForLevel(int currentLevel)
        {
            if (currentLevel >= maxLevel) return null;
            if (costs == null || costs.Length == 0) return System.Array.Empty<PieceCost>();

            var result = new PieceCost[costs.Length];
            for (int i = 0; i < costs.Length; i++)
            {
                float levelFactor = Mathf.Pow(costs[i].levelGrowth, currentLevel);
                long amount = (long)Mathf.Max(1f, costs[i].baseAmount * levelFactor);
                result[i] = new PieceCost(costs[i].objectIndex, amount);
            }
            return result;
        }
    }

    private readonly List<UpgradeNode> _nodes = new List<UpgradeNode>(); // 씬을 훑어 구성한 전체 노드 (Awake에서 구성)
    private readonly Dictionary<string, UpgradeNode> _nodeById = new Dictionary<string, UpgradeNode>();
    private readonly Dictionary<string, int> _levels = new Dictionary<string, int>(); // node.id -> 현재 레벨 (없으면 0)

    // 어떤 노드의 레벨이 바뀔 때마다 (node.id, 새 레벨) 전달 - 트리 UI, SaveManager 등이 구독
    public event Action<string, int> OnUpgradeChanged;

    public IReadOnlyList<UpgradeNode> Nodes => _nodes; // 세이브 등이 순회함

    void Awake()
    {
        // 씬에 UpgradeManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildNodes();
    }

    // 씬의 모든 UpgradeNodeUI/UpgradeTreeLink를 읽어 _nodes를 구성.
    // 선행관계는 "이 노드로 들어오는 링크의 시작 노드"로 결정함 - 별도 prereq 필드를 안 써도 됨
    private void BuildNodes()
    {
        _nodes.Clear();
        _nodeById.Clear();

        var nodeUis = FindObjectsByType<UpgradeNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var links = FindObjectsByType<UpgradeTreeLink>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var prereqByToRect = new Dictionary<RectTransform, UpgradeNodeUI>();
        foreach (UpgradeTreeLink link in links)
        {
            if (link.FromNode == null || link.ToNode == null) continue;
            UpgradeNodeUI fromUi = link.FromNode.GetComponent<UpgradeNodeUI>();
            if (fromUi != null) prereqByToRect[link.ToNode] = fromUi;
        }

        foreach (UpgradeNodeUI ui in nodeUis)
        {
            prereqByToRect.TryGetValue(ui.Rect, out UpgradeNodeUI prereqUi);

            int targetObjectIndex = ObjectManager.StaticIndexOfName(ui.TargetObjectName);

            var node = new UpgradeNode
            {
                id = ui.Id,
                displayName = ui.DisplayName,
                description = ui.Description,
                effect = ui.Effect,
                targetObjectIndex = targetObjectIndex,
                targetWeaponIndex = WeaponManager.StaticIndexOfWeaponName(ui.TargetWeaponName),
                maxLevel = Mathf.Max(1, ui.MaxLevel),
                prerequisiteId = prereqUi != null ? prereqUi.Id : null,
                valuePerLevel = ui.ValuePerLevel,
                costs = ResolveCosts(ui.Costs, targetObjectIndex),
            };

            _nodes.Add(node);
            _nodeById[node.id] = node;
        }
    }

    // UpgradeNodeUI의 비용 줄들을 조각 인덱스로 확정. 조각 이름 비우면 대상 오브젝트, 그것도 없으면 0번
    private static (int, long, float)[] ResolveCosts(UpgradeNodeUI.NodeCost[] uiCosts, int targetObjectIndex)
    {
        if (uiCosts == null || uiCosts.Length == 0) return System.Array.Empty<(int, long, float)>();

        var result = new (int, long, float)[uiCosts.Length];
        for (int i = 0; i < uiCosts.Length; i++)
        {
            int byName = ObjectManager.StaticIndexOfName(uiCosts[i].objectName);
            int idx = byName >= 0 ? byName : (targetObjectIndex >= 0 ? targetObjectIndex : 0);
            result[i] = (idx, System.Math.Max(1L, uiCosts[i].baseAmount), uiCosts[i].levelGrowth);
        }
        return result;
    }

    // ---- 노드 단위 조회/구매 (트리 UI가 사용) ----

    public UpgradeNode GetNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;
        return _nodeById.TryGetValue(nodeId, out UpgradeNode node) ? node : null;
    }

    public int GetLevel(string nodeId) => _levels.TryGetValue(nodeId, out int level) ? level : 0;

    public int GetMaxLevel(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        return node != null ? node.maxLevel : 0;
    }

    public string GetDisplayName(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        return node != null ? node.displayName : "";
    }

    public string GetDescription(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        return node != null ? node.description : "";
    }

    // 다음 레벨 비용 (없거나 최대면 null)
    public PieceCost[] GetNextCost(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        return node?.CostForLevel(GetLevel(nodeId));
    }

    // 이 노드가 트리에 공개(보이고 구매 가능)됐는지 - 선행이 1레벨 이상이면 열림
    public bool IsRevealed(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        if (node == null) return false;
        if (string.IsNullOrEmpty(node.prerequisiteId)) return true; // 루트

        return GetLevel(node.prerequisiteId) >= 1;
    }

    // 조각을 소모해서 한 레벨 올림. 실패(미공개/최대레벨/조각부족) 시 false
    public bool TryUpgrade(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        if (node == null) return false;

        if (!IsRevealed(nodeId))
        {
            Debug.Log($"{nodeId} 업그레이드는 아직 잠겨있음 (선행 조건 미충족)");
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

    // 저장 파일 로드 시 구매 로직 없이 레벨을 그대로 대입 (없는 id는 무시)
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

    // 테스트용 - 모든 업그레이드 레벨을 0으로 (조각 환불은 없음)
    public void ResetAll()
    {
        foreach (UpgradeNode node in _nodes)
        {
            _levels[node.id] = 0;
            OnUpgradeChanged?.Invoke(node.id, 0);
        }
    }

    // ---- 효과 값 (Click.cs, ComboManager 등에서 사용) ----

    // 특정 효과의 "구매한 레벨들의 값 총합" (선택적으로 대상 오브젝트가 일치하는 노드만)
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

    // 해금류 - 그 효과를 가진 노드 중 하나라도 1레벨 이상이면 true (objectIndexFilter 주면 그 오브젝트 대상 노드만 봄)
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

    // 장착한 무기로 클릭할 때의 데미지 보너스. 노드의 Target Weapon Name이 비어있으면 전역이라 항상 합산되고,
    // 채워져 있으면 그 무기를 지금 장착 중일 때만 합산됨 (전역/특정무기 강화를 같은 Effect로 다룸)
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
        return Mathf.RoundToInt(total);
    }

    public float CritChanceValue
    {
        get
        {
            if (SumEffectRawCount(UpgradeEffect.CritDamage) <= 0) return 0f; // 크리티컬 강화를 한 번도 안 샀으면 크리티컬 자체가 발동 안 함
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

    // "그 효과 노드들의 레벨 합" - CritDamage를 한 번이라도 올렸는지 판단용
    private int SumEffectRawCount(UpgradeEffect effect)
    {
        int total = 0;
        foreach (UpgradeNode node in _nodes)
            if (node.effect == effect) total += GetLevel(node.id);
        return total;
    }

    // 자동클릭은 오브젝트별로 따로 켜짐 - 지금 장착 중인 오브젝트를 대상으로 하는 노드가 있어야 작동함
    public bool AutoClickIsUnlockedFor(int objectIndex) => AnyUnlocked(UpgradeEffect.AutoClickUnlock, objectIndex);

    public float AutoClickIntervalSecondsFor(int objectIndex) =>
        Mathf.Max(minAutoClickInterval, baseAutoClickInterval - SumEffect(UpgradeEffect.AutoClickSpeed, objectIndex));

    public int AutoClickClicksPerTriggerFor(int objectIndex) =>
        baseAutoClickCount + Mathf.RoundToInt(SumEffect(UpgradeEffect.AutoClickCount, objectIndex));

    public bool ComboIsUnlocked => AnyUnlocked(UpgradeEffect.ComboUnlock);

    public float ComboCooldownSeconds =>
        Mathf.Max(minComboCooldown, baseComboCooldown - SumEffect(UpgradeEffect.ComboCooldown));

    public float ComboDurationSeconds =>
        baseComboDuration + SumEffect(UpgradeEffect.ComboDuration);

    public bool LuckyClickIsUnlocked => AnyUnlocked(UpgradeEffect.LuckyClick);
    public float LuckyClickChance => LuckyClickIsUnlocked ? luckyClickChanceValue : 0f;

    public bool DoubleClickIsUnlocked => AnyUnlocked(UpgradeEffect.DoubleClick);

    public bool AutoMineIsUnlocked => AnyUnlocked(UpgradeEffect.AutoMineUnlock);
    public int AutoMineTierOffset => autoMineTierOffset;

    public float AutoMineIntervalSeconds =>
        Mathf.Max(minAutoMineInterval, baseAutoMineInterval - SumEffect(UpgradeEffect.AutoMineSpeed));
}
