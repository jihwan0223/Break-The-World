using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ZoneArrowsUI : MonoBehaviour
{
    [SerializeField] private float sideOffset = 24f; // px
    [SerializeField] private float bottomOffset = 40f; // px
    [SerializeField] private float pixelScale = 8f; // 도트 한 칸의 px
    [SerializeField] private float popDuration = 0.35f; // 초

    private static readonly Color FillColor = Color.white;
    private static readonly Color OutlineColor = new Color(0.08f, 0.06f, 0.05f, 1f);
    private static readonly Color HoverTint = new Color(1f, 0.85f, 0.35f, 1f);

    private VisualElement _prevArrow;
    private VisualElement _nextArrow;
    private VisualElement _root;
    private bool _treeOpen;
    private bool _popupOpen;

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

        UpgradeTreeUI.OnTreeToggled += HandleTreeToggled;
        SidePanelUI.OnSelectorPanelToggled += HandlePopupToggled;

        Refresh();
    }

    void OnDisable()
    {
        if (ZoneManager.Instance != null)
            ZoneManager.Instance.OnZoneChanged -= Refresh;

        UpgradeTreeUI.OnTreeToggled -= HandleTreeToggled;
        SidePanelUI.OnSelectorPanelToggled -= HandlePopupToggled;
    }

    private void HandleTreeToggled(bool open)
    {
        _treeOpen = open;
        UpdateRootVisibility();
    }

    private void HandlePopupToggled(bool open)
    {
        _popupOpen = open;
        UpdateRootVisibility();
    }

    private void UpdateRootVisibility()
    {
        if (_root == null) return;

        _root.style.display = _treeOpen || _popupOpen ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void Build(VisualElement root)
    {
        _root = root;
        root.Clear();
        GameFonts.Apply(root);
        root.pickingMode = PickingMode.Ignore; // 화살표 밖 클릭은 뒤로 통과

        _prevArrow = CreateArrow(true);
        _nextArrow = CreateArrow(false);

        root.Add(_prevArrow);
        root.Add(_nextArrow);
    }

    private VisualElement CreateArrow(bool isLeft)
    {
        Texture2D texture = BuildArrowTexture(!isLeft);

        var arrow = new VisualElement();
        arrow.style.position = Position.Absolute;
        arrow.style.bottom = bottomOffset;
        if (isLeft) arrow.style.left = sideOffset;
        else arrow.style.right = sideOffset;

        arrow.style.width = texture.width * pixelScale;
        arrow.style.height = texture.height * pixelScale;
        arrow.style.backgroundImage = new StyleBackground(texture);
        arrow.style.unityBackgroundImageTintColor = Color.white;

        arrow.AddToClassList(DebrisPool.ClickSoundClass);
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

    private static Texture2D BuildArrowTexture(bool pointsRight)
    {
        const int rows = 13; // 도트
        const int thickness = 4; // 도트
        int cols = rows / 2 + thickness;
        int w = cols + 2;
        int h = rows + 2;

        var filled = new bool[w, h];
        for (int r = 0; r < rows; r++)
        {
            int step = Mathf.Min(r, rows - 1 - r);
            for (int t = 0; t < thickness; t++)
            {
                int x = step + t;
                int col = pointsRight ? x : cols - 1 - x;
                filled[col + 1, r + 1] = true;
            }
        }

        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (filled[x, y]) pixels[y * w + x] = FillColor;
                else if (HasFilledNeighbour(filled, x, y, w, h)) pixels[y * w + x] = OutlineColor;
            }
        }

        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private static bool HasFilledNeighbour(bool[,] filled, int x, int y, int w, int h)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < w && ny >= 0 && ny < h && filled[nx, ny])
                    return true;
            }
        }

        return false;
    }

    private void Refresh()
    {
        if (_prevArrow == null || _nextArrow == null) return;

        bool anyZoneUnlocked = ZoneManager.Instance != null && ZoneManager.Instance.UnlockedMaxZone > 0;
        bool canPrev = anyZoneUnlocked && ZoneManager.Instance.CanGoPrev;
        bool canNext = anyZoneUnlocked && ZoneManager.Instance.CanGoNext;

        SetVisible(_prevArrow, canPrev);
        SetVisible(_nextArrow, canNext);
    }

    private void SetVisible(VisualElement element, bool visible)
    {
        bool wasHidden = element.style.display.value == DisplayStyle.None;
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (visible && wasHidden)
            StartCoroutine(Pop(element));
    }

    private IEnumerator Pop(VisualElement element)
    {
        const float overshoot = 1.70158f; // ease-out-back

        for (float t = 0f; t < popDuration; t += Time.unscaledDeltaTime)
        {
            float x = t / popDuration - 1f;
            float s = 1f + (overshoot + 1f) * x * x * x + overshoot * x * x;
            element.style.scale = new Scale(new Vector3(s, s, 1f));
            yield return null;
        }

        element.style.scale = new Scale(Vector3.one);
    }
}
