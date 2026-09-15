using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// 화면 좌상단에 오브젝트별 조각(화폐) 보유량을 표시. 통합 화폐가 없어서 "0개 이상 보유한 조각"만 동적으로 나열됨.
// 업그레이드/무기/오브젝트 화면이 열려있는 동안에도 계속 보이도록, PanelSettings의 sortingOrder를 높게 잡아
// 그 화면들(다른 UIDocument/Canvas)보다 항상 위에 그려지게 함
[RequireComponent(typeof(UIDocument))]
public class PieceUI : MonoBehaviour
{
    [SerializeField] private float leftOffset = 20f; // 화면 좌상단 기준 왼쪽 여백
    [SerializeField] private float topOffset = 20f; // 화면 좌상단 기준 위쪽 여백
    [SerializeField] private int sortingOrder = 100; // 업그레이드/무기/오브젝트 화면 위에 그려지도록 높게 잡은 값

    private VisualElement _piecesContainer; // 오브젝트별 조각 라벨들을 세로로 쌓는 컨테이너
    private readonly Dictionary<int, Label> _pieceLabels = new Dictionary<int, Label>(); // objectIndex -> 그 오브젝트의 조각 라벨 (처음 보유하는 순간 생성됨)
    private readonly Dictionary<int, long> _lastAmounts = new Dictionary<int, long>(); // objectIndex -> 직전에 표시했던 조각 개수 (늘었는지 판단용)
    private readonly Dictionary<int, Coroutine> _pulseCoroutines = new Dictionary<int, Coroutine>(); // objectIndex -> 지금 재생 중인 펄스 연출 (연속으로 늘어날 때 중첩 재생 방지용)

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

            // 저장 파일을 이미 불러왔을 수 있으니, 지금 보유 중인 조각들을 한 번 훑어서 라벨을 미리 채움
            if (ObjectManager.Instance != null)
            {
                for (int i = 0; i < ObjectManager.Instance.ObjectCount; i++)
                {
                    long amount = CurrencyManager.Instance.GetPieces(i);
                    if (amount > 0)
                        UpdatePieceLabel(i, amount);
                }
            }
        }
    }

    void OnDisable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnPiecesChanged -= UpdatePieceLabel;
    }

    // UXML/USS 없이 코드로 직접 오브젝트별 조각 라벨 + 반투명 배경을 구성 (화면 좌상단)
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
        background.style.alignItems = Align.FlexStart; // 라벨들을 왼쪽 정렬로 쌓음

        _piecesContainer = background; // 배경 자체가 곧 라벨 컨테이너 (별도 자식 없이 바로 라벨을 쌓음)
        root.Add(background);
    }

    // 오브젝트 하나의 조각을 처음 보유하는 순간 라벨을 새로 만들고, 그 뒤로는 있는 라벨의 숫자만 갱신.
    // 0 이하로 돌아가면(초기화 등) 라벨 자체를 지움 - 다음에 다시 얻으면 "새로 생기는" 연출부터 재생됨
    private void UpdatePieceLabel(int objectIndex, long amount)
    {
        if (amount <= 0)
        {
            if (_pieceLabels.TryGetValue(objectIndex, out Label oldLabel))
            {
                _piecesContainer.Remove(oldLabel);
                _pieceLabels.Remove(objectIndex);
            }
            _lastAmounts[objectIndex] = 0;
            return;
        }

        if (!_pieceLabels.TryGetValue(objectIndex, out Label label))
        {
            label = new Label();
            label.style.fontSize = 20; // 보유 오브젝트 종류가 많아지면 세로로 길게 쌓여서 너무 크면 화면을 많이 차지함
            label.style.color = Color.white;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _piecesContainer.Add(label);
            _pieceLabels[objectIndex] = label;
        }

        string objectName = ObjectManager.Instance != null ? ObjectManager.Instance.GetObjectAt(objectIndex).objectName : $"오브젝트 {objectIndex}";
        label.text = $"{objectName} 조각: {NumberFormatUtil.Format(amount)}";

        // 조각이 늘어난 순간에만(줄어들 때/처음 생성될 땐 X) 숫자가 잠깐 커졌다 줄어드는 펄스 연출 재생
        bool increased = _lastAmounts.TryGetValue(objectIndex, out long previousAmount) && amount > previousAmount;
        _lastAmounts[objectIndex] = amount;

        if (increased)
        {
            // 이미 재생 중인 펄스가 있으면(연속으로 빠르게 늘어날 때) 멈추고 처음부터 다시 재생해서 크기가 안 꼬이게 함
            if (_pulseCoroutines.TryGetValue(objectIndex, out Coroutine running) && running != null)
                StopCoroutine(running);

            _pulseCoroutines[objectIndex] = StartCoroutine(PlayPieceGainPulse(label));
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
