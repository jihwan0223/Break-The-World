using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 조각/무기/오브젝트 선택 상태를 JSON 파일로 저장하고 불러오는 매니저.
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
        {
            CurrencyManager.Instance.OnPiecesChanged += _ => MarkDirty();
            CurrencyManager.Instance.OnCrystalsChanged += _ => MarkDirty();
        }

        if (WeaponManager.Instance != null)
        {
            WeaponManager.Instance.OnWeaponChanged += _ => MarkDirty();
            WeaponManager.Instance.OnUnlockChanged += _ => MarkDirty();
        }

        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.OnObjectChanged += _ => MarkDirty();
            ObjectManager.Instance.OnUnlockChanged += _ => MarkDirty();
            ObjectManager.Instance.OnGainLevelChanged += (_, __) => MarkDirty();
        }

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged += (_, __) => MarkDirty();

        if (ZoneManager.Instance != null)
            ZoneManager.Instance.OnZoneChanged += MarkDirty;
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
        // 업그레이드 노드별 레벨을 id로 저장 (레벨 0인 건 생략)
        var upgrades = new List<SavedUpgrade>();
        if (UpgradeManager.Instance != null)
        {
            foreach (UpgradeManager.UpgradeNode node in UpgradeManager.Instance.Nodes)
            {
                int level = UpgradeManager.Instance.GetLevel(node.id);
                if (level > 0)
                    upgrades.Add(new SavedUpgrade { id = node.id, level = level });
            }
        }

        // 오브젝트별 해금 상태 / 획득량 업그레이드 레벨
        int objectCount = ObjectManager.Instance != null ? ObjectManager.Instance.ObjectCount : 0;
        var unlockedObjects = new bool[objectCount];
        var gainLevels = new int[objectCount];
        if (ObjectManager.Instance != null)
        {
            for (int i = 0; i < objectCount; i++)
            {
                unlockedObjects[i] = ObjectManager.Instance.IsUnlocked(i);
                gainLevels[i] = ObjectManager.Instance.GetGainLevel(i);
            }
        }

        int weaponCount = WeaponManager.Instance != null ? WeaponManager.Instance.WeaponCount : 0;
        var unlockedWeapons = new bool[weaponCount];
        for (int i = 0; i < weaponCount; i++)
            unlockedWeapons[i] = WeaponManager.Instance.IsUnlocked(i);

        var data = new SaveData
        {
            unlockedWeapons = unlockedWeapons,
            pieces = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetPieces() : 0,
            crystals = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCrystals() : 0,
            pieceTutorialDone = PieceTutorial.Instance != null && PieceTutorial.Instance.Done,
            unlockedObjects = unlockedObjects,
            gainLevels = gainLevels,
            weaponIndex = WeaponManager.Instance != null ? WeaponManager.Instance.EquippedIndex : 0,
            objectIndex = ObjectManager.Instance != null ? ObjectManager.Instance.EquippedIndex : 0,
            unlockedMaxZone = ZoneManager.Instance != null ? ZoneManager.Instance.UnlockedMaxZone : 0,
            currentZone = ZoneManager.Instance != null ? ZoneManager.Instance.CurrentZone : 0,
            upgrades = upgrades.ToArray(),
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

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.SetPieces(data.pieces);
            CurrencyManager.Instance.SetCrystals(data.crystals);
        }

        if (PieceTutorial.Instance != null)
            PieceTutorial.Instance.Done = data.pieceTutorialDone;

        // 해금 상태를 먼저 복원해야 그 다음 Equip()이 막히지 않음
        if (ObjectManager.Instance != null && data.unlockedObjects != null)
        {
            for (int i = 0; i < data.unlockedObjects.Length; i++)
                ObjectManager.Instance.SetUnlocked(i, data.unlockedObjects[i]);
        }

        if (ObjectManager.Instance != null && data.gainLevels != null)
        {
            for (int i = 0; i < data.gainLevels.Length; i++)
                ObjectManager.Instance.SetGainLevel(i, data.gainLevels[i]);
        }

        ZoneManager.Instance?.SetState(data.unlockedMaxZone, data.currentZone);

        // 해금 상태를 먼저 복원해야 장착이 막히지 않음
        if (WeaponManager.Instance != null && data.unlockedWeapons != null)
        {
            for (int i = 0; i < data.unlockedWeapons.Length; i++)
                WeaponManager.Instance.SetUnlocked(i, data.unlockedWeapons[i]);
        }
        WeaponManager.Instance?.Equip(data.weaponIndex);
        ObjectManager.Instance?.Equip(data.objectIndex);

        // 업그레이드 레벨을 id로 복원 (지금 존재하지 않는 id는 SetLevel 내부에서 무시됨).
        // 옛 세이브에는 upgrades가 없어서 업그레이드만 전부 0부터 시작함 (조각/오브젝트/무기는 그대로 로드됨)
        if (UpgradeManager.Instance != null && data.upgrades != null)
        {
            foreach (SavedUpgrade u in data.upgrades)
                UpgradeManager.Instance.SetLevel(u.id, u.level);
        }
    }
}
