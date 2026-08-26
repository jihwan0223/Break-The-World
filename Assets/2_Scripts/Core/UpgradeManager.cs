using System;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    // 씬 어디서든 UpgradeManager.Instance로 접근하기 위한 싱글톤
    public static UpgradeManager Instance { get; private set; }

    // 업그레이드 종류. 순서를 바꾸면 안 됨 (SaveData.upgradeLevels 배열 인덱스와 대응)
    public enum UpgradeType
    {
        ClickDamage = 0, // 클릭 데미지 +N
        CritChance = 1, // 크리티컬 확률
        CritMultiplier = 2, // 크리티컬 배율 증가 (CritChance 1레벨 이상이어야 구매 가능)
        GoldGainPercent = 3, // 오브젝트 파괴 시 골드 획득량 +N%
        GlobalGoldMultiplier = 4, // 전체 골드 획득 배율
    }

    public const int UpgradeCount = 5; // 업그레이드 종류 수 (위 enum 개수와 일치해야 함)
    public const int MaxLevel = 5; // 업그레이드 최대 단계 - 여기 숫자만 바꾸면 전체 시스템에 반영됨 (레벨/원가/효과 배열 길이도 같이 늘려야 함)

    // 업그레이드 하나의 데이터: 이름 + 레벨별 비용/효과값. 인스펙터에서 자유롭게 수정 가능
    [Serializable]
    public class UpgradeDef
    {
        public string displayName; // 네모(버튼)에 표시할 이름 - 화면 표시 텍스트라 영어로 작성
        public int[] costs = new int[MaxLevel]; // costs[i] = i레벨에서 (i+1)레벨로 올릴 때 드는 골드
        public float[] values = new float[MaxLevel]; // values[i] = (i+1)레벨일 때 적용되는 효과 값
    }

    // 클릭 데미지 +N. 무기 클릭 데미지에 그대로 더해지는 고정값
    [SerializeField]
    private UpgradeDef clickDamage = new UpgradeDef
    {
        displayName = "Click Damage",
        costs = new int[] { 15, 60, 200, 600, 1800 },
        values = new float[] { 1, 3, 7, 15, 30 },
    };

    // 크리티컬 확률(%). 0레벨이면 크리티컬 자체가 발동하지 않음
    [SerializeField]
    private UpgradeDef critChance = new UpgradeDef
    {
        displayName = "Crit Chance",
        costs = new int[] { 50, 150, 400, 1000, 2500 },
        values = new float[] { 5, 10, 15, 20, 25 },
    };

    // 크리티컬 배율. CritChance가 1레벨 이상이어야 구매 가능 - 0레벨일 땐 기본 배율(BaseCritMultiplier)이 적용됨
    [SerializeField]
    private UpgradeDef critMultiplier = new UpgradeDef
    {
        displayName = "Crit Multiplier",
        costs = new int[] { 100, 300, 800, 2000, 5000 },
        values = new float[] { 4, 5, 6, 7, 8 },
    };

    // 오브젝트 파괴 시 골드 획득량 +N%
    [SerializeField]
    private UpgradeDef goldGainPercent = new UpgradeDef
    {
        displayName = "Gold Gain %",
        costs = new int[] { 40, 120, 350, 900, 2200 },
        values = new float[] { 10, 25, 45, 70, 100 },
    };

    // 전체 골드 획득 배율. 구간마다 하나씩 - 고가라서 임팩트 있게
    [SerializeField]
    private UpgradeDef globalGoldMultiplier = new UpgradeDef
    {
        displayName = "Gold Multiplier",
        costs = new int[] { 300, 900, 2500, 5500, 10000 },
        values = new float[] { 2, 3, 5, 8, 12 },
    };

    private const float BaseCritMultiplier = 3f; // CritChance는 있지만 CritMultiplier 업그레이드를 아직 안 샀을 때 기본 크리티컬 배율

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
            case UpgradeType.CritChance: return critChance;
            case UpgradeType.CritMultiplier: return critMultiplier;
            case UpgradeType.GoldGainPercent: return goldGainPercent;
            case UpgradeType.GlobalGoldMultiplier: return globalGoldMultiplier;
            default: return null;
        }
    }

    public string GetDisplayName(UpgradeType type) => GetDef(type).displayName;
    public int GetLevel(UpgradeType type) => _levels[(int)type];

    // 다음 레벨로 올리는 데 필요한 골드 (이미 최대 레벨이면 -1)
    public int GetNextCost(UpgradeType type)
    {
        int level = GetLevel(type);
        if (level >= MaxLevel) return -1;
        return GetDef(type).costs[level];
    }

    // CritMultiplier처럼 다른 업그레이드가 먼저 1레벨 이상이어야 구매 가능한 경우를 판단
    public bool IsLocked(UpgradeType type)
    {
        if (type == UpgradeType.CritMultiplier)
            return GetLevel(UpgradeType.CritChance) <= 0;

        return false;
    }

    // 골드를 소모해서 한 레벨 올림. 실패(잠김/최대레벨/골드부족) 시 false 반환
    public bool TryUpgrade(UpgradeType type)
    {
        if (IsLocked(type))
        {
            Debug.Log($"{type} 업그레이드는 아직 잠겨있음 (선행 업그레이드 필요)");
            return false;
        }

        int level = GetLevel(type);
        if (level >= MaxLevel)
        {
            Debug.Log($"{type} 업그레이드는 이미 최대 레벨");
            return false;
        }

        int cost = GetDef(type).costs[level];
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendGold(cost))
        {
            Debug.Log($"{type} 업그레이드 골드 부족 (필요: {cost})");
            return false;
        }

        _levels[(int)type] = level + 1;
        OnUpgradeChanged?.Invoke(type, _levels[(int)type]);
        return true;
    }

    // 저장 파일 로드 시 구매 로직 없이 레벨을 그대로 대입
    public void SetLevel(UpgradeType type, int level)
    {
        _levels[(int)type] = Mathf.Clamp(level, 0, MaxLevel);
        OnUpgradeChanged?.Invoke(type, _levels[(int)type]);
    }

    // ---- 실제 효과 값 (Click.cs 등에서 사용) ----

    // 클릭 한 번당 무기 데미지에 더해지는 고정 보너스
    public int ClickDamageBonus
    {
        get
        {
            int level = GetLevel(UpgradeType.ClickDamage);
            return level > 0 ? (int)clickDamage.values[level - 1] : 0;
        }
    }

    // 크리티컬 발동 확률 (0~1)
    public float CritChanceValue
    {
        get
        {
            int level = GetLevel(UpgradeType.CritChance);
            return level > 0 ? critChance.values[level - 1] / 100f : 0f;
        }
    }

    // 크리티컬 발동 시 데미지에 곱해지는 배율
    public float CritMultiplierValue
    {
        get
        {
            if (GetLevel(UpgradeType.CritChance) <= 0)
                return 1f; // 크리티컬 자체가 없음

            int level = GetLevel(UpgradeType.CritMultiplier);
            return level > 0 ? critMultiplier.values[level - 1] : BaseCritMultiplier;
        }
    }

    // 오브젝트 파괴 골드 보상에 곱해지는 배율 (1.0 = 보너스 없음)
    public float GoldGainMultiplier
    {
        get
        {
            int level = GetLevel(UpgradeType.GoldGainPercent);
            float percentBonus = level > 0 ? goldGainPercent.values[level - 1] / 100f : 0f;
            return 1f + percentBonus;
        }
    }

    // 전체 골드 획득에 곱해지는 배율 (1.0 = 보너스 없음)
    public float GlobalGoldMultiplierValue
    {
        get
        {
            int level = GetLevel(UpgradeType.GlobalGoldMultiplier);
            return level > 0 ? globalGoldMultiplier.values[level - 1] : 1f;
        }
    }
}
