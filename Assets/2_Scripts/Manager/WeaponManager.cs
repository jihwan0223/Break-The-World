using System;
using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance { get; private set; }

    // 같은 티어 오브젝트를 4~8클릭에 부수도록 잡은 데미지
    private static readonly List<WeaponData> weapons = new List<WeaponData>
    {
        new WeaponData(1, "맨손", 1),
        new WeaponData(2, "프라이팬", 6),
        new WeaponData(3, "망치", 16),
        new WeaponData(4, "야구 방망이", 36),
        new WeaponData(5, "곡괭이", 76),
        new WeaponData(6, "철퇴", 156),
    };

    [SerializeField] private Sprite[] weaponIcons; // weapons와 같은 순서
    [SerializeField] private float[] weaponSizeMultipliers; // weapons와 같은 순서, 0 이하는 1
    [SerializeField] private float[] weaponRotationOffsets; // 도, weapons와 같은 순서

    private int equippedIndex;
    private bool[] _unlocked;

    public event Action<int> OnUnlockChanged;

    public int WeaponCount => weapons.Count;
    public int EquippedIndex => equippedIndex;
    public WeaponData CurrentWeapon => weapons[equippedIndex];
    public int CurrentClickDamage => CurrentWeapon.clickDamage;

    public static int StaticWeaponCount => weapons.Count;

    public static string StaticWeaponNameAt(int index) =>
        index >= 0 && index < weapons.Count ? weapons[index].weaponName : "";

    public static int StaticIndexOfWeaponName(string weaponName)
    {
        if (string.IsNullOrWhiteSpace(weaponName)) return -1;

        string wanted = weaponName.Trim();
        for (int i = 0; i < weapons.Count; i++)
        {
            if (string.Equals(weapons[i].weaponName.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public event Action<WeaponData> OnWeaponChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        for (int i = 0; i < weapons.Count && weaponIcons != null && i < weaponIcons.Length; i++)
            weapons[i].icon = weaponIcons[i];

        _unlocked = new bool[weapons.Count];
        _unlocked[0] = true;
    }

    public bool IsUnlocked(int index) => index >= 0 && index < _unlocked.Length && _unlocked[index];

    public bool IsUnlockOrderMet(int index) => index > 0 && IsUnlocked(index - 1);

    // 새 티어 첫 오브젝트의 해금 비용과 같음
    public long GetUnlockCost(int index) =>
        ObjectManager.Instance != null ? ObjectManager.Instance.GetUnlockCost(ObjectManager.FirstIndexOfTier(weapons[index].tier)) : 0;

    public bool AnyUnlockAffordable()
    {
        if (CurrencyManager.Instance == null) return false;

        long have = CurrencyManager.Instance.GetPieces();
        for (int i = 1; i < weapons.Count; i++)
            if (!_unlocked[i] && IsUnlockOrderMet(i) && have >= GetUnlockCost(i)) return true;
        return false;
    }

    public bool TryUnlock(int index)
    {
        if (index <= 0 || index >= weapons.Count || _unlocked[index] || !IsUnlockOrderMet(index)) return false;

        var cost = new List<PieceCost> { new PieceCost(GetUnlockCost(index)) };
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendPieces(cost)) return false;

        SetUnlocked(index, true);
        return true;
    }

    public void SetUnlocked(int index, bool unlocked)
    {
        if (index <= 0 || index >= _unlocked.Length) return;
        _unlocked[index] = unlocked;
        OnUnlockChanged?.Invoke(index);
    }

    public void UnlockAllDebug()
    {
        for (int i = 1; i < _unlocked.Length; i++)
            SetUnlocked(i, true);
    }

    public void ResetUnlocks()
    {
        for (int i = 1; i < _unlocked.Length; i++)
            SetUnlocked(i, false);
        Equip(0);
    }

    public WeaponData GetWeaponAt(int index) => weapons[index];

    // 플레이 중 인스펙터 수정이 반영되게 매번 읽음
    public float GetSizeMultiplier(int index)
    {
        if (weaponSizeMultipliers != null && index >= 0 && index < weaponSizeMultipliers.Length && weaponSizeMultipliers[index] > 0f)
            return weaponSizeMultipliers[index];

        return 1f;
    }

    public float GetRotationOffset(int index)
    {
        if (weaponRotationOffsets != null && index >= 0 && index < weaponRotationOffsets.Length)
            return weaponRotationOffsets[index];

        return 0f;
    }

    public void Equip(int index)
    {
        index = Mathf.Clamp(index, 0, weapons.Count - 1);
        if (!IsUnlocked(index)) return;
        equippedIndex = index;
        OnWeaponChanged?.Invoke(CurrentWeapon);
    }
}
