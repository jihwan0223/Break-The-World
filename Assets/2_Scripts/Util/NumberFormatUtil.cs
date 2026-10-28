public static class NumberFormatUtil
{
    private const long ExponentThreshold = 1_000_000_000_000_000_000L; // 이 이상은 지수 표기
    private static readonly string[] Units = { "", "K", "M", "B", "T", "Q" };
    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

    public static string Format(long value)
    {
        if (value < 0) return "-" + Format(-value);
        if (value >= ExponentThreshold) return Exponent(value);
        if (!GameSettings.NumberUnits || value < 1000) return value.ToString("N0", Invariant);

        int tier = 0; // Log10은 경계값에서 틀려서 정수로 셈
        long unit = 1;
        while (tier < Units.Length - 1 && value / unit >= 1000) { unit *= 1000; tier++; }
        long whole = value / unit;
        // 반올림 대신 버림 - 못 사는 가격이 살 수 있어 보이지 않게
        string number = whole < 10 ? $"{whole}.{value / (unit / 100) % 100:00}"
                      : whole < 100 ? $"{whole}.{value / (unit / 10) % 10}"
                      : whole.ToString(Invariant);
        return number + Units[tier];
    }

    private static double Floor(double v, double scale) => System.Math.Floor(v * scale) / scale;

    private static string Exponent(long value)
    {
        int exponent = (int)System.Math.Floor(System.Math.Log10(value));
        double mantissa = Floor(value / System.Math.Pow(10, exponent), 100);
        return mantissa.ToString("0.00", Invariant) + "E+" + exponent;
    }

    public static string Format(int value) => Format((long)value);

    // 1,000 미만은 5 단위, 그 이상은 앞 세 자리(셋째 자리 0/5)
    public static long RoundPrice(long value)
    {
        if (value <= 0) return value;
        long step = 5;
        int digits = (int)System.Math.Floor(System.Math.Log10(value)) + 1;
        for (int d = 4; d <= digits; d++) step *= 10;
        if (step <= 0 || step > long.MaxValue / 2) return value;
        long rounded = (value + step / 2) / step * step;
        return System.Math.Max(5L, rounded);
    }
}
