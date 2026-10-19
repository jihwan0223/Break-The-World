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
    private Collider2D _collider; // 자동클릭/더블클릭처럼 실제 마우스 클릭이 없는 히트에서도 타격 연출 위치로 씀

    [SerializeField] private int pieceReward = 1; // 이 오브젝트를 파괴했을 때 기본으로 지급되는 조각 개수
    [SerializeField] private float doubleClickDelaySeconds = 0.08f; // 더블클릭의 두 번째 타격이 첫 타격보다 이만큼 늦게 나옴
    [SerializeField] private bool swingOnSurface; // 켜두면 무기 타격 연출이 테두리가 아니라 오브젝트 면(안쪽) 아무 데나 나타남 - 책상처럼 화면을 크게 덮는 오브젝트용
    [SerializeField] private int fixedObjectIndex = -1; // -1이면 지금 장착 중인 오브젝트(가운데서 화살표로 스왑되는 것). 0 이상이면 그 인덱스 오브젝트 전용(해금돼서 옆에 놓인 것)

    // 이 오브젝트가 지금 안 보이는 존에 있는지 - 그런 존의 오브젝트도 자동클릭은 계속 돌지만 소리/연출/그림은 내지 않음
    private bool IsZoneHidden => ZoneManager.Instance != null && ZoneManager.Instance.IsHidden(transform);

    // 이 Click이 다루는 오브젝트 인덱스 - 고정이면 그 값, 아니면 지금 장착 중인 것
    private int ObjIndex => fixedObjectIndex >= 0 ? fixedObjectIndex
        : (ObjectManager.Instance != null ? ObjectManager.Instance.EquippedIndex : -1);

    [SerializeField] private Sprite autoClickProjectile; // 채워두면 자동클릭이 즉시 때리는 대신 이 그림이 날아와서 맞는 순간에 타격이 들어감 (유리컵의 야구공처럼)
    [SerializeField] private float projectileScale = 2.5f; // 투사체 그림 크기 배율 (스프라이트 원래 크기 기준)
    [SerializeField] private float projectileFlightSeconds = 2f; // 타격 몇 초 전부터 날아오기 시작할지(초) - 자동클릭 주기보다 길면 주기만큼만 날아옴
    [SerializeField] private float projectileSpinDegreesPerSecond = 720f; // 날아오는 동안 도는 속도
    [SerializeField] private float projectileStaggerSeconds = 0.1f; // 한 번에 여러 번 때릴 때 투사체 사이의 간격(초)
    [SerializeField, Range(0.1f, 1f)] private float projectileFastestRatio = 0.35f; // 가장 빠른 투사체의 비행 시간 비율 - 투사체마다 비행 시간이 (이 값 ~ 1) × 기본 시간 사이에서 랜덤. 1이면 전부 같은 속도

    private readonly List<GameObject> _flyingProjectiles = new List<GameObject>(); // 지금 날아가는 중인 투사체들 - 오브젝트가 꺼질 때 남지 않게 지우려고 기억
    private int _lastClickSoundIndex = -1; // 방금 재생한 사운드 인덱스 (바로 다음 클릭에서 같은 소리가 안 나오게 기억)
    private bool _projectilesLaunched; // 이번 자동클릭 주기에 투사체를 이미 날렸는지
    private float _autoClickTimer; // 자동클릭 업그레이드의 다음 발동까지 누적된 시간(초)
    private float _autoMineTimer; // 자동채굴 업그레이드의 다음 발동까지 누적된 시간(초)
    private float _hoverHitCooldown; // 커서 타격을 다시 넣을 수 있을 때까지 남은 시간(초) - 0 이하면 바로 때릴 수 있음
    private bool _isTerrain; // 존 전체를 부수는 지형(책상/도로/아파트)인지 - 커서 타격 대상에서 뺌
    private WaxCoating _wax; // 이 오브젝트를 덮은 왁스 (맞을 때 튀는 부스러기 색을 왁스 색으로)

    void Start()
    {
        _health = GetComponent<Health>();
        _audioSource = GetComponent<AudioSource>();
        _collider = GetComponent<Collider2D>();
        _isTerrain = GetComponent<ZoneBreaker>() != null;
        _wax = GetComponent<WaxCoating>(); // HealthSpriteSwitcher가 붙여둠 - 없으면(그림 단계가 없는 오브젝트) 여기서 붙임
        if (_wax == null && GetComponent<SpriteRenderer>() != null) _wax = gameObject.AddComponent<WaxCoating>();
        _health.OnDied += HandleDied;

        // 맞을 때 움찔하는 연출이 안 붙어 있는 오브젝트(책상 등)에도 붙여서, 어떤 오브젝트든 맞는 게 보이게 함
        if (GetComponent<HitFeedback>() == null && GetComponent<SpriteRenderer>() != null)
            gameObject.AddComponent<HitFeedback>();
    }

    void OnDisable()
    {
        // 존이 바뀌거나 잠겨서 코루틴이 멈추면 날아가던 투사체가 화면에 남으므로 같이 지움
        foreach (GameObject projectile in _flyingProjectiles)
            if (projectile != null) Destroy(projectile);
        _flyingProjectiles.Clear();
        _projectilesLaunched = false; // 코루틴이 멈췄으니 다음에 켜지면 다시 날리게 함
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제 (오브젝트가 파괴될 때 CurrencyManager 쪽 참조가 남지 않도록)
        // Start()가 실행되기 전에 파괴되는 경우(비활성 상태로 있다가 파괴 등) _health가 아직 null일 수 있음
        if (_health != null)
            _health.OnDied -= HandleDied;
    }

    public int ObjectIndex => ObjIndex; // 이 오브젝트의 번호 (ZoneBreaker가 자기 업그레이드를 찾을 때 씀)

    // 존 전체 파괴(ZoneBreaker)용 - 남은 체력만큼 한 번에 깎아 부숨. 보고 있는 존이면 보상이 파편으로 떨어짐
    public void BreakByZone()
    {
        _health.TakeDamage(_health.CurrentHP);
    }

    private void HandleDied()
    {
        // 바로 아래에서 ObjectManager.Instance도 참조하니 둘 다 확인해야 함 (CurrencyManager만 확인하면 널 참조 위험)
        if (CurrencyManager.Instance == null || ObjectManager.Instance == null)
            return;

        int objectIndex = ObjIndex; // 방금 파괴된 오브젝트 (가운데 스왑 오브젝트면 장착 중인 것, 고정이면 그 인덱스)
        if (objectIndex < 0) return;
        int weaponIndex = WeaponManager.Instance != null ? WeaponManager.Instance.EquippedIndex : -1;

        // 오브젝트 티어에 따라 기하급수적으로 커지는 기본 보상 + "획득량 증가"(ObjectManager) 보너스 + 콤보 배율
        long baseReward = ObjectManager.Instance.GetBaseReward(objectIndex) * pieceReward;
        long gainBonus = ObjectManager.Instance.GetGainBonus(objectIndex);
        float comboMultiplier = ComboManager.Instance != null ? ComboManager.Instance.ShardMultiplier : 1f;

        // 업그레이드: 파편 획득 배율(전역), 오브젝트 드랍량 강화(개수), 무기별 처치 보너스 파편
        float upgradeMultiplier = 1f;
        long weaponKillBonus = 0;
        double objectBonus = 0; // 이 오브젝트 드랍량 강화로 더 얻는 조각 수
        if (UpgradeManager.Instance != null)
        {
            upgradeMultiplier = UpgradeManager.Instance.PieceGainMultiplier();
            weaponKillBonus = UpgradeManager.Instance.WeaponKillBonusPieces(weaponIndex);
            objectBonus = UpgradeManager.Instance.ObjectPieceBonus(objectIndex, baseReward);
        }

        // 후반 티어는 값이 int 범위를 넘어설 수 있어서 double로 계산함 (Mathf.RoundToInt는 int라 오버플로됨).
        // 드랍량 강화는 전역 배율과 서로 곱해지지 않고 더해짐 (예전에 둘 다 %였을 때와 같은 결과)
        double rawTotal = ((baseReward + gainBonus + weaponKillBonus) * (double)upgradeMultiplier + objectBonus) * comboMultiplier;
        // 소수 부분은 반올림으로 버리지 않고 그만큼의 확률로 1개를 더 줌 - 평균은 계산값과 정확히 같음
        long finalPieces = (long)System.Math.Floor(rawTotal);
        if (Random.value < rawTotal - finalPieces)
            finalPieces++;
        finalPieces = System.Math.Max(1L, finalPieces);

        // 오브젝트별 "확률적 2배 드랍" 업그레이드
        if (UpgradeManager.Instance != null && Random.value < UpgradeManager.Instance.ObjectDoubleDropChance(objectIndex))
            finalPieces *= 2;

        // 부서지면(직접 클릭이든 자동클릭이든, 보는 존이든 아니든) 그 오브젝트가 있는 존 바닥에 파편으로 떨어뜨려 주워야 돈이 들어옴.
        // 안 보이는 존 바닥에 떨어진 건 그 존으로 가면 보이고, 자동 줍기는 안 보이는 존도 같이 쓺
        bool dropOnFloor = DebrisPool.Instance != null; // 파편으로 떨어뜨릴지 (false면 바로 지급)
        int zone = ZoneManager.Instance != null ? ZoneManager.Instance.ZoneOf(transform) : -1; // 파편이 떨어질 존 (-1이면 보고 있는 존)
        if (dropOnFloor)
            DebrisPool.Instance.DropPieces(transform.position, objectIndex, finalPieces, zone);
        else
            CurrencyManager.Instance.AddPieces(finalPieces);

        // 확률로 결정이 나옴 - 조각과 같은 규칙으로 그 존 바닥에 떨어뜨림.
        // 결정을 아직 한 번도 못 얻었고 조각을 충분히 모았으면 첫 결정은 확정으로 나옴 (바닥에 떨어뜨릴 수 있을 때만).
        // 천장: 정해진 횟수만큼 부수는 동안 결정이 안 나왔으면 이번에 확정으로 나옴
        bool starterCrystal = dropOnFloor && CurrencyManager.Instance.TakeStarterCrystal(); // 이번이 첫 결정 확정 드랍인지
        bool pityCrystal = CurrencyManager.Instance.CountKillForPity(); // 천장에 닿았는지
        if (UpgradeManager.Instance != null && (starterCrystal || pityCrystal || Random.value < UpgradeManager.Instance.CrystalDropChance))
        {
            CurrencyManager.Instance.ResetCrystalPity();
            int crystals = UpgradeManager.Instance.CrystalDropAmount; // 이번에 나온 결정 개수
            if (dropOnFloor)
                DebrisPool.Instance.DropCrystal(transform.position, crystals, zone);
            else
                CurrencyManager.Instance.AddCrystals(crystals);
        }
    }

    // 현재 선택된 오브젝트(ObjectManager)의 clickSounds 중 하나를 랜덤 재생하되,
    // 바로 직전에 재생한 것과는 겹치지 않게 고름
    private void PlayRandomClickSound()
    {
        if (ObjectManager.Instance == null)
            return;

        int idx = ObjIndex;
        if (idx < 0 || idx >= ObjectManager.StaticObjectCount) return;
        AudioClip[] clickSounds = ObjectManager.Instance.GetObjectAt(idx).clickSounds;

        if (clickSounds == null || clickSounds.Length == 0)
            return;

        int index = Random.Range(0, clickSounds.Length);

        // 후보가 2개 이상인데 방금 재생한 것과 같은 게 뽑혔다면 바로 다음 번호로 넘겨서 회피
        if (clickSounds.Length > 1 && index == _lastClickSoundIndex)
            index = (index + 1) % clickSounds.Length;

        _lastClickSoundIndex = index;
        SfxPlayer.Play(_audioSource, clickSounds[index]);
    }

    // 클릭 한 번(플레이어 클릭/자동클릭/더블클릭 추가 타격 공용)의 데미지 계산 + 적용 + 연출.
    // 이미 죽어서 리스폰을 기다리는 중이면 아무것도 하지 않음 (더블클릭/자동클릭이 중복으로 때리는 걸 방지)
    private void PerformClickHit(bool playWeaponSwing = true, Vector2? swingPoint = null)
    {
        // 바로 부활형 오브젝트가 페이드아웃/부활 대기 중이면, 이 클릭이 씹히지 않게 즉시 되살림
        if (_health.IsDead)
        {
            _health.ForceRespawnNow();
            if (_health.IsDead) return; // 대기 시간이 있는 오브젝트는 그대로 스킵
        }

        // 고정 데미지 1 대신 현재 장착한 무기의 클릭 데미지를 적용
        int baseDamage = WeaponManager.Instance != null ? WeaponManager.Instance.CurrentClickDamage : 1;

        // 클릭 데미지 업그레이드 보너스를 더함 (전역 + 지금 장착한 무기 전용 강화 합산)
        int equippedWeaponIndex = WeaponManager.Instance != null ? WeaponManager.Instance.EquippedIndex : -1;
        int clickDamageBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.GetClickDamageBonus(equippedWeaponIndex) : 0;
        int damage = baseDamage + clickDamageBonus;

        // 크리티컬 확률 판정 - 성공하면 크리티컬 배율을 곱함
        bool isCrit = UpgradeManager.Instance != null && Random.value < UpgradeManager.Instance.CritChanceValue;
        if (isCrit)
            damage = Mathf.RoundToInt(damage * UpgradeManager.Instance.CritMultiplierValue);

        // 럭키 클릭 - 당첨되면 데미지가 모자라도 즉시 파괴. 원래 데미지가 남은 체력보다 크면 그대로 둠
        // (남은 체력으로 깎아버리면 뜨는 숫자가 무기 데미지가 아니라 체력만큼만 나옴)
        bool isLucky = UpgradeManager.Instance != null && Random.value < UpgradeManager.Instance.LuckyClickChance;
        if (isLucky)
            damage = Mathf.Max(damage, _health.CurrentHP);

        // 이번 타격으로 죽는 게 아닐 때만 클릭 사운드 재생 (죽을 땐 파괴 사운드만 나오게)
        bool willDie = damage >= _health.CurrentHP;
        if (!willDie && !IsZoneHidden)
            PlayRandomClickSound();

        bool wasAlive = _health.CurrentHP > 0; // 이미 부서져 있던 걸 때린 게 아닌지
        _health.TakeDamage(damage);

        // 맞은 자리에 부스러기 + 데미지 숫자 - 체력이 많은 오브젝트도 맞고 있다는 게 보이게.
        // 숫자는 남은 체력으로 자르지 않고 때린 데미지 그대로 보여줌
        if (!IsZoneHidden && _collider != null && wasAlive && damage > 0)
        {
            Bounds bounds = _collider.bounds; // 맞은 자리를 정할 오브젝트 범위
            Vector2 hitPoint = swingPoint ?? (Vector2)bounds.center + Vector2.Scale(Random.insideUnitCircle, bounds.extents) * 0.5f; // 커서로 때렸으면 그 자리, 아니면 오브젝트 안쪽 아무 데나
            int objectIndex = ObjIndex; // 부스러기 색을 정할 오브젝트 번호
            Color chipColor = ObjectManager.Instance != null && objectIndex >= 0 && objectIndex < ObjectManager.StaticObjectCount
                ? ObjectManager.Instance.GetObjectAt(objectIndex).pileColor : Color.white; // 그 오브젝트의 파편 색과 같은 색
            if (_wax != null && _wax.TryGetChipColor(out Color waxColor)) chipColor = waxColor; // 왁스로 덮여 있으면 왁스 부스러기가 튐
            HitEffects.Play(hitPoint, damage, isCrit || isLucky, chipColor);
            if (_wax != null) _wax.Hit(hitPoint, (float)damage / Mathf.Max(1, _health.MaxHP)); // 맞은 자리에서 왁스에 금이 뻗어나감
        }

        // 콜라이더 테두리 위 랜덤한 지점에 현재 무기 이미지로 타격 연출 재생
        if (playWeaponSwing && !IsZoneHidden && WeaponManager.Instance != null && _collider != null)
            WeaponSwingEffect.Instance?.PlaySwing(_collider, WeaponManager.Instance.CurrentWeapon.icon, swingOnSurface, swingPoint);
    }

    // 자동클릭 업그레이드가 켜져있으면 일정 주기마다 자동으로 PerformClickHit을 호출.
    // 자동클릭은 오브젝트별로 따로 설정하는 거라, 지금 장착 중인 오브젝트를 대상으로 하는 노드가 있을 때만 작동함
    private void UpdateAutoClick()
    {
        int objectIndex = ObjIndex;
        if (UpgradeManager.Instance == null || objectIndex < 0 || !UpgradeManager.Instance.AutoClickIsUnlockedFor(objectIndex))
            return;

        _autoClickTimer += Time.deltaTime;
        float interval = UpgradeManager.Instance.AutoClickIntervalSecondsFor(objectIndex);
        int clicks = UpgradeManager.Instance.AutoClickClicksPerTriggerFor(objectIndex);

        // 투사체는 타격 시각보다 flight초 먼저 날려서, 남은 시간 동안 날아와 타격 순간에 딱 맞게 도착시킴
        if (autoClickProjectile != null && !_projectilesLaunched && !IsZoneHidden && _autoClickTimer >= interval - Mathf.Min(projectileFlightSeconds, interval))
        {
            _projectilesLaunched = true;
            float remaining = Mathf.Max(interval - _autoClickTimer, 0.05f); // 타격까지 남은 시간 = 이번 비행 시간
            // 투사체마다 비행 시간을 다르게 뽑아서 어떤 건 빠르게, 어떤 건 느리게 날아옴 (가장 느린 게 원래 속도라 타격이 늦어지진 않음)
            for (int i = 0; i < clicks; i++)
                StartCoroutine(FlyProjectileThenHit(i * projectileStaggerSeconds, remaining * Random.Range(projectileFastestRatio, 1f)));
        }

        if (_autoClickTimer < interval)
            return;

        _autoClickTimer -= interval; // 0으로 딱 자르지 않고 남은 오차만 빼서 주기가 조금씩 밀리는 걸 방지

        bool projectilesFlying = _projectilesLaunched; // 이번 주기에 투사체를 날렸으면 타격은 투사체가 도착하면서 넣음 (안 보이는 존이면 날리지 않고 여기서 바로 타격)
        _projectilesLaunched = false;
        if (projectilesFlying)
            return;

        for (int i = 0; i < clicks; i++)
            PerformClickHit();
    }

    // 화면 옆 밖에서 투사체가 날아와 오브젝트에 맞는 순간 타격을 넣음. 무기 타격 이미지는 대신 투사체라 안 나옴
    private IEnumerator FlyProjectileThenHit(float startDelaySeconds, float flightSeconds)
    {
        if (startDelaySeconds > 0f)
            yield return new WaitForSeconds(startDelaySeconds);

        Camera cam = Camera.main; // 화면 밖 시작점을 구하려고 필요
        if (cam == null || _collider == null)
        {
            PerformClickHit();
            yield break;
        }

        Bounds bounds = _collider.bounds; // 맞출 오브젝트의 범위 - 이 안의 한 점을 노림
        float side = Random.value < 0.5f ? -1f : 1f; // 왼쪽/오른쪽 중 어느 쪽에서 날아올지
        float halfWidth = cam.orthographicSize * cam.aspect; // 화면 가로 절반(월드 유닛)
        Vector3 start = new Vector3(cam.transform.position.x + side * (halfWidth + 1f),
            bounds.center.y + bounds.extents.y * Random.Range(0.2f, 0.9f), transform.position.z); // 화면 밖 오른쪽/왼쪽, 오브젝트 위쪽 높이
        Vector3 end = PickClearLandingPoint(bounds); // 오브젝트 안쪽 한 점 - 책상 위 접시처럼 다른 오브젝트가 놓인 자리는 피함

        var projectile = new GameObject("AutoClickProjectile"); // 날아가는 그림 하나
        var projectileRenderer = projectile.AddComponent<SpriteRenderer>(); // 투사체 그림을 그리는 렌더러
        projectileRenderer.sprite = autoClickProjectile;
        SpriteRenderer ownRenderer = GetComponent<SpriteRenderer>(); // 오브젝트 자신의 렌더러 - 그 위에 그리려고 정렬 순서를 참고
        projectileRenderer.sortingOrder = (ownRenderer != null ? ownRenderer.sortingOrder : 0) + 5;
        projectile.transform.localScale = Vector3.one * projectileScale;
        projectile.transform.position = start;
        _flyingProjectiles.Add(projectile);

        float elapsed = 0f; // 날아간 시간
        while (elapsed < flightSeconds)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / flightSeconds); // 0~1 진행도
            Vector3 position = Vector3.Lerp(start, end, progress);
            position.y += Mathf.Sin(progress * Mathf.PI) * 1.2f; // 살짝 포물선으로 뜨는 높이
            projectile.transform.position = position;
            projectileRenderer.enabled = !IsZoneHidden; // 날아가는 도중 다른 존으로 넘어가면 그림만 숨김 (타격은 그대로)
            projectile.transform.Rotate(0f, 0f, projectileSpinDegreesPerSecond * Time.deltaTime * -side);
            yield return null;
        }

        _flyingProjectiles.Remove(projectile);
        Destroy(projectile);
        PerformClickHit(false);
    }

    // 투사체가 착지할 자리를 오브젝트 안쪽에서 몇 번 뽑아보고, 자기 자신 말고 다른 오브젝트(책상 위 접시 등)가 없는 자리를 고름.
    // 몇 번을 시도해도 못 찾으면(그 자리가 전부 다른 오브젝트로 덮여있으면) 마지막으로 뽑은 자리를 그냥 씀
    private Vector3 PickClearLandingPoint(Bounds bounds)
    {
        const int maxAttempts = 8; // 빈 자리를 찾을 때까지 다시 뽑아볼 횟수
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

    // 이 자리에 자기 자신(콜라이더) 말고 다른 오브젝트의 콜라이더가 있는지
    private bool IsOccupiedByOtherObject(Vector2 point)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(point);
        foreach (Collider2D hit in hits)
            if (hit != _collider) return true;
        return false;
    }

    // 자동채굴 업그레이드가 켜져있으면 일정 주기마다, 지금 캐는 오브젝트보다 몇 단계 전 오브젝트를 자동으로 캐서 조각을 지급함
    // (눈에 보이는 오브젝트/체력 시스템과는 무관하게, 뒤에서 조용히 조각만 채워주는 방식)
    private void UpdateAutoMine()
    {
        if (fixedObjectIndex >= 0) return; // 자동채굴은 가운데 스왑 오브젝트만 (해금돼서 옆에 놓인 오브젝트는 안 함)
        if (UpgradeManager.Instance == null || !UpgradeManager.Instance.AutoMineIsUnlocked)
            return;

        _autoMineTimer += Time.deltaTime;
        float interval = UpgradeManager.Instance.AutoMineIntervalSeconds;

        if (_autoMineTimer < interval)
            return;

        _autoMineTimer -= interval; // 0으로 딱 자르지 않고 남은 오차만 빼서 주기가 조금씩 밀리는 걸 방지

        if (ObjectManager.Instance == null || CurrencyManager.Instance == null)
            return;

        int targetIndex = ObjectManager.Instance.EquippedIndex - UpgradeManager.Instance.AutoMineTierOffset;

        // 그만큼 전 단계 오브젝트가 없거나(0 미만) 아직 해금 전이면 이번 틱은 아무 일도 안 일어남
        if (targetIndex < 0 || !ObjectManager.Instance.IsUnlocked(targetIndex))
            return;

        long gainBonus = ObjectManager.Instance.GetGainBonus(targetIndex); // 항상 0 이상이라 별도로 최솟값 보정 안 해도 됨
        long yieldBonus = UpgradeManager.Instance.AutoMineYieldBonus; // 자동채굴 획득량 강화 - 레벨당 기본 보상 1배씩 추가
        long amount = ObjectManager.Instance.GetBaseReward(targetIndex) * (1 + yieldBonus) + gainBonus;
        CurrencyManager.Instance.AddPieces(amount);
    }

    void Update()
    {
        UpdateAutoClick();
        UpdateAutoMine();

        if (Mouse.current == null)
            return;

        // 새 Input System 기반 마우스 좌클릭 감지
        bool pressed = Mouse.current.leftButton.wasPressedThisFrame; // 이번 프레임에 눌렀는지

        // 커서 타격(업그레이드로 해금) - 누르지 않아도 커서가 올라가 있으면 간격마다 클릭 한 번으로 침.
        // 지형(책상/도로/아파트)은 화면 전체라 직접 눌러야만 맞음
        _hoverHitCooldown -= Time.deltaTime;
        bool hover = !pressed && _hoverHitCooldown <= 0f && !_isTerrain && !IsZoneHidden
            && UpgradeManager.Instance != null && UpgradeManager.Instance.HoverClickIsUnlocked; // 이번 프레임에 커서 타격을 시도할지
        if (!pressed && !hover)
            return;

        // 포인터가 UI(무기/오브젝트 팝업 등) 위에 있거나, 첫 파편 튜토리얼 줌인 중이면 오브젝트 클릭 처리하지 않음
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
            PerformClickHit(true, _collider.ClosestPoint(worldPos)); // 타격 연출은 커서 위치에서, 커서 범위 가장자리에 걸쳐 맞았으면 커서에 가장 가까운 자리에서 나옴

            // 더블클릭 업그레이드 - 확률 판정 성공 시 살짝 늦게 두 번째 타격을 처리
            // (첫 타격에 죽었으면 지연된 PerformClickHit이 알아서 무시함 - IsDead 체크가 있음)
            if (UpgradeManager.Instance != null && Random.value < UpgradeManager.Instance.DoubleClickChanceValue)
                StartCoroutine(PerformDelayedDoubleClickHit());
        }
    }

    // 이번 클릭에 이 오브젝트가 맞는지.
    // 커서가 직접 가리키는(클릭한 점에서 맨 위에 그려진) 오브젝트는 항상 맞음 - 책상/도로 같은 지형도 직접 겨누면 맞음.
    // 커서 범위 업그레이드가 있으면 범위 원에 걸친 다른 오브젝트도 같이 맞음. 단, 지형은 화면 전체를 덮고 있어서
    // 원에 걸친 것만으로는 안 맞음 (안 그러면 소품을 누를 때마다 지형이 같이 맞게 됨)
    private bool IsClickTarget(Vector2 worldPos)
    {
        GameObject top = TopmostAt(worldPos); // 클릭한 점에서 맨 위에 그려진 것
        if (top == gameObject) return true;

        float radius = UpgradeManager.Instance != null ? UpgradeManager.Instance.CursorRadius : 0f; // 커서 범위 반지름
        if (radius <= 0f) return false; // 범위가 없으면 직접 가리킨 것만
        if (top != null && top.GetComponent<Click>() == null) return false; // 커서 바로 밑이 파편이면 파편 줍기가 우선
        if (GetComponent<ZoneBreaker>() != null) return false; // 지형은 직접 겨눴을 때만
        if (IsZoneHidden) return false; // 안 보이는 존의 오브젝트

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(worldPos, radius))
            if (hit == _collider) return true;
        return false;
    }

    private IEnumerator PerformDelayedDoubleClickHit()
    {
        yield return new WaitForSeconds(doubleClickDelaySeconds);
        PerformClickHit();
    }

    // 한 점에 겹친 콜라이더 중 화면상 가장 앞에 그려지는 오브젝트를 고름.
    // 책상처럼 화면 전체를 덮는 오브젝트가 그 위에 놓인 소품 클릭을 가로채지 않게 하려고 필요함
    private static GameObject TopmostAt(Vector2 worldPos)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos);

        GameObject best = null; // 지금까지 찾은 것 중 가장 앞에 있는 오브젝트
        int bestOrder = int.MinValue; // 그 오브젝트의 정렬 순서

        foreach (Collider2D hit in hits)
        {
            if (ZoneManager.Instance != null && ZoneManager.Instance.IsHidden(hit.transform))
                continue; // 안 보이는 존의 오브젝트는 클릭을 받지 않음

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
