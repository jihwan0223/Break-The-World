// 큰 숫자 표시용 유틸리티. 1조 미만은 콤마 구분 숫자 그대로, 1조 이상부터는 "1.23E+12" 같은 지수 표기로 표시함.
// 조각 보유량 표시, 업그레이드/해금 가격 표시 등 화면에 큰 숫자가 나오는 모든 곳에서 공용으로 씀
public static class NumberFormatUtil
{
    private const long ExponentThreshold = 1_000_000_000_000L; // 1조(1E+12) - 이 밑은 콤마 숫자, 이 위부터 지수 표기

    // 예: 999 -> "999", 111111111 -> "111,111,111", 1_500_000_000_000 -> "1.50E+12"
    public static string Format(long value)
    {
        if (value < ExponentThreshold)
            return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); // 콤마 구분 그대로

        int exponent = (int)System.Math.Floor(System.Math.Log10(value)); // 10의 몇 제곱인지
        double mantissa = System.Math.Round(value / System.Math.Pow(10, exponent), 2); // 앞자리 수 (소수점 둘째 자리까지)
        if (mantissa >= 10) // 9.999...가 반올림돼 10.00이 되면 자릿수를 하나 올림
        {
            mantissa /= 10;
            exponent++;
        }

        // 굴러 올라가는 숫자의 폭이 안 흔들리게 소수점은 항상 두 자리
        return mantissa.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "E+" + exponent;
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
