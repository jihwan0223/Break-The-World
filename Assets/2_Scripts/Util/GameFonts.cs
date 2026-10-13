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

    public const int UiColoredPixelSize = 36; // UiColored 글자를 찍어내는 크기(px) - 12px 픽셀 폰트의 3배. 이 크기(또는 그 배수)로 써야 도트가 또렷함

    private static UnityEngine.TextCore.Text.FontAsset _uiColored; // UI 툴킷용 색이 먹는 폰트 (처음 쓸 때 한 번 만들어 캐시)

    // UI 툴킷에서 글자색이 먹는 폰트. Ui(일반 Font)로 걸면 이 픽셀 폰트는 style.color를 줘도 항상 흰색으로 나와서,
    // 색을 입혀야 하는 글자는 이걸 써야 함. 번짐 없이 도트 그대로 보이게 흑백 비트맵(RASTER)으로 찍고 텍스처도 Point로 둠
    public static UnityEngine.TextCore.Text.FontAsset UiColored
    {
        get
        {
            if (_uiColored == null && Ui != null)
            {
                _uiColored = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(Ui, UiColoredPixelSize, 2,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.RASTER, 1024, 1024);
                if (_uiColored != null && _uiColored.atlasTexture != null)
                    _uiColored.atlasTexture.filterMode = FilterMode.Point;
            }
            return _uiColored;
        }
    }

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
