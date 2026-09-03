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

    [SerializeField] private ObjectSoundSet[] objectSounds; // objects 리스트와 같은 순서/개수로 채워야 함 (26개)

    // 오브젝트 하나의 체력 단계별 스프라이트 묶음. 인스펙터에서 objects 리스트와 같은 순서로 채워 넣음
    [Serializable]
    public class ObjectVisualSet
    {
        public Sprite[] healthStages; // 체력 100% -> 0% 순서 (예: Object1, Object1-1, Object1-2, Object1-3)
        // 크기는 코드에서 계산하지 않음 - 각 이미지의 Pixels Per Unit(Import Settings)으로 직접 맞출 것
    }

    [SerializeField] private ObjectVisualSet[] objectVisuals; // objects 리스트와 같은 순서/개수로 채워야 함 (26개)

    // 파괴 대상 오브젝트 
    private static readonly List<ObjectData> objects = new List<ObjectData>
    {
        // 오브젝트 색 조절
        new ObjectData(1, 1, "접시", new Color(0.95f, 0.95f, 0.92f)),
        new ObjectData(1, 2, "유리컵", new Color(0.93f, 0.95f, 0.96f)),
        new ObjectData(1, 3, "화분", new Color(0.80f, 0.42f, 0.25f)),
        new ObjectData(2, 1, "창문", new Color(0.60f, 0.80f, 0.90f)),
        new ObjectData(2, 2, "나무 의자", new Color(0.55f, 0.35f, 0.20f)),
        new ObjectData(2, 3, "나무 책상", new Color(0.50f, 0.30f, 0.15f)),
        new ObjectData(3, 1, "벽돌 벽", new Color(0.70f, 0.30f, 0.20f)),
        new ObjectData(3, 2, "문", new Color(0.40f, 0.25f, 0.15f)),
        new ObjectData(3, 3, "협탁", new Color(0.60f, 0.45f, 0.30f)),
        new ObjectData(4, 1, "옷장", new Color(0.35f, 0.22f, 0.12f)),
        new ObjectData(4, 2, "냉장고", new Color(0.90f, 0.90f, 0.90f)),
        new ObjectData(4, 3, "소파", new Color(0.40f, 0.45f, 0.55f)),
        new ObjectData(5, 1, "욕실", new Color(0.80f, 0.90f, 0.95f)),
        new ObjectData(5, 2, "원룸", new Color(0.75f, 0.70f, 0.60f)),
        new ObjectData(5, 3, "아파트", new Color(0.60f, 0.60f, 0.60f)),
        new ObjectData(6, 1, "저층 건물", new Color(0.55f, 0.55f, 0.58f)),
        new ObjectData(6, 2, "고층 건물", new Color(0.45f, 0.50f, 0.60f)),
        new ObjectData(6, 3, "도시 블록", new Color(0.50f, 0.50f, 0.50f)),
        new ObjectData(7, 1, "도시 전체", new Color(0.40f, 0.40f, 0.45f)),
        new ObjectData(7, 2, "대도시", new Color(0.30f, 0.30f, 0.40f)),
        new ObjectData(8, 1, "산맥", new Color(0.50f, 0.45f, 0.40f)),
        new ObjectData(8, 2, "대륙", new Color(0.40f, 0.50f, 0.30f)),
        new ObjectData(9, 1, "행성(지구형)", new Color(0.30f, 0.50f, 0.70f)),
        new ObjectData(9, 2, "가스 행성", new Color(0.80f, 0.60f, 0.30f)),
        new ObjectData(10, 1, "항성계", new Color(0.90f, 0.80f, 0.30f)),
        new ObjectData(10, 2, "은하", new Color(0.40f, 0.20f, 0.60f)),
    };

    private int equippedIndex; // 실제로 선택된 오브젝트의 objects 리스트 인덱스 (0부터 시작)

    public int ObjectCount => objects.Count; // UI에서 화살표로 둘러볼 때 범위 계산용
    public static int StaticObjectCount => objects.Count; // Instance가 아직 없는 시점(빌드 타임 등)에도 오브젝트 개수를 알아야 하는 UI용
    public int EquippedIndex => equippedIndex; // 지금 선택된 오브젝트의 인덱스 (UI에서 "Equipped" 표시용)
    public ObjectData CurrentObject => objects[equippedIndex];

    // 선택된 오브젝트가 바뀔 때마다(=Equip 호출 시) 새 오브젝트 데이터를 전달 - 오브젝트 UI 등이 구독
    public event Action<ObjectData> OnObjectChanged;

    // ---- 오브젝트 해금 시스템 ----
    // N번째 오브젝트는 (N-1)번째 오브젝트의 조각으로 해금해야 장착 가능. 0번(Plate)은 처음부터 해금된 상태로 시작
    private bool[] _unlocked;
    private const long UnlockCostBase = 8; // 해금 비용 시작값 (1번 오브젝트 기준)
    private const float UnlockCostGrowth = 4f; // 오브젝트 하나 넘어갈 때마다 해금 비용이 늘어나는 배율

    // 오브젝트가 새로 해금될 때 (objectIndex) 전달 - Object 선택 UI 등이 구독
    public event Action<int> OnUnlockChanged;

    // ---- 오브젝트별 "획득량 증가" 업그레이드 ----
    // N번째 오브젝트의 획득량 증가 업그레이드는 (N-1)번째 오브젝트의 조각으로 구매. 0번은 대상 아님(항상 0레벨)
    private int[] _gainLevel;
    private const int MaxGainLevel = 5;
    private const long GainCostBase = 15; // 비용 시작값
    private const float GainCostGrowthPerObject = 4f; // 오브젝트가 늦게 나올수록(index가 클수록) 비용이 늘어나는 배율
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

        _gainLevel = new int[objects.Count];
    }

    // 인덱스로 오브젝트 데이터를 조회만 함 (선택은 안 함) - UI가 화살표로 둘러볼 때 사용
    public ObjectData GetObjectAt(int index) => objects[index];

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

    // index번째 오브젝트를 해금하는 데 필요한 (index-1)번째 오브젝트의 조각 개수 (0번은 비용 없음 - 처음부터 해금)
    public long GetUnlockCost(int index)
    {
        if (index <= 0) return 0;
        return (long)(UnlockCostBase * Mathf.Pow(UnlockCostGrowth, index - 1));
    }

    // (index-1)번째 오브젝트의 조각으로 index번째 오브젝트를 해금 시도
    public bool TryUnlock(int index)
    {
        if (index <= 0 || index >= objects.Count) return false;
        if (_unlocked[index]) return true; // 이미 해금됨
        if (!_unlocked[index - 1]) return false; // 전 단계가 아직 안 열렸으면 순서상 해금 불가

        var cost = new List<PieceCost> { new PieceCost(index - 1, GetUnlockCost(index)) };
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendPieces(cost))
            return false;

        _unlocked[index] = true;
        OnUnlockChanged?.Invoke(index);
        return true;
    }

    // 저장 파일 로드 시 구매 로직 없이 해금 상태를 그대로 대입
    public void SetUnlocked(int index, bool unlocked)
    {
        if (index < 0 || index >= _unlocked.Length) return;
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

        float objectFactor = Mathf.Pow(GainCostGrowthPerObject, index - 1);
        float levelFactor = Mathf.Pow(GainCostGrowthPerLevel, level);
        return (long)(GainCostBase * objectFactor * levelFactor);
    }

    // index번째 오브젝트가 파괴될 때마다 추가로 더 얻는 조각 개수 (획득량 증가 업그레이드 보너스)
    public long GetGainBonus(int index)
    {
        int level = GetGainLevel(index);
        if (level <= 0) return 0;

        return level * Mathf.Max(1, index); // 오브젝트가 늦게 나올수록(index가 클수록) 레벨당 보너스도 커짐
    }

    public bool TryUpgradeGain(int index)
    {
        if (index <= 0 || index >= objects.Count) return false;
        if (!IsUnlocked(index)) return false; // 아직 해금 안 된 오브젝트는 획득량 업그레이드도 불가

        long cost = GetNextGainCost(index);
        if (cost < 0) return false; // 대상 아니거나 이미 최대 레벨

        var pieceCost = new List<PieceCost> { new PieceCost(index - 1, cost) };
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

    // 테스트용 - 0번(처음부터 해금)만 남기고 모든 오브젝트 해금/획득량 업그레이드를 초기 상태로 되돌림 (조각은 환불하지 않음)
    public void ResetAll()
    {
        for (int i = 1; i < _unlocked.Length; i++)
            _unlocked[i] = false;

        for (int i = 0; i < _gainLevel.Length; i++)
        {
            _gainLevel[i] = 0;
            OnGainLevelChanged?.Invoke(i, 0);
        }
    }
}
