using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// 화면 좌상단에 통합 조각(화폐) 보유량을 표시.
// 업그레이드/무기/오브젝트 화면이 열려있는 동안에도 계속 보이도록, PanelSettings의 sortingOrder를 높게 잡아
// 그 화면들(다른 UIDocument/Canvas)보다 항상 위에 그려지게 함
[RequireComponent(typeof(UIDocument))]
public class PieceUI : MonoBehaviour
{
    [SerializeField] private float leftOffset = 20f; // 화면 좌상단 기준 왼쪽 여백
    [SerializeField] private float topOffset = 20f; // 화면 좌상단 기준 위쪽 여백
    [SerializeField] private int sortingOrder = 100; // 업그레이드/무기/오브젝트 화면 위에 그려지도록 높게 잡은 값

    private Label _pieceLabel; // 조각 개수를 표시하는 라벨
    private long _lastAmount; // 직전에 표시했던 조각 개수 (늘었는지 판단용)
    private Coroutine _pulseCoroutine; // 지금 재생 중인 펄스 연출 (연속으로 늘어날 때 중첩 재생 방지용)
    private Label _crystalLabel; // 결정 개수를 표시하는 라벨
    private long _lastCrystals; // 직전에 표시했던 결정 개수 (늘었는지 판단용)
    private Coroutine _crystalPulseCoroutine; // 결정 라벨의 펄스 연출

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();

        if (uiDocument.panelSettings == null)
        {
            // PanelSettings를 비워둬도 동작하도록 런타임에 자동 생성 + 기본 런타임 테마 할당
            // (안 넣으면 UI Toolkit이 제대로 렌더링하지 않음)
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            uiDocument.panelSettings = settings;
        }

        uiDocument.panelSettings.sortingOrder = sortingOrder;

        VisualElement root = uiDocument.rootVisualElement;
        GameFonts.Apply(root);
        BuildContainer(root);
    }

    void Start()
    {
        // CurrencyManager.Awake()가 먼저 실행되도록 OnEnable이 아닌 Start에서 구독
        // (Unity는 모든 오브젝트의 Awake를 먼저 실행한 뒤 Start를 실행하므로 순서가 보장됨)
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnPiecesChanged += UpdatePieceLabel;
            CurrencyManager.Instance.OnCrystalsChanged += UpdateCrystalLabel;

            // 저장 파일을 이미 불러왔을 수 있으니, 지금 보유량을 한 번 읽어서 라벨을 미리 채움
            UpdatePieceLabel(CurrencyManager.Instance.GetPieces());
            UpdateCrystalLabel(CurrencyManager.Instance.GetCrystals());
        }
    }

    void OnDisable()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnPiecesChanged -= UpdatePieceLabel;
            CurrencyManager.Instance.OnCrystalsChanged -= UpdateCrystalLabel;
        }
    }

    // UXML/USS 없이 코드로 직접 조각 라벨 + 반투명 배경을 구성 (화면 좌상단)
    private void BuildContainer(VisualElement root)
    {
        root.Clear();

        var background = new VisualElement();
        background.style.position = Position.Absolute;
        background.style.top = topOffset;
        background.style.left = leftOffset;
        background.style.paddingLeft = 12;
        background.style.paddingRight = 12;
        background.style.paddingTop = 6;
        background.style.paddingBottom = 6;
        background.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        background.style.borderTopLeftRadius = 4;
        background.style.borderTopRightRadius = 4;
        background.style.borderBottomLeftRadius = 4;
        background.style.borderBottomRightRadius = 4;

        _pieceLabel = CreateLabel(Color.white);
        background.Add(_pieceLabel);

        _crystalLabel = CreateLabel(new Color(0.55f, 0.9f, 1f)); // 결정은 하늘색으로 구분
        background.Add(_crystalLabel);

        root.Add(background);
    }

    private static Label CreateLabel(Color color)
    {
        var label = new Label();
        label.style.fontSize = 20;
        label.style.color = color;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        return label;
    }

    private void UpdateCrystalLabel(long amount)
    {
        _crystalLabel.text = $"결정: {NumberFormatUtil.Format(amount)}";

        bool increased = amount > _lastCrystals;
        _lastCrystals = amount;

        if (increased)
        {
            if (_crystalPulseCoroutine != null)
                StopCoroutine(_crystalPulseCoroutine);

            _crystalPulseCoroutine = StartCoroutine(PlayPieceGainPulse(_crystalLabel));
        }
    }

    // 조각 보유량이 바뀔 때마다 라벨 갱신 + 늘어났을 때 펄스 연출 재생
    private void UpdatePieceLabel(long amount)
    {
        _pieceLabel.text = $"조각: {NumberFormatUtil.Format(amount)}";

        bool increased = amount > _lastAmount;
        _lastAmount = amount;

        if (increased)
        {
            // 이미 재생 중인 펄스가 있으면(연속으로 빠르게 늘어날 때) 멈추고 처음부터 다시 재생해서 크기가 안 꼬이게 함
            if (_pulseCoroutine != null)
                StopCoroutine(_pulseCoroutine);

            _pulseCoroutine = StartCoroutine(PlayPieceGainPulse(_pieceLabel));
        }
    }

    // 조각을 얻은 라벨이 잠깐 커졌다가(ease-out) 다시 원래 크기로 줄어드는(ease-in) 연출
    private IEnumerator PlayPieceGainPulse(Label label)
    {
        const float duration = 0.25f; // 연출 총 시간(초)
        const float peakScale = 1.3f; // 커질 때 최대 배율

        float elapsed = 0f; // 연출 시작 후 흐른 시간

        while (elapsed < duration)
        {
            // 업그레이드 페이지가 열려있는 동안 Time.timeScale이 0이라 deltaTime 대신 unscaledDeltaTime을 써야 연출이 재생됨
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration); // 0~1 진행률

            // 앞 절반은 1배 -> peakScale로 커지고, 뒤 절반은 peakScale -> 1배로 다시 줄어듦
            float scale = progress < 0.5f
                ? Mathf.Lerp(1f, peakScale, progress / 0.5f)
                : Mathf.Lerp(peakScale, 1f, (progress - 0.5f) / 0.5f);

            label.style.scale = new Scale(new Vector3(scale, scale, 1f));
            yield return null;
        }

        label.style.scale = new Scale(Vector3.one);
    }
}
