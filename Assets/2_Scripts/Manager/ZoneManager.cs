using System;
using UnityEngine;

public class ZoneManager : MonoBehaviour
{
    public static ZoneManager Instance { get; private set; }

    [SerializeField] private GameObject[] zoneRoots; // 0 = 책상 위
    [SerializeField] private int[] zoneFirstObjects; // -1 = 없음

    private int _currentZone;
    private int _unlockedMaxZone; // 0 = 첫 존만

    public int CurrentZone => _currentZone;
    public int UnlockedMaxZone => _unlockedMaxZone;
    public int ZoneCount => zoneRoots == null ? 0 : GameSettings.IsDemo ? Mathf.Min(GameSettings.DemoZoneCount, zoneRoots.Length) : zoneRoots.Length; // 데모는 맵3까지

    public bool CanGoPrev => _currentZone > 0;
    public bool CanGoNext => _currentZone < _unlockedMaxZone;

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

    public void SetState(int unlockedMaxZone, int currentZone)
    {
        _unlockedMaxZone = Mathf.Clamp(unlockedMaxZone, 0, Mathf.Max(0, ZoneCount - 1));
        _currentZone = Mathf.Clamp(currentZone, 0, _unlockedMaxZone);
        GrantZoneObjects();
        ApplyZone();
        OnZoneChanged?.Invoke();
    }

    public void ResetAll()
    {
        if (ObjectManager.Instance != null && zoneFirstObjects != null)
            for (int zone = 1; zone < zoneFirstObjects.Length; zone++)
                if (zoneFirstObjects[zone] >= 0)
                    ObjectManager.Instance.SetUnlocked(zoneFirstObjects[zone], false);

        SetState(0, 0);
    }

    public bool IsZoneFirstObject(int objectIndex) =>
        objectIndex >= 0 && zoneFirstObjects != null && Array.IndexOf(zoneFirstObjects, objectIndex) >= 1;

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

    private void GrantZoneObjects()
    {
        if (ObjectManager.Instance == null || zoneFirstObjects == null) return;

        for (int zone = 1; zone <= _unlockedMaxZone && zone < zoneFirstObjects.Length; zone++)
        {
            int objectIndex = zoneFirstObjects[zone];
            if (objectIndex >= 0 && !ObjectManager.Instance.IsUnlocked(objectIndex))
                ObjectManager.Instance.SetUnlocked(objectIndex, true);
        }
    }

    public bool IsHidden(Transform target)
    {
        int zone = ZoneOf(target);
        return zone >= 0 && zone != _currentZone;
    }

    public int ZoneOf(Transform target)
    {
        if (zoneRoots == null) return -1;

        for (Transform current = target; current != null; current = current.parent)
            for (int i = 0; i < zoneRoots.Length; i++)
                if (zoneRoots[i] != null && zoneRoots[i].transform == current)
                    return i;

        return -1;
    }

    // 자동클릭이 다른 존에서도 돌게 루트는 다 켜두고 그림만 끔
    private void ApplyZone()
    {
        if (zoneRoots == null) return;

        for (int i = 0; i < zoneRoots.Length; i++)
        {
            GameObject root = zoneRoots[i];
            if (root == null) continue;

            root.SetActive(i < ZoneCount); // 데모에서 막힌 맵은 끔
            bool hidden = i != _currentZone;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.forceRenderingOff = hidden; // enabled와 별개라 UnlockGate/Health와 충돌 없음
        }
    }
}
