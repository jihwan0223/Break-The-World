using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(AudioSource))]
public class Click : MonoBehaviour
{
    private Health _health;
    private AudioSource _audioSource;

    [SerializeField] private int goldReward = 1;

    private int _lastClickSoundIndex = -1; // 방금 재생한 사운드 인덱스 (바로 다음 클릭에서 같은 소리가 안 나오게 기억)

    void Start()
    {
        _health = GetComponent<Health>();
        _audioSource = GetComponent<AudioSource>();
        _health.OnDied += HandleDied;
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제 (오브젝트가 파괴될 때 CurrencyManager 쪽 참조가 남지 않도록)
        _health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.AddGold(goldReward);
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

    void Update()
    {
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

            // 고정 데미지 1 대신 현재 장착한 무기의 클릭 데미지를 적용
            int damage = WeaponManager.Instance != null ? WeaponManager.Instance.CurrentClickDamage : 1;

            // 이번 타격으로 죽는 게 아닐 때만 클릭 사운드 재생 (죽을 땐 파괴 사운드만 나오게)
            bool willDie = damage >= _health.CurrentHP;
            if (!willDie)
                PlayRandomClickSound();

            _health.TakeDamage(damage);

            // 맞은 콜라이더 테두리 위 랜덤한 지점에 현재 무기 이미지로 타격 연출 재생
            if (WeaponManager.Instance != null)
                WeaponSwingEffect.Instance?.PlaySwing(hit, WeaponManager.Instance.CurrentWeapon.icon);
        }
    }
}
