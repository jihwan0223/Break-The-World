using UnityEngine;

public class ConveyorRider : MonoBehaviour
{
    [SerializeField] private SpriteRenderer belt;
    [SerializeField] private float speed = 1.2f; // 월드 유닛/초 - ConveyorBelt 속도와 맞춤
    [SerializeField, Range(0f, 1f)] private float startPhase; // 벨트 위 고정 칸 - 다른 Rider와 겹치지 않게 서로 다른 값 (예: 0, 0.5)

    private Health _health;
    private Health _beltHealth;
    private float _progress; // 0 = 오른쪽 끝, 1 = 왼쪽 끝 - 모든 Rider가 같은 시계를 따라감
    private Sprite _shapeSprite;
    private Vector2 _localMin, _localMax;

    void Awake()
    {
        _health = GetComponent<Health>();
        if (_health != null) _health.AutoRespawn = false; // 부서지면 자기 칸이 오른쪽 끝으로 돌아올 때만 다시 나옴
        _progress = startPhase;
    }

    void OnEnable()
    {
        _beltHealth = belt != null ? belt.GetComponent<Health>() : null;
        if (_beltHealth != null) _beltHealth.OnDied += BreakWithBelt;
    }

    void OnDisable()
    {
        if (_beltHealth != null) _beltHealth.OnDied -= BreakWithBelt;
    }

    private void BreakWithBelt()
    {
        if (_health == null || !_health.isActiveAndEnabled || _health.IsDead) return;
        _health.TakeDamage(_health.CurrentHP);
    }

    // Health 재생성 코루틴(Update 뒤)이 끝난 프레임에 그리기 전에 옮기려고 LateUpdate
    void LateUpdate()
    {
        if (belt == null || belt.sprite == null) return;
        if (!belt.gameObject.activeInHierarchy) return;
        if (belt.sprite != _shapeSprite) MeasureBelt(belt.sprite);

        Transform t = belt.transform;
        Vector2 start = t.TransformPoint(new Vector3(_localMax.x, _localMax.y, 0f));
        Vector2 end = t.TransformPoint(new Vector3(_localMin.x, _localMax.y, 0f));
        float length = Mathf.Max(0.01f, Vector2.Distance(start, end));

        // 정해진 시간표대로 움직임 - 부서져도 칸은 계속 돌고, 오른쪽 끝에 다시 올 때 새로 나옴
        _progress += speed * Time.deltaTime / length;
        if (_progress >= 1f)
        {
            _progress -= 1f;
            if (_health != null) _health.ResetToFull();
        }
        Vector2 position = Vector2.Lerp(start, end, _progress);
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private void MeasureBelt(Sprite sprite)
    {
        _shapeSprite = sprite;
        _localMin = sprite.bounds.min;
        _localMax = sprite.bounds.max;
        if (sprite.GetPhysicsShapeCount() == 0) return;

        var points = new System.Collections.Generic.List<Vector2>();
        _localMin = new Vector2(float.MaxValue, float.MaxValue);
        _localMax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++)
        {
            sprite.GetPhysicsShape(i, points);
            foreach (Vector2 p in points)
            {
                _localMin = Vector2.Min(_localMin, p);
                _localMax = Vector2.Max(_localMax, p);
            }
        }
    }
}
