using System.Collections;
using UnityEngine;

// 자동 줍기 - 업그레이드로 해금되면 주기마다 바닥 왼쪽 밖에서 튀어나와 오른쪽 끝까지 지나가며,
// 지나가는 자리에 있는 파편을 맨 위층부터 업그레이드된 층 수만큼 전부 주워서 조각/결정을 지급함 (개수 제한 없음, 결정은 층과 상관없이 주움).
// 이미 지나간 자리와 출발한 뒤에 떨어진 파편은 안 주움. 지금 안 보는 존의 바닥도 같이 쓺.
// 그림 크기는 프레임 스프라이트의 Pixels Per Unit으로 맞춤. 스프라이트 Pivot(손잡이 끝)을 축으로 좌우로 흔들리며 쓰는 것처럼 보임
[RequireComponent(typeof(SpriteRenderer))]
public class AutoSweeper : MonoBehaviour
{
    [SerializeField] private Sprite[] frames; // 이동 중 반복 재생할 애니메이션 프레임 (GIF를 프레임별로 쪼갠 것)
    [SerializeField] private float framesPerSecond = 12f; // 애니메이션 재생 속도
    [SerializeField] private float moveSpeed = 8f; // 좌->우 이동 속도 (월드 유닛/초)
    [SerializeField] private float heightOffset = 0f; // 바닥 범위 세로 중앙에서 위(+)/아래(-)로 얼마나 띄울지 (월드 유닛) - 그림의 Pivot 위치 기준
    [SerializeField] private float swayAngle = 12f; // 스프라이트 Pivot을 축으로 좌우로 흔들리는 최대 각도(도). 0이면 안 흔들림
    [SerializeField] private float swaysPerSecond = 3f; // 1초에 좌우로 왕복하는 횟수
    [SerializeField] private float pickupReach = 0.5f; // 그림 중앙에서 앞쪽(진행 방향)으로 이 거리(월드 유닛)까지 닿는 파편을 미리 주움 - 클수록 빗자루 범위가 넓어짐

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

        // 그림이 Pivot(손잡이 끝)에서 가장 멀리 뻗는 거리 - Pivot이 가운데가 아니고 흔들리며 돌기도 해서,
        // 절반 폭이 아니라 Pivot에서 가장 먼 모서리까지의 거리만큼 화면 밖에 있어야 완전히 안 보임
        float reach = 0f;
        if (_renderer.sprite != null)
        {
            Bounds sprite = _renderer.sprite.bounds; // Pivot 기준 그림 범위
            reach = new Vector2(Mathf.Max(Mathf.Abs(sprite.min.x), Mathf.Abs(sprite.max.x)),
                Mathf.Max(Mathf.Abs(sprite.min.y), Mathf.Abs(sprite.max.y))).magnitude * Mathf.Abs(transform.lossyScale.x);
        }

        float leftEdge = floor.min.x; // 지나가야 할 구간의 왼쪽 끝
        float rightEdge = floor.max.x; // 지나가야 할 구간의 오른쪽 끝
        Camera cam = Camera.main; // 바닥이 화면보다 좁아도 화면 밖에서 나타났다 사라지게 화면 끝까지 넓힘
        if (cam != null)
        {
            float camHalfWidth = cam.orthographicSize * cam.aspect; // 화면 가로 절반(월드 유닛)
            leftEdge = Mathf.Min(leftEdge, cam.transform.position.x - camHalfWidth);
            rightEdge = Mathf.Max(rightEdge, cam.transform.position.x + camHalfWidth);
        }

        float x = leftEdge - reach; // 현재 x
        float endX = rightEdge + reach; // 도착 x
        float y = floor.center.y + heightOffset; // 지나가는 높이
        float elapsed = 0f; // 애니메이션용 경과 시간
        DebrisPool.Instance.MarkTopLayersForSweep(UpgradeManager.Instance.AutoSweepLayers); // 출발하는 순간, 위층부터 몇 층을 쓸지 정함

        _renderer.enabled = true;
        while (x < endX)
        {
            float prevX = x; // 이번 프레임 이동 전 x
            x += moveSpeed * Time.deltaTime;
            elapsed += Time.deltaTime;
            transform.position = new Vector3(x, y, transform.position.z);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * swaysPerSecond * Mathf.PI * 2f) * swayAngle);

            if (frames != null && frames.Length > 0)
                _renderer.sprite = frames[(int)(elapsed * framesPerSecond) % frames.Length];

            DebrisPool.Instance.CollectMarkedBetween(prevX, x + pickupReach); // 이번 프레임에 지나간 구간 + 앞쪽 범위 안의 대상 파편을 전부 주움
            DebrisPool.Instance.MoveCarried(x); // 주운 파편은 빗자루 앞에 모여서 같이 밀려감
            yield return null;
        }

        DebrisPool.Instance.ReleaseCarried(); // 화면 밖까지 밀고 나간 파편을 치움
        _renderer.enabled = false;
        _sweeping = false;
    }

    void OnDisable()
    {
        // 쓰는 도중에 꺼지면 코루틴이 멈추므로, 밀고 가던 파편과 그림이 화면에 남지 않게 정리하고 다음에 다시 나올 수 있게 함
        if (!_sweeping) return;
        _sweeping = false;
        _renderer.enabled = false;
        if (DebrisPool.Instance != null) DebrisPool.Instance.ReleaseCarried();
    }
}
