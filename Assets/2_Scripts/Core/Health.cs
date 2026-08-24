using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Health : MonoBehaviour
{
    [SerializeField] private int maxHP = 10;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private AudioClip breakSound;

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

    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;
    private AudioSource _audioSource;

    void Awake()
    {
        CurrentHP = maxHP;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        _audioSource = GetComponent<AudioSource>();
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

        // 오브젝트를 Destroy하지 않고 잠시 숨겨서 재생성에 대비 (클릭도 안 되도록 콜라이더도 끔)
        if (_spriteRenderer != null) _spriteRenderer.enabled = false;
        if (_collider != null) _collider.enabled = false;

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        RespawnRemaining = respawnDelay;

        while (RespawnRemaining > 0f)
        {
            yield return null;
            RespawnRemaining -= Time.deltaTime;
        }

        RespawnRemaining = 0f;
        CurrentHP = maxHP;
        IsDead = false;

        if (_spriteRenderer != null) _spriteRenderer.enabled = true;
        if (_collider != null) _collider.enabled = true;

        // 체력/스프라이트가 100%로 되돌아왔음을 구독자들에게 알림
        OnDamaged?.Invoke(CurrentHP, maxHP);
        OnRespawned?.Invoke();
    }
}
