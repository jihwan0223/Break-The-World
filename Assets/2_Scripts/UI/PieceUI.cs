using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class PieceUI : MonoBehaviour
{
    [SerializeField] private float leftOffset = 20f; // px
    [SerializeField] private float topOffset = 20f; // px
    [SerializeField] private int sortingOrder = 100; // 다른 화면보다 위에 그리려고 높게
    [SerializeField] private float fontSize = GameFonts.UiColoredPixelSize; // px - 폰트를 찍어낸 크기와 같아야 또렷함

    private Label _pieceLabel;
    private long _lastAmount;
    private double _shownPieces;
    private bool _shownInitialized;
    private const float CountUpSpeed = 8f; // 대략 0.4초
    private const float CountUpMinPerSecond = 30f;
    private const float CountUpSeconds = 0.35f; // 초 - 얼마를 얻든 이 안에 다 따라붙음
    private double _countUpFloor;
    private Label _crystalLabel;
    private VisualElement _crystalRow;
    private long _lastCrystals;

    private class PulseState
    {
        public int queued;
        public bool running;
    }
    private const int MaxQueuedPulses = 1; // 줍기를 멈추면 바로 멈추도록 작게
    private readonly Dictionary<Label, PulseState> _pulses = new Dictionary<Label, PulseState>();

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();

        if (uiDocument.panelSettings == null)
        {
            // PanelSettings가 비어 있으면 런타임에 만들어 넣음
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            uiDocument.panelSettings = settings;
        }

        uiDocument.panelSettings.sortingOrder = sortingOrder;

        VisualElement root = uiDocument.rootVisualElement;
        GameFonts.Apply(root);
        if (GameFonts.UiColored != null)
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiColored);
        BuildContainer(root);
    }

    void Start()
    {
        // CurrencyManager.Awake 이후에 구독하려고 Start에서
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnPiecesChanged += UpdatePieceLabel;
            CurrencyManager.Instance.OnCrystalsChanged += UpdateCrystalLabel;
            GameSettings.OnNumberFormatChanged += RedrawNumbers;

            UpdatePieceLabel(CurrencyManager.Instance.GetPieces());
            UpdateCrystalLabel(CurrencyManager.Instance.GetCrystals());
        }
    }

    void OnDisable()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnPiecesChanged -= UpdatePieceLabel;
            CurrencyManager.Instance.OnCrystalsChanged -= UpdateCrystalLabel;
        }
        GameSettings.OnNumberFormatChanged -= RedrawNumbers;
    }

    private void BuildContainer(VisualElement root)
    {
        root.Clear();

        var background = new VisualElement();
        background.AddToClassList("unity-button");
        background.pickingMode = PickingMode.Ignore; // 뒤쪽 클릭이 통과하게
        background.style.position = Position.Absolute;
        background.style.top = topOffset;
        background.style.left = leftOffset;
        background.style.minWidth = 340;
        background.style.paddingLeft = 16;
        background.style.paddingRight = 16;
        background.style.paddingTop = 14;
        background.style.paddingBottom = 22;
        background.style.justifyContent = Justify.Center;
        background.style.alignItems = Align.Stretch;
        PixelStyle.Frame(background, PixelStyle.Box);

        _pieceLabel = AddRow(background, ShardTexture(WarmShard), PixelStyle.Text.linear, out _);
        _crystalLabel = AddRow(background, ShardTexture(SkyShard), new Color(0.23f, 0.53f, 0.66f).linear, out _crystalRow);

        root.Add(background);
    }

    private Label AddRow(VisualElement box, Texture2D iconTexture, Color color, out VisualElement row)
    {
        row = new VisualElement();
        row.pickingMode = PickingMode.Ignore;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.height = 46;
        box.Add(row);

        var icon = new VisualElement();
        icon.pickingMode = PickingMode.Ignore;
        icon.style.width = iconTexture.width * 3;
        icon.style.height = iconTexture.height * 3;
        icon.style.flexShrink = 0;
        icon.style.backgroundImage = new StyleBackground(iconTexture);
        row.Add(icon);

        var label = new Label();
        label.pickingMode = PickingMode.Ignore;
        label.style.flexGrow = 1;
        label.style.marginLeft = 14;
        label.style.fontSize = fontSize;
        label.style.color = color;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.paddingLeft = label.style.paddingRight = 0;
        label.style.marginTop = label.style.marginBottom = 0;
        label.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
        row.Add(label);
        return label;
    }

    private static readonly string[] ShardRows =
    {
        ".....oo.....",
        "....oaao....",
        "...oahaao...",
        "..oaaaaabo..",
        ".oaaaaabbbo.",
        "oaaaaabbbbbo",
        "obbbbbbbccco",
        ".obbbbcccco.",
        "..occcccco..",
        "...oooooo...",
    };
    private static readonly Color32[] WarmShard = { new Color32(60, 36, 24, 255), new Color32(255, 214, 120, 255), new Color32(235, 160, 70, 255), new Color32(180, 100, 40, 255), new Color32(255, 255, 230, 255) };
    private static readonly Color32[] SkyShard = { new Color32(20, 40, 70, 255), new Color32(170, 230, 255, 255), new Color32(90, 180, 240, 255), new Color32(40, 110, 200, 255), new Color32(255, 255, 255, 255) };

    private static Texture2D ShardTexture(Color32[] palette)
    {
        int w = ShardRows[0].Length, h = ShardRows.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int k = "oabch".IndexOf(ShardRows[y][x]);
                tex.SetPixel(x, h - 1 - y, k >= 0 ? (Color)palette[k] : Color.clear);
            }
        tex.Apply();
        return tex;
    }

    private void UpdateCrystalLabel(long amount)
    {
        _crystalLabel.text = NumberFormatUtil.Format(amount);
        bool shown = amount > 0 || (CurrencyManager.Instance != null && CurrencyManager.Instance.EverHadCrystal);
        _crystalRow.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        bool increased = amount > _lastCrystals;
        _lastCrystals = amount;

        if (increased)
            QueuePulse(_crystalLabel);
    }

    private void QueuePulse(Label label)
    {
        if (!_pulses.TryGetValue(label, out PulseState state))
            _pulses[label] = state = new PulseState();

        if (!state.running)
        {
            state.queued = 1;
            StartCoroutine(RunPulses(label, state));
        }
        else
        {
            state.queued = Mathf.Min(state.queued + 1, MaxQueuedPulses);
        }
    }

    private IEnumerator RunPulses(Label label, PulseState state)
    {
        state.running = true;
        while (state.queued > 0)
        {
            state.queued--;
            yield return PlayPieceGainPulse(label);
        }
        state.running = false;
    }

    private void UpdatePieceLabel(long amount)
    {
        bool increased = amount > _lastAmount;
        _lastAmount = amount;

        if (!increased || !_shownInitialized)
        {
            _shownPieces = amount;
            _shownInitialized = true;
            SetPieceText();
        }
        else
        {
            _countUpFloor = System.Math.Max(_countUpFloor, (amount - _shownPieces) / CountUpSeconds);
        }
    }

    void Update()
    {
        if (_shownPieces >= _lastAmount)
        {
            _countUpFloor = 0;
            return;
        }

        float dt = Time.unscaledDeltaTime; // timeScale 0이어도 올라가게
        double gap = _lastAmount - _shownPieces;
        double floor = System.Math.Max(CountUpMinPerSecond, _countUpFloor);
        double step = System.Math.Max(gap * (1 - Mathf.Exp(-CountUpSpeed * dt)), floor * dt);
        _shownPieces = System.Math.Min(_lastAmount, _shownPieces + step);
        SetPieceText();
        QueuePulse(_pieceLabel);
    }

    private void RedrawNumbers()
    {
        SetPieceText();
        if (CurrencyManager.Instance != null) _crystalLabel.text = NumberFormatUtil.Format(CurrencyManager.Instance.GetCrystals());
    }

    private void SetPieceText() => _pieceLabel.text = NumberFormatUtil.Format((long)_shownPieces);

    private IEnumerator PlayPieceGainPulse(Label label)
    {
        const float duration = 0.25f;
        const float peakScale = 1.15f;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            float scale = progress < 0.5f
                ? Mathf.Lerp(1f, peakScale, progress / 0.5f)
                : Mathf.Lerp(peakScale, 1f, (progress - 0.5f) / 0.5f);

            label.style.scale = new Scale(new Vector3(scale, scale, 1f));
            yield return null;
        }

        label.style.scale = new Scale(Vector3.one);
    }
}
