public static class KoreanJosa
{
    // 0 = 받침 없음, 8 = ㄹ
    private static int FinalConsonant(string word)
    {
        if (string.IsNullOrEmpty(word)) return 0;
        char last = word.Trim()[word.Trim().Length - 1];
        return last >= '가' && last <= '힣' ? (last - '가') % 28 : 0;
    }

    public static string EulReul(string word) => word + (FinalConsonant(word) != 0 ? "을" : "를");
    public static string IGa(string word) => word + (FinalConsonant(word) != 0 ? "이" : "가");

    public static string EuroRo(string word)
    {
        int final = FinalConsonant(word);
        return word + (final == 0 || final == 8 ? "로" : "으로");
    }
}
