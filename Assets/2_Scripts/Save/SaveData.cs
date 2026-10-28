using System;

// id 문자열로 저장 - 노드 순서가 바뀌어도 안전
[Serializable]
public class SavedUpgrade
{
    public string id;
    public int level;
}

[Serializable]
public class SaveData
{
    public long pieces;
    public long crystals;
    public long totalPieces; // 누적 (옛 세이브엔 없음)
    public bool everHadCrystal;
    public int killsSinceCrystal; // 결정 천장용
    public long totalCrystals; // 누적
    public long statObjectsBroken;
    public long statClicks;
    public long statCrits;
    public long statSuperCrits;
    public long statBossClears;
    public double statPlaySeconds; // 초
    public long[] statBrokenPerObject;
    public bool pieceTutorialDone;
    public bool[] unlockedObjects;
    public int[] gainLevels;
    public int weaponIndex;
    public bool[] unlockedWeapons;
    public int objectIndex;
    public int unlockedMaxZone; // 0 = 첫 존만
    public int currentZone;
    public string[] seenGates;
    public bool finalBossCleared;
    public SavedUpgrade[] upgrades; // 레벨 0은 저장 안 함
}
