using UnityEngine;
using UnityEngine.UIElements;
using TMPro;

// 게임 전체 공용 폰트. 파일은 Assets/8_Font/Resources/ 에 있고 Resources.Load로 한 번만 불러와 캐시한다.
//  - UI 툴킷: element.style.unityFontDefinition = GameFonts.UiFontDefinition (하위로 상속됨)
//  - TMP: TMP 기본 폰트를 MonaS12로 지정해 뒀으므로 보통 신경 안 써도 되지만, 명시하려면 GameFonts.Tmp
public static class GameFonts
{
    private const string FontName = "MonaS12";     // Resources 안에서의 이름 (확장자 제외)
    private const string TmpFontName = "MonaS12 SDF"; // "폰트 세팅" 메뉴가 만드는 TMP 애셋 이름

    private static Font _ui;
    private static TMP_FontAsset _tmp;

    public static Font Ui => _ui != null ? _ui : (_ui = Resources.Load<Font>(FontName));

    public static TMP_FontAsset Tmp =>
        _tmp != null ? _tmp
        : (_tmp = Resources.Load<TMP_FontAsset>(TmpFontName) ?? TMP_Settings.defaultFontAsset);

    // UI 툴킷 루트에 한 번 걸어주면 그 문서 전체가 이 폰트를 씀
    public static void Apply(VisualElement root)
    {
        if (root != null && Ui != null)
            root.style.unityFontDefinition = FontDefinition.FromFont(Ui);
    }
}
