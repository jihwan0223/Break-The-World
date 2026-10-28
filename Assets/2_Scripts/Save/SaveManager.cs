using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [SerializeField] private string saveFileName = "savedata.json";
    [SerializeField] private float saveDebounceDelay = 1f; // 초

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private bool _isDirty;
    private Coroutine _saveRoutine;

    void Awake()
    {
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
        if (_isDirty)
            SaveNow();
    }

    public void DiscardPending()
    {
        if (_saveRoutine != null) StopCoroutine(_saveRoutine);
        _saveRoutine = null;
        _isDirty = false;
    }

    public void MarkDirty()
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

    public void SaveNow()
    {
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
            totalPieces = CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalPieces : 0,
            everHadCrystal = CurrencyManager.Instance != null && CurrencyManager.Instance.EverHadCrystal,
            killsSinceCrystal = CurrencyManager.Instance != null ? CurrencyManager.Instance.KillsSinceCrystal : 0,
            totalCrystals = CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalCrystals : 0,
            statObjectsBroken = GameStats.ObjectsBroken,
            statClicks = GameStats.Clicks,
            statCrits = GameStats.Crits,
            statSuperCrits = GameStats.SuperCrits,
            statBossClears = GameStats.BossClears,
            statPlaySeconds = GameStats.PlaySeconds,
            statBrokenPerObject = GameStats.BrokenPerObject,
            pieceTutorialDone = PieceTutorial.Instance != null && PieceTutorial.Instance.Done,
            unlockedObjects = unlockedObjects,
            gainLevels = gainLevels,
            weaponIndex = WeaponManager.Instance != null ? WeaponManager.Instance.EquippedIndex : 0,
            objectIndex = ObjectManager.Instance != null ? ObjectManager.Instance.EquippedIndex : 0,
            unlockedMaxZone = ZoneManager.Instance != null ? ZoneManager.Instance.UnlockedMaxZone : 0,
            currentZone = ZoneManager.Instance != null ? ZoneManager.Instance.CurrentZone : 0,
            finalBossCleared = ZoneBreaker.FinalBossCleared,
            seenGates = GateReveals.ToArray(),
            upgrades = upgrades.ToArray(),
        };

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        _isDirty = false;
    }

    public void Load()
    {
        GameStats.Reset();
        GateReveals.Reset();
        if (!File.Exists(SavePath))
            return;

        string json = File.ReadAllText(SavePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        GameStats.ObjectsBroken = data.statObjectsBroken;
        GameStats.Clicks = data.statClicks;
        GameStats.Crits = data.statCrits;
        GameStats.SuperCrits = data.statSuperCrits;
        GameStats.BossClears = data.statBossClears;
        GameStats.PlaySeconds = data.statPlaySeconds;
        GameStats.BrokenPerObject = data.statBrokenPerObject ?? new long[0];

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.SetPieces(data.pieces);

        if (PieceTutorial.Instance != null)
            PieceTutorial.Instance.Done = data.pieceTutorialDone;

        // Equip 전에 해금 상태부터 복원
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
        ZoneBreaker.FinalBossCleared = data.finalBossCleared;
        GateReveals.Load(data.seenGates);

        // 장착 전에 해금 상태부터 복원
        if (WeaponManager.Instance != null && data.unlockedWeapons != null)
        {
            for (int i = 0; i < data.unlockedWeapons.Length; i++)
                WeaponManager.Instance.SetUnlocked(i, data.unlockedWeapons[i]);
        }
        WeaponManager.Instance?.Equip(data.weaponIndex);
        ObjectManager.Instance?.Equip(data.objectIndex);

        if (UpgradeManager.Instance != null && data.upgrades != null)
        {
            foreach (SavedUpgrade u in data.upgrades)
                UpgradeManager.Instance.SetLevel(u.id, u.level);
        }

        // 업그레이드를 복원한 뒤에 넣음 (옛 세이브 보정에 필요)
        if (CurrencyManager.Instance != null)
        {
            bool hadCrystal = data.everHadCrystal || data.crystals > 0
                || (UpgradeManager.Instance != null && UpgradeManager.Instance.HoverPickupIsUnlocked);
            CurrencyManager.Instance.SetProgress(System.Math.Max(data.totalPieces, data.pieces), hadCrystal, data.killsSinceCrystal,
                System.Math.Max(data.totalCrystals, data.crystals)); // 옛 세이브는 보유량으로
            CurrencyManager.Instance.SetCrystals(data.crystals);
        }
    }
}
