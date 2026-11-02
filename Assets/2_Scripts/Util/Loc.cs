using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// 번역표: Resources/Localization.csv (첫 열 = 한국어 원문 = 키)
// 한국어 문장의 조사는 을(를) / 이(가) / 은(는) / 와(과) / (으)로 로 적으면 앞 글자 받침 보고 골라줌
public static class Loc
{
    public static readonly string[] Codes = { "ko", "en", "ja", "zh", "ru", "es", "pt", "de", "fr" }; // CSV 열 순서
    public static readonly string[] NativeNames = { "한국어", "English", "日本語", "简体中文", "Русский", "Español", "Português BR", "Deutsch", "Français" }; // 언어 선택 칸용 (칸 너비 230px에 맞춤)

    private const string LanguageKey = "Language"; // PlayerPrefs 키
    private const string TablePath = "Localization"; // Resources 안 경로

    private static Dictionary<string, string[]> _table; // 한국어 원문 -> 언어별 번역
    private static int _language = -1; // Codes 인덱스, -1 = 아직 안 읽음
    private static readonly List<(object owner, Action apply)> _bindings = new List<(object, Action)>(); // 언어 바뀌면 다시 실행할 글자 설정

    public static event Action Changed; // 언어 바뀌면 화면 글 다시 그리기 (매번 새로 계산하는 글자용)

    // 한 번 만들고 끝나는 글자: 지금 적용하고, 언어 바뀌면 다시 적용
    // owner가 파괴되면(UI 요소는 화면에서 떨어지면) 자동 해제
    public static void Bind(object owner, Action apply)
    {
        apply();
        _bindings.Add((owner, apply));
    }

    public static void Bind(UnityEngine.UIElements.TextElement element, string korean) => Bind(element, () => element.text = T(korean));

    private static bool Alive(object owner) => owner switch
    {
        UnityEngine.Object o => o != null,
        UnityEngine.UIElements.VisualElement e => e.panel != null,
        _ => true,
    };

    public static int Language
    {
        get
        {
            if (_language < 0) _language = PlayerPrefs.GetInt(LanguageKey, SystemDefault());
            return _language;
        }
        set
        {
            int clamped = Mathf.Clamp(value, 0, Codes.Length - 1); // 잘못된 값 방지
            if (clamped == Language) return;
            _language = clamped;
            PlayerPrefs.SetInt(LanguageKey, clamped);
            _bindings.RemoveAll(b => !Alive(b.owner));
            foreach (var (_, apply) in _bindings.ToArray()) apply();
            Changed?.Invoke();
        }
    }

    public static bool IsKorean => Language == 0;

    public static string T(string korean)
    {
        if (string.IsNullOrEmpty(korean)) return korean;
        return Lookup(korean) ?? Josa(korean); // 번역 없으면 한국어 그대로
    }

    // 문자열 인자도 같이 번역됨 (오브젝트 이름 등)
    public static string T(string korean, params object[] args)
    {
        var translated = new object[args.Length]; // 번역된 인자
        for (int i = 0; i < args.Length; i++)
            translated[i] = args[i] is string s ? T(s) : args[i];
        string found = Lookup(korean); // null = 한국어 틀 사용
        return found != null ? string.Format(found, translated) : Josa(string.Format(korean, translated));
    }

    private static string Lookup(string korean)
    {
        if (IsKorean || !Table.TryGetValue(korean, out string[] row)) return null;
        return Language < row.Length && !string.IsNullOrEmpty(row[Language]) ? row[Language] : null;
    }

    private static readonly Regex JosaPattern = new Regex(@"(.)(을\(를\)|이\(가\)|은\(는\)|와\(과\)|\(으\)로)"); // 앞 글자 + 조사 표기

    public static string Josa(string text)
    {
        if (text.IndexOf('(') < 0) return text;
        return JosaPattern.Replace(text, m =>
        {
            char last = m.Groups[1].Value[0]; // 조사 앞 글자
            int final = last >= '가' && last <= '힣' ? (last - '가') % 28 : 0; // 0 = 받침 없음, 8 = ㄹ
            bool has = final != 0; // 받침 있음
            string josa = m.Groups[2].Value switch
            {
                "을(를)" => has ? "을" : "를",
                "이(가)" => has ? "이" : "가",
                "은(는)" => has ? "은" : "는",
                "와(과)" => has ? "과" : "와",
                _ => has && final != 8 ? "으로" : "로",
            };
            return last + josa;
        });
    }

    private static Dictionary<string, string[]> Table => _table ??= Load();

    private static Dictionary<string, string[]> Load()
    {
        var table = new Dictionary<string, string[]>(); // 결과
        var asset = Resources.Load<TextAsset>(TablePath);
        if (asset == null) return table;

        List<List<string>> rows = ParseCsv(asset.text);
        for (int r = 1; r < rows.Count; r++) // 0번 줄 = 헤더
        {
            List<string> row = rows[r];
            if (row.Count == 0 || string.IsNullOrEmpty(row[0])) continue;
            table[row[0]] = row.ToArray();
        }
        return table;
    }

    // 따옴표 안 쉼표/줄바꿈, "" = " 지원 (엑셀/구글시트 내보내기 형식)
    public static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>(); // 전체 줄
        var row = new List<string>(); // 지금 줄
        var cell = new StringBuilder(); // 지금 칸
        bool quoted = false; // 따옴표 안인지
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c != '"') cell.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else quoted = false;
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n') { row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = new List<string>(); }
            else if (c != '\r') cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }

    private static int SystemDefault()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: return 0;
            case SystemLanguage.Japanese: return 2;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return 3;
            case SystemLanguage.Russian: return 4;
            case SystemLanguage.Spanish: return 5;
            case SystemLanguage.Portuguese: return 6;
            case SystemLanguage.German: return 7;
            case SystemLanguage.French: return 8;
            default: return 1; // 영어
        }
    }
}
