using System.Collections;
using System.IO;
using UnityEngine;

// 골드/무기/오브젝트 선택 상태를 JSON 파일로 저장하고 불러오는 매니저.
// 값이 바뀔 때마다 바로 쓰지 않고, 일정 시간 동안 변경이 없을 때 한 번만 저장(디바운스)해서
// 연속 클릭 중에 매번 디스크에 쓰는 걸 방지함.
public class SaveManager : MonoBehaviour
{
    // 씬 어디서든 SaveManager.Instance로 접근하기 위한 싱글톤
    public static SaveManager Instance { get; private set; }

    [SerializeField] private string saveFileName = "savedata.json"; // 저장 파일 이름
    [SerializeField] private float saveDebounceDelay = 1f; // 마지막 변경 후 이 시간(초)만큼 조용하면 저장

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private bool _isDirty; // 저장 안 된 변경사항이 있는지
    private Coroutine _saveRoutine; // 디바운스 대기 중인 저장 코루틴

    void Awake()
    {
        // 씬에 SaveManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        Load();

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGoldChanged += _ => MarkDirty();

        if (WeaponManager.Instance != null)
            WeaponManager.Instance.OnWeaponChanged += _ => MarkDirty();

        if (ObjectManager.Instance != null)
            ObjectManager.Instance.OnObjectChanged += _ => MarkDirty();

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged += (_, __) => MarkDirty();

        if (StatsManager.Instance != null)
            StatsManager.Instance.OnObjectDestroyCountChanged += (_, __) => MarkDirty();
    }

    void OnApplicationQuit()
    {
        // 디바운스 대기 중이던 변경사항이 있으면 종료 직전에 마지막으로 한 번 저장
        if (_isDirty)
            SaveNow();
    }

    private void MarkDirty()
    {
        _isDirty = true;

        if (_saveRoutine == null)
            _saveRoutine = StartCoroutine(DebouncedSave());
    }

    private IEnumerator DebouncedSave()
    {
        yield return new WaitForSeconds(saveDebounceDelay);
        SaveNow();
        _saveRoutine = null;
    }

    // 각 매니저의 현재 상태를 모아서 즉시 파일에 저장
    public void SaveNow()
    {
        // 업그레이드 레벨을 UpgradeType 순서대로 채운 배열 (매니저가 없으면 전부 0)
        var upgradeLevels = new int[UpgradeManager.UpgradeCount];
        if (UpgradeManager.Instance != null)
        {
            for (int i = 0; i < upgradeLevels.Length; i++)
                upgradeLevels[i] = UpgradeManager.Instance.GetLevel((UpgradeManager.UpgradeType)i);
        }

        // 오브젝트별 파괴 횟수를 ObjectManager 리스트 순서대로 채운 배열 (매니저가 없으면 전부 0)
        int objectCount = ObjectManager.Instance != null ? ObjectManager.Instance.ObjectCount : 0;
        var destroyCounts = new int[objectCount];
        if (StatsManager.Instance != null)
        {
            for (int i = 0; i < destroyCounts.Length; i++)
                destroyCounts[i] = StatsManager.Instance.GetDestroyCount(i);
        }

        var data = new SaveData
        {
            gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.Gold : 0,
            weaponIndex = WeaponManager.Instance != null ? WeaponManager.Instance.EquippedIndex : 0,
            objectIndex = ObjectManager.Instance != null ? ObjectManager.Instance.EquippedIndex : 0,
            upgradeLevels = upgradeLevels,
            destroyCounts = destroyCounts,
        };

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        _isDirty = false;
    }

    // 저장 파일이 있으면 읽어서 각 매니저에 적용, 없으면 아무것도 안 함(기본값 그대로 시작)
    public void Load()
    {
        if (!File.Exists(SavePath))
            return;

        string json = File.ReadAllText(SavePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        CurrencyManager.Instance?.SetGold(data.gold);
        WeaponManager.Instance?.Equip(data.weaponIndex);
        ObjectManager.Instance?.Equip(data.objectIndex);

        // 옛날 저장 파일(업그레이드 도입 전)에는 upgradeLevels가 없을 수 있으니 길이 확인 후 적용
        if (UpgradeManager.Instance != null && data.upgradeLevels != null)
        {
            for (int i = 0; i < data.upgradeLevels.Length && i < UpgradeManager.UpgradeCount; i++)
                UpgradeManager.Instance.SetLevel((UpgradeManager.UpgradeType)i, data.upgradeLevels[i]);
        }

        // 옛날 저장 파일(파괴 횟수 기록 도입 전)에는 destroyCounts가 없을 수 있으니 길이 확인 후 적용
        if (StatsManager.Instance != null && data.destroyCounts != null)
        {
            for (int i = 0; i < data.destroyCounts.Length; i++)
                StatsManager.Instance.SetDestroyCount(i, data.destroyCounts[i]);
        }
    }
}
