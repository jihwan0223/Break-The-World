// 오브젝트 체력을 "무기 티어 + 같은 티어 내 순번"만으로 자동 계산해주는 유틸리티.
// 기획: 같은 무기 안에서는 완만하게(+4), 무기가 바뀌는 시점엔 확 뛰게(x2) 해서
// "현재 도구로는 감당 안 되는 벽" 게이트 메커닉을 수치로 표현함.
public static class ObjectHealthCalculator
{
    // 무기 티어(1~10)별 오브젝트 개수. 총합 19개 (ObjectManager.objects 순서/개수와 정확히 일치해야 함)
    private static readonly int[] tierSizes = { 3, 2, 3, 2, 3, 2, 1, 1, 1, 1 };

    // 무기 티어(1~10)별로 체력과 클릭 데미지(무기 기본 데미지 + 데미지 강화 노드)에 똑같이 곱하는 배율.
    // 처음엔 1배라 숫자가 작고, 티어가 오를수록 점점 크게 불어남. 같은 티어의 무기와 오브젝트는 배율이 같아서
    // 부수는 데 드는 클릭 수(밸런스)는 그대로고 화면에 뜨는 숫자만 커짐
    private static readonly int[] tierScales = { 1, 2, 5, 10, 25, 50, 100, 250, 500, 1000 };

    // weaponTier(1부터)의 배율. 범위를 벗어나면 가장 가까운 티어 값
    public static int ScaleOfTier(int weaponTier) => tierScales[System.Math.Clamp(weaponTier, 1, tierScales.Length) - 1];

    private const int baseHP = 10; // 1티어 첫 오브젝트(접시)의 체력 (티어 배율 곱하기 전)
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

        return (tierBaseHP + stepPerObject * (indexInTier - 1)) * ScaleOfTier(weaponTier);
    }
}
