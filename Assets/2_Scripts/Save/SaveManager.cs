using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [SerializeField] private string saveFileName = "savedata.json";
    [SerializeField] private float saveDebounceDelay = 1f; // 초
    [SerializeField] private float periodicSaveInterval = 30f; // 초 - 통계(플레이 시간 등)만 바뀌어도 이 간격으로 저장

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private bool _isDirty;
    private Coroutine _saveRoutine;
    private bool _saveThisFrame; // 이번 프레임 끝에 저장 (같은 프레임에 여러 번 불려도 한 번만 씀)
    private float _periodicTimer; // 다음 주기 저장까지 남은 시간 (초)
    private bool _loaded; // 불러오기 전에 저장하면 빈 상태로 덮어써서 막음

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
            CurrencyManager.Instance.OnStarShardsChanged += _ => SaveSoon();
        }

        if (WeaponManager.Instance != null)
        {
            WeaponManager.Instance.OnWeaponChanged += _ => SaveSoon();
            WeaponManager.Instance.OnUnlockChanged += _ => SaveSoon();
        }

        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.OnObjectChanged += _ => SaveSoon();
            ObjectManager.Instance.OnUnlockChanged += _ => SaveSoon();
            ObjectManager.Instance.OnGainLevelChanged += (_, __) => SaveSoon();
        }

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged += (_, __) => SaveSoon();

        if (ZoneManager.Instance != null)
            ZoneManager.Instance.OnZoneChanged += SaveSoon;

        _periodicTimer = periodicSaveInterval;
        _loaded = true;
    }

    void Update()
    {
        _periodicTimer -= Time.unscaledDeltaTime;
        if (_periodicTimer <= 0f) SaveSoon();
    }

    void LateUpdate()
    {
        if (_saveThisFrame) SaveNow();
    }

    void OnApplicationQuit()
    {
        if (_isDirty || _saveThisFrame)
            SaveNow();
    }

    // 다른 창으로 가거나 최소화할 때 (그대로 꺼져도 안 날아가게)
    void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        SaveNow();
        PlayerPrefs.Save();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        SaveNow();
        PlayerPrefs.Save();
    }

    // 무기 변경, 업그레이드 구매 같은 중요한 변화: 1초 기다리지 않고 이번 프레임 끝에 저장
    public void SaveSoon() => _saveThisFrame = true;

    public void MarkDirty()
    {
        _isDirty = true;

        if (_saveRoutine == null)
            _saveRoutine = StartCoroutine(DebouncedSave());
    }

    private IEnumerator DebouncedSave()
    {
        yield return new WaitForSecondsRealtime(saveDebounceDelay); // 게임이 멈춰 있어도 저장
        SaveNow();
        _saveRoutine = null;
    }

    public void SaveNow()
    {
        // 불러오기 전이나 플레이 중 스크립트 리로드 뒤(매니저가 비어 있음)에 쓰면 세이브가 0으로 덮임
        if (!_loaded || CurrencyManager.Instance == null) return;
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
            starShards = CurrencyManager.Instance.GetStarShards(),
            totalStarShards = CurrencyManager.Instance.TotalStarShards,
            statObjectsBroken = GameStats.ObjectsBroken,
            statTotalDamage = GameStats.TotalDamage,
            statBestHit = GameStats.BestHit,
            statCrits = GameStats.Crits,
            statSuperCrits = GameStats.SuperCrits,
            statBossClears = GameStats.BossClears,
            statRebirths = GameStats.Rebirths,
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
        _saveThisFrame = false;
        _periodicTimer = periodicSaveInterval;
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
        GameStats.TotalDamage = data.statTotalDamage;
        GameStats.BestHit = data.statBestHit;
        GameStats.Crits = data.statCrits;
        GameStats.SuperCrits = data.statSuperCrits;
        GameStats.BossClears = data.statBossClears;
        GameStats.Rebirths = data.statRebirths;
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
            CurrencyManager.Instance.SetStarShardProgress(System.Math.Max(data.totalStarShards, data.starShards));
            CurrencyManager.Instance.SetStarShards(data.starShards);
        }
    }
}
