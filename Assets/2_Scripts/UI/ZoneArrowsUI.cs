using UnityEngine;
using UnityEngine.UIElements;

// 화면 좌우 끝에 붙는 지역 이동 화살표. 갈 수 있는 방향만 보이고, 두 번째 존이 해금되기 전에는 전부 숨김.
[RequireComponent(typeof(UIDocument))]
public class ZoneArrowsUI : MonoBehaviour
{
    [SerializeField] private float sideOffset = 24f; // 화면 좌우 끝에서 띄울 거리(px)
    [SerializeField] private float arrowSize = 64f; // 화살표 버튼 한 변 길이(px)

    private VisualElement _prevButton; // 이전(좁은 스케일) 존으로
    private VisualElement _nextButton; // 다음(넓은 스케일) 존으로

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

        if (ZoneManager.Instance != null)
            ZoneManager.Instance.OnZoneChanged += Refresh;

        Refresh();
    }

    void OnDisable()
    {
        if (ZoneManager.Instance != null)
            ZoneManager.Instance.OnZoneChanged -= Refresh;
    }

    private void Build(VisualElement root)
    {
        root.Clear();
        GameFonts.Apply(root);
        root.pickingMode = PickingMode.Ignore; // 화살표 밖 영역은 뒤쪽 오브젝트 클릭이 통과해야 함

        _prevButton = CreateArrow("<", true);
        _nextButton = CreateArrow(">", false);

        root.Add(_prevButton);
        root.Add(_nextButton);
    }

    private VisualElement CreateArrow(string label, bool isLeft)
    {
        var button = new Button(() =>
        {
            if (ZoneManager.Instance == null) return;
            if (isLeft) ZoneManager.Instance.GoPrev();
            else ZoneManager.Instance.GoNext();
        })
        { text = label };

        button.style.position = Position.Absolute;
        button.style.top = Length.Percent(50);
        button.style.marginTop = -arrowSize / 2f; // 버튼 높이 절반만큼 올려 세로 중앙 정렬
        if (isLeft) button.style.left = sideOffset;
        else button.style.right = sideOffset;

        button.style.width = arrowSize;
        button.style.height = arrowSize;
        button.style.fontSize = 32;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        button.style.borderTopLeftRadius = arrowSize / 2f;
        button.style.borderTopRightRadius = arrowSize / 2f;
        button.style.borderBottomLeftRadius = arrowSize / 2f;
        button.style.borderBottomRightRadius = arrowSize / 2f;

        button.RegisterCallback<PointerEnterEvent>(_ => UIPointerGuard.IsPointerOverUI = true);
        button.RegisterCallback<PointerLeaveEvent>(_ => UIPointerGuard.IsPointerOverUI = false);

        return button;
    }

    // 갈 수 있는 방향만 보여줌. 두 번째 존이 아직 안 열렸으면 양쪽 다 숨김
    private void Refresh()
    {
        if (_prevButton == null || _nextButton == null) return;

        bool anyZoneUnlocked = ZoneManager.Instance != null && ZoneManager.Instance.UnlockedMaxZone > 0;
        bool canPrev = anyZoneUnlocked && ZoneManager.Instance.CanGoPrev;
        bool canNext = anyZoneUnlocked && ZoneManager.Instance.CanGoNext;

        _prevButton.style.display = canPrev ? DisplayStyle.Flex : DisplayStyle.None;
        _nextButton.style.display = canNext ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
