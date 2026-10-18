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

    private const string TmpOutlineName = "MonaS12 SDF Outline"; // 검은 테두리를 켠 TMP 재질 이름 (Resources 안)
    private static Material _tmpOutline; // 처음 쓸 때 한 번 불러와 캐시

    // 검은 테두리가 켜진 MonaS12 재질 (데미지/줍기 숫자용). TMP 셰이더는 OUTLINE_ON 키워드가 켜진 재질이어야 테두리를 그림.
    // 글자 아틀라스는 동적이라 실행 중 바뀌므로 지금 폰트 재질을 복사해서 쓰고, Resources의 재질 애셋에서는 테두리 값만 가져옴
    // (그 애셋에 키워드가 켜져 있어야 빌드에 테두리 셰이더가 포함됨)
    public static Material TmpOutline
    {
        get
        {
            if (_tmpOutline == null && Tmp != null)
            {
                Material preset = Resources.Load<Material>(TmpOutlineName); // 테두리 두께/색 기준
                _tmpOutline = new Material(Tmp.material) { name = TmpOutlineName };
                _tmpOutline.EnableKeyword("OUTLINE_ON");
                _tmpOutline.SetFloat(ShaderUtilities.ID_OutlineWidth, preset != null ? preset.GetFloat(ShaderUtilities.ID_OutlineWidth) : 0.3f);
                _tmpOutline.SetColor(ShaderUtilities.ID_OutlineColor, preset != null ? preset.GetColor(ShaderUtilities.ID_OutlineColor) : Color.black);
                // 테두리는 글자 경계 안팎으로 반씩 그려져서 글자가 얇아지므로, 그만큼 글자를 부풀려 원래 굵기를 지킴
                _tmpOutline.SetFloat(ShaderUtilities.ID_FaceDilate, preset != null ? preset.GetFloat(ShaderUtilities.ID_FaceDilate) : 0.3f);
                ShaderUtilities.UpdateShaderRatios(_tmpOutline);
            }
            return _tmpOutline;
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
