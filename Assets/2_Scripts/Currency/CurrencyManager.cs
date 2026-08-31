using System;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    // 씬 어디서든 CurrencyManager.Instance로 접근하기 위한 싱글톤
    public static CurrencyManager Instance { get; private set; }

    // 테스트용: 켜두면 파편이 항상 최대치로 유지됨 (업그레이드 테스트할 때 파편 모으는 시간 아끼려고).
    // 실제 재화 밸런스를 테스트할 땐 꺼두면 됨
    [SerializeField] private bool debugAlwaysMaxShards = true;
    private const int DebugMaxShardsAmount = 999999999; // 테스트용 최대 파편 값 - 가장 비싼 업그레이드(현재 Double Click 1,600,000)보다 넉넉히 높게

    public int Shards { get; private set; } // 보유 파편(재화) 개수

    // 파편이 변경될 때마다 현재 값을 전달 - ShardUI 등이 구독
    public event Action<int> OnShardsChanged;

    void Awake()
    {
        // 씬에 CurrencyManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (debugAlwaysMaxShards)
            Shards = DebugMaxShardsAmount;
    }

    public void AddShards(int amount)
    {
        Shards += amount;
        OnShardsChanged?.Invoke(Shards);
    }

    // 저장 파일을 불러올 때 값을 직접 세팅하기 위한 함수 (증감이 아니라 절대값 지정).
    // debugAlwaysMaxShards가 켜져있으면 저장된 값 대신 항상 최대치로 덮어씀
    public void SetShards(int amount)
    {
        Shards = debugAlwaysMaxShards ? DebugMaxShardsAmount : amount;
        OnShardsChanged?.Invoke(Shards);
    }

    // 파편이 충분할 때만 차감하고 true 반환 (부족하면 아무것도 안 하고 false 반환) - 업그레이드 구매 등에 사용
    public bool TrySpendShards(int amount)
    {
        if (Shards < amount)
            return false;

        Shards -= amount;
        OnShardsChanged?.Invoke(Shards);
        return true;
    }
}
