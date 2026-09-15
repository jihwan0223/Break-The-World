using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 업그레이드 트리에서 선행(부모) 노드 -> 이 노드를 잇는 선. 선행 노드를 2개 이상(fromNode + extraFromNodes) 넣을 수 있고,
// 이 경우 하나라도(OR) 레벨업/해금되면 toNode가 공개됨 - 화면에는 각 선행 노드에서 toNode로 선이 하나씩 따로 그려짐.
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
    [SerializeField] private RectTransform[] extraFromNodes = System.Array.Empty<RectTransform>(); // 추가 선행 노드(2번째부터) - fromNode와 합쳐 하나라도(OR) 충족되면 toNode가 열림
    [SerializeField] private RectTransform toNode;   // 이 링크가 가리키는 자식 노드
    [SerializeField] private UpgradeManager.LinkRouting routing = UpgradeManager.LinkRouting.Straight; // 선 모양 (모든 선행 노드에 공통 적용)
    [SerializeField] private float thickness = 40f;  // 선 두께(px)
    [SerializeField, Range(0f, 1f)] private float bendRatio = 0.5f; // 계단형에서 꺾이는 지점 (from→to 사이 비율)
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.5f); // 선 색
    [SerializeField] private Sprite segmentSprite; // 세그먼트 스프라이트 (비우면 기본 흰 사각형)

    private readonly Dictionary<RectTransform, List<Image>> _segmentGroups = new Dictionary<RectTransform, List<Image>>(); // 선행 노드별 세그먼트 풀
    private readonly Dictionary<RectTransform, Vector2> _lastFrom = new Dictionary<RectTransform, Vector2>(); // 선행 노드별 직전 위치
    private readonly Dictionary<RectTransform, System.Func<bool>> _completionCheckCache = new Dictionary<RectTransform, System.Func<bool>>(); // 선행 노드별 IsLeveled 체크 캐시 (OR일 때 실제로 충족한 선만 그리기 위함)
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

            // 선행 노드와 toNode가 둘 다 보이고, 그 선행 노드 자체가 실제로 충족(레벨업/해금)됐을 때만 그 선을 그림.
            // (한쪽만 보이면 허공에서 시작/끝나는 반쪽짜리 선이 됨. 선행이 여러 개(OR)면 toNode는 하나만 충족해도 열리는데,
            //  이때 아직 안 채운 다른 선행 쪽 선까지 같이 나오면 이상해서 - 실제로 충족한 선행에서 나온 선만 보이게 함)
            // 단, Play 모드가 아닐 때(에디터에서 트리 짜는 중)는 레벨업 여부와 무관하게 다 보여줌 - 안 그러면
            // 에디터에선 UpgradeManager.Instance가 없어서 항상 false 취급돼 방금 이은 선도 안 보이는 문제가 있었음
            bool gameplayGate = !Application.isPlaying || IsSourceCompleted(source);
            bool visible = toVisible && source.gameObject.activeInHierarchy && gameplayGate;
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
            Rebuild(segs, sourceId, source, a, b);
        }

        _dirty = false;
    }

    // source(선행 노드)가 실제로 충족(업그레이드면 레벨업, 해금/획득 노드면 IsLeveled)됐는지 - 결과를 캐싱해둠
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
                check = economyNode != null ? (System.Func<bool>)economyNode.IsLeveled : () => true; // 알 수 없는 타입이면 막지 않음
            }
            _completionCheckCache[source] = check;
        }
        return check();
    }

    // source(선행 노드)의 세그먼트 풀을 가져오거나 새로 만듦
    private List<Image> GetOrCreateGroup(RectTransform source)
    {
        if (!_segmentGroups.TryGetValue(source, out List<Image> segs))
            _segmentGroups[source] = segs = new List<Image>();
        return segs;
    }

    // a, b = 선행 노드/toNode의 anchoredPosition (링크와 같은 좌표계, Content 중심 기준). sourceId = 선행 노드 인스턴스ID(세그먼트 이름표용).
    // source = 선행 노드 RectTransform - 선을 노드 중심이 아니라 노드 사각형 "변"에서 시작하게 하려고 크기가 필요함
    private void Rebuild(List<Image> segments, int sourceId, RectTransform source, Vector2 a, Vector2 b)
    {
        Vector2 halfA = source.rect.size * 0.5f; // 선행 노드 사각형의 절반 크기
        Vector2 halfB = toNode.rect.size * 0.5f; // toNode 사각형의 절반 크기

        switch (routing)
        {
            case UpgradeManager.LinkRouting.Straight:
            {
                Vector2 dir = (b - a).normalized; // a->b 방향
                if (dir == Vector2.zero) break;
                Vector2 clippedA = a + dir * RectEdgeDistance(dir, halfA); // a쪽 변에서 시작
                Vector2 clippedB = b - dir * RectEdgeDistance(dir, halfB); // b쪽 변에서 끝
                SetSegmentCount(segments, sourceId, 1);
                LayoutDiagonal(segments[0], clippedA, clippedB);
                break;
            }

            case UpgradeManager.LinkRouting.ElbowVerticalFirst: // ㄱ자: 세로 먼저, 그 다음 가로
            {
                float vSign = Mathf.Sign(b.y - a.y); // a에서 세로로 나가는 방향(위/아래)
                float hSign = Mathf.Sign(b.x - a.x); // b로 가로로 들어오는 방향(좌/우)
                Vector2 clippedA = new Vector2(a.x, a.y + vSign * halfA.y); // a의 위/아래 변에서 시작
                Vector2 corner = new Vector2(a.x, b.y);
                Vector2 clippedB = new Vector2(b.x - hSign * halfB.x, b.y); // b의 좌/우 변에서 끝
                SetSegmentCount(segments, sourceId, 2);
                LayoutVertical(segments[0], clippedA, corner);
                LayoutHorizontal(segments[1], corner, clippedB);
                break;
            }

            case UpgradeManager.LinkRouting.ElbowHorizontalFirst: // ㄴ자: 가로 먼저, 그 다음 세로
            {
                float hSign = Mathf.Sign(b.x - a.x); // a에서 가로로 나가는 방향(좌/우)
                float vSign = Mathf.Sign(b.y - a.y); // b로 세로로 들어오는 방향(위/아래)
                Vector2 clippedA = new Vector2(a.x + hSign * halfA.x, a.y); // a의 좌/우 변에서 시작
                Vector2 corner = new Vector2(b.x, a.y);
                Vector2 clippedB = new Vector2(b.x, b.y - vSign * halfB.y); // b의 위/아래 변에서 끝
                SetSegmentCount(segments, sourceId, 2);
                LayoutHorizontal(segments[0], clippedA, corner);
                LayoutVertical(segments[1], corner, clippedB);
                break;
            }

            case UpgradeManager.LinkRouting.Stepped: // 계단: 가로 - 세로 - 가로 (양 끝 다 가로로 드나듦)
            {
                float hSignA = Mathf.Sign(b.x - a.x);
                float bx = Mathf.Lerp(a.x, b.x, bendRatio); // 꺾이는 x 위치 (원래 중심 좌표 기준으로 계산 - 클리핑과 무관하게 안정적으로)
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

    // dir 방향으로 half 크기 사각형의 중심에서 변까지의 거리
    private static float RectEdgeDistance(Vector2 dir, Vector2 half)
    {
        float tx = Mathf.Approximately(dir.x, 0f) ? float.PositiveInfinity : half.x / Mathf.Abs(dir.x);
        float ty = Mathf.Approximately(dir.y, 0f) ? float.PositiveInfinity : half.y / Mathf.Abs(dir.y);
        return Mathf.Min(tx, ty);
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
        rt.sizeDelta = new Vector2(length + thickness, thickness); // +thickness로 양 끝을 노드 안쪽으로 살짝 겹쳐서 틈 없이 이어지게 함 (ㄱ자/계단 세그먼트와 동일한 처리)
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
