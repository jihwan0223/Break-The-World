public static class ObjectHealthCalculator
{
    // ObjectManager.objects와 순서/개수가 같아야 함
    private static readonly int[] tierSizes = { 3, 2, 3, 2, 3, 2, 3, 2 };

    // 티어별 체력·데미지 공통 배율
    private static readonly int[] tierScales = { 1, 2, 5, 10, 25, 50, 75, 100 };

    public static int ScaleOfTier(int weaponTier) => tierScales[System.Math.Clamp(weaponTier, 1, tierScales.Length) - 1];

    private const int baseHP = 10;
    private const int stepPerObject = 4;
    private const int gateMultiplier = 2;

    public static int Calculate(int weaponTier, int indexInTier)
    {
        int tierBaseHP = baseHP;

        for (int t = 1; t < weaponTier; t++)
        {
            int prevTierSize = tierSizes[t - 1];
            int prevTierLastHP = tierBaseHP + stepPerObject * (prevTierSize - 1);
            tierBaseHP = prevTierLastHP * gateMultiplier;
        }

        return (tierBaseHP + stepPerObject * (indexInTier - 1)) * ScaleOfTier(weaponTier);
    }
}
