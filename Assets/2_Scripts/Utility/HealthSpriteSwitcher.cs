using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Health))]
public class HealthSpriteSwitcher : MonoBehaviour
{
    // 체력 100% -> 0% 순서로 넣기 (예: Object1, Object1-1, Object1-2, Object1-3)
    [SerializeField] private Sprite[] healthStages;

    private SpriteRenderer _spriteRenderer;
    private Health _health;

    void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _health = GetComponent<Health>();

        _health.OnDamaged += UpdateSprite;
        UpdateSprite(_health.CurrentHP, _health.MaxHP);
    }

    void OnDestroy()
    {
        _health.OnDamaged -= UpdateSprite;
    }

    private void UpdateSprite(int current, int max)
    {
        if (healthStages == null || healthStages.Length == 0 || max <= 0)
            return;

        float ratio = (float)current / max;

        // 체력 비율을 healthStages 배열 길이만큼 균등하게 나눠서 단계 인덱스를 구함
        // 예: 4단계면 100~76% -> 0, 75~51% -> 1, 50~26% -> 2, 25~0% -> 3
        int stageIndex = Mathf.FloorToInt((1f - ratio) * healthStages.Length);
        stageIndex = Mathf.Clamp(stageIndex, 0, healthStages.Length - 1);

        _spriteRenderer.sprite = healthStages[stageIndex];
    }
}
