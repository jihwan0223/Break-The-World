public static class GameStats
{
    public static long ObjectsBroken;
    public static long TotalDamage; // 지금까지 넣은 데미지 합
    public static long BestHit; // 한 방 최고 데미지
    public static long Crits;
    public static long SuperCrits;
    public static long BossClears;
    public static long Rebirths; // 환생한 횟수 (환생 가격이 이만큼 오름)
    public static double PlaySeconds; // 초, 창이 열려 멈춘 시간 제외
    public static long[] BrokenPerObject = new long[0];

    public static void CountBroken(int objectIndex)
    {
        ObjectsBroken++;
        if (objectIndex < 0) return;
        if (objectIndex >= BrokenPerObject.Length) System.Array.Resize(ref BrokenPerObject, objectIndex + 1);
        BrokenPerObject[objectIndex]++;
    }

    public static long BrokenOf(int objectIndex) =>
        objectIndex >= 0 && objectIndex < BrokenPerObject.Length ? BrokenPerObject[objectIndex] : 0;

    public static void Reset()
    {
        ObjectsBroken = TotalDamage = BestHit = Crits = SuperCrits = BossClears = Rebirths = 0;
        PlaySeconds = 0;
        BrokenPerObject = new long[0];
    }
}
