using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 업그레이드 트리에서 선행(부모) 노드 -> 이 노드를 잇는 선. 선행 노드를 2개 이상(fromNode + extraFromNodes) 넣을 수 있고,
// 이 경우 전부(AND) 레벨업/해금돼야 toNode가 공개됨 - 화면에는 각 선행 노드에서 toNode로 선이 하나씩 따로 그려짐.
// 각 선은 자식으로 1~3개의 축정렬 사각형(Image)을 만들어 직선 / ㄱ자 / ㄴ자 / 계단(Z) 모양으로 꺾어 그림.
// from/to 노드의 RectTransform 위치를 매 프레임 읽어 따라감.
// 선은 그 선행 노드와 toNode가 둘 다 화면에 보일 때만 나타남 (한쪽만 보이면 반쪽짜리 선이 되므로) - 선행 노드별로 따로 판단함.
// 링크 GameObject는 노드와 같은 부모(Content) 아래에 있고, RectTransform은 anchor(0.5,0.5)/pivot(0.5,0.5)/offset0 이어야 함
// (그래야 세그먼트 anchoredPosition을 노드 anchoredPosition과 같은 좌표계로 계산할 수 있음).
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class UpgradeTreeLink : MonoBehaviour
{
    [SerializeField] private RectTransform fromNode; // 선행(부모) 노드 (1번째, 기존 씬 데이터 호환용 필드)
    [SerializeField] private RectTransform[] extraFromNodes = System.Array.Empty<RectTransform>(); // 추가 선행 노드(2번째부터) - fromNode와 합쳐 전부(AND) 충족돼야 toNode가 열림
    [SerializeField] private RectTransform toNode;   // 이 링크가 가리키는 자식 노드
    [SerializeField] private UpgradeManager.LinkRouting routing = UpgradeManager.LinkRouting.Straight; // 선 모양 (모든 선행 노드에 공통 적용)
    [SerializeField] private float thickness = 40f;  // 선 두께(px)
    [SerializeField, Range(0f, 1f)] private float bendRatio = 0.5f; // 계단형에서 꺾이는 지점 (from→to 사이 비율)
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.5f); // 선 색
    [SerializeField] private Sprite segmentSprite; // 세그먼트 스프라이트 (비우면 기본 흰 사각형)

    private readonly Dictionary<RectTransform, List<Image>> _segmentGroups = new Dictionary<RectTransform, List<Image>>(); // 선행 노드별 세그먼트 풀
    private readonly Dictionary<RectTransform, Vector2> _lastFrom = new Dictionary<RectTransform, Vector2>(); // 선행 노드별 직전 위치
    private Vector2 _lastTo; // 직전에 그린 toNode 위치
    private bool _dirty = true;

    // UpgradeManager/ObjectEconomyNodeUI가 선행관계(누가 누구의 선행인지)를 이 선으로 판단함
    public RectTransform FromNode => fromNode; // 기존 코드 호환용 - 첫번째 선행 노드만 필요할 때 씀
    public RectTransform ToNode => toNode;

    // 이 링크가 요구하는 모든 선행 노드 (fromNode + extraFromNodes, null 제외)
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

    void OnValidate() => _dirty = true; // 인스펙터에서 두께/색/routing 바꾸면 다시 그림

    void OnEnable()
    {
        // 세그먼트는 HideFlags.DontSave라 씬 저장/도메인 리로드 후 사라질 수 있음 - 남아있는 게 있으면
        // "Segment#<선행 노드 인스턴스ID>" 이름으로 다시 주워담고, 없으면 다음 LateUpdate에서 새로 만듦(중복 생성 방지)
        _segmentGroups.Clear();
        var recovered = new Dictionary<int, List<Image>>(); // 인스턴스ID -> 회수한 세그먼트들
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

        bool toVisible = toNode.gameObject.activeInHierarchy;
        Vector2 b = toNode.anchoredPosition;
        bool toMoved = _dirty || b != _lastTo;
        _lastTo = b;

        foreach (RectTransform source in FromNodes)
        {
            List<Image> segs = GetOrCreateGroup(source);
            int sourceId = source.GetInstanceID();

            // 선행 노드와 toNode가 둘 다 보일 때만 그 선을 그림 (한쪽만 보이면 허공에서 시작/끝나는 반쪽짜리 선이 됨)
            bool visible = toVisible && source.gameObject.activeInHierarchy;
            if (!visible)
            {
                foreach (Image seg in segs)
                    if (seg != null) seg.enabled = false;
                _lastFrom.Remove(source); // 다시 보이게 되면 강제로 재빌드하도록 기록 제거
                continue;
            }

            Vector2 a = source.anchoredPosition;
            bool aMoved = !_lastFrom.TryGetValue(source, out Vector2 prevA) || prevA != a;
            if (!aMoved && !toMoved) continue; // 이 선행 노드도, toNode도 안 움직였으면 다시 그릴 필요 없음

            _lastFrom[source] = a;
            Rebuild(segs, sourceId, a, b);
        }

        _dirty = false;
    }

    // source(선행 노드)의 세그먼트 풀을 가져오거나 새로 만듦
    private List<Image> GetOrCreateGroup(RectTransform source)
    {
        if (!_segmentGroups.TryGetValue(source, out List<Image> segs))
            _segmentGroups[source] = segs = new List<Image>();
        return segs;
    }

    // a, b = 선행 노드/toNode의 anchoredPosition (링크와 같은 좌표계, Content 중심 기준). sourceId = 선행 노드 인스턴스ID(세그먼트 이름표용)
    private void Rebuild(List<Image> segments, int sourceId, Vector2 a, Vector2 b)
    {
        switch (routing)
        {
            case UpgradeManager.LinkRouting.Straight:
                SetSegmentCount(segments, sourceId, 1);
                LayoutDiagonal(segments[0], a, b);
                break;

            case UpgradeManager.LinkRouting.ElbowVerticalFirst: // ㄱ자: 세로 먼저, 그 다음 가로
                SetSegmentCount(segments, sourceId, 2);
                LayoutVertical(segments[0], a, new Vector2(a.x, b.y));
                LayoutHorizontal(segments[1], new Vector2(a.x, b.y), b);
                break;

            case UpgradeManager.LinkRouting.ElbowHorizontalFirst: // ㄴ자: 가로 먼저, 그 다음 세로
                SetSegmentCount(segments, sourceId, 2);
                LayoutHorizontal(segments[0], a, new Vector2(b.x, a.y));
                LayoutVertical(segments[1], new Vector2(b.x, a.y), b);
                break;

            case UpgradeManager.LinkRouting.Stepped: // 계단: 가로 - 세로 - 가로
                SetSegmentCount(segments, sourceId, 3);
                float bx = Mathf.Lerp(a.x, b.x, bendRatio); // 꺾이는 x 위치
                LayoutHorizontal(segments[0], a, new Vector2(bx, a.y));
                LayoutVertical(segments[1], new Vector2(bx, a.y), new Vector2(bx, b.y));
                LayoutHorizontal(segments[2], new Vector2(bx, b.y), b);
                break;
        }
    }

    // 가로 세그먼트: 두 점의 y는 같다고 보고, x구간을 채우는 얇은 가로 막대
    private void LayoutHorizontal(Image seg, Vector2 p, Vector2 q)
    {
        var rt = (RectTransform)seg.transform;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition = (p + q) * 0.5f;
        rt.sizeDelta = new Vector2(Mathf.Abs(q.x - p.x) + thickness, thickness); // +thickness로 코너 빈틈을 메움
    }

    // 세로 세그먼트
    private void LayoutVertical(Image seg, Vector2 p, Vector2 q)
    {
        var rt = (RectTransform)seg.transform;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition = (p + q) * 0.5f;
        rt.sizeDelta = new Vector2(thickness, Mathf.Abs(q.y - p.y) + thickness);
    }

    // 직선(대각선): 두 점을 잇는 회전된 막대
    private void LayoutDiagonal(Image seg, Vector2 p, Vector2 q)
    {
        var rt = (RectTransform)seg.transform;
        Vector2 delta = q - p;
        float length = delta.magnitude;
        rt.anchoredPosition = (p + q) * 0.5f;
        rt.sizeDelta = new Vector2(length, thickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    // 필요한 개수만큼 세그먼트를 만들고(있으면 재사용), 나머지는 비활성. 세그먼트 이름에 소속 그룹(선행 노드) id를 심어둠(OnEnable 복구용)
    private void SetSegmentCount(List<Image> segments, int sourceId, int count)
    {
        while (segments.Count < count)
        {
            var go = new GameObject($"Segment#{sourceId}", typeof(RectTransform), typeof(Image));
            go.hideFlags = HideFlags.DontSave; // 자동 생성물이라 씬에 따로 저장하지 않음
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false; // 선이 클릭을 가로채면 안 됨
            segments.Add(img);
        }

        for (int i = 0; i < segments.Count; i++)
        {
            bool on = i < count;
            segments[i].enabled = on;
            if (on)
            {
                segments[i].color = color;
                segments[i].sprite = segmentSprite;
            }
        }
    }
}
