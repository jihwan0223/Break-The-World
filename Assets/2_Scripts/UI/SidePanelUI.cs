using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UT = UpgradeManager.UpgradeType; // 업그레이드 트리 정의(BuildUpgradeTreeSection)를 짧게 쓰기 위한 별칭

[RequireComponent(typeof(UIDocument))]
public class SidePanelUI : MonoBehaviour
{
    // 화살표로 둘러보는 중인 인덱스를 들고 있는 작은 상태 객체 (무기용/오브젝트용 각각 하나씩)
    private class BrowseState
    {
        public int index; // 지금 화면에 미리보기로 표시 중인 인덱스 (Select를 눌러야 실제로 적용됨)
    }

    [SerializeField] private Sprite upgradeTreeBackground; // 업그레이드 트리 배경 이미지 (3_Image/GBImage/Upgradebgimage)

    // 팬(드래그 이동) 가능한 범위 - 캔버스가 이 범위 밖으로 너무 멀리 밀려나가지 않도록 제한
    [SerializeField] private float upgradePanLimit = 1500f;
    [SerializeField] private float upgradeMinZoom = 0.5f; // 최소 축소 배율
    [SerializeField] private float upgradeMaxZoom = 2.5f; // 최대 확대 배율
    [SerializeField] private float upgradeZoomStep = 0.05f; // 스크롤 한 틱(delta)당 줄어드는/늘어나는 목표 배율 감도
    [SerializeField] private float upgradeZoomSmoothSpeed = 12f; // 목표 배율을 따라잡는 속도 (클수록 빠르게/뚝뚝 끊기게, 작을수록 부드럽게)

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

    private VisualElement _upgradeOverlay; // 업그레이드 전용 풀스크린 검은 오버레이
    private VisualElement _upgradeCanvas; // 오버레이 안에서 팬/줌이 실제로 적용되는 콘텐츠(배경+트리) 컨테이너
    private System.Action _refreshUpgradeTree; // 업그레이드 트리의 레벨(N/Max) 표시를 다시 그리는 함수
    private System.Action _resetUpgradeTree; // 업그레이드 트리를 전부 초기화(레벨 0, 공개 상태도 리셋)하는 함수

    // 업그레이드 오버레이가 열리면 true, 닫히면 false로 전달 - HealthBarUI처럼 별도 UIDocument로 뜨는 UI들이
    // 오버레이 위/아래로 비쳐 보이지 않게 스스로 숨기고 보여주는 데 사용
    public static event System.Action<bool> OnUpgradeOverlayToggled;
    private bool _isDraggingUpgradeCanvas; // 지금 마우스로 캔버스를 드래그하는 중인지
    private Vector2 _dragStartMousePos; // 드래그 시작한 순간의 마우스 화면 좌표
    private Vector2 _dragStartPan; // 드래그 시작한 순간의 팬 오프셋
    private Vector2 _upgradePan; // 현재 팬 오프셋(px)
    private float _upgradeZoom = 1f; // 현재(화면에 실제로 적용되는, 매끄럽게 보간되는) 줌 배율
    private float _upgradeZoomTarget = 1f; // 스크롤 입력이 가리키는 목표 줌 배율 - _upgradeZoom이 매 프레임 이 값을 따라감

    void OnEnable()
    {
        // 업그레이드 네모 배경 이미지가 인스펙터에 비어있으면 콘솔에 바로 알려줌 (원인 확인용)
        if (upgradeTreeBackground == null)
            Debug.LogWarning("SidePanelUI: Upgrade Tree Background가 비어있어서 업그레이드 네모에 이미지가 안 뜸 - 인스펙터에서 다시 드래그해서 넣어줘.");

        var uiDocument = GetComponent<UIDocument>();

        if (uiDocument.panelSettings == null)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            uiDocument.panelSettings = settings;
        }

        Build(uiDocument.rootVisualElement);
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

        // 저장 파일 로드(UpgradeManager.SetLevel)가 Build() 이후에 끝날 수 있어서 여기서 한 번 더 갱신
        _refreshUpgradeTree?.Invoke();
    }

    void Update()
    {
        // 업그레이드 오버레이가 열려있는 동안만, 현재 줌을 목표 줌 쪽으로 매 프레임 부드럽게 보간
        // (휠 이벤트가 뚝뚝 끊기게 들어와도 실제 확대/축소는 매끄럽게 이어지도록)
        if (_upgradeOverlay == null || _upgradeOverlay.style.display != DisplayStyle.Flex)
            return;

        if (Mathf.Approximately(_upgradeZoom, _upgradeZoomTarget))
            return;

        // 업그레이드 페이지가 열려있는 동안 Time.timeScale이 0이라 deltaTime 대신 unscaledDeltaTime을 써야 줌 보간이 계속 동작함
        _upgradeZoom = Mathf.Lerp(_upgradeZoom, _upgradeZoomTarget, Time.unscaledDeltaTime * upgradeZoomSmoothSpeed);
        ApplyUpgradeCanvasTransform();
    }

    private void Build(VisualElement root)
    {
        root.Clear();

        // 화면(1920x1080 기준)보다 살짝 작은 크기로 거의 꽉 채우는 빈 팝업 패널
        _panel = new VisualElement();
        _panel.style.position = Position.Absolute;
        _panel.style.left = 40;
        // 오른쪽 버튼 컬럼(폭 25%)과 겹치지 않도록 그 바로 앞까지만 채움
        _panel.style.right = Length.Percent(27);
        _panel.style.top = 40;
        _panel.style.bottom = 40;
        _panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
        _panel.style.borderTopLeftRadius = 8;
        _panel.style.borderTopRightRadius = 8;
        _panel.style.borderBottomLeftRadius = 8;
        _panel.style.borderBottomRightRadius = 8;
        _panel.style.display = DisplayStyle.None;

        // 이 패널 위에 포인터가 있는 동안은 뒤쪽 월드 오브젝트가 클릭되지 않도록 플래그를 켜고 끔
        _panel.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        _panel.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);

        _panelTitle = new Label();
        _panelTitle.style.position = Position.Absolute;
        _panelTitle.style.top = 16;
        _panelTitle.style.left = 16;
        _panelTitle.style.fontSize = 22;
        _panelTitle.style.color = Color.white;
        _panelTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        _panel.Add(_panelTitle);

        var closeButton = new Button(ClosePanel) { text = "X" };
        closeButton.style.position = Position.Absolute;
        closeButton.style.top = 16;
        closeButton.style.right = 16;
        closeButton.style.width = 36;
        closeButton.style.height = 36;
        closeButton.style.fontSize = 18;
        closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeButton.style.backgroundColor = new Color(0.3f, 0.1f, 0.1f, 0.9f);
        closeButton.style.color = Color.white;
        _panel.Add(closeButton);

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
            out _refreshObjectSelector);
        _panel.Add(_objectContent);

        root.Add(_panel);

        // 업그레이드는 Weapon/Object 팝업과 별개로, 화면을 완전히 채우는 전용 오버레이로 구성
        // (root에 붙이는 건 아래 buttonColumn 다음에 함 - 화면 전체를 덮으므로 오른쪽 버튼 컬럼보다 위에 쌓여야
        //  오버레이가 열려있을 때 우상단 X가 buttonColumn한테 클릭을 뺏기지 않음)
        _upgradeOverlay = BuildUpgradeOverlay();

        // 버튼 2개가 들어갈 오른쪽 컬럼. 폭이 화면 가로의 1/4
        var buttonColumn = new VisualElement();
        buttonColumn.style.position = Position.Absolute;
        buttonColumn.style.right = 0;
        buttonColumn.style.top = 0;
        buttonColumn.style.bottom = 0;
        buttonColumn.style.width = Length.Percent(25);
        buttonColumn.style.flexDirection = FlexDirection.Column;
        buttonColumn.style.justifyContent = Justify.Center;
        buttonColumn.style.paddingLeft = 8;
        buttonColumn.style.paddingRight = 8;

        // 버튼 컬럼 위에 포인터가 있는 동안도 마찬가지로 월드 클릭을 막음
        buttonColumn.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        buttonColumn.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);

        var weaponButton = CreateButton("Weapon", () =>
        {
            Debug.Log("무기 버튼 눌림");
            OpenPanel("Weapon", _weaponContent);
        });

        var objectButton = CreateButton("Object", () =>
        {
            Debug.Log("오브젝트 버튼 눌림");
            OpenPanel("Object", _objectContent);
        });

        var upgradeButton = CreateButton("Upgrade", () =>
        {
            Debug.Log("업그레이드 버튼 눌림");
            OpenUpgradeOverlay();
        });

        buttonColumn.Add(weaponButton);
        buttonColumn.Add(objectButton);
        buttonColumn.Add(upgradeButton);
        root.Add(buttonColumn);

        // buttonColumn보다 나중에 붙여야 오버레이(와 그 안의 X 버튼)가 위에 쌓여서 클릭을 받을 수 있음
        root.Add(_upgradeOverlay);
    }

    // 업그레이드 전용 풀스크린 오버레이: 검은 배경 + 우상단 닫기(X) + 팬/줌 가능한 스킬트리 캔버스
    private VisualElement BuildUpgradeOverlay()
    {
        var overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.right = 0;
        overlay.style.top = 0;
        overlay.style.bottom = 0;
        overlay.style.backgroundColor = Color.black;
        overlay.style.display = DisplayStyle.None;
        overlay.style.overflow = Overflow.Hidden; // 캔버스가 팬으로 밀려나도 화면 밖은 안 보이게 잘라냄

        // 오버레이가 떠있는 동안은 뒤쪽 월드 오브젝트가 클릭되지 않도록 플래그를 켜고 끔
        overlay.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        overlay.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);

        // 팬/줌이 실제로 적용되는 콘텐츠 컨테이너 (배경 이미지 + 트리가 이 안에 들어감)
        _upgradeCanvas = BuildUpgradeTreeSection(out _refreshUpgradeTree, out _resetUpgradeTree);
        overlay.Add(_upgradeCanvas);

        // 마우스 왼쪽 버튼을 누른 채 드래그하면 캔버스를 이동(팬)시킴.
        // 단, 버튼(닫기 X, 업그레이드 노드) 위에서 누른 거면 포인터를 가로채면 안 됨 - 안 그러면 Button의 클릭 판정이 씹힘
        overlay.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return; // 왼쪽 버튼만 드래그로 취급
            if (evt.target != overlay && evt.target != _upgradeCanvas) return; // 버튼 위에서는 드래그 시작 안 함
            _isDraggingUpgradeCanvas = true;
            _dragStartMousePos = evt.position;
            _dragStartPan = _upgradePan;
            overlay.CapturePointer(evt.pointerId); // 오버레이 밖으로 마우스가 나가도 계속 드래그로 인식
        });

        overlay.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!_isDraggingUpgradeCanvas) return;

            Vector2 delta = (Vector2)evt.position - _dragStartMousePos; // 드래그 시작 지점 대비 마우스 이동량
            _upgradePan = _dragStartPan + delta;
            _upgradePan.x = Mathf.Clamp(_upgradePan.x, -upgradePanLimit, upgradePanLimit);
            _upgradePan.y = Mathf.Clamp(_upgradePan.y, -upgradePanLimit, upgradePanLimit);
            ApplyUpgradeCanvasTransform();
        });

        overlay.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!_isDraggingUpgradeCanvas) return;
            _isDraggingUpgradeCanvas = false;
            overlay.ReleasePointer(evt.pointerId);
        });

        // 마우스 스크롤로 확대/축소 (휠을 위로 굴리면 delta.y가 음수로 들어옴 -> 확대)
        overlay.RegisterCallback<WheelEvent>(evt =>
        {
            // 즉시 적용하지 않고 목표 값만 갱신 - 실제 적용은 Update()에서 매 프레임 보간(부드럽게)
            _upgradeZoomTarget = Mathf.Clamp(_upgradeZoomTarget - evt.delta.y * upgradeZoomStep, upgradeMinZoom, upgradeMaxZoom);
        });

        var closeButton = new Button(CloseUpgradeOverlay) { text = "X" }; // 흰색 X, 배경 없음
        closeButton.style.position = Position.Absolute;
        closeButton.style.top = 16;
        closeButton.style.right = 16;
        closeButton.style.width = 44;
        closeButton.style.height = 44;
        closeButton.style.fontSize = 26;
        closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        closeButton.style.backgroundColor = new Color(0, 0, 0, 0); // 배경 완전 투명
        closeButton.style.borderTopWidth = 0;
        closeButton.style.borderBottomWidth = 0;
        closeButton.style.borderLeftWidth = 0;
        closeButton.style.borderRightWidth = 0;
        closeButton.style.color = Color.white;
        overlay.Add(closeButton);

        // 테스트용 - 좌상단에 업그레이드 트리 전체 초기화 버튼
        var resetButton = new Button(() => _resetUpgradeTree?.Invoke()) { text = "Reset" };
        resetButton.style.position = Position.Absolute;
        resetButton.style.top = 76; // 16 대신 76 - 좌상단 Shards 표시(ShardUI, 별도 UIDocument)와 겹치지 않게 그 아래로 내림
        resetButton.style.left = 16;
        resetButton.style.width = 90;
        resetButton.style.height = 44;
        resetButton.style.fontSize = 16;
        resetButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        resetButton.style.backgroundColor = new Color(0.3f, 0.1f, 0.1f, 0.9f);
        resetButton.style.color = Color.white;
        overlay.Add(resetButton);

        return overlay;
    }

    private void OpenUpgradeOverlay()
    {
        _upgradeOverlay.style.display = DisplayStyle.Flex;

        // PointerEnterEvent는 마우스가 "움직여야" 발생하는데, 버튼을 누른 자리에서 마우스를 안 움직이면
        // 오버레이가 떠 있어도 이벤트가 안 와서 가드가 꺼진 채로 남아 뒤쪽 오브젝트가 클릭되는 문제가 있었음.
        // 오버레이는 화면 전체를 덮는 모달이니 여기서 바로 켜버림
        UIPointerGuard.IsPointerOverUI = true;

        // 업그레이드 보는 동안 게임(자동클릭 등)이 계속 진행되면서 뒤에서 체력바가 줄어드는 게 보이는 문제가 있어서 시간을 멈춤.
        // 0으로 만들면 Time.deltaTime을 쓰는 모든 스크립트(자동클릭 타이머, 콤보, 체력 회복 연출 등)가 자연히 멈춤
        Time.timeScale = 0f;

        OnUpgradeOverlayToggled?.Invoke(true); // HealthBarUI 등 다른 UIDocument들에게 숨으라고 알림

        _refreshUpgradeTree?.Invoke(); // 열 때마다 최신 레벨/골드 상태로 다시 그림

        // 열 때마다 화면 중앙 기본 배율로 리셋 (전에 확대/이동해둔 상태를 기억하지 않음)
        _upgradePan = Vector2.zero;
        _upgradeZoom = 1f;
        _upgradeZoomTarget = 1f;
        ApplyUpgradeCanvasTransform();
    }

    private void CloseUpgradeOverlay()
    {
        _upgradeOverlay.style.display = DisplayStyle.None;

        // 닫는 순간 마우스가 다른 UI 위에 있지 않다면 다시 월드 클릭이 되도록 꺼줌
        // (버튼 컬럼처럼 그 자리에 다른 UI가 있으면 그쪽 PointerEnter가 다시 켜줄 것)
        UIPointerGuard.IsPointerOverUI = false;

        Time.timeScale = 1f; // 멈춰뒀던 게임 시간을 다시 정상 속도로

        OnUpgradeOverlayToggled?.Invoke(false); // HealthBarUI 등 다른 UIDocument들에게 다시 보이라고 알림
    }

    // 현재 팬 오프셋/줌 배율을 캔버스에 실제로 적용
    private void ApplyUpgradeCanvasTransform()
    {
        _upgradeCanvas.style.translate = new Translate(_upgradePan.x, _upgradePan.y);
        _upgradeCanvas.style.scale = new Scale(new Vector3(_upgradeZoom, _upgradeZoom, 1f));
    }

    // 업그레이드 노드들의 격자 좌표(열, 행). 격자 칸 하나 크기(cellWidth/cellHeight) 단위이고, 실제 픽셀 위치는
    // BuildUpgradeTreeSection에서 셀 크기를 곱해서 계산함. UpgradeManager.GetPrerequisite로 정의된 부모-자식 관계를
    // 사방(위/아래/좌/우)으로 자유롭게 배치해서, 사진 속 참고 이미지처럼 직선 격자망 모양으로 보이게 함
    private static readonly Dictionary<UT, Vector2Int> UpgradeGridPositions = new Dictionary<UT, Vector2Int>
    {
        { UT.ClickDamage, new Vector2Int(3, 0) },
        { UT.CritDamage, new Vector2Int(1, 1) },
        { UT.ShardGain, new Vector2Int(5, 1) },
        { UT.CritChanceUp, new Vector2Int(1, 2) },
        { UT.ShardMultiplier, new Vector2Int(5, 2) },
        { UT.AutoClickUnlock, new Vector2Int(1, 3) },
        { UT.ComboUnlock, new Vector2Int(5, 3) },
        { UT.AutoClickSpeed, new Vector2Int(0, 4) },
        { UT.AutoClickCount, new Vector2Int(2, 4) },
        { UT.ComboCooldown, new Vector2Int(4, 4) },
        { UT.ComboDuration, new Vector2Int(6, 4) },
        { UT.LuckyClick, new Vector2Int(2, 5) },
        { UT.DoubleClick, new Vector2Int(6, 5) },
    };

    // 업그레이드 트리 콘텐츠: Click Damage 하나에서 시작해서 끊기지 않고 끝까지 이어지는 트리 하나를,
    // 위 격자 좌표에 맞춰 배치하고 부모-자식 사이를 직선(가로/세로, 필요하면 꺾어서)으로 연결함.
    // 네모를 누르면 UpgradeManager.TryUpgrade로 실제 구매가 이뤄짐
    private VisualElement BuildUpgradeTreeSection(out System.Action refresh, out System.Action reset)
    {
        const float nodeSize = 300f; // 네모 한 변의 길이
        const float cellWidth = 380f; // 격자 한 칸의 가로 크기 (네모 크기 + 여백)
        const float cellHeight = 380f; // 격자 한 칸의 세로 크기
        const float connectorThickness = 6f; // 연결선 두께
        const float canvasWidth = 2800f; // 배경 이미지 + 트리를 담는 캔버스 폭 (팬/줌의 기준 크기)
        const float canvasHeight = 2400f; // 캔버스 높이

        // 캔버스: 화면 중앙에 고정된 크기로 배치되고, translate/scale(ApplyUpgradeCanvasTransform)로만 움직임/확대됨
        var content = new VisualElement();
        content.style.position = Position.Absolute;
        content.style.width = canvasWidth;
        content.style.height = canvasHeight;
        content.style.left = Length.Percent(50);
        content.style.top = Length.Percent(50);
        content.style.marginLeft = -canvasWidth / 2f;
        content.style.marginTop = -canvasHeight / 2f;

        // 노드 하나의 격자 좌표 -> 캔버스 안에서의 중심 픽셀 좌표로 변환
        Vector2 GetNodeCenter(UT type)
        {
            Vector2Int grid = UpgradeGridPositions[type];
            float cellLeft = grid.x * cellWidth + (cellWidth - nodeSize) / 2f;
            float cellTop = grid.y * cellHeight + (cellHeight - nodeSize) / 2f;
            return new Vector2(cellLeft + nodeSize / 2f, cellTop + nodeSize / 2f);
        }

        // 나중에 레벨/파편 상태가 바뀔 때마다 모든 노드를 다시 그리기 위해 (버튼, 업그레이드 타입) 쌍을 모아둠
        var nodeButtons = new List<(Button button, UpgradeManager.UpgradeType type)>();

        // 부모-자식 연결선들: (연결선, 그 연결선이 속한 부모의 타입) - 부모가 1레벨 이상이면 보여줌
        var connectors = new List<(VisualElement connector, UpgradeManager.UpgradeType parentType)>();

        // 이미 한 번 공개(reveal) 애니메이션을 재생한 노드는 다시 재생하지 않도록 기록
        var revealedTypes = new HashSet<UpgradeManager.UpgradeType>();

        // 버튼 클릭 콜백이 정의되기 전에 미리 참조해야 해서 null로 먼저 선언해두고, 트리를 다 만든 뒤에 실제 구현을 대입함
        // (지역 변수라도 클로저는 "그 변수가 나중에 무엇을 가리키는지"를 보고 실행되므로 이렇게 해도 안전함)
        System.Action refreshAll = null;

        // 연결선을 먼저 그려서 버튼들 뒤에 깔리게 함 (VisualElement는 나중에 Add한 게 위에 그려짐)
        foreach (UT type in System.Enum.GetValues(typeof(UT)))
        {
            UT? parentType = UpgradeManager.GetPrerequisite(type);
            if (parentType == null) continue; // 루트(ClickDamage)는 연결선 없음

            VisualElement connector = BuildGridConnector(GetNodeCenter(parentType.Value), GetNodeCenter(type), connectorThickness);
            connector.style.display = DisplayStyle.None;
            connectors.Add((connector, parentType.Value));
            content.Add(connector);
        }

        // 그 위에 노드 버튼들을 격자 좌표대로 절대 배치
        foreach (UT type in System.Enum.GetValues(typeof(UT)))
        {
            Button nodeButton = CreateUpgradeNodeButton(type, nodeSize, nodeButtons, () => refreshAll());
            Vector2 center = GetNodeCenter(type);
            nodeButton.style.position = Position.Absolute;
            nodeButton.style.left = center.x - nodeSize / 2f;
            nodeButton.style.top = center.y - nodeSize / 2f;
            content.Add(nodeButton);
        }

        refreshAll = () => RefreshUpgradeNodes(nodeButtons, revealedTypes, connectors);
        refresh = refreshAll;
        reset = () =>
        {
            UpgradeManager.Instance?.ResetAll();
            revealedTypes.Clear(); // 다음에 다시 공개될 때 튀어나오는 연출이 재생되도록 기록도 같이 초기화
            refreshAll();
        };
        refreshAll(); // 초기 상태(0/5, 공개 여부 등)를 바로 표시

        return content;
    }

    // 부모 중심 -> 자식 중심을 잇는 격자형 연결선. 같은 열/행이면 직선 하나, 아니면 세로로 내려간 뒤 가로로 꺾는 ㄱ자(직각) 2단
    private VisualElement BuildGridConnector(Vector2 parentCenter, Vector2 childCenter, float thickness)
    {
        var wrapper = new VisualElement(); // 세그먼트 1~2개를 감싸서 표시 여부를 한 번에 토글하기 위한 껍데기
        wrapper.pickingMode = PickingMode.Ignore;
        wrapper.style.position = Position.Absolute;
        wrapper.style.left = 0;
        wrapper.style.top = 0;

        bool sameColumn = Mathf.Approximately(parentCenter.x, childCenter.x);
        bool sameRow = Mathf.Approximately(parentCenter.y, childCenter.y);

        if (sameColumn)
        {
            wrapper.Add(BuildConnectorBar(parentCenter.x - thickness / 2f, Mathf.Min(parentCenter.y, childCenter.y), thickness, Mathf.Abs(childCenter.y - parentCenter.y)));
        }
        else if (sameRow)
        {
            wrapper.Add(BuildConnectorBar(Mathf.Min(parentCenter.x, childCenter.x), parentCenter.y - thickness / 2f, Mathf.Abs(childCenter.x - parentCenter.x), thickness));
        }
        else
        {
            // 부모 위치에서 자식의 행까지 먼저 내려간 다음(세로), 자식의 열까지 옆으로 이동(가로)
            wrapper.Add(BuildConnectorBar(parentCenter.x - thickness / 2f, Mathf.Min(parentCenter.y, childCenter.y), thickness, Mathf.Abs(childCenter.y - parentCenter.y)));
            wrapper.Add(BuildConnectorBar(Mathf.Min(parentCenter.x, childCenter.x), childCenter.y - thickness / 2f, Mathf.Abs(childCenter.x - parentCenter.x), thickness));
        }

        return wrapper;
    }

    // 연결선 한 조각(가로 또는 세로 막대)
    private VisualElement BuildConnectorBar(float left, float top, float width, float height)
    {
        var bar = new VisualElement();
        bar.pickingMode = PickingMode.Ignore;
        bar.style.position = Position.Absolute;
        bar.style.left = left;
        bar.style.top = top;
        bar.style.width = width;
        bar.style.height = height;
        bar.style.backgroundColor = Color.white;
        return bar;
    }

    // 업그레이드 노드 버튼 하나를 만들어서 nodeButtons 목록에 등록까지 해줌 (트리 전체 공용)
    private Button CreateUpgradeNodeButton(UpgradeManager.UpgradeType type, float nodeSize, List<(Button button, UpgradeManager.UpgradeType type)> nodeButtons, System.Action onClicked)
    {
        var nodeButton = new Button(); // 업그레이드 노드 하나
        nodeButton.style.width = nodeSize;
        nodeButton.style.height = nodeSize;
        nodeButton.style.fontSize = 32;
        nodeButton.style.whiteSpace = WhiteSpace.Normal;
        nodeButton.style.unityTextAlign = TextAnchor.MiddleCenter;
        nodeButton.style.color = Color.white;

        // 네모 칸 자체에 배경 이미지를 깔아줌 (색 오버레이는 RefreshUpgradeNodes에서 반투명하게 덧씌워 상태만 표시)
        if (upgradeTreeBackground != null)
        {
            nodeButton.style.backgroundImage = new StyleBackground(upgradeTreeBackground);
            nodeButton.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            nodeButton.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
        }

        nodeButton.clicked += () =>
        {
            bool success = UpgradeManager.Instance != null && UpgradeManager.Instance.TryUpgrade(type);

            // 실제로 레벨이 올라간 경우에만 연출 재생 (파편 부족/잠김 등으로 실패했을 땐 재생 안 함)
            if (success)
                StartCoroutine(PlayUpgradeRevealAnimation(nodeButton));

            onClicked();
        };

        nodeButtons.Add((nodeButton, type));
        return nodeButton;
    }

    // 업그레이드 노드 버튼들의 텍스트/색상/공개 여부, 그리고 각 연결선의 표시 여부를 UpgradeManager의 현재 레벨에 맞게 다시 그림
    private void RefreshUpgradeNodes(
        List<(Button button, UpgradeManager.UpgradeType type)> nodeButtons,
        HashSet<UpgradeManager.UpgradeType> revealedTypes,
        List<(VisualElement connector, UpgradeManager.UpgradeType parentType)> connectors)
    {
        foreach (var (button, type) in nodeButtons)
        {
            if (UpgradeManager.Instance == null)
            {
                button.text = $"{type}\n0/5";
                continue;
            }

            bool isRevealed = !UpgradeManager.Instance.IsLocked(type); // 부모 업그레이드가 1레벨 이상이면 공개됨

            if (!isRevealed)
            {
                button.style.display = DisplayStyle.None; // 아직 공개 전이면 아예 안 보이게 숨김
                continue;
            }

            bool justRevealed = revealedTypes.Add(type); // 이번에 처음 공개되는 순간인지 (HashSet.Add가 처음이면 true 반환)
            button.style.display = DisplayStyle.Flex;

            if (justRevealed)
                StartCoroutine(PlayUpgradeRevealAnimation(button));

            int maxLevel = UpgradeManager.Instance.GetMaxLevel(type); // 단일 구매 업그레이드는 1, 나머지는 5
            int level = UpgradeManager.Instance.GetLevel(type); // 현재 레벨
            bool maxed = level >= maxLevel; // 이미 최대 레벨인 상태
            int nextCost = UpgradeManager.Instance.GetNextCost(type); // 다음 레벨 비용 (최대 레벨이면 -1)

            string name = UpgradeManager.Instance.GetDisplayName(type);
            string levelLine = $"{level}/{maxLevel}"; // N/Max 표시
            string costLine = maxed ? "MAX" : $"{nextCost}"; // 파편 비용 (최대면 MAX만 표시)

            button.text = $"{name}\n{levelLine}\n{costLine}";

            // 배경 이미지가 비쳐 보이도록 색은 얇게만 덧씌움 (상태 구분용 틴트)
            button.style.backgroundColor = maxed
                ? new Color(0.2f, 0.55f, 0.2f, 0.45f) // 최대 레벨: 초록
                : new Color(0f, 0f, 0f, 0.35f); // 기본: 텍스트 가독성용 살짝만
        }

        foreach (var (connector, parentType) in connectors)
        {
            bool parentLeveled = UpgradeManager.Instance != null && UpgradeManager.Instance.GetLevel(parentType) >= 1;
            connector.style.display = parentLeveled ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    // 새로 공개된 업그레이드 노드가 튀어나올 때 재생하는 연출: 왼쪽으로 살짝 기울었다가 정자세로 돌아오면서 반짝임
    private IEnumerator PlayUpgradeRevealAnimation(VisualElement node)
    {
        const float duration = 0.35f; // 연출 총 시간(초)
        const float startAngleDegrees = -18f; // 시작 회전 각도 (왼쪽으로 기울어짐)
        const float flashPeakAlpha = 0.35f; // 반짝임 최대 밝기 (기존 0.9는 너무 쌔서 낮춤)

        // 반짝임 효과용 흰색 오버레이 - 클릭을 막으면 안 되므로 Ignore로 설정
        var flash = new VisualElement();
        flash.pickingMode = PickingMode.Ignore;
        flash.style.position = Position.Absolute;
        flash.style.left = 0;
        flash.style.right = 0;
        flash.style.top = 0;
        flash.style.bottom = 0;
        flash.style.backgroundColor = new Color(1f, 1f, 1f, flashPeakAlpha);
        node.Add(flash);

        float elapsed = 0f; // 연출 시작 후 흐른 시간

        while (elapsed < duration)
        {
            // 업그레이드 페이지가 열려있는 동안 Time.timeScale이 0이라 deltaTime 대신 unscaledDeltaTime을 써야 연출이 재생됨
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration); // 0~1 진행률
            float eased = 1f - Mathf.Pow(1f - progress, 3f); // ease-out(처음엔 빠르게, 끝에서 천천히)

            float angle = Mathf.Lerp(startAngleDegrees, 0f, eased);
            node.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));

            float flashAlpha = Mathf.Lerp(flashPeakAlpha, 0f, progress);
            flash.style.backgroundColor = new Color(1f, 1f, 1f, flashAlpha);

            yield return null;
        }

        node.style.rotate = new Rotate(new Angle(0f, AngleUnit.Degree));
        node.Remove(flash);
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
        out System.Action refresh)
    {
        var content = new VisualElement();
        content.style.position = Position.Absolute;
        content.style.left = 16;
        content.style.right = 16;
        content.style.top = 60;
        content.style.bottom = 16;
        content.style.display = DisplayStyle.None;

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
        var selectButton = new Button() { text = "Select" };
        selectButton.style.position = Position.Absolute;
        selectButton.style.top = Length.Percent(55);
        selectButton.style.right = 350; // 오른쪽 기준 30px 왼쪽으로 이동
        selectButton.style.width = 140;
        selectButton.style.height = 44;
        selectButton.style.fontSize = 18;
        selectButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        selectButton.style.backgroundColor = new Color(0.2f, 0.45f, 0.2f, 0.9f);
        selectButton.style.color = Color.white;

        var equippedLabel = new Label("Equipped");
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

        selectButton.clicked += () =>
        {
            onSelect(browse.index);
            RefreshSelectorState(nameLabel, selectButton, equippedLabel, previewImage, getName, getIcon, getEquippedIndex, browse.index);
        };

        content.Add(selectButton);
        content.Add(equippedLabel);
        content.Add(previewImage);

        var prevButton = new Button(() =>
        {
            int count = getCount();
            if (count <= 0) return;
            browse.index = Mathf.Max(0, browse.index - 1);
            RefreshSelectorState(nameLabel, selectButton, equippedLabel, previewImage, getName, getIcon, getEquippedIndex, browse.index);
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
            RefreshSelectorState(nameLabel, selectButton, equippedLabel, previewImage, getName, getIcon, getEquippedIndex, browse.index);
        })
        { text = ">" };
        SetArrowButtonStyle(nextButton);

        arrowRow.Add(prevButton);
        arrowRow.Add(nameLabel);
        arrowRow.Add(nextButton);

        content.Add(arrowRow);

        // 외부(Start, OpenPanel)에서 필요할 때마다 최신 상태로 다시 그릴 수 있도록 넘겨줌
        refresh = () => RefreshSelectorState(nameLabel, selectButton, equippedLabel, previewImage, getName, getIcon, getEquippedIndex, browse.index);

        return content;
    }

    // 이름 라벨, 미리보기 이미지, Select 버튼/Equipped 표시 중 무엇을 보여줄지 갱신
    private void RefreshSelectorState(
        Label nameLabel,
        Button selectButton,
        Label equippedLabel,
        Image previewImage,
        System.Func<int, string> getName,
        System.Func<int, Sprite> getIcon,
        System.Func<int> getEquippedIndex,
        int index)
    {
        nameLabel.text = getName(index);
        previewImage.sprite = getIcon(index);

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

    // 가로로 긴 직사각형 버튼 (폭은 부모 컬럼을 꽉 채우고, 높이는 낮게)
    private Button CreateButton(string text, System.Action onClick)
    {
        var button = new Button(onClick) { text = text };
        button.style.height = 70;
        button.style.marginTop = 8;
        button.style.marginBottom = 8;
        button.style.fontSize = 20;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        button.style.color = Color.white;
        button.style.borderTopLeftRadius = 6;
        button.style.borderTopRightRadius = 6;
        button.style.borderBottomLeftRadius = 6;
        button.style.borderBottomRightRadius = 6;
        return button;
    }

    private void OpenPanel(string title, VisualElement contentToShow)
    {
        _panelTitle.text = title;
        _currentlyOpenContent = contentToShow;
        _weaponContent.style.display = contentToShow == _weaponContent ? DisplayStyle.Flex : DisplayStyle.None;
        _objectContent.style.display = contentToShow == _objectContent ? DisplayStyle.Flex : DisplayStyle.None;
        _panel.style.display = DisplayStyle.Flex;

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

        // Object 팝업이 열려있는 동안 선택이 바뀌었다면, 닫히는 지금 전환 애니메이션 재생
        if (_currentlyOpenContent == _objectContent && ObjectManager.Instance != null)
        {
            ObjectData current = ObjectManager.Instance.CurrentObject;

            if (current != _objectAtPanelOpen)
                ObjectSwapController.Instance?.TriggerSwap(_objectAtPanelOpen, current);
        }
    }
}
