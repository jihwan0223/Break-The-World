using UnityEngine;

// 배경 장식이 제자리 주변을 천천히 떠다님 (충돌판정 없음 = 클릭 안 됨)
public class FloatDrift : MonoBehaviour
{
    [SerializeField] private float radius = 0.15f; // 월드 유닛, 떠다니는 반경
    [SerializeField] private float period = 6f; // 초, 한 바퀴 도는 시간
    [SerializeField] private float spinSpeed = 5f; // 도/초, 음수면 반대로 돔

    private Vector3 _home; // 배치된 원래 위치
    private float _phase; // 별마다 다른 시작점 (라디안)

    void Start()
    {
        _home = transform.localPosition;
        _phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        float t = Time.time * Mathf.PI * 2f / Mathf.Max(0.1f, period) + _phase; // 현재 각도
        transform.localPosition = _home + new Vector3(Mathf.Cos(t), Mathf.Sin(t * 0.7f), 0f) * radius; // 0.7배로 어긋나게 해서 원이 아닌 흔들리는 궤적
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }
}
