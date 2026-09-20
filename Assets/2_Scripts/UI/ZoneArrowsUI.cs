using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// 화면 좌우 아래 모서리에 붙는 지역 이동 화살표. 갈 수 있는 방향만 보이고, 두 번째 존이 해금되기 전에는 전부 숨김.
// 배경 없이 픽셀 도트로 그린 굵은 ">" / "<" 모양이라, 어떤 배경 위에서도 보이도록 흰색 안에 어두운 테두리를 둘렀다.
[RequireComponent(typeof(UIDocument))]
public class ZoneArrowsUI : MonoBehaviour
{
    [SerializeField] private float sideOffset = 24f; // 화면 좌우 끝에서 띄울 거리(px)
    [SerializeField] private float bottomOffset = 40f; // 화면 아래 끝에서 띄울 거리(px) - 좌상단 조각 표시(PieceUI)에 가리지 않도록 아래쪽에 둠
    [SerializeField] private float pixelScale = 8f; // 도트 한 칸을 화면에서 몇 px로 그릴지 (클수록 화살표가 커짐)
    [SerializeField] private float popDuration = 0.35f; // 화살표가 새로 나타날 때 튀어나오는 연출 시간(초)

    private static readonly Color FillColor = Color.white; // 화살표 안쪽 색
    private static readonly Color OutlineColor = new Color(0.08f, 0.06f, 0.05f, 1f); // 화살표 테두리 색
    private static readonly Color HoverTint = new Color(1f, 0.85f, 0.35f, 1f); // 마우스를 올렸을 때 입히는 색

    private VisualElement _prevArrow; // 이전(좁은 스케일) 존으로
    private VisualElement _nextArrow; // 다음(넓은 스케일) 존으로

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();

        if (uiDocument.panelSettings == null)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            uiDocument.panelSettings = settings;
        }

        Build(uiDocument.rootVisualElement);

        if (ZoneManager.Instance != null)
            ZoneManager.Instance.OnZoneChanged += Refresh;

        Refresh();
    }

    void OnDisable()
    {
        if (ZoneManager.Instance != null)
            ZoneManager.Instance.OnZoneChanged -= Refresh;
    }

    private void Build(VisualElement root)
    {
        root.Clear();
        GameFonts.Apply(root);
        root.pickingMode = PickingMode.Ignore; // 화살표 밖 영역은 뒤쪽 오브젝트 클릭이 통과해야 함

        _prevArrow = CreateArrow(true);
        _nextArrow = CreateArrow(false);

        root.Add(_prevArrow);
        root.Add(_nextArrow);
    }

    private VisualElement CreateArrow(bool isLeft)
    {
        Texture2D texture = BuildArrowTexture(!isLeft); // 왼쪽 화살표는 "<", 오른쪽은 ">"

        var arrow = new VisualElement(); // 배경 없이 도트 이미지만 보이는 요소
        arrow.style.position = Position.Absolute;
        arrow.style.bottom = bottomOffset;
        if (isLeft) arrow.style.left = sideOffset;
        else arrow.style.right = sideOffset;

        arrow.style.width = texture.width * pixelScale;
        arrow.style.height = texture.height * pixelScale;
        arrow.style.backgroundImage = new StyleBackground(texture);
        arrow.style.unityBackgroundImageTintColor = Color.white;

        arrow.RegisterCallback<ClickEvent>(_ =>
        {
            if (ZoneManager.Instance == null) return;
            if (isLeft) ZoneManager.Instance.GoPrev();
            else ZoneManager.Instance.GoNext();
        });

        arrow.RegisterCallback<PointerEnterEvent>(_ =>
        {
            UIPointerGuard.IsPointerOverUI = true;
            arrow.style.unityBackgroundImageTintColor = HoverTint;
        });
        arrow.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            UIPointerGuard.IsPointerOverUI = false;
            arrow.style.unityBackgroundImageTintColor = Color.white;
        });

        return arrow;
    }

    // 굵은 꺾쇠(>) 모양을 도트로 찍은 텍스처 (테두리 1칸 포함). 계단식으로 가운데 끝이 뾰족하게 나오고 위아래는 대칭
    private static Texture2D BuildArrowTexture(bool pointsRight)
    {
        const int rows = 13; // 화살표 높이(도트 수)
        const int thickness = 4; // 획 굵기(도트 수)
        int cols = rows / 2 + thickness; // 화살표 너비(도트 수)
        int w = cols + 2; // 테두리를 포함한 텍스처 너비
        int h = rows + 2; // 테두리를 포함한 텍스처 높이

        var filled = new bool[w, h]; // 안쪽 색을 칠할 칸
        for (int r = 0; r < rows; r++)
        {
            int step = Mathf.Min(r, rows - 1 - r); // 위·아래 끝에서 가운데로 갈수록 커지는 계단
            for (int t = 0; t < thickness; t++)
            {
                int x = step + t; // 이 행에서 칠할 가로 위치 (왼쪽 기준)
                int col = pointsRight ? x : cols - 1 - x;
                filled[col + 1, r + 1] = true;
            }
        }

        var pixels = new Color32[w * h]; // 기본값은 완전 투명
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (filled[x, y]) pixels[y * w + x] = FillColor;
                else if (HasFilledNeighbour(filled, x, y, w, h)) pixels[y * w + x] = OutlineColor;
            }
        }

        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false); // 만들어서 돌려줄 텍스처
        texture.filterMode = FilterMode.Point; // 도트가 뭉개지지 않게
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    // 주변 8칸 중 칠해진 칸이 있는지 (있으면 이 칸은 테두리)
    private static bool HasFilledNeighbour(bool[,] filled, int x, int y, int w, int h)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx; // 이웃 칸의 가로 위치
                int ny = y + dy; // 이웃 칸의 세로 위치
                if (nx >= 0 && nx < w && ny >= 0 && ny < h && filled[nx, ny])
                    return true;
            }
        }

        return false;
    }

    // 갈 수 있는 방향만 보여줌. 두 번째 존이 아직 안 열렸으면 양쪽 다 숨김
    private void Refresh()
    {
        if (_prevArrow == null || _nextArrow == null) return;

        bool anyZoneUnlocked = ZoneManager.Instance != null && ZoneManager.Instance.UnlockedMaxZone > 0;
        bool canPrev = anyZoneUnlocked && ZoneManager.Instance.CanGoPrev;
        bool canNext = anyZoneUnlocked && ZoneManager.Instance.CanGoNext;

        SetVisible(_prevArrow, canPrev);
        SetVisible(_nextArrow, canNext);
    }

    // 보이거나 숨김. 숨겨져 있다가 새로 나타나는 순간에는 튀어나오는 연출을 재생
    private void SetVisible(VisualElement element, bool visible)
    {
        bool wasHidden = element.style.display.value == DisplayStyle.None; // 방금까지 숨겨져 있었는지
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (visible && wasHidden)
            StartCoroutine(Pop(element));
    }

    // 0에서 살짝 넘치게 커졌다가 원래 크기로 자리잡음 (ease-out-back). timeScale에 안 멈추게 실제 시간 사용
    private IEnumerator Pop(VisualElement element)
    {
        const float overshoot = 1.70158f; // ease-out-back의 튀는 정도

        for (float t = 0f; t < popDuration; t += Time.unscaledDeltaTime)
        {
            float x = t / popDuration - 1f; // -1 -> 0
            float s = 1f + (overshoot + 1f) * x * x * x + overshoot * x * x; // 0 -> 1 (중간에 1을 살짝 넘김)
            element.style.scale = new Scale(new Vector3(s, s, 1f));
            yield return null;
        }

        element.style.scale = new Scale(Vector3.one);
    }
}
