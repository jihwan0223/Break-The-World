using System;

// 업그레이드 노드 하나의 레벨을 저장하기 위한 항목. 인덱스가 아니라 node.id 문자열로 저장하므로
// 템플릿을 재정렬/추가해도 레벨이 엉뚱한 업그레이드로 옮겨가지 않음
[Serializable]
public class SavedUpgrade
{
    public string id;
    public int level;
}

[Serializable]
public class SaveData
{
    public long pieces; // 보유 중인 조각 개수
    public long crystals; // 보유 중인 결정 개수
    public bool pieceTutorialDone; // 첫 파편 튜토리얼(줌인 + 깜빡임)을 이미 봤는지
    public bool[] unlockedObjects; // 오브젝트별 해금 여부 (ObjectManager 리스트 순서, 길이 ObjectManager.ObjectCount)
    public int[] gainLevels; // 오브젝트별 "획득량 증가" 업그레이드 레벨 (ObjectManager 리스트 순서)
    public int weaponIndex; // 장착 중인 무기의 WeaponManager 리스트 인덱스
    public bool[] unlockedWeapons; // 무기별 해금 여부 (WeaponManager 리스트 순서)
    public int objectIndex; // 선택된 오브젝트의 ObjectManager 리스트 인덱스
    public int unlockedMaxZone; // 해금된 마지막 존 번호 (0 = 첫 존만 열림, 옛 세이브에는 없어서 0으로 읽힘)
    public int currentZone; // 보고 있던 존 번호
    public SavedUpgrade[] upgrades; // 업그레이드 노드별 레벨 (id 기반, 레벨 0인 건 저장 안 함). 옛 세이브의 upgradeLevels(int[])는 무시됨
}
