using System;
using System.Collections.Generic;
using UnityEngine;

// 업그레이드를 "코드에 박힌 enum"이 아니라 "인스펙터에서 짜는 템플릿"으로 다루는 매니저.
// 템플릿 하나 = 같은 업그레이드를 트리에 여러 번(repeatCount) 펼치는 설계도이고,
// 펼쳐진 각 tier가 실제 노드(UpgradeNode) 하나가 됨. 노드 id로 레벨/비용/공개여부를 조회한다.
//  - 같은 종류의 업그레이드를 몇 개 만들든(돌려쓰든) templates 데이터만 늘리면 됨
//  - 새로운 "효과 종류"를 추가할 때만 UpgradeEffect enum + 아래 효과 게터에 코드 한 줄 추가
public class UpgradeManager : MonoBehaviour
{
    // 씬 어디서든 UpgradeManager.Instance로 접근하기 위한 싱글톤
    public static UpgradeManager Instance { get; private set; }

    // 업그레이드가 실제로 건드리는 게임 수치의 종류
    public enum UpgradeEffect
    {
        ClickDamage,       // 전역 클릭 데미지 +값 (정수 반올림)
        ClickDamageObject, // targetObjectName 오브젝트를 장착하고 클릭할 때만 데미지 +값
        CritChance,        // 크리티컬 확률 +값 % (기본 10% 위에 더함)
        CritDamage,        // 크리티컬 배율 +값 % (기본 150% 위에 더함)
        AutoClickUnlock,   // 자동 클릭 기능 해금 (레벨 1이면 켜짐)
        AutoClickSpeed,    // 자동 클릭 주기 -값 초
        AutoClickCount,    // 자동 클릭 1회당 +값 번
        ComboUnlock,       // 콤보 기능 해금
        ComboCooldown,     // 콤보 쿨타임 -값 초
        ComboDuration,     // 콤보 지속시간 +값 초
        LuckyClick,        // 럭키 클릭 해금
        DoubleClick,       // 더블 클릭 해금
        AutoMineUnlock,    // 자동 채굴 해금
        AutoMineSpeed,     // 자동 채굴 주기 -값 초
    }

    // 선행 노드와 잇는 선의 모양 (UpgradeTreeLink가 이 값을 보고 세그먼트를 배치함)
    public enum LinkRouting
    {
        Straight,            // 직선
        ElbowVerticalFirst,  // ㄱ자: 세로로 내려간 뒤 가로
        ElbowHorizontalFirst,// ㄴ자: 가로로 간 뒤 세로
        Stepped,             // 계단(Z): 중간에서 한 번 꺾어 3세그먼트
    }

    // ---- 인스펙터에서 작성하는 데이터 ----

    [Serializable]
    public class CostEntry
    {
        public string costObjectName; // 비용으로 낼 조각의 오브젝트 이름 (빈칸이면 대상 오브젝트, 그것도 없으면 0번)
        public long baseCost = 10;    // tier1의 레벨0->1 비용
    }

    [Serializable]
    public class PieceCostConfig
    {
        public CostEntry[] entries = { new CostEntry() }; // 동시에 요구하는 조각들 (보통 1개)
        public float levelGrowth = 2f;  // tier 안에서 레벨이 오를 때마다 비용에 곱해지는 배율
        public float tierGrowth = 4f;   // tier가 올라갈 때마다 비용에 곱해지는 배율
    }

    [Serializable]
    public class UpgradeTemplate
    {
        public string id;                        // 고유 식별자(영문 권장) - 세이브 키의 베이스. 예: "click_dmg", "click_dmg_glass"
        public string displayName;               // 노드에 표시할 이름 (돌려써도 같은 이름 그대로)
        public UpgradeEffect effect;             // 이 업그레이드가 건드리는 수치
        public string targetObjectName;          // ClickDamageObject 등에서 대상 오브젝트 (ObjectData.objectName과 일치). 전역이면 빈칸
        [Min(1)] public int repeatCount = 1;     // 이 템플릿을 트리에 몇 tier로 펼칠지 (돌려쓰기 횟수)
        [Min(1)] public int levelsPerTier = 5;   // tier 하나의 최대 레벨 (해금류는 1)
        public float[] valuePerLevel = { 1f };   // 레벨별 효과 값(증가량). 배열이 짧으면 마지막 값을 반복 사용
        public float tierValueMultiplier = 1f;   // tier가 올라갈 때마다 효과 값에 곱해지는 배율 (돌려쓸수록 세지게)
        public string prerequisiteId;            // 선행 템플릿 id (그 템플릿 마지막 tier가 조건을 만족하면 이 템플릿 tier1 공개). 빈칸 = 루트
        public bool requirePrereqMaxed;          // true면 선행 tier를 "끝까지" 채워야 다음이 열림. (돌려쓰기 tier 사이는 이 옵션과 무관하게 항상 최대레벨 요구)
        public PieceCostConfig cost = new PieceCostConfig();
        public Vector2 nodeOffset;               // 트리 자동배치 시 기준 위치에서의 미세조정 (에디터에서 노드를 직접 드래그해도 됨)
        public LinkRouting linkRouting = LinkRouting.Straight; // 선행 노드와 잇는 선 모양
    }

    [SerializeField] private UpgradeTemplate[] templates; // 여기에 업그레이드를 채운다. 비어있으면 아래 예시 템플릿으로 자동 대체됨
    [SerializeField] private bool useExampleTemplatesWhenEmpty = true; // templates가 비었을 때 예시로 게임을 돌려볼지

    // 효과 계산에 쓰는 기준값들 (예전엔 const였지만 인스펙터에서 조절할 수 있게 필드로 뺌)
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

    // ---- 템플릿을 펼쳐 만든 실제 노드 ----

    // tier 하나 = 노드 하나. 세이브/트리/UI가 node.id로 다룬다
    public class UpgradeNode
    {
        public string id;                 // "{template.id}#{tier}" - 세이브 키이자 트리 노드 식별자
        public string displayName;
        public UpgradeEffect effect;
        public int targetObjectIndex;     // -1이면 전역
        public int tier;                  // 1부터
        public int maxLevel;
        public string prerequisiteId;     // 이 노드가 공개되려면 필요한 다른 노드 id (빈칸 = 루트)
        public bool prerequisiteMustBeMaxed;
        public LinkRouting linkRouting;
        public Vector2 nodeOffset;
        public UpgradeTemplate template;  // 값/비용 계산에 참조

        // 이 노드의 level(1..maxLevel)에서 "그 레벨을 올렸을 때 추가되는" 효과 값
        public float ValueAtLevel(int level)
        {
            float[] v = template.valuePerLevel;
            if (v == null || v.Length == 0) return 0f;

            int idx = Mathf.Clamp(level - 1, 0, v.Length - 1); // 배열이 짧으면 마지막 값 반복
            float tierScale = Mathf.Pow(Mathf.Max(0.0001f, template.tierValueMultiplier), tier - 1);
            return v[idx] * tierScale;
        }

        // currentLevel(0..maxLevel-1)에서 다음 레벨로 올리는 비용. 이미 최대면 null
        public PieceCost[] CostForLevel(int currentLevel)
        {
            if (currentLevel >= maxLevel) return null;

            PieceCostConfig c = template.cost;
            if (c == null || c.entries == null || c.entries.Length == 0) return Array.Empty<PieceCost>();

            float levelFactor = Mathf.Pow(c.levelGrowth, currentLevel);
            float tierFactor = Mathf.Pow(c.tierGrowth, tier - 1);

            var result = new List<PieceCost>(c.entries.Length);
            foreach (CostEntry e in c.entries)
            {
                int costIndex = ResolveCostObjectIndex(e.costObjectName, targetObjectIndex);
                long amount = (long)Mathf.Max(1f, e.baseCost * levelFactor * tierFactor);
                result.Add(new PieceCost(costIndex, amount));
            }
            return result.ToArray();
        }
    }

    private readonly List<UpgradeNode> _nodes = new List<UpgradeNode>(); // 펼쳐진 전체 노드 (Awake에서 구성)
    private readonly Dictionary<string, UpgradeNode> _nodeById = new Dictionary<string, UpgradeNode>();
    private readonly Dictionary<string, int> _levels = new Dictionary<string, int>(); // node.id -> 현재 레벨 (없으면 0)

    // 어떤 노드의 레벨이 바뀔 때마다 (node.id, 새 레벨) 전달 - 트리 UI, SaveManager 등이 구독
    public event Action<string, int> OnUpgradeChanged;

    public IReadOnlyList<UpgradeNode> Nodes => _nodes; // 트리 자동생성/세이브가 순회함

    // 에디터에서(플레이 안 하고) 트리를 생성할 때 노드 목록이 필요함 - 그 자리에서 templates를 펼쳐 반환.
    // 레벨 상태는 건드리지 않으므로 플레이 중 호출해도 안전
    public IReadOnlyList<UpgradeNode> GetNodesForEditor()
    {
        BuildNodes();
        return _nodes;
    }

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

    // templates(또는 예시)를 펼쳐 _nodes를 구성. 오브젝트 이름은 여기서 인덱스로 확정함
    private void BuildNodes()
    {
        _nodes.Clear();
        _nodeById.Clear();

        UpgradeTemplate[] source = (templates != null && templates.Length > 0)
            ? templates
            : (useExampleTemplatesWhenEmpty ? BuildExampleTemplates() : Array.Empty<UpgradeTemplate>());

        foreach (UpgradeTemplate t in source)
        {
            if (t == null || string.IsNullOrEmpty(t.id)) continue;

            int targetIndex = ObjectManager.StaticIndexOfName(t.targetObjectName); // 전역이면 -1
            int tiers = Mathf.Max(1, t.repeatCount);

            for (int tier = 1; tier <= tiers; tier++)
            {
                var node = new UpgradeNode
                {
                    id = $"{t.id}#{tier}",
                    displayName = t.displayName,
                    effect = t.effect,
                    targetObjectIndex = targetIndex,
                    tier = tier,
                    maxLevel = Mathf.Max(1, t.levelsPerTier),
                    template = t,
                    linkRouting = t.linkRouting,
                    nodeOffset = t.nodeOffset,
                    // tier2 이상은 같은 템플릿의 이전 tier가 최대레벨이어야 공개. tier1은 다른 템플릿을 선행으로
                    prerequisiteId = tier > 1 ? $"{t.id}#{tier - 1}" : (string.IsNullOrEmpty(t.prerequisiteId) ? null : ResolveLastTierId(source, t.prerequisiteId)),
                    prerequisiteMustBeMaxed = tier > 1 || t.requirePrereqMaxed,
                };

                _nodes.Add(node);
                _nodeById[node.id] = node;
            }
        }
    }

    // 선행 템플릿 id("click_dmg")를 그 템플릿의 마지막 tier 노드 id("click_dmg#2")로 바꿔줌
    private static string ResolveLastTierId(UpgradeTemplate[] source, string prerequisiteTemplateId)
    {
        foreach (UpgradeTemplate t in source)
        {
            if (t != null && t.id == prerequisiteTemplateId)
                return $"{t.id}#{Mathf.Max(1, t.repeatCount)}";
        }
        Debug.LogWarning($"UpgradeManager: prerequisiteId '{prerequisiteTemplateId}' 에 해당하는 템플릿이 없음 (오타 확인). 이 노드는 트리에서 안 열림");
        return $"{prerequisiteTemplateId}#1";
    }

    // 비용 조각 오브젝트 인덱스 결정: 명시된 이름 > 대상 오브젝트 > 0번
    private static int ResolveCostObjectIndex(string costObjectName, int targetObjectIndex)
    {
        int byName = ObjectManager.StaticIndexOfName(costObjectName);
        if (byName >= 0) return byName;
        if (targetObjectIndex >= 0) return targetObjectIndex;
        return 0;
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

    // 다음 레벨 비용 (없거나 최대면 null)
    public PieceCost[] GetNextCost(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        return node?.CostForLevel(GetLevel(nodeId));
    }

    // 이 노드가 트리에 공개(보이고 구매 가능)됐는지
    public bool IsRevealed(string nodeId)
    {
        UpgradeNode node = GetNode(nodeId);
        if (node == null) return false;
        if (string.IsNullOrEmpty(node.prerequisiteId)) return true; // 루트

        int prereqLevel = GetLevel(node.prerequisiteId);
        if (node.prerequisiteMustBeMaxed)
        {
            UpgradeNode pn = GetNode(node.prerequisiteId);
            return pn != null && prereqLevel >= pn.maxLevel;
        }
        return prereqLevel >= 1;
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

    // 해금류 - 그 효과를 가진 노드 중 하나라도 1레벨 이상이면 true
    private bool AnyUnlocked(UpgradeEffect effect)
    {
        foreach (UpgradeNode node in _nodes)
            if (node.effect == effect && GetLevel(node.id) >= 1) return true;
        return false;
    }

    // 장착한 오브젝트를 클릭할 때의 데미지 보너스 (전역 + 그 오브젝트 대상 모두 합산)
    public int GetClickDamageBonus(int equippedObjectIndex)
    {
        float total = SumEffect(UpgradeEffect.ClickDamage);
        total += SumEffect(UpgradeEffect.ClickDamageObject, equippedObjectIndex);
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

    public bool AutoClickIsUnlocked => AnyUnlocked(UpgradeEffect.AutoClickUnlock);

    public float AutoClickIntervalSeconds =>
        Mathf.Max(minAutoClickInterval, baseAutoClickInterval - SumEffect(UpgradeEffect.AutoClickSpeed));

    public int AutoClickClicksPerTrigger =>
        baseAutoClickCount + Mathf.RoundToInt(SumEffect(UpgradeEffect.AutoClickCount));

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

    // ---- 예시 템플릿 ----

    // templates가 비었을 때 게임이 돌아가도록 넣어주는 최소 예시. 인스펙터 "예시 템플릿 채우기"로 이 내용을
    // templates 배열에 복사해서 편집 시작할 수 있음 (이건 어디까지나 출발점 - 실제 카탈로그는 직접 작성)
    // ponytail: 스캐폴딩. 실제 templates 카탈로그를 채우면 이 메서드 + useExampleTemplatesWhenEmpty 삭제
    private UpgradeTemplate[] BuildExampleTemplates()
    {
        return new[]
        {
            new UpgradeTemplate
            {
                id = "click_dmg", displayName = "클릭 데미지", effect = UpgradeEffect.ClickDamage,
                repeatCount = 3, levelsPerTier = 5, valuePerLevel = new[] { 1f, 2f, 3f, 4f, 5f },
                tierValueMultiplier = 4f,
                cost = new PieceCostConfig { entries = new[] { new CostEntry { baseCost = 10 } }, levelGrowth = 2.2f, tierGrowth = 30f },
            },
            new UpgradeTemplate
            {
                id = "crit_dmg", displayName = "크리티컬 데미지", effect = UpgradeEffect.CritDamage,
                repeatCount = 2, levelsPerTier = 5, valuePerLevel = new[] { 10f, 20f, 30f, 40f, 50f },
                tierValueMultiplier = 1.5f, prerequisiteId = "click_dmg",
                cost = new PieceCostConfig { entries = new[] { new CostEntry { baseCost = 50 } }, levelGrowth = 2.3f, tierGrowth = 40f },
            },
            new UpgradeTemplate
            {
                id = "crit_chance", displayName = "크리티컬 확률", effect = UpgradeEffect.CritChance,
                repeatCount = 2, levelsPerTier = 5, valuePerLevel = new[] { 5f, 6f, 7f, 8f, 9f },
                prerequisiteId = "crit_dmg",
                cost = new PieceCostConfig { entries = new[] { new CostEntry { baseCost = 150 } }, levelGrowth = 2.5f, tierGrowth = 25f },
            },
            new UpgradeTemplate
            {
                id = "autoclick", displayName = "자동 클릭", effect = UpgradeEffect.AutoClickUnlock,
                repeatCount = 1, levelsPerTier = 1, valuePerLevel = new[] { 1f }, prerequisiteId = "crit_chance",
                cost = new PieceCostConfig { entries = new[] { new CostEntry { baseCost = 3000 } } },
            },
            new UpgradeTemplate
            {
                id = "autoclick_spd", displayName = "자동 클릭 속도", effect = UpgradeEffect.AutoClickSpeed,
                repeatCount = 2, levelsPerTier = 5, valuePerLevel = new[] { 0.4f, 0.8f, 1.2f, 1.6f, 2f },
                prerequisiteId = "autoclick",
                cost = new PieceCostConfig { entries = new[] { new CostEntry { baseCost = 500 } }, levelGrowth = 2f, tierGrowth = 20f },
            },
            new UpgradeTemplate
            {
                id = "combo", displayName = "콤보", effect = UpgradeEffect.ComboUnlock,
                repeatCount = 1, levelsPerTier = 1, valuePerLevel = new[] { 1f }, prerequisiteId = "click_dmg",
                cost = new PieceCostConfig { entries = new[] { new CostEntry { baseCost = 25000 } } },
            },
        };
    }

#if UNITY_EDITOR
    // 인스펙터 우클릭 -> "예시 템플릿 채우기" : templates가 비어있을 때 출발점을 넣어줌
    [ContextMenu("예시 템플릿 채우기")]
    private void FillExampleTemplates()
    {
        templates = BuildExampleTemplates();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
