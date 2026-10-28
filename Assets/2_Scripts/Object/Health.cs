using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Health : MonoBehaviour
{
    [SerializeField] private int weaponTier = 1; // 1부터
    [SerializeField] private int objectIndexInTier = 1; // 1부터
    [SerializeField] private float hpMultiplier = 1f;
    [SerializeField] private int fixedObjectIndex = -1; // -1 = 장착 중인 오브젝트
    [SerializeField] private float respawnDelay = 1f; // 초, 0 이하면 바로 부활
    [SerializeField] private float dieFadeDuration = 0.25f; // 초
    [SerializeField] private float respawnFadeDuration = 0.25f; // 초
    [SerializeField] private bool stayVisibleWhenDead; // 죽어도 사라지지 않음 (배경 오브젝트용)

    private int maxHP;

    private const float GlobalHpScale = 3f;

    private static readonly float[] MapHpScale = { 1f, 2f, 4f };

    public static int ComputeMaxHP(int tier, int indexInTier, float multiplier) =>
        Mathf.Max(1, Mathf.RoundToInt(ObjectHealthCalculator.Calculate(tier, indexInTier) * multiplier
            * MapHpScale[Mathf.Clamp((tier - 1) / 2, 0, MapHpScale.Length - 1)] * GlobalHpScale));

    public int MaxHP => maxHP;
    public int CurrentHP { get; private set; }
    public bool IsDead { get; private set; }

    public bool HasRespawnDelay => respawnDelay > 0f;

    public float RespawnRemaining { get; private set; }

    public event Action<int, int> OnDamaged;

    public event Action OnDied;

    public event Action OnRespawned;

    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;
    private AudioSource _audioSource;
    private Color _originalColor;

    void Awake()
    {
        maxHP = ComputeMaxHP(weaponTier, objectIndexInTier, hpMultiplier);
        CurrentHP = maxHP;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        _audioSource = GetComponent<AudioSource>();
        _originalColor = _spriteRenderer != null ? _spriteRenderer.color : Color.white;
    }

    public void ApplyObjectTier(int newWeaponTier, int newIndexInTier, float newHpMultiplier = 1f)
    {
        StopAllCoroutines();

        weaponTier = newWeaponTier;
        objectIndexInTier = newIndexInTier;
        hpMultiplier = newHpMultiplier;
        maxHP = ComputeMaxHP(weaponTier, objectIndexInTier, hpMultiplier);
        CurrentHP = maxHP;
        IsDead = false;
        RespawnRemaining = 0f;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = true;
            _spriteRenderer.color = _originalColor;
        }

        if (_collider != null) _collider.enabled = true;

        // HitFeedback이 커진 채 끊겼을 수 있어 크기 원복
        transform.localScale = Vector3.one;

        OnDamaged?.Invoke(CurrentHP, maxHP);
    }

    // UnlockGate가 꺼둔 오브젝트는 건드리지 않음
    public void ResetToFull()
    {
        if (!enabled) return;

        StopAllCoroutines();
        RespawnRemaining = 0f;
        CurrentHP = maxHP;
        IsDead = false;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = true;
            _spriteRenderer.color = _originalColor;
        }
        if (_collider != null) _collider.enabled = true;

        OnDamaged?.Invoke(CurrentHP, maxHP);
    }

    public void TakeDamage(int amount)
    {
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

        AudioClip breakSound = null;
        if (ObjectManager.Instance != null)
        {
            breakSound = (fixedObjectIndex >= 0 && fixedObjectIndex < ObjectManager.StaticObjectCount)
                ? ObjectManager.Instance.GetObjectAt(fixedObjectIndex).breakSound
                : ObjectManager.Instance.CurrentObject.breakSound;
        }
        if (breakSound != null && !(ZoneManager.Instance != null && ZoneManager.Instance.IsHidden(transform)))
            SfxPlayer.Play(_audioSource, breakSound, priority: true);

        // 바로 부활형은 콜라이더를 켜둬서 페이드 중 클릭도 맞게
        if (_collider != null && HasRespawnDelay) _collider.enabled = false;

        StartCoroutine(DieAndRespawnRoutine());
    }

    public void ForceRespawnNow()
    {
        if (!IsDead || HasRespawnDelay) return;

        StopAllCoroutines();
        RespawnRemaining = 0f;
        CurrentHP = maxHP;
        IsDead = false;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = true;
            _spriteRenderer.color = _originalColor;
        }
        if (_collider != null) _collider.enabled = true;

        OnDamaged?.Invoke(CurrentHP, maxHP);
        OnRespawned?.Invoke();
    }

    private IEnumerator DieAndRespawnRoutine()
    {
        if (!stayVisibleWhenDead)
        {
            yield return Fade(_originalColor.a, 0f, dieFadeDuration);

            if (_spriteRenderer != null) _spriteRenderer.enabled = false;
        }

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

        OnDamaged?.Invoke(CurrentHP, maxHP);

        if (!stayVisibleWhenDead)
            yield return Fade(0f, _originalColor.a, respawnFadeDuration);

        OnRespawned?.Invoke();
    }

    public void FadeIn(float duration)
    {
        if (_spriteRenderer == null || !isActiveAndEnabled) return;
        Color color = _originalColor;
        color.a = 0f;
        _spriteRenderer.color = color;
        StartCoroutine(Fade(0f, _originalColor.a, duration));
    }

    private IEnumerator Fade(float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

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
