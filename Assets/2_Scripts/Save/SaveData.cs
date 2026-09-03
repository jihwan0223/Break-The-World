using System;

// 조각 하나의 보유량을 저장하기 위한 항목 (JsonUtility가 Dictionary를 직렬화 못 해서 배열로 저장함)
[Serializable]
public class SavedPiece
{
    public int objectIndex; // 어떤 오브젝트의 조각인지
    public long amount; // 보유 개수
}

// 업그레이드 노드 하나의 레벨을 저장하기 위한 항목. 인덱스가 아니라 node.id 문자열로 저장하므로
// 템플릿을 재정렬/추가해도 레벨이 엉뚱한 업그레이드로 옮겨가지 않음
[Serializable]
public class SavedUpgrade
{
    public string id; // UpgradeManager.UpgradeNode.id ("{템플릿id}#{tier}")
    public int level; // 그 노드의 레벨
}

// 저장 파일에 그대로 직렬화되는 데이터. JsonUtility로 JSON 문자열로 변환됨
[Serializable]
public class SaveData
{
    public SavedPiece[] pieces; // 오브젝트별 보유 조각 (0개인 것은 굳이 저장 안 함)
    public bool[] unlockedObjects; // 오브젝트별 해금 여부 (ObjectManager 리스트 순서, 길이 ObjectManager.ObjectCount)
    public int[] gainLevels; // 오브젝트별 "획득량 증가" 업그레이드 레벨 (ObjectManager 리스트 순서)
    public int weaponIndex; // 장착 중인 무기의 WeaponManager 리스트 인덱스
    public int objectIndex; // 선택된 오브젝트의 ObjectManager 리스트 인덱스
    public SavedUpgrade[] upgrades; // 업그레이드 노드별 레벨 (id 기반, 레벨 0인 건 저장 안 함). 옛 세이브의 upgradeLevels(int[])는 무시됨
}
