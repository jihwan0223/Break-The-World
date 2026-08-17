using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class GoldUI : MonoBehaviour
{
    [SerializeField] private float leftOffset = 20f;
    [SerializeField] private float topOffset = 20f;

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

        BuildLabel(uiDocument.rootVisualElement);
        UpdateLabel(0);
    }

    void Start()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnGoldChanged += UpdateLabel;
            UpdateLabel(CurrencyManager.Instance.Gold);
        }
    }

    void OnDisable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGoldChanged -= UpdateLabel;
    }

    private void BuildLabel(VisualElement root)
    {
        root.Clear();

        var background = new VisualElement();
        background.style.position = Position.Absolute;
        background.style.top = topOffset;
        background.style.left = leftOffset;
        background.style.paddingLeft = 10;
        background.style.paddingRight = 10;
        background.style.paddingTop = 4;
        background.style.paddingBottom = 4;
        background.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        background.style.borderTopLeftRadius = 4;
        background.style.borderTopRightRadius = 4;
        background.style.borderBottomLeftRadius = 4;
        background.style.borderBottomRightRadius = 4;

        _label = new Label();
        _label.style.fontSize = 20;
        _label.style.color = Color.white;
        _label.style.unityFontStyleAndWeight = FontStyle.Bold;

        background.Add(_label);
        root.Add(background);
    }

    private void UpdateLabel(int gold)
    {
        _label.text = $"Gold: {gold}";
    }
}
