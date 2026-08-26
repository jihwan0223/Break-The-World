using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class GoldUI : MonoBehaviour
{
    [SerializeField] private float leftOffset = 20f;
    [SerializeField] private float topOffset = 20f;

    // 지금은 화분(ObjectManager 인덱스 2)까지만 파괴 횟수를 표시함 - 인덱스 0=Plate, 1=Glass Cup, 2=Flower Pot
    private const int TrackedObjectCount = 3;

    private Label _label; // "Gold: N" 표시용 라벨
    private Label[] _destroyLabels; // 오브젝트별 "이름: N" 라벨 (0이면 숨김)

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
        // CurrencyManager/StatsManager/ObjectManager의 Awake()가 먼저 실행되도록 OnEnable이 아닌 Start에서 구독
        // (Unity는 모든 오브젝트의 Awake를 먼저 실행한 뒤 Start를 실행하므로 순서가 보장됨)
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnGoldChanged += UpdateLabel;
            UpdateLabel(CurrencyManager.Instance.Gold);
        }

        if (StatsManager.Instance != null)
        {
            StatsManager.Instance.OnObjectDestroyCountChanged += UpdateDestroyedLabel;

            for (int i = 0; i < TrackedObjectCount; i++)
                UpdateDestroyedLabel(i, StatsManager.Instance.GetDestroyCount(i));
        }
    }

    void OnDisable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGoldChanged -= UpdateLabel;

        if (StatsManager.Instance != null)
            StatsManager.Instance.OnObjectDestroyCountChanged -= UpdateDestroyedLabel;
    }

    // UXML/USS 없이 코드로 직접 "Gold: n" 라벨 + 오브젝트별 파괴 횟수 라벨 + 반투명 배경을 구성
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

        _destroyLabels = new Label[TrackedObjectCount];
        for (int i = 0; i < TrackedObjectCount; i++)
        {
            var destroyLabel = new Label(); // 오브젝트 하나의 파괴 횟수 라벨 (처음엔 0개라서 숨김)
            destroyLabel.style.fontSize = 20;
            destroyLabel.style.color = Color.white;
            destroyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            destroyLabel.style.display = DisplayStyle.None;
            background.Add(destroyLabel);
            _destroyLabels[i] = destroyLabel;
        }

        root.Add(background);
    }

    private void UpdateLabel(int gold)
    {
        _label.text = $"Gold: {gold}";
    }

    // objectIndex번째 오브젝트의 파괴 횟수 라벨을 갱신. 0개면 숨기고, 처음 1개가 되는 순간부터 보여줌
    private void UpdateDestroyedLabel(int objectIndex, int count)
    {
        if (objectIndex < 0 || objectIndex >= TrackedObjectCount || _destroyLabels == null)
            return;

        Label label = _destroyLabels[objectIndex];

        if (count <= 0)
        {
            label.style.display = DisplayStyle.None;
            return;
        }

        string objectName = ObjectManager.Instance != null ? ObjectManager.Instance.GetObjectAt(objectIndex).objectName : $"Object {objectIndex}";
        label.text = $"{objectName}: {count}";
        label.style.display = DisplayStyle.Flex;
    }
}
