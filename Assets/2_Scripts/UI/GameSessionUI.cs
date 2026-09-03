using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

// 게임 런의 화면을 담당하는 UI 툴킷 문서. GameSessionManager의 상태에 따라 전환해서 보여줌:
//  - PreStart : 화면 중앙 "게임 시작" 버튼
//  - Countdown: 화면 중앙 3-2-1 카운트다운 숫자
//  - Running  : 체력바 위 타이머 바(조금씩 닳음) + 화면 아래 "중도포기" 버튼
//  - Ended    : 이번 판에 얻은 조각을 보여주는 결과창 + [재시작] [나가기]
// HealthBarUI와 같은 방식으로 UXML/USS 없이 코드로만 구성함.
[RequireComponent(typeof(UIDocument))]
public class GameSessionUI : MonoBehaviour
{
    [SerializeField] private float timerBarWidth = 280f; // 타이머 바 너비(px) - 체력바(300)보다 살짝 작게
    [SerializeField] private float timerBarHeight = 20f; // 타이머 바 높이(px) - 체력바(24)보다 살짝 작게. 남은 시간 텍스트가 이 안에 들어감
    [SerializeField] private float timerBarTopOffset = 6f; // 화면 위에서 타이머 바까지 거리(px) - 체력바(topOffset 34)보다 위에 오도록

    private VisualElement _root; // UIDocument 최상위 - 업그레이드 창이 열리면 통째로 숨김
    private VisualElement _startScreen; // 시작 전 화면 (중앙 게임 시작 버튼을 담는 컨테이너)
    private Label _countdownLabel; // 카운트다운 중 화면 중앙에 크게 뜨는 숫자 (3 -> 2 -> 1)
    private VisualElement _timerBar; // 진행 중 타이머 바 배경
    private VisualElement _timerFill; // 진행 중 타이머 바 fill - 남은 시간 비율만큼 너비가 줄어듦
    private Label _timerLabel; // 타이머 바 위에 표시되는 남은 시간 텍스트
    private VisualElement _giveUpRow; // 진행 중 "중도포기" 버튼을 담는 컨테이너 (화면 하단 중앙)
    private VisualElement _resultScreen; // 결과창 전체 (반투명 배경 + 중앙 박스)
    private Label _resultBody; // 결과창 - 이번 판 획득 조각 총합 + 오브젝트별 내역

    private bool _subscribed; // GameSessionManager 이벤트 구독을 마쳤는지 (매니저가 늦게 생성될 수 있어 Update에서 재시도)

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();

        if (uiDocument.panelSettings == null)
        {
            // PanelSettings/테마를 비워둬도 동작하도록 런타임에 자동 생성 (HealthBarUI와 동일)
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            settings.sortingOrder = 10; // 다른 HUD 문서(체력바 등)보다 위에 - 결과창이 화면을 확실히 덮도록
            uiDocument.panelSettings = settings;
        }

        _root = uiDocument.rootVisualElement;
        Build(_root);

        TrySubscribe();

        // 체력바처럼, Canvas로 만든 업그레이드 화면이 열리고 닫힐 때 이 문서도 같이 숨기고 보여줌
        UpgradeTreeUI.OnTreeToggled += SetHiddenForUpgrade;
    }

    void OnDisable()
    {
        if (_subscribed && GameSessionManager.Instance != null)
            GameSessionManager.Instance.OnStateChanged -= HandleStateChanged;
        _subscribed = false;

        UpgradeTreeUI.OnTreeToggled -= SetHiddenForUpgrade;
    }

    // GameSessionManager가 이 UI보다 늦게 Awake될 수 있어, 구독이 안 됐으면 계속 시도함
    private void TrySubscribe()
    {
        if (_subscribed || GameSessionManager.Instance == null)
            return;

        GameSessionManager.Instance.OnStateChanged += HandleStateChanged;
        HandleStateChanged(GameSessionManager.Instance.CurrentState); // 지금 상태로 화면을 한 번 맞춤
        _subscribed = true;
    }

    void Update()
    {
        if (!_subscribed)
        {
            TrySubscribe();
            return;
        }

        if (GameSessionManager.Instance == null)
            return;

        GameSessionManager.State state = GameSessionManager.Instance.CurrentState;

        // 진행 중일 때만 타이머 바/텍스트를 매 프레임 갱신
        if (state == GameSessionManager.State.Running)
        {
            float remaining = GameSessionManager.Instance.RunTimeRemaining;
            _timerFill.style.width = Length.Percent(GameSessionManager.Instance.RunTimeNormalized * 100f);
            _timerLabel.text = $"{Mathf.CeilToInt(remaining)}초";
        }
        // 카운트다운 중이면 중앙 숫자를 매 프레임 갱신 (3 -> 2 -> 1)
        else if (state == GameSessionManager.State.Countdown)
        {
            _countdownLabel.text = GameSessionManager.Instance.CountdownNumber.ToString();
        }
    }

    private void Build(VisualElement root)
    {
        root.Clear();
        root.pickingMode = PickingMode.Ignore; // 빈 영역이 뒤쪽(다른 UIDocument/월드) 클릭을 먹지 않도록 - 실제 버튼/결과창 배경만 클릭을 받음

        BuildStartScreen(root);
        BuildCountdown(root);
        BuildTimerBar(root);
        BuildGiveUpButton(root);
        BuildResultScreen(root);
    }

    // ---- 카운트다운: 화면 중앙 큰 숫자 ----
    private void BuildCountdown(VisualElement root)
    {
        _countdownLabel = new Label("3");
        _countdownLabel.style.position = Position.Absolute;
        _countdownLabel.style.left = Length.Percent(50);
        _countdownLabel.style.top = Length.Percent(50);
        _countdownLabel.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
        _countdownLabel.style.fontSize = 120;
        _countdownLabel.style.color = Color.white;
        _countdownLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _countdownLabel.pickingMode = PickingMode.Ignore;
        root.Add(_countdownLabel);
    }

    // ---- 시작 전: 화면 중앙 "게임 시작" 버튼 ----
    private void BuildStartScreen(VisualElement root)
    {
        _startScreen = new VisualElement();
        _startScreen.style.position = Position.Absolute;
        _startScreen.style.left = Length.Percent(50);
        _startScreen.style.top = Length.Percent(50);
        _startScreen.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50)); // 자기 크기의 절반만큼 당겨서 정확히 중앙
        _startScreen.pickingMode = PickingMode.Ignore;

        var startButton = MakeButton("게임 시작");
        startButton.clicked += () => GameSessionManager.Instance?.StartRun();
        startButton.style.width = 240;
        startButton.style.height = 64;
        startButton.style.fontSize = 24;

        _startScreen.Add(startButton);
        root.Add(_startScreen);
    }

    // ---- 진행 중: 체력바 위 타이머 바 ----
    private void BuildTimerBar(VisualElement root)
    {
        _timerBar = new VisualElement();
        _timerBar.style.position = Position.Absolute;
        _timerBar.style.top = timerBarTopOffset;
        _timerBar.style.left = Length.Percent(50);
        _timerBar.style.marginLeft = -timerBarWidth / 2f;
        _timerBar.style.width = timerBarWidth;
        _timerBar.style.height = timerBarHeight;
        _timerBar.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        SetBorderRadius(_timerBar, 4);
        _timerBar.pickingMode = PickingMode.Ignore;

        _timerFill = new VisualElement();
        _timerFill.style.position = Position.Absolute;
        _timerFill.style.left = 0;
        _timerFill.style.top = 0;
        _timerFill.style.bottom = 0;
        _timerFill.style.width = Length.Percent(100);
        _timerFill.style.backgroundColor = new Color(0.95f, 0.85f, 0.25f); // 노란빛 - 체력바(빨강)와 구분
        SetBorderRadius(_timerFill, 4);
        _timerBar.Add(_timerFill);

        _timerLabel = new Label();
        _timerLabel.style.position = Position.Absolute;
        _timerLabel.style.left = 0;
        _timerLabel.style.right = 0;
        _timerLabel.style.top = 0;
        _timerLabel.style.bottom = 0;
        _timerLabel.style.unityTextAlign = TextAnchor.MiddleCenter; // 바 안에 세로 가운데 정렬
        _timerLabel.style.fontSize = 13;
        _timerLabel.style.color = Color.white;
        _timerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _timerLabel.pickingMode = PickingMode.Ignore;
        _timerBar.Add(_timerLabel);

        root.Add(_timerBar);
    }

    // ---- 진행 중: 화면 하단 중앙 "중도포기" 버튼 ----
    private void BuildGiveUpButton(VisualElement root)
    {
        _giveUpRow = new VisualElement();
        _giveUpRow.style.position = Position.Absolute;
        _giveUpRow.style.bottom = 24;
        _giveUpRow.style.left = Length.Percent(50);
        _giveUpRow.style.translate = new Translate(Length.Percent(-50), 0);
        _giveUpRow.pickingMode = PickingMode.Ignore;

        var giveUpButton = MakeButton("중도포기");
        giveUpButton.clicked += () => GameSessionManager.Instance?.GiveUp();
        giveUpButton.style.backgroundColor = new Color(0.35f, 0.12f, 0.12f, 0.9f);
        giveUpButton.style.fontSize = 15;

        _giveUpRow.Add(giveUpButton);
        root.Add(_giveUpRow);
    }

    // ---- 종료: 결과창 ----
    private void BuildResultScreen(VisualElement root)
    {
        _resultScreen = new VisualElement();
        _resultScreen.style.position = Position.Absolute;
        _resultScreen.style.left = 0;
        _resultScreen.style.top = 0;
        _resultScreen.style.right = 0;
        _resultScreen.style.bottom = 0;
        _resultScreen.style.backgroundColor = new Color(0f, 0f, 0f, 0.75f); // 뒤쪽 게임 화면을 덮는 반투명 배경
        _resultScreen.style.alignItems = Align.Center;
        _resultScreen.style.justifyContent = Justify.Center;
        _resultScreen.pickingMode = PickingMode.Position; // 결과창이 떠있는 동안은 뒤쪽 클릭을 막음

        var box = new VisualElement();
        box.style.minWidth = 320;
        box.style.paddingLeft = 32;
        box.style.paddingRight = 32;
        box.style.paddingTop = 24;
        box.style.paddingBottom = 24;
        box.style.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        SetBorderRadius(box, 10);
        box.style.alignItems = Align.Center;

        var title = new Label("이번 판 종료");
        title.style.fontSize = 22;
        title.style.color = Color.white;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 12;
        box.Add(title);

        _resultBody = new Label();
        _resultBody.style.fontSize = 16;
        _resultBody.style.color = new Color(0.9f, 0.9f, 0.9f);
        _resultBody.style.unityTextAlign = TextAnchor.UpperCenter;
        _resultBody.style.whiteSpace = WhiteSpace.Normal;
        _resultBody.style.marginBottom = 20;
        box.Add(_resultBody);

        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;

        var restartButton = MakeButton("재시작");
        restartButton.clicked += () => GameSessionManager.Instance?.StartRun();
        restartButton.style.marginRight = 8;

        var exitButton = MakeButton("나가기");
        exitButton.clicked += () => GameSessionManager.Instance?.ExitToPreStart();

        buttonRow.Add(restartButton);
        buttonRow.Add(exitButton);
        box.Add(buttonRow);

        _resultScreen.Add(box);
        root.Add(_resultScreen);
    }

    // 상태에 맞춰 각 화면의 표시/숨김을 전환
    private void HandleStateChanged(GameSessionManager.State state)
    {
        bool preStart = state == GameSessionManager.State.PreStart;
        bool countdown = state == GameSessionManager.State.Countdown;
        bool running = state == GameSessionManager.State.Running;
        bool ended = state == GameSessionManager.State.Ended;

        _startScreen.style.display = preStart ? DisplayStyle.Flex : DisplayStyle.None;
        _countdownLabel.style.display = countdown ? DisplayStyle.Flex : DisplayStyle.None;
        _timerBar.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
        _giveUpRow.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
        _resultScreen.style.display = ended ? DisplayStyle.Flex : DisplayStyle.None;

        if (countdown && GameSessionManager.Instance != null)
            _countdownLabel.text = GameSessionManager.Instance.CountdownNumber.ToString();

        if (ended)
            _resultBody.text = BuildResultText();
    }

    // 결과창 본문: "얻은 조각  N" + 오브젝트별 내역 몇 줄
    private string BuildResultText()
    {
        var sb = new StringBuilder();

        long total = GameSessionManager.Instance != null ? GameSessionManager.Instance.GetTotalPiecesGainedThisRun() : 0;
        sb.Append("얻은 조각  ").Append(NumberFormatUtil.Format(total));

        if (GameSessionManager.Instance != null)
        {
            foreach (var entry in GameSessionManager.Instance.GetPiecesGainedThisRun())
            {
                if (entry.Value <= 0) continue;

                string objectName = ObjectManager.Instance != null
                    ? ObjectManager.Instance.GetObjectAt(entry.Key).objectName
                    : $"오브젝트 {entry.Key}";
                sb.Append('\n').Append(objectName).Append("  ").Append(NumberFormatUtil.Format(entry.Value));
            }
        }

        if (total <= 0)
            sb.Append("\n(없음)");

        return sb.ToString();
    }

    // 업그레이드 오버레이가 열리면(true) 이 문서 전체를 숨기고, 닫히면 다시 보여줌
    private void SetHiddenForUpgrade(bool upgradeOverlayOpen)
    {
        if (_root != null)
            _root.style.display = upgradeOverlayOpen ? DisplayStyle.None : DisplayStyle.Flex;
    }

    // 공통 버튼 모양 (SidePanelUI의 버튼 톤과 비슷하게). 클릭 핸들러는 호출부에서 .clicked += 로 붙임
    private Button MakeButton(string text)
    {
        var button = new Button { text = text };
        button.style.paddingLeft = 16;
        button.style.paddingRight = 16;
        button.style.paddingTop = 10;
        button.style.paddingBottom = 10;
        button.style.fontSize = 16;
        button.style.color = Color.white;
        button.style.backgroundColor = new Color(0.2f, 0.2f, 0.25f, 0.95f);
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        SetBorderRadius(button, 6);
        return button;
    }

    private static void SetBorderRadius(VisualElement element, float radius)
    {
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
    }
}
