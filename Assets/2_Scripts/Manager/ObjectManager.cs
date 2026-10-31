using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    public static ObjectManager Instance { get; private set; }

    [Serializable]
    public class ObjectSoundSet
    {
        public AudioClip[] clickSounds;
        public AudioClip breakSound;
    }

    [SerializeField] private ObjectSoundSet[] objectSounds; // objects와 같은 순서/개수

    [Serializable]
    public class ObjectVisualSet
    {
        public Sprite[] healthStages;
        // 크기는 이미지의 Pixels Per Unit으로 맞춤
        public Color waxColor; // 알파 0이면 기본 색
        public bool noWax;
    }

    private static readonly Color[] DefaultWaxColors =
    {
        new Color(1f, 0.72f, 0.78f), new Color(0.68f, 0.85f, 1f), new Color(1f, 0.88f, 0.6f), new Color(0.74f, 0.95f, 0.72f),
        new Color(0.85f, 0.75f, 1f), new Color(1f, 0.8f, 0.65f), new Color(0.65f, 0.95f, 0.9f), new Color(1f, 0.7f, 0.9f),
    };

    public Color WaxColorOf(int index)
    {
        Color color = objectVisuals != null && index >= 0 && index < objectVisuals.Length ? objectVisuals[index].waxColor : default;
        if (color.a <= 0f) color = DefaultWaxColors[Mathf.Abs(index) % DefaultWaxColors.Length];
        color.a = 1f;
        return color;
    }

    public bool NoWaxOf(int index) =>
        objectVisuals != null && index >= 0 && index < objectVisuals.Length && objectVisuals[index].noWax;

    [SerializeField] private ObjectVisualSet[] objectVisuals; // objects와 같은 순서/개수

    [SerializeField] private bool debugUnlockAll = false; // 테스트용

    // ObjectHealthCalculator.tierSizes와 순서/개수가 같아야 함
    private static readonly List<ObjectData> objects = new List<ObjectData>
    {
        // 존1: 책상 위 (맨손 / 프라이팬)
        new ObjectData(1, 1, "달걀", new Color(0.98f, 0.94f, 0.84f)) { hpMultiplier = 1f / 3f }, // 체력 10 (맨손 10클릭)
        new ObjectData(1, 2, "사과", new Color(0.86f, 0.16f, 0.16f)),
        new ObjectData(1, 3, "버터", new Color(1.00f, 0.86f, 0.42f)),
        new ObjectData(2, 1, "식빵", new Color(0.86f, 0.62f, 0.34f)),
        new ObjectData(2, 2, "책상", new Color(0.55f, 0.38f, 0.22f)) { hpMultiplier = 9f }, // ★ 존2 관문, 체력 = 식빵의 10배
        // 존2: 길거리 (망치 / 야구 방망이)
        new ObjectData(3, 1, "가로등", new Color(0.23f, 0.24f, 0.35f)),
        new ObjectData(3, 2, "자전거", new Color(0.23f, 0.24f, 0.30f)),
        new ObjectData(3, 3, "가로수", new Color(0.24f, 0.59f, 0.32f)),
        new ObjectData(4, 1, "자동차", new Color(0.70f, 0.69f, 0.71f)),
        new ObjectData(4, 2, "도로", new Color(0.62f, 0.60f, 0.58f)) { hpMultiplier = 17600f / 1800f }, // ★ 존3 관문, 체력 = 자동차의 10배
        // 존3: 왁스 회사 (곡괭이 / 철퇴) - 왁뿌볼은 존3이 열릴 때 공짜
        new ObjectData(5, 1, "왁뿌볼", new Color(0.45f, 0.75f, 0.25f)),
        new ObjectData(5, 2, "간판", new Color(0.92f, 0.92f, 0.90f)),
        new ObjectData(5, 3, "트럭", new Color(0.95f, 0.95f, 0.95f)),
        new ObjectData(6, 1, "컨베이어 벨트", new Color(0.35f, 0.38f, 0.45f)),
        new ObjectData(6, 2, "회사", new Color(0.80f, 0.80f, 0.78f)) { hpMultiplier = 163500f / 37000f * 0.65f }, // ★ 데모 마지막 - 철퇴 풀업이면 초당 약 3클릭으로 30초 보스전 클리어
        // 존4: 바다 (풀버전) - 튜브는 존4가 열릴 때 공짜
        new ObjectData(7, 1, "튜브", new Color(1.00f, 0.55f, 0.70f)),
        new ObjectData(7, 2, "부표", new Color(0.90f, 0.30f, 0.30f)),
        new ObjectData(7, 3, "요트", new Color(0.92f, 0.94f, 0.98f)),
        new ObjectData(8, 1, "등대", new Color(0.95f, 0.40f, 0.40f)),
        new ObjectData(8, 2, "배", new Color(0.25f, 0.35f, 0.65f)) { hpMultiplier = 163500f / 37000f * 0.65f }, // ★ 존4 관문
    };

    private int equippedIndex;

    public int ObjectCount => objects.Count;
    public int VisibleObjectCount => GameSettings.IsDemo ? Math.Min(GameSettings.DemoObjectCount, objects.Count) : objects.Count; // 데모에선 풀버전 오브젝트 숨김 (도감/통계)
    public static int StaticObjectCount => objects.Count;
    public int EquippedIndex => equippedIndex;
    public ObjectData CurrentObject => objects[equippedIndex];

    public event Action<ObjectData> OnObjectChanged;

    private bool[] _unlocked;
    private const double UnlockBreaksStart = 25; // 사과 해금 = 달걀(강화 전) 25번
    private const double UnlockBreaksGrowth = 1.15; // 오브젝트마다 필요 처치 수에 곱함
    public const long UnlockCostBase = 15;

    public event Action<int> OnUnlockChanged;

    private int[] _gainLevel;
    public const int MaxGainLevel = 5;
    private const long GainCostBase = 8; // 해금 비용의 약 절반에서 시작
    private const float GainCostGrowthPerLevel = 1.8f;

    public event Action<int, int> OnGainLevelChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        for (int i = 0; i < objects.Count && objectSounds != null && i < objectSounds.Length; i++)
        {
            objects[i].clickSounds = objectSounds[i].clickSounds;
            objects[i].breakSound = objectSounds[i].breakSound;
        }

        for (int i = 0; i < objects.Count && objectVisuals != null && i < objectVisuals.Length; i++)
            objects[i].healthStages = objectVisuals[i].healthStages;

        _unlocked = new bool[objects.Count];
        _unlocked[0] = true;
        if (debugUnlockAll)
            for (int i = 0; i < _unlocked.Length; i++) _unlocked[i] = true;

        _gainLevel = new int[objects.Count];
    }

    public ObjectData GetObjectAt(int index) => objects[index];

    public static string StaticObjectNameAt(int index) =>
        index >= 0 && index < objects.Count ? objects[index].objectName : "";

    public static int StaticIndexOfName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return -1;

        string wanted = objectName.Trim();
        for (int i = 0; i < objects.Count; i++)
        {
            if (string.Equals(objects[i].objectName.Trim(), wanted, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public void Equip(int index)
    {
        index = Mathf.Clamp(index, 0, objects.Count - 1);
        if (!IsUnlocked(index))
            return;

        equippedIndex = index;
        OnObjectChanged?.Invoke(CurrentObject);
    }

    public bool IsUnlocked(int index) => index >= 0 && index < _unlocked.Length && _unlocked[index];

    public bool IsUnlockOrderMet(int index) => index <= 0 || IsUnlocked(index - 1);

    public int NextRequiredUnlockIndex(int index)
    {
        for (int i = 0; i < index; i++)
            if (!IsUnlocked(i)) return i;
        return index;
    }

    public string NextRequiredUnlockHintText(int index)
    {
        int next = NextRequiredUnlockIndex(index);
        if (OpensNextZone(next - 1))
            return $"{GetObjectAt(next - 1).objectName}을(를) 부수면 열립니다";
        return $"{GetObjectAt(next).objectName}을(를) 먼저 해금하세요";
    }

    public bool OpensNextZone(int index)
    {
        return index >= 0 && ZoneManager.Instance != null && ZoneManager.Instance.IsZoneFirstObject(index + 1);
    }

    private static readonly string[] zoneNames = { "책상 위", "길거리", "공사장" };

    public static string ZoneNameOf(int index) =>
        zoneNames[Mathf.Clamp((objects[index].tier - 1) / 2, 0, zoneNames.Length - 1)];

    public static int FirstIndexOfTier(int tier) => objects.FindIndex(o => o.tier == tier);

    public static int MaxHPOf(int index) =>
        Health.ComputeMaxHP(objects[index].tier, objects[index].indexInTier, objects[index].hpMultiplier);

    public long ExpectedPieces(int index)
    {
        float upgradeMultiplier = 1f;
        long weaponKillBonus = 0;
        double objectBonus = 0;
        if (UpgradeManager.Instance != null)
        {
            upgradeMultiplier = UpgradeManager.Instance.PieceGainMultiplier();
            weaponKillBonus = UpgradeManager.Instance.WeaponKillBonusPieces(WeaponManager.Instance != null ? WeaponManager.Instance.EquippedIndex : -1);
            objectBonus = UpgradeManager.Instance.ObjectPieceBonus(index, GetBaseReward(index));
        }

        return Math.Max(1L, (long)Math.Round((GetBaseReward(index) + GetGainBonus(index) + weaponKillBonus) * (double)upgradeMultiplier + objectBonus));
    }

    private const long BaseRewardStart = 1;
    private const float BaseRewardGrowth = 4.5f; // 맵1에서 오브젝트마다 곱함
    private const int ObjectsPerMap = 5;
    private const float LaterRewardGrowth = 2f; // 맵2부터 오브젝트마다 곱함

    public long GetBaseReward(int index) => Math.Max(1L, (long)(RawBaseReward(index) * (index >= ObjectsPerMap * 2 ? Map3DropMultiplier : 1.0)));

    // 맵3 조각만 곱함 - 후반 노드 가격용
    private const double Map3DropMultiplier = 10;

    private static double RawBaseReward(int index)
    {
        int safeIndex = Mathf.Max(0, index);
        int map1Steps = Math.Min(safeIndex, ObjectsPerMap - 1);
        return BaseRewardStart * Math.Pow(BaseRewardGrowth, map1Steps) * Math.Pow(LaterRewardGrowth, safeIndex - map1Steps);
    }

    public long GetUnlockCost(int index)
    {
        if (index <= 0) return 0;
        double breaksNeeded = UnlockBreaksStart * Math.Pow(UnlockBreaksGrowth, index - 1);
        if (index > ObjectsPerMap * 2) breaksNeeded *= Map3UnlockCostMultiplier;
        return NumberFormatUtil.RoundPrice((long)(breaksNeeded * RawBaseReward(index - 1)));
    }

    // 맵3 조각이 x10이라 해금비도 올림
    private const double Map3UnlockCostMultiplier = 3;

    public bool TryUnlock(int index)
    {
        if (index <= 0 || index >= objects.Count) return false;
        if (_unlocked[index]) return true;
        if (!IsUnlockOrderMet(index)) return false;

        var cost = new List<PieceCost> { new PieceCost(GetUnlockCost(index)) };
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendPieces(cost))
            return false;

        _unlocked[index] = true;
        OnUnlockChanged?.Invoke(index);
        return true;
    }

    public void SetUnlocked(int index, bool unlocked)
    {
        if (index < 0 || index >= _unlocked.Length) return;
        if (debugUnlockAll) unlocked = true;
        _unlocked[index] = unlocked;
        if (unlocked) OnUnlockChanged?.Invoke(index);
    }

    public int GetGainLevel(int index) => index >= 0 && index < _gainLevel.Length ? _gainLevel[index] : 0;

    public long GetNextGainCost(int index)
    {
        if (index <= 0) return -1;

        int level = GetGainLevel(index);
        if (level >= MaxGainLevel) return -1;

        double levelFactor = Math.Pow(GainCostGrowthPerLevel, level);
        return NumberFormatUtil.RoundPrice((long)(GetUnlockCost(index) * (GainCostBase / (double)UnlockCostBase) * levelFactor));
    }

    public long GetGainBonus(int index) => GetGainLevel(index) * GainBonusPerLevel(index);

    // 레벨당 기본 보상의 20%
    public long GainBonusPerLevel(int index) => Math.Max(1L, GetBaseReward(index) / 5);

    public bool TryUpgradeGain(int index)
    {
        if (index <= 0 || index >= objects.Count) return false;
        if (!IsUnlocked(index)) return false;

        long cost = GetNextGainCost(index);
        if (cost < 0) return false;

        var pieceCost = new List<PieceCost> { new PieceCost(cost) };
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendPieces(pieceCost))
            return false;

        _gainLevel[index]++;
        OnGainLevelChanged?.Invoke(index, _gainLevel[index]);
        return true;
    }

    public void SetGainLevel(int index, int level)
    {
        if (index < 0 || index >= _gainLevel.Length) return;
        _gainLevel[index] = Mathf.Clamp(level, 0, MaxGainLevel);
        OnGainLevelChanged?.Invoke(index, _gainLevel[index]);
    }

    public void UnlockAllDebug()
    {
        for (int i = 0; i < _unlocked.Length; i++)
        {
            if (_unlocked[i]) continue;
            _unlocked[i] = true;
            OnUnlockChanged?.Invoke(i);
        }
    }

    public void MaxAllDebug()
    {
        UnlockAllDebug();

        for (int i = 0; i < _gainLevel.Length; i++)
            SetGainLevel(i, MaxGainLevel);
    }

    public void ResetAll()
    {
        for (int i = 1; i < _unlocked.Length; i++)
            _unlocked[i] = debugUnlockAll;

        for (int i = 0; i < _gainLevel.Length; i++)
        {
            _gainLevel[i] = 0;
            OnGainLevelChanged?.Invoke(i, 0);
        }
    }
}
