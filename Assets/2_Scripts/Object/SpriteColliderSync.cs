using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public class SpriteColliderSync : MonoBehaviour
{
    // 월드 유닛
    [SerializeField] private float edgePadding = 0.3f;

    private SpriteRenderer _spriteRenderer;
    private PolygonCollider2D _collider;
    private Sprite _lastSprite;
    private readonly List<Vector2> _pathBuffer = new List<Vector2>();
    private readonly List<Vector2> _allPoints = new List<Vector2>();

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<PolygonCollider2D>();
        SyncCollider();
    }

    void LateUpdate()
    {
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

        int shapeCount = _lastSprite.GetPhysicsShapeCount();

        if (shapeCount == 0)
        {
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

        // 윤곽이 여러 덩어리면 구멍으로 클릭이 빠져서 볼록 껍질 하나로 만듦
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

    private static void ConvexHull(List<Vector2> points, List<Vector2> result)
    {
        points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        result.Clear();

        for (int pass = 0; pass < 2; pass++) // 0 = 아래 껍질, 1 = 위 껍질
        {
            int start = result.Count;
            for (int i = 0; i < points.Count; i++)
            {
                int index = pass == 0 ? i : points.Count - 1 - i;
                while (result.Count >= start + 2 && Cross(result[result.Count - 2], result[result.Count - 1], points[index]) <= 0f)
                    result.RemoveAt(result.Count - 1);
                result.Add(points[index]);
            }
            result.RemoveAt(result.Count - 1);
        }
    }

    private static float Cross(Vector2 o, Vector2 a, Vector2 b) =>
        (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

    // PolygonCollider2D엔 edgeRadius가 없어서 정점을 바깥으로 밀어냄
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
