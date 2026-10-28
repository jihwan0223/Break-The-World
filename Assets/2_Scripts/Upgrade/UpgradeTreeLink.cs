using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 노드와 같은 부모, anchor/pivot (0.5, 0.5)여야 좌표가 맞음
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class UpgradeTreeLink : MonoBehaviour
{
    [SerializeField] private RectTransform fromNode;
    [SerializeField] private RectTransform[] extraFromNodes = System.Array.Empty<RectTransform>();
    [SerializeField] private RectTransform toNode;
    [SerializeField] private UpgradeManager.LinkRouting routing = UpgradeManager.LinkRouting.Straight;
    [SerializeField] private float thickness = 30f; // px
    [SerializeField, Range(0f, 1f)] private float bendRatio = 0.5f; // 계단형 꺾이는 지점 (from→to 비율)
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color crystalColor = new Color(0.45f, 0.9f, 1f, 0.7f);
    [SerializeField] private Sprite segmentSprite;

    private readonly Dictionary<RectTransform, List<Image>> _segmentGroups = new Dictionary<RectTransform, List<Image>>();
    private readonly Dictionary<RectTransform, Vector2> _lastFrom = new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, System.Func<bool>> _completionCheckCache = new Dictionary<RectTransform, System.Func<bool>>();
    private Vector2 _lastTo;
    private bool _dirty = true;
    private Color _lastColor;

    public RectTransform ToNode => toNode;

    public IEnumerable<RectTransform> FromNodes
    {
        get
        {
            if (fromNode != null) yield return fromNode;
            if (extraFromNodes == null) yield break;
            foreach (RectTransform n in extraFromNodes)
                if (n != null) yield return n;
        }
    }

    void OnValidate() => _dirty = true;

    void OnEnable()
    {
        // 세그먼트는 DontSave라 사라질 수 있어 이름으로 다시 주워담음
        _segmentGroups.Clear();
        var recovered = new Dictionary<int, List<Image>>();
        foreach (Transform child in transform)
        {
            if (!child.name.StartsWith("Segment#")) continue;
            var img = child.GetComponent<Image>();
            if (img == null) continue;
            if (!int.TryParse(child.name.Substring("Segment#".Length), out int sourceId)) continue;
            if (!recovered.TryGetValue(sourceId, out List<Image> list))
                recovered[sourceId] = list = new List<Image>();
            list.Add(img);
        }
        foreach (RectTransform source in FromNodes)
            if (recovered.TryGetValue(source.GetInstanceID(), out List<Image> list))
                _segmentGroups[source] = list;

        _dirty = true;
    }

    void LateUpdate()
    {
        if (toNode == null) return;

        Color currentColor = CurrentColor();
        if (currentColor != _lastColor)
        {
            _lastColor = currentColor;
            _dirty = true;
        }

        bool toVisible = toNode.gameObject.activeInHierarchy;
        Vector2 b = toNode.anchoredPosition;
        bool toMoved = _dirty || b != _lastTo;
        _lastTo = b;

        foreach (RectTransform source in FromNodes)
        {
            List<Image> segs = GetOrCreateGroup(source);
            int sourceId = source.GetInstanceID();

            // 에디터에선 레벨과 무관하게 다 보임
            bool gameplayGate = !Application.isPlaying || IsSourceCompleted(source);
            bool visible = toVisible && source.gameObject.activeInHierarchy && gameplayGate;
            if (!visible)
            {
                foreach (Image seg in segs)
                    if (seg != null) seg.enabled = false;
                _lastFrom.Remove(source);
                continue;
            }

            Vector2 a = source.anchoredPosition;
            bool aMoved = !_lastFrom.TryGetValue(source, out Vector2 prevA) || prevA != a;
            if (!aMoved && !toMoved) continue;

            _lastFrom[source] = a;
            Rebuild(segs, sourceId, source, a, b);
        }

        _dirty = false;
    }

    private Color CurrentColor()
    {
        UpgradeNodeUI node = toNode.GetComponent<UpgradeNodeUI>();
        if (node != null && node.Costs != null)
            foreach (UpgradeNodeUI.NodeCost cost in node.Costs)
                if (cost != null && cost.useCrystals) return crystalColor;
        return color;
    }

    private bool IsSourceCompleted(RectTransform source)
    {
        if (!_completionCheckCache.TryGetValue(source, out System.Func<bool> check))
        {
            UpgradeNodeUI upgradeNode = source.GetComponent<UpgradeNodeUI>();
            if (upgradeNode != null)
            {
                check = upgradeNode.IsLeveled;
            }
            else
            {
                ObjectEconomyNodeUI economyNode = source.GetComponent<ObjectEconomyNodeUI>();
                check = economyNode != null ? (System.Func<bool>)economyNode.IsLeveled : () => true;
            }
            _completionCheckCache[source] = check;
        }
        return check();
    }

    private List<Image> GetOrCreateGroup(RectTransform source)
    {
        if (!_segmentGroups.TryGetValue(source, out List<Image> segs))
            _segmentGroups[source] = segs = new List<Image>();
        return segs;
    }

    private void Rebuild(List<Image> segments, int sourceId, RectTransform source, Vector2 a, Vector2 b)
    {
        Vector2 halfA = source.rect.size * 0.5f;
        Vector2 halfB = toNode.rect.size * 0.5f;

        switch (routing)
        {
            case UpgradeManager.LinkRouting.Straight:
            {
                Vector2 dir = (b - a).normalized;
                if (dir == Vector2.zero) break;
                Vector2 clippedA = a + dir * RectEdgeDistance(dir, halfA);
                Vector2 clippedB = b - dir * RectEdgeDistance(dir, halfB);
                SetSegmentCount(segments, sourceId, 1);
                LayoutDiagonal(segments[0], clippedA, clippedB);
                break;
            }

            case UpgradeManager.LinkRouting.ElbowVerticalFirst:
            {
                float vSign = Mathf.Sign(b.y - a.y);
                float hSign = Mathf.Sign(b.x - a.x);
                Vector2 clippedA = new Vector2(a.x, a.y + vSign * halfA.y);
                Vector2 corner = new Vector2(a.x, b.y);
                Vector2 clippedB = new Vector2(b.x - hSign * halfB.x, b.y);
                SetSegmentCount(segments, sourceId, 2);
                LayoutVertical(segments[0], clippedA, corner);
                LayoutHorizontal(segments[1], corner, clippedB);
                break;
            }

            case UpgradeManager.LinkRouting.ElbowHorizontalFirst:
            {
                float hSign = Mathf.Sign(b.x - a.x);
                float vSign = Mathf.Sign(b.y - a.y);
                Vector2 clippedA = new Vector2(a.x + hSign * halfA.x, a.y);
                Vector2 corner = new Vector2(b.x, a.y);
                Vector2 clippedB = new Vector2(b.x, b.y - vSign * halfB.y);
                SetSegmentCount(segments, sourceId, 2);
                LayoutHorizontal(segments[0], clippedA, corner);
                LayoutVertical(segments[1], corner, clippedB);
                break;
            }

            case UpgradeManager.LinkRouting.Stepped:
            {
                float hSignA = Mathf.Sign(b.x - a.x);
                float bx = Mathf.Lerp(a.x, b.x, bendRatio);
                Vector2 clippedA = new Vector2(a.x + hSignA * halfA.x, a.y);
                Vector2 clippedB = new Vector2(b.x - hSignA * halfB.x, b.y);
                SetSegmentCount(segments, sourceId, 3);
                LayoutHorizontal(segments[0], clippedA, new Vector2(bx, a.y));
                LayoutVertical(segments[1], new Vector2(bx, a.y), new Vector2(bx, b.y));
                LayoutHorizontal(segments[2], new Vector2(bx, b.y), clippedB);
                break;
            }
        }
    }

    private static float RectEdgeDistance(Vector2 dir, Vector2 half)
    {
        float tx = Mathf.Approximately(dir.x, 0f) ? float.PositiveInfinity : half.x / Mathf.Abs(dir.x);
        float ty = Mathf.Approximately(dir.y, 0f) ? float.PositiveInfinity : half.y / Mathf.Abs(dir.y);
        return Mathf.Min(tx, ty);
    }

    private void LayoutHorizontal(Image seg, Vector2 p, Vector2 q)
    {
        var rt = (RectTransform)seg.transform;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition = (p + q) * 0.5f;
        rt.sizeDelta = new Vector2(Mathf.Abs(q.x - p.x) + thickness, thickness); // +thickness로 코너 빈틈 메움
    }

    private void LayoutVertical(Image seg, Vector2 p, Vector2 q)
    {
        var rt = (RectTransform)seg.transform;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition = (p + q) * 0.5f;
        rt.sizeDelta = new Vector2(thickness, Mathf.Abs(q.y - p.y) + thickness);
    }

    private void LayoutDiagonal(Image seg, Vector2 p, Vector2 q)
    {
        var rt = (RectTransform)seg.transform;
        Vector2 delta = q - p;
        float length = delta.magnitude;
        rt.anchoredPosition = (p + q) * 0.5f;
        rt.sizeDelta = new Vector2(length + thickness, thickness); // +thickness로 양 끝 틈 메움
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private void SetSegmentCount(List<Image> segments, int sourceId, int count)
    {
        while (segments.Count < count)
        {
            var go = new GameObject($"Segment#{sourceId}", typeof(RectTransform), typeof(Image));
            go.hideFlags = HideFlags.DontSave; // 자동 생성물이라 저장 안 함
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false; // 클릭을 가로채지 않게
            segments.Add(img);
        }

        for (int i = 0; i < segments.Count; i++)
        {
            bool on = i < count;
            segments[i].enabled = on;
            if (on)
            {
                segments[i].color = _lastColor;
                segments[i].sprite = segmentSprite;
            }
        }
    }
}
