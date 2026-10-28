using UnityEngine;
using UnityEngine.UIElements;
using TMPro;

public static class GameFonts
{
    private const string FontName = "MonaS12";
    private const string TmpFontName = "MonaS12 SDF";

    private static Font _ui;
    private static TMP_FontAsset _tmp;

    public static Font Ui => _ui != null ? _ui : (_ui = Resources.Load<Font>(FontName));

    public const int UiColoredPixelSize = 36; // px - 12px 폰트의 배수로 써야 도트가 또렷함

    private static UnityEngine.TextCore.Text.FontAsset _uiColored;

    // 일반 Font로는 이 픽셀 폰트에 글자색이 안 먹음
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

    private static UnityEngine.TextCore.Text.FontAsset _uiOutlined;

    // 테두리가 필요한 글자용 (UiColored는 비트맵이라 테두리 불가)
    public static UnityEngine.TextCore.Text.FontAsset UiOutlined
    {
        get
        {
            if (_uiOutlined == null && Ui != null)
                _uiOutlined = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(Ui, UiColoredPixelSize, 8,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            return _uiOutlined;
        }
    }

    private const string TmpOutlineName = "MonaS12 SDF Outline";
    private static Material _tmpOutline;

    // Resources 재질에 OUTLINE_ON이 켜져 있어야 빌드에 셰이더가 포함됨
    public static Material TmpOutline
    {
        get
        {
            if (_tmpOutline == null && Tmp != null)
            {
                Material preset = Resources.Load<Material>(TmpOutlineName);
                _tmpOutline = new Material(Tmp.material) { name = TmpOutlineName };
                _tmpOutline.EnableKeyword("OUTLINE_ON");
                _tmpOutline.SetFloat(ShaderUtilities.ID_OutlineWidth, preset != null ? preset.GetFloat(ShaderUtilities.ID_OutlineWidth) : 0.3f);
                _tmpOutline.SetColor(ShaderUtilities.ID_OutlineColor, preset != null ? preset.GetColor(ShaderUtilities.ID_OutlineColor) : Color.black);
                // 테두리가 글자를 얇게 만들어서 그만큼 부풀림
                _tmpOutline.SetFloat(ShaderUtilities.ID_FaceDilate, preset != null ? preset.GetFloat(ShaderUtilities.ID_FaceDilate) : 0.3f);
                ShaderUtilities.UpdateShaderRatios(_tmpOutline);
            }
            return _tmpOutline;
        }
    }

    public static TMP_FontAsset Tmp =>
        _tmp != null ? _tmp
        : (_tmp = Resources.Load<TMP_FontAsset>(TmpFontName) ?? TMP_Settings.defaultFontAsset);

    public static void Apply(VisualElement root)
    {
        if (root != null && Ui != null)
            root.style.unityFontDefinition = FontDefinition.FromFont(Ui);
    }
}
