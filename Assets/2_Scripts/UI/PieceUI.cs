using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// 화면 우상단에 오브젝트별 조각(화폐) 보유량을 표시. 통합 화폐가 없어서 "0개 이상 보유한 조각"만 동적으로 나열됨
[RequireComponent(typeof(UIDocument))]
public class PieceUI : MonoBehaviour
{
    [SerializeField] private float rightOffset = 20f; // 화면 우상단 기준 오른쪽 여백
    [SerializeField] private float topOffset = 20f; // 화면 우상단 기준 위쪽 여백

    private VisualElement _root; // 이 UIDocument의 최상위 요소 - 다른 팝업이 우상단을 가릴 때 숨기기 위해 저장해둠
    private VisualElement _piecesContainer; // 오브젝트별 조각 라벨들을 세로로 쌓는 컨테이너
    private readonly Dictionary<int, Label> _pieceLabels = new Dictionary<int, Label>(); // objectIndex -> 그 오브젝트의 조각 라벨 (처음 보유하는 순간 생성됨)
    private readonly Dictionary<int, long> _lastAmounts = new Dictionary<int, long>(); // objectIndex -> 직전에 표시했던 조각 개수 (늘었는지 판단용)
    private readonly Dictionary<int, Coroutine> _pulseCoroutines = new Dictionary<int, Coroutine>(); // objectIndex -> 지금 재생 중인 펄스 연출 (연속으로 늘어날 때 중첩 재생 방지용)

    // Weapon/Object 팝업이 이제 우상단까지 안 가려서(패널 오른쪽에 여백을 둠) 더 이상 숨길 필요가 없어짐 -
    // 팝업이 열려있는 동안에도 조각 개수가 계속 보여야(눌렀을 때도 보이게) 하기 때문.
    // 업그레이드 화면(Canvas, UpgradeTreeUI)은 화면 전체를 덮고 우상단에 닫기(X) 버튼도 있어서, 그거 열려있는 동안은 계속 숨김
    private bool _upgradeOverlayOpen;
    private bool _selectorPanelOpen;

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

        _root = uiDocument.rootVisualElement;
        BuildContainer(_root);

        // 업그레이드 화면(Canvas)이 열리면 우상단 X 버튼과 겹치니까 숨김 - Canvas 쪽 UpgradeTreeUI가 이벤트를 쏨
        UpgradeTreeUI.OnTreeToggled += HandleUpgradeOverlayToggled;
        SidePanelUI.OnSelectorPanelToggled += HandleSelectorPanelToggled;
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

        UpgradeTreeUI.OnTreeToggled -= HandleUpgradeOverlayToggled;
        SidePanelUI.OnSelectorPanelToggled -= HandleSelectorPanelToggled;
    }

    private void HandleUpgradeOverlayToggled(bool open)
    {
        _upgradeOverlayOpen = open;
        RefreshVisibility();
    }

    private void HandleSelectorPanelToggled(bool open)
    {
        _selectorPanelOpen = open;
        RefreshVisibility();
    }

    // 이제 Weapon/Object 팝업이 열려있어도 계속 보임 (패널이 우상단을 안 가려서 겹칠 일이 없음) -
    // 업그레이드 오버레이가 열렸을 때만 숨기는데, 지금은 그 오버레이 자체가 없어서(Canvas로 새로 만들 예정)
    // 사실상 항상 보이는 상태가 됨
    private void RefreshVisibility()
    {
        if (_root != null)
            _root.style.display = _upgradeOverlayOpen ? DisplayStyle.None : DisplayStyle.Flex;
    }

    // UXML/USS 없이 코드로 직접 오브젝트별 조각 라벨 + 반투명 배경을 구성 (화면 우상단)
    private void BuildContainer(VisualElement root)
    {
        root.Clear();

        var background = new VisualElement();
        background.style.position = Position.Absolute;
        background.style.top = topOffset;
        background.style.right = rightOffset;
        background.style.paddingLeft = 10;
        background.style.paddingRight = 10;
        background.style.paddingTop = 4;
        background.style.paddingBottom = 4;
        background.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        background.style.borderTopLeftRadius = 4;
        background.style.borderTopRightRadius = 4;
        background.style.borderBottomLeftRadius = 4;
        background.style.borderBottomRightRadius = 4;
        background.style.alignItems = Align.FlexEnd; // 라벨들을 오른쪽 정렬로 쌓음

        _piecesContainer = background; // 배경 자체가 곧 라벨 컨테이너 (별도 자식 없이 바로 라벨을 쌓음)
        root.Add(background);
    }

    // 오브젝트 하나의 조각을 처음 보유하는 순간 라벨을 새로 만들고, 그 뒤로는 있는 라벨의 숫자만 갱신
    private void UpdatePieceLabel(int objectIndex, long amount)
    {
        if (!_pieceLabels.TryGetValue(objectIndex, out Label label))
        {
            label = new Label();
            label.style.fontSize = 18;
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
