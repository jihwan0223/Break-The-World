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
    private readonly List<Vector2> _pathBuffer = new List<Vector2>();

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
            // Sprite has no baked physics shape (Generate Physics Shape off) -> fall back to a rectangle.
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

        _collider.pathCount = shapeCount;

        for (int i = 0; i < shapeCount; i++)
        {
            _pathBuffer.Clear();
            _lastSprite.GetPhysicsShape(i, _pathBuffer);
            InflatePath(_pathBuffer);
            _collider.SetPath(i, _pathBuffer);
        }
    }

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
