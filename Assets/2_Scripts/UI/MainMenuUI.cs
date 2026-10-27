using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// 게임 진입 전 메인 메뉴(MenuScene 전용). 새 게임/불러오기/설정/나가기 4개 버튼.
// 새 게임/불러오기 버튼은 누르면 산산조각 나면서 게임이 시작되고, 설정의 음량은 유리 원통(GlassCylinderSlider)으로 조절함.
// 마우스 클릭은 UI 툴킷 Button이 기본으로 처리하고, 방향키+엔터/스페이스로도 똑같이 조작 가능하게 직접 처리함
// (이 프로젝트에 UI 툴킷 키보드 내비게이션을 쓰는 곳이 없어서, Keyboard.current를 직접 폴링하는 기존 Click.cs 방식을 따름).
[RequireComponent(typeof(UIDocument))]
public class MainMenuUI : MonoBehaviour
{
    private const string MainSceneName = "MainScene"; // 새 게임/불러오기가 이동할 씬 이름
    private const string SaveFileName = "savedata.json"; // SaveManager의 기본 저장 파일명과 반드시 같아야 "새 게임"이 실제로 지워짐
    private const string MasterVolumeKey = "MasterVolume"; // 마스터 음량을 저장할 PlayerPrefs 키
    private const float ShatterDuration = 0.6f; // 깨진 조각이 사라지는 시간(초)
    private const float StartDelay = 0.6f; // 버튼이 깨진 뒤 화면이 검게 덮이기 시작할 때까지 시간(초) - 조각이 거의 다 떨어질 때쯤
    private const int ShardRows = 3; // 버튼을 가로로 자르는 줄 수
    private const int ShardColumns = 6; // 한 줄을 세로로 자르는 조각 수
    private const float ShardGravity = 2600f; // 조각이 아래로 떨어지는 가속도(px/초²)

    private const float SelectedScale = 1.08f; // 선택된 버튼을 이만큼 키워서 표시
    private readonly Dictionary<Button, PixelStyle.Tone> _buttonTones = new Dictionary<Button, PixelStyle.Tone>(); // 버튼별 색 - 깨질 때 조각 색으로 씀

    // 메인 메뉴에서 방향키로 옮겨다닐 버튼들 (저장 파일이 없으면 "불러오기"는 목록에서 빠짐)
    private readonly List<Button> _menuButtons = new List<Button>();
    private readonly List<System.Action> _menuActions = new List<System.Action>(); // _menuButtons와 같은 순서 - Button.clicked는 밖에서 직접 실행할 방법이 없어서 액션을 따로 들고 있다가 엔터/스페이스 때 호출함
    private int _selectedIndex; // 지금 키보드로 선택된 메인 메뉴 버튼 인덱스

    private VisualElement _confirmDialog; // 새 게임/나가기 확인창 (평소엔 숨김)
    private Label _confirmMessage; // 확인창 안내 문구
    private Button _confirmYes; // 확인창 "예" 버튼
    private Button _confirmNo; // 확인창 "아니오" 버튼
    private int _confirmSelectedIndex; // 0=예, 1=아니오 - 방향키로 옮길 때 씀
    private System.Action _confirmPendingAction; // "예"를 눌렀을 때 실행할 동작 (새 게임 시작 / 게임 종료)

    private VisualElement _settingsPanel; // 설정 창 (평소엔 숨김)
    private readonly List<SettingItem> _settingItems = new List<SettingItem>(); // 설정창 항목들 (위아래 이동 순서)
    private int _settingsSelectedIndex; // 지금 선택된 설정 항목 번호
    private AudioSource _clickSource; // 버튼 클릭 소리 재생기

    // 날아가는 조각 하나 (깨진 버튼 조각 / 유리 원통에서 떨어진 조각 공용)
    private class Shard
    {
        public VisualElement element; // 조각 그림
        public Vector2 offset;        // 처음 자리에서 움직인 거리(px)
        public Vector2 velocity;      // 지금 속도(px/초)
        public float angle;           // 지금 회전 각도
        public float spin;            // 회전 속도(도/초)
        public float age;             // 생긴 뒤 흐른 시간(초)
    }

    private readonly List<Shard> _shards = new List<Shard>(); // 지금 날아가는 조각들
    private VisualElement _shardLayer; // 조각들이 올라가는 맨 위 층 (버튼이 깨지는 동안엔 클릭도 막음)
    private Vector2 _pointer; // 마지막 마우스 위치(패널 좌표) - 버튼을 누른 자리에서 조각이 퍼지게 하는 데 씀
    private System.Action _afterShatter; // 버튼이 다 깨진 뒤 실행할 동작 (게임 씬으로 이동)
    private float _shatterTimer; // 그 동작까지 남은 시간(초)

    // 지금 방향키/엔터 입력을 메인 메뉴/확인창/설정 중 어디로 보낼지
    private enum InputMode { Menu, Confirm, Settings, Starting } // Starting = 버튼이 깨지는 중 (입력 안 받음)
    private InputMode _mode = InputMode.Menu;

    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName); // SaveManager와 동일한 경로 계산

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
    }

    void Update()
    {
        UpdateShards();
        if (_mode == InputMode.Starting)
        {
            _shatterTimer -= Time.unscaledDeltaTime;
            if (_shatterTimer <= 0f && _afterShatter != null)
            {
                System.Action next = _afterShatter; // 한 번만 실행되게 비우고 호출
                _afterShatter = null;
                next();
            }
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        switch (_mode)
        {
            case InputMode.Menu: HandleMenuKeyboard(kb); break;
            case InputMode.Confirm: HandleConfirmKeyboard(kb); break;
            case InputMode.Settings: HandleSettingsKeyboard(kb); break;
        }
    }

    private void Build(VisualElement root)
    {
        root.Clear();
        _menuButtons.Clear();
        _menuActions.Clear();
        _shards.Clear();
        _buttonTones.Clear();
        _settingItems.Clear();
        root.RegisterCallback<PointerMoveEvent>(evt => _pointer = evt.position, TrickleDown.TrickleDown);
        GameFonts.Apply(root);
        if (GameFonts.UiColored != null)
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiColored); // 글자색이 먹는 도트 글꼴
        root.style.flexGrow = 1;
        PixelStyle.Frame(root, PixelStyle.Window, 8f); // 게임 화면 창과 같은 크림 바탕 + 갈색 테두리

        var title = new Label("BREAK THE WORLD"); // 타이틀 - 나중에 로고 이미지로 교체 가능
        title.style.position = Position.Absolute;
        title.style.top = Length.Percent(16);
        title.style.left = Length.Percent(50);
        title.style.translate = new Translate(Length.Percent(-50), 0); // 글자 길이만큼만 리본
        title.style.paddingLeft = 64;
        title.style.paddingRight = 64;
        title.style.paddingTop = 14;
        title.style.paddingBottom = 22;
        title.style.fontSize = 72;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        PixelStyle.Frame(title, PixelStyle.Ribbon, 8f); // 분홍 리본
        PixelStyle.OutlinedText(title, PixelStyle.Ribbon.text, 0.3f);
        root.Add(title);

        var menuColumn = new VisualElement(); // 버튼 4개가 세로로 쌓이는 가운데 정렬 칸
        menuColumn.style.position = Position.Absolute;
        menuColumn.style.left = 0;
        menuColumn.style.right = 0;
        menuColumn.style.top = Length.Percent(42);
        menuColumn.style.alignItems = Align.Center;
        root.Add(menuColumn);

        bool hasSave = File.Exists(SavePath); // 저장 파일이 없으면 불러오기를 눌러도 할 게 없으니 비활성화
        Button newGameButton = null; // 누르면 깨지면서 새로 시작하는 버튼
        Button loadButton = null; // 누르면 깨지면서 이어서 시작하는 버튼

        // 지울 저장 데이터가 있을 때만 먼저 물어보고, "예"를 누르면 그때 버튼이 깨짐
        System.Action newGameAction = () =>
        {
            if (hasSave)
                RequestConfirm("새 게임을 시작하시겠습니까?\n기존 저장 데이터가 사라집니다.", () => BreakButton(newGameButton, StartNewGame));
            else
                BreakButton(newGameButton, StartNewGame);
        };
        System.Action loadAction = () => BreakButton(loadButton, LoadGame);
        System.Action settingsAction = OpenSettings;
        System.Action quitAction = () =>
            RequestConfirm("게임을 종료하시겠습니까?", QuitGame);

        newGameAction = WithClick(newGameAction);
        loadAction = WithClick(loadAction);
        settingsAction = WithClick(settingsAction);
        quitAction = WithClick(quitAction);
        newGameButton = CreateMenuButton("새 게임", PixelStyle.Mint, newGameAction);
        loadButton = CreateMenuButton("불러오기", PixelStyle.Sky, loadAction);
        Button settingsButton = CreateMenuButton("설정", PixelStyle.Butter, settingsAction);
        Button quitButton = CreateMenuButton("나가기", PixelStyle.Pink, quitAction);

        if (!hasSave)
        {
            loadButton.SetEnabled(false);
            loadButton.style.opacity = 0.4f;
        }

        // 저장 파일이 있으면 불러오기를 맨 위에 둬서 기본 선택이 되게 함 (엔터 한 번이면 이어하기)
        if (hasSave)
        {
            menuColumn.Add(loadButton);
            menuColumn.Add(newGameButton);
            _menuButtons.Add(loadButton);
            _menuActions.Add(loadAction);
            _menuButtons.Add(newGameButton);
            _menuActions.Add(newGameAction);
        }
        else
        {
            menuColumn.Add(newGameButton);
            menuColumn.Add(loadButton); // 비활성 상태로 보이기만 하고 방향키 이동 대상에서는 제외
            _menuButtons.Add(newGameButton);
            _menuActions.Add(newGameAction);
        }
        menuColumn.Add(settingsButton);
        menuColumn.Add(quitButton);
        _menuButtons.Add(settingsButton);
        _menuActions.Add(settingsAction);
        _menuButtons.Add(quitButton);
        _menuActions.Add(quitAction);

        UpdateMenuSelectionVisual();

        _confirmDialog = BuildConfirmDialog();
        root.Add(_confirmDialog);

        _settingsPanel = BuildSettingsPanel();
        root.Add(_settingsPanel);

        _shardLayer = new VisualElement();
        _shardLayer.style.position = Position.Absolute;
        _shardLayer.style.left = 0;
        _shardLayer.style.right = 0;
        _shardLayer.style.top = 0;
        _shardLayer.style.bottom = 0;
        _shardLayer.pickingMode = PickingMode.Ignore; // 평소엔 클릭이 밑의 버튼으로 그대로 지나감
        root.Add(_shardLayer);
    }

    // 메인 메뉴용 큼직한 버튼 하나. 마우스 오버 시 키보드 선택 인덱스도 같이 옮겨서 둘이 어긋나지 않게 함
    private Button CreateMenuButton(string text, PixelStyle.Tone tone, System.Action onClick)
    {
        var button = new Button(onClick) { text = text };
        button.focusable = false; // UI 툴킷 기본 키보드 포커스/내비게이션을 꺼둠 - 켜져있으면 Enter/방향키가 여기(네이티브 포커스)와 밑의 수동 폴링(_selectedIndex) 두 군데서 따로 처리돼 서로 다른 버튼이 눌리는 사고가 남
        button.style.width = 360;
        button.style.height = 88;
        button.style.marginBottom = 18;
        button.style.fontSize = 36;
        PixelStyle.Button(button, tone); // 게임 화면과 같은 도트 입체 버튼
        _buttonTones[button] = tone;

        button.RegisterCallback<PointerEnterEvent>(_ =>
        {
            int idx = _menuButtons.IndexOf(button);
            if (idx < 0 || _mode != InputMode.Menu) return; // 확인창/설정이 열려있거나 버튼이 깨지는 동안은 뒤 메뉴에 반응 안 함
            _selectedIndex = idx;
            UpdateMenuSelectionVisual();
        });

        return button;
    }

    private void UpdateMenuSelectionVisual()
    {
        for (int i = 0; i < _menuButtons.Count; i++)
            SetSelected(_menuButtons[i], i == _selectedIndex);
    }

    private void HandleMenuKeyboard(Keyboard kb)
    {
        if (_menuButtons.Count == 0) return;

        if (kb.upArrowKey.wasPressedThisFrame)
        {
            _selectedIndex = (_selectedIndex - 1 + _menuButtons.Count) % _menuButtons.Count;
            UpdateMenuSelectionVisual();
        }
        else if (kb.downArrowKey.wasPressedThisFrame)
        {
            _selectedIndex = (_selectedIndex + 1) % _menuButtons.Count;
            UpdateMenuSelectionVisual();
        }
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            _menuActions[_selectedIndex]?.Invoke();
        }
    }

    // ---- 버튼 깨기 / 조각 ----

    // 버튼을 산산조각 내고, 조각이 다 사라지면 onBroken을 실행. 깨지는 동안엔 다른 입력을 안 받음
    private void BreakButton(Button button, System.Action onBroken)
    {
        if (_mode == InputMode.Starting) return;
        _mode = InputMode.Starting;
        _shardLayer.pickingMode = PickingMode.Position; // 깨지는 동안 다른 버튼이 눌리지 않게 맨 위 층이 클릭을 받아냄
        _afterShatter = onBroken;
        _shatterTimer = StartDelay;

        Rect bounds = _shardLayer.WorldToLocal(button.worldBound); // 조각 층 기준 버튼 자리
        // 조각이 퍼져 나가는 중심: 마우스로 눌렀으면 누른 자리, 키보드로 눌렀으면 버튼 가운데
        Vector2 origin = button.worldBound.Contains(_pointer) ? _shardLayer.WorldToLocal(_pointer) : bounds.center;
        float rowHeight = bounds.height / ShardRows; // 조각 한 줄 높이

        for (int row = 0; row < ShardRows; row++)
        {
            float x = 0f; // 이 줄에서 다음 조각이 시작하는 가로 위치
            for (int column = 0; column < ShardColumns; column++)
            {
                float average = (bounds.width - x) / (ShardColumns - column); // 남은 폭을 남은 조각 수로 나눈 평균 폭
                float width = column == ShardColumns - 1 ? bounds.width - x : average * Random.Range(0.6f, 1.4f); // 조각마다 폭을 다르게 (마지막 조각은 남은 폭 전부)
                var piece = new Rect(bounds.x + x, bounds.y + row * rowHeight, width, rowHeight); // 이 조각 자리
                Vector2 direction = (piece.center - origin).normalized; // 누른 자리에서 바깥으로
                Vector2 velocity = direction * Random.Range(250f, 700f) + new Vector2(0f, -300f); // 살짝 위로 튀었다가 떨어짐
                VisualElement element = SpawnShard(piece, _buttonTones.TryGetValue(button, out PixelStyle.Tone tone) ? tone.face.linear : Color.white, velocity); // 버튼 색 조각

                // 조각 안에 버튼 글자를 통째로 넣고 조각 밖은 잘라내서, 글자도 같이 쪼개진 것처럼 보이게 함
                var face = new Label(button.text);
                face.style.position = Position.Absolute;
                face.style.left = -x;
                face.style.top = -row * rowHeight;
                face.style.width = bounds.width;
                face.style.height = bounds.height;
                face.style.marginTop = face.style.marginBottom = face.style.marginLeft = face.style.marginRight = 0;
                face.style.paddingTop = face.style.paddingBottom = face.style.paddingLeft = face.style.paddingRight = 0;
                face.style.fontSize = button.resolvedStyle.fontSize;
                face.style.unityFontStyleAndWeight = FontStyle.Bold;
                face.style.color = Color.white;
                face.style.unityTextAlign = TextAnchor.MiddleCenter;
                element.Add(face);

                x += width;
            }
        }

        button.style.visibility = Visibility.Hidden; // 자리는 그대로 두고 안 보이게만 (밑의 버튼이 밀려 올라오지 않게)
    }

    // 유리 원통이 깨져 나간 자리에서 작은 유리 조각 몇 개가 떨어짐
    private void SpawnGlassChips(Vector2 panelPosition)
    {
        Vector2 center = _shardLayer.WorldToLocal(panelPosition); // 조각 층 기준 깨진 자리
        for (int i = 0; i < 3; i++)
        {
            float size = Random.Range(5f, 10f); // 조각 한 변 길이(px)
            var piece = new Rect(center.x + Random.Range(-22f, 22f), center.y - size, size, size); // 이 조각 자리
            SpawnShard(piece, GlassCylinderSlider.ChipColor, new Vector2(Random.Range(-140f, 140f), Random.Range(-320f, -80f)));
        }
    }

    private VisualElement SpawnShard(Rect rect, Color color, Vector2 velocity)
    {
        var element = new VisualElement(); // 조각 그림
        element.pickingMode = PickingMode.Ignore;
        element.style.position = Position.Absolute;
        element.style.left = rect.x;
        element.style.top = rect.y;
        element.style.width = rect.width;
        element.style.height = rect.height;
        element.style.backgroundColor = color;
        element.style.overflow = Overflow.Hidden;
        _shardLayer.Add(element);
        _shards.Add(new Shard { element = element, velocity = velocity, spin = Random.Range(-540f, 540f) });
        return element;
    }

    private void UpdateShards()
    {
        float deltaTime = Time.unscaledDeltaTime; // 이번 프레임 시간
        for (int i = _shards.Count - 1; i >= 0; i--)
        {
            Shard shard = _shards[i]; // 이번에 움직일 조각
            shard.age += deltaTime;
            float progress = shard.age / ShatterDuration; // 0~1 진행률
            if (progress >= 1f)
            {
                shard.element.RemoveFromHierarchy();
                _shards.RemoveAt(i);
                continue;
            }

            shard.velocity.y += ShardGravity * deltaTime;
            shard.offset += shard.velocity * deltaTime;
            shard.angle += shard.spin * deltaTime;
            shard.element.style.translate = new Translate(shard.offset.x, shard.offset.y);
            shard.element.style.rotate = new Rotate(shard.angle);
            shard.element.style.opacity = 1f - progress * progress; // 끝으로 갈수록 빠르게 흐려짐
        }
    }

    // ---- 새 게임 / 불러오기 / 설정 / 나가기 동작 ----

    private void StartNewGame()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath); // 파일이 없으면 SaveManager.Load()가 그냥 기본값으로 시작함 (기존 동작 그대로 재사용)

        SceneFader.FadeToScene(MainSceneName);
    }

    private void LoadGame()
    {
        SceneFader.FadeToScene(MainSceneName);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Application.Quit()이 아무 일도 안 해서 따로 처리
#else
        Application.Quit();
#endif
    }

    // ---- 확인창 (새 게임 / 나가기 공용) ----

    private VisualElement BuildConfirmDialog()
    {
        var overlay = new VisualElement(); // 화면 전체를 반투명 검정으로 덮어 뒤 메뉴를 흐리게 함
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.right = 0;
        overlay.style.top = 0;
        overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.display = DisplayStyle.None;
        // 확인창 밖(반투명 배경)을 누르면 "아니오"처럼 닫힘
        overlay.RegisterCallback<ClickEvent>(evt => { if (evt.target == overlay) CloseConfirm(); });

        var box = new VisualElement();
        box.style.width = 560;
        box.style.paddingTop = 32;
        box.style.paddingBottom = 32;
        box.style.paddingLeft = 32;
        box.style.paddingRight = 32;
        box.style.alignItems = Align.Center;
        PixelStyle.Frame(box, PixelStyle.Window, 6f); // 크림 픽셀 창

        _confirmMessage = new Label();
        _confirmMessage.style.fontSize = 36;
        _confirmMessage.style.color = PixelStyle.Text.linear;
        _confirmMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
        _confirmMessage.style.whiteSpace = WhiteSpace.Normal;
        _confirmMessage.style.marginBottom = 28;
        box.Add(_confirmMessage);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;

        _confirmYes = new Button(() => { PlayClick(); CloseConfirm(); _confirmPendingAction?.Invoke(); }) { text = "예" };
        _confirmNo = new Button(() => { PlayClick(); CloseConfirm(); }) { text = "아니오" };
        StyleDialogButton(_confirmYes, PixelStyle.Mint);
        StyleDialogButton(_confirmNo, PixelStyle.Pink);
        _confirmYes.style.marginRight = 20;

        _confirmYes.RegisterCallback<PointerEnterEvent>(_ => { _confirmSelectedIndex = 0; UpdateConfirmSelectionVisual(); });
        _confirmNo.RegisterCallback<PointerEnterEvent>(_ => { _confirmSelectedIndex = 1; UpdateConfirmSelectionVisual(); });

        row.Add(_confirmYes);
        row.Add(_confirmNo);
        box.Add(row);
        overlay.Add(box);

        return overlay;
    }

    private void RequestConfirm(string message, System.Action onYes)
    {
        _confirmMessage.text = message;
        _confirmPendingAction = onYes;
        _confirmSelectedIndex = 1; // 기본 선택은 "아니오" - 새 게임/종료처럼 되돌릴 수 없는 동작이 엔터 연타로 바로 확정되지 않게
        UpdateConfirmSelectionVisual();
        _confirmDialog.style.display = DisplayStyle.Flex;
        _mode = InputMode.Confirm;
    }

    private void CloseConfirm()
    {
        _confirmDialog.style.display = DisplayStyle.None;
        _mode = InputMode.Menu;
    }

    private void UpdateConfirmSelectionVisual()
    {
        SetSelected(_confirmYes, _confirmSelectedIndex == 0);
        SetSelected(_confirmNo, _confirmSelectedIndex == 1);
    }

    private void HandleConfirmKeyboard(Keyboard kb)
    {
        if (kb.escapeKey.wasPressedThisFrame)
            CloseConfirm();
        else if (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
        {
            _confirmSelectedIndex = 1 - _confirmSelectedIndex;
            UpdateConfirmSelectionVisual();
        }
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            PlayClick();
            if (_confirmSelectedIndex == 0) { CloseConfirm(); _confirmPendingAction?.Invoke(); }
            else CloseConfirm();
        }
    }

    // ---- 설정 패널 ----

    // 설정창에서 위아래로 옮겨 다니는 항목 하나 (유리관 / 선택 줄 / 뒤로가기)
    private class SettingItem
    {
        public VisualElement element; // 선택됐을 때 커지는 대상
        public System.Action<int> onStep; // 좌우 방향키 (-1/+1)
        public System.Action onEnter; // 엔터/스페이스
    }

    private VisualElement BuildSettingsPanel()
    {
        // 확인창처럼 반투명 배경 위 가운데 창 - 창 밖(배경)을 누르면 닫힘
        var panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.left = 0;
        panel.style.right = 0;
        panel.style.top = 0;
        panel.style.bottom = 0;
        panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        panel.style.alignItems = Align.Center;
        panel.style.justifyContent = Justify.Center;
        panel.style.display = DisplayStyle.None;
        panel.RegisterCallback<ClickEvent>(evt => { if (evt.target == panel) CloseSettings(); });

        var window = new VisualElement(); // 설정 창 본체
        window.style.paddingTop = 30;
        window.style.paddingBottom = 26;
        window.style.paddingLeft = 48;
        window.style.paddingRight = 48;
        window.style.alignItems = Align.Center;
        PixelStyle.Frame(window, PixelStyle.Window, 6f); // 크림 픽셀 창
        panel.Add(window);

        var title = new Label("설정"); // 분홍 리본 제목
        title.style.fontSize = 36;
        title.style.paddingLeft = 48;
        title.style.paddingRight = 48;
        title.style.paddingTop = 10;
        title.style.paddingBottom = 14;
        title.style.marginBottom = 24;
        PixelStyle.Frame(title, PixelStyle.Ribbon);
        PixelStyle.OutlinedText(title, PixelStyle.Ribbon.text);
        window.Add(title);

        var columns = new VisualElement(); // 왼쪽 소리 / 오른쪽 화면·게임
        columns.style.flexDirection = FlexDirection.Row;
        columns.style.alignItems = Align.FlexStart;
        window.Add(columns);

        var sound = new VisualElement(); // 소리 칸
        sound.style.alignItems = Align.Center;
        sound.style.marginRight = 56;
        sound.Add(PixelStyle.CardHeader("소리"));
        columns.Add(sound);

        float masterVolume = GameSettings.MasterVolume; // 저장된 마스터 음량
        AudioListener.volume = masterVolume; // 시작할 때 저장된 값으로 실제 음량도 맞춤
        AddVolumeCylinder(sound, "전체 음량", masterVolume, value =>
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
        });
        AddVolumeCylinder(sound, "효과음", SfxPlayer.Volume, value => SfxPlayer.Volume = value);
        AddVolumeCylinder(sound, "배경음악", GameSettings.BgmVolume, value => GameSettings.BgmVolume = value);

        var options = new VisualElement(); // 화면/게임 칸
        options.style.alignItems = Align.Center;
        columns.Add(options);

        options.Add(PixelStyle.CardHeader("화면"));
        AddOptionRow(options, "화면 모드", () => GameSettings.Fullscreen ? "전체화면" : "창 모드",
            _ => GameSettings.Fullscreen = !GameSettings.Fullscreen);
        AddOptionRow(options, "해상도", () => { Vector2Int r = GameSettings.Resolution; return $"{r.x}x{r.y}"; }, step =>
        {
            List<Vector2Int> list = GameSettings.Resolutions; // 고를 수 있는 해상도
            if (list.Count == 0) return;
            int index = Mathf.Max(0, list.IndexOf(GameSettings.Resolution)); // 지금 해상도 번호
            GameSettings.Resolution = list[(index + step + list.Count) % list.Count];
        });
        AddOptionRow(options, "프레임 제한", () =>
        {
            int fps = GameSettings.FpsOptions[GameSettings.FpsIndex]; // 제한 값
            return fps < 0 ? "무제한" : $"{fps} FPS";
        }, step => GameSettings.FpsIndex = (GameSettings.FpsIndex + step + GameSettings.FpsOptions.Length) % GameSettings.FpsOptions.Length);

        var gameHeader = PixelStyle.CardHeader("게임"); // 게임 항목 머리글
        gameHeader.style.marginTop = 10;
        options.Add(gameHeader);
        AddOptionRow(options, "화면 흔들림", () => GameSettings.ScreenShake ? "켜기" : "끄기",
            _ => GameSettings.ScreenShake = !GameSettings.ScreenShake);
        AddOptionRow(options, "데미지 숫자", () => GameSettings.DamageNumbers ? "켜기" : "끄기",
            _ => GameSettings.DamageNumbers = !GameSettings.DamageNumbers);
        AddOptionRow(options, "숫자 표시", () => GameSettings.NumberUnits ? "영어 단위" : "숫자",
            _ => GameSettings.NumberUnits = !GameSettings.NumberUnits);

        Button back = CreateMenuButton("뒤로가기", PixelStyle.Pink, WithClick(CloseSettings)); // 메인 메뉴로
        back.style.marginTop = 16;
        back.style.marginBottom = 0;
        window.Add(back);
        _settingItems.Add(new SettingItem { element = back, onEnter = () => { PlayClick(); CloseSettings(); } });
        back.RegisterCallback<PointerEnterEvent>(_ => SelectSetting(_settingItems.Count - 1));

        return panel;
    }

    // "이름 퍼센트" 한 줄 + 그 밑에 어두운 홈 안의 가로 유리관
    private void AddVolumeCylinder(VisualElement column, string label, float value, System.Action<float> onChanged)
    {
        var caption = new Label(); // 유리관 위 "이름 퍼센트" 글자
        caption.style.fontSize = 36;
        caption.style.color = PixelStyle.Text.linear;
        caption.style.marginBottom = 6;
        column.Add(caption);

        var cylinder = new GlassCylinderSlider(value, newValue =>
        {
            onChanged(newValue);
            caption.text = label + "  " + Mathf.RoundToInt(newValue * 100f) + "%";
        }, SpawnGlassChips);
        caption.text = label + "  " + Mathf.RoundToInt(cylinder.Value * 100f) + "%";

        var wrap = new VisualElement(); // 유리관이 놓이는 어두운 홈
        wrap.style.paddingTop = 14;
        wrap.style.paddingBottom = 18;
        wrap.style.paddingLeft = 18;
        wrap.style.paddingRight = 18;
        wrap.style.marginBottom = 22;
        PixelStyle.Frame(wrap, PixelStyle.Slot);
        wrap.Add(cylinder);
        column.Add(wrap);

        int index = _settingItems.Count; // 이 유리관의 선택 번호
        wrap.RegisterCallback<PointerEnterEvent>(_ => SelectSetting(index));
        _settingItems.Add(new SettingItem
        {
            element = wrap,
            onStep = step => cylinder.SetValue(cylinder.Value + step * GlassCylinderSlider.Step), // 오른쪽 = 올리기, 왼쪽 = 내리기(유리가 깨져 나감)
        });
    }

    // "이름  < 값 >" 한 줄 - 화살표나 좌우 방향키로 값을 바꿈 (바로 저장/적용)
    private void AddOptionRow(VisualElement column, string label, System.Func<string> getValue, System.Action<int> step)
    {
        var row = new VisualElement(); // 한 줄
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.width = 680;
        row.style.paddingLeft = 20;
        row.style.paddingRight = 12;
        row.style.paddingTop = 8;
        row.style.paddingBottom = 12;
        row.style.marginBottom = 12;
        PixelStyle.Frame(row, PixelStyle.Card);

        var name = new Label(label); // 항목 이름
        name.style.flexGrow = 1;
        name.style.fontSize = 36;
        name.style.color = PixelStyle.Text.linear;
        row.Add(name);

        var value = new Label(getValue()); // 지금 값
        System.Action<int> change = delta => { step(delta); value.text = getValue(); }; // 값 바꾸고 다시 표시
        Button left = CreateArrowButton("<", () => { PlayClick(); change(-1); }); // 이전 값
        Button right = CreateArrowButton(">", () => { PlayClick(); change(1); }); // 다음 값
        value.style.width = 230;
        value.style.fontSize = 36;
        value.style.color = PixelStyle.Text.linear;
        value.style.unityTextAlign = TextAnchor.MiddleCenter;
        row.Add(left);
        row.Add(value);
        row.Add(right);
        column.Add(row);

        int index = _settingItems.Count; // 이 줄의 선택 번호
        row.RegisterCallback<PointerEnterEvent>(_ => SelectSetting(index));
        _settingItems.Add(new SettingItem { element = row, onStep = d => { PlayClick(); change(d); }, onEnter = () => { PlayClick(); change(1); } });
    }

    private Button CreateArrowButton(string text, System.Action onClick)
    {
        var button = new Button(onClick) { text = text };
        button.focusable = false;
        button.style.width = 60;
        button.style.height = 60;
        button.style.fontSize = 36;
        PixelStyle.Button(button, PixelStyle.Mint);
        return button;
    }

    private void OpenSettings()
    {
        SelectSetting(0);
        _settingsPanel.style.display = DisplayStyle.Flex;
        _mode = InputMode.Settings;
    }

    private void CloseSettings()
    {
        _settingsPanel.style.display = DisplayStyle.None;
        _mode = InputMode.Menu;
        PlayerPrefs.Save(); // 바꾼 설정을 바로 디스크에
    }

    private void SelectSetting(int index)
    {
        _settingsSelectedIndex = index;
        for (int i = 0; i < _settingItems.Count; i++)
            SetSelected(_settingItems[i].element, i == _settingsSelectedIndex);
    }

    // 위아래 = 항목 이동, 좌우 = 선택된 항목 값 바꾸기, 엔터 = 선택(뒤로가기/값 넘기기)
    private void HandleSettingsKeyboard(Keyboard kb)
    {
        int count = _settingItems.Count; // 항목 수
        if (count == 0) return;
        SettingItem item = _settingItems[_settingsSelectedIndex]; // 지금 선택된 항목

        if (kb.escapeKey.wasPressedThisFrame)
            CloseSettings();
        else if (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            SelectSetting((_settingsSelectedIndex + (kb.downArrowKey.wasPressedThisFrame ? 1 : -1) + count) % count);
        else if (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            item.onStep?.Invoke(kb.rightArrowKey.wasPressedThisFrame ? 1 : -1);
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            item.onEnter?.Invoke();
    }

    // ---- 공용 스타일 헬퍼 ----

    // 버튼 클릭 소리 (효과음 음량 따름)
    private void PlayClick()
    {
        if (_clickSource == null) _clickSource = gameObject.AddComponent<AudioSource>();
        AudioClip clip = GameAudio.Instance != null ? GameAudio.Instance.click : null; // 클릭 소리
        SfxPlayer.Play(_clickSource, clip, priority: true);
    }

    private System.Action WithClick(System.Action action) => () => { PlayClick(); action?.Invoke(); };

    private void StyleDialogButton(Button button, PixelStyle.Tone tone)
    {
        button.focusable = false; // 메인 메뉴 버튼과 같은 이유로 네이티브 포커스/내비게이션 꺼둠
        button.style.width = 180;
        button.style.height = 72;
        button.style.fontSize = 36;
        PixelStyle.Button(button, tone); // 도트 입체 버튼
        _buttonTones[button] = tone;
    }

    // 키보드/마우스로 선택된 버튼은 살짝 크게
    private static void SetSelected(VisualElement button, bool selected)
    {
        float scale = selected ? SelectedScale : 1f; // 크기 배율
        button.style.scale = new Scale(new Vector3(scale, scale, 1f));
    }

    private void SetBorderWidth(VisualElement el, float width)
    {
        el.style.borderTopWidth = width;
        el.style.borderBottomWidth = width;
        el.style.borderLeftWidth = width;
        el.style.borderRightWidth = width;
    }

    private void SetBorderColor(VisualElement el, Color color)
    {
        el.style.borderTopColor = color;
        el.style.borderBottomColor = color;
        el.style.borderLeftColor = color;
        el.style.borderRightColor = color;
    }

    private void SetBorderRadius(VisualElement el, float radius)
    {
        el.style.borderTopLeftRadius = radius;
        el.style.borderTopRightRadius = radius;
        el.style.borderBottomLeftRadius = radius;
        el.style.borderBottomRightRadius = radius;
    }
}
