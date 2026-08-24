using System;

// 무기 하나의 데이터. ScriptableObject가 아니라 순수 C# 클래스로 둬서
// WeaponManager 코드 안에 10개를 바로 정의할 수 있게 함 (에디터에서 애셋 10개 만들 필요 없음)
[Serializable]
public class WeaponData
{
    public int tier; // 몇 번째 무기인지 (1부터 시작, 1=맨손)
    public string weaponName; // 화면에 보일 무기 이름 (영어)
    public int clickDamage; // 이 무기 장착 시 클릭 한 번당 데미지
    public int unlockCost; // 이 무기를 해금하는 데 필요한 골드 (지금은 사용 안 하지만 나중을 위해 미리 정의)

    public WeaponData(int tier, string weaponName, int clickDamage, int unlockCost)
    {
        this.tier = tier;
        this.weaponName = weaponName;
        this.clickDamage = clickDamage;
        this.unlockCost = unlockCost;
    }
}
