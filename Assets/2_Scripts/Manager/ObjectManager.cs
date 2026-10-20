using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    // 씬 어디서든 ObjectManager.Instance로 접근하기 위한 싱글톤
    public static ObjectManager Instance { get; private set; }

    // 오브젝트 하나가 낼 수 있는 사운드 묶음. 인스펙터에서 objects 리스트와 같은 순서로 채워 넣음
    [Serializable]
    public class ObjectSoundSet
    {
        public AudioClip[] clickSounds; // 클릭할 때 랜덤 재생할 사운드들
        public AudioClip breakSound; // 파괴될 때 재생할 사운드
    }

    [SerializeField] private ObjectSoundSet[] objectSounds; // objects 리스트와 같은 순서/개수로 채워야 함 (15개)

    // 오브젝트 하나의 체력 단계별 스프라이트 묶음. 인스펙터에서 objects 리스트와 같은 순서로 채워 넣음
    [Serializable]
    public class ObjectVisualSet
    {
        public Sprite[] healthStages; // 체력 100% -> 0% 순서 (예: Object1, Object1-1, Object1-2, Object1-3)
        // 크기는 코드에서 계산하지 않음 - 각 이미지의 Pixels Per Unit(Import Settings)으로 직접 맞출 것
        public Color waxColor; // 이 오브젝트를 덮는 왁스 색 (투명도는 무시됨). 비워두면(완전 투명) 기본 파스텔 색
        public bool noWax; // 켜면 이 오브젝트는 왁스를 안 덮음 (맵3 벨트/간판/트럭처럼 왁뿌볼 말고는 맨몸)
    }

    // 왁스 색을 안 정한 오브젝트에 쓰는 기본 파스텔 색 (오브젝트 순서대로 돌아가며)
    private static readonly Color[] DefaultWaxColors =
    {
        new Color(1f, 0.72f, 0.78f), new Color(0.68f, 0.85f, 1f), new Color(1f, 0.88f, 0.6f), new Color(0.74f, 0.95f, 0.72f),
        new Color(0.85f, 0.75f, 1f), new Color(1f, 0.8f, 0.65f), new Color(0.65f, 0.95f, 0.9f), new Color(1f, 0.7f, 0.9f),
    };

    // index번째 오브젝트의 왁스 색 - 인스펙터 값을 쓸 때마다 읽음 (플레이 중 바꿔도 다음 왁스부터 바로 반영)
    public Color WaxColorOf(int index)
    {
        Color color = objectVisuals != null && index >= 0 && index < objectVisuals.Length ? objectVisuals[index].waxColor : default; // 인스펙터에서 정한 색
        if (color.a <= 0f) color = DefaultWaxColors[Mathf.Abs(index) % DefaultWaxColors.Length];
        color.a = 1f;
        return color;
    }

    // index번째 오브젝트가 왁스를 안 덮는지 - 쓸 때마다 인스펙터 값을 읽음
    public bool NoWaxOf(int index) =>
        objectVisuals != null && index >= 0 && index < objectVisuals.Length && objectVisuals[index].noWax;

    [SerializeField] private ObjectVisualSet[] objectVisuals; // objects 리스트와 같은 순서/개수로 채워야 함 (15개)

    [SerializeField] private bool debugUnlockAll = false; // 테스트용 - 켜면 모든 오브젝트가 처음부터 해금됨 (업그레이드 무관)

    // 파괴 대상 오브젝트 15종(데모) - 책상 위 소품에서 시작해 지형(책상/길바닥)을 거쳐 왁스 공장 회사까지. ★=마일스톤(체력 x2)
    // 티어 구성은 ObjectHealthCalculator.tierSizes = {3,2,3,2,3,2} 와 순서/개수가 정확히 일치해야 함
    private static readonly List<ObjectData> objects = new List<ObjectData>
    {
        // --- 존1: 책상 위 (맨손 t1 / 망치 t2) ---
        new ObjectData(1, 1, "달걀", new Color(0.98f, 0.94f, 0.84f)),
        new ObjectData(1, 2, "사과", new Color(0.86f, 0.16f, 0.16f)),
        new ObjectData(1, 3, "버터", new Color(1.00f, 0.86f, 0.42f)),
        new ObjectData(2, 1, "식빵", new Color(0.86f, 0.62f, 0.34f)),
        new ObjectData(2, 2, "책상", new Color(0.55f, 0.38f, 0.22f)) { hpMultiplier = 9f }, // ★ -> 존2 (체력 = 식빵의 10배)
        // --- 존2: 길거리 (곡괭이 t3 / 전동드릴 t4) ---
        new ObjectData(3, 1, "가로등", new Color(0.23f, 0.24f, 0.35f)),
        new ObjectData(3, 2, "자전거", new Color(0.23f, 0.24f, 0.30f)),
        new ObjectData(3, 3, "가로수", new Color(0.24f, 0.59f, 0.32f)),
        new ObjectData(4, 1, "자동차", new Color(0.70f, 0.69f, 0.71f)),
        new ObjectData(4, 2, "도로", new Color(0.62f, 0.60f, 0.58f)) { hpMultiplier = 17600f / 1800f }, // ★ -> 존3 (체력 17,600 = 자동차의 10배)
        // --- 존3: 왁스 공장 (유압 브레이커 t5 / 다이너마이트 t6) ---
        new ObjectData(5, 1, "컨베이어 벨트", new Color(0.35f, 0.38f, 0.45f)),
        new ObjectData(5, 2, "왁뿌볼", new Color(0.45f, 0.75f, 0.25f)),
        new ObjectData(5, 3, "간판", new Color(0.92f, 0.92f, 0.90f)),
        new ObjectData(6, 1, "트럭", new Color(0.95f, 0.95f, 0.95f)),
        new ObjectData(6, 2, "회사", new Color(0.80f, 0.80f, 0.78f)) { hpMultiplier = 163500f / 37000f }, // ★ 데모 마지막 (체력 163,500 = 다이너마이트 풀업 + 전역 강화 + 치명타 풀업 한 방)
    };

    private int equippedIndex; // 실제로 선택된 오브젝트의 objects 리스트 인덱스 (0부터 시작)

    public int ObjectCount => objects.Count; // UI에서 화살표로 둘러볼 때 범위 계산용
    public static int StaticObjectCount => objects.Count; // Instance가 아직 없는 시점(빌드 타임 등)에도 오브젝트 개수를 알아야 하는 UI용
    public int EquippedIndex => equippedIndex; // 지금 선택된 오브젝트의 인덱스 (UI에서 "Equipped" 표시용)
    public ObjectData CurrentObject => objects[equippedIndex];

    // 선택된 오브젝트가 바뀔 때마다(=Equip 호출 시) 새 오브젝트 데이터를 전달 - 오브젝트 UI 등이 구독
    public event Action<ObjectData> OnObjectChanged;

    // ---- 오브젝트 해금 시스템 ----
    // 오브젝트는 앞 오브젝트부터 순서대로 조각으로 해금해야 장착 가능. 0번(Plate)은 처음부터 해금된 상태로 시작
    private bool[] _unlocked;
    // 해금 비용 = 직전 오브젝트의 기본 파편(강화 전) x 부숴야 하는 횟수 (무기 해금 비용도 이걸 따라감)
    // 횟수는 방치형 게임 흔한 방식처럼 오브젝트마다 일정 배율로 곱해져 올라감 (Start x Growth^(index-1))
    private const double UnlockBreaksStart = 25; // 사과를 열려면 달걀(강화 전 파편 기준)을 이만큼 부숴야 함
    private const double UnlockBreaksGrowth = 1.15; // 뒤 오브젝트로 갈 때마다 부숴야 하는 횟수에 곱해지는 배율 (회사는 트럭 약 154번)
    public const long UnlockCostBase = 15; // 획득량 증가(Gain) 비용 계산의 기준 횟수

    // 오브젝트가 새로 해금될 때 (objectIndex) 전달 - Object 선택 UI 등이 구독
    public event Action<int> OnUnlockChanged;

    // ---- 오브젝트별 "획득량 증가" 업그레이드 ----
    // 오브젝트의 획득량 증가 업그레이드는 조각으로 구매. 0번은 대상 아님(항상 0레벨)
    private int[] _gainLevel;
    private const int MaxGainLevel = 5;
    private const long GainCostBase = 8; // 비용 시작값 - 그 오브젝트 해금 비용의 약 절반
    private const float GainCostGrowthPerLevel = 1.8f; // 같은 업그레이드 안에서 레벨마다 늘어나는 배율

    // 오브젝트의 획득량 업그레이드 레벨이 바뀔 때 (objectIndex, 새 레벨) 전달
    public event Action<int, int> OnGainLevelChanged;

    void Awake()
    {
        // 씬에 ObjectManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 인스펙터에 넣어둔 사운드들을 같은 순서의 오브젝트 데이터에 채워 넣음
        for (int i = 0; i < objects.Count && objectSounds != null && i < objectSounds.Length; i++)
        {
            objects[i].clickSounds = objectSounds[i].clickSounds;
            objects[i].breakSound = objectSounds[i].breakSound;
        }

        // 인스펙터에 넣어둔 스프라이트 단계들을 같은 순서의 오브젝트 데이터에 채워 넣음
        for (int i = 0; i < objects.Count && objectVisuals != null && i < objectVisuals.Length; i++)
            objects[i].healthStages = objectVisuals[i].healthStages;

        _unlocked = new bool[objects.Count];
        _unlocked[0] = true; // 첫 오브젝트는 항상 해금된 상태로 시작
        if (debugUnlockAll)
            for (int i = 0; i < _unlocked.Length; i++) _unlocked[i] = true;

        _gainLevel = new int[objects.Count];
    }

    // 인덱스로 오브젝트 데이터를 조회만 함 (선택은 안 함) - UI가 화살표로 둘러볼 때 사용
    public ObjectData GetObjectAt(int index) => objects[index];

    // 인덱스로 오브젝트 이름 조회 (Instance 없이도). UpgradeManager CSV의 target=* 확장에 씀
    public static string StaticObjectNameAt(int index) =>
        index >= 0 && index < objects.Count ? objects[index].objectName : "";

    // objectName으로 인덱스를 찾음. 없으면 -1. Instance 없이도 동작(objects가 static이라) - UpgradeManager가
    // 템플릿의 targetObjectName을 인덱스로 바꿀 때 사용. 공백 차이는 무시하고 대소문자 구분 없이 비교
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

    // 잠긴 오브젝트는 장착 불가
    public void Equip(int index)
    {
        index = Mathf.Clamp(index, 0, objects.Count - 1);
        if (!IsUnlocked(index))
            return;

        equippedIndex = index;
        OnObjectChanged?.Invoke(CurrentObject);
    }

    public bool IsUnlocked(int index) => index >= 0 && index < _unlocked.Length && _unlocked[index];

    // index번째 오브젝트를 해금할 순서가 됐는지 - 바로 앞 오브젝트가 해금돼 있어야 함 (트리가 세 갈래로 나뉘어 있어서 순서는 여기서 지킴)
    public bool IsUnlockOrderMet(int index) => index <= 0 || IsUnlocked(index - 1);

    // index를 해금하려면 지금 당장 해금해야 하는 오브젝트의 인덱스 - 해금은 항상 0번부터 순서대로라
    // 앞에서부터 훑어 아직 안 풀린 첫 번째가 곧 "다음 할 일"임. 이미 순서가 충족됐으면 index 그대로 반환
    public int NextRequiredUnlockIndex(int index)
    {
        for (int i = 0; i < index; i++) // index 앞쪽만 확인 - 뒤쪽은 순서상 아직 볼 필요 없음
            if (!IsUnlocked(i)) return i;
        return index;
    }

    // index를 해금하기 위해 지금 당장 해금해야 하는 오브젝트 이름을 담은 안내 문구 (UI 툴팁용)
    public string NextRequiredUnlockHintText(int index)
    {
        int next = NextRequiredUnlockIndex(index); // 다음에 해금해야 할 오브젝트
        if (OpensNextZone(next - 1)) // 해금 노드가 없고 바로 앞 오브젝트(책상 등)를 부숴야 열리는 오브젝트
            return $"{GetObjectAt(next - 1).objectName}을(를) 부수면 열립니다";
        return $"{GetObjectAt(next).objectName}을(를) 먼저 해금하세요";
    }

    // index번째 오브젝트를 부수면 다음 맵이 열리는지 - 바로 다음 오브젝트가 맵이 열릴 때 공짜로 풀리는 오브젝트면 그렇게 봄
    public bool OpensNextZone(int index)
    {
        return index >= 0 && ZoneManager.Instance != null && ZoneManager.Instance.IsZoneFirstObject(index + 1);
    }

    // ---- 도감(오브젝트 팝업) 표시용 조회 ----
    private static readonly string[] zoneNames = { "책상 위", "길거리", "공사장" }; // 위 objects 목록의 존 구분과 같음 (무기 티어 2개당 존 1개)

    // index번째 오브젝트가 속한 존 이름
    public static string ZoneNameOf(int index) =>
        zoneNames[Mathf.Clamp((objects[index].tier - 1) / 2, 0, zoneNames.Length - 1)];

    // index번째 오브젝트의 최대 체력 - Health가 실제로 쓰는 공식과 같음
    // 이 무기 티어를 쓰는 첫 오브젝트의 인덱스 (없으면 -1) - 무기 해금 비용 기준
    public static int FirstIndexOfTier(int tier) => objects.FindIndex(o => o.tier == tier);

    public static int MaxHPOf(int index) =>
        Health.ComputeMaxHP(objects[index].tier, objects[index].indexInTier, objects[index].hpMultiplier);

    // 지금 업그레이드 기준으로 index번째 오브젝트를 한 번 부술 때 받는 파편 수.
    // Click.HandleDied 계산에서 콤보 배율과 2배 드랍 확률(둘 다 상황마다 달라짐)만 뺀 값
    public long ExpectedPieces(int index)
    {
        float upgradeMultiplier = 1f; // 파편 획득 배율 업그레이드
        long weaponKillBonus = 0; // 장착 무기의 처치 보너스 파편
        double objectBonus = 0; // 이 오브젝트 드랍량 강화로 더 얻는 조각 수
        if (UpgradeManager.Instance != null)
        {
            upgradeMultiplier = UpgradeManager.Instance.PieceGainMultiplier();
            weaponKillBonus = UpgradeManager.Instance.WeaponKillBonusPieces(WeaponManager.Instance != null ? WeaponManager.Instance.EquippedIndex : -1);
            objectBonus = UpgradeManager.Instance.ObjectPieceBonus(index, GetBaseReward(index));
        }

        return Math.Max(1L, (long)Math.Round((GetBaseReward(index) + GetGainBonus(index) + weaponKillBonus) * (double)upgradeMultiplier + objectBonus));
    }

    // ---- 클릭 시 기본 파편 보상 ----
    // 오브젝트를 부술 때마다 받는 기본 파편 수. 업그레이드/해금 비용이 티어마다 6배씩 기하급수적으로 커지는데
    // 기본 보상이 고정값(예: 1개)이면 콤보/업그레이드 배율을 아무리 곱해도(배율은 곱셈일 뿐 밑변이 1이면 그대로
    // 작은 수) 후반 티어 가격을 절대 못 따라잡음. 그래서 기본 보상도 오브젝트 티어마다 기하급수적으로 커지게 함.
    private const long BaseRewardStart = 1; // 0번 오브젝트 기본 보상
    private const float BaseRewardGrowth = 4.5f; // 맵1(달걀~책상)에서 오브젝트 하나 넘어갈 때마다 곱해지는 배율
    private const int ObjectsPerMap = 5; // 맵 하나에 들어있는 오브젝트 수 (달걀~책상 / 가로등~도로 / 컨베이어 벨트~회사)
    private const float LaterRewardGrowth = 2f; // 맵2부터 오브젝트 하나 넘어갈 때마다 곱해지는 배율 - 맵 넘어갈 때 파편이 확 뛰지 않게 (가로등 820)

    public long GetBaseReward(int index) => Math.Max(1L, (long)RawBaseReward(index));

    // 소수점을 버리기 전의 기본 보상 - 해금 비용이 이 값을 기준으로 계산됨
    private static double RawBaseReward(int index)
    {
        int safeIndex = Mathf.Max(0, index); // 음수 방지
        int map1Steps = Math.Min(safeIndex, ObjectsPerMap - 1); // 맵1 안에서 넘어간 횟수 (책상까지 최대 4)
        return BaseRewardStart * Math.Pow(BaseRewardGrowth, map1Steps) * Math.Pow(LaterRewardGrowth, safeIndex - map1Steps);
    }

    // index번째 오브젝트를 해금하는 데 필요한 조각 개수 (0번은 비용 없음 - 처음부터 해금)
    public long GetUnlockCost(int index)
    {
        if (index <= 0) return 0;
        // 가격은 강화와 상관없이 고정. 드랍량/획득량/데미지/자동 클릭을 강화할수록 같은 가격을 더 빨리 모음
        double breaksNeeded = UnlockBreaksStart * Math.Pow(UnlockBreaksGrowth, index - 1); // 앞 오브젝트를 (강화 전 기준) 부숴야 하는 횟수
        return NumberFormatUtil.RoundPrice((long)(breaksNeeded * RawBaseReward(index - 1))); // 딱 떨어지는 가격으로
    }

    // 조각으로 index번째 오브젝트를 해금 시도.
    // 바로 앞 오브젝트가 해금돼 있어야 하고, 조각이 충분해야 함 (트리 링크만으로는 갈래가 나뉘어 순서가 안 지켜져서 여기서 막음)
    public bool TryUnlock(int index)
    {
        if (index <= 0 || index >= objects.Count) return false;
        if (_unlocked[index]) return true; // 이미 해금됨
        if (!IsUnlockOrderMet(index)) return false; // 앞 오브젝트를 먼저 해금해야 함

        var cost = new List<PieceCost> { new PieceCost(GetUnlockCost(index)) };
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendPieces(cost))
            return false;

        _unlocked[index] = true;
        OnUnlockChanged?.Invoke(index);
        return true;
    }

    // 구매 로직 없이 해금 상태를 그대로 대입 - 저장 파일 로드, 존 해금 보상(ZoneManager)에서 씀
    public void SetUnlocked(int index, bool unlocked)
    {
        if (index < 0 || index >= _unlocked.Length) return;
        if (debugUnlockAll) unlocked = true; // 테스트 모드에서는 세이브가 뭐든 무조건 해금
        _unlocked[index] = unlocked;
        if (unlocked) OnUnlockChanged?.Invoke(index);
    }

    public int GetGainLevel(int index) => index >= 0 && index < _gainLevel.Length ? _gainLevel[index] : 0;

    // index번째 오브젝트의 획득량 증가 업그레이드 다음 레벨 비용((index-1)번 조각). 대상 아니거나 최대 레벨이면 -1
    public long GetNextGainCost(int index)
    {
        if (index <= 0) return -1;

        int level = GetGainLevel(index);
        if (level >= MaxGainLevel) return -1;

        double levelFactor = Math.Pow(GainCostGrowthPerLevel, level); // 레벨이 오를수록 비싸지는 배율
        return NumberFormatUtil.RoundPrice((long)(GetUnlockCost(index) * (GainCostBase / (double)UnlockCostBase) * levelFactor)); // 그 오브젝트 해금 비용의 약 절반에서 시작 (딱 떨어지는 가격으로)
    }

    // index번째 오브젝트가 파괴될 때마다 추가로 더 얻는 조각 개수 (획득량 증가 업그레이드 보너스)
    public long GetGainBonus(int index) => GetGainLevel(index) * GainBonusPerLevel(index);

    // 획득량 증가 1레벨당 보너스 - 기본 보상의 20% (만렙 5레벨이면 +100%)
    public long GainBonusPerLevel(int index) => Math.Max(1L, GetBaseReward(index) / 5);

    public bool TryUpgradeGain(int index)
    {
        if (index <= 0 || index >= objects.Count) return false;
        if (!IsUnlocked(index)) return false; // 아직 해금 안 된 오브젝트는 획득량 업그레이드도 불가

        long cost = GetNextGainCost(index);
        if (cost < 0) return false; // 대상 아니거나 이미 최대 레벨

        var pieceCost = new List<PieceCost> { new PieceCost(cost) };
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendPieces(pieceCost))
            return false;

        _gainLevel[index]++;
        OnGainLevelChanged?.Invoke(index, _gainLevel[index]);
        return true;
    }

    // 저장 파일 로드 시 구매 로직 없이 레벨을 그대로 대입
    public void SetGainLevel(int index, int level)
    {
        if (index < 0 || index >= _gainLevel.Length) return;
        _gainLevel[index] = Mathf.Clamp(level, 0, MaxGainLevel);
        OnGainLevelChanged?.Invoke(index, _gainLevel[index]);
    }

    // 테스트용 - 모든 오브젝트를 즉시 해금 (조각 소모 없음)
    public void UnlockAllDebug()
    {
        for (int i = 0; i < _unlocked.Length; i++)
        {
            if (_unlocked[i]) continue;
            _unlocked[i] = true;
            OnUnlockChanged?.Invoke(i);
        }
    }

    // 테스트용 - 모든 오브젝트를 해금하고 획득량 업그레이드도 전부 만렙으로 (조각 소모 없음)
    public void MaxAllDebug()
    {
        UnlockAllDebug();

        for (int i = 0; i < _gainLevel.Length; i++)
            SetGainLevel(i, MaxGainLevel);
    }

    // 테스트용 - 0번(처음부터 해금)만 남기고 모든 오브젝트 해금/획득량 업그레이드를 초기 상태로 되돌림 (조각은 환불하지 않음)
    public void ResetAll()
    {
        for (int i = 1; i < _unlocked.Length; i++)
            _unlocked[i] = debugUnlockAll; // 테스트 모드면 리셋해도 계속 다 해금

        for (int i = 0; i < _gainLevel.Length; i++)
        {
            _gainLevel[i] = 0;
            OnGainLevelChanged?.Invoke(i, 0);
        }
    }
}
