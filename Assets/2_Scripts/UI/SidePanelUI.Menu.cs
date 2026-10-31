using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public partial class SidePanelUI
{
    private const string MenuSceneName = "MenuScene";

    private Button _statsButton;
    private VisualElement _statsPanel;
    private readonly List<(Label value, System.Func<string> get)> _statRows = new List<(Label, System.Func<string>)>();
    private VisualElement _settingsPanel;
    private VisualElement _confirmBox;
    private Label _confirmMessage;
    private Button _confirmSave;
    private Button _confirmNoSave;
    private System.Action _confirmLeave;
    private VisualElement _demoBox;
    private static SidePanelUI _instance;
    private const float DemoBoxDelay = 2.5f; // 초

    public static void ShowDemoComplete()
    {
        if (_instance != null) _instance.StartCoroutine(_instance.ShowDemoBoxLater());
    }

    private System.Collections.IEnumerator ShowDemoBoxLater()
    {
        yield return new WaitForSecondsRealtime(DemoBoxDelay);
        _demoBox.style.display = DisplayStyle.Flex;
    }

    private void CloseDemoBox() => _demoBox.style.display = DisplayStyle.None;

    private void BuildDemoBox(VisualElement root)
    {
        _instance = this;
        _demoBox = new VisualElement();
        _demoBox.style.position = Position.Absolute;
        _demoBox.style.left = _demoBox.style.right = _demoBox.style.top = _demoBox.style.bottom = 0;
        _demoBox.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        _demoBox.style.alignItems = Align.Center;
        _demoBox.style.justifyContent = Justify.Center;
        _demoBox.style.display = DisplayStyle.None;
        _demoBox.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        _demoBox.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);
        root.Add(_demoBox);

        var window = new VisualElement();
        window.style.alignItems = Align.Center;
        window.style.paddingTop = 40;
        window.style.paddingBottom = 40;
        window.style.paddingLeft = 64;
        window.style.paddingRight = 64;
        PixelStyle.Frame(window, PixelStyle.Window, 6f);
        _demoBox.Add(window);

        var title = new Label("데모 완료!"); // '클'은 테두리 폰트에서 뭉개져서 '완료'로
        title.style.fontSize = 56;
        title.style.paddingLeft = 56;
        title.style.paddingRight = 56;
        title.style.paddingTop = 12;
        title.style.paddingBottom = 18;
        title.style.marginBottom = 36;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        PixelStyle.Frame(title, PixelStyle.Ribbon);
        PixelStyle.OutlinedText(title, PixelStyle.Ribbon.text);
        window.Add(title);

        string[] lines =
        {
            "데모를 끝까지 플레이해 주셔서 감사합니다!",
            "정식 버전에서는 더 많은 맵과 오브젝트가 기다리고 있어요.",
            "정식 출시 때 다시 만나요!",
        };
        foreach (string text in lines)
        {
            var line = new Label(text);
            line.style.fontSize = 36;
            line.style.color = PixelStyle.Text.linear;
            line.style.unityTextAlign = TextAnchor.MiddleCenter;
            line.style.marginBottom = 10;
            window.Add(line);
        }

        var hint = new Label("계속하기를 누르면 지금 진행 그대로 이어서 할 수 있어요.");
        hint.style.fontSize = 28;
        hint.style.color = PixelStyle.MutedText.linear;
        hint.style.marginTop = 14;
        hint.style.marginBottom = 32;
        window.Add(hint);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        window.Add(row);
        Button menu = CreateButton("메인 메뉴로", PixelStyle.Sky, () => { SaveManager.Instance?.SaveNow(); GoToMainMenu(); });
        Button resume = CreateButton("계속하기", PixelStyle.Mint, CloseDemoBox);
        foreach (Button b in new[] { menu, resume })
        {
            b.style.width = 340;
            b.style.height = 96;
            b.style.marginLeft = 10;
            b.style.marginRight = 10;
            row.Add(b);
        }
    }

    private void BuildSettingsPanel(VisualElement root)
    {
        _settingsPanel = CreatePopupFrame(CloseSettingsPanel, out Label title);
        title.text = "설정";

        var body = new VisualElement();
        body.style.position = Position.Absolute;
        body.style.left = 0;
        body.style.right = 0;
        body.style.top = 100;
        body.style.bottom = 0;
        body.style.alignItems = Align.Center;
        body.style.justifyContent = Justify.Center;
        _settingsPanel.Add(body);

        var columns = new VisualElement();
        columns.style.flexDirection = FlexDirection.Row;
        columns.style.alignItems = Align.FlexStart;
        body.Add(columns);

        var left = new VisualElement();
        left.style.marginRight = 56;
        left.style.alignItems = Align.Center;
        columns.Add(left);
        var right = new VisualElement();
        right.style.alignItems = Align.Center;
        columns.Add(right);

        left.Add(PixelStyle.CardHeader("소리"));
        AddVolumeBar(left, "전체 음량", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        AddVolumeBar(left, "효과음", () => SfxPlayer.Volume, v => SfxPlayer.Volume = v);

        right.Add(PixelStyle.CardHeader("화면"));
        AddSettingRow(right, "화면 모드", () => GameSettings.Fullscreen ? "전체화면" : "창 모드", _ => GameSettings.Fullscreen = !GameSettings.Fullscreen);
        AddSettingRow(right, "해상도", () => { Vector2Int r = GameSettings.Resolution; return $"{r.x}x{r.y}"; }, step =>
        {
            List<Vector2Int> list = GameSettings.Resolutions;
            if (list.Count == 0) return;
            int index = Mathf.Max(0, list.IndexOf(GameSettings.Resolution));
            GameSettings.Resolution = list[(index + step + list.Count) % list.Count];
        });
        AddSettingRow(right, "프레임 제한", () =>
        {
            int fps = GameSettings.FpsOptions[GameSettings.FpsIndex];
            return fps < 0 ? "무제한" : $"{fps} FPS";
        }, step => GameSettings.FpsIndex = (GameSettings.FpsIndex + step + GameSettings.FpsOptions.Length) % GameSettings.FpsOptions.Length);
        var gameHeader = PixelStyle.CardHeader("게임");
        gameHeader.style.marginTop = 10;
        right.Add(gameHeader);
        AddSettingRow(right, "화면 흔들림", () => GameSettings.ScreenShake ? "켜기" : "끄기", _ => GameSettings.ScreenShake = !GameSettings.ScreenShake);
        AddSettingRow(right, "데미지 숫자", () => GameSettings.DamageNumbers ? "켜기" : "끄기", _ => GameSettings.DamageNumbers = !GameSettings.DamageNumbers);
        AddSettingRow(right, "숫자 표시", () => GameSettings.NumberUnits ? "영어 단위" : "숫자", _ => GameSettings.NumberUnits = !GameSettings.NumberUnits);

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.marginTop = 36;
        body.Add(buttons);

        Button debug = CreateButton("디버그", PixelStyle.Butter, () => { CloseSettingsPanel(); OpenDebugPanel(); });
        Button menu = CreateButton("메인 메뉴로", PixelStyle.Sky, () => ShowLeaveConfirm("메인 메뉴로 나갈까요?", "나가기", GoToMainMenu));
        Button quit = CreateButton("게임 종료", PixelStyle.Pink, () => ShowLeaveConfirm("게임을 종료할까요?", "종료", QuitGame));
        foreach (Button b in new[] { debug, menu, quit })
        {
            b.style.width = 340;
            b.style.height = 96;
            b.style.marginLeft = 10;
            b.style.marginRight = 10;
            buttons.Add(b);
        }

        BuildLeaveConfirm();
        root.Add(_settingsPanel);
    }

    private void BuildLeaveConfirm()
    {
        _confirmBox = new VisualElement();
        _confirmBox.style.position = Position.Absolute;
        _confirmBox.style.left = 0;
        _confirmBox.style.right = 0;
        _confirmBox.style.top = 0;
        _confirmBox.style.bottom = 0;
        _confirmBox.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
        _confirmBox.style.alignItems = Align.Center;
        _confirmBox.style.justifyContent = Justify.Center;
        _confirmBox.style.display = DisplayStyle.None;
        _settingsPanel.Add(_confirmBox);

        var window = new VisualElement();
        window.style.alignItems = Align.Center;
        window.style.paddingTop = 36;
        window.style.paddingBottom = 36;
        window.style.paddingLeft = 48;
        window.style.paddingRight = 48;
        PixelStyle.Frame(window, PixelStyle.Window, 6f);
        _confirmBox.Add(window);

        _confirmMessage = new Label();
        _confirmMessage.style.fontSize = 36;
        _confirmMessage.style.color = PixelStyle.Text.linear;
        _confirmMessage.style.marginBottom = 30;
        window.Add(_confirmMessage);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        window.Add(row);
        _confirmSave = CreateButton("", PixelStyle.Mint, () => Leave(true));
        _confirmNoSave = CreateButton("", PixelStyle.Pink, () => Leave(false));
        Button back = CreateButton("돌아가기", PixelStyle.Sky, () => _confirmBox.style.display = DisplayStyle.None);
        foreach (Button b in new[] { _confirmSave, _confirmNoSave, back })
        {
            b.style.width = 460;
            b.style.height = 100;
            b.style.marginLeft = 8;
            b.style.marginRight = 8;
            row.Add(b);
        }
    }

    private void ShowLeaveConfirm(string question, string verb, System.Action leave)
    {
        _confirmMessage.text = question;
        _confirmSave.text = $"저장하고 {verb}";
        _confirmNoSave.text = $"저장 안 하고 {verb}";
        _confirmLeave = leave;
        _confirmBox.style.display = DisplayStyle.Flex;
    }

    private void Leave(bool save)
    {
        if (save) SaveManager.Instance?.SaveNow();
        else SaveManager.Instance?.DiscardPending();
        PlayerPrefs.Save(); // 설정 값은 저장 여부와 상관없이 유지
        _confirmLeave?.Invoke();
    }

    private void AddVolumeBar(VisualElement column, string label, System.Func<float> get, System.Action<float> set)
    {
        var caption = new Label();
        caption.style.fontSize = 36;
        caption.style.color = PixelStyle.Text.linear;
        caption.style.marginBottom = 6;
        column.Add(caption);

        var cylinder = new GlassCylinderSlider(get(), v => { set(v); caption.text = $"{label}  {Percent(v)}"; }, _ => { });
        var slot = new VisualElement();
        slot.style.paddingTop = 14;
        slot.style.paddingBottom = 18;
        slot.style.paddingLeft = 18;
        slot.style.paddingRight = 18;
        slot.style.marginBottom = 18;
        PixelStyle.Frame(slot, PixelStyle.Slot);
        slot.Add(cylinder);
        column.Add(slot);

        _settingRefreshers.Add(() => { cylinder.SetValue(get()); caption.text = $"{label}  {Percent(get())}"; });
    }

    private void AddSettingRow(VisualElement column, string label, System.Func<string> getValue, System.Action<int> step)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.width = 640;
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

        var value = new Label();
        value.style.width = 230;
        value.style.fontSize = 36;
        value.style.color = PixelStyle.Text.linear;
        value.style.unityTextAlign = TextAnchor.MiddleCenter;
        System.Action<int> change = d => { step(d); value.text = getValue(); };
        row.Add(ArrowButton("<", () => change(-1)));
        row.Add(value);
        row.Add(ArrowButton(">", () => change(1)));
        _settingRefreshers.Add(() => value.text = getValue());
        column.Add(row);
    }

    private readonly List<System.Action> _settingRefreshers = new List<System.Action>();

    private static Button ArrowButton(string text, System.Action onClick)
    {
        var button = new Button(onClick) { text = text };
        button.focusable = false;
        button.style.width = 60;
        button.style.height = 60;
        button.style.fontSize = 36;
        PixelStyle.Button(button, PixelStyle.Mint);
        return button;
    }

    private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

    private void OpenSettingsPanel()
    {
        foreach (System.Action refresh in _settingRefreshers) refresh();
        _confirmBox.style.display = DisplayStyle.None;
        _settingsPanel.style.display = DisplayStyle.Flex;
        RefreshButtonRowVisibility();
        Time.timeScale = 0f;
        OnSelectorPanelToggled?.Invoke(true);
    }

    private void CloseSettingsPanel()
    {
        _settingsPanel.style.display = DisplayStyle.None;
        PlayerPrefs.Save();
        RefreshButtonRowVisibility();
        Time.timeScale = 1f;
        OnSelectorPanelToggled?.Invoke(false);
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MenuSceneName);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private Button CreateGearButton()
    {
        Button button = CreateButton("", PixelStyle.Slot, OpenSettingsPanel);
        button.focusable = false;
        button.style.width = 110;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;

        var icon = new VisualElement();
        icon.pickingMode = PickingMode.Ignore;
        icon.style.width = 80;
        icon.style.height = 80;
        icon.style.backgroundImage = new StyleBackground(settingsIcon);
        button.Add(icon);
        return button;
    }

    private void BuildStatsPanel(VisualElement root)
    {
        _statsPanel = CreatePopupFrame(CloseStatsPanel, out Label title);
        title.text = "통계";

        var columns = StatPage();
        var left = StatColumn(columns, "기록");
        left.style.marginRight = 48;
        UpgradeManager U() => UpgradeManager.Instance;
        AddStat(left, "플레이 시간", () => { var t = System.TimeSpan.FromSeconds(GameStats.PlaySeconds); return $"{(int)t.TotalHours}시간 {t.Minutes}분"; });
        AddStat(left, "부순 오브젝트", () => NumberFormatUtil.Format(GameStats.ObjectsBroken) + "개");
        AddStat(left, "직접 클릭", () => NumberFormatUtil.Format(GameStats.Clicks) + "번");
        AddStat(left, "치명타", () => NumberFormatUtil.Format(GameStats.Crits) + "번");
        AddStat(left, "슈퍼 크리티컬", () => NumberFormatUtil.Format(GameStats.SuperCrits) + "번");
        AddStat(left, "보스 처치", () => NumberFormatUtil.Format(GameStats.BossClears) + "번");
        AddStat(left, "얻은 조각", () => NumberFormatUtil.Format(CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalPieces : 0));
        AddStat(left, "얻은 결정", () => NumberFormatUtil.Format(CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalCrystals : 0));
        AddStat(left, "해금한 오브젝트", () => UnlockedText(ObjectManager.Instance != null ? ObjectManager.Instance.VisibleObjectCount : 0, i => ObjectManager.Instance.IsUnlocked(i)));
        AddStat(left, "해금한 무기", () => UnlockedText(WeaponManager.Instance != null ? WeaponManager.Instance.VisibleWeaponCount : 0, i => WeaponManager.Instance.IsUnlocked(i)));

        var right = StatColumn(columns, "능력치");
        AddStat(right, "클릭 데미지", () =>
        {
            if (WeaponManager.Instance == null || U() == null) return "-";
            return NumberFormatUtil.Format(WeaponManager.Instance.CurrentClickDamage + U().GetClickDamageBonus(WeaponManager.Instance.EquippedIndex));
        });
        AddStat(right, "치명타 확률", () => U() != null ? Chance(U().CritChanceValue) : "-");
        AddStat(right, "치명타 데미지", () => U() != null ? $"x{U().CritMultiplierValue:0.##}" : "-");
        AddStat(right, "슈퍼 크리티컬 확률", () => U() != null ? Chance(U().LuckyClickChance) : "-");
        AddStat(right, "슈퍼 크리티컬 데미지", () => U() != null ? $"x{U().CritMultiplierValue * 2f:0.##}" : "-");
        AddStat(right, "더블 클릭 확률", () => U() != null && U().DoubleClickIsUnlocked ? Chance(U().DoubleClickChanceValue) : "잠김");
        AddStat(right, "조각 획득 보너스", () => U() != null ? $"+{Mathf.RoundToInt((U().PieceGainMultiplier() - 1f) * 100f)}%" : "-");
        AddStat(right, "결정 확률", () => U() != null ? Chance(U().CrystalDropChance) : "-");
        AddStat(right, "결정 개수", () => U() != null ? $"{NumberFormatUtil.Format(U().CrystalDropAmountIn(-1))}개" : "-");
        AddStat(right, "커서 범위", () => U() != null && U().HoverPickupIsUnlocked ? $"{U().CursorRadius:0.##}" : "잠김");

        var objectsPage = StatPage();
        int count = GameSettings.IsDemo ? Mathf.Min(GameSettings.DemoObjectCount, ObjectManager.StaticObjectCount) : ObjectManager.StaticObjectCount; // 데모는 풀버전 오브젝트 빼고
        int half = (count + 1) / 2;
        var objLeft = StatColumn(objectsPage, "부순 횟수");
        objLeft.style.marginRight = 48;
        var objRight = StatColumn(objectsPage, "부순 횟수");
        for (int i = 0; i < count; i++)
        {
            int index = i;
            AddStat(i < half ? objLeft : objRight, ObjectManager.StaticObjectNameAt(index),
                () => ObjectManager.Instance != null && !ObjectManager.Instance.IsUnlocked(index) && GameStats.BrokenOf(index) == 0
                    ? "???" : NumberFormatUtil.Format(GameStats.BrokenOf(index)) + "번");
        }

        Button prev = StatArrow("<", -1);
        prev.style.left = 40;
        Button next = StatArrow(">", 1);
        next.style.right = 40;
        _statPageLabel = new Label();
        _statPageLabel.pickingMode = PickingMode.Ignore;
        _statPageLabel.style.position = Position.Absolute;
        _statPageLabel.style.left = 0;
        _statPageLabel.style.right = 0;
        _statPageLabel.style.bottom = 36;
        _statPageLabel.style.fontSize = 36;
        _statPageLabel.style.color = PixelStyle.Text.linear;
        _statPageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _statsPanel.Add(_statPageLabel);

        root.Add(_statsPanel);
    }

    private readonly List<VisualElement> _statPages = new List<VisualElement>();
    private int _statPage;
    private Label _statPageLabel;

    private VisualElement StatPage()
    {
        var page = new VisualElement();
        page.style.position = Position.Absolute;
        page.style.left = 0;
        page.style.right = 0;
        page.style.top = 110;
        page.style.flexDirection = FlexDirection.Row;
        page.style.justifyContent = Justify.Center;
        page.style.alignItems = Align.FlexStart;
        _statsPanel.Add(page);
        _statPages.Add(page);
        return page;
    }

    private Button StatArrow(string text, int step)
    {
        var button = new Button(() => ShowStatPage(_statPage + step)) { text = text };
        button.focusable = false;
        button.style.position = Position.Absolute;
        button.style.top = Length.Percent(50);
        button.style.width = 90;
        button.style.height = 90;
        button.style.fontSize = 48;
        PixelStyle.Button(button, PixelStyle.Mint);
        _statsPanel.Add(button);
        return button;
    }

    private void ShowStatPage(int page)
    {
        _statPage = (page % _statPages.Count + _statPages.Count) % _statPages.Count;
        for (int i = 0; i < _statPages.Count; i++)
            _statPages[i].style.display = i == _statPage ? DisplayStyle.Flex : DisplayStyle.None;
        _statPageLabel.text = $"{_statPage + 1} / {_statPages.Count}";
    }

    private static VisualElement StatColumn(VisualElement parent, string header)
    {
        var column = new VisualElement();
        column.style.alignItems = Align.Center;
        column.Add(PixelStyle.CardHeader(header));
        parent.Add(column);
        return column;
    }

    private void AddStat(VisualElement column, string label, System.Func<string> get)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.width = 640;
        row.style.paddingLeft = 20;
        row.style.paddingRight = 20;
        row.style.paddingTop = 4;
        row.style.paddingBottom = 8;
        row.style.marginBottom = 8; // 한 칸에 10줄이 화면에 들어가게
        PixelStyle.Frame(row, PixelStyle.Card);

        var name = new Label(label);
        name.style.flexGrow = 1;
        name.style.fontSize = 36;
        name.style.color = PixelStyle.MutedText.linear;
        row.Add(name);

        var value = new Label();
        value.style.fontSize = 36;
        value.style.color = PixelStyle.Text.linear;
        row.Add(value);
        _statRows.Add((value, get));
        column.Add(row);
    }

    private static string Chance(float chance) => $"{chance * 100f:0.##}%";

    private static string UnlockedText(int count, System.Func<int, bool> unlocked)
    {
        int n = 0;
        for (int i = 0; i < count; i++) if (unlocked(i)) n++;
        return $"{n} / {count}";
    }

    private void OpenStatsPanel()
    {
        foreach (var (value, get) in _statRows) value.text = get();
        ShowStatPage(0);
        _statsPanel.style.display = DisplayStyle.Flex;
        RefreshButtonRowVisibility();
        Time.timeScale = 0f;
        OnSelectorPanelToggled?.Invoke(true);
    }

    private void CloseStatsPanel()
    {
        _statsPanel.style.display = DisplayStyle.None;
        RefreshButtonRowVisibility();
        Time.timeScale = 1f;
        OnSelectorPanelToggled?.Invoke(false);
    }
}
