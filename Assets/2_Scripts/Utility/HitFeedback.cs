using System.Collections;
using UnityEngine;

// 맞을 때마다 오브젝트가 움찔하는 연출 - 살짝 커졌다 돌아옴 (색은 건드리지 않음)
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Health))]
public class HitFeedback : MonoBehaviour
{
    [SerializeField] private float hitScale = 1.06f; // 맞았을 때 가장 커진 배율
    [SerializeField] private float maxGrowth = 0.15f; // 커지는 양의 상한(월드 유닛) - 책상처럼 큰 오브젝트가 화면째 출렁이지 않게
    [SerializeField] private float hitDuration = 0.12f; // 커졌다 돌아오는 전체 시간(초)

    private SpriteRenderer _spriteRenderer;
    private Health _health;
    private Vector3 _restScale; // 진짜 "쉬는 상태" 크기 - Awake 시점(펀치 연출이 한 번도 안 돈 상태)에 딱 한 번만 저장해둠
    private Coroutine _feedbackRoutine;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _health = GetComponent<Health>();
        _restScale = transform.localScale; // 연타 중 코루틴이 끊겨서 커진 채로 남아있어도 기준이 안 흔들리도록 고정값으로 저장
    }

    void OnEnable()
    {
        if (_health != null)
            _health.OnDamaged += HandleDamaged;
    }

    void OnDisable()
    {
        if (_health != null)
            _health.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(int current, int max)
    {
        // 연타 시 이전 연출을 끊고 새로 시작해서 매 클릭마다 반응하는 느낌을 유지
        if (_feedbackRoutine != null)
            StopCoroutine(_feedbackRoutine);

        _feedbackRoutine = StartCoroutine(PlayFeedback());
    }

    private IEnumerator PlayFeedback()
    {
        // 매번 Awake 때 저장해둔 고정 기준(_restScale)으로 되돌린 뒤 시작 - 연타 중 이전 펀치가 커지는 도중에
        // 끊겨도 그 값을 기준 삼지 않아서 클릭할수록 점점 커지는 현상을 방지함
        transform.localScale = _restScale;
        Vector3 originalScale = _restScale;

        float halfSize = Mathf.Max(_spriteRenderer.bounds.extents.x, _spriteRenderer.bounds.extents.y); // 오브젝트 반쪽 크기(월드 유닛)
        float peak = halfSize > 0f ? Mathf.Min(hitScale, 1f + maxGrowth / halfSize) : hitScale; // 이번에 커질 배율 - 큰 오브젝트일수록 덜 커짐

        float half = hitDuration / 2f;
        float t = 0f;

        // 살짝 커짐
        while (t < half)
        {
            t += Time.deltaTime;
            float p = t / half;
            transform.localScale = Vector3.Lerp(originalScale, originalScale * peak, p);
            yield return null;
        }

        t = 0f;

        // 원래 크기로 복귀
        while (t < half)
        {
            t += Time.deltaTime;
            float p = t / half;
            transform.localScale = Vector3.Lerp(originalScale * peak, originalScale, p);
            yield return null;
        }

        transform.localScale = originalScale;
        _feedbackRoutine = null;
    }
}
