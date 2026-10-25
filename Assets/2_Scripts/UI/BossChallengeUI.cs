using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// 보스전 UI: 평소엔 조각 표시 아래에 지금 맵 보스의 "보스 도전" 버튼, 보스전 중엔 화면 위에 보스 체력/보호막/남은 시간 + 포기 버튼,
// 시간 초과로 실패하면 재도전/나가기 창.
// 보스전 동안은 다른 UI(조각 표시 포함)를 전부 숨김. 성공 순간 슬로우 모션도 여기서 돌림.
// 씬에 따로 놓을 필요 없이 ZoneBreaker가 처음 켜질 때 만들어짐
public class BossChallengeUI : MonoBehaviour
{
    private const float Left = 20f; // 화면 왼쪽 여백 (조각 표시와 맞춤)
    private const float Top = 142f; // 화면 위쪽 여백 - 조각 표시(위 20 + 높이 110) 바로 아래
    private const float SlowScale = 0.3f; // 성공 순간 게임 속도
    private const float SlowSeconds = 1f; // 원래 속도로 돌아오는 데 걸리는 실제 시간(초)

    private static BossChallengeUI _instance; // 하나뿐인 인스턴스

    private UIDocument _document; // 이 UI를 그리는 문서
    private Button _challengeButton; // 보스 도전 버튼
    private VisualElement _fightPanel; // 보스전 중 상자 (체력/보호막/시간/포기) - 화면 위 가운데
    private Button _giveUpButton; // 포기 버튼 - 보스전 상자 안, 체력바 오른쪽
    private VisualElement _failPopup; // 실패 창 (재도전/나가기)
    private ZoneBreaker _failedBoss; // 방금 실패한 보스 - 재도전 대상

    private static readonly Color HpColor = new Color(0.91f, 0.38f, 0.35f); // 보스 체력 (산호빛 빨강)
    private static readonly Color HpBack = new Color(0.45f, 0.32f, 0.24f); // 체력/타이머 바 빈 곳 (갈색)
    private static readonly Color TimeColor = new Color(0.96f, 0.84f, 0.55f); // 타이머 (버터색)
    private Label _timeLabel; // 남은 시간 숫자 (타이머 바 오른쪽)
    private VisualElement _timeFill; // 타이머 바 채움 - 시간이 갈수록 줄어듦
    private VisualElement _hpFill; // 보스 체력바 채움 (빨강)
    private VisualElement _shieldFill; // 체력바 위에 겹치는 보호막 (흰색) - 보호막 오브젝트가 깎일수록 줄어듦
    private ZoneBreaker _ready; // 지금 도전 버튼이 가리키는 보스
    private bool _wasFighting; // 지난 프레임에 보스전 중이었는지 - 바뀔 때만 다른 UI를 숨기고/되돌림
    private readonly List<(VisualElement root, DisplayStyle display)> _hidden = new List<(VisualElement, DisplayStyle)>(); // 보스전 동안 숨긴 다른 UI와 원래 표시 상태
    private float _slowElapsed = -1f; // 슬로우 모션이 시작된 뒤 흐른 실제 시간 (-1이면 안 하는 중)
    private float _slowSetScale; // 슬로우 모션이 마지막으로 넣은 게임 속도 - 그새 다른 곳(창 열림)이 바꿨으면 손 뗌
    private float _baseFixedDelta; // 원래 물리 간격 - 느려진 동안 물리도 촘촘하게 돌려 파편이 끊겨 보이지 않게

    public static void Ensure()
    {
        if (_instance != null) return;
        var uiObject = new GameObject("BossChallengeUI"); // UI 문서를 담을 오브젝트
        uiObject.SetActive(false); // 문서 설정을 마친 뒤 켜서 처음부터 제대로 그려지게
        var document = uiObject.AddComponent<UIDocument>(); // 이 UI를 그릴 문서 (먼저 붙여야 켜질 때 먼저 준비됨)
        PieceUI pieceUI = FindFirstObjectByType<PieceUI>(); // 조각 표시와 같은 화면 설정(크기 배율/순서)을 씀
        PanelSettings settings = pieceUI != null ? pieceUI.GetComponent<UIDocument>().panelSettings : null; // 공유할 화면 설정
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            settings.sortingOrder = 100;
        }
        document.panelSettings = settings;
        document.sortingOrder = 10; // 같은 화면 설정을 쓰는 조각 표시보다 위
        _instance = uiObject.AddComponent<BossChallengeUI>();
        uiObject.SetActive(true);
    }

    // 보스를 잡은 순간 - 게임 속도를 확 낮췄다가 1초 동안 부드럽게 원래대로
    public static void PlaySlowMotion()
    {
        Ensure();
        _instance.StartSlow();
    }

    void OnEnable()
    {
        _document = GetComponent<UIDocument>();
        VisualElement root = _document.rootVisualElement; // 이 문서의 최상위
        root.Clear();
        GameFonts.Apply(root);
        if (GameFonts.UiColored != null)
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiColored); // 글자색이 먹는 글꼴
        root.pickingMode = PickingMode.Ignore; // 빈 곳 클릭은 게임으로

        // 보스 도전 버튼 - 조각 표시 아래 (분홍 픽셀 버튼)
        _challengeButton = new Button(() => { if (_ready != null) _ready.StartBoss(); }) { text = "보스 도전!" };
        Place(_challengeButton);
        _challengeButton.style.width = 300;
        _challengeButton.style.height = 80;
        _challengeButton.style.fontSize = 36;
        PixelStyle.Button(_challengeButton, PixelStyle.Pink);
        _challengeButton.style.display = DisplayStyle.None;
        GuardPointer(_challengeButton);
        root.Add(_challengeButton);

        // 보스전 상자 - 화면 위 가운데: [체력바 + 타이머] [포기]
        _fightPanel = new VisualElement();
        _fightPanel.style.position = Position.Absolute;
        _fightPanel.style.top = 24;
        _fightPanel.style.left = Length.Percent(17);
        _fightPanel.style.right = Length.Percent(17); // 가로 66%
        _fightPanel.style.flexDirection = FlexDirection.Row;
        _fightPanel.style.alignItems = Align.Center;
        _fightPanel.style.paddingLeft = 20;
        _fightPanel.style.paddingRight = 16;
        _fightPanel.style.paddingTop = 14;
        _fightPanel.style.paddingBottom = 16;
        PixelStyle.Frame(_fightPanel, PixelStyle.Box); // 크림 픽셀 틀
        _fightPanel.style.display = DisplayStyle.None;
        GuardPointer(_fightPanel);

        var bars = new VisualElement(); // 체력바 + 타이머 줄
        bars.style.flexGrow = 1;

        var hpBack = new VisualElement(); // 체력바 바탕
        hpBack.style.height = 24;
        hpBack.style.backgroundColor = HpBack.linear;
        SetBorder(hpBack, 3, PixelStyle.Text.linear); // 진갈색 테두리
        _hpFill = new VisualElement();
        _hpFill.style.height = Length.Percent(100);
        _hpFill.style.backgroundColor = HpColor.linear;
        hpBack.Add(_hpFill);
        _shieldFill = new VisualElement();
        _shieldFill.style.position = Position.Absolute; // 빨간 체력 위에 겹침
        _shieldFill.style.left = 0;
        _shieldFill.style.top = 0;
        _shieldFill.style.height = Length.Percent(100);
        _shieldFill.style.backgroundColor = new Color(1f, 1f, 1f, 0.65f); // 반투명 흰색 - 체력 위에 씌운 보호막
        hpBack.Add(_shieldFill);
        bars.Add(hpBack);

        var timeRow = new VisualElement(); // 타이머 바 + 남은 초
        timeRow.style.flexDirection = FlexDirection.Row;
        timeRow.style.alignItems = Align.Center;
        timeRow.style.marginTop = 8;
        var timeBack = new VisualElement(); // 타이머 바 바탕
        timeBack.style.flexGrow = 1;
        timeBack.style.height = 12;
        timeBack.style.backgroundColor = HpBack.linear;
        SetBorder(timeBack, 2, PixelStyle.Text.linear);
        _timeFill = new VisualElement();
        _timeFill.style.height = Length.Percent(100);
        _timeFill.style.backgroundColor = TimeColor.linear; // 버터색 타이머
        timeBack.Add(_timeFill);
        timeRow.Add(timeBack);
        _timeLabel = MakeLabel(36, PixelStyle.Text.linear);
        _timeLabel.style.width = 100; // 숫자 자리수가 바뀌어도 바 길이가 안 흔들리게
        _timeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        timeRow.Add(_timeLabel);
        bars.Add(timeRow);
        _fightPanel.Add(bars);

        // 포기 - 체력바 바로 오른쪽 (작은 분홍 버튼)
        _giveUpButton = new Button(() => ZoneBreaker.ActiveBoss?.GiveUpBoss()) { text = "포기" };
        _giveUpButton.style.width = 110;
        _giveUpButton.style.height = 64;
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

    // 실패 창: 어둡게 덮은 화면 가운데 크림 창 - "보스전 실패" + [재도전] [나가기]
    private void BuildFailPopup(VisualElement root)
    {
        _failPopup = new VisualElement();
        _failPopup.style.position = Position.Absolute;
        _failPopup.style.left = 0;
        _failPopup.style.right = 0;
        _failPopup.style.top = 0;
        _failPopup.style.bottom = 0;
        _failPopup.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f); // 뒤 게임을 어둡게
        _failPopup.style.alignItems = Align.Center;
        _failPopup.style.justifyContent = Justify.Center;
        _failPopup.style.display = DisplayStyle.None;
        GuardPointer(_failPopup);

        var window = new VisualElement(); // 가운데 창
        window.style.width = 620;
        window.style.alignItems = Align.Center;
        window.style.paddingTop = 36;
        window.style.paddingBottom = 40;
        window.style.paddingLeft = 32;
        window.style.paddingRight = 32;
        PixelStyle.Frame(window, PixelStyle.Window, 6f);

        var title = new Label("보스전 실패"); // 제목 리본
        title.style.fontSize = 36;
        title.style.paddingLeft = 40;
        title.style.paddingRight = 40;
        title.style.paddingTop = 8;
        title.style.paddingBottom = 14;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        PixelStyle.Frame(title, PixelStyle.Ribbon);
        PixelStyle.OutlinedText(title, PixelStyle.Ribbon.text);
        window.Add(title);

        var message = MakeLabel(36, PixelStyle.Text.linear); // 안내 문구
        message.text = "시간 안에 부수지 못했어요";
        message.style.marginTop = 24;
        message.style.marginBottom = 28;
        window.Add(message);

        var buttons = new VisualElement(); // [재도전] [나가기]
        buttons.style.flexDirection = FlexDirection.Row;
        var retry = new Button(() => CloseFailPopup(true)) { text = "재도전" };
        var exit = new Button(() => CloseFailPopup(false)) { text = "나가기" };
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
        window.Add(buttons);

        _failPopup.Add(window);
        root.Add(_failPopup);
    }

    // 실패했을 때 - 게임을 멈추고 실패 창을 띄움
    private void ShowFailPopup(ZoneBreaker boss)
    {
        _failedBoss = boss;
        _failPopup.style.display = DisplayStyle.Flex;
        if (_slowElapsed >= 0f) StopSlow();
        Time.timeScale = 0f; // 창이 떠 있는 동안 게임 정지 (다른 창들과 같음)
    }

    // retry면 같은 보스에 바로 다시 도전, 아니면 그냥 게임으로 돌아감
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

        ZoneBreaker active = ZoneBreaker.ActiveBoss; // 보스전 중인 보스
        _ready = active == null && Time.timeScale > 0f ? ZoneBreaker.ChallengeableBoss() : null; // 업그레이드 창/실패 창 등으로 게임이 멈춰 있으면 안 띄움

        SetShown(_challengeButton, _ready != null);
        if (_ready != null)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.04f; // 커졌다 작아졌다
            _challengeButton.style.scale = new Scale(new Vector3(pulse, pulse, 1f));
        }

        bool fighting = active != null; // 보스전 중인지
        SetShown(_fightPanel, fighting);
        if (fighting != _wasFighting) HideOtherUI(fighting);
        _wasFighting = fighting;
        if (fighting)
        {
            _timeLabel.text = $"{Mathf.Max(0f, active.BossTimeLeft):0.0}";
            _timeFill.style.width = Length.Percent(active.BossTimeRatio * 100f);
            _hpFill.style.width = Length.Percent(active.BossHpRatio * 100f);
            _shieldFill.style.width = Length.Percent(active.BossHpRatio * active.ShieldFill * 100f); // 빨간 체력 중 보호막이 덮은 만큼
        }

        UpdateSlow();
    }

    void OnDestroy()
    {
        if (_slowElapsed >= 0f) StopSlow();
        if (_wasFighting) HideOtherUI(false);
    }

    // 보스전 동안 이 UI만 남기고 다른 UI 문서(조각 표시 포함)를 숨김 / 끝나면 원래대로
    private void HideOtherUI(bool hide)
    {
        if (hide)
        {
            _hidden.Clear();
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (document == _document || document.rootVisualElement == null) continue;
                VisualElement root = document.rootVisualElement; // 숨길 UI
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
        UIPointerGuard.IsPointerOverUI = false; // 숨긴 UI 위에 있던 포인터 표시가 남아 월드 클릭이 막히지 않게
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
        if (!Mathf.Approximately(Time.timeScale, _slowSetScale)) { StopSlow(); return; } // 창이 열려 시간이 멈추는 등 다른 곳이 바꿈

        _slowElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_slowElapsed / SlowSeconds); // 0~1 진행률
        if (t >= 1f) { Time.timeScale = 1f; StopSlow(); return; }
        SetSlowScale(Mathf.Lerp(SlowScale, 1f, t * t)); // 처음엔 느리게 머물다 점점 빨리 돌아옴
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

    private static void Place(VisualElement element)
    {
        element.style.position = Position.Absolute;
        element.style.left = Left;
        element.style.top = Top;
    }

    // 이 UI 위에 포인터가 있으면 뒤의 오브젝트가 같이 맞지 않게
    private static void GuardPointer(VisualElement element)
    {
        element.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        element.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);
    }

    // 보였다/숨었다가 바뀔 때만 적용 - 숨는 순간 포인터가 위에 있었으면 표시를 풀어줌
    private static void SetShown(VisualElement element, bool shown)
    {
        DisplayStyle display = shown ? DisplayStyle.Flex : DisplayStyle.None; // 바꿀 표시 상태
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
