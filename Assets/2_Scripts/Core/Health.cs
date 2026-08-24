using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Health : MonoBehaviour
{
    [SerializeField] private int weaponTier = 1; // 이 오브젝트를 부수는 데 필요한 무기 티어 (1부터 시작)
    [SerializeField] private int objectIndexInTier = 1; // 같은 무기 티어 내에서 이 오브젝트의 순번 (1부터 시작)
    [SerializeField] private float respawnDelay = 1f; // 죽고 나서 재생성까지 대기하는 시간(초)
    [SerializeField] private AudioClip breakSound; // 파괴될 때 재생할 사운드
    [SerializeField] private float dieFadeDuration = 0.25f; // 죽을 때 흐려지며 사라지는 연출 시간(초)
    [SerializeField] private float respawnFadeDuration = 0.25f; // 재생성될 때 서서히 나타나는 연출 시간(초)

    private int maxHP; // weaponTier/objectIndexInTier로부터 자동 계산된 최대 체력

    public int MaxHP => maxHP;
    public int CurrentHP { get; private set; }
    public bool IsDead { get; private set; }

    // 재생성까지 남은 시간 (죽은 상태가 아니면 0) - RespawnTimerUI가 매 프레임 읽어감
    public float RespawnRemaining { get; private set; }

    // 체력이 깎일 때마다 (현재체력, 최대체력)을 전달 - 체력바 UI, 스프라이트 전환 등이 구독
    public event Action<int, int> OnDamaged;

    // 체력이 0 이하가 되는 순간 한 번 발행 - 보상 지급 등이 구독
    public event Action OnDied;

    // 재생성이 끝나는 순간 한 번 발행
    public event Action OnRespawned;

    private SpriteRenderer _spriteRenderer; // 페이드 연출에 사용할 스프라이트 렌더러
    private Collider2D _collider; // 죽어있는 동안 클릭을 막기 위한 콜라이더
    private AudioSource _audioSource; // 파괴 사운드 재생용
    private Color _originalColor; // 페이드 애니메이션 후 되돌아갈 원래 색상(알파 포함)

    void Awake()
    {
        maxHP = ObjectHealthCalculator.Calculate(weaponTier, objectIndexInTier);
        CurrentHP = maxHP;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        _audioSource = GetComponent<AudioSource>();
        _originalColor = _spriteRenderer != null ? _spriteRenderer.color : Color.white;
    }

    public void TakeDamage(int amount)
    {
        // 죽어서 재생성을 기다리는 동안은 데미지를 받지 않음
        if (IsDead)
            return;

        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        OnDamaged?.Invoke(CurrentHP, maxHP);

        if (CurrentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        IsDead = true;
        OnDied?.Invoke();

        if (breakSound != null)
            _audioSource.PlayOneShot(breakSound);

        DebrisPool.Instance?.AddPiece();

        // 클릭은 즉시 막되, 화면에서는 서서히 사라지도록 연출 후 숨김
        if (_collider != null) _collider.enabled = false;

        StartCoroutine(DieAndRespawnRoutine());
    }

    private IEnumerator DieAndRespawnRoutine()
    {
        yield return Fade(_originalColor.a, 0f, dieFadeDuration);

        if (_spriteRenderer != null) _spriteRenderer.enabled = false;

        RespawnRemaining = respawnDelay;

        while (RespawnRemaining > 0f)
        {
            yield return null;
            RespawnRemaining -= Time.deltaTime;
        }

        RespawnRemaining = 0f;
        CurrentHP = maxHP;
        IsDead = false;

        // 다시 나타나기 전에 미리 활성화해야 페이드 인 애니메이션이 보임
        if (_spriteRenderer != null) _spriteRenderer.enabled = true;
        if (_collider != null) _collider.enabled = true;

        // 페이드 인이 시작되기 전에 먼저 스프라이트를 100% 상태로 되돌려야
        // 깨진 이미지가 아니라 원래(온전한) 이미지가 서서히 나타난다
        OnDamaged?.Invoke(CurrentHP, maxHP);

        yield return Fade(0f, _originalColor.a, respawnFadeDuration);

        OnRespawned?.Invoke();
    }

    // 지정한 시간 동안 스프라이트 알파값을 from -> to로 보간 (흐려지며 사라지고/나타나는 연출)
    private IEnumerator Fade(float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f; // 코루틴 시작 후 흐른 시간

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration); // 0~1 진행률

            if (_spriteRenderer != null)
            {
                Color color = _originalColor;
                color.a = Mathf.Lerp(fromAlpha, toAlpha, progress);
                _spriteRenderer.color = color;
            }

            yield return null;
        }

        if (_spriteRenderer != null)
        {
            Color color = _originalColor;
            color.a = toAlpha;
            _spriteRenderer.color = color;
        }
    }
}
