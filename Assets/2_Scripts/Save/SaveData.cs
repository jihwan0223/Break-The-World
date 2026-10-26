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
    public long totalPieces; // 지금까지 얻은 조각 누적 (옛 세이브에는 없어서 0으로 읽힘 - 불러올 때 보유량으로 채움)
    public bool everHadCrystal; // 결정을 한 번이라도 얻었는지 (옛 세이브에는 없음 - 결정 보유/결정 노드 구매 여부로 채움)
    public int killsSinceCrystal; // 마지막 결정 이후 부순 오브젝트 수 (결정 천장용, 옛 세이브에는 없어서 0)
    public long totalCrystals; // 지금까지 얻은 결정 누적 (써도 줄지 않음) - 결정 천장을 끌 시점 판단용 (옛 세이브에는 없어서 0)
    public long statObjectsBroken; // 통계: 부순 오브젝트 수
    public long statClicks; // 통계: 직접 클릭 수
    public long statCrits; // 통계: 치명타 횟수
    public long statSuperCrits; // 통계: 슈퍼 크리티컬 횟수
    public long statBossClears; // 통계: 보스 처치 횟수
    public double statPlaySeconds; // 통계: 플레이 시간(초)
    public long[] statBrokenPerObject; // 통계: 오브젝트 종류별 부순 횟수
    public bool pieceTutorialDone; // 첫 파편 튜토리얼(줌인 + 깜빡임)을 이미 봤는지
    public bool[] unlockedObjects; // 오브젝트별 해금 여부 (ObjectManager 리스트 순서, 길이 ObjectManager.ObjectCount)
    public int[] gainLevels; // 오브젝트별 "획득량 증가" 업그레이드 레벨 (ObjectManager 리스트 순서)
    public int weaponIndex; // 장착 중인 무기의 WeaponManager 리스트 인덱스
    public bool[] unlockedWeapons; // 무기별 해금 여부 (WeaponManager 리스트 순서)
    public int objectIndex; // 선택된 오브젝트의 ObjectManager 리스트 인덱스
    public int unlockedMaxZone; // 해금된 마지막 존 번호 (0 = 첫 존만 열림, 옛 세이브에는 없어서 0으로 읽힘)
    public int currentZone; // 보고 있던 존 번호
    public bool finalBossCleared; // 마지막 보스(회사)를 잡았는지 (맵1/2 보스는 다음 맵 해금 여부로 판단)
    public SavedUpgrade[] upgrades; // 업그레이드 노드별 레벨 (id 기반, 레벨 0인 건 저장 안 함). 옛 세이브의 upgradeLevels(int[])는 무시됨
}
