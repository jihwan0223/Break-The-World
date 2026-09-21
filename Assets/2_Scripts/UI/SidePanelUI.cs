using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    private Label _panelTitle; // 팝업 좌상단 제목 ("Weapon" / "Object")
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

    void OnDisable()
    {
        UpgradeTreeUI.OnTreeToggled -= HandleUpgradeTreeToggled;
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

        // 투명한 루트가 화면 전체 클릭을 먹어서 뒤쪽 Canvas(업그레이드 트리)의 노드 클릭이 안 되는 문제 방지 -
        // 실제 버튼/팝업 패널은 각자 picking 모드를 그대로 유지하므로 그 위 클릭은 정상 동작함
        root.pickingMode = PickingMode.Ignore;

        // 화면 전체를 꽉 채우는 팝업 패널 - 업그레이드 화면과 동일하게 전체화면으로 덮고 X 버튼으로 닫음
        _panel = CreatePopupFrame(ClosePanel, out _panelTitle);

        _weaponContent = BuildSelectorSection(
            _weaponBrowse,
            () => WeaponManager.Instance != null ? WeaponManager.Instance.WeaponCount : 0,
            index => WeaponManager.Instance != null ? WeaponManager.Instance.GetWeaponAt(index).weaponName : "",
            index => WeaponManager.Instance != null ? WeaponManager.Instance.GetWeaponAt(index).icon : null,
            () => WeaponManager.Instance != null ? WeaponManager.Instance.EquippedIndex : 0,
            index =>
            {
                Debug.Log("무기 선택 버튼 눌림");
                WeaponManager.Instance?.Equip(index);
            },
            out _refreshWeaponSelector);
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

        var upgradeButton = CreateButton("업그레이드", () =>
        {
            Debug.Log("업그레이드 버튼 눌림");
            // Canvas로 만든 업그레이드 화면을 직접 켬 (닫기는 그 화면 안의 X 버튼 -> UpgradeTreeUI.Close()가 담당)
            if (upgradeTreePanel != null) upgradeTreePanel.SetActive(true);
            else Debug.LogWarning("SidePanelUI: Upgrade Tree Panel이 인스펙터에 비어있어서 업그레이드 화면을 못 엶");
        });

        var weaponButton = CreateButton("무기", () =>
        {
            Debug.Log("무기 버튼 눌림");
            OpenPanel("무기", _weaponContent);
        });

        var objectButton = CreateButton("도감", () =>
        {
            Debug.Log("도감 버튼 눌림");
            OpenPanel("도감", _objectContent);
        });

        var debugButton = CreateButton("디버그", () =>
        {
            Debug.Log("디버그 버튼 눌림");
            OpenDebugPanel();
        });

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
        panel.style.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f); // 업그레이드 화면처럼 불투명 단색(검정 대신 짙은 남색조)

        // 밋밋한 단색 대신 위쪽이 살짝 밝은 남색 그라데이션을 깔고, 그 위에 은은한 십자 점 무늬를 반복해서 깔아 꾸밈 (이미지 파일 없이 코드로 그림)
        panel.style.backgroundImage = new StyleBackground(MakeTexture(1, 64, (x, y) =>
            Color.Lerp(new Color(0.03f, 0.04f, 0.08f), new Color(0.13f, 0.16f, 0.26f), y / 63f).linear, FilterMode.Bilinear, TextureWrapMode.Clamp)); // .linear: 텍스처 색은 감마 보정 없이 그려져서 그냥 쓰면 훨씬 밝게 보임

        var pattern = new VisualElement(); // 무늬 층 - 클릭은 통과시킴
        pattern.pickingMode = PickingMode.Ignore;
        pattern.style.position = Position.Absolute;
        pattern.style.left = 0;
        pattern.style.right = 0;
        pattern.style.top = 0;
        pattern.style.bottom = 0;
        Color dot = new Color(0.7f, 0.8f, 1f, 0.025f); // 무늬 색 - 어두운 배경 위에 섞이면 알파가 조금만 커도 꽤 밝아 보여서 아주 낮게 잡음
        Color corner = new Color(0.7f, 0.8f, 1f, 0.012f); // 모서리 점은 십자보다 더 옅게
        pattern.style.backgroundImage = new StyleBackground(MakeTexture(16, 16, (x, y) =>
            (x == 8 && y >= 6 && y <= 10) || (y == 8 && x >= 6 && x <= 10) ? dot // 칸 가운데 십자
            : (x <= 1 && y <= 1) ? corner // 칸 모서리 점
            : Color.clear, FilterMode.Point, TextureWrapMode.Repeat));
        pattern.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
        pattern.style.backgroundSize = new BackgroundSize(96, 96); // 16px 무늬를 6배로 키워 픽셀 느낌 유지
        panel.Add(pattern);
        panel.style.display = DisplayStyle.None;

        // 이 패널 위에 포인터가 있는 동안은 뒤쪽 월드 오브젝트가 클릭되지 않도록 플래그를 켜고 끔
        panel.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        panel.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);

        titleLabel = new Label();
        titleLabel.style.position = Position.Absolute;
        titleLabel.style.top = 16;
        titleLabel.style.left = 16;
        titleLabel.style.fontSize = 22;
        titleLabel.style.color = Color.white;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        panel.Add(titleLabel);

        // 업그레이드 화면의 닫기 버튼처럼 배경 없이 흰 글자 X만
        var closeButton = new Button(onClose) { text = "X" };
        closeButton.style.position = Position.Absolute;
        closeButton.style.top = 16;
        closeButton.style.right = 16;
        closeButton.style.width = 44;
        closeButton.style.height = 44;
        closeButton.style.fontSize = 28;
        closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeButton.style.backgroundColor = Color.clear;
        closeButton.style.borderTopWidth = 0;
        closeButton.style.borderBottomWidth = 0;
        closeButton.style.borderLeftWidth = 0;
        closeButton.style.borderRightWidth = 0;
        closeButton.style.color = Color.white;
        panel.Add(closeButton);

        return panel;
    }

    // 픽셀마다 색을 계산해 작은 텍스처를 만듦 - 팝업 배경 장식용. 어두운 색에서 8비트 단계가 띠로 보이지 않도록 Half 포맷 사용
    private static Texture2D MakeTexture(int width, int height, System.Func<int, int, Color> pixel, FilterMode filter, TextureWrapMode wrap)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBAHalf, false) { filterMode = filter, wrapMode = wrap };
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, pixel(x, y));
        texture.Apply();
        return texture;
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
        list.style.top = 90;
        list.style.alignItems = Align.Center;

        // 각 동작은 눌린 결과를 한 줄 문구로 돌려줌 - 화면이 팝업에 가려져서 눌렀는지 알 수 없기 때문
        AddDebugButton(list, "돈 최대", () =>
        {
            CurrencyManager.Instance?.MaxAllDebug();
            return "모든 조각을 최대치로 채웠습니다";
        });
        AddDebugButton(list, "돈 초기화", () =>
        {
            CurrencyManager.Instance?.ResetAll();
            return "모든 조각을 0으로 되돌렸습니다";
        });
        AddDebugButton(list, "전체 해금", () =>
        {
            UpgradeManager.Instance?.UnlockAllDebug();
            ObjectManager.Instance?.UnlockAllDebug();
            UpgradeTreeUI.Instance?.RefreshAll();
            return "모든 업그레이드와 오브젝트를 해금했습니다";
        });
        AddDebugButton(list, "업그레이드 초기화", () =>
        {
            UpgradeManager.Instance?.ResetAll();
            ObjectManager.Instance?.ResetAll();
            UpgradeTreeUI.Instance?.RefreshAll();
            return "업그레이드와 오브젝트 해금을 처음 상태로 되돌렸습니다";
        });
        AddDebugButton(list, "존 해금 초기화", () =>
        {
            ZoneManager.Instance?.ResetAll();
            return "존1만 열린 상태로 되돌렸습니다. 책상을 부수면 해금 연출이 나옵니다";
        });
        _debugPanel.Add(list);

        _debugStatus = new Label();
        _debugStatus.style.position = Position.Absolute;
        _debugStatus.style.left = 0;
        _debugStatus.style.right = 0;
        _debugStatus.style.bottom = 40;
        _debugStatus.style.fontSize = 26;
        _debugStatus.style.color = Color.white;
        _debugStatus.style.unityTextAlign = TextAnchor.MiddleCenter;
        _debugPanel.Add(_debugStatus);

        root.Add(_debugPanel);
    }

    // 디버그 목록에 버튼 하나 추가 - 누르면 action을 실행하고 돌려받은 문구를 아래 줄에 표시
    private void AddDebugButton(VisualElement list, string text, System.Func<string> action)
    {
        Button button = CreateButton(text, () => _debugStatus.text = action());
        button.style.width = 460; // "업그레이드 초기화"처럼 긴 글자도 들어가도록 기본 폭보다 넓게
        button.style.marginBottom = 14;
        list.Add(button);
    }

    private void OpenDebugPanel()
    {
        _debugStatus.text = "";
        _debugPanel.style.display = DisplayStyle.Flex;
        RefreshButtonRowVisibility(); // 팝업이 열렸으니 버튼 줄 숨김

        Time.timeScale = 0f; // 다른 팝업과 동일 - 열려있는 동안 게임 시간 정지
        OnSelectorPanelToggled?.Invoke(true); // DebrisPool 등에게 팝업 열림을 알림
    }

    private void CloseDebugPanel()
    {
        _debugPanel.style.display = DisplayStyle.None;
        RefreshButtonRowVisibility(); // 팝업이 닫혔으니 버튼 줄 다시 보임

        Time.timeScale = 1f;
        OnSelectorPanelToggled?.Invoke(false);
    }

    // 팝업 안에 들어갈 전체 영역: 화살표 사이에 이름, 그 아래 왼쪽엔 이미지 자리 / 오른쪽엔 설명 + Select 버튼.
    // 무기/오브젝트 팝업이 완전히 같은 모양을 공유하도록 콜백만 받아서 공용으로 구성함.
    private VisualElement BuildSelectorSection(
        BrowseState browse,
        System.Func<int> getCount,
        System.Func<int, string> getName,
        System.Func<int, Sprite> getIcon,
        System.Func<int> getEquippedIndex,
        System.Action<int> onSelect,
        out System.Action refresh,
        System.Func<int, bool> isLocked = null, // Object 패널에서만 씀 - null이면(Weapon) 잠김 개념 자체가 없음
        System.Func<int, (string label, string value, Color color)[]> getInfoRows = null) // Object 패널에서만 씀 - 도감 정보 카드의 (항목, 값, 값 색) 줄들. null이면 카드 없음
    {
        var content = new VisualElement();
        content.style.position = Position.Absolute;
        content.style.left = 16;
        content.style.right = 16;
        content.style.top = 60;
        content.style.bottom = 16;
        content.style.display = DisplayStyle.None;

        System.Action redraw = null; // 이름/이미지/버튼/정보 카드를 현재 browse.index 기준으로 다시 그리는 함수 (아래 끝에서 채움)

        // 위쪽 줄: < 이름 >
        var arrowRow = new VisualElement();
        arrowRow.style.flexDirection = FlexDirection.Row;
        arrowRow.style.justifyContent = Justify.Center;
        arrowRow.style.alignItems = Align.Center;
        arrowRow.style.height = 60;

        var nameLabel = new Label();
        nameLabel.style.width = 260;
        nameLabel.style.fontSize = 24;
        nameLabel.style.color = Color.white;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        // Select 버튼 / Equipped 표시 - 중간보다 약간 아래, 오른쪽에 위치. 장착 여부에 따라 둘 중 하나만 보임
        var selectButton = new Button() { text = "선택" };
        selectButton.style.position = Position.Absolute;
        selectButton.style.top = Length.Percent(55);
        selectButton.style.right = 350; // 오른쪽 기준 30px 왼쪽으로 이동
        selectButton.style.width = 140;
        selectButton.style.height = 44;
        selectButton.style.fontSize = 18;
        selectButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        selectButton.style.backgroundColor = new Color(0.2f, 0.45f, 0.2f, 0.9f);
        selectButton.style.color = Color.white;

        var equippedLabel = new Label("장착됨");
        equippedLabel.style.position = Position.Absolute;
        equippedLabel.style.top = Length.Percent(55);
        equippedLabel.style.right = 350; // Select 버튼과 같은 위치를 공유해야 하므로 동일하게 이동
        equippedLabel.style.width = 140;
        equippedLabel.style.height = 44;
        equippedLabel.style.fontSize = 18;
        equippedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        equippedLabel.style.color = new Color(0.5f, 0.9f, 0.5f);
        equippedLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        // 미리보기 이미지 - 맨 처음 크기(100)에서 7배인 700. 세로는 화면 중앙 정렬,
        // 가로는 왼쪽 화살표의 실제 위치를 기준으로 그보다 살짝 왼쪽에 중심이 오도록 계산함 (아래 GeometryChangedEvent에서)
        const float previewImageSize = 700f;
        const float previewImageLeftOffsetFromArrow = 40f; // 화살표 중심에서 이만큼 더 왼쪽으로

        var previewImage = new Image();
        previewImage.style.position = Position.Absolute;
        previewImage.style.top = Length.Percent(50);
        previewImage.style.marginTop = -previewImageSize / 2f; // 이미지 높이의 절반만큼 올려서 세로 중앙 정렬
        previewImage.style.width = previewImageSize;
        previewImage.style.height = previewImageSize;
        previewImage.scaleMode = ScaleMode.ScaleToFit;

        // 미리보기 액자 - 반투명 어두운 판에 테두리를 둘러 도감 진열칸처럼 보이게 함
        previewImage.style.backgroundColor = new Color(0.55f, 0.6f, 0.8f, 0.1f); // 어두운 배경 위에서도 새까만 그림자 모양이 보이도록 살짝 밝은 판
        previewImage.style.borderTopWidth = 3;
        previewImage.style.borderBottomWidth = 3;
        previewImage.style.borderLeftWidth = 3;
        previewImage.style.borderRightWidth = 3;
        previewImage.style.borderTopColor = InfoBorderColor;
        previewImage.style.borderBottomColor = InfoBorderColor;
        previewImage.style.borderLeftColor = InfoBorderColor;
        previewImage.style.borderRightColor = InfoBorderColor;
        previewImage.style.borderTopLeftRadius = 14;
        previewImage.style.borderTopRightRadius = 14;
        previewImage.style.borderBottomLeftRadius = 14;
        previewImage.style.borderBottomRightRadius = 14;

        // 도감 정보 카드 - 항목 이름은 왼쪽, 값은 오른쪽에 한 줄씩. 선택 버튼(right 350, 폭 140) 위쪽에 가운데가 맞도록 둠
        var infoCard = new VisualElement();
        infoCard.style.position = Position.Absolute;
        infoCard.style.top = 110;
        infoCard.style.right = 200;
        infoCard.style.width = 440;
        infoCard.style.paddingTop = 24;
        infoCard.style.paddingBottom = 10;
        infoCard.style.paddingLeft = 24;
        infoCard.style.paddingRight = 24;
        infoCard.style.backgroundColor = new Color(0.03f, 0.04f, 0.08f, 0.8f);
        infoCard.style.borderTopWidth = 3;
        infoCard.style.borderBottomWidth = 3;
        infoCard.style.borderLeftWidth = 3;
        infoCard.style.borderRightWidth = 3;
        infoCard.style.borderTopColor = InfoBorderColor;
        infoCard.style.borderBottomColor = InfoBorderColor;
        infoCard.style.borderLeftColor = InfoBorderColor;
        infoCard.style.borderRightColor = InfoBorderColor;
        infoCard.style.borderTopLeftRadius = 8;
        infoCard.style.borderTopRightRadius = 8;
        infoCard.style.borderBottomLeftRadius = 8;
        infoCard.style.borderBottomRightRadius = 8;
        infoCard.style.display = DisplayStyle.None;

        selectButton.clicked += () =>
        {
            if (isLocked != null && isLocked(browse.index))
                return; // 잠긴 오브젝트는 여기서 선택 안 됨 - 업그레이드 트리에서 먼저 해금해야 함

            onSelect(browse.index);
            redraw();
        };

        content.Add(selectButton);
        content.Add(equippedLabel);
        content.Add(previewImage);
        content.Add(infoCard);

        var prevButton = new Button(() =>
        {
            int count = getCount();
            if (count <= 0) return;
            browse.index = Mathf.Max(0, browse.index - 1);
            redraw();
        })
        { text = "<" };
        SetArrowButtonStyle(prevButton);

        // 왼쪽 화살표의 실제 레이아웃이 계산된 뒤, 그 중심보다 살짝 왼쪽에 미리보기 이미지 중심이 오도록 위치 계산
        prevButton.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            Vector2 arrowCenterInContent = content.WorldToLocal(prevButton.worldBound.center);
            previewImage.style.left = arrowCenterInContent.x - previewImageLeftOffsetFromArrow - previewImageSize / 2f;
        });

        var nextButton = new Button(() =>
        {
            int count = getCount();
            if (count <= 0) return;
            browse.index = Mathf.Min(count - 1, browse.index + 1);
            redraw();
        })
        { text = ">" };
        SetArrowButtonStyle(nextButton);

        arrowRow.Add(prevButton);
        arrowRow.Add(nameLabel);
        arrowRow.Add(nextButton);

        content.Add(arrowRow);

        // 외부(Start, OpenPanel)에서 필요할 때마다 최신 상태로 다시 그릴 수 있도록 넘겨줌
        redraw = () => RefreshSelectorState(nameLabel, selectButton, equippedLabel, previewImage, infoCard, getName, getIcon, getEquippedIndex, getInfoRows, browse.index, isLocked);
        refresh = redraw;

        return content;
    }

    private static readonly Color InfoBorderColor = new Color(0.32f, 0.38f, 0.55f); // 도감 카드/미리보기 액자 테두리색

    // 도감 카드의 한 줄 - 항목 이름은 왼쪽, 값은 오른쪽 끝
    private VisualElement CreateInfoRow(string label, string value, Color valueColor)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.marginBottom = 14;

        var labelText = new Label(label);
        labelText.style.fontSize = 22;
        labelText.style.color = new Color(0.65f, 0.7f, 0.8f);
        row.Add(labelText);

        var valueText = new Label(value);
        valueText.style.fontSize = 24;
        valueText.style.unityFontStyleAndWeight = FontStyle.Bold;
        valueText.style.color = valueColor;
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
            ("획득 파편", locked ? "???" : $"{NumberFormatUtil.Format(manager.ExpectedPieces(index))}개", locked ? hidden : Color.white),
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

    private void SetArrowButtonStyle(Button button)
    {
        button.style.width = 48;
        button.style.height = 48;
        button.style.fontSize = 22;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        button.style.color = Color.white;
    }

    private const float NavButtonPunchAmount = 0.15f; // 업글/무기/오브젝트 버튼 클릭시 커지는 비율
    private const float NavButtonPunchDuration = 0.12f; // 펀치 애니메이션 길이(초)

    // 좌상단 가로줄에 나란히 놓이는 버튼 (고정 폭/높이) - 3개 합쳐서 화면(1920 기준) 절반 가까이 오도록 키움
    private Button CreateButton(string text, System.Action onClick)
    {
        // onClick이 패널을 열거나 버튼 줄 자체를 숨겨버리므로, 펀치가 다 보인 뒤에 실제 동작을 실행해야 함
        // (먼저 실행하면 버튼이 그 즉시 사라져서 펀치가 눈에 안 보임)
        // 카메라 리셋 버튼처럼 밝은 배경+진회색 글씨로 바꿔봤는데, 이 패널에서는 텍스트 color 자체가
        // 무슨 값을 넣어도 흰색으로 고정되는 문제가 있어서(재컴파일 후에도 재현) 일단 원래 스타일로 되돌림
        var button = new Button { text = text };
        button.clicked += () => StartCoroutine(PunchThenInvoke(button, NavButtonPunchDuration, NavButtonPunchAmount, onClick));
        button.style.width = 300;
        button.style.height = 110;
        button.style.fontSize = 34;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        button.style.color = Color.white;
        button.style.borderTopLeftRadius = 6;
        button.style.borderTopRightRadius = 6;
        button.style.borderBottomLeftRadius = 6;
        button.style.borderBottomRightRadius = 6;
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

        OnSelectorPanelToggled?.Invoke(true); // DebrisPool 등에게 팝업 열림을 알림

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

        OnSelectorPanelToggled?.Invoke(false); // DebrisPool 등에게 팝업 닫힘을 알림

        // Object 팝업이 열려있는 동안 선택이 바뀌었다면, 닫히는 지금 전환 애니메이션 재생
        if (_currentlyOpenContent == _objectContent && ObjectManager.Instance != null)
        {
            ObjectData current = ObjectManager.Instance.CurrentObject;

            if (current != _objectAtPanelOpen)
                ObjectSwapController.Instance?.TriggerSwap(_objectAtPanelOpen, current);
        }
    }
}
