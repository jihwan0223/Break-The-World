using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Health))]
public class HealthSpriteSwitcher : MonoBehaviour
{
    // ObjectManager가 없을 때를 대비한 로컬 스프라이트 목록 (체력 100% -> 0% 순서)
    [SerializeField] private Sprite[] fallbackHealthStages;
    [SerializeField] private float targetSize = 3f; // 이미지 원본 크기와 무관하게 맞출 목표 크기 (면적 기준, 월드 유닛)

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
        Sprite[] healthStages = GetHealthStages();

        if (healthStages == null || healthStages.Length == 0 || max <= 0)
            return;

        float ratio = (float)current / max;

        // 체력 비율을 healthStages 배열 길이만큼 균등하게 나눠서 단계 인덱스를 구함
        // 예: 4단계면 100~76% -> 0, 75~51% -> 1, 50~26% -> 2, 25~0% -> 3
        int stageIndex = Mathf.FloorToInt((1f - ratio) * healthStages.Length);
        stageIndex = Mathf.Clamp(stageIndex, 0, healthStages.Length - 1);

        Sprite sprite = healthStages[stageIndex];
        _spriteRenderer.sprite = sprite;

        // 이미지 원본 크기/가로세로 비율이 제각각이어도 체감 크기가 비슷하도록, 면적(가로x세로의 제곱근) 기준으로 스케일 보정
        // 긴 변만 기준으로 하면 가늘고 긴 이미지가 실제보다 작아 보이는 문제가 있어서 면적 기준으로 변경
        float equivalentSide = Mathf.Sqrt(sprite.bounds.size.x * sprite.bounds.size.y);

        if (equivalentSide > 0f)
        {
            float sizeMultiplier = ObjectManager.Instance != null ? ObjectManager.Instance.CurrentObject.sizeMultiplier : 1f;
            if (sizeMultiplier <= 0f) sizeMultiplier = 1f; // 잘못된 값이 들어와도 최소한 안 보이게 되진 않도록

            float scale = (targetSize / equivalentSide) * sizeMultiplier;
            transform.localScale = Vector3.one * scale;
        }
    }

    // 현재 선택된 오브젝트(ObjectManager)의 스프라이트 단계를 우선 사용, 없으면 로컬 목록으로 대체
    private Sprite[] GetHealthStages()
    {
        if (ObjectManager.Instance != null)
            return ObjectManager.Instance.CurrentObject.healthStages;

        return fallbackHealthStages;
    }
}
