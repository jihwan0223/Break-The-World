// 이름 뒤에 붙는 조사를 받침에 맞게 골라줌 ("사과를", "달걀을", "망치로", "곡괭이로"). 한글이 아니면 받침 없는 쪽으로
public static class KoreanJosa
{
    // 마지막 글자 받침 번호 (0 = 받침 없음, 8 = ㄹ). 한글 음절이 아니면 0
    private static int FinalConsonant(string word)
    {
        if (string.IsNullOrEmpty(word)) return 0;
        char last = word.Trim()[word.Trim().Length - 1]; // 마지막 글자
        return last >= '가' && last <= '힣' ? (last - '가') % 28 : 0;
    }

    public static string EulReul(string word) => word + (FinalConsonant(word) != 0 ? "을" : "를"); // 목적격
    public static string IGa(string word) => word + (FinalConsonant(word) != 0 ? "이" : "가"); // 주격

    // 도구격 - 받침이 없거나 ㄹ 받침이면 "로", 나머지는 "으로"
    public static string EuroRo(string word)
    {
        int final = FinalConsonant(word); // 받침 번호
        return word + (final == 0 || final == 8 ? "로" : "으로");
    }
}
