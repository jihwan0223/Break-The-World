using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 업그레이드 노드에 마우스를 올리면 그 노드 위에 뜨는 설명 박스. 노드와 달리 Content(줌/팬 대상) 밖,
// UpgradeTreePanel 바로 아래에 두어 확대/축소 영향을 안 받게 함. 배경 Image + TMP 텍스트를 코드로 만든다.
[RequireComponent(typeof(RectTransform))]
public class UpgradeTooltip : MonoBehaviour
{
    public static UpgradeTooltip Instance { get; private set; }

    [SerializeField] private float maxWidth = 560f;    // 툴팁 고정 폭 (텍스트는 이 안에서 줄바꿈)
    [SerializeField] private float gapAboveNode = 18f;  // 노드 위 여백(px)
    [SerializeField] private Vector2 padding = new Vector2(22f, 16f);
    [SerializeField] private float fontSize = 32f;      // 설명 글자 크기 (줌 아웃해도 잘 보이게 크게) - 제목/메타는 이 값에서 비율로 계산됨
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 1f); // 완전 검은색 배경
    [SerializeField] private Color mutedTextColor = new Color(0.62f, 0.62f, 0.66f, 1f); // 설명/라벨 색
    [SerializeField] private Color borderColor = Color.white;   // 테두리 선 색
    [SerializeField] private float borderThickness = 3f;        // 테두리 선 두께(px)
    [SerializeField] private Color dividerColor = new Color(1f, 1f, 1f, 0.5f); // 줄 사이 구분선 색

    // 제목 - 설명 - 레벨 - 가격 4줄 구조. description/level/price가 비어있으면 그 줄은 안 보임(예: 해금 완료 시 가격 줄 숨김 등)
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
    private RectTransform _dividerAfterTitle;       // 제목-설명 사이 구분선
    private RectTransform _dividerAfterDescription;  // 설명-레벨 사이 구분선
    private RectTransform _dividerAfterLevel;        // 레벨-가격 사이 구분선
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
        _rect.pivot = new Vector2(0.5f, 0f); // 아래쪽 기준 - 노드 위에 놓기 편하게
        _rect.sizeDelta = new Vector2(maxWidth, 0f); // 폭은 고정, 높이는 내용에 따라 매번 다시 계산됨

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var bg = (RectTransform)bgGo.transform;
        bg.SetParent(_rect, false);
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.sizeDelta = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = backgroundColor;
        bgImg.raycastTarget = false;

        // 테두리: 4개 얇은 흰색 막대를 상하좌우 가장자리에 스트레치 앵커로 붙임 (프레임 스프라이트 없이 순수 코드로)
        CreateBorderEdge("BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, borderThickness));
        CreateBorderEdge("BorderBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, borderThickness));
        CreateBorderEdge("BorderLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(borderThickness, 0f));
        CreateBorderEdge("BorderRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(borderThickness, 0f));
        // ↑ 각 호출의 마지막 Vector2는 sizeDelta: 두께 축만 값을 주고 나머지 축은 0(=부모 폭/높이에 꽉 붙는 스트레치)

        // Content: 제목/설명/구분선/레벨/가격을 세로로 쌓는 컨테이너. VerticalLayoutGroup+ContentSizeFitter 조합은
        // TMP가 폭 확정 전에 줄바꿈 높이를 미리 계산해버리는 타이밍 문제 때문에 텍스트가 박스 밖으로 삐져나오는
        // 버그가 계속 나서, 아예 안 쓰고 ApplyContent()에서 각 줄의 위치/높이를 직접 계산해서 박음(LayoutRow 참고).
        // RectMask2D는 그래도 혹시 모를 폭 초과에 대비한 최종 안전장치로 유지.
        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(RectMask2D));
        _content = (RectTransform)contentGo.transform;
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.SetParent(_rect, false);
        _content.anchoredPosition = Vector2.zero;
        _content.sizeDelta = Vector2.zero; // 높이는 ApplyContent가 매번 직접 재계산해서 넣어줌

        _titleText = CreateText("Title", fontSize * 1.05f, FontStyles.Bold, Color.white);
        _dividerAfterTitle = CreateDivider("DividerAfterTitle");
        _descriptionText = CreateText("Description", fontSize * 0.78f, FontStyles.Normal, mutedTextColor);
        _dividerAfterDescription = CreateDivider("DividerAfterDescription");
        _levelText = CreateText("Level", fontSize * 0.82f, FontStyles.Normal, Color.white);
        _dividerAfterLevel = CreateDivider("DividerAfterLevel");
        _priceText = CreateText("Price", fontSize * 0.82f, FontStyles.Normal, Color.white);
    }

    // Content 아래에 가로 한 줄짜리 구분선 생성 (제목/설명/레벨/가격 사이에 씀). 위치/높이는 ApplyContent가 직접 배치.
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

    // 툴팁 테두리용 얇은 막대 하나. anchorMin/Max로 가장자리에 스트레치시키고 sizeDelta의 두께 축만 값을 줌
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

    // Content 아래에 TMP 텍스트 한 줄 생성 (제목/설명/레벨/가격 공용). 가로는 Content 폭에 패딩만큼 인셋된
    // 스트레치 앵커로 고정해서, 레이아웃 시스템의 리빌드 타이밍과 무관하게 항상 정확한 폭으로 줄바꿈됨.
    // 세로 위치/높이는 매번 ApplyContent()의 LayoutRow가 실제 줄바꿈 결과(ForceMeshUpdate)를 보고 직접 넣어줌.
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
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        if (GameFonts.Tmp != null) tmp.font = GameFonts.Tmp;
        return tmp;
    }

    // anchor 노드 위에 툴팁을 띄움
    public void Show(Content content, RectTransform anchor)
    {
        gameObject.SetActive(true);
        _rect.localRotation = Quaternion.identity; // 직전 흔들림이 멈춰서 기울어진 채 남아있을 수 있음
        ApplyContent(content);

        // 노드 화면 위치 + 노드 렌더 높이 절반 + 여백 만큼 위로
        float nodeHalfHeight = anchor.rect.height * 0.5f * anchor.lossyScale.y;
        _rect.position = anchor.position + Vector3.up * (nodeHalfHeight + gapAboveNode);

        ClampInsideParent();
        _rect.SetAsLastSibling(); // 항상 맨 앞
    }

    public void Hide()
    {
        if (this != null) gameObject.SetActive(false);
    }

    // 이미 떠있는 툴팁의 내용만 갈아끼움 (위치/회전은 안 건드림) - 구매로 레벨이 바뀌었을 때 씀
    public void UpdateContent(Content content)
    {
        if (!gameObject.activeSelf) return;
        ApplyContent(content);
    }

    // 제목/설명/레벨/가격 텍스트를 채우고(빈 줄은 숨김) 각 줄을 위에서부터 차례로 직접 쌓아 박스 높이를 맞춤.
    // Unity 레이아웃 시스템(VerticalLayoutGroup/ContentSizeFitter)에 안 맡기고 여기서 직접 계산하는 이유는
    // LayoutRow 주석 참고 - 그쪽 조합은 리빌드 타이밍 문제로 텍스트가 박스 밖으로 삐져나오는 버그가 반복됐음.
    private void ApplyContent(Content content)
    {
        _titleText.text = content.title;
        SetRow(_descriptionText.gameObject, _descriptionText, content.description);
        SetRow(_levelText.gameObject, _levelText, content.level);
        SetRow(_priceText.gameObject, _priceText, content.price);

        bool hasDescription = !string.IsNullOrEmpty(content.description); // 제목은 항상 있으니 따로 안 봐도 됨
        bool hasLevel = !string.IsNullOrEmpty(content.level);
        bool hasPrice = !string.IsNullOrEmpty(content.price);

        // 각 구분선은 "바로 앞 줄이 보이고 + 뒤에 보일 줄이 하나라도 있을 때"만 켜서, 줄이 비어 숨겨져도 선만 덩그러니 남지 않게 함
        _dividerAfterTitle.gameObject.SetActive(hasDescription || hasLevel || hasPrice);
        _dividerAfterDescription.gameObject.SetActive(hasDescription && (hasLevel || hasPrice));
        _dividerAfterLevel.gameObject.SetActive(hasLevel && hasPrice);

        float spacing = fontSize * 0.2f; // 줄 사이 간격 (기존 VerticalLayoutGroup.spacing과 동일 값)
        float cursorY = padding.y; // 위쪽 패딩만큼 아래로 시작해서 한 줄씩 내려가며 쌓음

        cursorY = LayoutRow(_titleText, cursorY, spacing);
        cursorY = LayoutDivider(_dividerAfterTitle, cursorY, spacing);
        if (hasDescription) cursorY = LayoutRow(_descriptionText, cursorY, spacing);
        cursorY = LayoutDivider(_dividerAfterDescription, cursorY, spacing);
        if (hasLevel) cursorY = LayoutRow(_levelText, cursorY, spacing);
        cursorY = LayoutDivider(_dividerAfterLevel, cursorY, spacing);
        if (hasPrice) cursorY = LayoutRow(_priceText, cursorY, spacing);

        float totalHeight = cursorY + padding.y; // 마지막 줄 뒤 아래쪽 패딩 추가
        _rect.sizeDelta = new Vector2(maxWidth, totalHeight);
        // Content 자신의 높이도 같이 갱신해야 함 - RectMask2D가 이 rect 기준으로 자르기 때문에, 안 늘려주면
        // 매번 이전 높이(맨 처음엔 0)로 잘려서 내용이 안 보이거나 잘린 채로 남음
        _content.sizeDelta = new Vector2(_content.sizeDelta.x, totalHeight);
    }

    // 보이는 TMP 한 줄을 cursorY(박스 상단에서부터의 거리) 위치에 배치하고, 실제 줄바꿈 높이만큼 커서를 내림.
    // ForceMeshUpdate()로 지금 폭(CreateText에서 이미 패딩만큼 인셋해둔 폭) 기준 줄바꿈을 이 자리에서 강제로 계산해서
    // preferredHeight가 레이아웃 리빌드 타이밍에 좌우되지 않고 항상 지금 텍스트의 진짜 줄 수를 반영하게 함.
    private float LayoutRow(TextMeshProUGUI tmp, float cursorY, float spacing)
    {
        tmp.ForceMeshUpdate();
        float height = tmp.preferredHeight;
        RectTransform rt = tmp.rectTransform;
        rt.offsetMax = new Vector2(rt.offsetMax.x, -cursorY);
        rt.offsetMin = new Vector2(rt.offsetMin.x, -(cursorY + height));
        return cursorY + height + spacing;
    }

    // 구분선 한 줄(고정 두께 1px)을 cursorY에 배치. 안 보이면 커서 그대로 반환(자리 안 차지함)
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

    // 노드에서 구매/해금 성공 시 호출 - 떠있는 툴팁도 노드처럼 대각선으로 흔들림
    public void PlayShake()
    {
        if (!gameObject.activeSelf) return; // 안 떠 있으면 무시
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _rect.localRotation = Quaternion.identity;
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        const float duration = 0.22f;       // 흔들림 총 시간(초)
        const float amplitudeDegrees = 7f;  // 최대 각도
        const float oscillations = 1.5f;    // 좌우 왕복 횟수

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // 업그레이드 화면이 timeScale 0이라 unscaled
            float p = Mathf.Clamp01(elapsed / duration);
            float angle = amplitudeDegrees * Mathf.Sin(p * oscillations * Mathf.PI * 2f) * (1f - p);
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        _rect.localRotation = Quaternion.identity;
        _shakeRoutine = null;
    }

    // 부모(패널) 바깥으로 삐져나가면 안쪽으로 밀어넣음
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
