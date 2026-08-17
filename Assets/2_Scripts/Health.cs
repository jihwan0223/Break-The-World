using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private int maxHP = 10;

    public int MaxHP => maxHP;
    public int CurrentHP { get; private set; }

    public event Action<int, int> OnDamaged; // (currentHP, maxHP)
    public event Action OnDied;

    void Awake()
    {
        CurrentHP = maxHP;
    }

    public void TakeDamage(int amount)
    {
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
