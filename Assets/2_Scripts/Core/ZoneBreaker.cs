using UnityEngine;

// 책상처럼 "존 하나를 대표하는" 오브젝트. 부수면 같은 존에 놓인 다른 오브젝트도 전부 같이 부서져 조각이 들어오고,
// 처음 부술 때는 다음 존을 해금하는 연출(ZoneUnlockEffect)을 재생한다. 이후엔 부술 때마다 존 전체 수확만 함.
[RequireComponent(typeof(Health))]
public class ZoneBreaker : MonoBehaviour
{
    [SerializeField] private Transform zoneRoot; // 이 존의 루트 - 이 아래에 있는 오브젝트를 전부 같이 부숨
    [SerializeField] private int unlockZone = 1; // 처음 부술 때 해금할 존 번호
    [SerializeField] private ZoneUnlockEffect unlockEffect; // 첫 파괴 연출 (비워두면 연출 없이 바로 해금)

    private Health _health; // 이 오브젝트(책상)의 체력
    private bool _unlockStarted; // 이번 실행 중 해금 연출을 이미 시작했는지 (연출 도중 또 부서져도 한 번만)

    void Start()
    {
        _health = GetComponent<Health>();
        _health.OnDied += HandleDied;
    }

    void OnDestroy()
    {
        if (_health != null)
            _health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        HarvestZone();

        if (ZoneManager.Instance == null || _unlockStarted || ZoneManager.Instance.UnlockedMaxZone >= unlockZone)
            return;

        _unlockStarted = true;

        if (unlockEffect != null) unlockEffect.Play(unlockZone);
        else ZoneManager.Instance.UnlockZone(unlockZone);
    }

    // 존 안의 다른 오브젝트를 전부 부숨. 각자의 Click이 조각 지급/파편 연출을 알아서 처리함.
    // 해금 전(UnlockGate가 Health를 꺼둔 것)이나 이미 부서져 있는 것(TakeDamage가 무시)은 건너뜀
    private void HarvestZone()
    {
        if (zoneRoot == null) return;

        foreach (Health h in zoneRoot.GetComponentsInChildren<Health>())
            if (h != _health && h.enabled)
                h.TakeDamage(h.CurrentHP);
    }
}
