using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class UpgradeTreeUI : MonoBehaviour, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static UpgradeTreeUI Instance { get; private set; } // 업그레이드 트리 (환생 창은 아님)
    public static UpgradeTreeUI Current { get; private set; } // 지금 열려 있는 트리 창 (업그레이드 / 환생)

    [Tooltip("환생 창이면 체크 (업그레이드 트리 Instance로 안 잡힘)")]
    [SerializeField] private bool isRebirthTree; // 환생 창
    public bool IsRebirthTree => isRebirthTree; // 재화 표시 바꿀 때 씀

    public static event System.Action<bool> OnTreeToggled;

    [SerializeField] private RectTransform content;
    public RectTransform Content => content; // 노드가 들어있는 판 (환생 연출용)

    [Header("줌")]
    [SerializeField] private float minZoom = 0.15f;
    [SerializeField] private float maxZoom = 1.5f;
    [SerializeField] private float zoomStep = 0.1f; // 곱셈 비율 (0.1 = 틱당 ±10%)
    [SerializeField] private float zoomSmoothSpeed = 8f;

    private UpgradeNodeUI[] _upgradeNodes = System.Array.Empty<UpgradeNodeUI>();
    private ObjectEconomyNodeUI[] _economyNodes = System.Array.Empty<ObjectEconomyNodeUI>();

    private float _zoomTarget = 1f;
    private Vector2 _zoomFocalPoint;

    // 카메라 초기화 때 정중앙에 올 노드
    [SerializeField] private string centerNodeId = "0_달걀_드랍량_강화";
    // centerNodeId를 못 찾을 때만
    [SerializeField] private Vector2 defaultAnchoredPosition = new Vector2(1500f, -2200f);
    [SerializeField] private float defaultZoom = 0.5f;

    void Awake()
    {
        if (!isRebirthTree)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        CacheNodes();
        StyleButton("CloseButton", PixelStyle.Pink, "X", new Vector2(84f, 84f));
        StyleButton("CameraResetButton", PixelStyle.Mint, "카메라 리셋", new Vector2(300f, 90f));
    }

    private void StyleButton(string buttonName, PixelStyle.Tone tone, string label, Vector2 size)
    {
        foreach (UnityEngine.UI.Button button in GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            if (button.name != buttonName) continue;
            ((RectTransform)button.transform).sizeDelta = size;
            PixelStyle.UguiButton(button, tone);
            var text = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (text != null)
            {
                Loc.Bind(text, () => text.text = Loc.T(label));
                text.enableAutoSizing = false;
                text.fontSize = 32;
                text.alignment = TMPro.TextAlignmentOptions.Center;
            }
            return;
        }
    }

    private void CacheNodes()
    {
        Transform root = content != null ? content : transform;
        _upgradeNodes = root.GetComponentsInChildren<UpgradeNodeUI>(true);
        _economyNodes = root.GetComponentsInChildren<ObjectEconomyNodeUI>(true);
    }

    private bool _hasOpenedBefore;

    void OnEnable()
    {
        if (!_hasOpenedBefore)
        {
            ResetCamera();
            _hasOpenedBefore = true;
        }

        RefreshAll();
        Loc.Changed += OnLanguageChanged;

        UIPointerGuard.SetPopup(name, true); // 게임은 계속 돌고 월드 클릭만 막음
        if (isRebirthTree) Time.timeScale = 0f; // 환생 창에선 자동클릭 등 게임 진행 멈춤
        Current = this;
        UpgradeTooltip.Use(GetComponentInChildren<UpgradeTooltip>(true));

        OnTreeToggled?.Invoke(true);
    }

    private void OnLanguageChanged() => RefreshAll();

    void OnDisable()
    {
        Loc.Changed -= OnLanguageChanged;
        // 이동 중 닫히면 코루틴이 멈춰서 바로 끝냄
        System.Action pending = _pendingReveals;
        _pendingReveals = null;
        pending?.Invoke();
        _revealRunning = false;

        if (_leaveConfirm != null) _leaveConfirm.SetActive(false); // 다음에 열 때 팝업 없이
        UIPointerGuard.SetPopup(name, false);
        if (Current == this) Current = null;
        if (isRebirthTree) Time.timeScale = 1f;

        UIPointerGuard.IsPointerOverUI = false;

        OnTreeToggled?.Invoke(false);
    }

    private GameObject _leaveConfirm; // 환생 창 나가기 확인 팝업

    // X 버튼 / Esc. 환생 창은 나가면 다음 환생까지 못 돌아오니 한 번 물어봄
    public void Close()
    {
        if (!isRebirthTree || !gameObject.activeInHierarchy) { CloseNow(); return; }
        if (_leaveConfirm == null) _leaveConfirm = BuildLeaveConfirm();
        _leaveConfirm.SetActive(!_leaveConfirm.activeSelf); // 팝업이 떠 있을 때 Esc면 취소
        _leaveConfirm.transform.SetAsLastSibling();
    }

    public void CloseNow() => gameObject.SetActive(false);

    private GameObject BuildLeaveConfirm()
    {
        RectTransform dim = NewRect("LeaveConfirm", transform); // 화면 전체를 어둡게 덮어 뒤쪽 클릭을 막음
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        dim.sizeDelta = Vector2.zero;
        dim.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.6f);

        RectTransform window = NewRect("Window", dim); // 가운데 창
        window.sizeDelta = new Vector2(1100f, 460f);
        PixelStyle.UguiFrame(window.gameObject.AddComponent<UnityEngine.UI.Image>(), PixelStyle.Window);

        NewLabel(window, "환생 창을 나갈까요?", 44f, PixelStyle.Text, new Vector2(0f, 120f));
        NewLabel(window, "나가면 다음 환생을 할 때까지 다시 돌아올 수 없어요.", 30f, PixelStyle.MutedText, new Vector2(0f, 35f));
        NewButton(window, "나가기", PixelStyle.Pink, new Vector2(-190f, -110f), CloseNow);
        NewButton(window, "돌아가기", PixelStyle.Sky, new Vector2(190f, -110f), () => _leaveConfirm.SetActive(false));

        dim.gameObject.SetActive(false);
        return dim.gameObject;
    }

    private static RectTransform NewRect(string objectName, Transform parent)
    {
        var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>(); // 새 UI 오브젝트
        rect.SetParent(parent, false);
        return rect;
    }

    private static TMPro.TextMeshProUGUI NewLabel(RectTransform parent, string korean, float size, Color color, Vector2 pos)
    {
        RectTransform rect = NewRect("Label", parent); // 글자 자리
        rect.sizeDelta = new Vector2(parent.sizeDelta.x - 80f, size * 1.6f);
        rect.anchoredPosition = pos;
        var text = rect.gameObject.AddComponent<TMPro.TextMeshProUGUI>(); // 글자
        if (GameFonts.Tmp != null) text.font = GameFonts.Tmp;
        text.fontSize = size;
        text.color = color;
        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.raycastTarget = false;
        Loc.Bind(text, () => text.text = Loc.T(korean));
        return text;
    }

    private static void NewButton(RectTransform parent, string korean, PixelStyle.Tone tone, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = NewRect("Button", parent); // 버튼 자리
        rect.sizeDelta = new Vector2(340f, 100f);
        rect.anchoredPosition = pos;
        rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); // 버튼
        button.onClick.AddListener(onClick);

        TMPro.TextMeshProUGUI label = NewLabel(rect, korean, 36f, Color.white, Vector2.zero); // 버튼 글자
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.sizeDelta = Vector2.zero;
        PixelStyle.UguiButton(button, tone);
    }

    public void RefreshAll(bool animateReveals = false)
    {
        foreach (UpgradeNodeUI node in _upgradeNodes)
            if (node != null) node.Refresh(animateReveals);

        foreach (ObjectEconomyNodeUI node in _economyNodes)
            if (node != null) node.Refresh(animateReveals);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (content == null) return;

        RectTransform viewport = content.parent as RectTransform;
        if (viewport != null)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, eventData.pressEventCamera, out _zoomFocalPoint);

        // OS마다 틱 크기가 달라 부호만 씀
        float dir = Mathf.Sign(eventData.scrollDelta.y);
        float factor = dir > 0f ? (1f + zoomStep) : (1f / (1f + zoomStep));
        _zoomTarget = Mathf.Clamp(_zoomTarget * factor, Mathf.Min(minZoom, FitAllZoom()), maxZoom);
    }

    void Update()
    {
        if (content == null) return;

        float currentScale = content.localScale.x;
        if (Mathf.Approximately(currentScale, _zoomTarget)) return;

        // timeScale과 무관하게 unscaled
        // 프레임레이트와 무관한 보간 계수
        float t = 1f - Mathf.Exp(-zoomSmoothSpeed * Time.unscaledDeltaTime);
        float newScale = Mathf.Lerp(currentScale, _zoomTarget, t);

        // anchoredPosition은 원점이 달라 localPosition으로 보정
        float ratio = newScale / currentScale;
        Vector2 localPos = content.localPosition;
        Vector2 newLocalPos = _zoomFocalPoint - (_zoomFocalPoint - localPos) * ratio;
        content.localPosition = new Vector3(newLocalPos.x, newLocalPos.y, content.localPosition.z);

        content.localScale = new Vector3(newScale, newScale, 1f);
    }

    public void PlayGateReveal(RectTransform node, System.Action onArrive)
    {
        if (content == null || !isActiveAndEnabled) { onArrive?.Invoke(); return; }
        _pendingReveals += onArrive;
        StartCoroutine(GateRevealRoutine(node, onArrive));
    }

    private System.Action _pendingReveals;
    private bool _revealRunning;

    private System.Collections.IEnumerator GateRevealRoutine(RectTransform node, System.Action onArrive)
    {
        const float moveSeconds = 0.9f; // 초
        const float revealZoom = 1.1f;
        while (_revealRunning) yield return null;
        _revealRunning = true;
        yield return new WaitForSecondsRealtime(0.4f);
        Vector2 fromPos = content.anchoredPosition;
        float fromZoom = content.localScale.x;
        Vector2 toPos = CenteredAnchoredPosition(node, revealZoom);
        for (float t = 0f; t < moveSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / moveSeconds);
            float zoom = Mathf.Lerp(fromZoom, revealZoom, k);
            content.localScale = new Vector3(zoom, zoom, 1f);
            _zoomTarget = zoom;
            content.anchoredPosition = Vector2.Lerp(fromPos, toPos, k);
            yield return null;
        }
        content.localScale = new Vector3(revealZoom, revealZoom, 1f);
        _zoomTarget = revealZoom;
        content.anchoredPosition = toPos;
        _pendingReveals -= onArrive;
        onArrive?.Invoke();
        yield return new WaitForSecondsRealtime(0.6f);
        _revealRunning = false;
    }

    private float _fitAllZoom; // 노드 전체가 한 화면에 들어오는 배율 (한 번만 계산)

    private float FitAllZoom()
    {
        if (_fitAllZoom > 0f) return _fitAllZoom;
        RectTransform viewport = content.parent as RectTransform;
        if (viewport == null) return minZoom;
        var bounds = new Bounds();
        bool first = true;
        foreach (RectTransform rect in _upgradeNodes.Select(n => n != null ? n.Rect : null).Concat(_economyNodes.Select(n => n != null ? n.Rect : null)))
        {
            if (rect == null) continue;
            var b = new Bounds(content.InverseTransformPoint(rect.position), rect.rect.size);
            if (first) { bounds = b; first = false; }
            else bounds.Encapsulate(b);
        }
        if (first) return minZoom;
        _fitAllZoom = Mathf.Min(viewport.rect.width / bounds.size.x, viewport.rect.height / bounds.size.y) * 0.95f;
        return _fitAllZoom;
    }

    // 카메라 배율을 바로 바꿈 (부드러운 줌 보간이 되돌리지 않게 목표도 같이)
    public void SetZoom(float zoom)
    {
        if (content == null) return;
        content.localScale = new Vector3(zoom, zoom, 1f);
        _zoomTarget = zoom;
    }

    public Vector2 CenteredAnchoredPosition(RectTransform node, float zoom)
    {
        RectTransform viewport = content.parent as RectTransform;
        if (viewport == null) return content.anchoredPosition;
        Vector2 nodeLocalInContent = content.InverseTransformPoint(node.position);
        Vector2 viewportHalfSize = new Vector2(viewport.rect.width * 0.5f, -viewport.rect.height * 0.5f);
        return viewportHalfSize - nodeLocalInContent * zoom;
    }

    public void ResetCamera()
    {
        if (content == null) return;

        content.localScale = new Vector3(defaultZoom, defaultZoom, 1f);
        _zoomTarget = defaultZoom; // 목표도 맞춰야 Update가 되돌리지 않음
        content.anchoredPosition = ComputeCenteredAnchoredPosition();
    }

    // 노드 anchoredPosition은 부모 기준이라 실제 트랜스폼으로 잼
    private Vector2 ComputeCenteredAnchoredPosition()
    {
        UpgradeNodeUI centerNode = System.Array.Find(_upgradeNodes, n => n != null && n.Id == centerNodeId);
        if (centerNode == null) return defaultAnchoredPosition;

        RectTransform viewport = content.parent as RectTransform;
        if (viewport == null) return defaultAnchoredPosition;

        Vector2 nodeLocalInContent = content.InverseTransformPoint(centerNode.Rect.position);
        Vector2 viewportHalfSize = new Vector2(viewport.rect.width * 0.5f, -viewport.rect.height * 0.5f);
        return viewportHalfSize - nodeLocalInContent * defaultZoom;
    }

    public void OnPointerEnter(PointerEventData eventData) => UIPointerGuard.IsPointerOverUI = true;
    public void OnPointerExit(PointerEventData eventData) => UIPointerGuard.IsPointerOverUI = false;
}
