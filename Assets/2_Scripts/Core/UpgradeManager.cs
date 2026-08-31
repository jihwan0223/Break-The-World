using System;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    // 씬 어디서든 UpgradeManager.Instance로 접근하기 위한 싱글톤
    public static UpgradeManager Instance { get; private set; }

    // 업그레이드 종류. 순서를 바꾸면 안 됨 (SaveData.upgradeLevels 배열 인덱스와 대응)
    public enum UpgradeType
    {
        ClickDamage = 0, // 클릭 강화: 클릭 데미지 +N (루트)
        CritDamage = 1, // 크리티컬 강화: 크리티컬 데미지 배율 증가 (ClickDamage 자식)
        CritChanceUp = 2, // 크리티컬 확률 강화 (단일 - 초반이라 1/1까지만, CritDamage 자식)
        AutoClickUnlock = 3, // 자동클릭 (단일 - 이걸 사야 자동클릭 자체가 켜짐)
        AutoClickSpeed = 4, // 자동클릭 주기 단축 (AutoClickUnlock 자식)
        AutoClickCount = 5, // 자동클릭 1회당 클릭 수 증가 (AutoClickUnlock 자식)
        ComboUnlock = 6, // 콤보 (단일 - 이걸 사야 콤보 시스템 자체가 켜짐)
        ComboCooldown = 7, // 콤보 쿨타임 단축 (ComboUnlock 자식)
        ComboDuration = 8, // 콤보 지속시간 증가 (ComboUnlock 자식)
        LuckyClick = 9, // 럭키 클릭 (단일 - 0.1% 확률 즉시 파괴)
        DoubleClick = 10, // 더블클릭 (단일 - 클릭 한 번에 2번 처리)
        CritChanceUp2 = 11, // 크리티컬 확률 강화 2차 - "돌려쓰는" 후속 버전 (CritChanceUp 자식)
        ClickDamageUp2 = 12, // 클릭 강화 2차 - "돌려쓰는" 후속 버전 (AutoClickUnlock 자식)
        CritDamageUp2 = 13, // 크리티컬 강화 2차 - "돌려쓰는" 후속 버전 (AutoClickUnlock 자식)
        AutoMineUnlock = 14, // 자동채굴 (단일 - 현재 오브젝트보다 몇 단계 전 오브젝트를 자동으로 캐줌, AutoClickUnlock 자식)
        AutoMineSpeed = 15, // 자동채굴 주기 단축 (AutoMineUnlock 자식)
    }

    public const int UpgradeCount = 16; // 업그레이드 종류 수 (위 enum 개수와 일치해야 함)
    public const int MaxLevel = 5; // 여러 단계로 성장하는 업그레이드의 최대 단계 (단일 업그레이드는 GetMaxLevel에서 1로 별도 처리)

    private const float BaseCritChance = 0.1f; // 크리티컬 발동 확률(10%) - CritDamage가 1레벨 이상이면 항상 이 확률로 고정
    private const float BaseCritMultiplier = 1.5f; // CritDamage 1레벨 시점의 기본 크리티컬 배율 - 레벨마다 이 위에 값이 더해짐

    private const float BaseAutoClickIntervalSeconds = 5f; // 자동클릭 기본 주기(초) - AutoClickSpeed 레벨만큼 줄어듦
    private const float MinAutoClickIntervalSeconds = 1.5f; // 자동클릭 주기 하한
    private const int BaseAutoClickCount = 1; // 자동클릭 1회 발동 시 기본 클릭 횟수 - AutoClickCount 레벨만큼 늘어남

    private const float BaseComboCooldownSeconds = 30f; // 콤보 발동 간 기본 대기시간(초) - ComboCooldown 레벨만큼 줄어듦
    private const float MinComboCooldownSeconds = 10f; // 콤보 쿨타임 하한
    private const float BaseComboDurationSeconds = 5f; // 콤보 기본 지속시간(초) - ComboDuration 레벨만큼 늘어남

    private const float LuckyClickChanceValue = 0.001f; // 럭키 클릭 확률 (0.1%)

    private const float BaseAutoMineIntervalSeconds = 8f; // 자동채굴 기본 주기(초) - AutoMineSpeed 레벨만큼 줄어듦
    private const float MinAutoMineIntervalSeconds = 2f; // 자동채굴 주기 하한
    [SerializeField] private int autoMineTierOffset = 3; // 지금 캐는 오브젝트보다 몇 단계 전 오브젝트를 자동으로 캐줄지

    // 조각 하나(오브젝트 인덱스 objectIndex)로만 내는 비용을, 레벨 수만큼 만들어주는 헬퍼
    private static UpgradeLevelCost[] SingleCurrency(int objectIndex, params long[] amounts)
    {
        var result = new UpgradeLevelCost[amounts.Length];
        for (int i = 0; i < amounts.Length; i++)
            result[i] = new UpgradeLevelCost { pieces = new[] { new PieceCost(objectIndex, amounts[i]) } };
        return result;
    }

    // 조각 두 종류(objectA, objectB)를 동시에 요구하는 비용을 레벨 수만큼 만들어주는 헬퍼 - 중반 업그레이드용
    private static UpgradeLevelCost[] DualCurrency(int objectA, int objectB, params (long a, long b)[] amounts)
    {
        var result = new UpgradeLevelCost[amounts.Length];
        for (int i = 0; i < amounts.Length; i++)
        {
            result[i] = new UpgradeLevelCost
            {
                pieces = new[] { new PieceCost(objectA, amounts[i].a), new PieceCost(objectB, amounts[i].b) },
            };
        }
        return result;
    }

    // 업그레이드 하나의 데이터: 이름 + 레벨별 비용/효과값
    [Serializable]
    public class UpgradeDef
    {
        public string displayName; // 네모(버튼)에 표시할 이름 - 유저 요청으로 한국어로 표시 (2026-08-31)
        public UpgradeLevelCost[] costsPerLevel; // costsPerLevel[i] = i레벨에서 (i+1)레벨로 올릴 때 드는 조각들
        public float[] values; // values[i] = (i+1)레벨일 때 적용되는 효과 값 (단일 업그레이드는 values[0]만 사용)
    }

    // 업그레이드 레벨 하나를 올리는 데 필요한 조각들 (한 종류 이상)
    [Serializable]
    public class UpgradeLevelCost
    {
        public PieceCost[] pieces;
    }

    // 전부 0번 오브젝트(Plate) 조각으로 사는 초반 업그레이드들
    private readonly UpgradeDef clickDamage = new UpgradeDef
    {
        displayName = "클릭 데미지",
        costsPerLevel = SingleCurrency(0, 10, 25, 60, 150, 350),
        values = new float[] { 1, 2, 3, 4, 5 },
    };

    private readonly UpgradeDef critDamage = new UpgradeDef
    {
        displayName = "크리티컬 데미지",
        costsPerLevel = SingleCurrency(0, 50, 120, 280, 600, 1200),
        values = new float[] { 10, 20, 30, 40, 50 },
    };

    private readonly UpgradeDef critChanceUp = new UpgradeDef
    {
        displayName = "크리티컬 확률",
        costsPerLevel = SingleCurrency(0, 150),
        values = new float[] { 5 }, // % 추가치
    };

    private readonly UpgradeDef autoClickUnlock = new UpgradeDef
    {
        displayName = "자동 클릭",
        costsPerLevel = SingleCurrency(0, 3000),
        values = new float[] { 1 },
    };

    private readonly UpgradeDef autoClickSpeed = new UpgradeDef
    {
        displayName = "자동 클릭 속도",
        costsPerLevel = SingleCurrency(0, 500, 1000, 2000, 3800, 7000),
        values = new float[] { 0.5f, 1f, 1.5f, 2f, 2.5f }, // 초당 감소량
    };

    private readonly UpgradeDef autoClickCount = new UpgradeDef
    {
        displayName = "자동 클릭 횟수",
        costsPerLevel = SingleCurrency(0, 600, 1200, 2400, 4500, 8500),
        values = new float[] { 1, 2, 3, 4, 5 }, // 1회 발동당 추가 클릭 수
    };

    private readonly UpgradeDef comboUnlock = new UpgradeDef
    {
        displayName = "콤보",
        costsPerLevel = SingleCurrency(0, 25000),
        values = new float[] { 1 },
    };

    private readonly UpgradeDef comboCooldown = new UpgradeDef
    {
        displayName = "콤보 쿨타임",
        costsPerLevel = SingleCurrency(0, 800, 1600, 3200, 6000, 11000),
        values = new float[] { 3, 6, 9, 12, 15 }, // 초당 감소량
    };

    private readonly UpgradeDef comboDuration = new UpgradeDef
    {
        displayName = "콤보 지속시간",
        costsPerLevel = SingleCurrency(0, 800, 1600, 3200, 6000, 11000),
        values = new float[] { 1, 2, 3, 4, 5 }, // 초당 증가량
    };

    private readonly UpgradeDef luckyClick = new UpgradeDef
    {
        displayName = "럭키 클릭",
        costsPerLevel = SingleCurrency(0, 200000),
        values = new float[] { 1 },
    };

    private readonly UpgradeDef doubleClick = new UpgradeDef
    {
        displayName = "더블 클릭",
        costsPerLevel = SingleCurrency(0, 1600000),
        values = new float[] { 1 },
    };

    // 중반부 "돌려쓰기" 후속 업그레이드들 - 0번(Plate) + 1번(Glass Cup) 조각을 동시에 요구함
    private readonly UpgradeDef critChanceUp2 = new UpgradeDef
    {
        displayName = "크리티컬 확률 II",
        costsPerLevel = DualCurrency(0, 1, (4000, 50), (8000, 100), (15000, 200), (28000, 400), (50000, 700)),
        values = new float[] { 10, 20, 30, 40, 50 }, // % 추가치
    };

    private readonly UpgradeDef clickDamageUp2 = new UpgradeDef
    {
        displayName = "클릭 데미지 II",
        costsPerLevel = DualCurrency(0, 1, (2000, 40), (4000, 80), (7500, 150), (14000, 280), (25000, 500)),
        values = new float[] { 10, 20, 30, 40, 50 },
    };

    private readonly UpgradeDef critDamageUp2 = new UpgradeDef
    {
        displayName = "크리티컬 데미지 II",
        costsPerLevel = DualCurrency(0, 1, (3000, 60), (6000, 120), (11000, 220), (20000, 400), (35000, 700)),
        values = new float[] { 10, 20, 30, 40, 50 },
    };

    private readonly UpgradeDef autoMineUnlock = new UpgradeDef
    {
        displayName = "자동 채굴",
        costsPerLevel = DualCurrency(0, 1, (50000, 800)),
        values = new float[] { 1 },
    };

    private readonly UpgradeDef autoMineSpeed = new UpgradeDef
    {
        displayName = "자동 채굴 속도",
        costsPerLevel = SingleCurrency(0, 2000, 4000, 7500, 14000, 25000),
        values = new float[] { 0.5f, 1f, 1.5f, 2f, 2.5f }, // 초당 감소량
    };

    private readonly int[] _levels = new int[UpgradeCount]; // 각 업그레이드의 현재 레벨 (0 = 미구매)

    // 업그레이드 레벨이 바뀔 때마다 (타입, 새 레벨) 전달 - 업그레이드 UI, SaveManager 등이 구독
    public event Action<UpgradeType, int> OnUpgradeChanged;

    void Awake()
    {
        // 씬에 UpgradeManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private UpgradeDef GetDef(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.ClickDamage: return clickDamage;
            case UpgradeType.CritDamage: return critDamage;
            case UpgradeType.CritChanceUp: return critChanceUp;
            case UpgradeType.AutoClickUnlock: return autoClickUnlock;
            case UpgradeType.AutoClickSpeed: return autoClickSpeed;
            case UpgradeType.AutoClickCount: return autoClickCount;
            case UpgradeType.ComboUnlock: return comboUnlock;
            case UpgradeType.ComboCooldown: return comboCooldown;
            case UpgradeType.ComboDuration: return comboDuration;
            case UpgradeType.LuckyClick: return luckyClick;
            case UpgradeType.DoubleClick: return doubleClick;
            case UpgradeType.CritChanceUp2: return critChanceUp2;
            case UpgradeType.ClickDamageUp2: return clickDamageUp2;
            case UpgradeType.CritDamageUp2: return critDamageUp2;
            case UpgradeType.AutoMineUnlock: return autoMineUnlock;
            case UpgradeType.AutoMineSpeed: return autoMineSpeed;
            default: return null;
        }
    }

    public string GetDisplayName(UpgradeType type) => GetDef(type).displayName;
    public int GetLevel(UpgradeType type) => _levels[(int)type];

    // 단일 구매(1/1) 업그레이드인지에 따라 최대 레벨이 다름
    public int GetMaxLevel(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.AutoClickUnlock:
            case UpgradeType.ComboUnlock:
            case UpgradeType.LuckyClick:
            case UpgradeType.DoubleClick:
            case UpgradeType.CritChanceUp:
            case UpgradeType.AutoMineUnlock:
                return 1;
            default:
                return MaxLevel;
        }
    }

    // 다음 레벨로 올리는 데 필요한 조각들 (이미 최대 레벨이면 null)
    public PieceCost[] GetNextCost(UpgradeType type)
    {
        int level = GetLevel(type);
        if (level >= GetMaxLevel(type)) return null;
        return GetDef(type).costsPerLevel[level].pieces;
    }

    // 이 업그레이드가 트리에서 아직 공개되기 전인지(=부모 업그레이드를 1레벨도 안 올렸는지) 판단하기 위한 부모 조회.
    // 트리 전체가 ClickDamage 하나에서 시작해서 끊기지 않고 쭉 이어지도록 구성함:
    // ClickDamage -> CritDamage -> CritChanceUp -> CritChanceUp2 ("돌려쓰기" 후속)
    //                            -> AutoClickUnlock -> (AutoClickSpeed -> ClickDamageUp2,
    //                                                   AutoClickCount -> (LuckyClick, CritDamageUp2 -> AutoMineUnlock -> AutoMineSpeed))
    //             -> ComboUnlock -> (ComboCooldown, ComboDuration -> DoubleClick)
    // (한 노드가 한 번에 공개하는 자식은 평균 1~2개, 최대 4개를 넘지 않게 맞춰둠 - 너무 많은 버튼이 한꺼번에 안 튀어나오게)
    // 인스턴스 상태를 안 쓰는 순수 함수라 static으로 둠 - UI가 Instance 없이도(빌드 시점 등) 트리 모양을 조회할 수 있게
    public static UpgradeType? GetPrerequisite(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.CritDamage: return UpgradeType.ClickDamage;
            case UpgradeType.ComboUnlock: return UpgradeType.ClickDamage;
            case UpgradeType.CritChanceUp: return UpgradeType.CritDamage;
            case UpgradeType.CritChanceUp2: return UpgradeType.CritChanceUp;
            case UpgradeType.AutoClickUnlock: return UpgradeType.CritChanceUp;
            case UpgradeType.AutoClickSpeed: return UpgradeType.AutoClickUnlock;
            case UpgradeType.AutoClickCount: return UpgradeType.AutoClickUnlock;
            case UpgradeType.ClickDamageUp2: return UpgradeType.AutoClickSpeed; // AutoClickUnlock 자식이 너무 많아져서 한 단계 더 들어감
            case UpgradeType.CritDamageUp2: return UpgradeType.AutoClickCount; // 위와 같은 이유
            case UpgradeType.AutoMineUnlock: return UpgradeType.CritDamageUp2; // AutoClickUnlock은 이미 자식이 4개라 더 안 늘리려고 옮김
            case UpgradeType.AutoMineSpeed: return UpgradeType.AutoMineUnlock;
            case UpgradeType.LuckyClick: return UpgradeType.AutoClickCount;
            case UpgradeType.ComboCooldown: return UpgradeType.ComboUnlock;
            case UpgradeType.ComboDuration: return UpgradeType.ComboUnlock;
            case UpgradeType.DoubleClick: return UpgradeType.ComboDuration;
            default: return null; // ClickDamage만 트리의 루트
        }
    }

    public bool IsLocked(UpgradeType type)
    {
        UpgradeType? prerequisite = GetPrerequisite(type);
        if (prerequisite == null) return false;

        return GetLevel(prerequisite.Value) <= 0;
    }

    // 조각을 소모해서 한 레벨 올림. 실패(잠김/최대레벨/조각부족) 시 false 반환
    public bool TryUpgrade(UpgradeType type)
    {
        if (IsLocked(type))
        {
            Debug.Log($"{type} 업그레이드는 아직 잠겨있음 (선행 업그레이드 필요)");
            return false;
        }

        int level = GetLevel(type);
        int maxLevel = GetMaxLevel(type);
        if (level >= maxLevel)
        {
            Debug.Log($"{type} 업그레이드는 이미 최대 레벨");
            return false;
        }

        PieceCost[] cost = GetDef(type).costsPerLevel[level].pieces;
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendPieces(cost))
        {
            Debug.Log($"{type} 업그레이드 조각 부족");
            return false;
        }

        _levels[(int)type] = level + 1;
        OnUpgradeChanged?.Invoke(type, _levels[(int)type]);
        return true;
    }

    // 저장 파일 로드 시 구매 로직 없이 레벨을 그대로 대입
    public void SetLevel(UpgradeType type, int level)
    {
        _levels[(int)type] = Mathf.Clamp(level, 0, GetMaxLevel(type));
        OnUpgradeChanged?.Invoke(type, _levels[(int)type]);
    }

    // 테스트용 - 모든 업그레이드 레벨을 0으로 되돌림 (조각은 환불하지 않음)
    public void ResetAll()
    {
        for (int i = 0; i < UpgradeCount; i++)
        {
            _levels[i] = 0;
            OnUpgradeChanged?.Invoke((UpgradeType)i, 0);
        }
    }

    // ---- 실제 효과 값 (Click.cs, ComboManager 등에서 사용) ----

    public int ClickDamageBonus
    {
        get
        {
            int bonus = 0;

            int level = GetLevel(UpgradeType.ClickDamage);
            if (level > 0) bonus += (int)clickDamage.values[level - 1];

            int level2 = GetLevel(UpgradeType.ClickDamageUp2);
            if (level2 > 0) bonus += (int)clickDamageUp2.values[level2 - 1];

            return bonus;
        }
    }

    // 크리티컬 발동 확률 (0~1). CritDamage를 한 번도 안 샀으면 크리티컬 자체가 발동하지 않음
    public float CritChanceValue
    {
        get
        {
            if (GetLevel(UpgradeType.CritDamage) <= 0) return 0f;

            float bonus = 0f;

            int upLevel = GetLevel(UpgradeType.CritChanceUp);
            if (upLevel > 0) bonus += critChanceUp.values[upLevel - 1] / 100f;

            int up2Level = GetLevel(UpgradeType.CritChanceUp2);
            if (up2Level > 0) bonus += critChanceUp2.values[up2Level - 1] / 100f;

            return Mathf.Clamp01(BaseCritChance + bonus);
        }
    }

    // 크리티컬 발동 시 데미지에 곱해지는 배율
    public float CritMultiplierValue
    {
        get
        {
            int level = GetLevel(UpgradeType.CritDamage);
            if (level <= 0) return 1f;

            float multiplier = BaseCritMultiplier + critDamage.values[level - 1] / 100f;

            int level2 = GetLevel(UpgradeType.CritDamageUp2);
            if (level2 > 0) multiplier += critDamageUp2.values[level2 - 1] / 100f;

            return multiplier;
        }
    }

    public bool AutoClickIsUnlocked => GetLevel(UpgradeType.AutoClickUnlock) > 0;

    public float AutoClickIntervalSeconds
    {
        get
        {
            int level = GetLevel(UpgradeType.AutoClickSpeed);
            float reduction = level > 0 ? autoClickSpeed.values[level - 1] : 0f;
            return Mathf.Max(MinAutoClickIntervalSeconds, BaseAutoClickIntervalSeconds - reduction);
        }
    }

    public int AutoClickClicksPerTrigger
    {
        get
        {
            int level = GetLevel(UpgradeType.AutoClickCount);
            int bonus = level > 0 ? (int)autoClickCount.values[level - 1] : 0;
            return BaseAutoClickCount + bonus;
        }
    }

    public bool ComboIsUnlocked => GetLevel(UpgradeType.ComboUnlock) > 0;

    public float ComboCooldownSeconds
    {
        get
        {
            int level = GetLevel(UpgradeType.ComboCooldown);
            float reduction = level > 0 ? comboCooldown.values[level - 1] : 0f;
            return Mathf.Max(MinComboCooldownSeconds, BaseComboCooldownSeconds - reduction);
        }
    }

    public float ComboDurationSeconds
    {
        get
        {
            int level = GetLevel(UpgradeType.ComboDuration);
            float bonus = level > 0 ? comboDuration.values[level - 1] : 0f;
            return BaseComboDurationSeconds + bonus;
        }
    }

    public bool LuckyClickIsUnlocked => GetLevel(UpgradeType.LuckyClick) > 0;
    public float LuckyClickChance => LuckyClickIsUnlocked ? LuckyClickChanceValue : 0f;

    public bool DoubleClickIsUnlocked => GetLevel(UpgradeType.DoubleClick) > 0;

    public bool AutoMineIsUnlocked => GetLevel(UpgradeType.AutoMineUnlock) > 0;
    public int AutoMineTierOffset => autoMineTierOffset; // 지금 캐는 오브젝트보다 몇 단계 전을 자동으로 캘지

    public float AutoMineIntervalSeconds
    {
        get
        {
            int level = GetLevel(UpgradeType.AutoMineSpeed);
            float reduction = level > 0 ? autoMineSpeed.values[level - 1] : 0f;
            return Mathf.Max(MinAutoMineIntervalSeconds, BaseAutoMineIntervalSeconds - reduction);
        }
    }
}
