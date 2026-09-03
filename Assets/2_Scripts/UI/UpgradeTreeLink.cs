using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 업그레이드 트리에서 선행(부모) 노드 -> 이 노드를 잇는 선. 자식으로 1~3개의 축정렬 사각형(Image)을 만들어
// 직선 / ㄱ자 / ㄴ자 / 계단(Z) 모양으로 꺾어 그림. from/to 노드의 RectTransform 위치를 매 프레임 읽어 따라감.
// 링크 GameObject는 노드와 같은 부모(Content) 아래에 있고, RectTransform은 anchor(0.5,0.5)/pivot(0.5,0.5)/offset0 이어야 함
// (그래야 세그먼트 anchoredPosition을 노드 anchoredPosition과 같은 좌표계로 계산할 수 있음).
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class UpgradeTreeLink : MonoBehaviour
{
    [SerializeField] private RectTransform fromNode; // 선행(부모) 노드
    [SerializeField] private RectTransform toNode;   // 이 링크가 가리키는 자식 노드
    [SerializeField] private UpgradeManager.LinkRouting routing = UpgradeManager.LinkRouting.Straight; // 선 모양
    [SerializeField] private float thickness = 6f;   // 선 두께(px)
    [SerializeField, Range(0f, 1f)] private float bendRatio = 0.5f; // 계단형에서 꺾이는 지점 (from→to 사이 비율)
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.5f); // 선 색
    [SerializeField] private Sprite segmentSprite; // 세그먼트 스프라이트 (비우면 기본 흰 사각형)

    private readonly List<Image> _segments = new List<Image>(); // 재사용하는 세그먼트 풀
    private Vector2 _lastA, _lastB; // 직전에 그린 from/to 위치 - 안 움직였으면 Rebuild 생략 (매 프레임 RectTransform 더럽히지 않게)
    private bool _dirty = true;

    // "부모노드id->자식노드id" - 트리 생성기가 링크 중복 생성을 피할 때 비교용
    public string PairKey => (fromNode != null ? fromNode.name : "?") + "->" + (toNode != null ? toNode.name : "?");

    // 트리 생성기가 호출 - 어떤 두 노드를 어떤 모양으로 이을지 지정
    public void Bind(RectTransform from, RectTransform to, UpgradeManager.LinkRouting linkRouting)
    {
        fromNode = from;
        toNode = to;
        routing = linkRouting;
        _dirty = true;
    }

    void OnValidate() => _dirty = true; // 인스펙터에서 두께/색/routing 바꾸면 다시 그림

    void OnEnable()
    {
        // 세그먼트는 HideFlags.DontSave라 씬 저장/도메인 리로드 후 사라질 수 있음 - 남아있는 게 있으면 다시 주워담고,
        // 없으면 다음 LateUpdate에서 새로 만듦 (중복 생성 방지)
        _segments.Clear();
        foreach (Transform child in transform)
        {
            if (child.name != "Segment") continue;
            var img = child.GetComponent<Image>();
            if (img != null) _segments.Add(img);
        }
    }

    void LateUpdate()
    {
        bool visible = fromNode != null && toNode != null
            && fromNode.gameObject.activeInHierarchy && toNode.gameObject.activeInHierarchy;

        if (!visible)
        {
            foreach (Image seg in _segments)
                if (seg != null) seg.enabled = false;
            _dirty = true; // 다시 보이게 되면 세그먼트를 켜야 하므로 강제 재빌드 표시
            return;
        }

        Vector2 a = fromNode.anchoredPosition;
        Vector2 b = toNode.anchoredPosition;
        if (!_dirty && a == _lastA && b == _lastB) return; // 노드가 안 움직였으면 다시 그릴 필요 없음

        _lastA = a;
        _lastB = b;
        _dirty = false;
        Rebuild(a, b);
    }

    // a, b = from/to 노드의 anchoredPosition (링크와 같은 좌표계, Content 중심 기준)
    private void Rebuild(Vector2 a, Vector2 b)
    {
        switch (routing)
        {
            case UpgradeManager.LinkRouting.Straight:
                SetSegmentCount(1);
                LayoutDiagonal(_segments[0], a, b);
                break;

            case UpgradeManager.LinkRouting.ElbowVerticalFirst: // ㄱ자: 세로 먼저, 그 다음 가로
                SetSegmentCount(2);
                LayoutVertical(_segments[0], a, new Vector2(a.x, b.y));
                LayoutHorizontal(_segments[1], new Vector2(a.x, b.y), b);
                break;

            case UpgradeManager.LinkRouting.ElbowHorizontalFirst: // ㄴ자: 가로 먼저, 그 다음 세로
                SetSegmentCount(2);
                LayoutHorizontal(_segments[0], a, new Vector2(b.x, a.y));
                LayoutVertical(_segments[1], new Vector2(b.x, a.y), b);
                break;

            case UpgradeManager.LinkRouting.Stepped: // 계단: 가로 - 세로 - 가로
                SetSegmentCount(3);
                float bx = Mathf.Lerp(a.x, b.x, bendRatio); // 꺾이는 x 위치
                LayoutHorizontal(_segments[0], a, new Vector2(bx, a.y));
                LayoutVertical(_segments[1], new Vector2(bx, a.y), new Vector2(bx, b.y));
                LayoutHorizontal(_segments[2], new Vector2(bx, b.y), b);
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

    // 필요한 개수만큼 세그먼트를 만들고(있으면 재사용), 나머지는 비활성
    private void SetSegmentCount(int count)
    {
        while (_segments.Count < count)
        {
            var go = new GameObject("Segment", typeof(RectTransform), typeof(Image));
            go.hideFlags = HideFlags.DontSave; // 자동 생성물이라 씬에 따로 저장하지 않음
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false; // 선이 클릭을 가로채면 안 됨
            _segments.Add(img);
        }

        for (int i = 0; i < _segments.Count; i++)
        {
            bool on = i < count;
            _segments[i].enabled = on;
            if (on)
            {
                _segments[i].color = color;
                _segments[i].sprite = segmentSprite;
            }
        }
    }
}
