using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 클릭이 성공했을 때, 맞은 오브젝트 콜라이더의 테두리(외곽선) 위 랜덤한 지점에
// 현재 장착 중인 무기 이미지를 잠깐 띄워서 "때리는" 느낌을 주는 이펙트.
// 더블클릭/자동클릭처럼 한 프레임에 여러 번 때릴 때 각각 따로 보이도록, 호출마다 풀에서 독립된 오브젝트를 꺼내 씀
// (DebrisPool과 같은 방식: GameObject를 재사용하되, 동시에 여러 개가 떠 있을 수 있게 함)
public class WeaponSwingEffect : MonoBehaviour
{
    // 씬 어디서든 WeaponSwingEffect.Instance로 접근하기 위한 싱글톤
    public static WeaponSwingEffect Instance { get; private set; }

    [SerializeField] private float swingSize = 0.05f; // 무기 이미지가 표시될 크기 (월드 유닛)
    [SerializeField] private float swingDuration = 0.15f; // 타격 연출이 재생되는 시간(초)

    private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>(); // 재사용 가능한(비활성) 타격 연출 오브젝트들
    private Vector3? _lastPoint; // 바로 직전 타격 연출이 나타났던 위치 - 다음 연출이 그 자리와 겹치지 않게 확인하는 데 씀

    void Awake()
    {
        // 씬에 WeaponSwingEffect가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // hitCollider의 외곽선 위 랜덤한 지점에 icon을 잠깐 띄움. 같은 프레임에 여러 번 불러도 각각 독립적으로 보임
    public void PlaySwing(Collider2D hitCollider, Sprite icon)
    {
        if (icon == null || hitCollider == null)
            return;

        Vector3 point = GetRandomPointOnColliderEdge(hitCollider);

        // 이미지의 "위쪽"(로컬 +Y)이 그 지점에서 콜라이더 중심을 향하도록 회전 계산
        Vector2 towardCollider = (Vector2)(hitCollider.bounds.center - point);
        float angle = Mathf.Atan2(towardCollider.y, towardCollider.x) * Mathf.Rad2Deg - 90f;

        // 현재 장착 중인 무기의 크기 배율/회전 보정값을 그때그때 조회 (플레이 중 인스펙터 값 수정이 바로 반영되도록)
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

    // 재사용 가능한 연출 오브젝트를 풀에서 꺼내거나, 없으면(동시에 여러 개가 떠 있는 상황) 새로 만듦
    private SpriteRenderer GetFromPool()
    {
        if (_pool.Count > 0)
            return _pool.Pop();

        var visualObject = new GameObject("WeaponSwingVisual");
        visualObject.transform.SetParent(transform);

        var spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 20; // 도트/오브젝트보다도 위에 그려지도록

        visualObject.SetActive(false);
        return spriteRenderer;
    }

    // 콜라이더가 PolygonCollider2D면 바깥 테두리(Path) 위 랜덤한 점을 반환하되, 직전 연출 위치와 너무 가까우면 다시 뽑음.
    // 다른 콜라이더 타입이면 정확한 외곽선을 알 수 없으니 콜라이더 중심으로 대체
    private Vector3 GetRandomPointOnColliderEdge(Collider2D collider)
    {
        if (collider is PolygonCollider2D polygon && polygon.pathCount > 0)
        {
            // 금이 가거나 뚫린 부분이 있는 스프라이트는 콜라이더에 안쪽 작은 경로(구멍)가 추가로 생기는데,
            // 그 경로를 뽑으면 무기가 오브젝트 한가운데(틈 안쪽)에 나타나 버림 - 그래서 항상 둘레가 가장 긴(=바깥 테두리) 경로만 씀
            Vector2[] points = GetOutermostPath(polygon);

            if (points != null && points.Length >= 2)
            {
                // 같은 자리(직전 연출 위치)가 다시 나오지 않도록 최소 거리 이상 떨어진 점이 나올 때까지 몇 번 다시 뽑음
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

    // 폴리곤 콜라이더의 여러 경로(Path) 중 둘레가 가장 긴 것을 바깥 테두리로 간주해서 반환
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
