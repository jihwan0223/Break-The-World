using System;
using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    // 씬 어디서든 WeaponManager.Instance로 접근하기 위한 싱글톤
    public static WeaponManager Instance { get; private set; }

    // 무기 10종 데이터. clickDamage는 ObjectHealthCalculator의 티어별 시작 체력(4,24,64,144,304,624,1264,2536,5080,10168)을
    // 대략 4로 나눈 값으로 잡아서, 같은 티어 오브젝트를 4~8클릭 정도면 부술 수 있게 맞춤.
    // 해금 비용은 GetUnlockCost에서 오브젝트 해금 곡선을 따라 계산함
    private static readonly List<WeaponData> weapons = new List<WeaponData>
    {
        new WeaponData(1, "맨손", 1),
        new WeaponData(2, "망치", 6),
        new WeaponData(3, "곡괭이", 16),
        new WeaponData(4, "전동 드릴", 36),
        new WeaponData(5, "유압 브레이커", 76),
        new WeaponData(6, "다이너마이트", 156),
        new WeaponData(7, "폭탄", 316),
        new WeaponData(8, "미사일", 634),
        new WeaponData(9, "운석", 1270),
        new WeaponData(10, "빅뱅", 2542),
    };

    [SerializeField] private Sprite[] weaponIcons; // 무기별 이미지 (weapons 리스트와 같은 순서로 채워야 함, 아직 없는 무기는 비워둬도 됨)
    [SerializeField] private float[] weaponSizeMultipliers; // 무기별 타격 연출 크기 배율 (weapons 리스트와 같은 순서, 0 이하는 무시하고 기본값 1 유지)
    [SerializeField] private float[] weaponRotationOffsets; // 무기별 타격 연출 회전 보정 각도 (weapons 리스트와 같은 순서)

    private int equippedIndex; // 실제로 장착되어 데미지에 반영되는 무기의 weapons 리스트 인덱스 (0부터 시작)
    private bool[] _unlocked; // 무기별 해금 여부 (weapons 리스트 순서) - 맨손(0번)만 처음부터 해금

    // 무기 해금 상태가 바뀔 때마다 무기 인덱스 전달 - 세이브/UI가 구독
    public event Action<int> OnUnlockChanged;

    public int WeaponCount => weapons.Count; // UI에서 화살표로 둘러볼 때 범위 계산용
    public int EquippedIndex => equippedIndex; // 지금 장착 중인 무기의 인덱스 (UI에서 "Equipped" 표시용)
    public WeaponData CurrentWeapon => weapons[equippedIndex];
    public int CurrentClickDamage => CurrentWeapon.clickDamage;

    // Instance 없이도(에디터 등에서) 무기 이름 조회 - UpgradeManager/드롭다운 필드가 씀
    public static int StaticWeaponCount => weapons.Count;

    public static string StaticWeaponNameAt(int index) =>
        index >= 0 && index < weapons.Count ? weapons[index].weaponName : "";

    // weaponName으로 인덱스를 찾음. 없으면 -1. 공백 차이는 무시하고 대소문자 구분 없이 비교
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

    // 장착 무기가 바뀔 때마다(=Equip 호출 시) 새 무기 데이터를 전달 - 무기 UI 등이 구독
    public event Action<WeaponData> OnWeaponChanged;

    void Awake()
    {
        // 씬에 WeaponManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 인스펙터에 넣어둔 아이콘들을 같은 순서의 무기 데이터에 채워 넣음
        for (int i = 0; i < weapons.Count && weaponIcons != null && i < weaponIcons.Length; i++)
            weapons[i].icon = weaponIcons[i];

        _unlocked = new bool[weapons.Count];
        _unlocked[0] = true;
    }

    public bool IsUnlocked(int index) => index >= 0 && index < _unlocked.Length && _unlocked[index];

    // 바로 앞 무기를 해금해야 다음 무기를 살 수 있음
    public bool IsUnlockOrderMet(int index) => index > 0 && IsUnlocked(index - 1);

    // 이 무기 티어의 첫 오브젝트 해금 비용과 같음 - 새 티어에 들어갈 때 오브젝트와 무기를 둘 다 사야 해서 티어 경계가 한 번 더 무거워짐
    public long GetUnlockCost(int index) =>
        ObjectManager.Instance != null ? ObjectManager.Instance.GetUnlockCost(ObjectManager.FirstIndexOfTier(weapons[index].tier)) : 0;

    // 조각을 내고 무기 해금. 이미 해금됐거나 순서가 안 맞거나 조각이 부족하면 false
    public bool TryUnlock(int index)
    {
        if (index <= 0 || index >= weapons.Count || _unlocked[index] || !IsUnlockOrderMet(index)) return false;

        var cost = new List<PieceCost> { new PieceCost(GetUnlockCost(index)) };
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.TrySpendPieces(cost)) return false;

        SetUnlocked(index, true);
        return true;
    }

    // 구매 로직 없이 해금 상태를 그대로 대입 - 저장 파일 로드, 디버그에서 씀 (맨손은 항상 해금)
    public void SetUnlocked(int index, bool unlocked)
    {
        if (index <= 0 || index >= _unlocked.Length) return;
        _unlocked[index] = unlocked;
        OnUnlockChanged?.Invoke(index);
    }

    // 테스트용 - 모든 무기 해금
    public void UnlockAllDebug()
    {
        for (int i = 1; i < _unlocked.Length; i++)
            SetUnlocked(i, true);
    }

    // 테스트용 - 맨손만 남기고 전부 잠그고 맨손 장착
    public void ResetUnlocks()
    {
        for (int i = 1; i < _unlocked.Length; i++)
            SetUnlocked(i, false);
        Equip(0);
    }

    // 인덱스로 무기 데이터를 조회만 함 (장착은 안 함) - UI가 화살표로 둘러볼 때 사용
    public WeaponData GetWeaponAt(int index) => weapons[index];

    // Awake 시점에 한 번만 복사해두면 플레이 중 인스펙터 값을 바꿔도 반영이 안 되니까,
    // 필요할 때마다(타격 연출 재생 시) 배열에서 직접 읽어오도록 함
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

    // 해금된 무기만 장착 가능
    public void Equip(int index)
    {
        index = Mathf.Clamp(index, 0, weapons.Count - 1);
        if (!IsUnlocked(index)) return;
        equippedIndex = index;
        OnWeaponChanged?.Invoke(CurrentWeapon);
    }
}
