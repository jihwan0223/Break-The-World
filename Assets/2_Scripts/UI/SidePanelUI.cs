using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SidePanelUI : MonoBehaviour
{
    // 화살표로 둘러보는 중인 인덱스를 들고 있는 작은 상태 객체 (무기용/오브젝트용 각각 하나씩)
    private class BrowseState
    {
        public int index; // 지금 화면에 미리보기로 표시 중인 인덱스 (Select를 눌러야 실제로 적용됨)
    }

    // Canvas로 만든 업그레이드 화면 전체 오브젝트 - 처음엔 인스펙터에서 꺼둔(비활성) 상태로 시작해야 함.
    // "업그레이드" 버튼을 누르면 이 오브젝트를 직접 SetActive(true)로 켬 - UpgradeTreeUI.Instance를 거치지 않는 이유는
    // 비활성 오브젝트에서는 Awake가 안 돌아서, 맨 처음 열 때는 Instance가 아직 null이라 그걸로 열려고 하면 에러가 나기 때문
    [SerializeField] private GameObject upgradeTreePanel;

    private VisualElement _panel; // 튀어나오는 빈 팝업 패널 (Weapon/Object 전용)
    private Label _panelTitle; // 팝업 가운데 위 제목 ("무기" / "도감")
    private VisualElement _weaponContent; // 무기 팝업일 때만 보이는 영역
    private VisualElement _objectContent; // 오브젝트 팝업일 때만 보이는 영역
    private BrowseState _weaponBrowse = new BrowseState(); // 무기 화살표 미리보기 상태
    private BrowseState _objectBrowse = new BrowseState(); // 오브젝트 화살표 미리보기 상태
    private System.Action _refreshWeaponSelector; // 무기 팝업의 이름/Select-Equipped 표시를 다시 그리는 함수
    private System.Action _refreshObjectSelector; // 오브젝트 팝업의 이름/Select-Equipped 표시를 다시 그리는 함수
    private VisualElement _currentlyOpenContent; // 지금 열려있는 팝업 내용 (닫을 때 오브젝트 전환 여부 판단용)
    private ObjectData _objectAtPanelOpen; // Object 팝업을 열었을 때 장착돼있던 오브젝트 (닫을 때와 비교해서 바뀌었는지 확인)

    private VisualElement _buttonRow; // 우상단 Upgrade/Weapon/Object 버튼 줄 - 셋 중 아무거나 하나라도 열려있으면 전부 숨김
    private bool _upgradeTreeOpen; // Canvas 업그레이드 화면이 지금 열려있는지 (UpgradeTreeUI.OnTreeToggled로 갱신됨)

    private VisualElement _upgradeBadge; // 업그레이드 버튼의 빨간 느낌표 - 조각으로 살 수 있는 노드가 있을 때만 보임
    private VisualElement _crystalBadge; // 업그레이드 버튼의 노란 느낌표 - 결정으로 살 수 있는 노드가 있을 때만 보임 (빨간 느낌표 대신 같은 자리에)
    private VisualElement _weaponBadge; // 무기 버튼의 느낌표 - 지금 해금할 수 있는 무기가 있을 때만 보임
    private float _badgeTimer; // 느낌표를 다시 판단할 때까지 남은 시간(초)
    private const float BadgeRefreshInterval = 0.25f; // 느낌표 판단 주기(초) - 매 프레임 트리 전체를 훑지 않게

    private VisualElement _debugPanel; // 디버그 팝업 (테스트용 버튼 모음)
    private Label _debugStatus; // 디버그 버튼을 누른 결과를 보여주는 줄

    // Weapon/Object 팝업(_panel)이 열리면 true, 닫히면 false로 전달 - DebrisPool 등이 구독해서 팝업 열릴 때 바닥 조각을 치움
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

        // Canvas 업그레이드 화면이 열리고 닫힐 때도 버튼 줄을 같이 숨기고 보여주기 위해 구독
        UpgradeTreeUI.OnTreeToggled += HandleUpgradeTreeToggled;
    }

    // ESC로 지금 열려있는 창(무기/도감 팝업, 디버그, 업그레이드 화면) 닫기
    void Update()
    {
        RefreshBadges();

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (_panel.style.display == DisplayStyle.Flex) ClosePanel();
        else if (_debugPanel.style.display == DisplayStyle.Flex) CloseDebugPanel();
        else if (_upgradeTreeOpen) UpgradeTreeUI.Instance?.Close();
    }

    void OnDisable()
    {
        UpgradeTreeUI.OnTreeToggled -= HandleUpgradeTreeToggled;
    }

    // 지금 재화로 살 수 있는 게 있으면 업그레이드/무기 버튼에 느낌표를 띄움. 버튼 줄이 보일 때만, 일정 주기로 판단
    private void RefreshBadges()
    {
        _badgeTimer -= Time.unscaledDeltaTime;
        if (_badgeTimer > 0f || _buttonRow == null || _buttonRow.style.display == DisplayStyle.None) return;
        _badgeTimer = BadgeRefreshInterval;

        bool upgrade = UpgradeManager.Instance != null && UpgradeManager.Instance.AnyUpgradeAvailable(false); // 조각으로 살 수 있는 노드가 있는지
        bool crystal = UpgradeManager.Instance != null && UpgradeManager.Instance.AnyUpgradeAvailable(true); // 결정으로 살 수 있는 노드가 있는지
        bool weapon = WeaponManager.Instance != null && WeaponManager.Instance.AnyUnlockAffordable(); // 해금할 수 있는 무기가 있는지
        // 느낌표는 하나만: 결정으로 살 수 있으면 노란 느낌표가 빨간 느낌표 대신 뜸
        _upgradeBadge.style.display = upgrade && !crystal ? DisplayStyle.Flex : DisplayStyle.None;
        _crystalBadge.style.display = crystal ? DisplayStyle.Flex : DisplayStyle.None;
        _weaponBadge.style.display = weapon ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // 버튼 오른쪽 위에 붙는 느낌표 (처음엔 숨김) - 트리 노드의 느낌표와 같은 그림. crystal이면 노란 느낌표
    private static VisualElement CreateBadge(bool crystal = false)
    {
        var badge = new VisualElement();
        badge.pickingMode = PickingMode.Ignore; // 버튼 클릭을 가로채지 않게
        badge.style.position = Position.Absolute;
        badge.style.top = -14;
        badge.style.right = 4; // 옆 버튼에 가려지지 않게 버튼 안쪽에 둠
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

    // Weapon/Object 팝업이나 Canvas 업그레이드 화면 - 이 중 하나라도 열려있으면 버튼 줄 전체를 숨김.
    // 다른 버튼을 또 눌러서 팝업이 겹쳐 열리는 걸 막음
    private void RefreshButtonRowVisibility()
    {
        if (_buttonRow == null) return;

        bool hide = (_panel != null && _panel.style.display == DisplayStyle.Flex)
                    || (_debugPanel != null && _debugPanel.style.display == DisplayStyle.Flex)
                    || _upgradeTreeOpen;
        _buttonRow.style.display = hide ? DisplayStyle.None : DisplayStyle.Flex;
    }

    void Start()
    {
        if (WeaponManager.Instance != null)
            _weaponBrowse.index = WeaponManager.Instance.EquippedIndex;

        if (ObjectManager.Instance != null)
            _objectBrowse.index = ObjectManager.Instance.EquippedIndex;

        // Build() 시점엔 매니저 값을 아직 못 읽었을 수 있어서, 인덱스를 다시 맞춘 뒤 여기서 한 번 더 갱신
        _refreshWeaponSelector?.Invoke();
        _refreshObjectSelector?.Invoke();
    }

    private void Build(VisualElement root)
    {
        root.Clear();
        GameFonts.Apply(root);
        if (GameFonts.UiColored != null)
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiColored); // 기본 글꼴은 글자색이 안 먹어서(늘 흰색) 크림 바탕 위 갈색 글씨를 쓰려면 색이 먹는 글꼴이어야 함

        // 투명한 루트가 화면 전체 클릭을 먹어서 뒤쪽 Canvas(업그레이드 트리)의 노드 클릭이 안 되는 문제 방지 -
        // 실제 버튼/팝업 패널은 각자 picking 모드를 그대로 유지하므로 그 위 클릭은 정상 동작함
        root.pickingMode = PickingMode.Ignore;

        // 화면 전체를 꽉 채우는 팝업 패널 - 업그레이드 화면과 동일하게 전체화면으로 덮고 X 버튼으로 닫음
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

        // Upgrade/Weapon/Object 버튼 3개가 화면 우상단에 일렬로 나오는 가로줄
        // (업그레이드 페이지 자체는 유저가 Canvas로 새로 만든 UpgradeTreeUI가 담당 - 여기선 그걸 여는 버튼만 있음)
        var buttonRow = new VisualElement();
        buttonRow.style.position = Position.Absolute;
        buttonRow.style.right = 20;
        buttonRow.style.top = 20;
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.alignItems = Align.Center;

        // 버튼 줄 위에 포인터가 있는 동안도 마찬가지로 월드 클릭을 막음
        buttonRow.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        buttonRow.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);

        var upgradeButton = CreateButton("업그레이드", PixelStyle.Mint, () =>
        {
            Debug.Log("업그레이드 버튼 눌림");
            // Canvas로 만든 업그레이드 화면을 직접 켬 (닫기는 그 화면 안의 X 버튼 -> UpgradeTreeUI.Close()가 담당)
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

        var debugButton = CreateButton("디버그", PixelStyle.Butter, () =>
        {
            Debug.Log("디버그 버튼 눌림");
            OpenDebugPanel();
        });

        _upgradeBadge = CreateBadge();
        upgradeButton.Add(_upgradeBadge);
        _crystalBadge = CreateBadge(crystal: true);
        upgradeButton.Add(_crystalBadge);
        _weaponBadge = CreateBadge();
        weaponButton.Add(_weaponBadge);

        debugButton.style.marginRight = 12; // 맨 왼쪽에 둠 - 줄이 오른쪽 기준 정렬이라 기존 3개 위치가 안 바뀜
        upgradeButton.style.marginRight = 12;
        weaponButton.style.marginRight = 12;

        buttonRow.Add(debugButton);
        buttonRow.Add(upgradeButton);
        buttonRow.Add(weaponButton);
        buttonRow.Add(objectButton);
        root.Add(buttonRow);

        _buttonRow = buttonRow; // OpenPanel/ClosePanel/업그레이드 화면 토글에서 보이기/숨기기 위해 저장해둠
    }

    // 화면 전체를 꽉 채우는 팝업 틀 - 업그레이드 화면과 동일하게 전체화면으로 덮고 좌상단 제목, 우상단 X 버튼(onClose)이 있음
    private VisualElement CreatePopupFrame(System.Action onClose, out Label titleLabel)
    {
        var panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.left = 0;
        panel.style.right = 0;
        panel.style.top = 0;
        panel.style.bottom = 0;
        PixelStyle.Frame(panel, PixelStyle.Window, 6f); // 픽셀 창: 크림 바탕 + 갈색 외곽선 (무기/도감/디버그 공통)

        panel.style.display = DisplayStyle.None;

        // 이 패널 위에 포인터가 있는 동안은 뒤쪽 월드 오브젝트가 클릭되지 않도록 플래그를 켜고 끔
        panel.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        panel.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);

        // 제목은 가운데 위 - 좌상단은 조각/결정 표시(PieceUI)가 항상 위에 떠 있어서 겹침
        titleLabel = new Label();
        titleLabel.pickingMode = PickingMode.Ignore;
        titleLabel.style.position = Position.Absolute;
        titleLabel.style.top = 22; // 창 외곽선 안쪽
        titleLabel.style.left = Length.Percent(50);
        titleLabel.style.translate = new Translate(Length.Percent(-50), 0); // 글자 길이만큼만 리본이 생기게 가운데 정렬
        titleLabel.style.paddingLeft = 48;
        titleLabel.style.paddingRight = 48;
        titleLabel.style.paddingTop = 10;
        titleLabel.style.paddingBottom = 14; // 리본 아래 그늘 줄만큼 더
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.fontSize = 36;
        PixelStyle.Frame(titleLabel, PixelStyle.Ribbon); // 분홍 리본
        PixelStyle.OutlinedText(titleLabel, PixelStyle.Ribbon.text); // 흰 글씨 + 진한 그림자
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        panel.Add(titleLabel);

        // 업그레이드 화면의 닫기 버튼처럼 배경 없이 흰 글자 X만
        var closeButton = new Button(onClose) { text = "X" };
        closeButton.style.position = Position.Absolute;
        closeButton.style.top = 16;
        closeButton.style.right = 16;
        closeButton.style.width = 60;
        closeButton.style.height = 60;
        closeButton.style.fontSize = 36;
        closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        PixelStyle.Button(closeButton, PixelStyle.Pink); // 픽셀 입체 버튼
        panel.Add(closeButton);

        return panel;
    }

    // 디버그 팝업 - 다른 팝업과 같은 전체 화면 모양. 테스트용 버튼을 세로로 모아둠 (원래 업그레이드 화면 아래쪽에 있던 것들)
    private void BuildDebugPanel(VisualElement root)
    {
        _debugPanel = CreatePopupFrame(CloseDebugPanel, out Label title);
        title.text = "디버그";

        var list = new VisualElement(); // 버튼 세로 목록
        list.style.position = Position.Absolute;
        list.style.left = 0;
        list.style.right = 0;
        list.style.top = 110; // 제목 리본 아래부터
        list.style.alignItems = Align.Center;

        // 각 동작은 눌린 결과를 한 줄 문구로 돌려줌 - 화면이 팝업에 가려져서 눌렀는지 알 수 없기 때문
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
            if (PieceTutorial.Instance != null) PieceTutorial.Instance.Done = false; // 첫 파편 튜토리얼도 다시 볼 수 있게
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
            // 위 초기화들을 한 번에 - 새 게임을 시작한 것과 같은 상태로 만들고 바로 저장함
            CurrencyManager.Instance?.ResetAll();
            DebrisPool.Instance?.ClearAll();
            if (PieceTutorial.Instance != null) PieceTutorial.Instance.Done = false;
            UpgradeManager.Instance?.ResetAll();
            ObjectManager.Instance?.ResetAll();
            ObjectManager.Instance?.Equip(0); // 잠긴 오브젝트를 들고 있지 않게 달걀로
            WeaponManager.Instance?.ResetUnlocks();
            _weaponBrowse.index = 0;
            ZoneManager.Instance?.ResetAll();
            ZoneBreaker.ResetBosses();
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

    // 디버그 목록에 버튼 하나 추가 - 누르면 action을 실행하고 돌려받은 문구를 아래 줄에 표시
    private void AddDebugButton(VisualElement list, string text, System.Func<string> action)
    {
        Button button = CreateButton(text, PixelStyle.Butter, () => _debugStatus.text = action());
        button.style.width = 460; // "업그레이드 초기화"처럼 긴 글자도 들어가도록 기본 폭보다 넓게
        button.style.height = 90; // 8개가 화면 안에 다 들어가게 기본보다 낮게
        button.style.marginBottom = 14;
        list.Add(button);
    }

    private void OpenDebugPanel()
    {
        _debugStatus.text = "";
        _debugPanel.style.display = DisplayStyle.Flex;
        RefreshButtonRowVisibility(); // 팝업이 열렸으니 버튼 줄 숨김

        Time.timeScale = 0f; // 다른 팝업과 동일 - 열려있는 동안 게임 시간 정지
        OnSelectorPanelToggled?.Invoke(true); // ZoneArrowsUI 등에게 팝업 열림을 알림
    }

    private void CloseDebugPanel()
    {
        _debugPanel.style.display = DisplayStyle.None;
        RefreshButtonRowVisibility(); // 팝업이 닫혔으니 버튼 줄 다시 보임

        Time.timeScale = 1f;
        OnSelectorPanelToggled?.Invoke(false);
    }

    // 팝업 안에 들어갈 전체 영역: 화면 가운데에 < 이름/큰 이미지/선택 버튼 >, 오른쪽에 설명 카드.
    // 무기/오브젝트 팝업이 완전히 같은 모양을 공유하도록 콜백만 받아서 공용으로 구성함.
    private VisualElement BuildSelectorSection(
        BrowseState browse,
        System.Func<int> getCount,
        System.Func<int, string> getName,
        System.Func<int, Sprite> getIcon,
        System.Func<int> getEquippedIndex,
        System.Action<int> onSelect,
        out System.Action refresh,
        System.Func<int, bool> isLocked = null, // 잠긴 항목인지 - null이면 잠김 개념 자체가 없음
        System.Func<int, (string label, string value, Color color)[]> getInfoRows = null) // 도감 정보 카드의 (항목, 값, 값 색) 줄들. null이면 카드 없음
    {
        var content = new VisualElement();
        content.style.position = Position.Absolute;
        content.style.left = 16;
        content.style.right = 16;
        content.style.top = 96; // 제목 리본 아래부터
        content.style.bottom = 16;
        content.style.display = DisplayStyle.None;

        System.Action redraw = null; // 이름/이미지/버튼/정보 카드를 현재 browse.index 기준으로 다시 그리는 함수 (아래 끝에서 채움)

        // 가운데: < [이름 / 큰 미리보기 / 선택 버튼] > , 오른쪽: 정보 카드 (Keep on Mining처럼 그림이 화면 가운데, 설명은 오른쪽)
        var nameLabel = new Label();
        nameLabel.style.fontSize = 36;
        nameLabel.style.color = PixelStyle.Text.linear;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.marginBottom = 16;

        const float previewImageSize = 600f; // 미리보기 액자 한 변
        var previewImage = new Image(); // 오브젝트 그림만 (뒤 액자 없음)
        previewImage.style.width = previewImageSize;
        previewImage.style.height = previewImageSize;
        previewImage.scaleMode = ScaleMode.ScaleToFit;

        // 선택 버튼 / 장착됨 표시 - 미리보기 아래. 장착 여부에 따라 둘 중 하나만 보임 (도감 모드에선 둘 다 숨김)
        var selectButton = new Button() { text = "선택" };
        selectButton.style.width = 360;
        selectButton.style.height = 64;
        selectButton.style.marginTop = 24;
        selectButton.style.fontSize = 24;
        selectButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        PixelStyle.Button(selectButton, PixelStyle.Mint); // 픽셀 입체 버튼

        var equippedLabel = new Label("장착됨");
        equippedLabel.style.height = 64;
        equippedLabel.style.marginTop = 24;
        equippedLabel.style.fontSize = 36;
        equippedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        equippedLabel.style.color = PixelStyle.Good.linear;
        equippedLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        var infoCard = CreateSideCard(); // 오른쪽 정보 카드
        infoCard.style.display = DisplayStyle.None;

        selectButton.clicked += () =>
        {
            if (isLocked != null && isLocked(browse.index))
                return; // 잠긴 오브젝트는 여기서 선택 안 됨 - 업그레이드 트리에서 먼저 해금해야 함

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

        var center = new VisualElement(); // 이름 / 미리보기 / 버튼 세로 묶음
        center.style.alignItems = Align.Center;
        center.Add(nameLabel);
        center.Add(previewImage);
        center.Add(selectButton);
        center.Add(equippedLabel);
        content.Add(CreateCenterRow(prevButton, center, nextButton));
        content.Add(infoCard);

        // 외부(Start, OpenPanel)에서 필요할 때마다 최신 상태로 다시 그릴 수 있도록 넘겨줌
        redraw = () => RefreshSelectorState(nameLabel, selectButton, equippedLabel, previewImage, infoCard, getName, getIcon, getEquippedIndex, getInfoRows, browse.index, isLocked);
        refresh = redraw;

        return content;
    }

    // 팝업 가운데 줄: 화면 가운데에 < [내용] > (화살표는 배경 없이 큰 갈색 글자)
    private static VisualElement CreateCenterRow(Button prevButton, VisualElement center, Button nextButton)
    {
        var row = new VisualElement(); // 가운데 정렬 줄
        row.style.flexGrow = 1;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.Center;
        row.style.paddingRight = 220; // 오른쪽 설명 카드에 화살표가 붙지 않게 가운데 묶음을 살짝 왼쪽으로
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

    // 팝업 오른쪽 설명 카드 - 화면 오른쪽, 세로 가운데
    private static VisualElement CreateSideCard()
    {
        var card = new VisualElement(); // 설명 카드
        card.style.position = Position.Absolute;
        card.style.right = 60;
        card.style.top = Length.Percent(50);
        card.style.translate = new Translate(0, Length.Percent(-50)); // 세로 가운데
        card.style.width = 440; // 가로는 좁게 (36px 글씨 한 줄이 들어가는 만큼)
        card.style.paddingTop = 28; // 내용만큼만 높아지되 위아래 여백을 넉넉히
        card.style.paddingBottom = 16;
        card.style.paddingLeft = 24;
        card.style.paddingRight = 24;
        PixelStyle.Frame(card, PixelStyle.Card); // 픽셀 카드
        return card;
    }

    private static readonly Color GoodColor = new Color(0.5f, 0.9f, 0.5f); // 장착 중/해금 완료 표시색
    private static readonly Color BadColor = new Color(0.95f, 0.45f, 0.4f); // 잠김 표시색
    private static readonly Color HiddenColor = new Color(0.5f, 0.52f, 0.6f); // ???로 가린 값의 색

    // 테두리 + 둥근 모서리를 한 번에 입힘
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

    // 무기 팝업: 화면 가운데에 < 이름/큰 미리보기/장착·해금 버튼 >, 오른쪽에 능력치 카드, 아래에 무기 슬롯 줄
    private VisualElement BuildWeaponSection(out System.Action refresh)
    {
        var content = new VisualElement();
        content.style.position = Position.Absolute;
        content.style.left = 16;
        content.style.right = 16;
        content.style.top = 96; // 제목 리본 아래부터
        content.style.bottom = 16;
        content.style.display = DisplayStyle.None;

        System.Action redraw = null; // 현재 _weaponBrowse.index 기준으로 전체를 다시 그림 (아래 끝에서 채움)

        // 가운데: < [이름 / 큰 미리보기 / 장착·해금 버튼] > , 오른쪽: 능력치 카드 (Keep on Mining처럼 그림이 화면 가운데, 설명은 오른쪽)
        var prevButton = new Button(() => { _weaponBrowse.index = Mathf.Max(0, _weaponBrowse.index - 1); redraw(); }) { text = "<" };
        var nextButton = new Button(() => { _weaponBrowse.index = Mathf.Min(WeaponCount() - 1, _weaponBrowse.index + 1); redraw(); }) { text = ">" };

        var nameLabel = new Label();
        nameLabel.style.fontSize = 36;
        nameLabel.style.color = PixelStyle.Text.linear;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.marginBottom = 16;

        const float previewSize = 520f; // 미리보기 액자 한 변 (아래 슬롯 줄 자리를 남김)
        var preview = new Image { scaleMode = ScaleMode.ScaleToFit }; // 무기 그림만 (뒤 액자 없음)
        preview.style.width = previewSize;
        preview.style.height = previewSize;

        var card = CreateSideCard(); // 오른쪽 능력치 카드

        var actionButton = new Button();
        actionButton.style.width = 400;
        actionButton.style.height = 64;
        actionButton.style.marginTop = 24;
        actionButton.style.fontSize = 24;
        actionButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        PixelStyle.Button(actionButton, PixelStyle.Mint); // 픽셀 입체 버튼 (조각이 모자라 못 누르면 흐려짐)
        actionButton.clicked += () =>
        {
            WeaponManager weapons = WeaponManager.Instance; // 장착/해금 처리
            if (weapons == null) return;

            int index = _weaponBrowse.index; // 지금 보고 있는 무기
            if (weapons.IsUnlocked(index)) weapons.Equip(index);
            else weapons.TryUnlock(index);
            redraw();
        };

        var center = new VisualElement(); // 이름 / 미리보기 / 버튼 세로 묶음
        center.style.alignItems = Align.Center;
        center.Add(nameLabel);
        center.Add(preview);
        center.Add(actionButton);
        content.Add(CreateCenterRow(prevButton, center, nextButton));
        content.Add(card);

        // 아래 줄: 무기 슬롯 10칸 - 누르면 그 무기로 바로 이동
        var slotRow = new VisualElement();
        slotRow.style.flexDirection = FlexDirection.Row;
        slotRow.style.justifyContent = Justify.Center;
        slotRow.style.marginBottom = 12;

        var slots = new List<Image>(); // 무기 순서대로 슬롯 이미지
        for (int i = 0; i < WeaponCount(); i++)
        {
            int index = i; // 클로저용 복사
            var slot = new Image { scaleMode = ScaleMode.ScaleToFit };
            slot.style.width = 96;
            slot.style.height = 96;
            slot.style.marginLeft = 6;
            slot.style.marginRight = 6;
            slot.style.backgroundColor = PixelStyle.Card.face.linear; // 픽셀 칸 안쪽
            slot.RegisterCallback<ClickEvent>(_ => { _weaponBrowse.index = index; redraw(); });
            slot.AddToClassList(DebrisPool.ClickSoundClass); // 누르면 버튼 클릭 소리
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
        WeaponManager weapons = WeaponManager.Instance; // 무기 데이터/해금 상태 조회
        if (weapons == null) return;

        int index = Mathf.Clamp(_weaponBrowse.index, 0, weapons.WeaponCount - 1); // 지금 보고 있는 무기
        _weaponBrowse.index = index;
        WeaponData data = weapons.GetWeaponAt(index);

        bool unlocked = weapons.IsUnlocked(index);
        bool equipped = weapons.EquippedIndex == index;
        bool buyable = !unlocked && weapons.IsUnlockOrderMet(index); // 바로 다음에 살 수 있는 무기
        bool hidden = !unlocked && !buyable; // 아직 살 수도 없는 무기는 이름/수치를 가림

        prevButton.SetEnabled(index > 0);
        nextButton.SetEnabled(index < weapons.WeaponCount - 1);

        preview.sprite = data.icon;
        preview.tintColor = unlocked ? Color.white : Color.black; // 잠긴 무기는 검은 실루엣
        nameLabel.text = hidden ? "???" : data.weaponName;

        int bonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.GetClickDamageBonus(index) : 0; // 업그레이드로 붙은 데미지
        long cost = buyable ? weapons.GetUnlockCost(index) : 0; // 해금 가격

        card.Clear();
        card.Add(PixelStyle.CardHeader("능력치")); // 카드 머리글
        card.Add(CreateInfoRow("공격력", hidden ? "???" : NumberFormatUtil.Format(data.clickDamage), hidden ? HiddenColor : Color.white));
        card.Add(CreateInfoRow("강화 보너스", hidden ? "???" : $"+{NumberFormatUtil.Format(bonus)}", hidden ? HiddenColor : Color.white));
        card.Add(CreateInfoRow("클릭당 합계", hidden ? "???" : NumberFormatUtil.Format(data.clickDamage + bonus), hidden ? HiddenColor : Color.white));
        if (buyable)
            card.Add(CreateInfoRow("해금 가격", $"{NumberFormatUtil.Format(cost)} 조각", Color.white));
        card.Add(CreateInfoRow("상태", equipped ? "장착 중" : unlocked ? "해금됨" : "잠김", equipped || unlocked ? GoodColor : BadColor));

        // 버튼: 장착하기 / 장착 중(비활성) / 해금(조각 부족하면 비활성) / 가려진 무기면 숨김
        actionButton.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
        if (equipped)
        {
            actionButton.text = "장착 중";
            actionButton.SetEnabled(false);
            actionButton.style.opacity = 1f; // 장착 중은 흐리게 하지 않음 (글자로 충분히 구분됨)
        }
        else if (unlocked)
        {
            actionButton.text = "장착하기";
            actionButton.SetEnabled(true);
            actionButton.style.opacity = StyleKeyword.Null;
        }
        else
        {
            long have = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetPieces() : 0; // 보유 조각
            actionButton.text = $"해금 ({NumberFormatUtil.Format(cost)} 조각)";
            actionButton.SetEnabled(have >= cost);
            actionButton.style.opacity = StyleKeyword.Null; // 조각이 모자라면 흐리게
        }

        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].sprite = weapons.GetWeaponAt(i).icon;
            slots[i].tintColor = weapons.IsUnlocked(i) ? Color.white : Color.black;
            // 장착 중 = 금색, 지금 보고 있는 칸 = 흰색, 나머지 = 기본 테두리
            Color border = (i == weapons.EquippedIndex ? PixelStyle.Highlight : i == index ? PixelStyle.Text : PixelStyle.Card.outline).linear; // 장착 중 주황 / 보는 중 진갈색 / 나머지 연갈색
            SetFrame(slots[i], border, i == index || i == weapons.EquippedIndex ? 4 : 2, 0); // 각진 픽셀 칸
        }
    }

    // 도감 카드의 한 줄 - 항목 이름은 왼쪽, 값은 오른쪽 끝
    private VisualElement CreateInfoRow(string label, string value, Color valueColor)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.marginBottom = 26; // 줄 사이를 넓혀 세로로 길쭉한 카드

        var labelText = new Label(label);
        labelText.style.fontSize = 36;
        labelText.style.color = PixelStyle.MutedText.linear;
        row.Add(labelText);

        var valueText = new Label(value);
        valueText.style.fontSize = 36;
        valueText.style.unityFontStyleAndWeight = FontStyle.Bold;
        valueText.style.color = (valueColor == GoodColor ? PixelStyle.Good : valueColor == BadColor ? PixelStyle.Bad
            : valueColor == HiddenColor ? PixelStyle.Hidden : PixelStyle.Text).linear; // 크림 바탕에서 보이는 진한 색으로
        row.Add(valueText);

        return row;
    }

    // 도감 카드에 들어갈 (항목, 값, 값 색) 목록. 잠긴 오브젝트는 지역/체력/파편 획득량을 ???로 가림
    private (string label, string value, Color color)[] GetObjectInfoRows(int index)
    {
        var manager = ObjectManager.Instance; // 오브젝트 데이터/해금 여부 조회용
        if (manager == null) return null;

        Color hidden = new Color(0.5f, 0.52f, 0.6f); // ???로 가린 값의 색
        Color good = new Color(0.5f, 0.9f, 0.5f); // 해금 완료
        Color bad = new Color(0.95f, 0.45f, 0.4f); // 잠김

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

    // 이름 라벨, 미리보기 이미지, Select 버튼/Equipped 표시 중 무엇을 보여줄지 갱신
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
        bool locked = isLocked != null && isLocked(index); // 잠긴 오브젝트인지
        nameLabel.text = locked ? "???" : getName(index); // 잠긴 오브젝트는 이름도 가림
        previewImage.sprite = getIcon(index);
        previewImage.tintColor = locked ? Color.black : Color.white; // 잠겼으면 완전히 새까만 그림자로 (tint는 그림 색에 곱해지므로 검정이면 안쪽 무늬가 전부 사라지고 모양만 남음)

        // 도감 정보 카드를 현재 오브젝트 기준으로 새로 채움
        var rows = getInfoRows?.Invoke(index);
        infoCard.Clear();
        infoCard.Add(PixelStyle.CardHeader("도감 정보")); // 카드 머리글
        infoCard.style.display = rows == null ? DisplayStyle.None : DisplayStyle.Flex;
        if (rows != null)
            foreach (var row in rows)
                infoCard.Add(CreateInfoRow(row.label, row.value, row.color));

        // 도감 모드(오브젝트 팝업): 선택 버튼/장착됨 표시 없이 카드의 "상태" 줄(잠김/해금 완료)만 씀
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

    private const float NavButtonPunchAmount = 0.15f; // 업글/무기/오브젝트 버튼 클릭시 커지는 비율
    private const float NavButtonPunchDuration = 0.12f; // 펀치 애니메이션 길이(초)

    // 좌상단 가로줄에 나란히 놓이는 버튼 (고정 폭/높이) - 3개 합쳐서 화면(1920 기준) 절반 가까이 오도록 키움
    private Button CreateButton(string text, PixelStyle.Tone tone, System.Action onClick)
    {
        // onClick이 패널을 열거나 버튼 줄 자체를 숨겨버리므로, 펀치가 다 보인 뒤에 실제 동작을 실행해야 함
        // (먼저 실행하면 버튼이 그 즉시 사라져서 펀치가 눈에 안 보임)
        // 카메라 리셋 버튼처럼 밝은 배경+진회색 글씨로 바꿔봤는데, 이 패널에서는 텍스트 color 자체가
        // 무슨 값을 넣어도 흰색으로 고정되는 문제가 있어서(재컴파일 후에도 재현) 일단 원래 스타일로 되돌림
        var button = new Button { text = text };
        button.clicked += () => StartCoroutine(PunchThenInvoke(button, NavButtonPunchDuration, NavButtonPunchAmount, onClick));
        button.style.width = 300;
        button.style.height = 110;
        button.style.fontSize = 36;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        PixelStyle.Button(button, tone); // 픽셀 입체 버튼
        return button;
    }

    // VisualElement를 1배 -> 1+punch배 -> 1배로 튕긴 뒤, 그제서야 실제 클릭 동작(onClick)을 실행
    private IEnumerator PunchThenInvoke(VisualElement element, float duration, float punch, System.Action onClick)
    {
        float t = 0f; // 경과 시간
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // 패널 열림 등으로 timeScale이 0이어도 정상 재생되도록 unscaled 사용
            float p = Mathf.Clamp01(t / duration); // 0~1 진행률
            float s = 1f + punch * Mathf.Sin(p * Mathf.PI); // 커졌다 돌아오는 곡선
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
        RefreshButtonRowVisibility(); // 팝업이 열렸으니 버튼 줄 숨김

        Time.timeScale = 0f; // 열려있는 동안 자동클릭/자동채굴 등이 계속 진행되는 걸 막음 (업그레이드 화면과 동일)

        OnSelectorPanelToggled?.Invoke(true); // ZoneArrowsUI 등에게 팝업 열림을 알림

        // 열 때마다 항상 최신 상태로 다시 그려서, 이름이 비어 보이는 경우가 없게 함
        _refreshWeaponSelector?.Invoke();
        _refreshObjectSelector?.Invoke();

        // Object 팝업을 여는 시점의 오브젝트를 기억해뒀다가, 닫을 때 바뀌었는지 비교함
        // (팝업이 화면을 거의 가리므로, 전환 애니메이션은 닫을 때 재생해야 보임)
        if (contentToShow == _objectContent && ObjectManager.Instance != null)
            _objectAtPanelOpen = ObjectManager.Instance.CurrentObject;
    }

    private void ClosePanel()
    {
        _panel.style.display = DisplayStyle.None;
        RefreshButtonRowVisibility(); // 팝업이 닫혔으니 버튼 줄 다시 보임

        Time.timeScale = 1f; // 멈춰뒀던 게임 시간을 다시 정상 속도로

        OnSelectorPanelToggled?.Invoke(false); // ZoneArrowsUI 등에게 팝업 닫힘을 알림

        // Object 팝업이 열려있는 동안 선택이 바뀌었다면, 닫히는 지금 전환 애니메이션 재생
        if (_currentlyOpenContent == _objectContent && ObjectManager.Instance != null)
        {
            ObjectData current = ObjectManager.Instance.CurrentObject;

            if (current != _objectAtPanelOpen)
                ObjectSwapController.Instance?.TriggerSwap(_objectAtPanelOpen, current);
        }
    }
}
