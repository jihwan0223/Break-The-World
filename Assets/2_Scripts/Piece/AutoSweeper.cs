using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AutoSweeper : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Sprite[] zoneSprites;
    [SerializeField] private float framesPerSecond = 12f;
    [SerializeField] private float moveSpeed = 8f; // 월드 유닛/초
    [SerializeField] private float heightOffset = 0f; // 월드 유닛
    [SerializeField] private float swayAngle = 12f; // 도
    [SerializeField] private float swaysPerSecond = 3f;
    [SerializeField] private float pickupReach = 0.5f; // 월드 유닛

    private SpriteRenderer _renderer;
    private float _timer;
    private bool _sweeping;

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _renderer.enabled = false;
    }

    void Update()
    {
        if (_sweeping || UpgradeManager.Instance == null || !UpgradeManager.Instance.AutoSweepIsUnlocked || DebrisPool.Instance == null)
            return;
        if (ZoneBreaker.ActiveBoss != null) return;

        _timer += Time.deltaTime;
        if (_timer < UpgradeManager.Instance.AutoSweepIntervalSeconds) return;

        if (!DebrisPool.Instance.HasLandedPieces()) return;

        _timer = 0f;
        StartCoroutine(Sweep());
    }

    private Sprite SpriteAt(float elapsed)
    {
        int zone = ZoneManager.Instance != null ? ZoneManager.Instance.CurrentZone : 0;
        if (zoneSprites != null && zone < zoneSprites.Length && zoneSprites[zone] != null)
            return zoneSprites[zone];

        if (frames != null && frames.Length > 0)
            return frames[(int)(elapsed * framesPerSecond) % frames.Length];
        return _renderer.sprite;
    }

    private IEnumerator Sweep()
    {
        _sweeping = true;

        Bounds floor = DebrisPool.Instance.FloorBounds;
        _renderer.sprite = SpriteAt(0f);

        // Pivot이 끝에 있고 흔들리기도 해서 가장 먼 모서리까지의 거리
        float reach = 0f;
        if (_renderer.sprite != null)
        {
            Bounds sprite = _renderer.sprite.bounds;
            reach = new Vector2(Mathf.Max(Mathf.Abs(sprite.min.x), Mathf.Abs(sprite.max.x)),
                Mathf.Max(Mathf.Abs(sprite.min.y), Mathf.Abs(sprite.max.y))).magnitude * Mathf.Abs(transform.lossyScale.x);
        }

        float leftEdge = floor.min.x;
        float rightEdge = floor.max.x;
        Camera cam = Camera.main;
        if (cam != null)
        {
            float camHalfWidth = cam.orthographicSize * cam.aspect;
            leftEdge = Mathf.Min(leftEdge, cam.transform.position.x - camHalfWidth);
            rightEdge = Mathf.Max(rightEdge, cam.transform.position.x + camHalfWidth);
        }

        float x = leftEdge - reach;
        float endX = rightEdge + reach;
        float y = floor.center.y + heightOffset;
        float elapsed = 0f;
        DebrisPool.Instance.MarkTopLayersForSweep(UpgradeManager.Instance.AutoSweepLayers);

        _renderer.enabled = true;
        while (x < endX)
        {
            float prevX = x;
            x += moveSpeed * Time.deltaTime;
            elapsed += Time.deltaTime;
            transform.position = new Vector3(x, y, transform.position.z);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * swaysPerSecond * Mathf.PI * 2f) * swayAngle);

            _renderer.sprite = SpriteAt(elapsed);

            DebrisPool.Instance.CollectMarkedBetween(prevX, x + pickupReach);
            DebrisPool.Instance.MoveCarried(x);
            yield return null;
        }

        DebrisPool.Instance.ReleaseCarried();
        _renderer.enabled = false;
        _sweeping = false;
    }

    void OnDisable()
    {
        if (!_sweeping) return;
        _sweeping = false;
        _renderer.enabled = false;
        if (DebrisPool.Instance != null) DebrisPool.Instance.ReleaseCarried();
    }
}
