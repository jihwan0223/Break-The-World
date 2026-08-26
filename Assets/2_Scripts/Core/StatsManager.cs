using System;
using System.Collections.Generic;
using UnityEngine;

// 오브젝트별(ObjectManager 리스트 인덱스 기준) 파괴 횟수를 관리하는 매니저
public class StatsManager : MonoBehaviour
{
    // 씬 어디서든 StatsManager.Instance로 접근하기 위한 싱글톤
    public static StatsManager Instance { get; private set; }

    private readonly Dictionary<int, int> _destroyCounts = new Dictionary<int, int>(); // objectIndex -> 파괴 횟수 (기록 없으면 0)

    // 오브젝트 하나의 파괴 횟수가 바뀔 때마다 (objectIndex, 새 횟수) 전달 - GoldUI 등이 구독
    public event Action<int, int> OnObjectDestroyCountChanged;

    void Awake()
    {
        // 씬에 StatsManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public int GetDestroyCount(int objectIndex)
    {
        return _destroyCounts.TryGetValue(objectIndex, out int count) ? count : 0;
    }

    public void AddDestroyed(int objectIndex)
    {
        int newCount = GetDestroyCount(objectIndex) + 1;
        _destroyCounts[objectIndex] = newCount;
        OnObjectDestroyCountChanged?.Invoke(objectIndex, newCount);
    }

    // 저장 파일을 불러올 때 값을 직접 세팅하기 위한 함수 (증감이 아니라 절대값 지정)
    public void SetDestroyCount(int objectIndex, int count)
    {
        _destroyCounts[objectIndex] = count;
        OnObjectDestroyCountChanged?.Invoke(objectIndex, count);
    }
}
