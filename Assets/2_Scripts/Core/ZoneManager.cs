using System;
using UnityEngine;

// 지역(존) 전환 관리. 존 루트를 하나만 켜두는 방식이라 존마다 배경/오브젝트를 통째로 갈아끼운다.
// 해금은 ★ 지형 오브젝트(책상 등)를 처음 부술 때 UnlockZone으로 열어준다.
public class ZoneManager : MonoBehaviour
{
    // 씬 어디서든 ZoneManager.Instance로 접근하기 위한 싱글톤
    public static ZoneManager Instance { get; private set; }

    [SerializeField] private GameObject[] zoneRoots; // 존 번호 순서대로 넣을 것 (0 = 책상 위)

    private int _currentZone; // 지금 보고 있는 존 번호
    private int _unlockedMaxZone; // 해금된 마지막 존 번호 (0이면 첫 존만 열린 상태)

    public int CurrentZone => _currentZone;
    public int UnlockedMaxZone => _unlockedMaxZone;
    public int ZoneCount => zoneRoots != null ? zoneRoots.Length : 0;

    public bool CanGoPrev => _currentZone > 0;
    public bool CanGoNext => _currentZone < _unlockedMaxZone;

    // 존이 바뀌거나 새로 해금될 때 발행 - 화살표 UI가 구독해서 표시를 갱신함
    public event Action OnZoneChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ApplyZone();
    }

    // 세이브 로드용 - 구매/연출 없이 상태만 대입
    public void SetState(int unlockedMaxZone, int currentZone)
    {
        _unlockedMaxZone = Mathf.Clamp(unlockedMaxZone, 0, Mathf.Max(0, ZoneCount - 1));
        _currentZone = Mathf.Clamp(currentZone, 0, _unlockedMaxZone);
        ApplyZone();
        OnZoneChanged?.Invoke();
    }

    // 해당 존을 해금 (이미 열려있으면 무시). 실제 호출은 ★ 오브젝트 첫 파괴 시점
    public void UnlockZone(int zone)
    {
        if (zone <= _unlockedMaxZone || zone >= ZoneCount) return;

        _unlockedMaxZone = zone;
        OnZoneChanged?.Invoke();
    }

    public void GoTo(int zone)
    {
        if (zone < 0 || zone > _unlockedMaxZone || zone >= ZoneCount) return;

        _currentZone = zone;
        ApplyZone();
        OnZoneChanged?.Invoke();
    }

    public void GoPrev() => GoTo(_currentZone - 1);
    public void GoNext() => GoTo(_currentZone + 1);

    // 현재 존 루트만 켜고 나머지는 끔
    private void ApplyZone()
    {
        if (zoneRoots == null) return;

        for (int i = 0; i < zoneRoots.Length; i++)
            if (zoneRoots[i] != null)
                zoneRoots[i].SetActive(i == _currentZone);
    }
}
