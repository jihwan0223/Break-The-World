using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// 화면 좌상단에 통합 조각(화폐) 보유량을 표시.
// 업그레이드/무기/오브젝트 화면이 열려있는 동안에도 계속 보이도록, PanelSettings의 sortingOrder를 높게 잡아
// 그 화면들(다른 UIDocument/Canvas)보다 항상 위에 그려지게 함
[RequireComponent(typeof(UIDocument))]
public class PieceUI : MonoBehaviour
{
    [SerializeField] private float leftOffset = 20f; // 화면 좌상단 기준 왼쪽 여백
    [SerializeField] private float topOffset = 20f; // 화면 좌상단 기준 위쪽 여백
    [SerializeField] private int sortingOrder = 100; // 업그레이드/무기/오브젝트 화면 위에 그려지도록 높게 잡은 값
    [SerializeField] private float fontSize = GameFonts.UiColoredPixelSize; // 조각/결정 숫자 글자 크기(px) - 폰트를 찍어낸 크기와 같아야 번지지 않고 도트가 또렷함 (우상단 버튼 글자 34와 거의 같은 크기)

    private Label _pieceLabel; // 조각 개수를 표시하는 라벨
    private long _lastAmount; // 실제 조각 보유량 (화면 숫자가 따라갈 목표값)
    private double _shownPieces; // 지금 화면에 보이는 조각 수 - 늘어날 땐 목표값까지 굴러 올라감
    private bool _shownInitialized; // 처음 값은 굴러 올라가지 않고 바로 표시하기 위한 플래그
    private const float CountUpSpeed = 8f; // 숫자가 목표값을 따라붙는 속도 (클수록 빨리 따라붙음, 대략 0.4초)
    private const float CountUpMinPerSecond = 30f; // 차이가 작을 때 최소 초당 올라가는 양 - 작은 획득도 한 칸씩 올라가는 게 보이게
    private const float CountUpSeconds = 0.35f; // 한 번에 얼마를 얻든 이 시간 안에는 화면 숫자가 다 따라붙음 (큰 수일수록 끝자리가 오래 구르던 것 방지)
    private double _countUpFloor; // 지금 따라붙는 동안의 최소 속도(초당) - 얻은 양이 클수록 커지고, 다 따라붙으면 0으로 돌아감
    private Label _crystalLabel; // 결정 개수를 표시하는 라벨
    private VisualElement _crystalRow; // 결정 줄 전체 (결정을 얻기 전엔 숨김)
    private long _lastCrystals; // 직전에 표시했던 결정 개수 (늘었는지 판단용)

    // 라벨 하나의 펄스 대기열 - 연속으로 늘어나면(빗자루로 줍는 중 등) 커졌다 작아졌다를 끊기지 않고 반복
    private class PulseState
    {
        public int queued; // 지금 재생 중인 것 다음에 이어서 재생할 횟수
        public bool running; // 재생 코루틴이 돌고 있는지
    }
    private const int MaxQueuedPulses = 1; // 미리 쌓아두는 펄스 수 상한 - 줍기를 멈추면 바로 멈추도록 작게
    private readonly Dictionary<Label, PulseState> _pulses = new Dictionary<Label, PulseState>(); // 라벨별 펄스 대기열

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();

        if (uiDocument.panelSettings == null)
        {
            // PanelSettings를 비워둬도 동작하도록 런타임에 자동 생성 + 기본 런타임 테마 할당
            // (안 넣으면 UI Toolkit이 제대로 렌더링하지 않음)
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            uiDocument.panelSettings = settings;
        }

        uiDocument.panelSettings.sortingOrder = sortingOrder;

        VisualElement root = uiDocument.rootVisualElement;
        GameFonts.Apply(root);
        if (GameFonts.UiColored != null)
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiColored); // 결정 글자를 하늘색으로 칠하려면 색이 먹는 폰트여야 함
        BuildContainer(root);
    }

    void Start()
    {
        // CurrencyManager.Awake()가 먼저 실행되도록 OnEnable이 아닌 Start에서 구독
        // (Unity는 모든 오브젝트의 Awake를 먼저 실행한 뒤 Start를 실행하므로 순서가 보장됨)
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnPiecesChanged += UpdatePieceLabel;
            CurrencyManager.Instance.OnCrystalsChanged += UpdateCrystalLabel;
            GameSettings.OnNumberFormatChanged += RedrawNumbers;

            // 저장 파일을 이미 불러왔을 수 있으니, 지금 보유량을 한 번 읽어서 라벨을 미리 채움
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

    // UXML/USS 없이 코드로 직접 조각 라벨 + 반투명 배경을 구성 (화면 좌상단)
    private void BuildContainer(VisualElement root)
    {
        root.Clear();

        // 우상단 버튼(SidePanelUI.CreateButton)과 같은 모양 - 같은 높이/배경색/모서리에, 버튼 테마 클래스를 붙여 테두리와 바깥 여백도 똑같이 맞춤
        var background = new VisualElement();
        background.AddToClassList("unity-button");
        background.pickingMode = PickingMode.Ignore; // 버튼처럼 생겼지만 누르는 게 아니라서 마우스에 반응하지 않게 (뒤쪽 클릭도 통과)
        background.style.position = Position.Absolute;
        background.style.top = topOffset;
        background.style.left = leftOffset;
        background.style.minWidth = 340;
        background.style.paddingLeft = 16;
        background.style.paddingRight = 16;
        background.style.paddingTop = 14;
        background.style.paddingBottom = 22; // 아래 두께만큼 더
        background.style.justifyContent = Justify.Center; // 두 줄을 상자 세로 가운데에
        background.style.alignItems = Align.Stretch; // 줄들을 상자 폭에 맞춤
        PixelStyle.Frame(background, PixelStyle.Box); // 오른쪽 위 버튼과 같은 픽셀 틀 (크림)

        _pieceLabel = AddRow(background, ShardTexture(WarmShard), PixelStyle.Text.linear, out _); // 크림 위 진한 갈색
        _crystalLabel = AddRow(background, ShardTexture(SkyShard), new Color(0.23f, 0.53f, 0.66f).linear, out _crystalRow); // 결정은 진한 하늘색

        root.Add(background);
    }

    // 상자 안 한 줄: 왼쪽 아이콘 + 바로 옆 숫자
    private Label AddRow(VisualElement box, Texture2D iconTexture, Color color, out VisualElement row)
    {
        row = new VisualElement();
        row.pickingMode = PickingMode.Ignore;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.height = 46;
        box.Add(row);

        var icon = new VisualElement(); // 조각/결정 도트 그림
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
        label.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50)); // 커졌다 작아질 때 왼쪽 끝 기준 - 상자 왼쪽 밖으로 안 넘치게
        row.Add(label);
        return label;
    }

    // 조각 도트 (o 테두리, a 밝은 면, b 중간, c 어두운 면, h 반짝) - 업그레이드 노드 그림의 조각과 같은 모양
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
    private static readonly Color32[] WarmShard = { new Color32(60, 36, 24, 255), new Color32(255, 214, 120, 255), new Color32(235, 160, 70, 255), new Color32(180, 100, 40, 255), new Color32(255, 255, 230, 255) }; // 조각 색 (o a b c h)
    private static readonly Color32[] SkyShard = { new Color32(20, 40, 70, 255), new Color32(170, 230, 255, 255), new Color32(90, 180, 240, 255), new Color32(40, 110, 200, 255), new Color32(255, 255, 255, 255) }; // 결정 색 (o a b c h)

    private static Texture2D ShardTexture(Color32[] palette)
    {
        int w = ShardRows[0].Length, h = ShardRows.Length; // 도트 크기
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point }; // sRGB 텍스처
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int k = "oabch".IndexOf(ShardRows[y][x]); // 팔레트 번호 (-1이면 빈칸)
                tex.SetPixel(x, h - 1 - y, k >= 0 ? (Color)palette[k] : Color.clear); // 위에서부터 그린 그림을 아래부터 채움
            }
        tex.Apply();
        return tex;
    }

    private void UpdateCrystalLabel(long amount)
    {
        _crystalLabel.text = NumberFormatUtil.Format(amount);
        // 결정을 한 번이라도 얻기 전에는 결정 줄을 숨김 (다 써서 0이 돼도 얻은 적이 있으면 계속 보임)
        bool shown = amount > 0 || (CurrencyManager.Instance != null && CurrencyManager.Instance.EverHadCrystal); // 결정 줄을 보여줄지
        _crystalRow.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        bool increased = amount > _lastCrystals;
        _lastCrystals = amount;

        if (increased)
            QueuePulse(_crystalLabel);
    }

    // 늘어날 때마다 펄스를 한 번 예약 - 이미 재생 중이면 끝난 뒤 이어서 재생
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

    // 조각 보유량이 바뀔 때마다 목표값 갱신 - 늘어난 건 Update에서 숫자가 굴러 올라가듯 따라가고(좌라락), 줄어든 건(구매/초기화) 바로 반영
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
            _countUpFloor = System.Math.Max(_countUpFloor, (amount - _shownPieces) / CountUpSeconds); // 지금 벌어진 차이를 CountUpSeconds 안에 메울 속도
        }
    }

    void Update()
    {
        if (_shownPieces >= _lastAmount)
        {
            _countUpFloor = 0;
            return;
        }

        // 남은 차이를 매 프레임 일정 비율씩 줄여 빠르게 따라붙고, 끝자리는 최소 속도로 마저 올라감.
        // 최소 속도는 얻은 양에 비례해서, 큰 수를 얻어도 작은 수를 얻을 때와 같은 시간 안에 끝남
        float dt = Time.unscaledDeltaTime; // 업그레이드 창 등으로 timeScale이 0이어도 올라가게
        double gap = _lastAmount - _shownPieces; // 아직 못 따라간 양
        double floor = System.Math.Max(CountUpMinPerSecond, _countUpFloor); // 이번 프레임의 최소 속도(초당)
        double step = System.Math.Max(gap * (1 - Mathf.Exp(-CountUpSpeed * dt)), floor * dt); // 이번 프레임에 올릴 양
        _shownPieces = System.Math.Min(_lastAmount, _shownPieces + step);
        SetPieceText();
        QueuePulse(_pieceLabel); // 올라가는 동안 커졌다 작아졌다 반복 (대기열 상한이 있어서 멈추면 곧 멈춤)
    }

    // 숫자 표시 방식(영어 단위/숫자)이 바뀌면 지금 값을 새 방식으로 다시 씀
    private void RedrawNumbers()
    {
        SetPieceText();
        if (CurrencyManager.Instance != null) _crystalLabel.text = NumberFormatUtil.Format(CurrencyManager.Instance.GetCrystals());
    }

    private void SetPieceText() => _pieceLabel.text = NumberFormatUtil.Format((long)_shownPieces);

    // 조각을 얻은 라벨이 잠깐 커졌다가(ease-out) 다시 원래 크기로 줄어드는(ease-in) 연출
    private IEnumerator PlayPieceGainPulse(Label label)
    {
        const float duration = 0.25f; // 연출 총 시간(초)
        const float peakScale = 1.15f; // 커질 때 최대 배율 (글자가 커진 만큼 줄임 - 상자 밖으로 많이 넘치지 않게)

        float elapsed = 0f; // 연출 시작 후 흐른 시간

        while (elapsed < duration)
        {
            // 업그레이드 페이지가 열려있는 동안 Time.timeScale이 0이라 deltaTime 대신 unscaledDeltaTime을 써야 연출이 재생됨
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration); // 0~1 진행률

            // 앞 절반은 1배 -> peakScale로 커지고, 뒤 절반은 peakScale -> 1배로 다시 줄어듦
            float scale = progress < 0.5f
                ? Mathf.Lerp(1f, peakScale, progress / 0.5f)
                : Mathf.Lerp(peakScale, 1f, (progress - 0.5f) / 0.5f);

            label.style.scale = new Scale(new Vector3(scale, scale, 1f));
            yield return null;
        }

        label.style.scale = new Scale(Vector3.one);
    }
}
