using System;
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
        ShardGain = 2, // 파편 강화: 파괴 시 얻는 파편 +N (ClickDamage 자식)
        CritChanceUp = 3, // 크리티컬 확률 강화 (CritDamage 자식)
        ShardMultiplier = 4, // 파편 배율 강화 (ShardGain 자식)
        AutoClickUnlock = 5, // 자동클릭 (단일 - 이걸 사야 자동클릭 자체가 켜짐)
        AutoClickSpeed = 6, // 자동클릭 주기 단축 (AutoClickUnlock 자식)
        AutoClickCount = 7, // 자동클릭 1회당 클릭 수 증가 (AutoClickUnlock 자식)
        ComboUnlock = 8, // 콤보 (단일 - 이걸 사야 콤보 시스템 자체가 켜짐)
        ComboCooldown = 9, // 콤보 쿨타임 단축 (ComboUnlock 자식)
        ComboDuration = 10, // 콤보 지속시간 증가 (ComboUnlock 자식)
        LuckyClick = 11, // 럭키 클릭 (단일 - 0.1% 확률 즉시 파괴)
        DoubleClick = 12, // 더블클릭 (단일 - 클릭 한 번에 2번 처리)
    }

    public const int UpgradeCount = 13; // 업그레이드 종류 수 (위 enum 개수와 일치해야 함)
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

    // 업그레이드 하나의 데이터: 이름 + 레벨별 비용/효과값. 인스펙터에서 자유롭게 수정 가능
    [Serializable]
    public class UpgradeDef
    {
        public string displayName; // 네모(버튼)에 표시할 이름 - 화면 표시 텍스트라 영어로 작성
        public int[] costs; // costs[i] = i레벨에서 (i+1)레벨로 올릴 때 드는 파편
        public float[] values; // values[i] = (i+1)레벨일 때 적용되는 효과 값 (단일 업그레이드는 costs[0]/values[0]만 사용)
    }

    [SerializeField]
    private UpgradeDef clickDamage = new UpgradeDef
    {
        displayName = "Click Damage",
        costs = new int[] { 10, 30, 80, 200, 450 },
        values = new float[] { 1, 2, 3, 4, 5 },
    };

    [SerializeField]
    private UpgradeDef critDamage = new UpgradeDef
    {
        displayName = "Crit Damage",
        costs = new int[] { 20, 60, 150, 350, 700 },
        values = new float[] { 10, 20, 30, 40, 50 },
    };

    [SerializeField]
    private UpgradeDef shardGain = new UpgradeDef
    {
        displayName = "Shard Gain",
        costs = new int[] { 15, 45, 110, 260, 550 },
        values = new float[] { 1, 2, 3, 4, 5 },
    };

    [SerializeField]
    private UpgradeDef critChanceUp = new UpgradeDef
    {
        displayName = "Crit Chance",
        costs = new int[] { 40, 100, 220, 450, 800 },
        values = new float[] { 5, 10, 15, 20, 25 }, // % 추가치
    };

    [SerializeField]
    private UpgradeDef shardMultiplier = new UpgradeDef
    {
        displayName = "Shard Multiplier",
        costs = new int[] { 50, 120, 260, 500, 900 },
        values = new float[] { 10, 20, 30, 40, 50 }, // % 배율 보너스
    };

    [SerializeField]
    private UpgradeDef autoClickUnlock = new UpgradeDef
    {
        displayName = "Auto Click",
        costs = new int[] { 200 },
        values = new float[] { 1 },
    };

    [SerializeField]
    private UpgradeDef autoClickSpeed = new UpgradeDef
    {
        displayName = "Auto Click Speed",
        costs = new int[] { 50, 120, 250, 450, 700 },
        values = new float[] { 0.5f, 1f, 1.5f, 2f, 2.5f }, // 초당 감소량
    };

    [SerializeField]
    private UpgradeDef autoClickCount = new UpgradeDef
    {
        displayName = "Auto Click Count",
        costs = new int[] { 60, 150, 300, 550, 900 },
        values = new float[] { 1, 2, 3, 4, 5 }, // 1회 발동당 추가 클릭 수
    };

    [SerializeField]
    private UpgradeDef comboUnlock = new UpgradeDef
    {
        displayName = "Combo",
        costs = new int[] { 300 },
        values = new float[] { 1 },
    };

    [SerializeField]
    private UpgradeDef comboCooldown = new UpgradeDef
    {
        displayName = "Combo Cooldown",
        costs = new int[] { 80, 180, 350, 600, 950 },
        values = new float[] { 3, 6, 9, 12, 15 }, // 초당 감소량
    };

    [SerializeField]
    private UpgradeDef comboDuration = new UpgradeDef
    {
        displayName = "Combo Duration",
        costs = new int[] { 80, 180, 350, 600, 950 },
        values = new float[] { 1, 2, 3, 4, 5 }, // 초당 증가량
    };

    [SerializeField]
    private UpgradeDef luckyClick = new UpgradeDef
    {
        displayName = "Lucky Click",
        costs = new int[] { 500 },
        values = new float[] { 1 },
    };

    [SerializeField]
    private UpgradeDef doubleClick = new UpgradeDef
    {
        displayName = "Double Click",
        costs = new int[] { 800 },
        values = new float[] { 1 },
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
            case UpgradeType.ShardGain: return shardGain;
            case UpgradeType.CritChanceUp: return critChanceUp;
            case UpgradeType.ShardMultiplier: return shardMultiplier;
            case UpgradeType.AutoClickUnlock: return autoClickUnlock;
            case UpgradeType.AutoClickSpeed: return autoClickSpeed;
            case UpgradeType.AutoClickCount: return autoClickCount;
            case UpgradeType.ComboUnlock: return comboUnlock;
            case UpgradeType.ComboCooldown: return comboCooldown;
            case UpgradeType.ComboDuration: return comboDuration;
            case UpgradeType.LuckyClick: return luckyClick;
            case UpgradeType.DoubleClick: return doubleClick;
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
                return 1;
            default:
                return MaxLevel;
        }
    }

    // 다음 레벨로 올리는 데 필요한 파편 (이미 최대 레벨이면 -1)
    public int GetNextCost(UpgradeType type)
    {
        int level = GetLevel(type);
        if (level >= GetMaxLevel(type)) return -1;
        return GetDef(type).costs[level];
    }

    // 이 업그레이드가 트리에서 아직 공개되기 전인지(=부모 업그레이드를 1레벨도 안 올렸는지) 판단하기 위한 부모 조회.
    // 트리 전체가 ClickDamage 하나에서 시작해서 끊기지 않고 쭉 이어지도록 구성함 (독립된 트리 없음):
    // ClickDamage -> CritDamage -> CritChanceUp -> AutoClickUnlock -> (AutoClickSpeed, AutoClickCount -> LuckyClick)
    //             -> ShardGain  -> ShardMultiplier -> ComboUnlock -> (ComboCooldown, ComboDuration -> DoubleClick)
    // 인스턴스 상태를 안 쓰는 순수 함수라 static으로 둠 - UI가 UpgradeManager.Instance 없이도(빌드 시점 등) 트리 모양을 조회할 수 있게
    public static UpgradeType? GetPrerequisite(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.CritDamage: return UpgradeType.ClickDamage;
            case UpgradeType.ShardGain: return UpgradeType.ClickDamage;
            case UpgradeType.CritChanceUp: return UpgradeType.CritDamage;
            case UpgradeType.ShardMultiplier: return UpgradeType.ShardGain;
            case UpgradeType.AutoClickUnlock: return UpgradeType.CritChanceUp;
            case UpgradeType.AutoClickSpeed: return UpgradeType.AutoClickUnlock;
            case UpgradeType.AutoClickCount: return UpgradeType.AutoClickUnlock;
            case UpgradeType.LuckyClick: return UpgradeType.AutoClickCount;
            case UpgradeType.ComboUnlock: return UpgradeType.ShardMultiplier;
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

    // 파편을 소모해서 한 레벨 올림. 실패(잠김/최대레벨/파편부족) 시 false 반환
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

        int cost = GetDef(type).costs[level];
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendShards(cost))
        {
            Debug.Log($"{type} 업그레이드 파편 부족 (필요: {cost})");
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

    // 테스트용 - 모든 업그레이드 레벨을 0으로 되돌림 (파편은 환불하지 않음)
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
            int level = GetLevel(UpgradeType.ClickDamage);
            return level > 0 ? (int)clickDamage.values[level - 1] : 0;
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
            if (upLevel > 0) bonus = critChanceUp.values[upLevel - 1] / 100f;

            return Mathf.Clamp01(BaseCritChance + bonus);
        }
    }

    // 크리티컬 발동 시 데미지에 곱해지는 배율 (기본 배율 + 레벨별 보너스%)
    public float CritMultiplierValue
    {
        get
        {
            int level = GetLevel(UpgradeType.CritDamage);
            return level > 0 ? BaseCritMultiplier + critDamage.values[level - 1] / 100f : 1f;
        }
    }

    // 오브젝트 파괴 시 얻는 파편에 더해지는 고정 보너스
    public int ShardGainBonus
    {
        get
        {
            int level = GetLevel(UpgradeType.ShardGain);
            return level > 0 ? (int)shardGain.values[level - 1] : 0;
        }
    }

    // 파편 총량에 곱해지는 배율 (1.0 = 보너스 없음). ShardGain을 한 번도 안 샀으면 배율 자체가 없음
    public float ShardMultiplierValue
    {
        get
        {
            if (GetLevel(UpgradeType.ShardGain) <= 0) return 1f;

            int level = GetLevel(UpgradeType.ShardMultiplier);
            return level > 0 ? 1f + shardMultiplier.values[level - 1] / 100f : 1f;
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
}
