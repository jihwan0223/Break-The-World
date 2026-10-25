using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// 픽셀 힐링 느낌의 UI 툴킷 꾸밈 (크림 + 민트 파스텔). 그림 파일 없이 코드로 작은 도트 틀 그림(12x12)을 만들어 9칸 늘리기로 씀:
// 모서리 한 칸을 깎은 갈색 외곽선 + 위쪽 밝은 줄 + (버튼은) 아래 두께. 버튼은 마우스를 올리면 밝아지고 누르면 두께만큼 내려감.
// 이 화면은 색을 실제보다 밝게 그리는 설정이라(선형 색공간) 여기 적은 색 그대로 보이게 .linear로 바꿔 넣음
public static class PixelStyle
{
    // 틀 하나의 색 묶음
    public readonly struct Tone
    {
        public readonly Color face; // 면
        public readonly Color light; // 위쪽 밝은 줄
        public readonly Color dark; // 아래 두께/그늘
        public readonly Color outline; // 외곽선
        public readonly Color text; // 이 틀 위 글씨

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

    private static readonly Color Outline = Hex("#8B6B4A"); // 공통 외곽선 (부드러운 갈색)
    public static readonly Color Text = Hex("#5A3E2B"); // 크림 위 기본 글씨 (진한 갈색)
    public static readonly Color MutedText = Hex("#9A7A5A"); // 항목 이름 같은 옅은 글씨
    public static readonly Color Good = Hex("#3E7A60"); // 장착 중/해금 완료 (진한 민트)
    public static readonly Color Bad = Hex("#B8524A"); // 잠김 (벽돌색)
    public static readonly Color Hidden = Hex("#B8A48C"); // ???로 가린 값
    public static readonly Color Highlight = Hex("#E9A23B"); // 강조 테두리 (무기 슬롯 - 장착 중)

    // 버튼 색 (같은 크림 톤 안에서 버튼마다 다른 파스텔)
    public static readonly Tone Mint = new Tone(Hex("#7FC8A9"), Hex("#B5E6CF"), Hex("#4F9A7A"), Outline, Hex("#21473A")); // 업그레이드 / 기본
    public static readonly Tone Pink = new Tone(Hex("#F2A7A0"), Hex("#F9D0CB"), Hex("#C9776F"), Outline, Hex("#5A2E2A")); // 무기 / 닫기
    public static readonly Tone Sky = new Tone(Hex("#9CC7EA"), Hex("#CBE3F6"), Hex("#6A98C2"), Outline, Hex("#23415E")); // 도감
    public static readonly Tone Butter = new Tone(Hex("#F4D58D"), Hex("#FAE9C0"), Hex("#C9A452"), Outline, Hex("#5A4316")); // 디버그

    // 판 색
    public static readonly Tone Window = new Tone(Hex("#FFF6E6"), Hex("#FFFDF7"), Hex("#EAD9BE"), Outline, Text); // 팝업 창 바탕
    public static readonly Tone Card = new Tone(Hex("#FDEBD0"), Hex("#FFF6E6"), Hex("#E8CFA8"), Hex("#C9A67E"), Text); // 정보 카드/액자
    public static readonly Tone Ribbon = new Tone(Hex("#F2A7A0"), Hex("#F9D0CB"), Hex("#C9776F"), Outline, Hex("#5A2E2A")); // 제목 리본
    public static readonly Tone Box = new Tone(Hex("#FFF6E6"), Hex("#FFFDF7"), Hex("#EAD9BE"), Outline, Text); // 조각 표시 상자

    private const int Size = 12; // 틀 그림 한 변(도트)
    private const int Slice = 4; // 9칸 늘리기에서 테두리로 남길 도트 수
    private const float Dot = 4f; // 도트 한 칸의 화면 크기(px)
    private const int Depth = 2; // 버튼 아래 두께(도트) - 누르면 이만큼 내려감

    private enum Kind { Frame, Button, Hover, Pressed } // 틀 그림 종류

    private static readonly Dictionary<(Kind, Color, Color, Color, Color), Texture2D> _cache = new Dictionary<(Kind, Color, Color, Color, Color), Texture2D>(); // 만든 틀 그림 (색/종류별로 한 번만)

    // 판 (창/카드/리본/표시 상자): 깎인 모서리 외곽선 + 위 밝은 줄 + 아래 그늘 한 줄
    public static void Frame(VisualElement element, Tone tone, float dot = Dot)
    {
        Paint(element, Kind.Frame, tone, dot);
    }

    // 흰 글씨 + 진한 테두리 (Keep on Mining처럼 만화풍으로 또렷하게). 테두리가 먹는 SDF 글꼴로 바꿔 씀
    public static void OutlinedText(VisualElement element, Color outline, float width = 0.35f)
    {
        if (GameFonts.UiOutlined != null)
            element.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiOutlined);
        element.style.color = Color.white;
        element.style.unityFontStyleAndWeight = FontStyle.Bold; // 굵게 해야 테두리가 그려짐
        element.style.unityTextOutlineColor = outline.linear;
        element.style.unityTextOutlineWidth = width;
    }

    // 카드 위 작은 제목 "— 제목 —" (Keep on Mining의 카드 머리글처럼)
    public static Label CardHeader(string title)
    {
        var header = new Label($"— {title} —"); // 양옆 줄이 달린 작은 제목
        header.pickingMode = PickingMode.Ignore;
        header.style.fontSize = 20;
        header.style.color = MutedText.linear;
        header.style.unityTextAlign = TextAnchor.MiddleCenter;
        header.style.marginBottom = 12;
        return header;
    }

    // 입체 버튼: 마우스를 올리면 밝아지고, 누르면 두께만큼 내려감. 글씨는 흰색 + 버튼 색의 진한 그림자
    public static void Button(Button button, Tone tone, float dot = Dot)
    {
        OutlinedText(button, tone.text);
        Paint(button, Kind.Button, tone, dot);
        button.RegisterCallback<PointerEnterEvent>(_ => Paint(button, Kind.Hover, tone, dot));
        button.RegisterCallback<PointerLeaveEvent>(_ => Paint(button, Kind.Button, tone, dot));
        button.RegisterCallback<PointerDownEvent>(_ => Paint(button, Kind.Pressed, tone, dot), TrickleDown.TrickleDown);
        button.RegisterCallback<PointerUpEvent>(_ => Paint(button, Kind.Hover, tone, dot), TrickleDown.TrickleDown);
    }

    private static void Paint(VisualElement element, Kind kind, Tone tone, float dot)
    {
        element.style.backgroundColor = Color.clear;
        element.style.backgroundImage = new StyleBackground(Texture(kind, tone));
        element.style.unitySliceTop = Slice;
        element.style.unitySliceBottom = Slice;
        element.style.unitySliceLeft = Slice;
        element.style.unitySliceRight = Slice;
        element.style.unitySliceScale = dot; // 도트 한 칸 = dot px
        element.style.borderTopWidth = 0;
        element.style.borderBottomWidth = 0;
        element.style.borderLeftWidth = 0;
        element.style.borderRightWidth = 0;
        element.style.borderTopLeftRadius = 0;
        element.style.borderTopRightRadius = 0;
        element.style.borderBottomLeftRadius = 0;
        element.style.borderBottomRightRadius = 0;
        if (kind == Kind.Frame) return;

        bool pressed = kind == Kind.Pressed; // 눌린 상태
        element.style.translate = new Translate(0f, pressed ? Depth * dot : 0f); // 눌리면 두께만큼 내려감
        element.style.paddingBottom = pressed ? 0f : Depth * dot; // 글자를 두께 위 면 가운데에
        element.style.paddingTop = 0f;
    }

    // 12x12 도트 틀 그림 (색/종류별로 캐시)
    private static Texture2D Texture(Kind kind, Tone tone)
    {
        var key = (kind, tone.face, tone.light, tone.dark, tone.outline); // 캐시 키
        if (_cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        Color face = kind == Kind.Hover ? Color.Lerp(tone.face, Color.white, 0.18f) : tone.face; // 마우스를 올리면 살짝 밝게
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp }; // sRGB 그림 - 그림 색은 글자/면 색보다 한 번 더 밝아져서 .linear 값을 넣어야 적은 색 그대로 보임
        for (int y = 0; y < Size; y++) // y = 0이 맨 위
            for (int x = 0; x < Size; x++)
            {
                bool edgeX = x == 0 || x == Size - 1, edgeY = y == 0 || y == Size - 1; // 맨 바깥 줄
                Color c; // 이 도트 색
                if (edgeX && edgeY) c = Color.clear; // 모서리 한 칸 깎기
                else if (edgeX || edgeY) c = tone.outline;
                else if (kind == Kind.Pressed) c = y == 1 ? tone.dark : face; // 눌림: 위에 안쪽 그늘, 두께 없음
                else if (y == 1) c = tone.light; // 위 밝은 줄
                else if (kind == Kind.Frame) c = y == Size - 2 ? tone.dark : face; // 판: 아래 그늘 한 줄
                else c = y >= Size - 1 - Depth ? tone.dark : face; // 버튼: 아래 두께
                texture.SetPixel(x, Size - 1 - y, c.linear); // 텍스처는 아래가 y=0
            }
        texture.Apply();
        _cache[key] = texture;
        return texture;
    }
}
