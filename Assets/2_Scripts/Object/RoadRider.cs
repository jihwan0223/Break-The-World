using UnityEngine;

// 도로 위를 혼자 달리는 오브젝트 (자동차). 화면 밖으로 나가면 반대쪽 끝에서 새로 나옴
public class RoadRider : MonoBehaviour
{
    [SerializeField] private float speed = 1.2f; // 월드 유닛/초

    private Health _health;
    private SpriteRenderer _sprite;
    private float _direction; // -1 = 왼쪽, 1 = 오른쪽 (그림이 왼쪽을 봄 - 뒤집힌 차는 오른쪽으로)

    void Awake()
    {
        _health = GetComponent<Health>();
        if (_health != null) _health.AutoRespawn = false; // 부서지면 화면 끝을 돌아 다시 들어올 때 새로 나옴
        _sprite = GetComponent<SpriteRenderer>();
        _direction = transform.lossyScale.x >= 0f ? -1f : 1f;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float center = transform.parent != null ? transform.parent.position.x : 0f; // 맵 가운데
        float margin = _sprite != null ? _sprite.bounds.extents.x : 1f; // 차가 다 빠져나갈 만큼
        float edge = cam.orthographicSize * cam.aspect + margin; // 가운데에서 화면 밖까지

        Vector3 position = transform.position;
        position.x += _direction * speed * Time.deltaTime;
        if ((position.x - center) * _direction > edge)
        {
            position.x = center - _direction * edge;
            if (_health != null) _health.ResetToFull();
        }
        transform.position = position;
    }
}
