using System;
using UnityEngine;

// 지역(존) 전환 관리. 존 루트를 하나만 켜두는 방식이라 존마다 배경/오브젝트를 통째로 갈아끼운다.
// 해금은 ★ 지형 오브젝트(책상 등)를 처음 부술 때 UnlockZone으로 열어준다.
public class ZoneManager : MonoBehaviour
{
    // 씬 어디서든 ZoneManager.Instance로 접근하기 위한 싱글톤
    public static ZoneManager Instance { get; private set; }

    [SerializeField] private GameObject[] zoneRoots; // 존 번호 순서대로 넣을 것 (0 = 책상 위)
    [SerializeField] private int[] zoneFirstObjects; // 존 번호별로, 그 존이 열릴 때 조각 없이 공짜로 해금해줄 오브젝트 인덱스 (-1이면 없음). 존이 늘면 여기도 같이 채울 것

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
        GrantZoneObjects();
        ApplyZone();
        OnZoneChanged?.Invoke();
    }

    // 테스트용 - 첫 존만 열린 처음 상태로 되돌림 (디버그 메뉴의 "존 해금 초기화"). 존 해금으로 공짜로 열렸던 오브젝트도 다시 잠그고, 첫 파괴 연출도 다시 나옴
    public void ResetAll()
    {
        if (ObjectManager.Instance != null && zoneFirstObjects != null)
            for (int zone = 1; zone < zoneFirstObjects.Length; zone++)
                if (zoneFirstObjects[zone] >= 0)
                    ObjectManager.Instance.SetUnlocked(zoneFirstObjects[zone], false);

        SetState(0, 0);
    }

    // 해당 존을 해금 (이미 열려있으면 무시). 실제 호출은 ★ 오브젝트 첫 파괴 시점
    public void UnlockZone(int zone)
    {
        if (zone <= _unlockedMaxZone || zone >= ZoneCount) return;

        _unlockedMaxZone = zone;
        GrantZoneObjects();
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

    // 열려 있는 존들의 첫 오브젝트를 해금 (이미 해금된 건 건너뜀). 저장 파일을 불러올 때도 불려서, 이미 열린 존의 오브젝트가 빠져있지 않게 맞춰줌
    private void GrantZoneObjects()
    {
        if (ObjectManager.Instance == null || zoneFirstObjects == null) return;

        for (int zone = 1; zone <= _unlockedMaxZone && zone < zoneFirstObjects.Length; zone++)
        {
            int objectIndex = zoneFirstObjects[zone]; // 이 존이 열리면 같이 열어줄 오브젝트
            if (objectIndex >= 0 && !ObjectManager.Instance.IsUnlocked(objectIndex))
                ObjectManager.Instance.SetUnlocked(objectIndex, true);
        }
    }

    // 현재 존 루트만 켜고 나머지는 끔
    private void ApplyZone()
    {
        if (zoneRoots == null) return;

        for (int i = 0; i < zoneRoots.Length; i++)
            if (zoneRoots[i] != null)
                zoneRoots[i].SetActive(i == _currentZone);
    }
}
