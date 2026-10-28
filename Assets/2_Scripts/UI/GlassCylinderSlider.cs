using UnityEngine;
using UnityEngine.UIElements;

public class GlassCylinderSlider : VisualElement
{
    private const float Length = 400f; // px
    private const float Thickness = 26f; // px
    public const float Step = 0.05f;
    private const float ToothDepth = 12f; // px
    private const float WallWidth = 2f; // px

    private static readonly Color GlassColor = new Color(0.6f, 0.85f, 1f, 0.25f);
    private static readonly Color GlassEdgeColor = new Color(0.75f, 0.92f, 1f, 0.9f);
    private static readonly Color GhostColor = new Color(1f, 1f, 1f, 0.12f);
    private static readonly float[] Teeth = { 0.2f, 1f, 0.35f, 0.8f, 0f, 0.65f }; // 0~1, 위에서 아래 순

    public static Color ChipColor => GlassEdgeColor;

    private readonly System.Action<float> _onChanged;
    private readonly System.Action<Vector2> _onChipped;
    private float _value;

    public float Value => _value;

    public GlassCylinderSlider(float value, System.Action<float> onChanged, System.Action<Vector2> onChipped)
    {
        _value = Snap(value);
        _onChanged = onChanged;
        _onChipped = onChipped;

        style.width = Length;
        style.height = Thickness;
        generateVisualContent += Draw;

        RegisterCallback<PointerDownEvent>(evt =>
        {
            this.CapturePointer(evt.pointerId);
            SetValue(evt.localPosition.x / Length);
            evt.StopPropagation();
        });
        RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (this.HasPointerCapture(evt.pointerId))
                SetValue(evt.localPosition.x / Length);
        });
        RegisterCallback<PointerUpEvent>(evt =>
        {
            if (this.HasPointerCapture(evt.pointerId))
                this.ReleasePointer(evt.pointerId);
        });
    }

    private static float Snap(float value) => Mathf.Round(Mathf.Clamp01(value) / Step) * Step;

    public void SetValue(float value)
    {
        float snapped = Snap(value);
        if (Mathf.Approximately(snapped, _value)) return;

        bool chipped = snapped < _value;
        _value = snapped;
        MarkDirtyRepaint();
        _onChanged?.Invoke(_value);
        if (chipped)
            _onChipped?.Invoke(this.LocalToWorld(new Vector2(Length * _value, Thickness * 0.5f)));
    }

    private void Draw(MeshGenerationContext context)
    {
        Painter2D painter = context.painter2D;
        float edgeX = Length * _value;

        painter.lineWidth = 2f;
        painter.strokeColor = GhostColor;
        painter.BeginPath();
        painter.MoveTo(new Vector2(0f, 0f));
        painter.LineTo(new Vector2(Length, 0f));
        painter.LineTo(new Vector2(Length, Thickness));
        painter.LineTo(new Vector2(0f, Thickness));
        painter.Stroke();

        bool whole = _value >= 1f;
        int shift = Mathf.RoundToInt(_value / Step);
        painter.lineWidth = WallWidth;
        painter.strokeColor = GlassEdgeColor;
        painter.fillColor = GlassColor;
        painter.BeginPath();
        painter.MoveTo(new Vector2(0f, 0f));
        for (int i = 0; i < Teeth.Length; i++)
        {
            float y = Thickness * i / (Teeth.Length - 1);
            float tooth = whole ? 0f : Teeth[(i + shift) % Teeth.Length] * ToothDepth;
            painter.LineTo(new Vector2(Mathf.Max(edgeX - tooth, 0f), y));
        }
        painter.LineTo(new Vector2(0f, Thickness));
        painter.ClosePath();
        painter.Fill();
        painter.Stroke();

        float bodyEnd = Mathf.Max(edgeX - ToothDepth, 0f);
        if (bodyEnd < 12f) return;

        painter.lineWidth = 2f;
        painter.strokeColor = new Color(1f, 1f, 1f, 0.35f);
        painter.BeginPath();
        painter.MoveTo(new Vector2(8f, Thickness * 0.3f));
        painter.LineTo(new Vector2(bodyEnd - 6f, Thickness * 0.3f));
        painter.Stroke();

        if (whole) return;

        float crackEnd = Mathf.Max(bodyEnd - 34f, 4f);
        painter.strokeColor = GlassEdgeColor;
        painter.BeginPath();
        painter.MoveTo(new Vector2(bodyEnd, Thickness * 0.62f));
        painter.LineTo(new Vector2(Mathf.Lerp(bodyEnd, crackEnd, 0.5f), Thickness * 0.45f));
        painter.LineTo(new Vector2(crackEnd, Thickness * 0.7f));
        painter.Stroke();
    }
}
