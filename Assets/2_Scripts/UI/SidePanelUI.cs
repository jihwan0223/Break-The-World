using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SidePanelUI : MonoBehaviour
{
    private VisualElement _panel; // 튀어나오는 빈 팝업 패널
    private Label _panelTitle; // 팝업 좌상단 제목 ("Weapon" / "Object")
    private VisualElement _weaponContent; // 무기 팝업일 때만 보이는 화살표+이름 영역
    private Label _weaponNameLabel; // 현재 장착 중인 무기 이름 표시
    private VisualElement _objectContent; // 오브젝트 팝업일 때만 보이는 화살표+이름 영역
    private Label _objectNameLabel; // 현재 선택된 오브젝트 이름 표시

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
        {
            WeaponManager.Instance.OnWeaponChanged += HandleWeaponChanged;
            RefreshWeaponLabel();
        }

        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.OnObjectChanged += HandleObjectChanged;
            RefreshObjectLabel();
        }
    }

    void OnDisable()
    {
        if (WeaponManager.Instance != null)
            WeaponManager.Instance.OnWeaponChanged -= HandleWeaponChanged;

        if (ObjectManager.Instance != null)
            ObjectManager.Instance.OnObjectChanged -= HandleObjectChanged;
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

        _weaponNameLabel = new Label("Bare Hands");
        _weaponContent = BuildArrowSection(
            _weaponNameLabel,
            () =>
            {
                Debug.Log("무기 이전 화살표 눌림");
                WeaponManager.Instance?.SelectPrevious();
            },
            () =>
            {
                Debug.Log("무기 다음 화살표 눌림");
                WeaponManager.Instance?.SelectNext();
            });
        _panel.Add(_weaponContent);

        _objectNameLabel = new Label("Plate");
        _objectContent = BuildArrowSection(
            _objectNameLabel,
            () =>
            {
                Debug.Log("오브젝트 이전 화살표 눌림");
                ObjectManager.Instance?.SelectPrevious();
            },
            () =>
            {
                Debug.Log("오브젝트 다음 화살표 눌림");
                ObjectManager.Instance?.SelectNext();
            });
        _panel.Add(_objectContent);

        root.Add(_panel);

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

        buttonColumn.Add(weaponButton);
        buttonColumn.Add(objectButton);
        root.Add(buttonColumn);
    }

    // 팝업 안에 들어갈 화살표(<, >) + 이름 라벨 영역. 무기/오브젝트 팝업이 같은 모양을 공유하도록 공용화
    private VisualElement BuildArrowSection(Label nameLabel, System.Action onPrev, System.Action onNext)
    {
        var content = new VisualElement();
        content.style.position = Position.Absolute;
        content.style.left = 0;
        content.style.right = 0;
        content.style.top = 70; // 제목/닫기 버튼 바로 아래, 위쪽에 배치 (아래는 이미지 자리로 비워둠)
        content.style.height = 60;
        content.style.flexDirection = FlexDirection.Row;
        content.style.justifyContent = Justify.Center;
        content.style.alignItems = Align.Center;
        content.style.display = DisplayStyle.None;

        var prevButton = new Button(onPrev) { text = "<" };
        SetArrowButtonStyle(prevButton);

        nameLabel.style.width = 240;
        nameLabel.style.fontSize = 24;
        nameLabel.style.color = Color.white;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        var nextButton = new Button(onNext) { text = ">" };
        SetArrowButtonStyle(nextButton);

        content.Add(prevButton);
        content.Add(nameLabel);
        content.Add(nextButton);

        return content;
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
        _weaponContent.style.display = contentToShow == _weaponContent ? DisplayStyle.Flex : DisplayStyle.None;
        _objectContent.style.display = contentToShow == _objectContent ? DisplayStyle.Flex : DisplayStyle.None;
        _panel.style.display = DisplayStyle.Flex;
    }

    private void ClosePanel()
    {
        _panel.style.display = DisplayStyle.None;
    }

    private void HandleWeaponChanged(WeaponData newWeapon)
    {
        RefreshWeaponLabel();
    }

    private void RefreshWeaponLabel()
    {
        if (_weaponNameLabel != null && WeaponManager.Instance != null)
            _weaponNameLabel.text = WeaponManager.Instance.CurrentWeapon.weaponName;
    }

    private void HandleObjectChanged(ObjectData newObject)
    {
        RefreshObjectLabel();
    }

    private void RefreshObjectLabel()
    {
        if (_objectNameLabel != null && ObjectManager.Instance != null)
            _objectNameLabel.text = ObjectManager.Instance.CurrentObject.objectName;
    }
}
