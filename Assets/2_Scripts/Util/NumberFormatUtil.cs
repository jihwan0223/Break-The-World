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
}
