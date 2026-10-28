using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Health))]
public class HealthSpriteSwitcher : MonoBehaviour
{
    [SerializeField] private Sprite[] fallbackHealthStages;
    [SerializeField] private int fixedObjectIndex = -1; // -1 = 장착 중인 오브젝트

    private SpriteRenderer _spriteRenderer;
    private Health _health;
    private WaxCoating _wax;

    void Awake()
    {
        // Click보다 먼저 왁스를 붙이려고 Awake에서
        _wax = GetComponent<WaxCoating>();
        if (_wax == null) _wax = gameObject.AddComponent<WaxCoating>();
    }

    void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _health = GetComponent<Health>();

        _health.OnDamaged += UpdateSprite;
        UpdateSprite(_health.CurrentHP, _health.MaxHP);
    }

    void OnDestroy()
    {
        if (_health != null)
            _health.OnDamaged -= UpdateSprite;
    }

    // 크기는 이미지의 Pixels Per Unit으로 맞춤
    private void UpdateSprite(int current, int max)
    {
        Sprite[] healthStages = GetHealthStages();

        if (healthStages == null || healthStages.Length == 0 || max <= 0)
            return;

        float ratio = (float)current / max;

        int stageIndex = Mathf.FloorToInt((1f - ratio) * healthStages.Length);
        stageIndex = Mathf.Clamp(stageIndex, 0, healthStages.Length - 1);
        if (_wax != null && _wax.Covers) stageIndex = 0;

        _spriteRenderer.sprite = healthStages[stageIndex];
    }

    private Sprite[] GetHealthStages()
    {
        if (ObjectManager.Instance != null)
        {
            if (fixedObjectIndex >= 0 && fixedObjectIndex < ObjectManager.StaticObjectCount)
                return ObjectManager.Instance.GetObjectAt(fixedObjectIndex).healthStages;

            return ObjectManager.Instance.CurrentObject.healthStages;
        }

        return fallbackHealthStages;
    }
}
