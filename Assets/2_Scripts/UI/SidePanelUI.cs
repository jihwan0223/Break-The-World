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
    private bool _isDraggingUpgradeCanvas; // 지금 마우스로 캔버스를 드래그하는 중인지
    private Vector2 _dragStartMousePos; // 드래그 시작한 순간의 마우스 화면 좌표
    private Vector2 _dragStartPan; // 드래그 시작한 순간의 팬 오프셋
    private Vector2 _upgradePan; // 현재 팬 오프셋(px)
    private float _upgradeZoom = 1f; // 현재(화면에 실제로 적용되는, 매끄럽게 보간되는) 줌 배율
    private float _upgradeZoomTarget = 1f; // 스크롤 입력이 가리키는 목표 줌 배율 - _upgradeZoom이 매 프레임 이 값을 따라감

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

        _upgradeZoom = Mathf.Lerp(_upgradeZoom, _upgradeZoomTarget, Time.deltaTime * upgradeZoomSmoothSpeed);
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
        _upgradeCanvas = BuildUpgradeTreeSection(out _refreshUpgradeTree);
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

        return overlay;
    }

    private void OpenUpgradeOverlay()
    {
        _upgradeOverlay.style.display = DisplayStyle.Flex;
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
    }

    // 현재 팬 오프셋/줌 배율을 캔버스에 실제로 적용
    private void ApplyUpgradeCanvasTransform()
    {
        _upgradeCanvas.style.translate = new Translate(_upgradePan.x, _upgradePan.y);
        _upgradeCanvas.style.scale = new Scale(new Vector3(_upgradeZoom, _upgradeZoom, 1f));
    }

    // 업그레이드 트리 콘텐츠: 배경 이미지 위에 브랜치 2개(클릭 / 골드)를 가로로 나열하고,
    // 각 브랜치 안에서 네모(버튼)들을 세로 막대(선)로 연결. 네모를 누르면 UpgradeManager.TryUpgrade로 실제 구매가 이뤄짐
    private VisualElement BuildUpgradeTreeSection(out System.Action refresh)
    {
        const float nodeSize = 300f; // 네모 한 변의 길이
        const float connectorLength = 40f; // 네모 사이 연결선 길이
        const float connectorThickness = 4f; // 연결선 두께
        const float canvasWidth = 2200f; // 배경 이미지 + 트리를 담는 캔버스 폭 (팬/줌의 기준 크기)
        const float canvasHeight = 1500f; // 캔버스 높이

        // 브랜치별로 어떤 업그레이드 노드가 세로로 이어지는지 순서 정의
        var branches = new[]
        {
            new[] { UpgradeManager.UpgradeType.ClickDamage, UpgradeManager.UpgradeType.CritChance, UpgradeManager.UpgradeType.CritMultiplier },
            new[] { UpgradeManager.UpgradeType.GoldGainPercent, UpgradeManager.UpgradeType.GlobalGoldMultiplier },
        };

        // 캔버스: 화면 중앙에 고정된 크기로 배치되고, translate/scale(ApplyUpgradeCanvasTransform)로만 움직임/확대됨
        var content = new VisualElement();
        content.style.position = Position.Absolute;
        content.style.width = canvasWidth;
        content.style.height = canvasHeight;
        content.style.left = Length.Percent(50);
        content.style.top = Length.Percent(50);
        content.style.marginLeft = -canvasWidth / 2f;
        content.style.marginTop = -canvasHeight / 2f;
        content.style.flexDirection = FlexDirection.Row; // 브랜치들을 가로로 나열
        content.style.justifyContent = Justify.SpaceEvenly;
        content.style.alignItems = Align.Center;

        // 캔버스 전체 배경은 오버레이의 검은색을 그대로 씀 (이미지는 네모 버튼 안에만 적용)

        // 나중에 레벨/골드 상태가 바뀔 때마다 모든 노드를 다시 그리기 위해 (버튼, 업그레이드 타입) 쌍을 모아둠
        var nodeButtons = new List<(Button button, UpgradeManager.UpgradeType type)>();

        foreach (var branchTypes in branches)
        {
            var branchColumn = new VisualElement(); // 브랜치 하나(네모+선 세로 스택)
            branchColumn.style.flexDirection = FlexDirection.Column;
            branchColumn.style.alignItems = Align.Center;
            branchColumn.style.marginTop = 20;

            for (int node = 0; node < branchTypes.Length; node++)
            {
                UpgradeManager.UpgradeType type = branchTypes[node]; // 이 네모가 나타내는 업그레이드 종류

                var nodeButton = new Button(); // 업그레이드 노드 하나
                nodeButton.style.width = nodeSize;
                nodeButton.style.height = nodeSize;
                nodeButton.style.fontSize = 32;
                nodeButton.style.whiteSpace = WhiteSpace.Normal;
                nodeButton.style.unityTextAlign = TextAnchor.MiddleCenter;
                nodeButton.style.color = Color.white;

                // 네모 칸 자체에도 배경 이미지를 깔아줌 (색 오버레이는 RefreshUpgradeNodes에서 반투명하게 덧씌워 상태만 표시)
                if (upgradeTreeBackground != null)
                {
                    nodeButton.style.backgroundImage = new StyleBackground(upgradeTreeBackground);
                    nodeButton.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
                    nodeButton.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                }

                nodeButton.clicked += () =>
                {
                    UpgradeManager.Instance?.TryUpgrade(type);
                    RefreshUpgradeNodes(nodeButtons);
                };
                branchColumn.Add(nodeButton);
                nodeButtons.Add((nodeButton, type));

                bool isLastNode = node == branchTypes.Length - 1;
                if (!isLastNode)
                {
                    var connector = new VisualElement(); // 위/아래 네모를 잇는 세로 선
                    connector.style.width = connectorThickness;
                    connector.style.height = connectorLength;
                    connector.style.backgroundColor = Color.white;
                    branchColumn.Add(connector);
                }
            }

            content.Add(branchColumn);
        }

        refresh = () => RefreshUpgradeNodes(nodeButtons);
        RefreshUpgradeNodes(nodeButtons); // 초기 상태(0/5 등)를 바로 표시

        return content;
    }

    // 업그레이드 노드 버튼들의 텍스트/색상을 UpgradeManager의 현재 레벨에 맞게 다시 그림
    private void RefreshUpgradeNodes(List<(Button button, UpgradeManager.UpgradeType type)> nodeButtons)
    {
        foreach (var (button, type) in nodeButtons)
        {
            if (UpgradeManager.Instance == null)
            {
                button.text = $"{type}\n0/{UpgradeManager.MaxLevel}";
                continue;
            }

            bool locked = UpgradeManager.Instance.IsLocked(type); // 선행 업그레이드가 없어서 아직 못 사는 상태
            int level = UpgradeManager.Instance.GetLevel(type); // 현재 레벨
            bool maxed = level >= UpgradeManager.MaxLevel; // 이미 최대 레벨인 상태
            int nextCost = UpgradeManager.Instance.GetNextCost(type); // 다음 레벨 비용 (최대 레벨이면 -1)

            string name = UpgradeManager.Instance.GetDisplayName(type);
            string levelLine = $"{level}/{UpgradeManager.MaxLevel}"; // N/Max 표시
            string costLine = locked ? "" : maxed ? "MAX" : $"{nextCost}G"; // 잠김이면 비용 대신 Locked만 표시

            button.text = locked ? $"{name}\nLocked" : $"{name}\n{levelLine}\n{costLine}";

            // 배경 이미지가 비쳐 보이도록 색은 얇게만 덧씌움 (상태 구분용 틴트)
            if (locked)
                button.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.55f); // 잠김: 어둡게
            else if (maxed)
                button.style.backgroundColor = new Color(0.2f, 0.55f, 0.2f, 0.45f); // 최대 레벨: 초록
            else
                button.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f); // 기본: 텍스트 가독성용 살짝만
        }
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
