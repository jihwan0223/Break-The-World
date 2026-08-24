using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SidePanelUI : MonoBehaviour
{
    private VisualElement _panel;
    private Label _panelTitle;

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

    private void Build(VisualElement root)
    {
        root.Clear();

        // 화면(1920x1080 기준)보다 살짝 작은 크기로 거의 꽉 채우는 빈 팝업 패널
        _panel = new VisualElement();
        _panel.style.position = Position.Absolute;
        _panel.style.left = 40;
        _panel.style.right = 40;
        _panel.style.top = 40;
        _panel.style.bottom = 40;
        _panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
        _panel.style.borderTopLeftRadius = 8;
        _panel.style.borderTopRightRadius = 8;
        _panel.style.borderBottomLeftRadius = 8;
        _panel.style.borderBottomRightRadius = 8;
        _panel.style.display = DisplayStyle.None;

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

        var weaponButton = CreateButton("무기", () =>
        {
            Debug.Log("무기 버튼 눌림");
            OpenPanel("무기");
        });

        var objectButton = CreateButton("오브젝트", () =>
        {
            Debug.Log("오브젝트 버튼 눌림");
            OpenPanel("오브젝트");
        });

        buttonColumn.Add(weaponButton);
        buttonColumn.Add(objectButton);
        root.Add(buttonColumn);
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

    private void OpenPanel(string title)
    {
        _panelTitle.text = title;
        _panel.style.display = DisplayStyle.Flex;
    }

    private void ClosePanel()
    {
        _panel.style.display = DisplayStyle.None;
    }
}
