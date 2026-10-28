using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Health))]
public class HitFeedback : MonoBehaviour
{
    [SerializeField] private float hitScale = 1.06f;
    [SerializeField] private float maxGrowth = 0.15f; // 월드 유닛
    [SerializeField] private float hitDuration = 0.12f; // 초

    private SpriteRenderer _spriteRenderer;
    private Health _health;
    private Vector3 _restScale; // Awake에서 한 번만 저장 - 연타로 끊겨도 기준이 안 변함
    private Coroutine _feedbackRoutine;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _health = GetComponent<Health>();
        _restScale = transform.localScale;
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
        if (_feedbackRoutine != null)
            StopCoroutine(_feedbackRoutine);

        _feedbackRoutine = StartCoroutine(PlayFeedback());
    }

    private IEnumerator PlayFeedback()
    {
        transform.localScale = _restScale;
        Vector3 originalScale = _restScale;

        float halfSize = Mathf.Max(_spriteRenderer.bounds.extents.x, _spriteRenderer.bounds.extents.y);
        float peak = halfSize > 0f ? Mathf.Min(hitScale, 1f + maxGrowth / halfSize) : hitScale;

        float half = hitDuration / 2f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            float p = t / half;
            transform.localScale = Vector3.Lerp(originalScale, originalScale * peak, p);
            yield return null;
        }

        t = 0f;

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
