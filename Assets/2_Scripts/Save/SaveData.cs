using System;

// 조각 하나의 보유량을 저장하기 위한 항목 (JsonUtility가 Dictionary를 직렬화 못 해서 배열로 저장함)
[Serializable]
public class SavedPiece
{
    public int objectIndex; // 어떤 오브젝트의 조각인지
    public long amount; // 보유 개수
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
    public int[] upgradeLevels; // 업그레이드별 현재 레벨 (UpgradeManager.UpgradeType 순서, 길이 UpgradeManager.UpgradeCount)
}
