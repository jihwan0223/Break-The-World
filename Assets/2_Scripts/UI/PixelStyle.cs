using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// 선형 색공간이라 .linear로 넣어야 적은 색 그대로 보임
public static class PixelStyle
{
    public readonly struct Tone
    {
        public readonly Color face;
        public readonly Color light;
        public readonly Color dark;
        public readonly Color outline;
        public readonly Color text;

        public Tone(Color face, Color light, Color dark, Color outline, Color text)
        {
            this.face = face;
            this.light = light;
            this.dark = dark;
            this.outline = outline;
            this.text = text;
        }
    }

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;

    private static readonly Color Outline = Hex("#8B6B4A");
    public static readonly Color Text = Hex("#5A3E2B");
    public static readonly Color MutedText = Hex("#7E5E40");
    public static readonly Color Good = Hex("#3E7A60");
    public static readonly Color Bad = Hex("#B8524A");
    public static readonly Color Hidden = Hex("#B8A48C");
    public static readonly Color Highlight = Hex("#E9A23B");

    public static readonly Tone Mint = new Tone(Hex("#7FC8A9"), Hex("#B5E6CF"), Hex("#4F9A7A"), Outline, Hex("#21473A"));
    public static readonly Tone Pink = new Tone(Hex("#F2A7A0"), Hex("#F9D0CB"), Hex("#C9776F"), Outline, Hex("#5A2E2A"));
    public static readonly Tone Sky = new Tone(Hex("#9CC7EA"), Hex("#CBE3F6"), Hex("#6A98C2"), Outline, Hex("#23415E"));
    public static readonly Tone Butter = new Tone(Hex("#F4D58D"), Hex("#FAE9C0"), Hex("#C9A452"), Outline, Hex("#5A4316"));

    public static readonly Tone Window = new Tone(Hex("#FFF6E6"), Hex("#FFFDF7"), Hex("#EAD9BE"), Outline, Text);
    public static readonly Tone Card = new Tone(Hex("#FDEBD0"), Hex("#FFF6E6"), Hex("#E8CFA8"), Hex("#C9A67E"), Text);
    public static readonly Tone Ribbon = new Tone(Hex("#F2A7A0"), Hex("#F9D0CB"), Hex("#C9776F"), Outline, Hex("#5A2E2A"));
    public static readonly Tone Box = new Tone(Hex("#FFF6E6"), Hex("#FFFDF7"), Hex("#EAD9BE"), Outline, Text);
    public static readonly Tone Slot = new Tone(
        new Color(0.42f, 0.52f, 0.64f), new Color(0.54f, 0.64f, 0.76f), new Color(0.32f, 0.40f, 0.51f), new Color(0.22f, 0.28f, 0.37f), Color.white);

    private const int Size = 12; // 도트
    private const int Slice = 4; // 도트
    private const float Dot = 4f; // px
    private const int Depth = 2; // 도트

    private enum Kind { Frame, Button, Hover, Pressed }

    private static readonly Dictionary<(Kind, Color, Color, Color, Color), Texture2D> _cache = new Dictionary<(Kind, Color, Color, Color, Color), Texture2D>();

    public static void Frame(VisualElement element, Tone tone, float dot = Dot)
    {
        Paint(element, Kind.Frame, tone, dot);
    }

    public const float OutlinedLetterSpacing = 6f; // px

    public static void OutlinedText(VisualElement element, Color outline, float width = 0.35f)
    {
        if (GameFonts.UiOutlined != null)
            element.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiOutlined);
        element.style.color = Color.white;
        element.style.unityFontStyleAndWeight = FontStyle.Bold; // 굵게 해야 테두리가 그려짐
        element.style.unityTextOutlineColor = outline.linear;
        element.style.unityTextOutlineWidth = width;
        element.style.letterSpacing = OutlinedLetterSpacing;
    }

    public static Label CardHeader(string title)
    {
        var header = new Label($"— {title} —");
        header.pickingMode = PickingMode.Ignore;
        header.style.fontSize = 36;
        header.style.color = MutedText.linear;
        header.style.unityTextAlign = TextAnchor.MiddleCenter;
        header.style.marginBottom = 12;
        return header;
    }

    public static void Button(Button button, Tone tone, float dot = Dot)
    {
        OutlinedText(button, tone.text);
        Paint(button, Kind.Button, tone, dot);
        button.RegisterCallback<PointerEnterEvent>(_ => Paint(button, Kind.Hover, tone, dot));
        button.RegisterCallback<PointerLeaveEvent>(_ => Paint(button, Kind.Button, tone, dot));
        button.RegisterCallback<PointerDownEvent>(_ => Paint(button, Kind.Pressed, tone, dot), TrickleDown.TrickleDown);
        button.RegisterCallback<PointerUpEvent>(_ => Paint(button, Kind.Hover, tone, dot), TrickleDown.TrickleDown);
    }

    private static readonly Dictionary<(Kind, Color, Color, Color, Color), Sprite> _sprites = new Dictionary<(Kind, Color, Color, Color, Color), Sprite>();

    public static void UguiButton(UnityEngine.UI.Button button, Tone tone, float dot = Dot)
    {
        var image = button.targetGraphic as UnityEngine.UI.Image;
        if (image == null) image = button.GetComponent<UnityEngine.UI.Image>();
        if (image != null)
        {
            image.sprite = SpriteOf(Kind.Button, tone);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = Color.white;
            image.pixelsPerUnitMultiplier = 1f / dot; // 도트 한 칸 = dot px
        }
        button.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
        button.spriteState = new UnityEngine.UI.SpriteState
        {
            highlightedSprite = SpriteOf(Kind.Hover, tone),
            selectedSprite = SpriteOf(Kind.Button, tone),
            pressedSprite = SpriteOf(Kind.Pressed, tone),
        };

        var text = button.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (text == null) return;
        text.color = Color.white;
        text.fontStyle = TMPro.FontStyles.Bold;
        text.outlineColor = tone.text;
        text.outlineWidth = 0.3f;
        text.characterSpacing = 3f; // 1/100 em
        text.margin = new Vector4(0f, 0f, 0f, Depth * dot);
    }

    private static Sprite SpriteOf(Kind kind, Tone tone)
    {
        var key = (kind, tone.face, tone.light, tone.dark, tone.outline);
        if (_sprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        Texture2D texture = Texture(kind, tone);
        var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(Slice, Slice, Slice, Slice));
        _sprites[key] = sprite;
        return sprite;
    }

    private static void Paint(VisualElement element, Kind kind, Tone tone, float dot)
    {
        element.style.backgroundColor = Color.clear;
        element.style.backgroundImage = new StyleBackground(Texture(kind, tone));
        element.style.unitySliceTop = Slice;
        element.style.unitySliceBottom = Slice;
        element.style.unitySliceLeft = Slice;
        element.style.unitySliceRight = Slice;
        element.style.unitySliceScale = dot;
        element.style.borderTopWidth = 0;
        element.style.borderBottomWidth = 0;
        element.style.borderLeftWidth = 0;
        element.style.borderRightWidth = 0;
        element.style.borderTopLeftRadius = 0;
        element.style.borderTopRightRadius = 0;
        element.style.borderBottomLeftRadius = 0;
        element.style.borderBottomRightRadius = 0;
        if (kind == Kind.Frame) return;

        bool pressed = kind == Kind.Pressed;
        element.style.translate = new Translate(0f, pressed ? Depth * dot : 0f);
        element.style.paddingBottom = pressed ? 0f : Depth * dot;
        element.style.paddingTop = 0f;
    }

    private static Texture2D Texture(Kind kind, Tone tone)
    {
        var key = (kind, tone.face, tone.light, tone.dark, tone.outline);
        if (_cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        Color face = kind == Kind.Hover ? Color.Lerp(tone.face, Color.white, 0.18f) : tone.face;
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp }; // sRGB 텍스처라 .linear 값을 넣음
        for (int y = 0; y < Size; y++) // y = 0이 맨 위
            for (int x = 0; x < Size; x++)
            {
                bool edgeX = x == 0 || x == Size - 1, edgeY = y == 0 || y == Size - 1;
                Color c;
                if (edgeX && edgeY) c = Color.clear;
                else if (edgeX || edgeY) c = tone.outline;
                else if (kind == Kind.Pressed) c = y == 1 ? tone.dark : face;
                else if (y == 1) c = tone.light;
                else if (kind == Kind.Frame) c = y == Size - 2 ? tone.dark : face;
                else c = y >= Size - 1 - Depth ? tone.dark : face;
                texture.SetPixel(x, Size - 1 - y, c.linear); // 텍스처는 아래가 y = 0
            }
        texture.Apply();
        _cache[key] = texture;
        return texture;
    }
}
