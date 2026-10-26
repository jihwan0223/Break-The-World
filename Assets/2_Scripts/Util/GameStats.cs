// 통계 창에 보여줄 누적 기록 (세이브에 같이 저장됨). 얻은 조각/결정 누적은 CurrencyManager가 따로 들고 있음
public static class GameStats
{
    public static long ObjectsBroken; // 부순 오브젝트 수 (자동클릭/빗자루 등 포함 전부)
    public static long Clicks; // 직접 마우스로 때린 횟수
    public static long Crits; // 치명타가 터진 횟수
    public static long SuperCrits; // 슈퍼 크리티컬이 터진 횟수
    public static long BossClears; // 보스를 잡은 횟수
    public static double PlaySeconds; // 게임 화면에서 보낸 시간(초) - 창이 열려 멈춘 시간은 빼고
    public static long[] BrokenPerObject = new long[0]; // 오브젝트 종류(번호)별 부순 횟수

    // 오브젝트 하나를 부쉈을 때
    public static void CountBroken(int objectIndex)
    {
        ObjectsBroken++;
        if (objectIndex < 0) return;
        if (objectIndex >= BrokenPerObject.Length) System.Array.Resize(ref BrokenPerObject, objectIndex + 1);
        BrokenPerObject[objectIndex]++;
    }

    public static long BrokenOf(int objectIndex) =>
        objectIndex >= 0 && objectIndex < BrokenPerObject.Length ? BrokenPerObject[objectIndex] : 0;

    // 새 게임/전체 초기화
    public static void Reset()
    {
        ObjectsBroken = Clicks = Crits = SuperCrits = BossClears = 0;
        PlaySeconds = 0;
        BrokenPerObject = new long[0];
    }
}
