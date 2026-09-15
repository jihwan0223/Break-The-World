using UnityEngine;
using UnityEngine.UIElements;

// 콤보(ComboManager)가 활성화된 동안 화면 하단 중앙에 "콤보 x배율 (남은시간)"을 표시. 비활성일 땐 안 보임
[RequireComponent(typeof(UIDocument))]
public class ComboUI : MonoBehaviour
{
    [SerializeField] private float bottomOffset = 60f; // 화면 하단 기준 여백

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
        GameFonts.Apply(root);

        _background = new VisualElement();
        _background.style.position = Position.Absolute;
        _background.style.bottom = bottomOffset;
        _background.style.left = Length.Percent(50);
        _background.style.translate = new Translate(Length.Percent(-50), 0); // 폭이 텍스트마다 달라서 marginLeft 대신 -50% 이동으로 정중앙 정렬
        _background.style.paddingLeft = 16;
        _background.style.paddingRight = 16;
        _background.style.paddingTop = 6;
        _background.style.paddingBottom = 6;
        _background.style.backgroundColor = new Color(0.85f, 0.55f, 0.1f, 0.9f); // 콤보 느낌의 주황
        _background.style.borderTopLeftRadius = 6;
        _background.style.borderTopRightRadius = 6;
        _background.style.borderBottomLeftRadius = 6;
        _background.style.borderBottomRightRadius = 6;
        _background.style.display = DisplayStyle.None; // 평소엔 숨김, 콤보 활성 중에만 표시

        _label = new Label();
        _label.style.fontSize = 22;
        _label.style.color = Color.white;
        _label.style.unityFontStyleAndWeight = FontStyle.Bold;

        _background.Add(_label);
        root.Add(_background);
    }

    void Update()
    {
        if (_background == null) return;

        if (ComboManager.Instance == null || !ComboManager.Instance.IsComboActive)
        {
            _background.style.display = DisplayStyle.None;
            return;
        }

        _background.style.display = DisplayStyle.Flex;
        _label.text = $"콤보! x{ComboManager.Instance.ShardMultiplier:0.#} ({ComboManager.Instance.ActiveRemaining:F1}s)";
    }
}
