// 큰 숫자 표시용 유틸리티. T(조) 미만은 콤마 구분 숫자 그대로, T 이상부터는 T/Q 같은 영어 단위 축약형으로 표시함.
// 조각 보유량 표시, 업그레이드/해금 가격 표시 등 화면에 큰 숫자가 나오는 모든 곳에서 공용으로 씀
public static class NumberFormatUtil
{
    private const long TeraThreshold = 1_000_000_000_000L; // 1조(T) - 이 밑은 콤마 숫자, 이 위부터 단위 축약
    private static readonly string[] Suffixes = { "T", "Q" }; // T(조)/Q(천조) 단위

    // 예: 999 -> "999", 111111111 -> "111,111,111", 1_500_000_000_000 -> "1.5T"
    public static string Format(long value)
    {
        if (value < TeraThreshold)
            return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); // 콤마 구분 그대로

        double shortValue = value / (double)TeraThreshold; // T 단위로 나눈 값
        int suffixIndex = 0; // Suffixes 배열에서 지금 몇 번째 단위까지 왔는지

        while (shortValue >= 1000 && suffixIndex < Suffixes.Length - 1)
        {
            shortValue /= 1000;
            suffixIndex++;
        }

        // 정수로 딱 떨어지면 소수점 없이("10T"), 아니면 소수점 한 자리까지("1.5T")
        string number = shortValue % 1 == 0 ? shortValue.ToString("0") : shortValue.ToString("0.0");
        return number + Suffixes[suffixIndex];
    }

    public static string Format(int value) => Format((long)value);
}
