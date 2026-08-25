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
    public AudioClip[] clickSounds; // 이 오브젝트를 클릭할 때 랜덤 재생할 사운드 목록 (ObjectManager 인스펙터에서 채워짐)
    public AudioClip breakSound; // 이 오브젝트가 파괴될 때 재생할 사운드 (ObjectManager 인스펙터에서 채워짐)
    public Sprite[] healthStages; // 체력 100% -> 0% 순서의 스프라이트 단계 (ObjectManager 인스펙터에서 채워짐)
    public float sizeMultiplier = 1f; // 자동 계산된 크기에 곱해지는 오브젝트별 미세 보정값 (ObjectManager 인스펙터에서 채워짐)

    public ObjectData(int tier, int indexInTier, string objectName, Color pileColor)
    {
        this.tier = tier;
        this.indexInTier = indexInTier;
        this.objectName = objectName;
        this.pileColor = pileColor;
    }
}
