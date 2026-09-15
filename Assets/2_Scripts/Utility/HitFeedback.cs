using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Health))]
public class HitFeedback : MonoBehaviour
{
    [SerializeField] private float punchScale = 1.02f;
    [SerializeField] private float punchDuration = 0.1f;
    [SerializeField] private Color flashColor = Color.white;

    private SpriteRenderer _spriteRenderer;
    private Health _health;
    private Color _originalColor;
    private Vector3 _restScale; // 진짜 "쉬는 상태" 크기 - Awake 시점(펀치 연출이 한 번도 안 돈 상태)에 딱 한 번만 저장해둠
    private Coroutine _feedbackRoutine;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _health = GetComponent<Health>();
        _originalColor = _spriteRenderer.color;
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

        float half = punchDuration / 2f;
        float t = 0f;

        _spriteRenderer.color = flashColor;

        // 살짝 커짐
        while (t < half)
        {
            t += Time.deltaTime;
            float p = t / half;
            transform.localScale = Vector3.Lerp(originalScale, originalScale * punchScale, p);
            yield return null;
        }

        t = 0f;

        // 원래 크기/색상으로 복귀
        while (t < half)
        {
            t += Time.deltaTime;
            float p = t / half;
            transform.localScale = Vector3.Lerp(originalScale * punchScale, originalScale, p);
            _spriteRenderer.color = Color.Lerp(flashColor, _originalColor, p);
            yield return null;
        }

        transform.localScale = originalScale;
        _spriteRenderer.color = _originalColor;
        _feedbackRoutine = null;
    }
}
