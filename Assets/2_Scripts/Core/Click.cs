using System.Collections;
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

    [SerializeField] private int shardReward = 1; // 이 오브젝트를 파괴했을 때 기본으로 지급되는 파편 개수
    [SerializeField] private float doubleClickDelaySeconds = 0.08f; // 더블클릭의 두 번째 타격이 첫 타격보다 이만큼 늦게 나옴

    private int _lastClickSoundIndex = -1; // 방금 재생한 사운드 인덱스 (바로 다음 클릭에서 같은 소리가 안 나오게 기억)
    private float _autoClickTimer; // 자동클릭 업그레이드의 다음 발동까지 누적된 시간(초)

    void Start()
    {
        _health = GetComponent<Health>();
        _audioSource = GetComponent<AudioSource>();
        _collider = GetComponent<Collider2D>();
        _health.OnDied += HandleDied;
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제 (오브젝트가 파괴될 때 CurrencyManager 쪽 참조가 남지 않도록)
        // Start()가 실행되기 전에 파괴되는 경우(비활성 상태로 있다가 파괴 등) _health가 아직 null일 수 있음
        if (_health != null)
            _health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        if (CurrencyManager.Instance == null)
            return;

        // 파편 강화(고정 보너스) + 파편 배율 + 콤보 배율을 순서대로 적용
        int shardBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.ShardGainBonus : 0;
        float shardMultiplier = UpgradeManager.Instance != null ? UpgradeManager.Instance.ShardMultiplierValue : 1f;
        float comboMultiplier = ComboManager.Instance != null ? ComboManager.Instance.ShardMultiplier : 1f;

        int finalShards = Mathf.Max(1, Mathf.RoundToInt((shardReward + shardBonus) * shardMultiplier * comboMultiplier));
        CurrencyManager.Instance.AddShards(finalShards);
    }

    // 현재 선택된 오브젝트(ObjectManager)의 clickSounds 중 하나를 랜덤 재생하되,
    // 바로 직전에 재생한 것과는 겹치지 않게 고름
    private void PlayRandomClickSound()
    {
        if (ObjectManager.Instance == null)
            return;

        AudioClip[] clickSounds = ObjectManager.Instance.CurrentObject.clickSounds;

        if (clickSounds == null || clickSounds.Length == 0)
            return;

        int index = Random.Range(0, clickSounds.Length);

        // 후보가 2개 이상인데 방금 재생한 것과 같은 게 뽑혔다면 바로 다음 번호로 넘겨서 회피
        if (clickSounds.Length > 1 && index == _lastClickSoundIndex)
            index = (index + 1) % clickSounds.Length;

        _lastClickSoundIndex = index;
        _audioSource.PlayOneShot(clickSounds[index]);
    }

    // 클릭 한 번(플레이어 클릭/자동클릭/더블클릭 추가 타격 공용)의 데미지 계산 + 적용 + 연출.
    // 이미 죽어서 리스폰을 기다리는 중이면 아무것도 하지 않음 (더블클릭/자동클릭이 중복으로 때리는 걸 방지)
    private void PerformClickHit()
    {
        if (_health.IsDead)
            return;

        // 고정 데미지 1 대신 현재 장착한 무기의 클릭 데미지를 적용
        int baseDamage = WeaponManager.Instance != null ? WeaponManager.Instance.CurrentClickDamage : 1;

        // 클릭 데미지 업그레이드 보너스를 더함
        int clickDamageBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.ClickDamageBonus : 0;
        int damage = baseDamage + clickDamageBonus;

        // 크리티컬 확률 판정 - 성공하면 크리티컬 배율을 곱함
        bool isCrit = UpgradeManager.Instance != null && Random.value < UpgradeManager.Instance.CritChanceValue;
        if (isCrit)
            damage = Mathf.RoundToInt(damage * UpgradeManager.Instance.CritMultiplierValue);

        // 럭키 클릭 - 당첨되면 데미지 계산과 상관없이 즉시 파괴
        bool isLucky = UpgradeManager.Instance != null && Random.value < UpgradeManager.Instance.LuckyClickChance;
        if (isLucky)
            damage = _health.CurrentHP;

        // 이번 타격으로 죽는 게 아닐 때만 클릭 사운드 재생 (죽을 땐 파괴 사운드만 나오게)
        bool willDie = damage >= _health.CurrentHP;
        if (!willDie)
            PlayRandomClickSound();

        _health.TakeDamage(damage);

        // 콜라이더 테두리 위 랜덤한 지점에 현재 무기 이미지로 타격 연출 재생
        if (WeaponManager.Instance != null && _collider != null)
            WeaponSwingEffect.Instance?.PlaySwing(_collider, WeaponManager.Instance.CurrentWeapon.icon);
    }

    // 자동클릭 업그레이드가 켜져있으면 일정 주기마다 자동으로 PerformClickHit을 호출
    private void UpdateAutoClick()
    {
        if (UpgradeManager.Instance == null || !UpgradeManager.Instance.AutoClickIsUnlocked)
            return;

        _autoClickTimer += Time.deltaTime;
        float interval = UpgradeManager.Instance.AutoClickIntervalSeconds;

        if (_autoClickTimer < interval)
            return;

        _autoClickTimer -= interval; // 0으로 딱 자르지 않고 남은 오차만 빼서 주기가 조금씩 밀리는 걸 방지

        int clicks = UpgradeManager.Instance.AutoClickClicksPerTrigger;
        for (int i = 0; i < clicks; i++)
            PerformClickHit();
    }

    void Update()
    {
        UpdateAutoClick();

        // 새 Input System 기반 마우스 좌클릭 감지
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // 포인터가 UI(무기/오브젝트 팝업 등) 위에 있으면 그 뒤의 월드 오브젝트는 클릭 처리하지 않음
        if (UIPointerGuard.IsPointerOverUI)
            return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        // 클릭한 월드 좌표에 콜라이더가 있는지 확인하고,
        // 그 콜라이더가 다른 오브젝트가 아니라 "나 자신"인지 비교 (여러 콜라이더가 겹쳐도 안전)
        Collider2D hit = Physics2D.OverlapPoint(worldPos);
        if (hit != null && hit.gameObject == gameObject)
        {
            Debug.Log("Click");
            PerformClickHit();

            // 더블클릭 업그레이드 - 첫 타격과 겹쳐 보이지 않도록 살짝 늦게 두 번째 타격을 처리
            // (첫 타격에 죽었으면 지연된 PerformClickHit이 알아서 무시함 - IsDead 체크가 있음)
            if (UpgradeManager.Instance != null && UpgradeManager.Instance.DoubleClickIsUnlocked)
                StartCoroutine(PerformDelayedDoubleClickHit());
        }
    }

    private IEnumerator PerformDelayedDoubleClickHit()
    {
        yield return new WaitForSeconds(doubleClickDelaySeconds);
        PerformClickHit();
    }
}
