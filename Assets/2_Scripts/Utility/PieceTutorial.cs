using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 첫 파편 튜토리얼 - 세이브 통틀어 처음 파편이 떨어지면, 파편이 떨어진 곳을 화면 중앙으로 잡고 크게 줌인한 뒤
// 파편 위에 흰 빛이 깜빡이게 함. 그중 하나를 주우면 카메라가 원래대로 돌아가고 다시는 안 나옴.
// 줌인 동안은 오브젝트 클릭을 막음 (Click이 Active를 확인)
public class PieceTutorial : MonoBehaviour
{
    public static PieceTutorial Instance { get; private set; }

    // 튜토리얼 진행 중인지 - 이 동안 Click은 오브젝트 클릭을 무시함
    public static bool Active { get; private set; }

    [SerializeField] private float zoomDuration = 0.5f; // 줌인/복귀에 걸리는 시간(초)
    [SerializeField] private float padding = 0.4f; // 파편들 둘레로 화면에 더 보여줄 여백 (월드 유닛)
    [SerializeField] private float minOrthoSize = 0.8f; // 파편이 하나뿐이어도 이 이상은 확대하지 않음 (카메라 세로 절반 크기)
    [SerializeField] private float blinkPerSecond = 1.5f; // 초당 깜빡임 횟수
    [SerializeField] private float glowMaxAlpha = 0.85f; // 흰 빛이 가장 밝을 때 불투명도
    [SerializeField] private float glowScale = 1.6f; // 흰 빛 크기 (파편 그림 대비 배율)

    public bool Done { get; set; } // 이미 봤는지 - 세이브에 저장됨

    private readonly List<Transform> _targets = new List<Transform>(); // 튜토리얼 대상 파편들
    private bool _collected; // 대상 파편 중 하나를 주웠는지
    private Sprite _glowSprite; // 가운데가 밝고 가장자리로 갈수록 투명해지는 흰 원 (코드로 생성)

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
        Done = true; // 주운 순간 바로 기록해야 조각 획득으로 인한 저장에 같이 들어감
    }

    private IEnumerator Run()
    {
        // 파편이 바닥에 다 떨어질 때까지 기다림 (업그레이드 창 등으로 timeScale이 0이어도 진행되게 실시간 기준)
        yield return new WaitForSecondsRealtime(DebrisPool.Instance.FallDuration + 0.05f);

        Camera cam = Camera.main;
        Vector3 homePosition = cam.transform.position; // 원래 카메라 위치
        float homeSize = cam.orthographicSize; // 원래 카메라 크기

        // 파편들이 떨어진 범위를 화면 중앙에 꽉 차게
        var bounds = new Bounds(_targets[0].position, Vector3.zero);
        foreach (Transform t in _targets) bounds.Encapsulate(t.position);
        float targetSize = Mathf.Min(homeSize, Mathf.Max(minOrthoSize, bounds.extents.y + padding, (bounds.extents.x + padding) / cam.aspect));

        // 배경은 원래 화면에 딱 맞춰져 있어서, 파편이 화면 가장자리에 있을 때 그대로 가운데로 잡으면 배경 밖(스카이박스)이 보임 -
        // 줌인 화면이 원래 화면 범위를 벗어나지 않도록 중심을 안쪽으로 밀어넣음 (파편은 여전히 화면 안에 들어옴)
        float roomY = homeSize - targetSize; // 원래 화면 안에서 줌인 화면 중심이 움직일 수 있는 세로 여유
        float roomX = (homeSize - targetSize) * cam.aspect; // 가로 여유
        Vector3 targetPosition = new Vector3(
            Mathf.Clamp(bounds.center.x, homePosition.x - roomX, homePosition.x + roomX),
            Mathf.Clamp(bounds.center.y, homePosition.y - roomY, homePosition.y + roomY),
            homePosition.z);

        List<SpriteRenderer> glows = CreateGlows();
        yield return MoveCamera(cam, homePosition, homeSize, targetPosition, targetSize, glows);

        // 주울 때까지 깜빡임
        float elapsed = 0f; // 깜빡임 경과 시간
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

    private float Blink(float elapsed) => glowMaxAlpha * (0.5f - 0.5f * Mathf.Cos(elapsed * blinkPerSecond * 2f * Mathf.PI)); // 0에서 시작해 부드럽게 밝아졌다 어두워짐

    // 카메라를 from -> to로 부드럽게 이동/확대. 줌인 동안에도 흰 빛이 깜빡이기 시작하게 glows를 같이 갱신
    private IEnumerator MoveCamera(Camera cam, Vector3 fromPos, float fromSize, Vector3 toPos, float toSize, List<SpriteRenderer> glows)
    {
        float elapsed = 0f; // 이동 경과 시간
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

    // 대상 파편마다 위에 흰 빛을 하나씩 붙임 (파편을 따라다니도록 자식으로)
    private List<SpriteRenderer> CreateGlows()
    {
        var glows = new List<SpriteRenderer>(); // 만든 흰 빛들
        foreach (Transform piece in _targets)
        {
            var pieceRenderer = piece.GetComponent<SpriteRenderer>();
            var glowObject = new GameObject("TutorialGlow");
            glowObject.transform.SetParent(piece, false);

            Vector3 size = pieceRenderer.sprite.bounds.size; // 파편 그림 크기 (로컬)
            glowObject.transform.localScale = Vector3.one * Mathf.Max(size.x, size.y) * glowScale;

            var glow = glowObject.AddComponent<SpriteRenderer>();
            glow.sprite = _glowSprite;
            glow.sortingOrder = pieceRenderer.sortingOrder + 1; // 파편 위에 덮어서 파편이 하얗게 번쩍이게
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

    // 1유닛 크기의 흰 원 - 가운데는 불투명, 가장자리로 갈수록 투명
    private static Sprite CreateGlowSprite()
    {
        const int size = 64; // 텍스처 한 변 픽셀 수 (= Pixels Per Unit이라 월드 1유닛)
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float radius = size / 2f; // 원 반지름(픽셀)
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius; // 중심 0 ~ 가장자리 1
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - distance * distance)));
            }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
