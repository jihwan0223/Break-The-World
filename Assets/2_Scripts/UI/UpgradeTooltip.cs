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

    [SerializeField] private float maxWidth = 560f;    // 텍스트 줄바꿈 폭
    [SerializeField] private float gapAboveNode = 18f;  // 노드 위 여백(px)
    [SerializeField] private Vector2 padding = new Vector2(22f, 16f);
    [SerializeField] private float fontSize = 32f;      // 툴팁 글자 크기 (줌 아웃해도 잘 보이게 크게)
    [SerializeField] private Color backgroundColor = new Color(0.06f, 0.06f, 0.08f, 0.95f);

    private RectTransform _rect;
    private RectTransform _bg;
    private TextMeshProUGUI _text;
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

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        _bg = (RectTransform)bgGo.transform;
        _bg.SetParent(_rect, false);
        _bg.anchorMin = Vector2.zero;
        _bg.anchorMax = Vector2.one;
        _bg.sizeDelta = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = backgroundColor;
        bgImg.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        var tr = (RectTransform)textGo.transform;
        tr.SetParent(_rect, false);
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(padding.x, padding.y);
        tr.offsetMax = new Vector2(-padding.x, -padding.y);
        _text = textGo.GetComponent<TextMeshProUGUI>();
        _text.fontSize = fontSize;
        _text.enableAutoSizing = false;
        _text.color = Color.white;
        _text.alignment = TextAlignmentOptions.TopLeft;
        _text.textWrappingMode = TextWrappingModes.Normal;
        _text.raycastTarget = false;
        if (GameFonts.Tmp != null) _text.font = GameFonts.Tmp;
    }

    // anchor 노드 위에 툴팁을 띄움
    public void Show(string content, RectTransform anchor)
    {
        gameObject.SetActive(true);
        _rect.localRotation = Quaternion.identity; // 직전 흔들림이 멈춰서 기울어진 채 남아있을 수 있음
        _text.text = content;

        // 텍스트가 필요로 하는 크기로 박스 맞춤 (최대 폭 제한)
        Vector2 pref = _text.GetPreferredValues(content, maxWidth - padding.x * 2f, 0f);
        float w = Mathf.Min(maxWidth, pref.x + padding.x * 2f);
        float h = pref.y + padding.y * 2f;
        _rect.sizeDelta = new Vector2(w, h);

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

    // 이미 떠있는 툴팁의 텍스트만 갈아끼움 (위치/회전은 안 건드림) - 구매로 레벨이 바뀌었을 때 씀
    public void UpdateContent(string content)
    {
        if (!gameObject.activeSelf) return;

        _text.text = content;
        Vector2 pref = _text.GetPreferredValues(content, maxWidth - padding.x * 2f, 0f);
        float w = Mathf.Min(maxWidth, pref.x + padding.x * 2f);
        float h = pref.y + padding.y * 2f;
        _rect.sizeDelta = new Vector2(w, h);
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
