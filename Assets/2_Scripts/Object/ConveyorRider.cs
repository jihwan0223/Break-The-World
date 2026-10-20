using UnityEngine;

// 컨베이어 벨트 위를 타고 가는 오브젝트(왁뿌볼). 벨트 오른쪽 끝(트럭 짐칸 쪽)에서 나와 벨트 윗면을 따라 왼쪽 끝까지 가면 사라지고,
// 다시 오른쪽 끝에서 새로 나옴(체력 꽉 찬 채로 - 그 전에 부숴야 조각을 얻음). 부서지면 다시 살아날 때 오른쪽 끝에서 새로 나옴.
// 길은 매 프레임 벨트 위치/크기/회전에서 계산해서, 벨트를 옮기거나 늘리면 그대로 따라감
public class ConveyorRider : MonoBehaviour
{
    [SerializeField] private SpriteRenderer belt; // 타고 갈 벨트 그림
    [SerializeField] private float speed = 1.2f; // 움직이는 속도(월드 단위/초) - 벨트(ConveyorBelt) 속도와 맞춤
    [SerializeField, Range(0f, 1f)] private float startPhase; // 처음 위치 (0 = 오른쪽 끝, 1 = 왼쪽 끝) - 여러 개가 한자리에 겹치지 않게

    private Health _health; // 체력 - 부서져 있는 동안은 멈춤
    private float _progress; // 지금 위치 (0 = 오른쪽 끝, 1 = 왼쪽 끝)
    private Sprite _shapeSprite; // 윗면 높이를 잰 벨트 그림
    private Vector2 _localMin, _localMax; // 벨트 그림에서 실제로 그려진 부분 범위 (벨트 기준, 투명 테두리 제외)

    void Awake()
    {
        _health = GetComponent<Health>();
        _progress = startPhase;
    }

    void OnEnable()
    {
        if (_health != null) _health.OnRespawned += Restart;
    }

    void OnDisable()
    {
        if (_health != null) _health.OnRespawned -= Restart;
    }

    private void Restart() => _progress = 0f;

    void Update()
    {
        if (belt == null || belt.sprite == null) return;
        if (belt.sprite != _shapeSprite) MeasureBelt(belt.sprite);

        Transform t = belt.transform; // 벨트 위치/크기/회전
        Vector2 start = t.TransformPoint(new Vector3(_localMax.x, _localMax.y, 0f)); // 오른쪽 끝 윗면 (월드)
        Vector2 end = t.TransformPoint(new Vector3(_localMin.x, _localMax.y, 0f)); // 왼쪽 끝 윗면 (월드)
        float length = Mathf.Max(0.01f, Vector2.Distance(start, end)); // 길 길이

        if (_health == null || !_health.IsDead) _progress += speed * Time.deltaTime / length;
        if (_progress >= 1f)
        {
            _progress = 0f;
            if (_health != null) _health.ResetToFull(); // 못 부수고 지나간 건 새것으로 다시 나옴
        }
        Vector2 position = Vector2.Lerp(start, end, _progress); // 지금 자리
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    // 벨트 그림에서 실제로 그려진 부분(물리 모양 = 투명 테두리 뺀 윤곽) 범위를 잼. 물리 모양이 없으면 그림 전체 범위
    private void MeasureBelt(Sprite sprite)
    {
        _shapeSprite = sprite;
        _localMin = sprite.bounds.min;
        _localMax = sprite.bounds.max;
        if (sprite.GetPhysicsShapeCount() == 0) return;

        var points = new System.Collections.Generic.List<Vector2>(); // 윤곽 점들
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
