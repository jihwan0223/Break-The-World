// 큰 숫자를 K/M/B/T/Q 같은 영어 단위 축약형으로 표시하기 위한 유틸리티.
// 조각 보유량 표시, 업그레이드/해금 가격 표시 등 화면에 큰 숫자가 나오는 모든 곳에서 공용으로 씀
public static class NumberFormatUtil
{
    // 1,000 미만이면 그대로 표시. 그 이상이면 K(천)/M(백만)/B(십억)/T(조)/Q(천조) 단위로 줄여서 표시함
    // 예: 999 -> "999", 1500 -> "1.5K", 2400000 -> "2.4M"
    private static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Q" };

    public static string Format(long value)
    {
        if (value < 1000) return value.ToString();

        double shortValue = value; // 단위를 나눠가며 줄여나갈 값
        int suffixIndex = 0; // Suffixes 배열에서 지금 몇 번째 단위까지 왔는지

        while (shortValue >= 1000 && suffixIndex < Suffixes.Length - 1)
        {
            shortValue /= 1000;
            suffixIndex++;
        }

        // 정수로 딱 떨어지면 소수점 없이("10K"), 아니면 소수점 한 자리까지("1.5K")
        string number = shortValue % 1 == 0 ? shortValue.ToString("0") : shortValue.ToString("0.0");
        return number + Suffixes[suffixIndex];
    }

    public static string Format(int value) => Format((long)value);
}
