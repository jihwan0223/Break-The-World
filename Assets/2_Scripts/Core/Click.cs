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

    [SerializeField] private int pieceReward = 1; // 이 오브젝트를 파괴했을 때 기본으로 지급되는 조각 개수 (그 오브젝트 종류의 조각)
    [SerializeField] private float doubleClickDelaySeconds = 0.08f; // 더블클릭의 두 번째 타격이 첫 타격보다 이만큼 늦게 나옴

    private int _lastClickSoundIndex = -1; // 방금 재생한 사운드 인덱스 (바로 다음 클릭에서 같은 소리가 안 나오게 기억)
    private float _autoClickTimer; // 자동클릭 업그레이드의 다음 발동까지 누적된 시간(초)
    private float _autoMineTimer; // 자동채굴 업그레이드의 다음 발동까지 누적된 시간(초)

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
        // 바로 아래에서 ObjectManager.Instance도 참조하니 둘 다 확인해야 함 (CurrencyManager만 확인하면 널 참조 위험)
        if (CurrencyManager.Instance == null || ObjectManager.Instance == null)
            return;

        // 죽은 시점에 장착돼있던 오브젝트가 곧 방금 파괴된 오브젝트 (Health 하나가 선택에 따라 티어만 바뀌는 구조)
        int objectIndex = ObjectManager.Instance.EquippedIndex;

        // 그 오브젝트의 "획득량 증가" 업그레이드 보너스 + 콤보 배율을 적용
        long gainBonus = ObjectManager.Instance.GetGainBonus(objectIndex);
        float comboMultiplier = ComboManager.Instance != null ? ComboManager.Instance.ShardMultiplier : 1f;

        long finalPieces = Mathf.Max(1, Mathf.RoundToInt((pieceReward + gainBonus) * comboMultiplier));
        CurrencyManager.Instance.AddPieces(objectIndex, finalPieces);
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

        // 클릭 데미지 업그레이드 보너스를 더함 (전역 + 지금 장착한 오브젝트 전용 강화 합산)
        int equippedObjectIndex = ObjectManager.Instance != null ? ObjectManager.Instance.EquippedIndex : -1;
        int clickDamageBonus = UpgradeManager.Instance != null ? UpgradeManager.Instance.GetClickDamageBonus(equippedObjectIndex) : 0;
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

    // 자동채굴 업그레이드가 켜져있으면 일정 주기마다, 지금 캐는 오브젝트보다 몇 단계 전 오브젝트를 자동으로 캐서 조각을 지급함
    // (눈에 보이는 오브젝트/체력 시스템과는 무관하게, 뒤에서 조용히 조각만 채워주는 방식)
    private void UpdateAutoMine()
    {
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
        long amount = 1 + gainBonus;
        CurrencyManager.Instance.AddPieces(targetIndex, amount);
    }

    void Update()
    {
        // 게임이 진행 중이 아니면(시작 전 화면 / 결과창) 클릭·자동클릭·자동채굴을 전부 멈춤.
        // GameSessionManager가 아직 씬에 없으면(구버전 씬 등) 종전처럼 항상 동작하도록 통과시킴
        if (GameSessionManager.Instance != null && !GameSessionManager.Instance.IsRunActive)
            return;

        UpdateAutoClick();
        UpdateAutoMine();

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
