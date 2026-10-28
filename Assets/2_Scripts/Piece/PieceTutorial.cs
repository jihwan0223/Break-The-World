using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceTutorial : MonoBehaviour
{
    public static PieceTutorial Instance { get; private set; }

    public static bool Active { get; private set; }

    [SerializeField] private float zoomDuration = 0.8f; // 초
    [SerializeField] private float padding = 0.4f; // 월드 유닛
    [SerializeField] private float minOrthoSize = 0.8f; // orthographicSize 하한
    [SerializeField] private float blinkPerSecond = 1.5f;
    [SerializeField] private float glowMaxAlpha = 0.85f;
    [SerializeField] private float glowScale = 1.6f;

    public bool Done { get; set; }

    private readonly List<Transform> _targets = new List<Transform>();
    private bool _collected;
    private Sprite _glowSprite;

    void Awake()
    {
        Instance = this;
        _glowSprite = CreateGlowSprite();
    }

    void Start()
    {
        if (DebrisPool.Instance == null) return;
        DebrisPool.Instance.OnPiecesDropped += HandlePiecesDropped;
        DebrisPool.Instance.OnPieceCollected += HandlePieceCollected;
    }

    void OnDestroy()
    {
        Active = false;
        if (DebrisPool.Instance == null) return;
        DebrisPool.Instance.OnPiecesDropped -= HandlePiecesDropped;
        DebrisPool.Instance.OnPieceCollected -= HandlePieceCollected;
    }

    private void HandlePiecesDropped(IReadOnlyList<Transform> pieces)
    {
        if (Done || Active || pieces.Count == 0) return;

        _targets.Clear();
        _targets.AddRange(pieces);
        _collected = false;
        Active = true;
        StartCoroutine(Run());
    }

    private void HandlePieceCollected(Transform piece)
    {
        if (!Active || !_targets.Contains(piece)) return;
        _collected = true;
        Done = true; // 조각 획득 저장에 같이 들어가게 바로 기록
    }

    private IEnumerator Run()
    {
        // timeScale 0이어도 진행되게 실시간 기준
        yield return new WaitForSecondsRealtime(DebrisPool.Instance.FallDuration + 0.05f);

        Camera cam = Camera.main;
        Vector3 homePosition = cam.transform.position;
        float homeSize = cam.orthographicSize;

        var bounds = new Bounds(_targets[0].position, Vector3.zero);
        foreach (Transform t in _targets) bounds.Encapsulate(t.position);
        float targetSize = Mathf.Min(homeSize, Mathf.Max(minOrthoSize, bounds.extents.y + padding, (bounds.extents.x + padding) / cam.aspect));

        // 줌인 화면이 원래 화면 밖을 보이지 않게 중심을 안쪽으로
        float roomY = homeSize - targetSize;
        float roomX = (homeSize - targetSize) * cam.aspect;
        Vector3 targetPosition = new Vector3(
            Mathf.Clamp(bounds.center.x, homePosition.x - roomX, homePosition.x + roomX),
            Mathf.Clamp(bounds.center.y, homePosition.y - roomY, homePosition.y + roomY),
            homePosition.z);

        List<SpriteRenderer> glows = CreateGlows();
        yield return MoveCamera(cam, homePosition, homeSize, targetPosition, targetSize, glows);

        float elapsed = 0f;
        while (!_collected)
        {
            elapsed += Time.unscaledDeltaTime;
            SetGlowAlpha(glows, Blink(elapsed));
            yield return null;
        }

        foreach (SpriteRenderer glow in glows)
            if (glow != null) Destroy(glow.gameObject);

        yield return MoveCamera(cam, targetPosition, targetSize, homePosition, homeSize, null);
        Active = false;
    }

    private float Blink(float elapsed) => glowMaxAlpha * (0.5f - 0.5f * Mathf.Cos(elapsed * blinkPerSecond * 2f * Mathf.PI));

    private IEnumerator MoveCamera(Camera cam, Vector3 fromPos, float fromSize, Vector3 toPos, float toSize, List<SpriteRenderer> glows)
    {
        float elapsed = 0f;
        while (elapsed < zoomDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / zoomDuration));
            cam.transform.position = Vector3.Lerp(fromPos, toPos, t);
            cam.orthographicSize = Mathf.Lerp(fromSize, toSize, t);
            if (glows != null) SetGlowAlpha(glows, Blink(elapsed));
            yield return null;
        }
        cam.transform.position = toPos;
        cam.orthographicSize = toSize;
    }

    private List<SpriteRenderer> CreateGlows()
    {
        var glows = new List<SpriteRenderer>();
        foreach (Transform piece in _targets)
        {
            var pieceRenderer = piece.GetComponent<SpriteRenderer>();
            var glowObject = new GameObject("TutorialGlow");
            glowObject.transform.SetParent(piece, false);

            Vector3 size = pieceRenderer.sprite.bounds.size;
            glowObject.transform.localScale = Vector3.one * Mathf.Max(size.x, size.y) * glowScale;

            var glow = glowObject.AddComponent<SpriteRenderer>();
            glow.sprite = _glowSprite;
            glow.sortingOrder = pieceRenderer.sortingOrder + 1;
            glow.color = new Color(1f, 1f, 1f, 0f);
            glows.Add(glow);
        }
        return glows;
    }

    private static void SetGlowAlpha(List<SpriteRenderer> glows, float alpha)
    {
        foreach (SpriteRenderer glow in glows)
            if (glow != null) glow.color = new Color(1f, 1f, 1f, alpha);
    }

    private static Sprite CreateGlowSprite()
    {
        const int size = 64; // px = PPU라 1유닛
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - distance * distance)));
            }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
