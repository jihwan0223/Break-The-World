using System;
using UnityEngine;

// 파괴 대상 오브젝트 하나의 데이터. WeaponData와 같은 이유로 순수 C# 클래스로 둠
// (ScriptableObject 애셋 26개를 에디터에서 직접 만들 필요 없이 코드에 바로 정의)
[Serializable]
public class ObjectData
{
    public int tier; // 이 오브젝트가 속한 무기 티어 (1부터 시작, WeaponData.tier와 대응)
    public int indexInTier; // 같은 티어 내에서 이 오브젝트의 순번 (1부터 시작, ObjectHealthCalculator에 그대로 넘김)
    public string objectName; // 화면에 보일 오브젝트 이름 (영어)
    public Color pileColor; // 이 오브젝트를 부술 때 DebrisPool에 쌓이는 조각 색상 (지금은 placeholder, 나중에 조정)

    public ObjectData(int tier, int indexInTier, string objectName, Color pileColor)
    {
        this.tier = tier;
        this.indexInTier = indexInTier;
        this.objectName = objectName;
        this.pileColor = pileColor;
    }
}
