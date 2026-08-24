// 오브젝트 체력을 "무기 티어 + 같은 티어 내 순번"만으로 자동 계산해주는 유틸리티.
// 기획: 같은 무기 안에서는 완만하게(+4), 무기가 바뀌는 시점엔 확 뛰게(x2) 해서
// "현재 도구로는 감당 안 되는 벽" 게이트 메커닉을 수치로 표현함.
public static class ObjectHealthCalculator
{
    // 무기 티어(1~10)별 오브젝트 개수. 총합 26개 (기획 기준 6개 티어는 3개씩, 4개 티어는 2개씩)
    private static readonly int[] tierSizes = { 3, 3, 3, 3, 3, 3, 2, 2, 2, 2 };

    private const int baseHP = 10; // 1티어 첫 오브젝트(접시)의 체력
    private const int stepPerObject = 4; // 같은 티어 내에서 오브젝트 하나 넘어갈 때마다 늘어나는 체력
    private const int gateMultiplier = 2; // 무기 티어가 바뀔 때(게이트) 곱해지는 배율

    // weaponTier: 1부터 시작하는 무기 티어 번호
    // indexInTier: 1부터 시작하는, 같은 티어 내에서 이 오브젝트의 순번
    public static int Calculate(int weaponTier, int indexInTier)
    {
        int tierBaseHP = baseHP; // 현재 계산 중인 티어의 첫 오브젝트 체력

        for (int t = 1; t < weaponTier; t++)
        {
            int prevTierSize = tierSizes[t - 1]; // 이전 티어에 오브젝트가 몇 개 있었는지
            int prevTierLastHP = tierBaseHP + stepPerObject * (prevTierSize - 1); // 이전 티어 마지막 오브젝트 체력
            tierBaseHP = prevTierLastHP * gateMultiplier; // 게이트 배율을 적용해서 다음 티어 시작 체력을 구함
        }

        return tierBaseHP + stepPerObject * (indexInTier - 1);
    }
}
