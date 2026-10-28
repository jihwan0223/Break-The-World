using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// 방향키 + 엔터/스페이스는 Keyboard.current를 직접 폴링
[RequireComponent(typeof(UIDocument))]
public class MainMenuUI : MonoBehaviour
{
    private const string MainSceneName = "MainScene";
    private const string SaveFileName = "savedata.json"; // SaveManager의 저장 파일명과 같아야 함
    private const string MasterVolumeKey = "MasterVolume";
    private const float ShatterDuration = 0.6f; // 초
    private const float StartDelay = 0.6f; // 초
    private const int ShardRows = 3;
    private const int ShardColumns = 6;
    private const float ShardGravity = 2600f; // px/초²

    private const float SelectedScale = 1.08f;
    private readonly Dictionary<Button, PixelStyle.Tone> _buttonTones = new Dictionary<Button, PixelStyle.Tone>();

    private readonly List<Button> _menuButtons = new List<Button>();
    private readonly List<System.Action> _menuActions = new List<System.Action>(); // _menuButtons와 같은 순서
    private int _selectedIndex;

    private VisualElement _confirmDialog;
    private Label _confirmMessage;
    private Button _confirmYes;
    private Button _confirmNo;
    private int _confirmSelectedIndex; // 0 = 예, 1 = 아니오
    private System.Action _confirmPendingAction;

    private VisualElement _settingsPanel;
    private readonly List<SettingItem> _settingItems = new List<SettingItem>();
    private int _settingsSelectedIndex;
    private AudioSource _clickSource;

    private class Shard
    {
        public VisualElement element;
        public Vector2 offset; // px
        public Vector2 velocity; // px/초
        public float angle;
        public float spin; // 도/초
        public float age; // 초
    }

    private readonly List<Shard> _shards = new List<Shard>();
    private VisualElement _shardLayer;
    private Vector2 _pointer;
    private System.Action _afterShatter;
    private float _shatterTimer;

    private enum InputMode { Menu, Confirm, Settings, Starting }
    private InputMode _mode = InputMode.Menu;

    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

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
                System.Action next = _afterShatter;
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
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiColored);
        root.style.flexGrow = 1;
        PixelStyle.Frame(root, PixelStyle.Window, 8f);

        var title = new Label("BREAK THE WORLD");
        title.style.position = Position.Absolute;
        title.style.top = Length.Percent(16);
        title.style.left = Length.Percent(50);
        title.style.translate = new Translate(Length.Percent(-50), 0);
        title.style.paddingLeft = 64;
        title.style.paddingRight = 64;
        title.style.paddingTop = 14;
        title.style.paddingBottom = 22;
        title.style.fontSize = 72;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        PixelStyle.Frame(title, PixelStyle.Ribbon, 8f);
        PixelStyle.OutlinedText(title, PixelStyle.Ribbon.text, 0.3f);
        root.Add(title);

        var menuColumn = new VisualElement();
        menuColumn.style.position = Position.Absolute;
        menuColumn.style.left = 0;
        menuColumn.style.right = 0;
        menuColumn.style.top = Length.Percent(42);
        menuColumn.style.alignItems = Align.Center;
        root.Add(menuColumn);

        bool hasSave = File.Exists(SavePath);
        Button newGameButton = null;
        Button loadButton = null;

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
            menuColumn.Add(loadButton);
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
        _shardLayer.pickingMode = PickingMode.Ignore;
        root.Add(_shardLayer);
    }

    private Button CreateMenuButton(string text, PixelStyle.Tone tone, System.Action onClick)
    {
        var button = new Button(onClick) { text = text };
        button.focusable = false; // 켜두면 Enter/방향키가 직접 폴링과 이중으로 처리됨
        button.style.width = 360;
        button.style.height = 88;
        button.style.marginBottom = 18;
        button.style.fontSize = 36;
        PixelStyle.Button(button, tone);
        _buttonTones[button] = tone;

        button.RegisterCallback<PointerEnterEvent>(_ =>
        {
            int idx = _menuButtons.IndexOf(button);
            if (idx < 0 || _mode != InputMode.Menu) return;
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

    private void BreakButton(Button button, System.Action onBroken)
    {
        if (_mode == InputMode.Starting) return;
        _mode = InputMode.Starting;
        _shardLayer.pickingMode = PickingMode.Position;
        _afterShatter = onBroken;
        _shatterTimer = StartDelay;

        Rect bounds = _shardLayer.WorldToLocal(button.worldBound);
        Vector2 origin = button.worldBound.Contains(_pointer) ? _shardLayer.WorldToLocal(_pointer) : bounds.center;
        float rowHeight = bounds.height / ShardRows;

        for (int row = 0; row < ShardRows; row++)
        {
            float x = 0f;
            for (int column = 0; column < ShardColumns; column++)
            {
                float average = (bounds.width - x) / (ShardColumns - column);
                float width = column == ShardColumns - 1 ? bounds.width - x : average * Random.Range(0.6f, 1.4f);
                var piece = new Rect(bounds.x + x, bounds.y + row * rowHeight, width, rowHeight);
                Vector2 direction = (piece.center - origin).normalized;
                Vector2 velocity = direction * Random.Range(250f, 700f) + new Vector2(0f, -300f);
                VisualElement element = SpawnShard(piece, _buttonTones.TryGetValue(button, out PixelStyle.Tone tone) ? tone.face.linear : Color.white, velocity);

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

        button.style.visibility = Visibility.Hidden; // 자리는 유지 - 밑의 버튼이 밀려 올라오지 않게
    }

    private void SpawnGlassChips(Vector2 panelPosition)
    {
        Vector2 center = _shardLayer.WorldToLocal(panelPosition);
        for (int i = 0; i < 3; i++)
        {
            float size = Random.Range(5f, 10f); // px
            var piece = new Rect(center.x + Random.Range(-22f, 22f), center.y - size, size, size);
            SpawnShard(piece, GlassCylinderSlider.ChipColor, new Vector2(Random.Range(-140f, 140f), Random.Range(-320f, -80f)));
        }
    }

    private VisualElement SpawnShard(Rect rect, Color color, Vector2 velocity)
    {
        var element = new VisualElement();
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
        float deltaTime = Time.unscaledDeltaTime;
        for (int i = _shards.Count - 1; i >= 0; i--)
        {
            Shard shard = _shards[i];
            shard.age += deltaTime;
            float progress = shard.age / ShatterDuration;
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
            shard.element.style.opacity = 1f - progress * progress;
        }
    }

    private void StartNewGame()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath); // 파일이 없으면 SaveManager가 기본값으로 시작

        SceneFader.FadeToScene(MainSceneName);
    }

    private void LoadGame()
    {
        SceneFader.FadeToScene(MainSceneName);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에선 Application.Quit이 동작 안 함
#else
        Application.Quit();
#endif
    }

    private VisualElement BuildConfirmDialog()
    {
        var overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.right = 0;
        overlay.style.top = 0;
        overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.display = DisplayStyle.None;
        overlay.RegisterCallback<ClickEvent>(evt => { if (evt.target == overlay) CloseConfirm(); });

        var box = new VisualElement();
        box.style.width = 560;
        box.style.paddingTop = 32;
        box.style.paddingBottom = 32;
        box.style.paddingLeft = 32;
        box.style.paddingRight = 32;
        box.style.alignItems = Align.Center;
        PixelStyle.Frame(box, PixelStyle.Window, 6f);

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
        _confirmSelectedIndex = 1; // 기본은 아니오 - 엔터 연타로 되돌릴 수 없는 동작이 실행되지 않게
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

    private class SettingItem
    {
        public VisualElement element;
        public System.Action<int> onStep;
        public System.Action onEnter;
    }

    private VisualElement BuildSettingsPanel()
    {
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

        var window = new VisualElement();
        window.style.paddingTop = 30;
        window.style.paddingBottom = 26;
        window.style.paddingLeft = 48;
        window.style.paddingRight = 48;
        window.style.alignItems = Align.Center;
        PixelStyle.Frame(window, PixelStyle.Window, 6f);
        panel.Add(window);

        var title = new Label("설정");
        title.style.fontSize = 36;
        title.style.paddingLeft = 48;
        title.style.paddingRight = 48;
        title.style.paddingTop = 10;
        title.style.paddingBottom = 14;
        title.style.marginBottom = 24;
        PixelStyle.Frame(title, PixelStyle.Ribbon);
        PixelStyle.OutlinedText(title, PixelStyle.Ribbon.text);
        window.Add(title);

        var columns = new VisualElement();
        columns.style.flexDirection = FlexDirection.Row;
        columns.style.alignItems = Align.FlexStart;
        window.Add(columns);

        var sound = new VisualElement();
        sound.style.alignItems = Align.Center;
        sound.style.marginRight = 56;
        sound.Add(PixelStyle.CardHeader("소리"));
        columns.Add(sound);

        float masterVolume = GameSettings.MasterVolume;
        AudioListener.volume = masterVolume;
        AddVolumeCylinder(sound, "전체 음량", masterVolume, value =>
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
        });
        AddVolumeCylinder(sound, "효과음", SfxPlayer.Volume, value => SfxPlayer.Volume = value);
        AddVolumeCylinder(sound, "배경음악", GameSettings.BgmVolume, value => GameSettings.BgmVolume = value);

        var options = new VisualElement();
        options.style.alignItems = Align.Center;
        columns.Add(options);

        options.Add(PixelStyle.CardHeader("화면"));
        AddOptionRow(options, "화면 모드", () => GameSettings.Fullscreen ? "전체화면" : "창 모드",
            _ => GameSettings.Fullscreen = !GameSettings.Fullscreen);
        AddOptionRow(options, "해상도", () => { Vector2Int r = GameSettings.Resolution; return $"{r.x}x{r.y}"; }, step =>
        {
            List<Vector2Int> list = GameSettings.Resolutions;
            if (list.Count == 0) return;
            int index = Mathf.Max(0, list.IndexOf(GameSettings.Resolution));
            GameSettings.Resolution = list[(index + step + list.Count) % list.Count];
        });
        AddOptionRow(options, "프레임 제한", () =>
        {
            int fps = GameSettings.FpsOptions[GameSettings.FpsIndex];
            return fps < 0 ? "무제한" : $"{fps} FPS";
        }, step => GameSettings.FpsIndex = (GameSettings.FpsIndex + step + GameSettings.FpsOptions.Length) % GameSettings.FpsOptions.Length);

        var gameHeader = PixelStyle.CardHeader("게임");
        gameHeader.style.marginTop = 10;
        options.Add(gameHeader);
        AddOptionRow(options, "화면 흔들림", () => GameSettings.ScreenShake ? "켜기" : "끄기",
            _ => GameSettings.ScreenShake = !GameSettings.ScreenShake);
        AddOptionRow(options, "데미지 숫자", () => GameSettings.DamageNumbers ? "켜기" : "끄기",
            _ => GameSettings.DamageNumbers = !GameSettings.DamageNumbers);
        AddOptionRow(options, "숫자 표시", () => GameSettings.NumberUnits ? "영어 단위" : "숫자",
            _ => GameSettings.NumberUnits = !GameSettings.NumberUnits);

        Button back = CreateMenuButton("뒤로가기", PixelStyle.Pink, WithClick(CloseSettings));
        back.style.marginTop = 16;
        back.style.marginBottom = 0;
        window.Add(back);
        _settingItems.Add(new SettingItem { element = back, onEnter = () => { PlayClick(); CloseSettings(); } });
        back.RegisterCallback<PointerEnterEvent>(_ => SelectSetting(_settingItems.Count - 1));

        return panel;
    }

    private void AddVolumeCylinder(VisualElement column, string label, float value, System.Action<float> onChanged)
    {
        var caption = new Label();
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

        var wrap = new VisualElement();
        wrap.style.paddingTop = 14;
        wrap.style.paddingBottom = 18;
        wrap.style.paddingLeft = 18;
        wrap.style.paddingRight = 18;
        wrap.style.marginBottom = 22;
        PixelStyle.Frame(wrap, PixelStyle.Slot);
        wrap.Add(cylinder);
        column.Add(wrap);

        int index = _settingItems.Count;
        wrap.RegisterCallback<PointerEnterEvent>(_ => SelectSetting(index));
        _settingItems.Add(new SettingItem
        {
            element = wrap,
            onStep = step => cylinder.SetValue(cylinder.Value + step * GlassCylinderSlider.Step),
        });
    }

    private void AddOptionRow(VisualElement column, string label, System.Func<string> getValue, System.Action<int> step)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.width = 680;
        row.style.paddingLeft = 20;
        row.style.paddingRight = 12;
        row.style.paddingTop = 8;
        row.style.paddingBottom = 12;
        row.style.marginBottom = 12;
        PixelStyle.Frame(row, PixelStyle.Card);

        var name = new Label(label);
        name.style.flexGrow = 1;
        name.style.fontSize = 36;
        name.style.color = PixelStyle.Text.linear;
        row.Add(name);

        var value = new Label(getValue());
        System.Action<int> change = delta => { step(delta); value.text = getValue(); };
        Button left = CreateArrowButton("<", () => { PlayClick(); change(-1); });
        Button right = CreateArrowButton(">", () => { PlayClick(); change(1); });
        value.style.width = 230;
        value.style.fontSize = 36;
        value.style.color = PixelStyle.Text.linear;
        value.style.unityTextAlign = TextAnchor.MiddleCenter;
        row.Add(left);
        row.Add(value);
        row.Add(right);
        column.Add(row);

        int index = _settingItems.Count;
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
        PlayerPrefs.Save();
    }

    private void SelectSetting(int index)
    {
        _settingsSelectedIndex = index;
        for (int i = 0; i < _settingItems.Count; i++)
            SetSelected(_settingItems[i].element, i == _settingsSelectedIndex);
    }

    private void HandleSettingsKeyboard(Keyboard kb)
    {
        int count = _settingItems.Count;
        if (count == 0) return;
        SettingItem item = _settingItems[_settingsSelectedIndex];

        if (kb.escapeKey.wasPressedThisFrame)
            CloseSettings();
        else if (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            SelectSetting((_settingsSelectedIndex + (kb.downArrowKey.wasPressedThisFrame ? 1 : -1) + count) % count);
        else if (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            item.onStep?.Invoke(kb.rightArrowKey.wasPressedThisFrame ? 1 : -1);
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            item.onEnter?.Invoke();
    }

    private void PlayClick()
    {
        if (_clickSource == null) _clickSource = gameObject.AddComponent<AudioSource>();
        AudioClip clip = GameAudio.Instance != null ? GameAudio.Instance.click : null;
        SfxPlayer.Play(_clickSource, clip, priority: true);
    }

    private System.Action WithClick(System.Action action) => () => { PlayClick(); action?.Invoke(); };

    private void StyleDialogButton(Button button, PixelStyle.Tone tone)
    {
        button.focusable = false; // 메인 메뉴 버튼과 같은 이유로 포커스 끔
        button.style.width = 180;
        button.style.height = 72;
        button.style.fontSize = 36;
        PixelStyle.Button(button, tone);
        _buttonTones[button] = tone;
    }

    private static void SetSelected(VisualElement button, bool selected)
    {
        float scale = selected ? SelectedScale : 1f;
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
