using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponSwingEffect : MonoBehaviour
{
    public static WeaponSwingEffect Instance { get; private set; }

    [SerializeField] private float swingSize = 0.05f; // 월드 유닛
    [SerializeField] private float swingDuration = 0.15f; // 초
    [SerializeField] private float viewMargin = 0.8f; // 월드 유닛

    private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
    private Vector3? _lastPoint;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void PlaySwing(Collider2D hitCollider, Sprite icon, bool onSurface = false, Vector2? cursorPoint = null)
    {
        if (icon == null || hitCollider == null)
            return;

        Vector3 point;
        if (cursorPoint.HasValue)
            point = new Vector3(cursorPoint.Value.x, cursorPoint.Value.y, hitCollider.bounds.center.z);
        else
            point = ClampToView(onSurface ? GetRandomPointInsideCollider(hitCollider) : GetRandomPointOnColliderEdge(hitCollider));

        Vector2 towardCollider = (Vector2)(hitCollider.bounds.center - point);
        float angle = Mathf.Atan2(towardCollider.y, towardCollider.x) * Mathf.Rad2Deg - 90f;

        float sizeMultiplier = 1f;
        float rotationOffset = 0f;

        if (WeaponManager.Instance != null)
        {
            int equippedIndex = WeaponManager.Instance.EquippedIndex;
            sizeMultiplier = WeaponManager.Instance.GetSizeMultiplier(equippedIndex);
            rotationOffset = WeaponManager.Instance.GetRotationOffset(equippedIndex);
        }

        _lastPoint = point;

        SpriteRenderer spriteRenderer = GetFromPool();
        StartCoroutine(SwingRoutine(spriteRenderer, point, angle, icon, sizeMultiplier, rotationOffset));
    }

    private IEnumerator SwingRoutine(SpriteRenderer spriteRenderer, Vector3 position, float angle, Sprite icon, float sizeMultiplier, float rotationOffset)
    {
        spriteRenderer.transform.position = position;
        spriteRenderer.transform.localScale = Vector3.one * swingSize * sizeMultiplier;
        spriteRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
        spriteRenderer.sprite = icon;
        spriteRenderer.gameObject.SetActive(true);

        yield return new WaitForSeconds(swingDuration);

        spriteRenderer.gameObject.SetActive(false);
        _pool.Push(spriteRenderer);
    }

    private Vector3 ClampToView(Vector3 point)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return point;

        float halfHeight = cam.orthographicSize - viewMargin;
        float halfWidth = cam.orthographicSize * cam.aspect - viewMargin;
        Vector3 center = cam.transform.position;
        point.x = Mathf.Clamp(point.x, center.x - halfWidth, center.x + halfWidth);
        point.y = Mathf.Clamp(point.y, center.y - halfHeight, center.y + halfHeight);
        return point;
    }

    private SpriteRenderer GetFromPool()
    {
        if (_pool.Count > 0)
            return _pool.Pop();

        var visualObject = new GameObject("WeaponSwingVisual");
        visualObject.transform.SetParent(transform);

        var spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 20;

        visualObject.SetActive(false);
        return spriteRenderer;
    }

    private Vector3 GetRandomPointOnColliderEdge(Collider2D collider)
    {
        if (collider is PolygonCollider2D polygon && polygon.pathCount > 0)
        {
            // 구멍 경로를 피하려고 둘레가 가장 긴 바깥 경로만 씀
            Vector2[] points = GetOutermostPath(polygon);

            if (points != null && points.Length >= 2)
            {
                float minDistance = collider.bounds.size.magnitude * 0.35f;

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int i = Random.Range(0, points.Length);
                    int next = (i + 1) % points.Length;
                    Vector2 localPoint = Vector2.Lerp(points[i], points[next], Random.value);
                    Vector3 worldPoint = polygon.transform.TransformPoint(localPoint);

                    if (_lastPoint == null || Vector3.Distance(worldPoint, _lastPoint.Value) >= minDistance)
                        return worldPoint;
                }
            }
        }

        return collider.bounds.center;
    }

    private Vector3 GetRandomPointInsideCollider(Collider2D collider)
    {
        Bounds bounds = collider.bounds;
        float minDistance = bounds.size.magnitude * 0.2f;
        Vector2 fallback = bounds.center;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            var point = new Vector2(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y));
            if (!collider.OverlapPoint(point)) continue;

            fallback = point;
            if (_lastPoint == null || Vector3.Distance(point, _lastPoint.Value) >= minDistance)
                return point;
        }

        return fallback;
    }

    private Vector2[] GetOutermostPath(PolygonCollider2D polygon)
    {
        Vector2[] outermost = null;
        float longestPerimeter = -1f;

        for (int p = 0; p < polygon.pathCount; p++)
        {
            Vector2[] path = polygon.GetPath(p);
            float perimeter = 0f;

            for (int i = 0; i < path.Length; i++)
                perimeter += Vector2.Distance(path[i], path[(i + 1) % path.Length]);

            if (perimeter > longestPerimeter)
            {
                longestPerimeter = perimeter;
                outermost = path;
            }
        }

        return outermost;
    }
}
