using System.Collections;
using UnityEngine;

// 책상처럼 "존 하나를 대표하는" 오브젝트. 부수면 같은 존에 놓인 다른 오브젝트도 전부 같이 부서져 조각이 들어오고,
// 처음 부술 때는 다음 존을 해금하는 연출(ZoneUnlockEffect)을 재생한다. 이후엔 부술 때마다 존 전체 수확만 함.
// 결정 업그레이드(존 자동 파괴 해금)를 사면 이 오브젝트가 갈라진 상태가 되어, 직접 부수지 않아도 autoBreakInterval마다 존 전체가 계속 부서진다.
// shockwaveSpeed를 넣으면 이 오브젝트에서 충격파가 퍼져나가며 가까운 오브젝트부터 차례로 부서짐 (0이면 전부 동시에)
[RequireComponent(typeof(Health))]
public class ZoneBreaker : MonoBehaviour
{
    [SerializeField] private Transform zoneRoot; // 이 존의 루트 - 이 아래에 있는 오브젝트를 전부 같이 부숨
    [SerializeField] private int unlockZone = 1; // 처음 부술 때 해금할 존 번호 (ZoneManager에 그 존이 없으면 해금/연출 안 함)
    [SerializeField] private ZoneUnlockEffect unlockEffect; // 첫 파괴 연출 (비워두면 연출 없이 바로 해금)
    [SerializeField] private float shockwaveSpeed = 0f; // 충격파가 퍼지는 속도(월드 유닛/초) - 0이면 충격파 없이 전부 동시에 부서짐
    [SerializeField] private float shockwaveWidth = 0.15f; // 충격파 고리 두께(월드 유닛)
    [SerializeField] private Color shockwaveColor = new Color(1f, 1f, 1f, 0.8f); // 충격파 고리 색
    [SerializeField] private float shakeAmount = 0.15f; // 충격파 때 카메라 흔들림 최대 세기(월드 유닛) - 존 해금 연출이 나올 땐 그쪽 흔들림을 씀
    [SerializeField] private float shakeDuration = 0.3f; // 카메라 흔들림 시간(초)
    [SerializeField] private float autoBreakInterval = 5f; // 존 자동 파괴를 해금하면 이 주기(초)마다 존 안의 다른 오브젝트가 전부 부서짐
    [SerializeField] private GameObject unlockedVisual; // 존 자동 파괴를 해금하면 켜지는 연출 오브젝트 (책상 균열 / 도로 싱크홀 그림 - 비워둬도 됨)

    private const int RingSegments = 64; // 충격파 고리를 이루는 점 개수

    private Health _health; // 이 오브젝트(책상/도로)의 체력
    private LineRenderer _ring; // 충격파 고리 (처음 쓸 때 만듦)
    private Click _click; // 이 오브젝트의 클릭 처리 - 오브젝트 번호를 알아내는 데 씀
    private float _autoBreakTimer; // 마지막 자동 파괴 이후 흐른 시간(초)
    private int _waveId; // 가장 최근 충격파 번호 - 연타로 겹치면 고리는 최신 충격파만 그림

    void Start()
    {
        _health = GetComponent<Health>();
        _health.OnDied += HandleDied;
        _click = GetComponent<Click>();
    }

    void Update()
    {
        // 존 자동 파괴(결정 업그레이드)를 해금했으면 갈라진 연출을 켜고, 주기마다 존 전체를 부숨. 안 보는 존에서도 계속 돎
        bool unlocked = _click != null && UpgradeManager.Instance != null
            && UpgradeManager.Instance.ZoneAutoBreakIsUnlockedFor(_click.ObjectIndex); // 이 오브젝트의 자동 파괴를 해금했는지
        if (unlockedVisual != null && unlockedVisual.activeSelf != unlocked) unlockedVisual.SetActive(unlocked);
        if (!unlocked)
        {
            _autoBreakTimer = 0f;
            return;
        }

        _autoBreakTimer += Time.deltaTime; // 업그레이드 화면이 열려 timeScale이 0이면 같이 멈춤
        if (_autoBreakTimer < autoBreakInterval) return;

        _autoBreakTimer = 0f;
        if (shockwaveSpeed > 0f) StartCoroutine(Shockwave(false)); // 주기마다 흔들리면 거슬려서 카메라 흔들림은 뺌
        else HarvestZone();
    }

    void OnDestroy()
    {
        if (_health != null)
            _health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        bool unlocking = ZoneManager.Instance != null
            && ZoneManager.Instance.UnlockedMaxZone < unlockZone
            && unlockZone < ZoneManager.Instance.ZoneCount; // 이번 파괴로 다음 존을 새로 여는지

        if (shockwaveSpeed > 0f) StartCoroutine(Shockwave(!unlocking));
        else HarvestZone();

        if (!unlocking) return;

        if (unlockEffect != null) unlockEffect.Play(unlockZone);
        else ZoneManager.Instance.UnlockZone(unlockZone);
    }

    // 존 안의 다른 오브젝트를 전부 부숨. 각자의 Click이 조각 지급/파편 연출을 알아서 처리함.
    // 해금 전(UnlockGate가 Health를 꺼둔 것)이나 이미 부서져 있는 것(TakeDamage가 무시)은 건너뜀
    private void HarvestZone()
    {
        if (zoneRoot == null) return;

        foreach (Health h in zoneRoot.GetComponentsInChildren<Health>())
            Break(h);
    }

    private void Break(Health h)
    {
        if (h == _health || !h.enabled) return;

        Click click = h.GetComponent<Click>(); // 대상의 클릭 처리 - 있으면 "직접 부순 것"으로 쳐서 보상이 파편으로 떨어지게 함
        if (click != null && click.enabled) click.BreakByZone();
        else h.TakeDamage(h.CurrentHP);
    }

    // 이 오브젝트 중심에서 고리가 퍼져나가고, 고리가 닿은 오브젝트부터 부서짐
    private IEnumerator Shockwave(bool shake)
    {
        if (zoneRoot == null) yield break;

        Vector3 center = transform.position; // 충격파 시작점
        Health[] targets = zoneRoot.GetComponentsInChildren<Health>(); // 부술 대상들
        bool[] done = new bool[targets.Length]; // 이미 충격파가 닿은 대상인지
        float maxRadius = 0f; // 가장 먼 대상까지의 거리 - 고리가 여기까지 퍼지면 끝
        foreach (Health h in targets)
            maxRadius = Mathf.Max(maxRadius, DistanceTo(h, center));
        maxRadius += 1f; // 마지막 대상을 지나 조금 더 퍼지다가 사라지게

        bool visible = ZoneManager.Instance == null || !ZoneManager.Instance.IsHidden(transform); // 안 보이는 존(자동클릭)이면 고리/흔들림 없이 부수기만
        LineRenderer ring = visible ? GetRing() : null; // 이번에 그릴 고리
        int waveId = ++_waveId; // 이 충격파 번호
        if (visible && shake) CameraShake.Shake(shakeAmount, shakeDuration);

        for (float t = 0f; ; t += Time.deltaTime)
        {
            float radius = t * shockwaveSpeed; // 지금 고리 반지름
            for (int i = 0; i < targets.Length; i++)
            {
                if (done[i] || targets[i] == null || DistanceTo(targets[i], center) > radius) continue;
                done[i] = true;
                Break(targets[i]);
            }

            if (ring != null && waveId == _waveId) DrawRing(ring, center, radius, 1f - radius / maxRadius);

            if (radius >= maxRadius) break;
            yield return null;
        }

        if (ring != null && waveId == _waveId) ring.enabled = false;
    }

    // 대상 그림의 중심까지 거리 (피벗이 아래쪽인 오브젝트도 있어서 그림 기준)
    private static float DistanceTo(Health h, Vector3 center)
    {
        Renderer renderer = h.GetComponent<Renderer>(); // 대상 그림
        Vector3 position = renderer != null ? renderer.bounds.center : h.transform.position; // 대상 중심
        return Vector2.Distance(position, center);
    }

    private LineRenderer GetRing()
    {
        if (_ring != null) return _ring;

        var ringObject = new GameObject("ShockwaveRing"); // 고리를 담을 오브젝트
        ringObject.transform.SetParent(transform, false);
        _ring = ringObject.AddComponent<LineRenderer>();
        _ring.loop = true;
        _ring.useWorldSpace = true;
        _ring.positionCount = RingSegments;
        _ring.widthMultiplier = shockwaveWidth;
        _ring.sharedMaterial = GetComponent<SpriteRenderer>() != null ? GetComponent<SpriteRenderer>().sharedMaterial : null; // 스프라이트 기본 머티리얼을 같이 씀
        _ring.sortingOrder = 25; // 오브젝트/파편보다 위
        return _ring;
    }

    private void DrawRing(LineRenderer ring, Vector3 center, float radius, float alpha)
    {
        ring.enabled = true;
        Color color = shockwaveColor; // 멀리 퍼질수록 흐려지는 고리 색
        color.a *= Mathf.Clamp01(alpha);
        ring.startColor = color;
        ring.endColor = color;
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RingSegments; // 이 점의 각도
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }
}
