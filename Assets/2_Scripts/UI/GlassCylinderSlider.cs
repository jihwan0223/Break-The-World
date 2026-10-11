using UnityEngine;
using UnityEngine.UIElements;

// 가로로 누운 유리관 모양 음량 조절기 (로비 설정창 전용). 왼쪽부터 남아있는 유리 길이가 곧 값(0~1)이고, 그 오른쪽은 깨져 나간 모습.
// 유리관을 누르거나 드래그하면 그 자리까지로 값이 바뀜 - 값이 줄면 유리가 깨져 나간 것이라 onChipped로 알려줌
public class GlassCylinderSlider : VisualElement
{
    private const float Length = 400f;      // 유리관 가로 길이(px) - 값 1일 때 유리 길이
    private const float Thickness = 26f;    // 유리관 굵기(px)
    public const float Step = 0.05f;        // 값이 바뀌는 단위 (드래그해도 이 단위로 끊김, 방향키 한 번도 이만큼)
    private const float ToothDepth = 12f;   // 깨진 끝선의 톱니 깊이(px)
    private const float WallWidth = 2f;     // 유리 테두리 굵기(px)

    private static readonly Color GlassColor = new Color(0.6f, 0.85f, 1f, 0.25f);     // 유리 안쪽 색
    private static readonly Color GlassEdgeColor = new Color(0.75f, 0.92f, 1f, 0.9f); // 유리 테두리/금 색
    private static readonly Color GhostColor = new Color(1f, 1f, 1f, 0.12f);          // 깨져 나간 부분의 원래 윤곽 (최대 길이가 어디인지 보이게)
    private static readonly float[] Teeth = { 0.2f, 1f, 0.35f, 0.8f, 0f, 0.65f }; // 깨진 끝선 톱니 깊이 무늬 (0~1, 위에서 아래 순)

    public static Color ChipColor => GlassEdgeColor; // 깨져 떨어지는 유리 조각 색

    private readonly System.Action<float> _onChanged;    // 값이 바뀔 때 호출 (새 값 0~1)
    private readonly System.Action<Vector2> _onChipped;  // 값이 줄어 유리가 깨져 나갈 때 호출 (깨진 끝선 가운데의 패널 좌표)
    private float _value; // 지금 값 (0~1)

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
            this.CapturePointer(evt.pointerId); // 유리관 밖으로 나가도 드래그가 이어지게
            SetValue(evt.localPosition.x / Length);
            evt.StopPropagation();
        });
        RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (this.HasPointerCapture(evt.pointerId))
                SetValue(evt.localPosition.x / Length); // 누른 자리에 해당하는 값
        });
        RegisterCallback<PointerUpEvent>(evt =>
        {
            if (this.HasPointerCapture(evt.pointerId))
                this.ReleasePointer(evt.pointerId);
        });
    }

    private static float Snap(float value) => Mathf.Round(Mathf.Clamp01(value) / Step) * Step;

    // 값을 바꿈 (Step 단위로 맞춰짐). 드래그와 방향키 조절이 둘 다 여기로 옴
    public void SetValue(float value)
    {
        float snapped = Snap(value); // Step 단위로 맞춘 새 값
        if (Mathf.Approximately(snapped, _value)) return;

        bool chipped = snapped < _value; // 줄어들었으면 유리가 깨져 나간 것
        _value = snapped;
        MarkDirtyRepaint();
        _onChanged?.Invoke(_value);
        if (chipped)
            _onChipped?.Invoke(this.LocalToWorld(new Vector2(Length * _value, Thickness * 0.5f)));
    }

    private void Draw(MeshGenerationContext context)
    {
        Painter2D painter = context.painter2D; // 선/면을 직접 그리는 도구
        float edgeX = Length * _value;         // 남아있는 유리의 오른쪽 끝선

        // 깨져 나간 부분까지 포함한 원래 윤곽
        painter.lineWidth = 2f;
        painter.strokeColor = GhostColor;
        painter.BeginPath();
        painter.MoveTo(new Vector2(0f, 0f));
        painter.LineTo(new Vector2(Length, 0f));
        painter.LineTo(new Vector2(Length, Thickness));
        painter.LineTo(new Vector2(0f, Thickness));
        painter.Stroke();

        // 남아있는 유리: 왼쪽 끝에서 끝선까지, 끝선은 다 차있을 때만 반듯하고 아니면 톱니
        bool whole = _value >= 1f; // 하나도 안 깨진 상태인지
        int shift = Mathf.RoundToInt(_value / Step); // 값마다 톱니 무늬를 밀어서 자리마다 깨진 모양이 조금씩 다르게
        painter.lineWidth = WallWidth;
        painter.strokeColor = GlassEdgeColor;
        painter.fillColor = GlassColor;
        painter.BeginPath();
        painter.MoveTo(new Vector2(0f, 0f));
        for (int i = 0; i < Teeth.Length; i++)
        {
            float y = Thickness * i / (Teeth.Length - 1); // 톱니 꼭짓점 세로 위치
            float tooth = whole ? 0f : Teeth[(i + shift) % Teeth.Length] * ToothDepth; // 이 꼭짓점이 끝선에서 왼쪽으로 들어간 깊이
            painter.LineTo(new Vector2(Mathf.Max(edgeX - tooth, 0f), y));
        }
        painter.LineTo(new Vector2(0f, Thickness));
        painter.ClosePath();
        painter.Fill();
        painter.Stroke();

        float bodyEnd = Mathf.Max(edgeX - ToothDepth, 0f); // 톱니 왼쪽, 유리가 온전히 차있는 부분의 끝
        if (bodyEnd < 12f) return; // 남은 유리가 너무 짧으면 광택/금은 생략

        // 유리 광택 한 줄
        painter.lineWidth = 2f;
        painter.strokeColor = new Color(1f, 1f, 1f, 0.35f);
        painter.BeginPath();
        painter.MoveTo(new Vector2(8f, Thickness * 0.3f));
        painter.LineTo(new Vector2(bodyEnd - 6f, Thickness * 0.3f));
        painter.Stroke();

        if (whole) return;

        // 깨진 자리에서 왼쪽으로 뻗은 금
        float crackEnd = Mathf.Max(bodyEnd - 34f, 4f); // 금이 끝나는 자리
        painter.strokeColor = GlassEdgeColor;
        painter.BeginPath();
        painter.MoveTo(new Vector2(bodyEnd, Thickness * 0.62f));
        painter.LineTo(new Vector2(Mathf.Lerp(bodyEnd, crackEnd, 0.5f), Thickness * 0.45f));
        painter.LineTo(new Vector2(crackEnd, Thickness * 0.7f));
        painter.Stroke();
    }
}
