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
    private Vector3 _originalScale;
    private Color _originalColor;
    private Coroutine _feedbackRoutine;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _health = GetComponent<Health>();
        _originalScale = transform.localScale;
        _originalColor = _spriteRenderer.color;
    }

    void OnEnable()
    {
        _health.OnDamaged += HandleDamaged;
    }

    void OnDisable()
    {
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
        float half = punchDuration / 2f;
        float t = 0f;

        _spriteRenderer.color = flashColor;

        // 살짝 커짐
        while (t < half)
        {
            t += Time.deltaTime;
            float p = t / half;
            transform.localScale = Vector3.Lerp(_originalScale, _originalScale * punchScale, p);
            yield return null;
        }

        t = 0f;

        // 원래 크기/색상으로 복귀
        while (t < half)
        {
            t += Time.deltaTime;
            float p = t / half;
            transform.localScale = Vector3.Lerp(_originalScale * punchScale, _originalScale, p);
            _spriteRenderer.color = Color.Lerp(flashColor, _originalColor, p);
            yield return null;
        }

        transform.localScale = _originalScale;
        _spriteRenderer.color = _originalColor;
        _feedbackRoutine = null;
    }
}
