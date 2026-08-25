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
    private Coroutine _feedbackRoutine;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _health = GetComponent<Health>();
        _originalColor = _spriteRenderer.color;
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
        // 매번 호출 시점의 실제 크기를 기준으로 삼음 (HealthSpriteSwitcher가 이미지마다 스케일을 자동 보정하므로,
        // 미리 캐싱해두면 오래된 크기로 되돌아가는 문제가 생김)
        Vector3 originalScale = transform.localScale;

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
