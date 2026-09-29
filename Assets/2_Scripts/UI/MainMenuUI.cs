using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// 게임 진입 전 메인 메뉴(MenuScene 전용). 새 게임/불러오기/설정/나가기 4개 버튼.
// 마우스 클릭은 UI 툴킷 Button이 기본으로 처리하고, 방향키+엔터/스페이스로도 똑같이 조작 가능하게 직접 처리함
// (이 프로젝트에 UI 툴킷 키보드 내비게이션을 쓰는 곳이 없어서, Keyboard.current를 직접 폴링하는 기존 Click.cs 방식을 따름).
[RequireComponent(typeof(UIDocument))]
public class MainMenuUI : MonoBehaviour
{
    private const string MainSceneName = "MainScene"; // 새 게임/불러오기가 이동할 씬 이름
    private const string SaveFileName = "savedata.json"; // SaveManager의 기본 저장 파일명과 반드시 같아야 "새 게임"이 실제로 지워짐
    private const string MasterVolumeKey = "MasterVolume"; // 마스터 음량을 저장할 PlayerPrefs 키
    private const float VolumeStep = 0.05f; // 설정에서 방향키로 음량 조절할 때 한 번에 바뀌는 양

    private static readonly Color ButtonColor = new Color(0.15f, 0.15f, 0.15f, 0.85f); // 버튼 기본 배경 (SidePanelUI와 동일 톤)
    private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.35f, 1f); // 키보드/마우스로 선택된 항목 테두리색 (ZoneArrowsUI의 HoverTint와 통일)
    private static readonly Color PanelBg = new Color(0.08f, 0.08f, 0.1f, 1f); // 확인창/설정 패널 배경 (SidePanelUI 팝업과 동일 톤)
    private static readonly Color BorderColor = new Color(0.32f, 0.38f, 0.55f); // 패널 테두리색 (SidePanelUI InfoBorderColor와 동일)

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

    private VisualElement _settingsPanel; // 설정 전체화면 패널 (평소엔 숨김)
    private VisualElement _volumeWrap; // 음량 슬라이더를 감싼 테두리용 컨테이너 (선택 강조 표시용)
    private Slider _volumeSlider; // 마스터 음량 슬라이더
    private Button _settingsBackButton; // 설정에서 메인 메뉴로 돌아가는 버튼
    private int _settingsSelectedIndex; // 0=음량 슬라이더, 1=뒤로가기 버튼

    // 지금 방향키/엔터 입력을 메인 메뉴/확인창/설정 중 어디로 보낼지
    private enum InputMode { Menu, Confirm, Settings }
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
        GameFonts.Apply(root);
        root.style.flexGrow = 1;
        root.style.backgroundColor = PanelBg;

        var title = new Label("BREAK THE WORLD"); // 대충 만든 타이틀 - 나중에 로고 이미지로 교체 가능
        title.style.position = Position.Absolute;
        title.style.top = Length.Percent(18);
        title.style.left = 0;
        title.style.right = 0;
        title.style.fontSize = 56;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = Color.white;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        root.Add(title);

        var menuColumn = new VisualElement(); // 버튼 4개가 세로로 쌓이는 가운데 정렬 칸
        menuColumn.style.position = Position.Absolute;
        menuColumn.style.left = 0;
        menuColumn.style.right = 0;
        menuColumn.style.top = Length.Percent(42);
        menuColumn.style.alignItems = Align.Center;
        root.Add(menuColumn);

        System.Action newGameAction = () =>
            RequestConfirm("새 게임을 시작하시겠습니까?\n기존 저장 데이터가 사라집니다.", StartNewGame);
        System.Action loadAction = LoadGame;
        System.Action settingsAction = OpenSettings;
        System.Action quitAction = () =>
            RequestConfirm("게임을 종료하시겠습니까?", QuitGame);

        Button newGameButton = CreateMenuButton("새 게임", newGameAction);
        Button loadButton = CreateMenuButton("불러오기", loadAction);
        Button settingsButton = CreateMenuButton("설정", settingsAction);
        Button quitButton = CreateMenuButton("나가기", quitAction);

        bool hasSave = File.Exists(SavePath); // 저장 파일이 없으면 불러오기를 눌러도 할 게 없으니 비활성화
        if (!hasSave)
        {
            loadButton.SetEnabled(false);
            loadButton.style.opacity = 0.4f;
        }

        menuColumn.Add(newGameButton);
        menuColumn.Add(loadButton);
        menuColumn.Add(settingsButton);
        menuColumn.Add(quitButton);

        _menuButtons.Add(newGameButton);
        _menuActions.Add(newGameAction);
        if (hasSave) // 비활성 버튼은 방향키 이동 대상에서 제외
        {
            _menuButtons.Add(loadButton);
            _menuActions.Add(loadAction);
        }
        _menuButtons.Add(settingsButton);
        _menuActions.Add(settingsAction);
        _menuButtons.Add(quitButton);
        _menuActions.Add(quitAction);

        UpdateMenuSelectionVisual();

        _confirmDialog = BuildConfirmDialog();
        root.Add(_confirmDialog);

        _settingsPanel = BuildSettingsPanel();
        root.Add(_settingsPanel);
    }

    // 메인 메뉴용 큼직한 버튼 하나. 마우스 오버 시 키보드 선택 인덱스도 같이 옮겨서 둘이 어긋나지 않게 함
    private Button CreateMenuButton(string text, System.Action onClick)
    {
        var button = new Button(onClick) { text = text };
        button.focusable = false; // UI 툴킷 기본 키보드 포커스/내비게이션을 꺼둠 - 켜져있으면 Enter/방향키가 여기(네이티브 포커스)와 밑의 수동 폴링(_selectedIndex) 두 군데서 따로 처리돼 서로 다른 버튼이 눌리는 사고가 남
        button.style.width = 320;
        button.style.height = 80;
        button.style.marginBottom = 16;
        button.style.fontSize = 30;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.backgroundColor = ButtonColor;
        button.style.color = Color.white;
        SetBorderWidth(button, 3);
        SetBorderColor(button, Color.clear);
        SetBorderRadius(button, 8);

        button.RegisterCallback<PointerEnterEvent>(_ =>
        {
            int idx = _menuButtons.IndexOf(button);
            if (idx < 0 || _mode != InputMode.Menu) return; // 확인창/설정이 열려있는 동안은 뒤 메뉴에 반응 안 함
            _selectedIndex = idx;
            UpdateMenuSelectionVisual();
        });

        return button;
    }

    private void UpdateMenuSelectionVisual()
    {
        for (int i = 0; i < _menuButtons.Count; i++)
            SetBorderColor(_menuButtons[i], i == _selectedIndex ? SelectedColor : Color.clear);
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

    // ---- 새 게임 / 불러오기 / 설정 / 나가기 동작 ----

    private void StartNewGame()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath); // 파일이 없으면 SaveManager.Load()가 그냥 기본값으로 시작함 (기존 동작 그대로 재사용)

        SceneManager.LoadScene(MainSceneName);
    }

    private void LoadGame()
    {
        SceneManager.LoadScene(MainSceneName);
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

        var box = new VisualElement();
        box.style.width = 560;
        box.style.paddingTop = 32;
        box.style.paddingBottom = 32;
        box.style.paddingLeft = 32;
        box.style.paddingRight = 32;
        box.style.backgroundColor = PanelBg;
        box.style.alignItems = Align.Center;
        SetBorderWidth(box, 2);
        SetBorderColor(box, BorderColor);
        SetBorderRadius(box, 10);

        _confirmMessage = new Label();
        _confirmMessage.style.fontSize = 26;
        _confirmMessage.style.color = Color.white;
        _confirmMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
        _confirmMessage.style.whiteSpace = WhiteSpace.Normal;
        _confirmMessage.style.marginBottom = 28;
        box.Add(_confirmMessage);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;

        _confirmYes = new Button(() => { CloseConfirm(); _confirmPendingAction?.Invoke(); }) { text = "예" };
        _confirmNo = new Button(CloseConfirm) { text = "아니오" };
        StyleDialogButton(_confirmYes);
        StyleDialogButton(_confirmNo);
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
        SetBorderColor(_confirmYes, _confirmSelectedIndex == 0 ? SelectedColor : Color.clear);
        SetBorderColor(_confirmNo, _confirmSelectedIndex == 1 ? SelectedColor : Color.clear);
    }

    private void HandleConfirmKeyboard(Keyboard kb)
    {
        if (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
        {
            _confirmSelectedIndex = 1 - _confirmSelectedIndex;
            UpdateConfirmSelectionVisual();
        }
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            if (_confirmSelectedIndex == 0) { CloseConfirm(); _confirmPendingAction?.Invoke(); }
            else CloseConfirm();
        }
    }

    // ---- 설정 패널 ----

    private VisualElement BuildSettingsPanel()
    {
        var panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.left = 0;
        panel.style.right = 0;
        panel.style.top = 0;
        panel.style.bottom = 0;
        panel.style.backgroundColor = PanelBg;
        panel.style.display = DisplayStyle.None;

        var title = new Label("설정");
        title.style.position = Position.Absolute;
        title.style.top = Length.Percent(18);
        title.style.left = 0;
        title.style.right = 0;
        title.style.fontSize = 44;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = Color.white;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        panel.Add(title);

        var column = new VisualElement();
        column.style.position = Position.Absolute;
        column.style.left = 0;
        column.style.right = 0;
        column.style.top = Length.Percent(42);
        column.style.alignItems = Align.Center;
        panel.Add(column);

        var volumeLabel = new Label("마스터 음량");
        volumeLabel.style.fontSize = 24;
        volumeLabel.style.color = Color.white;
        volumeLabel.style.marginBottom = 14;
        column.Add(volumeLabel);

        _volumeWrap = new VisualElement(); // 슬라이더 자체엔 테두리가 안 어울려서, 감싸서 선택 강조 테두리를 줌
        _volumeWrap.style.paddingTop = 10;
        _volumeWrap.style.paddingBottom = 10;
        _volumeWrap.style.paddingLeft = 16;
        _volumeWrap.style.paddingRight = 16;
        _volumeWrap.style.marginBottom = 30;
        SetBorderWidth(_volumeWrap, 3);
        SetBorderColor(_volumeWrap, Color.clear);
        SetBorderRadius(_volumeWrap, 8);

        _volumeSlider = new Slider(0f, 1f) { value = PlayerPrefs.GetFloat(MasterVolumeKey, 1f) };
        _volumeSlider.focusable = false; // 버튼과 같은 이유로 네이티브 포커스/방향키 내비게이션 꺼둠 (좌우 조절은 HandleSettingsKeyboard가 직접 함)
        _volumeSlider.style.width = 400;
        AudioListener.volume = _volumeSlider.value; // 시작할 때 저장된 값으로 실제 음량도 맞춤
        _volumeSlider.RegisterValueChangedCallback(evt =>
        {
            AudioListener.volume = evt.newValue;
            PlayerPrefs.SetFloat(MasterVolumeKey, evt.newValue);
        });
        _volumeWrap.Add(_volumeSlider);
        column.Add(_volumeWrap);

        _settingsBackButton = CreateMenuButton("뒤로가기", CloseSettings);
        column.Add(_settingsBackButton);

        _volumeWrap.RegisterCallback<PointerEnterEvent>(_ => { _settingsSelectedIndex = 0; UpdateSettingsSelectionVisual(); });
        _settingsBackButton.RegisterCallback<PointerEnterEvent>(_ => { _settingsSelectedIndex = 1; UpdateSettingsSelectionVisual(); });

        return panel;
    }

    private void OpenSettings()
    {
        _settingsSelectedIndex = 0;
        UpdateSettingsSelectionVisual();
        _settingsPanel.style.display = DisplayStyle.Flex;
        _mode = InputMode.Settings;
    }

    private void CloseSettings()
    {
        _settingsPanel.style.display = DisplayStyle.None;
        _mode = InputMode.Menu;
    }

    private void UpdateSettingsSelectionVisual()
    {
        SetBorderColor(_volumeWrap, _settingsSelectedIndex == 0 ? SelectedColor : Color.clear);
        SetBorderColor(_settingsBackButton, _settingsSelectedIndex == 1 ? SelectedColor : Color.clear);
    }

    private void HandleSettingsKeyboard(Keyboard kb)
    {
        if (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
        {
            _settingsSelectedIndex = 1 - _settingsSelectedIndex;
            UpdateSettingsSelectionVisual();
        }
        else if (_settingsSelectedIndex == 0 && (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame))
        {
            float delta = kb.rightArrowKey.wasPressedThisFrame ? VolumeStep : -VolumeStep; // 슬라이더가 선택돼 있을 땐 좌우 방향키로 값 조절
            _volumeSlider.value = Mathf.Clamp01(_volumeSlider.value + delta); // value 대입이 곧 RegisterValueChangedCallback을 태워서 저장까지 됨
        }
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            if (_settingsSelectedIndex == 1)
                CloseSettings();
        }
    }

    // ---- 공용 스타일 헬퍼 ----

    private void StyleDialogButton(Button button)
    {
        button.focusable = false; // 메인 메뉴 버튼과 같은 이유로 네이티브 포커스/내비게이션 꺼둠
        button.style.width = 140;
        button.style.height = 56;
        button.style.fontSize = 22;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.backgroundColor = ButtonColor;
        button.style.color = Color.white;
        SetBorderWidth(button, 3);
        SetBorderColor(button, Color.clear);
        SetBorderRadius(button, 6);
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
