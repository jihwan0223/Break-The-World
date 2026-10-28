using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public partial class SidePanelUI : MonoBehaviour
{
    private class BrowseState
    {
        public int index; // Select를 눌러야 실제로 적용됨
    }

    // 비활성으로 시작해서 Instance 대신 직접 SetActive로 켬
    [SerializeField] private GameObject upgradeTreePanel;
    [SerializeField] private Sprite settingsIcon;

    private VisualElement _panel;
    private Label _panelTitle;
    private VisualElement _weaponContent;
    private VisualElement _objectContent;
    private BrowseState _weaponBrowse = new BrowseState();
    private BrowseState _objectBrowse = new BrowseState();
    private System.Action _refreshWeaponSelector;
    private System.Action _refreshObjectSelector;
    private VisualElement _currentlyOpenContent;
    private ObjectData _objectAtPanelOpen;

    private VisualElement _buttonRow;
    private bool _upgradeTreeOpen;

    private VisualElement _upgradeBadge;
    private VisualElement _crystalBadge;
    private VisualElement _weaponBadge;
    private float _badgeTimer;
    private const float BadgeRefreshInterval = 0.25f; // 초 - 매 프레임 트리 전체를 훑지 않게

    private VisualElement _debugPanel;
    private Label _debugStatus;

    public static event System.Action<bool> OnSelectorPanelToggled;

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

        UpgradeTreeUI.OnTreeToggled += HandleUpgradeTreeToggled;
    }

    void Update()
    {
        RefreshBadges();
        GameStats.PlaySeconds += Time.deltaTime; // timeScale 0 동안은 안 늘어남

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (_demoBox.style.display == DisplayStyle.Flex) CloseDemoBox();
        else if (_panel.style.display == DisplayStyle.Flex) ClosePanel();
        else if (_debugPanel.style.display == DisplayStyle.Flex) CloseDebugPanel();
        else if (_statsPanel.style.display == DisplayStyle.Flex) CloseStatsPanel();
        else if (_confirmBox.style.display == DisplayStyle.Flex) _confirmBox.style.display = DisplayStyle.None;
        else if (_settingsPanel.style.display == DisplayStyle.Flex) CloseSettingsPanel();
        else if (_upgradeTreeOpen) UpgradeTreeUI.Instance?.Close();
        else OpenSettingsPanel();
    }

    void OnDisable()
    {
        UpgradeTreeUI.OnTreeToggled -= HandleUpgradeTreeToggled;
    }

    private void RefreshBadges()
    {
        _badgeTimer -= Time.unscaledDeltaTime;
        if (_badgeTimer > 0f || _buttonRow == null || _buttonRow.style.display == DisplayStyle.None) return;
        _badgeTimer = BadgeRefreshInterval;

        bool upgrade = UpgradeManager.Instance != null && UpgradeManager.Instance.AnyUpgradeAvailable(false);
        bool crystal = UpgradeManager.Instance != null && UpgradeManager.Instance.AnyUpgradeAvailable(true);
        bool weapon = WeaponManager.Instance != null && WeaponManager.Instance.AnyUnlockAffordable();
        _upgradeBadge.style.display = upgrade && !crystal ? DisplayStyle.Flex : DisplayStyle.None;
        _crystalBadge.style.display = crystal ? DisplayStyle.Flex : DisplayStyle.None;
        _weaponBadge.style.display = weapon ? DisplayStyle.Flex : DisplayStyle.None;
        bool stats = UpgradeManager.Instance != null && UpgradeManager.Instance.StatsIsUnlocked;
        _statsButton.style.display = stats ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static VisualElement CreateBadge(bool crystal = false)
    {
        var badge = new VisualElement();
        badge.pickingMode = PickingMode.Ignore; // 버튼 클릭을 가로채지 않게
        badge.style.position = Position.Absolute;
        badge.style.top = -14;
        badge.style.right = 4;
        badge.style.width = 48; // 16px 그림의 3배
        badge.style.height = 48;
        badge.style.backgroundImage = new StyleBackground(crystal ? NoticeBadge.CrystalSprite : NoticeBadge.Sprite);
        badge.style.display = DisplayStyle.None;
        return badge;
    }

    private void HandleUpgradeTreeToggled(bool open)
    {
        _upgradeTreeOpen = open;
        RefreshButtonRowVisibility();
    }

    private void RefreshButtonRowVisibility()
    {
        if (_buttonRow == null) return;

        bool hide = (_panel != null && _panel.style.display == DisplayStyle.Flex)
                    || (_debugPanel != null && _debugPanel.style.display == DisplayStyle.Flex)
                    || (_statsPanel != null && _statsPanel.style.display == DisplayStyle.Flex)
                    || (_settingsPanel != null && _settingsPanel.style.display == DisplayStyle.Flex)
                    || _upgradeTreeOpen;
        _buttonRow.style.display = hide ? DisplayStyle.None : DisplayStyle.Flex;
    }

    void Start()
    {
        if (WeaponManager.Instance != null)
            _weaponBrowse.index = WeaponManager.Instance.EquippedIndex;

        if (ObjectManager.Instance != null)
            _objectBrowse.index = ObjectManager.Instance.EquippedIndex;

        _refreshWeaponSelector?.Invoke();
        _refreshObjectSelector?.Invoke();
    }

    private void Build(VisualElement root)
    {
        root.Clear();
        GameFonts.Apply(root);
        if (GameFonts.UiColored != null)
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiColored); // 기본 글꼴은 글자색이 안 먹음

        // 투명 루트가 클릭을 먹지 않게 - 뒤쪽 업그레이드 트리 노드가 눌리도록
        root.pickingMode = PickingMode.Ignore;

        _panel = CreatePopupFrame(ClosePanel, out _panelTitle);

        _weaponContent = BuildWeaponSection(out _refreshWeaponSelector);
        _panel.Add(_weaponContent);

        _objectContent = BuildSelectorSection(
            _objectBrowse,
            () => ObjectManager.Instance != null ? ObjectManager.Instance.ObjectCount : 0,
            index => ObjectManager.Instance != null ? ObjectManager.Instance.GetObjectAt(index).objectName : "",
            index =>
            {
                if (ObjectManager.Instance == null) return null;
                Sprite[] stages = ObjectManager.Instance.GetObjectAt(index).healthStages;
                return stages != null && stages.Length > 0 ? stages[0] : null;
            },
            () => ObjectManager.Instance != null ? ObjectManager.Instance.EquippedIndex : 0,
            index =>
            {
                Debug.Log("오브젝트 선택 버튼 눌림");
                ObjectManager.Instance?.Equip(index);
            },
            out _refreshObjectSelector,
            index => ObjectManager.Instance != null && !ObjectManager.Instance.IsUnlocked(index),
            GetObjectInfoRows);
        _panel.Add(_objectContent);

        root.Add(_panel);
        BuildDebugPanel(root);
        BuildStatsPanel(root);
        BuildSettingsPanel(root);

        var buttonRow = new VisualElement();
        buttonRow.style.position = Position.Absolute;
        buttonRow.style.right = 20;
        buttonRow.style.top = 20;
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.alignItems = Align.Center;

        buttonRow.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        buttonRow.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);

        var upgradeButton = CreateButton("업그레이드", PixelStyle.Mint, () =>
        {
            Debug.Log("업그레이드 버튼 눌림");
            if (upgradeTreePanel != null) upgradeTreePanel.SetActive(true);
            else Debug.LogWarning("SidePanelUI: Upgrade Tree Panel이 인스펙터에 비어있어서 업그레이드 화면을 못 엶");
        });

        var weaponButton = CreateButton("무기", PixelStyle.Pink, () =>
        {
            Debug.Log("무기 버튼 눌림");
            OpenPanel("무기", _weaponContent);
        });

        var objectButton = CreateButton("도감", PixelStyle.Sky, () =>
        {
            Debug.Log("도감 버튼 눌림");
            OpenPanel("도감", _objectContent);
        });

        _statsButton = CreateButton("통계", PixelStyle.Butter, OpenStatsPanel);
        _statsButton.style.display = DisplayStyle.None;
        Button settingsButton = CreateGearButton();

        _upgradeBadge = CreateBadge();
        upgradeButton.Add(_upgradeBadge);
        _crystalBadge = CreateBadge(crystal: true);
        upgradeButton.Add(_crystalBadge);
        _weaponBadge = CreateBadge();
        weaponButton.Add(_weaponBadge);

        _statsButton.style.marginRight = 12;
        upgradeButton.style.marginRight = 12;
        weaponButton.style.marginRight = 12;
        objectButton.style.marginRight = 12;

        buttonRow.Add(_statsButton);
        buttonRow.Add(upgradeButton);
        buttonRow.Add(weaponButton);
        buttonRow.Add(objectButton);
        buttonRow.Add(settingsButton);
        root.Add(buttonRow);

        BuildDemoBox(root);
        _buttonRow = buttonRow;
    }

    private VisualElement CreatePopupFrame(System.Action onClose, out Label titleLabel)
    {
        var panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.left = 0;
        panel.style.right = 0;
        panel.style.top = 0;
        panel.style.bottom = 0;
        PixelStyle.Frame(panel, PixelStyle.Window, 6f);

        panel.style.display = DisplayStyle.None;

        panel.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        panel.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);

        // 좌상단은 조각 표시와 겹쳐서 가운데 위
        titleLabel = new Label();
        titleLabel.pickingMode = PickingMode.Ignore;
        titleLabel.style.position = Position.Absolute;
        titleLabel.style.top = 22;
        titleLabel.style.left = Length.Percent(50);
        titleLabel.style.translate = new Translate(Length.Percent(-50), 0);
        titleLabel.style.paddingLeft = 48;
        titleLabel.style.paddingRight = 48;
        titleLabel.style.paddingTop = 10;
        titleLabel.style.paddingBottom = 14;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.fontSize = 36;
        PixelStyle.Frame(titleLabel, PixelStyle.Ribbon);
        PixelStyle.OutlinedText(titleLabel, PixelStyle.Ribbon.text);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        panel.Add(titleLabel);

        var closeButton = new Button(onClose) { text = "X" };
        closeButton.style.position = Position.Absolute;
        closeButton.style.top = 16;
        closeButton.style.right = 16;
        closeButton.style.width = 60;
        closeButton.style.height = 60;
        closeButton.style.fontSize = 36;
        closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        PixelStyle.Button(closeButton, PixelStyle.Pink);
        panel.Add(closeButton);

        return panel;
    }

    private void BuildDebugPanel(VisualElement root)
    {
        _debugPanel = CreatePopupFrame(CloseDebugPanel, out Label title);
        title.text = "디버그";

        var list = new VisualElement();
        list.style.position = Position.Absolute;
        list.style.left = 0;
        list.style.right = 0;
        list.style.top = 110;
        list.style.alignItems = Align.Center;

        AddDebugButton(list, "돈 최대", () =>
        {
            CurrencyManager.Instance?.AddPieces(10_000_000_000L);
            CurrencyManager.Instance?.AddCrystals(1000);
            return "조각 100억 개와 결정 1,000개를 지급했습니다";
        });
        AddDebugButton(list, "돈 초기화", () =>
        {
            CurrencyManager.Instance?.ResetAll();
            DebrisPool.Instance?.ClearAll();
            if (PieceTutorial.Instance != null) PieceTutorial.Instance.Done = false;
            return "조각과 결정을 0으로 되돌렸습니다 (첫 파편 튜토리얼 다시 나옴)";
        });
        AddDebugButton(list, "업그레이드 해금", () =>
        {
            UpgradeManager.Instance?.UnlockAllDebug();
            ObjectManager.Instance?.UnlockAllDebug();
            WeaponManager.Instance?.UnlockAllDebug();
            UpgradeTreeUI.Instance?.RefreshAll();
            return "모든 업그레이드, 오브젝트, 무기를 해금했습니다";
        });
        AddDebugButton(list, "업그레이드 만렙", () =>
        {
            UpgradeManager.Instance?.MaxAllDebug();
            ObjectManager.Instance?.MaxAllDebug();
            UpgradeTreeUI.Instance?.RefreshAll();
            return "모든 업그레이드와 오브젝트 획득량을 만렙으로 채웠습니다";
        });
        AddDebugButton(list, "업글 초기화", () =>
        {
            UpgradeManager.Instance?.ResetAll();
            ObjectManager.Instance?.ResetAll();
            UpgradeTreeUI.Instance?.RefreshAll();
            return "업그레이드와 오브젝트 해금을 처음 상태로 되돌렸습니다";
        });
        AddDebugButton(list, "무기 초기화", () =>
        {
            WeaponManager.Instance?.ResetUnlocks();
            _weaponBrowse.index = 0;
            UpgradeTreeUI.Instance?.RefreshAll();
            return "맨손만 남기고 모든 무기를 잠갔습니다";
        });
        AddDebugButton(list, "맵 해금 초기화", () =>
        {
            ZoneManager.Instance?.ResetAll();
            ZoneBreaker.ResetBosses();
            return "존1만 열린 상태로 되돌렸습니다. 책상을 부수면 해금 연출이 나옵니다";
        });
        AddDebugButton(list, "전체 초기화", () =>
        {
            CurrencyManager.Instance?.ResetAll();
            DebrisPool.Instance?.ClearAll();
            if (PieceTutorial.Instance != null) PieceTutorial.Instance.Done = false;
            UpgradeManager.Instance?.ResetAll();
            ObjectManager.Instance?.ResetAll();
            ObjectManager.Instance?.Equip(0);
            WeaponManager.Instance?.ResetUnlocks();
            _weaponBrowse.index = 0;
            ZoneManager.Instance?.ResetAll();
            ZoneBreaker.ResetBosses();
            GameStats.Reset();
            GateReveals.Reset();
            UpgradeTreeUI.Instance?.RefreshAll();
            SaveManager.Instance?.SaveNow();
            return "모든 데이터를 처음 상태로 되돌렸습니다";
        });
        _debugPanel.Add(list);

        _debugStatus = new Label();
        _debugStatus.style.position = Position.Absolute;
        _debugStatus.style.left = 0;
        _debugStatus.style.right = 0;
        _debugStatus.style.bottom = 40;
        _debugStatus.style.fontSize = 36;
        _debugStatus.style.color = PixelStyle.Text.linear;
        _debugStatus.style.unityTextAlign = TextAnchor.MiddleCenter;
        _debugPanel.Add(_debugStatus);

        root.Add(_debugPanel);
    }

    private void AddDebugButton(VisualElement list, string text, System.Func<string> action)
    {
        Button button = CreateButton(text, PixelStyle.Butter, () => _debugStatus.text = action());
        button.style.width = 460;
        button.style.height = 90;
        button.style.marginBottom = 14;
        list.Add(button);
    }

    private void OpenDebugPanel()
    {
        _debugStatus.text = "";
        _debugPanel.style.display = DisplayStyle.Flex;
        RefreshButtonRowVisibility();

        Time.timeScale = 0f;
        OnSelectorPanelToggled?.Invoke(true);
    }

    private void CloseDebugPanel()
    {
        _debugPanel.style.display = DisplayStyle.None;
        RefreshButtonRowVisibility();

        Time.timeScale = 1f;
        OnSelectorPanelToggled?.Invoke(false);
    }

    private VisualElement BuildSelectorSection(
        BrowseState browse,
        System.Func<int> getCount,
        System.Func<int, string> getName,
        System.Func<int, Sprite> getIcon,
        System.Func<int> getEquippedIndex,
        System.Action<int> onSelect,
        out System.Action refresh,
        System.Func<int, bool> isLocked = null,
        System.Func<int, (string label, string value, Color color)[]> getInfoRows = null)
    {
        var content = new VisualElement();
        content.style.position = Position.Absolute;
        content.style.left = 16;
        content.style.right = 16;
        content.style.top = 96;
        content.style.bottom = 16;
        content.style.display = DisplayStyle.None;

        System.Action redraw = null;

        var nameLabel = new Label();
        nameLabel.style.fontSize = 36;
        nameLabel.style.color = PixelStyle.Text.linear;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.marginBottom = 16;

        const float previewImageSize = 600f;
        var previewImage = new Image();
        previewImage.style.width = previewImageSize;
        previewImage.style.height = previewImageSize;
        previewImage.scaleMode = ScaleMode.ScaleToFit;

        var selectButton = new Button() { text = "선택" };
        selectButton.style.width = 360;
        selectButton.style.height = 80;
        selectButton.style.marginTop = 24;
        selectButton.style.fontSize = 24;
        selectButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        PixelStyle.Button(selectButton, PixelStyle.Mint);

        var equippedLabel = new Label("장착됨");
        equippedLabel.style.height = 80;
        equippedLabel.style.marginTop = 24;
        equippedLabel.style.fontSize = 36;
        equippedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        equippedLabel.style.color = PixelStyle.Good.linear;
        equippedLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        var infoCard = CreateSideCard();
        infoCard.style.display = DisplayStyle.None;

        selectButton.clicked += () =>
        {
            if (isLocked != null && isLocked(browse.index))
                return;

            onSelect(browse.index);
            redraw();
        };

        var prevButton = new Button(() =>
        {
            int count = getCount();
            if (count <= 0) return;
            browse.index = Mathf.Max(0, browse.index - 1);
            redraw();
        })
        { text = "<" };

        var nextButton = new Button(() =>
        {
            int count = getCount();
            if (count <= 0) return;
            browse.index = Mathf.Min(count - 1, browse.index + 1);
            redraw();
        })
        { text = ">" };

        var center = new VisualElement();
        center.style.alignItems = Align.Center;
        center.Add(nameLabel);
        center.Add(previewImage);
        center.Add(selectButton);
        center.Add(equippedLabel);
        content.Add(CreateCenterRow(prevButton, center, nextButton));
        content.Add(infoCard);

        redraw = () => RefreshSelectorState(nameLabel, selectButton, equippedLabel, previewImage, infoCard, getName, getIcon, getEquippedIndex, getInfoRows, browse.index, isLocked);
        refresh = redraw;

        return content;
    }

    private static VisualElement CreateCenterRow(Button prevButton, VisualElement center, Button nextButton)
    {
        var row = new VisualElement();
        row.style.flexGrow = 1;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.Center;
        row.style.paddingRight = 220;
        foreach (Button arrow in new[] { prevButton, nextButton })
        {
            arrow.style.width = 140;
            arrow.style.height = 220;
            arrow.style.fontSize = 140;
            arrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            arrow.style.color = PixelStyle.Text.linear;
            arrow.style.backgroundColor = Color.clear;
            SetFrame(arrow, Color.clear, 0, 0);
        }
        prevButton.style.marginRight = 30;
        nextButton.style.marginLeft = 30;
        row.Add(prevButton);
        row.Add(center);
        row.Add(nextButton);
        return row;
    }

    private static VisualElement CreateSideCard()
    {
        var card = new VisualElement();
        card.style.position = Position.Absolute;
        card.style.right = 60;
        card.style.top = Length.Percent(50);
        card.style.translate = new Translate(0, Length.Percent(-50));
        card.style.width = 440;
        card.style.paddingTop = 28;
        card.style.paddingBottom = 16;
        card.style.paddingLeft = 24;
        card.style.paddingRight = 24;
        PixelStyle.Frame(card, PixelStyle.Card);
        return card;
    }

    private static readonly Color GoodColor = new Color(0.5f, 0.9f, 0.5f);
    private static readonly Color BadColor = new Color(0.95f, 0.45f, 0.4f);
    private static readonly Color HiddenColor = new Color(0.5f, 0.52f, 0.6f);

    private static void SetFrame(VisualElement element, Color borderColor, float borderWidth, float radius)
    {
        element.style.borderTopWidth = borderWidth;
        element.style.borderBottomWidth = borderWidth;
        element.style.borderLeftWidth = borderWidth;
        element.style.borderRightWidth = borderWidth;
        element.style.borderTopColor = borderColor;
        element.style.borderBottomColor = borderColor;
        element.style.borderLeftColor = borderColor;
        element.style.borderRightColor = borderColor;
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
    }

    private VisualElement BuildWeaponSection(out System.Action refresh)
    {
        var content = new VisualElement();
        content.style.position = Position.Absolute;
        content.style.left = 16;
        content.style.right = 16;
        content.style.top = 96;
        content.style.bottom = 16;
        content.style.display = DisplayStyle.None;

        System.Action redraw = null;

        var prevButton = new Button(() => { _weaponBrowse.index = Mathf.Max(0, _weaponBrowse.index - 1); redraw(); }) { text = "<" };
        var nextButton = new Button(() => { _weaponBrowse.index = Mathf.Min(WeaponCount() - 1, _weaponBrowse.index + 1); redraw(); }) { text = ">" };

        var nameLabel = new Label();
        nameLabel.style.fontSize = 36;
        nameLabel.style.color = PixelStyle.Text.linear;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.marginBottom = 16;

        const float previewSize = 520f;
        var preview = new Image { scaleMode = ScaleMode.ScaleToFit };
        preview.style.width = previewSize;
        preview.style.height = previewSize;

        var card = CreateSideCard();

        var actionButton = new Button();
        actionButton.style.width = 400;
        actionButton.style.height = 80;
        actionButton.style.marginTop = 24;
        actionButton.style.fontSize = 24;
        actionButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        PixelStyle.Button(actionButton, PixelStyle.Mint);
        actionButton.clicked += () =>
        {
            WeaponManager weapons = WeaponManager.Instance;
            if (weapons == null) return;

            int index = _weaponBrowse.index;
            if (weapons.IsUnlocked(index)) weapons.Equip(index);
            else if (weapons.TryUnlock(index)) weapons.Equip(index);
            redraw();
        };

        var center = new VisualElement();
        center.style.alignItems = Align.Center;
        center.Add(nameLabel);
        center.Add(preview);
        center.Add(actionButton);
        content.Add(CreateCenterRow(prevButton, center, nextButton));
        content.Add(card);

        var slotRow = new VisualElement();
        slotRow.style.flexDirection = FlexDirection.Row;
        slotRow.style.justifyContent = Justify.Center;
        slotRow.style.marginBottom = 12;

        var slots = new List<Image>();
        for (int i = 0; i < WeaponCount(); i++)
        {
            int index = i;
            var slot = new Image { scaleMode = ScaleMode.ScaleToFit };
            slot.style.width = 96;
            slot.style.height = 96;
            slot.style.marginLeft = 6;
            slot.style.marginRight = 6;
            slot.style.backgroundColor = PixelStyle.Card.face.linear;
            slot.RegisterCallback<ClickEvent>(_ => { _weaponBrowse.index = index; redraw(); });
            slot.AddToClassList(DebrisPool.ClickSoundClass);
            slots.Add(slot);
            slotRow.Add(slot);
        }
        content.Add(slotRow);

        redraw = () => RefreshWeaponSection(prevButton, nextButton, preview, nameLabel, card, actionButton, slots);
        refresh = redraw;
        return content;
    }

    private static int WeaponCount() => WeaponManager.Instance != null ? WeaponManager.Instance.WeaponCount : 0;

    private void RefreshWeaponSection(Button prevButton, Button nextButton, Image preview, Label nameLabel,
        VisualElement card, Button actionButton, List<Image> slots)
    {
        WeaponManager weapons = WeaponManager.Instance;
        if (weapons == null) return;

        int index = Mathf.Clamp(_weaponBrowse.index, 0, weapons.WeaponCount - 1);
        _weaponBrowse.index = index;
        WeaponData data = weapons.GetWeaponAt(index);

        bool unlocked = weapons.IsUnlocked(index);
        bool equipped = weapons.EquippedIndex == index;
        bool buyable = !unlocked && weapons.IsUnlockOrderMet(index);
        bool hidden = !unlocked && !buyable;

        prevButton.SetEnabled(index > 0);
        nextButton.SetEnabled(index < weapons.WeaponCount - 1);

        preview.sprite = data.icon;
        preview.tintColor = unlocked ? Color.white : Color.black;
        nameLabel.text = hidden ? "???" : data.weaponName;

        int bonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.GetClickDamageBonus(index) : 0;
        long cost = buyable ? weapons.GetUnlockCost(index) : 0;

        card.Clear();
        card.Add(PixelStyle.CardHeader("능력치"));
        card.Add(CreateInfoRow("공격력", hidden ? "???" : NumberFormatUtil.Format(data.clickDamage), hidden ? HiddenColor : Color.white));
        card.Add(CreateInfoRow("강화 보너스", hidden ? "???" : $"+{NumberFormatUtil.Format(bonus)}", hidden ? HiddenColor : Color.white));
        card.Add(CreateInfoRow("클릭당 합계", hidden ? "???" : NumberFormatUtil.Format(data.clickDamage + bonus), hidden ? HiddenColor : Color.white));
        if (buyable)
            card.Add(CreateInfoRow("해금 가격", $"{NumberFormatUtil.Format(cost)} 조각", Color.white));
        card.Add(CreateInfoRow("상태", equipped ? "장착 중" : unlocked ? "해금됨" : "잠김", equipped || unlocked ? GoodColor : BadColor));

        actionButton.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
        if (equipped)
        {
            actionButton.text = "장착 중";
            actionButton.SetEnabled(false);
            actionButton.style.opacity = 1f;
        }
        else if (unlocked)
        {
            actionButton.text = "장착하기";
            actionButton.SetEnabled(true);
            actionButton.style.opacity = StyleKeyword.Null;
        }
        else
        {
            long have = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetPieces() : 0;
            actionButton.text = $"해금 ({NumberFormatUtil.Format(cost)} 조각)";
            actionButton.SetEnabled(have >= cost);
            actionButton.style.opacity = StyleKeyword.Null;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].sprite = weapons.GetWeaponAt(i).icon;
            slots[i].tintColor = weapons.IsUnlocked(i) ? Color.white : Color.black;
            Color border = (i == weapons.EquippedIndex ? PixelStyle.Highlight : i == index ? PixelStyle.Text : PixelStyle.Card.outline).linear;
            SetFrame(slots[i], border, i == index || i == weapons.EquippedIndex ? 4 : 2, 0);
        }
    }

    private VisualElement CreateInfoRow(string label, string value, Color valueColor)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.marginBottom = 26;

        var labelText = new Label(label);
        labelText.style.fontSize = 36;
        labelText.style.color = PixelStyle.MutedText.linear;
        row.Add(labelText);

        var valueText = new Label(value);
        valueText.style.fontSize = 36;
        valueText.style.unityFontStyleAndWeight = FontStyle.Bold;
        valueText.style.color = (valueColor == GoodColor ? PixelStyle.Good : valueColor == BadColor ? PixelStyle.Bad
            : valueColor == HiddenColor ? PixelStyle.Hidden : PixelStyle.Text).linear;
        row.Add(valueText);

        return row;
    }

    private (string label, string value, Color color)[] GetObjectInfoRows(int index)
    {
        var manager = ObjectManager.Instance;
        if (manager == null) return null;

        Color hidden = new Color(0.5f, 0.52f, 0.6f);
        Color good = new Color(0.5f, 0.9f, 0.5f);
        Color bad = new Color(0.95f, 0.45f, 0.4f);

        bool locked = !manager.IsUnlocked(index);

        var rows = new List<(string label, string value, Color color)>
        {
            ("지역", locked ? "???" : ObjectManager.ZoneNameOf(index), locked ? hidden : Color.white),
            ("체력", locked ? "???" : NumberFormatUtil.Format(ObjectManager.MaxHPOf(index)), locked ? hidden : Color.white),
            ("획득 조각",locked ? "???" : $"{NumberFormatUtil.Format(manager.ExpectedPieces(index))}개", locked ? hidden : Color.white),
            ("상태", locked ? "잠김" : "해금 완료", locked ? bad : good),
        };

        return rows.ToArray();
    }

    private void RefreshSelectorState(
        Label nameLabel,
        Button selectButton,
        Label equippedLabel,
        Image previewImage,
        VisualElement infoCard,
        System.Func<int, string> getName,
        System.Func<int, Sprite> getIcon,
        System.Func<int> getEquippedIndex,
        System.Func<int, (string label, string value, Color color)[]> getInfoRows,
        int index,
        System.Func<int, bool> isLocked = null)
    {
        bool locked = isLocked != null && isLocked(index);
        nameLabel.text = locked ? "???" : getName(index);
        previewImage.sprite = getIcon(index);
        previewImage.tintColor = locked ? Color.black : Color.white;

        var rows = getInfoRows?.Invoke(index);
        infoCard.Clear();
        infoCard.Add(PixelStyle.CardHeader("도감 정보"));
        infoCard.style.display = rows == null ? DisplayStyle.None : DisplayStyle.Flex;
        if (rows != null)
            foreach (var row in rows)
                infoCard.Add(CreateInfoRow(row.label, row.value, row.color));

        if (getInfoRows != null)
        {
            selectButton.style.display = DisplayStyle.None;
            equippedLabel.style.display = DisplayStyle.None;
            return;
        }

        selectButton.text = "선택";

        bool isEquipped = getEquippedIndex() == index;
        selectButton.style.display = isEquipped ? DisplayStyle.None : DisplayStyle.Flex;
        equippedLabel.style.display = isEquipped ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private const float NavButtonPunchAmount = 0.15f;
    private const float NavButtonPunchDuration = 0.12f;

    private Button CreateButton(string text, PixelStyle.Tone tone, System.Action onClick)
    {
        // 펀치가 다 보인 뒤에 동작 실행 (먼저 하면 버튼이 바로 사라짐)
        // 이 패널에선 텍스트 color가 늘 흰색으로 고정되는 문제가 있어 원래 스타일 유지
        var button = new Button { text = text };
        button.clicked += () => StartCoroutine(PunchThenInvoke(button, NavButtonPunchDuration, NavButtonPunchAmount, onClick));
        button.style.width = 300;
        button.style.height = 110;
        button.style.fontSize = 36;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        PixelStyle.Button(button, tone);
        return button;
    }

    private IEnumerator PunchThenInvoke(VisualElement element, float duration, float punch, System.Action onClick)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // timeScale 0이어도 재생
            float p = Mathf.Clamp01(t / duration);
            float s = 1f + punch * Mathf.Sin(p * Mathf.PI);
            element.style.scale = new Scale(new Vector3(s, s, 1f));
            yield return null;
        }
        element.style.scale = new Scale(Vector3.one);
        onClick?.Invoke();
    }

    private void OpenPanel(string title, VisualElement contentToShow)
    {
        _panelTitle.text = title;
        _currentlyOpenContent = contentToShow;
        _weaponContent.style.display = contentToShow == _weaponContent ? DisplayStyle.Flex : DisplayStyle.None;
        _objectContent.style.display = contentToShow == _objectContent ? DisplayStyle.Flex : DisplayStyle.None;
        _panel.style.display = DisplayStyle.Flex;
        RefreshButtonRowVisibility();

        Time.timeScale = 0f; // 열려 있는 동안 게임 정지

        OnSelectorPanelToggled?.Invoke(true);

        _refreshWeaponSelector?.Invoke();
        _refreshObjectSelector?.Invoke();

        // 팝업이 화면을 가려서 전환 연출은 닫을 때 재생
        if (contentToShow == _objectContent && ObjectManager.Instance != null)
            _objectAtPanelOpen = ObjectManager.Instance.CurrentObject;
    }

    private void ClosePanel()
    {
        _panel.style.display = DisplayStyle.None;
        RefreshButtonRowVisibility();

        Time.timeScale = 1f;

        OnSelectorPanelToggled?.Invoke(false);

        if (_currentlyOpenContent == _objectContent && ObjectManager.Instance != null)
        {
            ObjectData current = ObjectManager.Instance.CurrentObject;

            if (current != _objectAtPanelOpen)
                ObjectSwapController.Instance?.TriggerSwap(_objectAtPanelOpen, current);
        }
    }
}
