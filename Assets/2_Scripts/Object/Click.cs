using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(AudioSource))]
public class Click : MonoBehaviour
{
    private Health _health;
    private AudioSource _audioSource;
    private Collider2D _collider;

    [SerializeField] private int pieceReward = 1;
    [SerializeField] private float doubleClickDelaySeconds = 0.08f; // 초
    [SerializeField] private bool swingOnSurface;
    [SerializeField] private int fixedObjectIndex = -1; // -1 = 장착 중인 오브젝트

    private bool IsZoneHidden => ZoneManager.Instance != null && ZoneManager.Instance.IsHidden(transform);

    private int ObjIndex => fixedObjectIndex >= 0 ? fixedObjectIndex
        : (ObjectManager.Instance != null ? ObjectManager.Instance.EquippedIndex : -1);

    [SerializeField] private float projectileWorldSize = 0.9f; // 월드 유닛
    private const float SuperCritMultiplier = 2f;
    private const float AutoClickDamageRatio = 1f; // 직접 클릭 데미지 대비
    [SerializeField] private float projectileFlightSeconds = 2f; // 초
    [SerializeField] private float projectileSpinDegreesPerSecond = 720f; // 도/초
    [SerializeField] private float projectileStaggerSeconds = 0.1f; // 초
    [SerializeField, Range(0.1f, 1f)] private float projectileFastestRatio = 0.35f; // 가장 빠른 투사체의 비행 시간 비율

    private readonly List<GameObject> _flyingProjectiles = new List<GameObject>();
    private int _lastClickSoundIndex = -1;
    private bool _projectilesLaunched;
    private float _autoClickTimer;
    private float _autoMineTimer;
    private float _hoverHitCooldown;
    private bool _isTerrain;
    private ZoneBreaker _boss;
    private WaxCoating _wax;

    private const float StreakWindow = 0.45f; // 초
    private const float StreakPitchStep = 0.025f;
    private const int StreakMaxSteps = 12;
    private static int _streak;
    private static float _lastStreakTime = -1f; // unscaled
    private static int _lastStreakFrame = -1; // 커서 범위로 여러 개를 때려도 한 번만 셈

    void Start()
    {
        _health = GetComponent<Health>();
        _audioSource = GetComponent<AudioSource>();
        _collider = GetComponent<Collider2D>();
        _boss = GetComponent<ZoneBreaker>();
        _isTerrain = _boss != null;
        _wax = GetComponent<WaxCoating>();
        if (_wax == null && GetComponent<SpriteRenderer>() != null) _wax = gameObject.AddComponent<WaxCoating>();
        _health.OnDied += HandleDied;

        if (GetComponent<HitFeedback>() == null && GetComponent<SpriteRenderer>() != null)
            gameObject.AddComponent<HitFeedback>();
    }

    void OnDisable()
    {
        foreach (GameObject projectile in _flyingProjectiles)
            if (projectile != null) Destroy(projectile);
        _flyingProjectiles.Clear();
        _projectilesLaunched = false;
    }

    void OnDestroy()
    {
        if (_health != null)
            _health.OnDied -= HandleDied;
    }

    public int ObjectIndex => ObjIndex;

    public void BreakByZone()
    {
        _health.TakeDamage(_health.CurrentHP);
    }

    private void HandleDied()
    {
        if (CurrencyManager.Instance == null || ObjectManager.Instance == null)
            return;

        GameStats.CountBroken(ObjIndex);
        if (ZoneBreaker.IsShieldObject(_health)) return;

        int objectIndex = ObjIndex;
        if (objectIndex < 0) return;
        int weaponIndex = WeaponManager.Instance != null ? WeaponManager.Instance.EquippedIndex : -1;

        long baseReward = ObjectManager.Instance.GetBaseReward(objectIndex) * pieceReward;
        long gainBonus = ObjectManager.Instance.GetGainBonus(objectIndex);

        float upgradeMultiplier = 1f;
        long weaponKillBonus = 0;
        double objectBonus = 0;
        if (UpgradeManager.Instance != null)
        {
            upgradeMultiplier = UpgradeManager.Instance.PieceGainMultiplier();
            weaponKillBonus = UpgradeManager.Instance.WeaponKillBonusPieces(weaponIndex);
            objectBonus = UpgradeManager.Instance.ObjectPieceBonus(objectIndex, baseReward);
        }

        // int 범위를 넘을 수 있어 double로 계산
        double rawTotal = ((baseReward + gainBonus + weaponKillBonus) * (double)upgradeMultiplier + objectBonus);
        // 소수 부분은 그 확률로 1개 더 - 평균은 정확히 같음
        long finalPieces = (long)System.Math.Floor(rawTotal);
        if (Random.value < rawTotal - finalPieces)
            finalPieces++;
        finalPieces = System.Math.Max(1L, finalPieces);

        if (UpgradeManager.Instance != null && Random.value < UpgradeManager.Instance.ObjectDoubleDropChance(objectIndex))
            finalPieces *= 2;

        bool dropOnFloor = DebrisPool.Instance != null;
        int zone = ZoneManager.Instance != null ? ZoneManager.Instance.ZoneOf(transform) : -1;
        if (dropOnFloor)
            DebrisPool.Instance.DropPieces(transform.position, objectIndex, finalPieces, zone);
        else
            CurrencyManager.Instance.AddPieces(finalPieces);

        bool starterCrystal = dropOnFloor && CurrencyManager.Instance.TakeStarterCrystal();
        bool pityCrystal = CurrencyManager.Instance.CountKillForPity();
        if (UpgradeManager.Instance != null && (starterCrystal || pityCrystal || Random.value < UpgradeManager.Instance.CrystalDropChance))
        {
            CurrencyManager.Instance.ResetCrystalPity();
            long crystals = UpgradeManager.Instance.CrystalDropAmountIn(zone);
            if (dropOnFloor)
                DebrisPool.Instance.DropCrystal(transform.position, crystals, zone);
            else
                CurrencyManager.Instance.AddCrystals(crystals);
        }
    }

    private static float NextStreakPitch()
    {
        float now = Time.unscaledTime;
        if (Time.frameCount != _lastStreakFrame)
        {
            bool continued = now >= _lastStreakTime && now - _lastStreakTime <= StreakWindow; // 재시작하면 시각이 0부터라 now가 더 작을 수 있음
            _streak = continued ? Mathf.Min(_streak + 1, StreakMaxSteps) : 0;
            _lastStreakFrame = Time.frameCount;
        }
        _lastStreakTime = now;
        return 1f + _streak * StreakPitchStep;
    }

    private void PlayRandomClickSound(float pitch)
    {
        if (ObjectManager.Instance == null)
            return;

        int idx = ObjIndex;
        if (idx < 0 || idx >= ObjectManager.StaticObjectCount) return;
        AudioClip[] clickSounds = ObjectManager.Instance.GetObjectAt(idx).clickSounds;

        if (clickSounds == null || clickSounds.Length == 0)
            return;

        int index = Random.Range(0, clickSounds.Length);

        if (clickSounds.Length > 1 && index == _lastClickSoundIndex)
            index = (index + 1) % clickSounds.Length;

        _lastClickSoundIndex = index;
        SfxPlayer.Play(_audioSource, clickSounds[index], pitch: pitch);
    }

    private void PerformClickHit(bool playWeaponSwing = true, Vector2? swingPoint = null, bool playerClick = false, bool autoClick = false)
    {
        if (_boss != null && !_boss.BossAllowsDamage) return;

        // 바로 부활형이 페이드 중이면 클릭이 씹히지 않게 되살림
        if (_health.IsDead)
        {
            _health.ForceRespawnNow();
            if (_health.IsDead) return;
        }

        int baseDamage = WeaponManager.Instance != null ? WeaponManager.Instance.CurrentClickDamage : 1;

        int equippedWeaponIndex = WeaponManager.Instance != null ? WeaponManager.Instance.EquippedIndex : -1;
        int clickDamageBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.GetClickDamageBonus(equippedWeaponIndex) : 0;
        int damage = baseDamage + clickDamageBonus;

        bool isSuper = UpgradeManager.Instance != null && Random.value < UpgradeManager.Instance.LuckyClickChance;
        bool isCrit = !isSuper && UpgradeManager.Instance != null && Random.value < UpgradeManager.Instance.CritChanceValue;
        if (isSuper)
            damage = Mathf.RoundToInt(damage * UpgradeManager.Instance.CritMultiplierValue * SuperCritMultiplier);
        else if (isCrit)
            damage = Mathf.RoundToInt(damage * UpgradeManager.Instance.CritMultiplierValue);
        if (playerClick) GameStats.Clicks++;
        if (isSuper) GameStats.SuperCrits++;
        else if (isCrit) GameStats.Crits++;
        if (autoClick)
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * AutoClickDamageRatio));

        bool bossFight = _boss != null && _boss.BossActive;
        bool shielded = bossFight && _boss.ShieldRatio > 0f;
        if (shielded) damage = Mathf.Max(1, Mathf.FloorToInt(damage * _boss.BossDamageMultiplier)); // 내림이라 작은 데미지도 줄어든 게 보임

        bool willDie = damage >= _health.CurrentHP;
        bool hasBreakSound = ObjectManager.Instance != null && ObjIndex >= 0 && ObjIndex < ObjectManager.StaticObjectCount
            && ObjectManager.Instance.GetObjectAt(ObjIndex).breakSound != null;
        float pitch = playerClick ? NextStreakPitch() : 1f;
        if ((!willDie || !hasBreakSound) && !IsZoneHidden)
            PlayRandomClickSound(pitch);

        bool wasAlive = _health.CurrentHP > 0;
        _health.TakeDamage(damage);

        if (!IsZoneHidden && _collider != null && wasAlive && damage > 0)
        {
            Bounds bounds = _collider.bounds;
            Vector2 hitPoint = swingPoint ?? (Vector2)bounds.center + Vector2.Scale(Random.insideUnitCircle, bounds.extents) * 0.5f;
            int objectIndex = ObjIndex;
            Color chipColor = ObjectManager.Instance != null && objectIndex >= 0 && objectIndex < ObjectManager.StaticObjectCount
                ? ObjectManager.Instance.GetObjectAt(objectIndex).pileColor : Color.white;
            if (_wax != null && _wax.TryGetChipColor(out Color waxColor)) chipColor = waxColor;
            HitEffects.Play(hitPoint, damage, isCrit || isSuper, chipColor, shielded, isSuper);
            if (_wax != null) _wax.Hit(hitPoint, (float)damage / Mathf.Max(1, _health.MaxHP));
        }

        if (playWeaponSwing && !IsZoneHidden && WeaponManager.Instance != null && _collider != null)
            WeaponSwingEffect.Instance?.PlaySwing(_collider, WeaponManager.Instance.CurrentWeapon.icon, swingOnSurface, swingPoint);
    }

    private void UpdateAutoClick()
    {
        if (ZoneBreaker.ActiveBoss != null) return;

        int objectIndex = ObjIndex;
        if (UpgradeManager.Instance == null || objectIndex < 0 || !UpgradeManager.Instance.AutoClickIsUnlockedFor(objectIndex))
            return;

        _autoClickTimer += Time.deltaTime;
        float interval = UpgradeManager.Instance.AutoClickIntervalSecondsFor(objectIndex);
        int clicks = UpgradeManager.Instance.AutoClickClicksPerTriggerFor(objectIndex);

        if (ProjectileSprite != null && !_projectilesLaunched && !IsZoneHidden && _autoClickTimer >= interval - Mathf.Min(projectileFlightSeconds, interval))
        {
            _projectilesLaunched = true;
            float remaining = Mathf.Max(interval - _autoClickTimer, 0.05f);
            for (int i = 0; i < clicks; i++)
                StartCoroutine(FlyProjectileThenHit(i * projectileStaggerSeconds, remaining * Random.Range(projectileFastestRatio, 1f)));
        }

        if (_autoClickTimer < interval)
            return;

        _autoClickTimer -= interval; // 남은 오차만 빼서 주기가 밀리지 않게

        bool projectilesFlying = _projectilesLaunched;
        _projectilesLaunched = false;
        if (projectilesFlying)
            return;

        for (int i = 0; i < clicks; i++)
            PerformClickHit(autoClick: true);
    }

    private static Sprite ProjectileSprite => WeaponManager.Instance != null ? WeaponManager.Instance.CurrentWeapon.icon : null;

    private IEnumerator FlyProjectileThenHit(float startDelaySeconds, float flightSeconds)
    {
        if (startDelaySeconds > 0f)
            yield return new WaitForSeconds(startDelaySeconds);

        Camera cam = Camera.main;
        Sprite sprite = ProjectileSprite;
        if (cam == null || _collider == null || sprite == null)
        {
            PerformClickHit(autoClick: true);
            yield break;
        }

        Bounds bounds = _collider.bounds;
        float side = Random.value < 0.5f ? -1f : 1f;
        float halfWidth = cam.orthographicSize * cam.aspect;
        Vector3 start = new Vector3(cam.transform.position.x + side * (halfWidth + 1f),
            bounds.center.y + bounds.extents.y * Random.Range(0.2f, 0.9f), transform.position.z);
        Vector3 end = PickClearLandingPoint(bounds);

        var projectile = new GameObject("AutoClickProjectile");
        var projectileRenderer = projectile.AddComponent<SpriteRenderer>();
        projectileRenderer.sprite = sprite;
        SpriteRenderer ownRenderer = GetComponent<SpriteRenderer>();
        projectileRenderer.sortingOrder = (ownRenderer != null ? ownRenderer.sortingOrder : 0) + 5;
        projectile.transform.localScale = Vector3.one * (projectileWorldSize / Mathf.Max(0.01f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y)));
        projectile.transform.position = start;
        _flyingProjectiles.Add(projectile);

        float elapsed = 0f;
        while (elapsed < flightSeconds)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / flightSeconds);
            Vector3 position = Vector3.Lerp(start, end, progress);
            position.y += Mathf.Sin(progress * Mathf.PI) * 1.2f;
            projectile.transform.position = position;
            projectileRenderer.enabled = !IsZoneHidden;
            projectile.transform.Rotate(0f, 0f, projectileSpinDegreesPerSecond * Time.deltaTime * -side);
            yield return null;
        }

        _flyingProjectiles.Remove(projectile);
        Destroy(projectile);
        if (ZoneBreaker.ActiveBoss == null) PerformClickHit(false, autoClick: true);
    }

    private Vector3 PickClearLandingPoint(Bounds bounds)
    {
        const int maxAttempts = 8;
        Vector3 candidate = bounds.center;

        for (int i = 0; i < maxAttempts; i++)
        {
            candidate = new Vector3(bounds.center.x + bounds.extents.x * Random.Range(-0.4f, 0.4f),
                bounds.center.y + bounds.extents.y * Random.Range(-0.4f, 0.4f), transform.position.z);

            if (!IsOccupiedByOtherObject(candidate))
                return candidate;
        }

        return candidate;
    }

    private bool IsOccupiedByOtherObject(Vector2 point)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(point);
        foreach (Collider2D hit in hits)
            if (hit != _collider) return true;
        return false;
    }

    private void UpdateAutoMine()
    {
        if (fixedObjectIndex >= 0) return;
        if (UpgradeManager.Instance == null || !UpgradeManager.Instance.AutoMineIsUnlocked)
            return;

        _autoMineTimer += Time.deltaTime;
        float interval = UpgradeManager.Instance.AutoMineIntervalSeconds;

        if (_autoMineTimer < interval)
            return;

        _autoMineTimer -= interval; // 남은 오차만 빼서 주기가 밀리지 않게

        if (ObjectManager.Instance == null || CurrencyManager.Instance == null)
            return;

        int targetIndex = ObjectManager.Instance.EquippedIndex - UpgradeManager.Instance.AutoMineTierOffset;

        if (targetIndex < 0 || !ObjectManager.Instance.IsUnlocked(targetIndex))
            return;

        long gainBonus = ObjectManager.Instance.GetGainBonus(targetIndex);
        long yieldBonus = UpgradeManager.Instance.AutoMineYieldBonus;
        long amount = ObjectManager.Instance.GetBaseReward(targetIndex) * (1 + yieldBonus) + gainBonus;
        CurrencyManager.Instance.AddPieces(amount);
    }

    void Update()
    {
        UpdateAutoClick();
        UpdateAutoMine();

        if (Mouse.current == null)
            return;

        bool pressed = Mouse.current.leftButton.wasPressedThisFrame;

        // 지형은 직접 눌러야만 맞음
        _hoverHitCooldown -= Time.deltaTime;
        bool hover = !pressed && _hoverHitCooldown <= 0f && !_isTerrain && !IsZoneHidden
            && ZoneBreaker.ActiveBoss == null
            && UpgradeManager.Instance != null && UpgradeManager.Instance.HoverClickIsUnlocked;
        if (!pressed && !hover)
            return;

        if (UIPointerGuard.IsPointerOverUI || PieceTutorial.Active)
            return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        if (IsClickTarget(worldPos))
        {
            if (hover)
                _hoverHitCooldown = UpgradeManager.Instance.HoverClickIntervalSeconds;
            else
                Debug.Log("Click");
            PerformClickHit(true, _collider.ClosestPoint(worldPos), !hover);

            // 첫 타격에 죽었으면 지연 타격은 IsDead로 무시됨
            if (UpgradeManager.Instance != null && Random.value < UpgradeManager.Instance.DoubleClickChanceValue)
                StartCoroutine(PerformDelayedDoubleClickHit());
        }
    }

    private bool IsClickTarget(Vector2 worldPos)
    {
        GameObject top = TopmostAt(worldPos);
        if (top == gameObject) return true;

        float radius = UpgradeManager.Instance != null ? UpgradeManager.Instance.CursorRadius : 0f;
        if (radius <= 0f) return false;
        if (top != null && top.GetComponent<Click>() == null) return false; // 커서 밑이 파편이면 줍기가 우선
        if (GetComponent<ZoneBreaker>() != null) return false; // 지형은 직접 겨눴을 때만
        if (IsZoneHidden) return false;

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(worldPos, radius))
            if (hit == _collider) return true;
        return false;
    }

    private IEnumerator PerformDelayedDoubleClickHit()
    {
        yield return new WaitForSeconds(doubleClickDelaySeconds);
        PerformClickHit();
    }

    // 큰 오브젝트가 위 소품 클릭을 가로채지 않게 가장 앞의 것을 고름
    private static GameObject TopmostAt(Vector2 worldPos)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);

        GameObject best = null;
        int bestOrder = int.MinValue;

        foreach (Collider2D hit in hits)
        {
            if (ZoneManager.Instance != null && ZoneManager.Instance.IsHidden(hit.transform))
                continue;

            SpriteRenderer renderer = hit.GetComponent<SpriteRenderer>();
            int order = renderer != null ? renderer.sortingOrder : 0;

            if (best == null || order > bestOrder)
            {
                best = hit.gameObject;
                bestOrder = order;
            }
        }

        return best;
    }
}
