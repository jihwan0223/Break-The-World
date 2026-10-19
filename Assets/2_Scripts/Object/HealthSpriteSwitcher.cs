using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Health))]
public class HealthSpriteSwitcher : MonoBehaviour
{
    // ObjectManager가 없을 때를 대비한 로컬 스프라이트 목록 (체력 100% -> 0% 순서)
    [SerializeField] private Sprite[] fallbackHealthStages;
    [SerializeField] private int fixedObjectIndex = -1; // -1이면 지금 장착 중인 오브젝트. 0 이상이면 그 오브젝트의 스프라이트 단계를 씀 (해금돼서 옆에 놓인 것)

    private SpriteRenderer _spriteRenderer;
    private Health _health;
    private WaxCoating _wax; // 오브젝트를 덮은 왁스 - 덮여 있는 동안은 오브젝트 그림을 깨진 단계로 안 바꿈 (끝에 한 번에 부서짐)

    void Awake()
    {
        // 모든 파괴 오브젝트에 왁스를 자동으로 붙임 (씬의 오브젝트마다 따로 넣지 않아도 되게). Click보다 먼저 붙도록 Awake에서
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
        // Start()가 실행되기 전에 파괴되는 경우(비활성 상태로 있다가 파괴 등) _health가 아직 null일 수 있음
        if (_health != null)
            _health.OnDamaged -= UpdateSprite;
    }

    // 스프라이트만 갈아끼움. 크기는 각 이미지의 Pixels Per Unit(Import Settings)으로 직접 맞출 것
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
        if (_wax != null && _wax.Covers) stageIndex = 0; // 왁스가 덮고 있으면 안의 오브젝트는 끝까지 멀쩡한 그림

        _spriteRenderer.sprite = healthStages[stageIndex];
    }

    // 현재 선택된 오브젝트(ObjectManager)의 스프라이트 단계를 우선 사용, 없으면 로컬 목록으로 대체
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
