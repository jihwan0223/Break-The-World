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
    [SerializeField] private AudioClip clickSound;

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

    void Update()
    {
        // 새 Input System 기반 마우스 좌클릭 감지
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        // 클릭한 월드 좌표에 콜라이더가 있는지 확인하고,
        // 그 콜라이더가 다른 오브젝트가 아니라 "나 자신"인지 비교 (여러 콜라이더가 겹쳐도 안전)
        Collider2D hit = Physics2D.OverlapPoint(worldPos);
        if (hit != null && hit.gameObject == gameObject)
        {
            Debug.Log("Click");

            if (clickSound != null)
                _audioSource.PlayOneShot(clickSound);

            _health.TakeDamage(1);
        }
    }
}
