using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class RespawnTimerUI : MonoBehaviour
{
    [SerializeField] private Health targetHealth;
    [SerializeField] private float topOffset = 60f;

    private VisualElement _background;
    private Label _label;

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

        _background = new VisualElement();
        _background.style.position = Position.Absolute;
        _background.style.top = topOffset;
        _background.style.left = Length.Percent(50);
        _background.style.marginLeft = -70;
        _background.style.paddingLeft = 10;
        _background.style.paddingRight = 10;
        _background.style.paddingTop = 4;
        _background.style.paddingBottom = 4;
        _background.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        _background.style.borderTopLeftRadius = 4;
        _background.style.borderTopRightRadius = 4;
        _background.style.borderBottomLeftRadius = 4;
        _background.style.borderBottomRightRadius = 4;
        _background.style.display = DisplayStyle.None; // 평소엔 숨김, 재생성 대기 중일 때만 표시

        _label = new Label();
        _label.style.fontSize = 18;
        _label.style.color = Color.white;
        _label.style.unityFontStyleAndWeight = FontStyle.Bold;

        _background.Add(_label);
        root.Add(_background);
    }

    void Update()
    {
        if (targetHealth == null || _background == null)
            return;

        if (targetHealth.IsDead && targetHealth.RespawnRemaining > 0f)
        {
            _background.style.display = DisplayStyle.Flex;
            _label.text = $"Respawn: {targetHealth.RespawnRemaining:F1}s";
        }
        else
        {
            _background.style.display = DisplayStyle.None;
        }
    }
}
