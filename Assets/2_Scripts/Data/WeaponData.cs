using System;
using UnityEngine;

[Serializable]
public class WeaponData
{
    public int tier; // 1부터 (1 = 맨손)
    public string weaponName;
    public int clickDamage;
    public Sprite icon;

    public WeaponData(int tier, string weaponName, int clickDamage)
    {
        this.tier = tier;
        this.weaponName = weaponName;
        this.clickDamage = clickDamage * ObjectHealthCalculator.ScaleOfTier(tier); // 같은 티어 오브젝트 체력과 같은 배율
    }
}
