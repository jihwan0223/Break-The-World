using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private int maxHP = 10;

    public int MaxHP => maxHP;
    public int CurrentHP { get; private set; }

    // 체력이 깎일 때마다 (현재체력, 최대체력)을 전달 - 체력바 UI 등이 구독
    public event Action<int, int> OnDamaged;

    // 체력이 0 이하가 되는 순간 한 번 발행 - 파괴 이펙트, 보상 지급 등이 구독
    public event Action OnDied;

    void Awake()
    {
        CurrentHP = maxHP;
    }

    public void TakeDamage(int amount)
    {
        // 이미 죽은 오브젝트는 중복으로 죽지 않도록 방지
        if (CurrentHP <= 0)
            return;

        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        OnDamaged?.Invoke(CurrentHP, maxHP);

        if (CurrentHP <= 0)
        {
            OnDied?.Invoke();
            Destroy(gameObject);
        }
    }
}
