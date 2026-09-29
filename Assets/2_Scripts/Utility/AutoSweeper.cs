using System.Collections;
using UnityEngine;

// 자동 줍기 - 업그레이드로 해금되면 주기마다 바닥 왼쪽 밖에서 튀어나와 오른쪽 끝까지 지나가며,
// 지나가는 자리에 있는 파편(결정 포함)을 한 번에 정해진 개수까지 주워서 조각/결정을 지급함. 이미 지나간 자리에 새로 떨어진 건 안 주움. 지금 안 보는 존의 바닥도 같이 쓺 (개수 제한은 존마다 따로).
// 그림 크기는 프레임 스프라이트의 Pixels Per Unit으로 맞춤
[RequireComponent(typeof(SpriteRenderer))]
public class AutoSweeper : MonoBehaviour
{
    [SerializeField] private Sprite[] frames; // 이동 중 반복 재생할 애니메이션 프레임 (GIF를 프레임별로 쪼갠 것)
    [SerializeField] private float framesPerSecond = 12f; // 애니메이션 재생 속도
    [SerializeField] private float moveSpeed = 8f; // 좌->우 이동 속도 (월드 유닛/초)
    [SerializeField] private float heightOffset = 0f; // 바닥 범위 세로 중앙에서 위(+)/아래(-)로 얼마나 띄울지 (월드 유닛)

    private SpriteRenderer _renderer; // 이 오브젝트의 그림
    private float _timer; // 마지막 줍기 이후 흐른 시간
    private bool _sweeping; // 지금 지나가는 중인지

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _renderer.enabled = false;
    }

    void Update()
    {
        if (_sweeping || UpgradeManager.Instance == null || !UpgradeManager.Instance.AutoSweepIsUnlocked || DebrisPool.Instance == null)
            return;

        _timer += Time.deltaTime; // 업그레이드/무기 화면이 열려 timeScale이 0이면 같이 멈춤
        if (_timer < UpgradeManager.Instance.AutoSweepIntervalSeconds) return;

        if (!DebrisPool.Instance.HasLandedPieces()) return; // 어느 존에도 쓸 게 없으면 주울 게 생길 때까지 대기

        _timer = 0f;
        StartCoroutine(Sweep());
    }

    private IEnumerator Sweep()
    {
        _sweeping = true;

        Bounds floor = DebrisPool.Instance.FloorBounds;
        if (frames != null && frames.Length > 0) _renderer.sprite = frames[0];
        float halfWidth = _renderer.sprite != null ? _renderer.sprite.bounds.extents.x * transform.lossyScale.x : 0f; // 화면 밖에서 시작/끝나도록 쓰는 그림 절반 폭

        float x = floor.min.x - halfWidth; // 현재 x
        float endX = floor.max.x + halfWidth; // 도착 x
        float y = floor.center.y + heightOffset; // 지나가는 높이
        float elapsed = 0f; // 애니메이션용 경과 시간
        int zoneCount = ZoneManager.Instance != null ? Mathf.Max(1, ZoneManager.Instance.ZoneCount) : 1; // 쓸고 지나갈 존 수
        int[] remaining = new int[zoneCount]; // 존마다 이번에 더 주울 수 있는 파편 수
        for (int zone = 0; zone < zoneCount; zone++)
            remaining[zone] = UpgradeManager.Instance.AutoSweepPickupCount;

        _renderer.enabled = true;
        while (x < endX)
        {
            float prevX = x; // 이번 프레임 이동 전 x
            x += moveSpeed * Time.deltaTime;
            elapsed += Time.deltaTime;
            transform.position = new Vector3(x, y, transform.position.z);

            if (frames != null && frames.Length > 0)
                _renderer.sprite = frames[(int)(elapsed * framesPerSecond) % frames.Length];

            for (int zone = 0; zone < zoneCount; zone++)
                if (remaining[zone] > 0)
                    remaining[zone] -= DebrisPool.Instance.CollectLandedBetween(prevX, x, zone, remaining[zone]); // 그림 중앙이 이번 프레임에 지나간 구간만, 남은 개수만큼 주움
            yield return null;
        }

        _renderer.enabled = false;
        _sweeping = false;
    }
}
