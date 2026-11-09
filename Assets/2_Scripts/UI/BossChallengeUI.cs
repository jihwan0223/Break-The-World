using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class BossChallengeUI : MonoBehaviour
{
    private const float Left = 20f;
    private const float Top = 142f; // 조각 표시(위 20 + 높이 110) 바로 아래
    private const float SlowScale = 0.3f;
    private const float SlowSeconds = 1f; // 실제 시간(초)

    private static BossChallengeUI _instance;

    private UIDocument _document;
    private Button _challengeButton;
    private VisualElement _fightPanel;
    private Button _giveUpButton;
    private VisualElement _failPopup;
    private ZoneBreaker _failedBoss;
    private Button[] _failButtons; // 실패 창 버튼 (뜨자마자 연타로 눌리지 않게 잠깐 막음)
    private const long FailButtonDelayMs = 700; // 실패 창 버튼이 눌리기 시작하는 시간 (밀리초, 실제 시간)

    private static readonly Color HpColor = new Color(0.91f, 0.38f, 0.35f);
    private static readonly Color HpBack = new Color(0.45f, 0.32f, 0.24f);
    private static readonly Color TimeColor = new Color(0.96f, 0.84f, 0.55f);
    private const float PanelAlpha = 0.7f; // 1 = 불투명
    private static readonly Color ShieldTextColor = new Color(0.25f, 0.47f, 0.72f);
    private Label _timeLabel;
    private VisualElement _timeFill;
    private VisualElement _hpFill;
    private VisualElement _shieldFill;
    private Label _shieldLabel;
    private ZoneBreaker _ready;
    private ZoneBreaker _announcedBoss; // 가운데 등장 연출을 이미 보여준 보스 (새 보스일 때만 다시 보여줌)
    private float _introElapsed = -1f; // 등장 연출 진행 시간 (실제 시간, 초) - 음수면 연출 없음
    private const float IntroPopSeconds = 0.25f; // 가운데에서 튀어나오는 시간
    private const float IntroHoldSeconds = 1f; // 가운데에 머무는 시간
    private const float IntroMoveSeconds = 0.5f; // 원래 자리로 날아가는 시간
    private const float IntroScale = 1.4f; // 가운데에 있을 때 크기 배율
    private bool _wasFighting;
    private readonly List<(VisualElement root, DisplayStyle display)> _hidden = new List<(VisualElement, DisplayStyle)>();
    private float _slowElapsed = -1f;
    private float _slowSetScale;
    private float _baseFixedDelta; // 느려진 동안 물리도 촘촘하게

    public static void Ensure()
    {
        if (_instance != null) return;
        var uiObject = new GameObject("BossChallengeUI");
        uiObject.SetActive(false); // 설정을 마친 뒤 켜야 처음부터 제대로 그려짐
        var document = uiObject.AddComponent<UIDocument>();
        PieceUI pieceUI = FindFirstObjectByType<PieceUI>();
        PanelSettings settings = pieceUI != null ? pieceUI.GetComponent<UIDocument>().panelSettings : null;
        if (settings == null)
        {
            settings = PixelStyle.NewPanelSettings();
            settings.sortingOrder = 100;
        }
        document.panelSettings = settings;
        document.sortingOrder = 10; // 조각 표시보다 위
        _instance = uiObject.AddComponent<BossChallengeUI>();
        uiObject.SetActive(true);
    }

    public static void PlaySlowMotion()
    {
        Ensure();
        _instance.StartSlow();
    }

    void OnEnable()
    {
        _document = GetComponent<UIDocument>();
        VisualElement root = _document.rootVisualElement;
        root.Clear();
        GameFonts.Apply(root);
        if (GameFonts.UiColored != null)
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiColored);
        root.pickingMode = PickingMode.Ignore; // 빈 곳 클릭은 게임으로

        _challengeButton = new Button(() => { if (_ready != null) _ready.StartBoss(); });
        Loc.Bind(_challengeButton, "보스 도전!");
        Place(_challengeButton);
        _challengeButton.style.width = 300;
        _challengeButton.style.height = 80;
        _challengeButton.style.fontSize = 36;
        PixelStyle.Button(_challengeButton, PixelStyle.Pink);
        _challengeButton.style.display = DisplayStyle.None;
        GuardPointer(_challengeButton);
        root.Add(_challengeButton);

        _fightPanel = new VisualElement();
        _fightPanel.style.position = Position.Absolute;
        _fightPanel.style.top = 24;
        _fightPanel.style.left = Length.Percent(17);
        _fightPanel.style.right = Length.Percent(17);
        _fightPanel.style.flexDirection = FlexDirection.Row;
        _fightPanel.style.alignItems = Align.Center;
        _fightPanel.style.paddingLeft = 20;
        _fightPanel.style.paddingRight = 16;
        _fightPanel.style.paddingTop = 14;
        _fightPanel.style.paddingBottom = 16;
        PixelStyle.Frame(_fightPanel, PixelStyle.Box);
        _fightPanel.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, PanelAlpha);
        _fightPanel.style.display = DisplayStyle.None;
        GuardPointer(_fightPanel);

        var bars = new VisualElement();
        bars.style.flexGrow = 1;

        var hpBack = new VisualElement();
        hpBack.style.height = 24;
        hpBack.style.backgroundColor = HpBack.linear;
        SetBorder(hpBack, 3, PixelStyle.Text.linear);
        _hpFill = new VisualElement();
        _hpFill.style.height = Length.Percent(100);
        _hpFill.style.backgroundColor = HpColor.linear;
        hpBack.Add(_hpFill);
        _shieldFill = new VisualElement();
        _shieldFill.style.position = Position.Absolute;
        _shieldFill.style.left = 0;
        _shieldFill.style.top = 0;
        _shieldFill.style.height = Length.Percent(100);
        _shieldFill.style.backgroundColor = new Color(1f, 1f, 1f, 0.65f);
        hpBack.Add(_shieldFill);
        hpBack.style.flexGrow = 1;
        var hpRow = new VisualElement();
        hpRow.style.flexDirection = FlexDirection.Row;
        hpRow.style.alignItems = Align.Center;
        hpRow.Add(hpBack);
        _shieldLabel = MakeLabel(36, ShieldTextColor.linear);
        _shieldLabel.style.width = 230; // 숫자가 바뀌어도 바 길이가 안 흔들리게
        _shieldLabel.style.marginLeft = 12;
        _shieldLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        hpRow.Add(_shieldLabel);
        bars.Add(hpRow);

        var timeRow = new VisualElement();
        timeRow.style.flexDirection = FlexDirection.Row;
        timeRow.style.alignItems = Align.Center;
        timeRow.style.marginTop = 8;
        var timeBack = new VisualElement();
        timeBack.style.flexGrow = 1;
        timeBack.style.height = 12;
        timeBack.style.backgroundColor = HpBack.linear;
        SetBorder(timeBack, 2, PixelStyle.Text.linear);
        _timeFill = new VisualElement();
        _timeFill.style.height = Length.Percent(100);
        _timeFill.style.backgroundColor = TimeColor.linear;
        timeBack.Add(_timeFill);
        timeRow.Add(timeBack);
        _timeLabel = MakeLabel(36, PixelStyle.Text.linear);
        _timeLabel.style.width = 242;
        _timeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        timeRow.Add(_timeLabel);
        bars.Add(timeRow);
        _fightPanel.Add(bars);

        _giveUpButton = new Button(() => ZoneBreaker.ActiveBoss?.GiveUpBoss());
        Loc.Bind(_giveUpButton, "포기");
        _giveUpButton.style.width = 130;
        _giveUpButton.style.height = 80;
        _giveUpButton.style.marginLeft = 18;
        _giveUpButton.style.fontSize = 24;
        PixelStyle.Button(_giveUpButton, PixelStyle.Pink);
        _fightPanel.Add(_giveUpButton);
        root.Add(_fightPanel);

        BuildFailPopup(root);
        ZoneBreaker.BossFailed += ShowFailPopup;
    }

    void OnDisable()
    {
        ZoneBreaker.BossFailed -= ShowFailPopup;
    }

    private void BuildFailPopup(VisualElement root)
    {
        _failPopup = new VisualElement();
        _failPopup.style.position = Position.Absolute;
        _failPopup.style.left = 0;
        _failPopup.style.right = 0;
        _failPopup.style.top = 0;
        _failPopup.style.bottom = 0;
        _failPopup.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
        _failPopup.style.alignItems = Align.Center;
        _failPopup.style.justifyContent = Justify.Center;
        _failPopup.style.display = DisplayStyle.None;
        GuardPointer(_failPopup);

        var window = new VisualElement();
        window.style.width = 620;
        window.style.alignItems = Align.Center;
        window.style.paddingTop = 36;
        window.style.paddingBottom = 40;
        window.style.paddingLeft = 32;
        window.style.paddingRight = 32;
        PixelStyle.Frame(window, PixelStyle.Window, 6f);

        var title = new Label();
        Loc.Bind(title, "보스전 실패");
        title.style.fontSize = 36;
        title.style.paddingLeft = 40;
        title.style.paddingRight = 40;
        title.style.paddingTop = 8;
        title.style.paddingBottom = 14;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        PixelStyle.Frame(title, PixelStyle.Ribbon);
        PixelStyle.OutlinedText(title, PixelStyle.Ribbon.text);
        window.Add(title);

        var message = MakeLabel(36, PixelStyle.Text.linear);
        Loc.Bind(message, "시간 안에 부수지 못했어요");
        message.style.marginTop = 24;
        message.style.marginBottom = 28;
        window.Add(message);

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        var retry = new Button(() => CloseFailPopup(true));
        Loc.Bind(retry, "재도전");
        var exit = new Button(() => CloseFailPopup(false));
        Loc.Bind(exit, "나가기");
        foreach (Button button in new[] { retry, exit })
        {
            button.style.width = 220;
            button.style.height = 76;
            button.style.fontSize = 36;
            button.style.marginLeft = 12;
            button.style.marginRight = 12;
        }
        PixelStyle.Button(retry, PixelStyle.Mint);
        PixelStyle.Button(exit, PixelStyle.Pink);
        buttons.Add(retry);
        buttons.Add(exit);
        _failButtons = new[] { retry, exit };
        window.Add(buttons);

        _failPopup.Add(window);
        root.Add(_failPopup);
    }

    private void ShowFailPopup(ZoneBreaker boss)
    {
        _failedBoss = boss;
        _failPopup.style.display = DisplayStyle.Flex;
        foreach (Button button in _failButtons) button.SetEnabled(false);
        _failPopup.schedule.Execute(() => { foreach (Button button in _failButtons) button.SetEnabled(true); }).StartingIn(FailButtonDelayMs);
        if (_slowElapsed >= 0f) StopSlow();
        Time.timeScale = 0f;
    }

    private void CloseFailPopup(bool retry)
    {
        _failPopup.style.display = DisplayStyle.None;
        UIPointerGuard.IsPointerOverUI = false;
        Time.timeScale = 1f;
        if (retry && _failedBoss != null) _failedBoss.StartBoss();
        _failedBoss = null;
    }

    void Update()
    {
        if (_challengeButton == null) return;

        ZoneBreaker active = ZoneBreaker.ActiveBoss;
        _ready = active == null && Time.timeScale > 0f && !UIPointerGuard.PopupOpen ? ZoneBreaker.ChallengeableBoss() : null; // 게임이 멈췄거나 창이 열려 있으면 버튼 숨김

        SetShown(_challengeButton, _ready != null);
        if (_ready != null)
        {
            if (_ready != _announcedBoss) { _announcedBoss = _ready; _introElapsed = 0f; } // 새 보스: 가운데에서 등장
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.04f;
            float scale = _introElapsed >= 0f ? UpdateIntro() : 1f; // 등장 연출 크기
            _challengeButton.style.scale = new Scale(new Vector3(pulse * scale, pulse * scale, 1f));
        }
        else if (_introElapsed >= 0f)
        {
            _introElapsed = -1f; // 연출 중에 숨으면 원래 자리로
            Place(_challengeButton);
        }

        bool fighting = active != null;
        SetShown(_fightPanel, fighting);
        if (fighting != _wasFighting) HideOtherUI(fighting);
        _wasFighting = fighting;
        if (fighting)
        {
            _timeLabel.text = $"{Mathf.Max(0f, active.BossTimeLeft):0.0}";
            _timeFill.style.width = Length.Percent(active.BossTimeRatio * 100f);
            _hpFill.style.width = Length.Percent(active.BossHpRatio * 100f);
            _shieldFill.style.width = Length.Percent(active.BossHpRatio * active.ShieldRatio * 100f); // 남은 체력 중 보호막 % 만큼 흰색
            int shieldPercent = Mathf.RoundToInt(active.ShieldRatio * 100f);
            _shieldLabel.text = Loc.T("보호막 -{0}%", shieldPercent);
            _shieldLabel.style.visibility = shieldPercent > 0 ? Visibility.Visible : Visibility.Hidden;
        }

        UpdateSlow();
    }

    void OnDestroy()
    {
        if (_slowElapsed >= 0f) StopSlow();
        if (_wasFighting) HideOtherUI(false);
    }

    private void HideOtherUI(bool hide)
    {
        if (hide)
        {
            _hidden.Clear();
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (document == _document || document.rootVisualElement == null) continue;
                VisualElement root = document.rootVisualElement;
                _hidden.Add((root, root.style.display.value));
                root.style.display = DisplayStyle.None;
            }
        }
        else
        {
            foreach ((VisualElement root, DisplayStyle display) in _hidden)
                if (root != null) root.style.display = display;
            _hidden.Clear();
        }
        UIPointerGuard.IsPointerOverUI = false; // 숨긴 UI의 포인터 표시가 남아 월드 클릭이 막히지 않게
    }

    private void StartSlow()
    {
        if (_slowElapsed < 0f) _baseFixedDelta = Time.fixedDeltaTime;
        _slowElapsed = 0f;
        SetSlowScale(SlowScale);
    }

    private void UpdateSlow()
    {
        if (_slowElapsed < 0f) return;
        if (!Mathf.Approximately(Time.timeScale, _slowSetScale)) { StopSlow(); return; } // 다른 곳(창 열림)이 timeScale을 바꿨으면 중단

        _slowElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_slowElapsed / SlowSeconds);
        if (t >= 1f) { Time.timeScale = 1f; StopSlow(); return; }
        SetSlowScale(Mathf.Lerp(SlowScale, 1f, t * t));
    }

    private void SetSlowScale(float scale)
    {
        Time.timeScale = _slowSetScale = scale;
        Time.fixedDeltaTime = _baseFixedDelta * scale;
    }

    private void StopSlow()
    {
        if (Mathf.Approximately(Time.timeScale, _slowSetScale)) Time.timeScale = 1f;
        Time.fixedDeltaTime = _baseFixedDelta;
        _slowElapsed = -1f;
    }

    // 가운데에서 튀어나와 잠깐 머문 뒤 원래 자리(돈 아래)로 날아감. 지금 크기 배율을 돌려줌
    private float UpdateIntro()
    {
        _introElapsed += Time.unscaledDeltaTime;
        VisualElement root = _document.rootVisualElement; // 화면 크기 기준
        float width = _challengeButton.resolvedStyle.width, height = _challengeButton.resolvedStyle.height; // 버튼 크기
        Vector2 center = new Vector2((root.layout.width - width) / 2f, (root.layout.height - height) / 2f); // 화면 가운데 자리
        float t = _introElapsed; // 경과 시간

        if (t < IntroPopSeconds)
        {
            SetPosition(center);
            float x = t / IntroPopSeconds - 1f; // ease-out-back
            const float overshoot = 1.70158f; // 살짝 넘쳤다 돌아오는 정도
            return IntroScale * (1f + (overshoot + 1f) * x * x * x + overshoot * x * x);
        }
        t -= IntroPopSeconds;
        if (t < IntroHoldSeconds)
        {
            SetPosition(center);
            return IntroScale;
        }
        t -= IntroHoldSeconds;
        if (t < IntroMoveSeconds)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / IntroMoveSeconds); // 이동 진행도
            SetPosition(Vector2.Lerp(center, new Vector2(Left, Top), k));
            return Mathf.Lerp(IntroScale, 1f, k);
        }
        _introElapsed = -1f;
        Place(_challengeButton);
        return 1f;
    }

    private void SetPosition(Vector2 position)
    {
        _challengeButton.style.left = position.x;
        _challengeButton.style.top = position.y;
    }

    private static void Place(VisualElement element)
    {
        element.style.position = Position.Absolute;
        element.style.left = Left;
        element.style.top = Top;
    }

    private static void GuardPointer(VisualElement element)
    {
        element.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        element.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);
    }

    private static void SetShown(VisualElement element, bool shown)
    {
        DisplayStyle display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        if (element.style.display.value == display) return;
        element.style.display = display;
        if (!shown) UIPointerGuard.IsPointerOverUI = false;
    }

    private static Label MakeLabel(int fontSize, Color color)
    {
        var label = new Label();
        label.style.fontSize = fontSize;
        label.style.color = color;
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    private static void SetBorder(VisualElement element, float width, Color color)
    {
        element.style.borderTopWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderTopColor = color;
        element.style.borderBottomColor = color;
        element.style.borderLeftColor = color;
        element.style.borderRightColor = color;
    }
}
