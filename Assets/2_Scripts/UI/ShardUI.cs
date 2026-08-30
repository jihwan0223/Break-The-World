using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class ShardUI : MonoBehaviour
{
    [SerializeField] private float leftOffset = 20f;
    [SerializeField] private float topOffset = 20f;

    private Label _label; // "Shards: N" 표시용 라벨

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();

        if (uiDocument.panelSettings == null)
        {
            // PanelSettings + 기본 런타임 테마를 자동으로 채워줌 (HealthBarUI와 동일한 이유)
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            uiDocument.panelSettings = settings;
        }

        BuildLabel(uiDocument.rootVisualElement);
        UpdateLabel(0);
    }

    void Start()
    {
        // CurrencyManager.Awake()가 먼저 실행되도록 OnEnable이 아닌 Start에서 구독
        // (Unity는 모든 오브젝트의 Awake를 먼저 실행한 뒤 Start를 실행하므로 순서가 보장됨)
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnShardsChanged += UpdateLabel;
            UpdateLabel(CurrencyManager.Instance.Shards);
        }
    }

    void OnDisable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnShardsChanged -= UpdateLabel;
    }

    // UXML/USS 없이 코드로 직접 "Shards: n" 라벨 + 반투명 배경을 구성
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

    private void UpdateLabel(int shards)
    {
        _label.text = $"Shards: {shards}";
    }
}
