using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 줌 영향을 안 받게 Content 밖, UpgradeTreePanel 바로 아래에 둠
[RequireComponent(typeof(RectTransform))]
public class UpgradeTooltip : MonoBehaviour
{
    public static UpgradeTooltip Instance { get; private set; }

    [SerializeField] private float maxWidth = 560f;
    [SerializeField] private float gapAboveNode = 18f; // px
    [SerializeField] private Vector2 padding = new Vector2(22f, 16f);
    [SerializeField] private float fontSize = 32f;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color mutedTextColor = new Color(0.62f, 0.62f, 0.66f, 1f);
    [SerializeField] private Color borderColor = Color.white;
    [SerializeField] private float borderThickness = 3f; // px
    [SerializeField] private Color dividerColor = new Color(1f, 1f, 1f, 0.5f);

    public struct Content
    {
        public string title;
        public string description;
        public string level;
        public string price;
    }

    private RectTransform _rect;
    private RectTransform _content;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _descriptionText;
    private RectTransform _dividerAfterTitle;
    private RectTransform _dividerAfterDescription;
    private RectTransform _dividerAfterLevel;
    private TextMeshProUGUI _levelText;
    private TextMeshProUGUI _priceText;
    private Coroutine _shakeRoutine;

    void Awake()
    {
        Instance = this;
        _rect = (RectTransform)transform;
        Build();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build()
    {
        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0f);
        _rect.sizeDelta = new Vector2(maxWidth, 0f);

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var bg = (RectTransform)bgGo.transform;
        bg.SetParent(_rect, false);
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.sizeDelta = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = backgroundColor;
        bgImg.raycastTarget = false;

        CreateBorderEdge("BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, borderThickness));
        CreateBorderEdge("BorderBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, borderThickness));
        CreateBorderEdge("BorderLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(borderThickness, 0f));
        CreateBorderEdge("BorderRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(borderThickness, 0f));

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(RectMask2D));
        _content = (RectTransform)contentGo.transform;
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.SetParent(_rect, false);
        _content.anchoredPosition = Vector2.zero;
        _content.sizeDelta = Vector2.zero;

        _titleText = CreateText("Title", fontSize * 1.05f, FontStyles.Bold, Color.white);
        _dividerAfterTitle = CreateDivider("DividerAfterTitle");
        _descriptionText = CreateText("Description", fontSize * 0.78f, FontStyles.Normal, mutedTextColor);
        _dividerAfterDescription = CreateDivider("DividerAfterDescription");
        _levelText = CreateText("Level", fontSize * 0.82f, FontStyles.Normal, Color.white);
        _dividerAfterLevel = CreateDivider("DividerAfterLevel");
        _priceText = CreateText("Price", fontSize * 0.82f, FontStyles.Normal, Color.white);
    }

    private RectTransform CreateDivider(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.SetParent(_content, false);
        rt.offsetMin = new Vector2(padding.x, rt.offsetMin.y);
        rt.offsetMax = new Vector2(-padding.x, rt.offsetMax.y);
        var img = go.GetComponent<Image>();
        img.color = dividerColor;
        img.raycastTarget = false;
        return rt;
    }

    private void CreateBorderEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_rect, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = borderColor;
        img.raycastTarget = false;
    }

    private TextMeshProUGUI CreateText(string name, float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.SetParent(_content, false);
        rt.offsetMin = new Vector2(padding.x, rt.offsetMin.y);
        rt.offsetMax = new Vector2(-padding.x, rt.offsetMax.y);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.enableAutoSizing = false;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Top;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        if (GameFonts.Tmp != null) tmp.font = GameFonts.Tmp;
        return tmp;
    }

    public void Show(Content content, RectTransform anchor)
    {
        gameObject.SetActive(true);
        _rect.localRotation = Quaternion.identity; // 직전 흔들림이 멈춰 기울어진 채 남았을 수 있음
        ApplyContent(content);

        float nodeHalfHeight = anchor.rect.height * 0.5f * anchor.lossyScale.y;
        _rect.position = anchor.position + Vector3.up * (nodeHalfHeight + gapAboveNode);

        ClampInsideParent();
        _rect.SetAsLastSibling();
    }

    public void Hide()
    {
        if (this != null) gameObject.SetActive(false);
    }

    public void UpdateContent(Content content)
    {
        if (!gameObject.activeSelf) return;
        ApplyContent(content);
    }

    private void ApplyContent(Content content)
    {
        _titleText.text = content.title;
        SetRow(_descriptionText.gameObject, _descriptionText, content.description);
        SetRow(_levelText.gameObject, _levelText, content.level);
        SetRow(_priceText.gameObject, _priceText, content.price);

        bool hasDescription = !string.IsNullOrEmpty(content.description);
        bool hasLevel = !string.IsNullOrEmpty(content.level);
        bool hasPrice = !string.IsNullOrEmpty(content.price);

        _dividerAfterTitle.gameObject.SetActive(hasDescription || hasLevel || hasPrice);
        _dividerAfterDescription.gameObject.SetActive(hasDescription && (hasLevel || hasPrice));
        _dividerAfterLevel.gameObject.SetActive(hasLevel && hasPrice);

        float spacing = fontSize * 0.2f;
        float cursorY = padding.y;

        cursorY = LayoutRow(_titleText, cursorY, spacing);
        cursorY = LayoutDivider(_dividerAfterTitle, cursorY, spacing);
        if (hasDescription) cursorY = LayoutRow(_descriptionText, cursorY, spacing);
        cursorY = LayoutDivider(_dividerAfterDescription, cursorY, spacing);
        if (hasLevel) cursorY = LayoutRow(_levelText, cursorY, spacing);
        cursorY = LayoutDivider(_dividerAfterLevel, cursorY, spacing);
        if (hasPrice) cursorY = LayoutRow(_priceText, cursorY, spacing);

        float totalHeight = cursorY + padding.y;
        _rect.sizeDelta = new Vector2(maxWidth, totalHeight);
        _content.sizeDelta = new Vector2(_content.sizeDelta.x, totalHeight); // RectMask2D가 이 rect 기준으로 자름
    }

    // ForceMeshUpdate로 지금 폭 기준 줄바꿈 높이를 바로 계산
    private float LayoutRow(TextMeshProUGUI tmp, float cursorY, float spacing)
    {
        tmp.ForceMeshUpdate();
        float height = tmp.preferredHeight;
        RectTransform rt = tmp.rectTransform;
        rt.offsetMax = new Vector2(rt.offsetMax.x, -cursorY);
        rt.offsetMin = new Vector2(rt.offsetMin.x, -(cursorY + height));
        return cursorY + height + spacing;
    }

    private float LayoutDivider(RectTransform divider, float cursorY, float spacing)
    {
        if (!divider.gameObject.activeSelf) return cursorY;

        const float thickness = 1f;
        divider.offsetMax = new Vector2(divider.offsetMax.x, -cursorY);
        divider.offsetMin = new Vector2(divider.offsetMin.x, -(cursorY + thickness));
        return cursorY + thickness + spacing;
    }

    private static void SetRow(GameObject row, TextMeshProUGUI tmp, string text)
    {
        bool has = !string.IsNullOrEmpty(text);
        row.SetActive(has);
        if (has) tmp.text = text;
    }

    public void PlayShake()
    {
        if (!gameObject.activeSelf) return;
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _rect.localRotation = Quaternion.identity;
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        const float duration = 0.22f; // 초
        const float amplitudeDegrees = 7f; // 도
        const float oscillations = 1.5f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // timeScale 0이라 unscaled
            float p = Mathf.Clamp01(elapsed / duration);
            float angle = amplitudeDegrees * Mathf.Sin(p * oscillations * Mathf.PI * 2f) * (1f - p);
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        _rect.localRotation = Quaternion.identity;
        _shakeRoutine = null;
    }

    private void ClampInsideParent()
    {
        if (_rect.parent is not RectTransform parent) return;

        Vector3[] parentCorners = new Vector3[4];
        parent.GetWorldCorners(parentCorners);
        Vector3[] myCorners = new Vector3[4];
        _rect.GetWorldCorners(myCorners);

        Vector3 shift = Vector3.zero;
        if (myCorners[0].x < parentCorners[0].x) shift.x += parentCorners[0].x - myCorners[0].x;
        if (myCorners[2].x > parentCorners[2].x) shift.x -= myCorners[2].x - parentCorners[2].x;
        if (myCorners[0].y < parentCorners[0].y) shift.y += parentCorners[0].y - myCorners[0].y;
        if (myCorners[2].y > parentCorners[2].y) shift.y -= myCorners[2].y - parentCorners[2].y;

        _rect.position += shift;
    }
}
