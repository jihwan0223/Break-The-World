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

            DebrisPool.Instance.CollectMarkedBetween(prevX, x); // 그림 중앙이 이번 프레임에 지나간 구간의 대상 파편을 전부 주움
            yield return null;
        }

        _renderer.enabled = false;
        _sweeping = false;
    }
}
