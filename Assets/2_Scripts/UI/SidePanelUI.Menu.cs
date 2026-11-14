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
    private readonly List<(VisualElement row, System.Func<bool> visible)> _statGates = new List<(VisualElement, System.Func<bool>)>(); // 해금 전엔 숨길 통계 줄
    private VisualElement _settingsPanel;
    private Label _settingHelp; // 설정 줄 제목에 마우스를 올리면 뜨는 설명 상자
    private const float SettingHelpWidth = 520f; // 설명 상자 최대 너비 (px)
    private VisualElement _confirmBox;
    private Label _confirmMessage;
    private Button _confirmLeaveButton; // 나가기/종료 버튼 (누르면 저장하고 나감)
    private System.Action _confirmLeave;
    private VisualElement _demoBox;
    private static SidePanelUI _instance;
    private const float DemoBoxDelay = 2.5f; // 초
    private const float DemoButtonLock = 0.7f; // 초 - 창이 뜬 뒤 실수로 바로 넘기지 않게 버튼을 막는 시간 (보스전 실패 창과 같음)
    private VisualElement _demoButtons; // 데모 완료 창의 버튼 줄
    private Label _demoRebirthNote; // 환생 칸으로 열었을 때만 보이는 '환생 미지원' 안내

    public static void ShowDemoComplete(float delay = DemoBoxDelay, bool fromRebirth = false)
    {
        if (_instance == null) return;
        _instance._demoRebirthNote.style.display = fromRebirth ? DisplayStyle.Flex : DisplayStyle.None;
        _instance.StartCoroutine(_instance.ShowDemoBoxLater(delay));
    }

    private System.Collections.IEnumerator ShowDemoBoxLater(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        _demoBox.style.display = DisplayStyle.Flex;
        _demoButtons.SetEnabled(false);
        yield return new WaitForSecondsRealtime(DemoButtonLock);
        _demoButtons.SetEnabled(true);
    }

    private void CloseDemoBox() => _demoBox.style.display = DisplayStyle.None;

    // 환생 직후 / 환생 버튼
    public static void OpenRebirthTree()
    {
        if (_instance != null && _instance.rebirthTreePanel != null) _instance.rebirthTreePanel.SetActive(true);
    }

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

        var title = new Label();
        Loc.Bind(title, "데모 완료!"); // '클'은 테두리 폰트에서 뭉개져서 '완료'로
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
            "데모 버전을 플레이해 주셔서 감사합니다!",
            "정식 출시를 기다려 주세요!",
        };
        foreach (string text in lines)
        {
            var line = new Label();
            Loc.Bind(line, text);
            line.style.fontSize = 36;
            line.style.color = PixelStyle.Text.linear;
            line.style.unityTextAlign = TextAnchor.MiddleCenter;
            line.style.marginBottom = 10;
            window.Add(line);
        }

        _demoRebirthNote = new Label();
        Loc.Bind(_demoRebirthNote, "데모 버전에서는 환생을 지원하지 않아요.");
        _demoRebirthNote.style.fontSize = 28;
        _demoRebirthNote.style.color = PixelStyle.MutedText.linear;
        _demoRebirthNote.style.marginTop = 14;
        _demoRebirthNote.style.display = DisplayStyle.None;
        window.Add(_demoRebirthNote);

        var hint = new Label();
        Loc.Bind(hint, "계속하기를 누르면 지금 진행 그대로 이어서 할 수 있어요.");
        hint.style.fontSize = 28;
        hint.style.color = PixelStyle.MutedText.linear;
        hint.style.marginTop = 14;
        hint.style.marginBottom = 32;
        window.Add(hint);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        window.Add(row);
        _demoButtons = row;
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
        Loc.Bind(title, "설정");

        var body = new VisualElement();
        body.style.position = Position.Absolute;
        body.style.left = 0;
        body.style.right = 0;
        body.style.top = 100;
        body.style.bottom = 30; // 아래 창 테두리와 버튼 사이 여백
        body.style.alignItems = Align.Center;
        body.style.justifyContent = Justify.Center;
        _settingsPanel.Add(body);

        var columns = new VisualElement(); // 소리 / 게임 / 비디오 세 칸을 한 줄로
        columns.style.flexDirection = FlexDirection.Row;
        columns.style.alignItems = Align.FlexStart;
        body.Add(columns);

        VisualElement sound = StatColumn(columns, "소리"); // 왼쪽 칸: 소리
        AddVolumeBar(sound, "전체 음량", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        AddVolumeBar(sound, "효과음", () => SfxPlayer.Volume, v => SfxPlayer.Volume = v);
        AddVolumeBar(sound, "배경음악", () => GameSettings.BgmVolume, v => GameSettings.BgmVolume = v);

        VisualElement game = StatColumn(columns, "게임"); // 가운데 칸: 게임
        game.style.marginLeft = 32;
        AddSettingRow(game, "언어", () => Loc.NativeNames[Loc.Language], step => Loc.Language = (Loc.Language + step + Loc.Codes.Length) % Loc.Codes.Length);
        AddSettingRow(game, "숫자 단위", () => GameSettings.NumberUnits ? "1K" : "1,000", _ => GameSettings.NumberUnits = !GameSettings.NumberUnits,
            "큰 숫자를 1K나 1M처럼 줄여서 보여줘요.");
        AddSettingRow(game, "데미지 표시", () => GameSettings.DamageNumbers ? "켜짐" : "꺼짐", _ => GameSettings.DamageNumbers = !GameSettings.DamageNumbers);
        AddSettingRow(game, "화면 흔들림", () => GameSettings.ScreenShake ? "켜짐" : "꺼짐", _ => GameSettings.ScreenShake = !GameSettings.ScreenShake);
        VisualElement hoverClickRow = AddSettingRow(game, "커서 타격", () => GameSettings.HoverClick ? "켜짐" : "꺼짐", _ => GameSettings.HoverClick = !GameSettings.HoverClick,
            "켜면 클릭하지 않고 커서만 올려도 오브젝트를 때려요."); // 커서 타격 켜기/끄기 줄
        AddSettingRow(game, "백그라운드 실행", () => GameSettings.RunInBackground ? "켜짐" : "꺼짐", _ => GameSettings.RunInBackground = !GameSettings.RunInBackground,
            "켜면 게임을 최소화해도 게임이 계속 진행돼요.");
        _settingRefreshers.Add(() => hoverClickRow.style.display = UpgradeManager.Instance != null && UpgradeManager.Instance.HoverClickIsUnlocked ? DisplayStyle.Flex : DisplayStyle.None); // 산 뒤에만 보임

        VisualElement video = StatColumn(columns, "비디오"); // 오른쪽 칸: 비디오
        video.style.marginLeft = 32;
        AddSettingRow(video, "화면 모드", () => GameSettings.Fullscreen ? "전체 화면" : "창 모드", _ => GameSettings.Fullscreen = !GameSettings.Fullscreen);
        AddSettingRow(video, "해상도", () => { Vector2Int r = GameSettings.Resolution; return $"{r.x}x{r.y}"; }, step =>
        {
            List<Vector2Int> list = GameSettings.Resolutions;
            if (list.Count == 0) return;
            int index = Mathf.Max(0, list.IndexOf(GameSettings.Resolution));
            GameSettings.Resolution = list[(index + step + list.Count) % list.Count];
        });
        AddSettingRow(video, "수직 동기화", () => GameSettings.VSync ? "켜짐" : "꺼짐", _ => GameSettings.VSync = !GameSettings.VSync,
            "모니터 주사율에 맞춰 화면 찢김을 줄여요.");
        VisualElement fpsRow = AddSettingRow(video, "프레임 제한", () =>
        {
            int fps = GameSettings.FpsOptions[GameSettings.FpsIndex];
            return fps < 0 ? "무제한" : $"{fps} FPS";
        }, step => GameSettings.FpsIndex = (GameSettings.FpsIndex + step + GameSettings.FpsOptions.Length) % GameSettings.FpsOptions.Length); // 프레임 제한 줄
        _settingRefreshers.Add(() =>
        {
            fpsRow.SetEnabled(!GameSettings.VSync); // 수직 동기화 중엔 의미 없어서 못 바꾸게
            fpsRow.style.opacity = GameSettings.VSync ? 0.4f : 1f; // 흐리게
        });

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.marginTop = 24;
        body.Add(buttons);

        Button menu = CreateButton("메인 메뉴로", PixelStyle.Sky, () => ShowLeaveConfirm("메인 메뉴로 나갈까요?", "나가기", GoToMainMenu));
        Button quit = CreateButton("게임 종료", PixelStyle.Pink, () => ShowLeaveConfirm("게임을 종료할까요?", "종료", QuitGame));
#if UNITY_EDITOR
        Button debug = CreateButton("디버그", PixelStyle.Butter, () => { CloseSettingsPanel(); OpenDebugPanel(); }); // 에디터 전용 (빌드엔 코드째 빠짐)
        Button[] row = { debug, menu, quit }; // 설정 아래 버튼 줄
#else
        Button[] row = { menu, quit }; // 설정 아래 버튼 줄
#endif
        foreach (Button b in row)
        {
            b.style.width = 340;
            b.style.height = 96;
            b.style.marginLeft = 10;
            b.style.marginRight = 10;
            HoverGrow(b);
            buttons.Add(b);
        }

        BuildLeaveConfirm();
        _settingHelp = new Label(); // 설명 상자 (창 위에 그려지게 마지막에 붙임)
        _settingHelp.style.position = Position.Absolute;
        _settingHelp.style.maxWidth = SettingHelpWidth;
        _settingHelp.style.whiteSpace = WhiteSpace.Normal;
        _settingHelp.style.fontSize = 28;
        _settingHelp.style.color = PixelStyle.Text.linear;
        _settingHelp.style.paddingLeft = 18;
        _settingHelp.style.paddingRight = 18;
        _settingHelp.style.paddingTop = 12;
        _settingHelp.style.paddingBottom = 14;
        PixelStyle.Frame(_settingHelp, PixelStyle.Window);
        _settingHelp.pickingMode = PickingMode.Ignore;
        _settingHelp.style.display = DisplayStyle.None;
        _settingsPanel.Add(_settingHelp);
        root.Add(_settingsPanel);
        Loc.Bind(_settingsPanel, () => { foreach (System.Action refresh in _settingRefreshers) refresh(); });
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
        _confirmMessage.style.marginBottom = 10;
        window.Add(_confirmMessage);

        var autoSaveHint = new Label(); // 나가도 진행이 남는다는 안내
        Loc.Bind(autoSaveHint, "진행 상황은 자동으로 저장돼요.");
        autoSaveHint.style.fontSize = 28;
        autoSaveHint.style.color = PixelStyle.MutedText.linear;
        autoSaveHint.style.marginBottom = 30;
        window.Add(autoSaveHint);

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        window.Add(row);
        _confirmLeaveButton = CreateButton("", PixelStyle.Pink, Leave);
        Button back = CreateButton("돌아가기", PixelStyle.Sky, () => _confirmBox.style.display = DisplayStyle.None);
        foreach (Button b in new[] { _confirmLeaveButton, back })
        {
            b.style.width = 340;
            b.style.height = 100;
            b.style.marginLeft = 8;
            b.style.marginRight = 8;
            HoverGrow(b);
            row.Add(b);
        }
    }

    private void ShowLeaveConfirm(string question, string verb, System.Action leave)
    {
        _confirmMessage.text = Loc.T(question);
        _confirmLeaveButton.text = Loc.T(verb);
        _confirmLeave = leave;
        _confirmBox.style.display = DisplayStyle.Flex;
    }

    private void Leave()
    {
        SaveManager.Instance?.SaveNow(); // 나갈 때는 항상 자동 저장
        PlayerPrefs.Save();
        _confirmLeave?.Invoke();
    }

    private void AddVolumeBar(VisualElement column, string label, System.Func<float> get, System.Action<float> set)
    {
        var caption = new Label();
        caption.style.fontSize = 36;
        caption.style.color = PixelStyle.Text.linear;
        caption.style.marginBottom = 6;
        column.Add(caption);

        var cylinder = new GlassCylinderSlider(get(), v => { set(v); caption.text = $"{Loc.T(label)}  {Percent(v)}"; }, _ => { });
        var slot = new VisualElement();
        slot.style.paddingTop = 14;
        slot.style.paddingBottom = 18;
        slot.style.paddingLeft = 18;
        slot.style.paddingRight = 18;
        slot.style.marginBottom = 18;
        PixelStyle.Frame(slot, PixelStyle.GlassTrack);
        slot.Add(cylinder);
        HoverGrow(slot);
        column.Add(slot);

        _settingRefreshers.Add(() => { cylinder.SetValue(get()); caption.text = $"{Loc.T(label)}  {Percent(get())}"; });
    }

    private VisualElement AddSettingRow(VisualElement column, string label, System.Func<string> getValue, System.Action<int> step, string help = null)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.width = 650; // 세 칸이 한 줄에 들어가는 너비
        row.style.paddingLeft = 20;
        row.style.paddingRight = 12;
        row.style.paddingTop = 10; // 줄 간격
        row.style.paddingBottom = 12;
        row.style.marginBottom = 8;
        PixelStyle.Frame(row, PixelStyle.Card);

        var name = new Label();
        Loc.Bind(name, label);
        name.style.flexGrow = 1;
        PixelStyle.FitText(name, 40);
        name.style.color = PixelStyle.Text.linear;
        row.Add(name);
        if (help != null) AddHelpHover(name, help);

        var value = new Label();
        value.style.width = 200;
        PixelStyle.FitText(value, 40);
        value.style.flexShrink = 0;
        value.style.color = PixelStyle.Text.linear;
        value.style.unityTextAlign = TextAnchor.MiddleCenter;
        System.Action<int> change = d =>
        {
            step(d);
            PlayerPrefs.Save(); // 설정 바꾸면 바로 저장
            foreach (System.Action refresh in _settingRefreshers) refresh(); // 다른 줄 표시/숨김도 같이 (수직 동기화 - 프레임 제한)
        };
        row.Add(ArrowButton("<", () => change(-1)));
        row.Add(value);
        row.Add(ArrowButton(">", () => change(1)));
        _settingRefreshers.Add(() => value.text = Loc.T(getValue()));
        HoverGrow(row);
        column.Add(row);
        return row;
    }

    // 제목에 마우스를 올리면 제목 아래에 설명 상자
    private void AddHelpHover(Label name, string help)
    {
        name.RegisterCallback<PointerEnterEvent>(_ =>
        {
            _settingHelp.text = Loc.T(help);
            Vector2 at = _settingsPanel.WorldToLocal(new Vector2(name.worldBound.xMin, name.worldBound.yMax + 8f)); // 제목 바로 아래
            float maxLeft = _settingsPanel.resolvedStyle.width - SettingHelpWidth - 40f; // 창 오른쪽 밖으로 안 나가게
            _settingHelp.style.left = Mathf.Max(0f, Mathf.Min(at.x, maxLeft));
            _settingHelp.style.top = at.y;
            _settingHelp.style.display = DisplayStyle.Flex;
        });
        name.RegisterCallback<PointerLeaveEvent>(_ => _settingHelp.style.display = DisplayStyle.None);
    }

    // 메인 메뉴 설정창처럼 마우스 올리면 살짝 커짐
    private static void HoverGrow(VisualElement element)
    {
        const float hoverScale = 1.08f; // MainMenuUI.SelectedScale과 같은 값
        element.RegisterCallback<PointerEnterEvent>(_ => element.style.scale = new Scale(new Vector3(hoverScale, hoverScale, 1f)));
        element.RegisterCallback<PointerLeaveEvent>(_ => element.style.scale = StyleKeyword.Null);
    }

    private readonly List<System.Action> _settingRefreshers = new List<System.Action>();

    private static Button ArrowButton(string text, System.Action onClick)
    {
        var button = new Button(onClick) { text = text };
        button.focusable = false;
        button.style.width = 68;
        button.style.height = 68;
        button.style.flexShrink = 0; // 글이 길어도 화살표는 안 밀리게
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
        PauseForWindow("Settings", true); // 열려 있는 동안 게임 정지 (환생 노드가 있으면 계속 진행)
        OnSelectorPanelToggled?.Invoke(true);
    }

    private void CloseSettingsPanel()
    {
        _settingsPanel.style.display = DisplayStyle.None;
        _settingHelp.style.display = DisplayStyle.None; // 마우스를 올린 채 닫아도 다음에 안 남게
        PlayerPrefs.Save();
        RefreshButtonRowVisibility();
        PauseForWindow("Settings", false);
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
        Loc.Bind(title, "통계");

        var columns = StatPage();
        var left = StatColumn(columns, "기록");
        left.style.marginRight = 48;
        UpgradeManager U() => UpgradeManager.Instance;
        bool CritOn() => U() != null && U().CritChanceValue > 0f; // 치명타 데미지를 한 번이라도 샀는지
        bool SuperCritOn() => U() != null && U().LuckyClickIsUnlocked; // 슈퍼 크리티컬 해금
        AddStat(left, "플레이 시간", () => { var t = System.TimeSpan.FromSeconds(GameStats.PlaySeconds); return Loc.T("{0}시간 {1}분", (int)t.TotalHours, t.Minutes); });
        AddCountStat(left, "부순 오브젝트", () => GameStats.ObjectsBroken, n => Loc.T("{0}개", NumberFormatUtil.Format(n)));
        AddCountStat(left, "총 데미지", () => GameStats.TotalDamage, NumberFormatUtil.Format);
        AddCountStat(left, "최고 데미지", () => GameStats.BestHit, NumberFormatUtil.Format);
        AddCountStat(left, "치명타", () => GameStats.Crits, Times, CritOn);
        AddCountStat(left, "슈퍼 크리티컬", () => GameStats.SuperCrits, Times, SuperCritOn);
        AddCountStat(left, "보스 처치", () => GameStats.BossClears, Times);
        AddCountStat(left, "얻은 조각", () => CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalPieces : 0, NumberFormatUtil.Format);
        AddCountStat(left, "얻은 결정", () => CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalCrystals : 0, NumberFormatUtil.Format);
        AddStat(left, "해금한 오브젝트", () => UnlockedText(ObjectManager.Instance != null ? ObjectManager.Instance.VisibleObjectCount : 0, i => ObjectManager.Instance.IsUnlocked(i)));
        AddStat(left, "해금한 무기", () => UnlockedText(WeaponManager.Instance != null ? WeaponManager.Instance.VisibleWeaponCount : 0, i => WeaponManager.Instance.IsUnlocked(i)));
        AddStat(left, "구매한 업그레이드", () =>
        {
            int bought = 0, total = 0; // 환생 창 노드와 이번 버전에 없는 노드(데모의 풀버전 노드)는 빼고 셈
            if (U() != null)
                foreach (var node in U().Nodes)
                {
                    if (U().UsesStarShards(node.id) || !U().IsInThisVersion(node.id)) continue;
                    total++;
                    if (U().GetLevel(node.id) >= 1) bought++;
                }
            return $"{bought} / {total}";
        });

        var right = StatColumn(columns, "능력치");
        AddStat(right, "클릭 데미지", () =>
        {
            if (WeaponManager.Instance == null || U() == null) return "-";
            return NumberFormatUtil.Format((long)WeaponManager.Instance.CurrentClickDamage + U().GetClickDamageBonus(WeaponManager.Instance.EquippedIndex));
        });
        AddStat(right, "치명타 확률", () => U() != null ? Chance(U().CritChanceValue) : "-", CritOn);
        AddStat(right, "치명타 데미지", () => U() != null ? $"x{U().CritMultiplierValue:0.##}" : "-", CritOn);
        AddStat(right, "슈퍼 크리티컬 확률", () => U() != null ? Chance(U().LuckyClickChance) : "-", SuperCritOn);
        AddStat(right, "슈퍼 크리티컬 데미지", () => U() != null ? $"x{U().CritMultiplierValue * 2f:0.##}" : "-", SuperCritOn);
        AddStat(right, "더블 클릭 확률", () => U() != null ? Chance(U().DoubleClickChanceValue) : "-", () => U() != null && U().DoubleClickIsUnlocked);
        AddStat(right, "조각 획득 보너스", () => U() != null ? $"+{Mathf.RoundToInt((U().PieceGainMultiplier() - 1f) * 100f)}%" : "-");
        AddStat(right, "결정 확률", () => U() != null ? Chance(U().CrystalDropChance) : "-");
        AddStat(right, "결정 개수", () => U() != null ? Loc.T("{0}개", NumberFormatUtil.Format(U().CrystalDropAmountIn(-1))) : "-");
        AddStat(right, "커서 범위", () => U() != null ? $"{U().CursorRadius:0.##}" : "-", () => U() != null && U().HoverPickupIsUnlocked);

        // 부순 횟수: 한 쪽에 맵 2개씩 (맵1,2 / 맵3,4 / 맵5), 칸 제목 = 맵 이름
        int count = GameSettings.IsDemo ? Mathf.Min(GameSettings.DemoObjectCount, ObjectManager.StaticObjectCount) : ObjectManager.StaticObjectCount; // 데모는 풀버전 오브젝트 빼고
        int zones = count > 0 ? ObjectManager.ZoneOfObject(count - 1) + 1 : 0; // 오브젝트가 있는 맵 수
        bool ZoneSeen(int zone) // 그 맵 첫 오브젝트를 해금했는지 (오브젝트는 순서대로 해금)
        {
            if (ObjectManager.Instance == null) return false;
            for (int i = 0; i < count; i++)
                if (ObjectManager.ZoneOfObject(i) == zone) return ObjectManager.Instance.IsUnlocked(i);
            return false;
        }
        for (int firstZone = 0; firstZone < zones; firstZone += 2)
        {
            var page = StatPage();
            int pageZone = firstZone; // 이 쪽의 첫 맵
            _statPageGates[page] = () => ZoneSeen(pageZone); // 아직 못 간 맵 쪽은 건너뜀
            for (int zone = firstZone; zone < Mathf.Min(firstZone + 2, zones); zone++)
            {
                int columnZone = zone; // 이 칸의 맵
                var column = StatColumn(page, ObjectManager.ZoneNameKey(zone));
                if (zone > firstZone) column.style.marginLeft = 48;
                _statGates.Add((column, () => ZoneSeen(columnZone))); // 줄이 다 숨으면 빈 제목만 남아서 칸째로 숨김
                for (int i = 0; i < count; i++)
                {
                    if (ObjectManager.ZoneOfObject(i) != zone) continue;
                    int index = i; // 이 줄의 오브젝트
                    AddCountStat(column, ObjectManager.StaticObjectNameAt(index),
                        () => GameStats.BrokenOf(index), Times,
                        () => ObjectManager.Instance != null && ObjectManager.Instance.IsUnlocked(index), index); // 해금 전 오브젝트는 숨김
                }
            }
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
    private readonly Dictionary<VisualElement, System.Func<bool>> _statPageGates = new Dictionary<VisualElement, System.Func<bool>>(); // 조건이 있는 쪽 (false면 넘길 때 건너뜀)
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
        var open = _statPages.FindAll(p => !_statPageGates.TryGetValue(p, out var gate) || gate()); // 지금 볼 수 있는 쪽
        _statPage = (page % open.Count + open.Count) % open.Count;
        foreach (var p in _statPages)
            p.style.display = p == open[_statPage] ? DisplayStyle.Flex : DisplayStyle.None;
        _statPageLabel.text = $"{_statPage + 1} / {open.Count}";
    }

    private static VisualElement StatColumn(VisualElement parent, string header)
    {
        var column = new VisualElement();
        column.style.alignItems = Align.Center;
        column.Add(PixelStyle.CardHeader(header));
        parent.Add(column);
        return column;
    }

    private void AddStat(VisualElement column, string label, System.Func<string> get, System.Func<bool> visible = null)
    {
        Label value = AddStatRow(column, label, visible); // 값 칸
        _statRows.Add((value, () => Loc.T(get())));
    }

    // 개수 줄: 돈처럼 숫자가 굴러 올라감
    private void AddCountStat(VisualElement column, string label, System.Func<long> number, System.Func<long, string> format, System.Func<bool> visible = null, int objectIndex = -1)
    {
        _statCounters.Add(new StatCounter { label = AddStatRow(column, label, visible, objectIndex), target = number, format = format });
    }

    private const float StatIconSize = 48f; // 통계 줄 오브젝트 그림 크기
    private readonly List<(VisualElement icon, int index)> _statIcons = new List<(VisualElement, int)>(); // 오브젝트 그림 칸 (그림은 창 열 때 채움)

    // 그림 데이터가 창을 만든 뒤에 들어올 수 있어서 열 때 채움
    private void FillStatIcons()
    {
        if (ObjectManager.Instance == null) return;
        foreach (var (icon, index) in _statIcons)
        {
            if (icon.style.backgroundImage.value.sprite != null) continue;
            Sprite[] stages = ObjectManager.Instance.GetObjectAt(index).healthStages; // 체력 단계별 그림 (0 = 멀쩡한 모습)
            if (stages != null && stages.Length > 0 && stages[0] != null) icon.style.backgroundImage = new StyleBackground(stages[0]);
        }
    }

    private static string Times(long n) => Loc.T("{0}번", NumberFormatUtil.Format(n));

    private Label AddStatRow(VisualElement column, string label, System.Func<bool> visible, int objectIndex = -1)
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

        if (objectIndex >= 0)
        {
            var icon = new VisualElement(); // 오브젝트 그림 (이름 앞)
            icon.style.width = StatIconSize;
            icon.style.height = StatIconSize;
            icon.style.flexShrink = 0;
            icon.style.marginRight = 14;
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            icon.pickingMode = PickingMode.Ignore;
            row.Add(icon);
            _statIcons.Add((icon, objectIndex));
        }

        var name = new Label();
        Loc.Bind(name, label);
        name.style.flexGrow = 1;
        PixelStyle.FitText(name, 36);
        name.style.color = PixelStyle.MutedText.linear;
        row.Add(name);

        var value = new Label();
        value.style.fontSize = 36;
        value.style.flexShrink = 0;
        value.style.marginLeft = 12;
        value.style.color = PixelStyle.Text.linear;
        value.style.transformOrigin = new TransformOrigin(Length.Percent(100), Length.Percent(50)); // 오른쪽 끝 기준으로 커져서 칸 밖으로 안 나감
        row.Add(value);
        if (visible != null) _statGates.Add((row, visible));
        column.Add(row);
        return value;
    }

    private class StatCounter
    {
        public Label label; // 숫자 칸
        public System.Func<long> target; // 실제 값
        public System.Func<long, string> format; // 표시 문자열
        public double shown; // 지금 보이는 값 (굴러 올라가는 중)
        public double rate; // 초당 최소 증가량 (CountUpSeconds 안에 따라붙게)
    }
    private readonly List<StatCounter> _statCounters = new List<StatCounter>(); // 굴러 올라가는 줄들
    private readonly Dictionary<Label, float> _statPulses = new Dictionary<Label, float>(); // 커졌다 작아지는 중인 칸, 남은 시간 (초)
    private const float StatCountUpSpeed = 8f; // 남은 차이를 따라붙는 속도 (돈 표시와 같음)
    private const float StatCountUpSeconds = 0.35f; // 얼마가 늘든 이 안에 다 따라붙음 (초)
    private const float StatCountUpMinPerSecond = 30f; // 작은 값도 너무 느리지 않게 (초당)
    private const float StatPulseDuration = 0.25f; // 커졌다 작아지는 시간 (초, 돈 표시와 같음)
    private const float StatPulseScale = 0.15f; // 가장 클 때 늘어나는 비율

    private void PulseStat(Label label)
    {
        if (!_statPulses.ContainsKey(label)) _statPulses[label] = StatPulseDuration; // 진행 중이면 이어서, 계속 오르면 계속 뜀
    }

    // 매 프레임: 숫자 굴리기 + 커졌다 작아지기
    private void TickStatEffects(float dt)
    {
        foreach (var c in _statCounters)
        {
            long target = c.target(); // 실제 값
            if (c.shown >= target)
            {
                c.rate = 0;
                if (c.shown > target) { c.shown = target; c.label.text = Loc.T(c.format(target)); } // 초기화 등으로 줄었을 때
                continue;
            }
            double gap = target - c.shown; // 남은 차이
            c.rate = System.Math.Max(c.rate, gap / StatCountUpSeconds);
            double step = System.Math.Max(gap * (1 - Mathf.Exp(-StatCountUpSpeed * dt)), System.Math.Max(StatCountUpMinPerSecond, c.rate) * dt); // 이번 프레임 증가량
            c.shown = System.Math.Min(target, c.shown + step);
            c.label.text = Loc.T(c.format((long)c.shown));
            PulseStat(c.label);
        }

        if (_statPulses.Count == 0) return;
        var labels = new List<Label>(_statPulses.Keys); // 도는 중에 지우려고 복사
        foreach (var label in labels)
        {
            float left = _statPulses[label] - dt; // 남은 시간
            float scale = 1f; // 이번 프레임 크기
            if (left <= 0f) _statPulses.Remove(label);
            else
            {
                _statPulses[label] = left;
                scale += StatPulseScale * Mathf.Sin(Mathf.PI * (1f - left / StatPulseDuration));
            }
            label.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }
    }

    private static string Chance(float chance) => $"{chance * 100f:0.##}%";

    private static string UnlockedText(int count, System.Func<int, bool> unlocked)
    {
        int n = 0;
        for (int i = 0; i < count; i++) if (unlocked(i)) n++;
        return $"{n} / {count}";
    }

    private float _statsRefreshTimer; // 다음 통계 갱신까지 남은 시간 (초)
    private const float StatsRefreshInterval = 0.2f; // 열려 있는 동안 갱신 간격 (초)

    // pulse: 열린 뒤 바뀐 값만 뜀 (처음 열 때는 조용히)
    private void RefreshStatValues(bool pulse)
    {
        foreach (var (value, get) in _statRows)
        {
            string text = get(); // 새 값
            if (pulse && text != value.text) PulseStat(value);
            value.text = text;
        }
        foreach (var c in _statCounters)
        {
            if (!pulse) c.shown = c.target(); // 열 때는 굴리지 않고 바로 맞춤
            c.label.text = Loc.T(c.format((long)c.shown)); // 언어/숫자 표기 바뀐 것도 반영
        }
        foreach (var (row, visible) in _statGates) row.style.display = visible() ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // 열려 있는 동안 게임이 계속 돌아서 값도 실시간으로 갱신
    private void RefreshStatsLive()
    {
        if (_statsPanel == null || _statsPanel.style.display != DisplayStyle.Flex) return;
        TickStatEffects(Time.unscaledDeltaTime);
        _statsRefreshTimer -= Time.unscaledDeltaTime;
        if (_statsRefreshTimer > 0f) return;
        _statsRefreshTimer = StatsRefreshInterval;
        RefreshStatValues(true);
        ShowStatPage(_statPage); // 새 맵이 열리면 쪽 수도 갱신
    }

    private void OpenStatsPanel()
    {
        foreach (var label in _statPulses.Keys) label.style.scale = new Scale(Vector3.one); // 닫힐 때 뛰던 칸 원래 크기로
        _statPulses.Clear();
        FillStatIcons();
        RefreshStatValues(false);
        ShowStatPage(0);
        _statsPanel.style.display = DisplayStyle.Flex;
        RefreshButtonRowVisibility();
        UIPointerGuard.SetPopup("Stats", true); // 게임은 계속 돌고 월드 클릭만 막음
        OnSelectorPanelToggled?.Invoke(true);
    }

    private void CloseStatsPanel()
    {
        _statsPanel.style.display = DisplayStyle.None;
        RefreshButtonRowVisibility();
        UIPointerGuard.SetPopup("Stats", false); // 게임은 계속 돌고 월드 클릭만 막음
        OnSelectorPanelToggled?.Invoke(false);
    }
}
