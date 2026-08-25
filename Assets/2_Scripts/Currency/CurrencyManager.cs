using System;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    // 씬 어디서든 CurrencyManager.Instance로 접근하기 위한 싱글톤
    public static CurrencyManager Instance { get; private set; }

    public int Gold { get; private set; }

    // 골드가 변경될 때마다 현재 값을 전달 - GoldUI 등이 구독
    public event Action<int> OnGoldChanged;

    void Awake()
    {
        // 씬에 CurrencyManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void AddGold(int amount)
    {
        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    // 저장 파일을 불러올 때 값을 직접 세팅하기 위한 함수 (증감이 아니라 절대값 지정)
    public void SetGold(int amount)
    {
        Gold = amount;
        OnGoldChanged?.Invoke(Gold);
    }
}
