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

    // 업그레이드 페이지는 UI 툴킷으로 만들었던 걸 지웠음(유저가 Canvas로 직접 새로 만들 예정) - 이 이벤트 자체는
    // PieceUI/HealthBarUI가 계속 구독하고 있어서 남겨둠(그쪽 스크립트를 안 건드리려고). 지금은 아무도 Invoke 안 해서
    // 항상 안 숨겨진(보이는) 상태로 유지됨 - 나중에 새 업그레이드 페이지에서 필요하면 여기 이벤트를 다시 Invoke하면 됨
    public static event System.Action<bool> OnUpgradeOverlayToggled;

    // Weapon/Object 팝업(_panel)이 열리면 true, 닫히면 false로 전달 - 화면 우상단을 가릴 수 있는 다른
    // UIDocument(PieceUI 등)가 스스로 숨고 보여주는 데 사용
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

        // 화면(1920x1080 기준)보다 살짝 작은 크기로 거의 꽉 채우는 빈 팝업 패널
        _panel = new VisualElement();
        _panel.style.position = Position.Absolute;
        _panel.style.left = 40;
        // 우상단 조각 표시(PieceUI)와 겹치지 않도록 그 자리만큼 오른쪽을 비워둠 - 버튼은 이제 좌상단에 있어서
        // 패널이 오른쪽 끝까지 안 가도 되고, 대신 조각 표시가 가려지지 않게 이 여백이 필요함
        _panel.style.right = 280;
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
            out _refreshObjectSelector,
            index => ObjectManager.Instance != null && !ObjectManager.Instance.IsUnlocked(index));
        _panel.Add(_objectContent);

        root.Add(_panel);

        // Weapon/Object 버튼 2개가 화면 좌상단에 일렬로 나오는 가로줄
        // (업그레이드 페이지는 유저가 Canvas로 직접 새로 만들 예정이라 버튼도 같이 지웠음)
        var buttonRow = new VisualElement();
        buttonRow.style.position = Position.Absolute;
        buttonRow.style.left = 20;
        buttonRow.style.top = 20;
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.alignItems = Align.Center;

        // 버튼 줄 위에 포인터가 있는 동안도 마찬가지로 월드 클릭을 막음
        buttonRow.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        buttonRow.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);

        var weaponButton = CreateButton("무기", () =>
        {
            Debug.Log("무기 버튼 눌림");
            OpenPanel("무기", _weaponContent);
        });

        var objectButton = CreateButton("오브젝트", () =>
        {
            Debug.Log("오브젝트 버튼 눌림");
            OpenPanel("오브젝트", _objectContent);
        });

        weaponButton.style.marginRight = 8;

        buttonRow.Add(weaponButton);
        buttonRow.Add(objectButton);
        root.Add(buttonRow);
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
        System.Func<int, bool> isLocked = null) // Object 패널에서만 씀 - null이면(Weapon) 잠김 개념 자체가 없음
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

        selectButton.clicked += () =>
        {
            if (isLocked != null && isLocked(browse.index))
                return; // 잠긴 오브젝트는 여기서 선택 안 됨 - 업그레이드 트리에서 먼저 해금해야 함

            onSelect(browse.index);
            RefreshSelectorState(nameLabel, selectButton, equippedLabel, previewImage, getName, getIcon, getEquippedIndex, browse.index, isLocked);
        };

        content.Add(selectButton);
        content.Add(equippedLabel);
        content.Add(previewImage);

        var prevButton = new Button(() =>
        {
            int count = getCount();
            if (count <= 0) return;
            browse.index = Mathf.Max(0, browse.index - 1);
            RefreshSelectorState(nameLabel, selectButton, equippedLabel, previewImage, getName, getIcon, getEquippedIndex, browse.index, isLocked);
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
            RefreshSelectorState(nameLabel, selectButton, equippedLabel, previewImage, getName, getIcon, getEquippedIndex, browse.index, isLocked);
        })
        { text = ">" };
        SetArrowButtonStyle(nextButton);

        arrowRow.Add(prevButton);
        arrowRow.Add(nameLabel);
        arrowRow.Add(nextButton);

        content.Add(arrowRow);

        // 외부(Start, OpenPanel)에서 필요할 때마다 최신 상태로 다시 그릴 수 있도록 넘겨줌
        refresh = () => RefreshSelectorState(nameLabel, selectButton, equippedLabel, previewImage, getName, getIcon, getEquippedIndex, browse.index, isLocked);

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
        int index,
        System.Func<int, bool> isLocked = null)
    {
        nameLabel.text = getName(index);
        previewImage.sprite = getIcon(index);

        // 잠긴 오브젝트는 Select 대신 Locked만 보여주고, Equipped 표시는 아예 숨김
        if (isLocked != null && isLocked(index))
        {
            selectButton.text = "잠김";
            selectButton.style.display = DisplayStyle.Flex;
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

    // 좌상단 가로줄에 나란히 놓이는 버튼 (고정 폭/높이)
    private Button CreateButton(string text, System.Action onClick)
    {
        var button = new Button(onClick) { text = text };
        button.style.width = 110;
        button.style.height = 70;
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

        OnSelectorPanelToggled?.Invoke(true); // PieceUI 등 우상단 UI들에게 숨으라고 알림

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

        OnSelectorPanelToggled?.Invoke(false); // PieceUI 등 우상단 UI들에게 다시 보이라고 알림

        // Object 팝업이 열려있는 동안 선택이 바뀌었다면, 닫히는 지금 전환 애니메이션 재생
        if (_currentlyOpenContent == _objectContent && ObjectManager.Instance != null)
        {
            ObjectData current = ObjectManager.Instance.CurrentObject;

            if (current != _objectAtPanelOpen)
                ObjectSwapController.Instance?.TriggerSwap(_objectAtPanelOpen, current);
        }
    }
}
