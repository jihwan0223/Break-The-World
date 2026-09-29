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

    private Label _pieceLabel; // 조각 개수를 표시하는 라벨
    private long _lastAmount; // 실제 조각 보유량 (화면 숫자가 따라갈 목표값)
    private double _shownPieces; // 지금 화면에 보이는 조각 수 - 늘어날 땐 목표값까지 굴러 올라감
    private bool _shownInitialized; // 처음 값은 굴러 올라가지 않고 바로 표시하기 위한 플래그
    private const float CountUpSpeed = 8f; // 숫자가 목표값을 따라붙는 속도 (클수록 빨리 따라붙음, 대략 0.4초)
    private const float CountUpMinPerSecond = 30f; // 차이가 작을 때 최소 초당 올라가는 양 - 작은 획득도 한 칸씩 올라가는 게 보이게
    private Label _crystalLabel; // 결정 개수를 표시하는 라벨
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
    }

    // UXML/USS 없이 코드로 직접 조각 라벨 + 반투명 배경을 구성 (화면 좌상단)
    private void BuildContainer(VisualElement root)
    {
        root.Clear();

        var background = new VisualElement();
        background.style.position = Position.Absolute;
        background.style.top = topOffset;
        background.style.left = leftOffset;
        background.style.paddingLeft = 12;
        background.style.paddingRight = 12;
        background.style.paddingTop = 6;
        background.style.paddingBottom = 6;
        background.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        background.style.borderTopLeftRadius = 4;
        background.style.borderTopRightRadius = 4;
        background.style.borderBottomLeftRadius = 4;
        background.style.borderBottomRightRadius = 4;

        _pieceLabel = CreateLabel(Color.white);
        background.Add(_pieceLabel);

        _crystalLabel = CreateLabel(new Color(0.55f, 0.9f, 1f)); // 결정은 하늘색으로 구분
        background.Add(_crystalLabel);

        root.Add(background);
    }

    private static Label CreateLabel(Color color)
    {
        var label = new Label();
        label.style.fontSize = 20;
        label.style.color = color;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        return label;
    }

    private void UpdateCrystalLabel(long amount)
    {
        _crystalLabel.text = $"결정: {NumberFormatUtil.Format(amount)}";

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
    }

    void Update()
    {
        if (_shownPieces >= _lastAmount) return;

        // 남은 차이를 매 프레임 일정 비율씩 줄여 빠르게 따라붙고, 차이가 작을 땐 최소 속도로 한 칸씩 올라감
        float dt = Time.unscaledDeltaTime; // 업그레이드 창 등으로 timeScale이 0이어도 올라가게
        double gap = _lastAmount - _shownPieces; // 아직 못 따라간 양
        double step = System.Math.Max(gap * (1 - Mathf.Exp(-CountUpSpeed * dt)), CountUpMinPerSecond * dt); // 이번 프레임에 올릴 양
        _shownPieces = System.Math.Min(_lastAmount, _shownPieces + step);
        SetPieceText();
        QueuePulse(_pieceLabel); // 올라가는 동안 커졌다 작아졌다 반복 (대기열 상한이 있어서 멈추면 곧 멈춤)
    }

    private void SetPieceText() => _pieceLabel.text = $"조각: {NumberFormatUtil.Format((long)_shownPieces)}";

    // 조각을 얻은 라벨이 잠깐 커졌다가(ease-out) 다시 원래 크기로 줄어드는(ease-in) 연출
    private IEnumerator PlayPieceGainPulse(Label label)
    {
        const float duration = 0.25f; // 연출 총 시간(초)
        const float peakScale = 1.3f; // 커질 때 최대 배율

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
