using System;

// 저장 파일에 그대로 직렬화되는 데이터. JsonUtility로 JSON 문자열로 변환됨
[Serializable]
public class SaveData
{
    public int gold; // 보유 골드
    public int weaponIndex; // 장착 중인 무기의 WeaponManager 리스트 인덱스
    public int objectIndex; // 선택된 오브젝트의 ObjectManager 리스트 인덱스
}
