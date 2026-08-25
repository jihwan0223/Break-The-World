using System.Collections;
using UnityEngine;

// 클릭이 성공했을 때, 맞은 오브젝트 콜라이더의 테두리(외곽선) 위 랜덤한 지점에
// 현재 장착 중인 무기 이미지를 잠깐 띄워서 "때리는" 느낌을 주는 이펙트.
// 프리팹 없이 코드로 오브젝트 하나만 만들어두고 재사용함 (SetActive 대신 SpriteRenderer 표시만 껐다 켬).
public class WeaponSwingEffect : MonoBehaviour
{
    // 씬 어디서든 WeaponSwingEffect.Instance로 접근하기 위한 싱글톤
    public static WeaponSwingEffect Instance { get; private set; }

    [SerializeField] private float swingSize = 0.05f; // 무기 이미지가 표시될 크기 (월드 유닛)
    [SerializeField] private float swingDuration = 0.15f; // 타격 연출이 재생되는 시간(초)

    private SpriteRenderer _spriteRenderer; // 무기 이미지를 보여주는 전용 렌더러 (재사용됨)
    private Coroutine _swingRoutine; // 현재 재생 중인 타격 연출 코루틴 (연타 시 갈아치우기 위해 보관)

    void Awake()
    {
        // 씬에 WeaponSwingEffect가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        var visualObject = new GameObject("WeaponSwingVisual");
        visualObject.transform.SetParent(transform);

        _spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
        _spriteRenderer.sortingOrder = 20; // 도트/오브젝트보다도 위에 그려지도록
        _spriteRenderer.enabled = false;
    }

    // hitCollider의 외곽선 위 랜덤한 지점에 icon을 잠깐 띄움
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

        if (_swingRoutine != null)
            StopCoroutine(_swingRoutine);

        _swingRoutine = StartCoroutine(SwingRoutine(point, angle, icon, sizeMultiplier, rotationOffset));
    }

    private IEnumerator SwingRoutine(Vector3 position, float angle, Sprite icon, float sizeMultiplier, float rotationOffset)
    {
        _spriteRenderer.transform.position = position;
        _spriteRenderer.transform.localScale = Vector3.one * swingSize * sizeMultiplier;
        _spriteRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
        _spriteRenderer.sprite = icon;
        _spriteRenderer.enabled = true;

        yield return new WaitForSeconds(swingDuration);

        _spriteRenderer.enabled = false;
        _swingRoutine = null;
    }

    // 콜라이더가 PolygonCollider2D면 외곽선(Path)에서 랜덤한 변을 고르고, 그 변 위 랜덤한 점을 반환
    // 다른 콜라이더 타입이면 정확한 외곽선을 알 수 없으니 콜라이더 중심으로 대체
    private Vector3 GetRandomPointOnColliderEdge(Collider2D collider)
    {
        if (collider is PolygonCollider2D polygon && polygon.pathCount > 0)
        {
            int pathIndex = Random.Range(0, polygon.pathCount);
            Vector2[] points = polygon.GetPath(pathIndex);

            if (points.Length >= 2)
            {
                int i = Random.Range(0, points.Length);
                int next = (i + 1) % points.Length;
                Vector2 localPoint = Vector2.Lerp(points[i], points[next], Random.value);
                return polygon.transform.TransformPoint(localPoint);
            }
        }

        return collider.bounds.center;
    }
}
