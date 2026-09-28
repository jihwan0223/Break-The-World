using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public class SpriteColliderSync : MonoBehaviour
{
    // 콜라이더를 스프라이트 외곽선보다 얼마나 더 넉넉하게 만들지 (월드 유닛 단위)
    [SerializeField] private float edgePadding = 0.3f;

    private SpriteRenderer _spriteRenderer;
    private PolygonCollider2D _collider;
    private Sprite _lastSprite;
    private readonly List<Vector2> _pathBuffer = new List<Vector2>(); // 콜라이더 경로를 만들 때 재사용하는 점 목록
    private readonly List<Vector2> _allPoints = new List<Vector2>(); // 여러 윤곽선의 점을 한데 모아 볼록 껍질을 구할 때 쓰는 목록

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<PolygonCollider2D>();
        SyncCollider();
    }

    void LateUpdate()
    {
        // 매 프레임 스프라이트가 바뀌었는지만 저렴하게 체크 (참조 비교)
        if (_spriteRenderer.sprite != _lastSprite)
        {
            SyncCollider();
        }
    }

    private void SyncCollider()
    {
        _lastSprite = _spriteRenderer.sprite;

        if (_lastSprite == null)
        {
            _collider.pathCount = 0;
            return;
        }

        // 스프라이트 임포트 시 알파 채널 기준으로 미리 계산된 외곽선(Physics Shape)을 가져옴
        int shapeCount = _lastSprite.GetPhysicsShapeCount();

        if (shapeCount == 0)
        {
            // Physics Shape이 없는 스프라이트(Generate Physics Shape 꺼짐)는 사각형으로 대체
            Bounds bounds = _lastSprite.bounds;
            _pathBuffer.Clear();
            _pathBuffer.Add(new Vector2(bounds.min.x, bounds.min.y));
            _pathBuffer.Add(new Vector2(bounds.max.x, bounds.min.y));
            _pathBuffer.Add(new Vector2(bounds.max.x, bounds.max.y));
            _pathBuffer.Add(new Vector2(bounds.min.x, bounds.max.y));

            InflatePath(_pathBuffer);
            _collider.pathCount = 1;
            _collider.SetPath(0, _pathBuffer);
            return;
        }

        // 윤곽선이 여러 덩어리(자전거 바퀴처럼 안이 비어 구멍이 생기거나 파편이 떨어져 나간 그림)면 클릭이 구멍으로 빠지므로
        // 전체를 감싸는 볼록 껍질 하나로 콜라이더를 만듦
        if (shapeCount > 1)
        {
            _allPoints.Clear();
            for (int i = 0; i < shapeCount; i++)
            {
                _lastSprite.GetPhysicsShape(i, _pathBuffer);
                _allPoints.AddRange(_pathBuffer);
            }

            ConvexHull(_allPoints, _pathBuffer);
            InflatePath(_pathBuffer);
            _collider.pathCount = 1;
            _collider.SetPath(0, _pathBuffer);
            return;
        }

        _collider.pathCount = shapeCount;

        for (int i = 0; i < shapeCount; i++)
        {
            _pathBuffer.Clear();
            _lastSprite.GetPhysicsShape(i, _pathBuffer);
            InflatePath(_pathBuffer);
            _collider.SetPath(i, _pathBuffer);
        }
    }

    // 점들을 감싸는 볼록 껍질(Andrew monotone chain)을 result에 채움
    private static void ConvexHull(List<Vector2> points, List<Vector2> result)
    {
        points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        result.Clear();

        for (int pass = 0; pass < 2; pass++) // 0: 아래 껍질, 1: 위 껍질
        {
            int start = result.Count;
            for (int i = 0; i < points.Count; i++)
            {
                int index = pass == 0 ? i : points.Count - 1 - i; // 위 껍질은 반대 방향으로 훑음
                while (result.Count >= start + 2 && Cross(result[result.Count - 2], result[result.Count - 1], points[index]) <= 0f)
                    result.RemoveAt(result.Count - 1);
                result.Add(points[index]);
            }
            result.RemoveAt(result.Count - 1); // 마지막 점은 다음 껍질의 첫 점과 겹침
        }
    }

    private static float Cross(Vector2 o, Vector2 a, Vector2 b) =>
        (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

    // PolygonCollider2D엔 edgeRadius가 없어서(EdgeCollider2D 전용 속성),
    // 각 정점을 도형 중심에서 바깥 방향으로 밀어내는 방식으로 여유 공간을 흉내낸다.
    private void InflatePath(List<Vector2> path)
    {
        if (edgePadding == 0f || path.Count == 0)
            return;

        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < path.Count; i++)
            centroid += path[i];
        centroid /= path.Count;

        for (int i = 0; i < path.Count; i++)
        {
            Vector2 direction = (path[i] - centroid).normalized;
            path[i] += direction * edgePadding;
        }
    }
}
