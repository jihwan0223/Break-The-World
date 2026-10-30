using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class UpgradeTreeUI : MonoBehaviour, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static UpgradeTreeUI Instance { get; private set; }

    public static event System.Action<bool> OnTreeToggled;

    [SerializeField] private RectTransform content;

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

    [SerializeField] private bool pauseGameWhileOpen = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
                text.text = label;
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

        if (pauseGameWhileOpen)
            Time.timeScale = 0f;

        OnTreeToggled?.Invoke(true);
    }

    void OnDisable()
    {
        // 이동 중 닫히면 코루틴이 멈춰서 바로 끝냄
        System.Action pending = _pendingReveals;
        _pendingReveals = null;
        pending?.Invoke();
        _revealRunning = false;

        if (pauseGameWhileOpen)
            Time.timeScale = 1f;

        UIPointerGuard.IsPointerOverUI = false;

        OnTreeToggled?.Invoke(false);
    }

    public void Close() => gameObject.SetActive(false);

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

        // timeScale 0이라 unscaled
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

    private Vector2 CenteredAnchoredPosition(RectTransform node, float zoom)
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
