// 큰 숫자 표시용 유틸리티. 설정(GameSettings.NumberUnits)에 따라 영어 단위(1.23K, 45.6M ... Q까지) 또는 콤마 숫자(12,345)로 표시하고,
// 둘 다 Q 단위를 넘어가면(1E+18 이상) "1.23E+18" 같은 지수 표기로 표시함.
// 조각 보유량, 업그레이드/해금 가격, 데미지/줍기 숫자 등 화면에 큰 숫자가 나오는 모든 곳에서 공용으로 씀
public static class NumberFormatUtil
{
    private const long ExponentThreshold = 1_000_000_000_000_000_000L; // Q(1E+15) 단위를 넘어가는 수 - 이 위부터는 지수 표기 (double로 비교하면 반올림돼서 long으로)
    private static readonly string[] Units = { "", "K", "M", "B", "T", "Q" }; // 1000배마다 붙는 단위
    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture; // 기기 언어와 상관없이 점/콤마 고정

    // 예(영어 단위): 999 -> "999", 1234 -> "1.23K", 45_600_000 -> "45.6M" / (숫자): 111111111 -> "111,111,111"
    public static string Format(long value)
    {
        if (value < 0) return "-" + Format(-value);
        if (value >= ExponentThreshold) return Exponent(value);
        if (!GameSettings.NumberUnits || value < 1000) return value.ToString("N0", Invariant);

        int tier = 0; // 몇 번째 단위인지 (1=K ... 5=Q) - Log10은 999...같은 경계에서 반올림돼 틀려서 정수로 셈
        long unit = 1; // 그 단위의 크기
        while (tier < Units.Length - 1 && value / unit >= 1000) { unit *= 1000; tier++; }
        long whole = value / unit; // 단위 앞 정수 부분 (1 ~ 999)
        // 세 자리 유효숫자로 내림 (정수 나눗셈 - double은 999.999...를 1000으로 올려버림).
        // 반올림하면 실제보다 많아 보여서(가격이 살 수 있어 보이는 등) 버림
        string number = whole < 10 ? $"{whole}.{value / (unit / 100) % 100:00}"
                      : whole < 100 ? $"{whole}.{value / (unit / 10) % 10}"
                      : whole.ToString(Invariant);
        return number + Units[tier];
    }

    private static double Floor(double v, double scale) => System.Math.Floor(v * scale) / scale;

    // 1.23E+18 - 굴러 올라가는 숫자의 폭이 안 흔들리게 소수점은 항상 두 자리
    private static string Exponent(long value)
    {
        int exponent = (int)System.Math.Floor(System.Math.Log10(value)); // 10의 몇 제곱인지
        double mantissa = Floor(value / System.Math.Pow(10, exponent), 100); // 앞자리 수 (소수점 둘째 자리까지 버림)
        return mantissa.ToString("0.00", Invariant) + "E+" + exponent;
    }

    public static string Format(int value) => Format((long)value);

    // 가격을 딱 떨어지는 수로 반올림 - 1,000 미만은 5 단위, 그 이상은 앞 세 자리만 남기되 셋째 자리를 0이나 5로 (예: 6 -> 5, 62 -> 60, 1,743 -> 1,750, 77,555 -> 77,500). 최소 5
    public static long RoundPrice(long value)
    {
        if (value <= 0) return value;
        long step = 5; // 반올림 단위
        int digits = (int)System.Math.Floor(System.Math.Log10(value)) + 1; // 자릿수
        for (int d = 4; d <= digits; d++) step *= 10; // 네 자리부터는 앞 세 자리 아래를 버림 (셋째 자리를 0이나 5로)
        if (step <= 0 || step > long.MaxValue / 2) return value;
        long rounded = (value + step / 2) / step * step; // 가장 가까운 단위로
        return System.Math.Max(5L, rounded);
    }
}
