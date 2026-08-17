using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Health targetHealth;
    [SerializeField] private float barWidth = 300f;
    [SerializeField] private float barHeight = 24f;
    [SerializeField] private float topOffset = 20f;

    private VisualElement _fill;

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();

        if (uiDocument.panelSettings == null)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            uiDocument.panelSettings = settings;
        }

        BuildBar(uiDocument.rootVisualElement);

        if (targetHealth != null)
        {
            targetHealth.OnDamaged += UpdateBar;
            targetHealth.OnDied += HandleDied;
            UpdateBar(targetHealth.CurrentHP, targetHealth.MaxHP);
        }
    }

    void OnDisable()
    {
        if (targetHealth != null)
        {
            targetHealth.OnDamaged -= UpdateBar;
            targetHealth.OnDied -= HandleDied;
        }
    }

    private void BuildBar(VisualElement root)
    {
        root.Clear();

        var background = new VisualElement();
        background.style.position = Position.Absolute;
        background.style.top = topOffset;
        background.style.left = Length.Percent(50);
        background.style.marginLeft = -barWidth / 2f;
        background.style.width = barWidth;
        background.style.height = barHeight;
        background.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        background.style.borderTopLeftRadius = 4;
        background.style.borderTopRightRadius = 4;
        background.style.borderBottomLeftRadius = 4;
        background.style.borderBottomRightRadius = 4;

        _fill = new VisualElement();
        _fill.style.position = Position.Absolute;
        _fill.style.left = 0;
        _fill.style.top = 0;
        _fill.style.bottom = 0;
        _fill.style.width = Length.Percent(100);
        _fill.style.backgroundColor = new Color(0.85f, 0.2f, 0.2f);
        _fill.style.borderTopLeftRadius = 4;
        _fill.style.borderBottomLeftRadius = 4;

        background.Add(_fill);
        root.Add(background);
    }

    private void UpdateBar(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        _fill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);
    }

    private void HandleDied()
    {
        _fill.style.width = Length.Percent(0);
    }
}
