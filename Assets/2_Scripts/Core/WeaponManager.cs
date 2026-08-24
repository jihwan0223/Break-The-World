using System;
using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    // 씬 어디서든 WeaponManager.Instance로 접근하기 위한 싱글톤
    public static WeaponManager Instance { get; private set; }

    // 무기 10종 데이터. clickDamage는 ObjectHealthCalculator의 티어별 시작 체력(4,24,64,144,304,624,1264,2536,5080,10168)을
    // 대략 4로 나눈 값으로 잡아서, 같은 티어 오브젝트를 4~8클릭 정도면 부술 수 있게 맞춤.
    // unlockCost는 지금 당장 쓰이진 않지만, 나중에 골드 해금 붙일 때 쓸 자리표시 값(대략 x3씩 증가)
    private static readonly List<WeaponData> weapons = new List<WeaponData>
    {
        new WeaponData(1, "Bare Hands", 1, 0),
        new WeaponData(2, "Hammer", 6, 150),
        new WeaponData(3, "Pickaxe", 16, 450),
        new WeaponData(4, "Power Drill", 36, 1350),
        new WeaponData(5, "Hydraulic Breaker", 76, 4050),
        new WeaponData(6, "Dynamite", 156, 12150),
        new WeaponData(7, "Bomb", 316, 36450),
        new WeaponData(8, "Missile", 634, 109350),
        new WeaponData(9, "Meteor", 1270, 328050),
        new WeaponData(10, "Big Bang", 2542, 984150),
    };

    private int equippedIndex; // 실제로 장착되어 데미지에 반영되는 무기의 weapons 리스트 인덱스 (0부터 시작)

    public int WeaponCount => weapons.Count; // UI에서 화살표로 둘러볼 때 범위 계산용
    public int EquippedIndex => equippedIndex; // 지금 장착 중인 무기의 인덱스 (UI에서 "Equipped" 표시용)
    public WeaponData CurrentWeapon => weapons[equippedIndex];
    public int CurrentClickDamage => CurrentWeapon.clickDamage;

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
    }

    // 인덱스로 무기 데이터를 조회만 함 (장착은 안 함) - UI가 화살표로 둘러볼 때 사용
    public WeaponData GetWeaponAt(int index) => weapons[index];

    // 지금은 해금 여부와 상관없이 자유롭게 장착 가능 (제한 없음)
    public void Equip(int index)
    {
        index = Mathf.Clamp(index, 0, weapons.Count - 1);
        equippedIndex = index;
        OnWeaponChanged?.Invoke(CurrentWeapon);
    }
}
