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
    public long starShards; // 별조각 (환생 재화)
    public long totalStarShards; // 지금까지 얻은 별조각
    public long statObjectsBroken;
    public long statTotalDamage;
    public long statBestHit;
    public long statCrits;
    public long statSuperCrits;
    public long statBossClears;
    public long statRebirths; // 환생한 횟수
    public double statPlaySeconds; // 초
    public long[] statBrokenPerObject;
    public bool pieceTutorialDone;
    public int tutorialStep; // 안내 글 진행 단계 (TutorialGuideUI)
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
