using System.Collections.Generic;
using UnityEngine;

// 오브젝트를 덮은 반투명 왁스 (왁뿌볼처럼). 그림 파일 없이 오브젝트 그림 모양을 따라 도트 느낌의 왁스 덩어리를 코드로 만들어 덮음.
// 맞을 때마다 맞은 자리에서 금이 사방으로 뻗어나가고(많이 깎이면 틈이 벌어짐), 반쯤부터는 왁스 조각이 떨어져 나가 안의 오브젝트가 더 드러남.
// 체력 0이 되면 왁스 파편이 사방으로 튀고(오브젝트 파괴는 Health가 그대로 처리), 다시 살아나면 새 왁스로 덮임.
// 책상/도로/아파트 같은 지형(ZoneBreaker)은 왁스 코팅 업그레이드를 해금해야 덮임.
// HealthSpriteSwitcher가 자동으로 붙이므로 씬에 따로 넣을 필요 없음
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Health))]
public class WaxCoating : MonoBehaviour
{
    private const float PixelWorldSize = 0.03f; // 왁스 도트 한 칸의 월드 크기 - 오브젝트 크기와 상관없이 도트 크기가 같게
    private const int Bulge = 5; // 왁스가 오브젝트 그림 밖으로 둥글게 부풀어 나오는 칸 수 (왁스 두께 - 버터처럼 두툼하게)
    private const float DomeRadius = 12f; // 가장자리에서 이 칸 수만큼 들어가면 왁스가 가장 두꺼움 (클수록 베개처럼 완만하게 둥긂)
    private const float ThinAlpha = 0.86f; // 가장자리 불투명도 - 두꺼운 왁스라 거의 안 비침
    private const float ThickAlpha = 0.95f; // 안쪽 불투명도
    private const int BlurCells = 10; // 안의 오브젝트가 왁스에 흐릿하게 비치는 번짐 정도(칸)
    private const float ShowThrough = 0.2f; // 안의 오브젝트 색이 왁스 위로 비치는 정도 (0이면 안 보임)
    private static readonly Vector3 LightDir = new Vector3(-0.45f, 0.65f, 0.6f).normalized; // 빛이 오는 방향 (왼쪽 위 앞)
    private const float ChunkStart = 0.5f; // 체력이 이만큼 깎이면 왁스 조각이 떨어지기 시작함
    private const float OpenCracksAt = 0.45f; // 체력이 이만큼 깎인 뒤에 생기는 금은 시작 부분이 벌어진 틈으로 보임
    private const int ChunkCount = 9; // 왁스를 나누는 조각 수 (떨어져 나가는 단위)
    private const int ShatterPieces = 16; // 부서질 때 튀는 왁스 파편 수
    private const int MaxChunkPlans = 4; // 그림마다 미리 만들어 두는 조각 나눔 수 (이 수만큼 만든 뒤로는 그중 하나를 골라 씀)

    // 오브젝트 그림 하나에서 뽑아낸 왁스 모양 (같은 그림을 쓰는 오브젝트끼리 공유)
    private class Shape
    {
        public int width, height; // 왁스 그림 칸 수
        public bool[] mask; // 왁스가 덮는 칸
        public float[] thickness; // 칸별 왁스 두께 0(가장자리)~1(안쪽) - 두꺼울수록 덜 비침
        public float[] diffuse; // 칸별 빛 받는 정도 (볼록한 면의 방향으로 계산)
        public float[] specular; // 칸별 반짝이는 정도 (매끈한 왁스 표면의 하이라이트)
        public Color[] inside; // 칸별로 왁스 너머 흐릿하게 비치는 안쪽 오브젝트 색 (a = 비치는 양)
        public bool[] solid; // 오브젝트 그림이 있는 칸 (오브젝트 금은 여기에만)
        public Color[] objectColor; // 칸별 오브젝트 그림 색 (번지기 전) - 오브젝트 금 색을 정할 때 씀
        public float pixelsPerUnit; // 왁스 그림의 Pixels Per Unit (칸 하나 = 오브젝트 그림 몇 픽셀)
        public Vector2 pivot; // 오브젝트 그림과 같은 자리에 겹치도록 맞춘 기준점 (0~1)
        public readonly List<(float[] chunkAt, float[] crackAt, float[] fallAt)> plans = new List<(float[] chunkAt, float[] crackAt, float[] fallAt)>(); // 미리 만들어 둔 조각 나눔 (다시 덮을 때마다 새로 계산하면 느려서 몇 개만 만들고 돌려 씀)
        public float scaleY; // 왁스 그림의 세로 배율 - 칸이 가로세로 다른 픽셀 수일 때(오브젝트를 한쪽으로만 늘린 경우) 칸이 화면에서 정사각형이 되게
    }

    private static readonly Dictionary<(Sprite, Vector2Int), Shape> _shapes = new Dictionary<(Sprite, Vector2Int), Shape>(); // 그림+칸 크기별 왁스 모양 캐시
    private static ParticleSystem _shatter; // 부서질 때 왁스 파편을 뿌리는 공용 파티클

    private SpriteRenderer _object; // 안쪽 오브젝트 그림
    private Health _health; // 체력 - 금/조각 진행도 기준
    private Click _click; // 오브젝트 번호를 알아내는 데 씀
    private bool _isTerrain; // 지형(책상/도로/아파트)인지 - 업그레이드로 해금해야 덮임
    private SpriteRenderer _wax; // 왁스 그림 (자식 오브젝트)
    private Texture2D _texture; // 왁스 그림 픽셀
    private Color32[] _pixels; // 왁스 그림 픽셀 작업 공간
    private SpriteRenderer _objectCracks; // 오브젝트 위(왁스 아래)에 그리는 오브젝트 금 (자식 오브젝트) - 왁스가 떨어져 나간 자리로 보임
    private Texture2D _crackTexture; // 오브젝트 금 그림 픽셀
    private Color32[] _crackPixels; // 오브젝트 금 그림 픽셀 작업 공간
    private Shape _shape; // 지금 오브젝트 그림에 맞춘 왁스 모양
    private Sprite _shapeSprite; // 왁스 모양을 만든 오브젝트 그림 - 그림이 바뀌면(가운데 오브젝트 교체 등) 다시 만듦
    private float[] _crackAt; // 조각 경계 칸이 미리 금으로 보이기 시작하는 깎인 비율 (경계가 아니면 2)
    private byte[] _crack; // 칸별 금 상태 - 0 없음, 1 실금, 2 벌어진 틈 (맞은 자리에서 뻗어나가며 쌓임)
    private bool _dirty; // 왁스 그림 전체를 다시 그려야 하는지 (새로 덮었거나 금이 다 지워졌을 때)
    private bool _cracksDirty; // 오브젝트 금 그림 전체를 다시 그려야 하는지
    private RectInt _hitRect; // 새 금이 생긴 범위(칸) - 맞을 때는 그 둘레만 다시 그림 (책상처럼 큰 그림을 통째로 다시 그리면 렉)
    private bool _hasHitRect; // _hitRect에 다시 그릴 곳이 있는지
    private float[] _fallAt; // 지금 조각 나눔의 조각별 떨어지는 깎인 비율 - 체력이 이 값을 지날 때만 그림이 바뀜
    private bool _noCracks = true; // 아직 금이 하나도 없는지 (금 없고 체력이 꽉 찬 왁스는 늘 같은 그림이라 저장해 둔 걸 씀)
    private Color32[] _pristine; // 금 없는 온전한 왁스 그림 (존을 오갈 때/다시 덮을 때 큰 그림을 매번 계산하지 않게)
    private Color[] _baseColors; // 칸별 왁스 기본 색 (음영/결/비침까지 - 금/가장자리 처리 전). 모양/색이 같으면 계속 씀
    private float[] _baseAlphas; // 칸별 왁스 기본 불투명도
    private Shape _baseShape; // _baseColors를 만든 모양
    private Color _baseColor; // _baseColors를 만든 왁스 색
    private int[] _rayStamp; // 칸별로 마지막에 칠한 금 줄기 번호 - 두껍게 칠한 자기 줄기를 다른 금으로 착각해 멈추지 않게
    private int _rayId; // 지금 그리는 금 줄기 번호
    private float[] _chunkAt; // 칸별로 떨어져 나가는 깎인 비율 (안 떨어지면 2)
    private float _shownDamage = -1f; // 마지막으로 그린 깎인 비율 - 같으면 다시 안 그림
    private bool _broken; // 부서져서 왁스가 사라진 상태인지
    private Color _color = Color.white; // 이 오브젝트의 왁스 색
    private Vector2 _baseScale; // 시작할 때의 월드 크기 배율(가로, 세로) - 도트 크기 계산 기준
    private bool _forceCover; // 보스전처럼 조건과 상관없이 왁스를 덮을지
    private SpriteRenderer _shield; // 보스전 보호막 덧칠 - 연결된 오브젝트 색 왁스를 모서리/끝부분에 덧바름 (자식 오브젝트)
    private Texture2D _shieldTexture; // 보호막 덧칠 그림 픽셀
    private Color32[] _shieldPixels; // 보호막 덧칠 그림 픽셀 작업 공간
    private Vector2Int[] _shieldAnchors; // 연결별 덧칠 중심 칸 (모서리 → 변 가운데 → 나머지 가장자리 순)
    private Shape _shieldShape; // 덧칠 중심/그림을 만든 왁스 모양 - 모양이 바뀌면 다시 만듦
    private bool[] _shieldShown; // 마지막으로 그린 연결별 켜짐 상태 - 같으면 다시 안 그림
    private bool _shieldOn; // 보호막 덧칠을 보여줄지
    private const float ShieldRadius = 0.12f; // 덧칠 한 덩이 반지름 (왁스 그림 짧은 변 대비)

    // 플레이를 새로 시작할 때 모양 캐시를 비움 (플레이 시작 때 스크립트를 다시 불러오지 않는 설정이라, 그 사이 그림을 다시 불러왔으면 옛 모양이 남음)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => _shapes.Clear();

    // 지금 왁스가 오브젝트를 덮고 있는지 (HealthSpriteSwitcher가 깨진 단계 그림을 안 쓰는 데 씀)
    public bool Covers => enabled && (_forceCover || (!NoWax && (!_isTerrain || TerrainUnlocked)));

    // 보스전 동안 켜서 지형/왁스 없는 오브젝트도 왁스를 입은 모습으로 만듦
    public bool ForceCover
    {
        get => _forceCover;
        set
        {
            if (_forceCover == value) return;
            _forceCover = value;
            _dirty = true; // 덮개가 바뀌었으니 왁스 그림을 처음부터 다시
        }
    }

    // 왁스를 안 덮는 오브젝트(ObjectManager noWax)인지 - 왁스는 없어도 맞으면 오브젝트에 금은 감
    private bool NoWax => ObjectManager.Instance != null && ObjectManager.Instance.NoWaxOf(ObjectIndex);
    // 맞으면 금이 가는지 (왁스가 덮고 있거나, 왁스 없는 오브젝트)
    private bool Cracks => enabled && (NoWax || Covers);

    private int ObjectIndex => _click != null ? _click.ObjectIndex : -1;
    private static readonly int[] WaxOnUnlockTerrain = { 4, 9 }; // 해금 노드를 사면 바로 왁스가 덮이는 지형 (책상, 도로)
    private bool TerrainUnlocked =>
        (UpgradeManager.Instance != null && UpgradeManager.Instance.WaxIsUnlockedFor(ObjectIndex))
        || (System.Array.IndexOf(WaxOnUnlockTerrain, ObjectIndex) >= 0 && ObjectManager.Instance != null && ObjectManager.Instance.IsUnlocked(ObjectIndex));

    // 맞을 때 튀는 파편 색 - 왁스가 덮고 있으면 왁스 색 (Click이 씀)
    public bool TryGetChipColor(out Color color)
    {
        color = _color;
        return Covers && !_broken;
    }

    void Awake()
    {
        _object = GetComponent<SpriteRenderer>();
        _health = GetComponent<Health>();
        _click = GetComponent<Click>();
        _isTerrain = GetComponent<ZoneBreaker>() != null;
        _baseScale = new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));

        var waxObject = new GameObject("Wax"); // 왁스 그림을 그릴 자식
        waxObject.transform.SetParent(transform, false);
        _wax = waxObject.AddComponent<SpriteRenderer>();
        _wax.sharedMaterial = _object.sharedMaterial;
        _wax.enabled = false;

        var cracksObject = new GameObject("ObjectCracks"); // 오브젝트 금을 그릴 자식
        cracksObject.transform.SetParent(transform, false);
        _objectCracks = cracksObject.AddComponent<SpriteRenderer>();
        _objectCracks.sharedMaterial = _object.sharedMaterial;
        _objectCracks.enabled = false;

        var shieldObject = new GameObject("WaxShield"); // 보호막 덧칠을 그릴 자식
        shieldObject.transform.SetParent(transform, false);
        _shield = shieldObject.AddComponent<SpriteRenderer>();
        _shield.sharedMaterial = _object.sharedMaterial;
        _shield.enabled = false;
    }

    void OnEnable()
    {
        _health.OnDamaged += HandleDamaged;
        _health.OnDied += HandleDied;
    }

    void OnDisable()
    {
        _health.OnDamaged -= HandleDamaged;
        _health.OnDied -= HandleDied;
        if (_wax != null) _wax.enabled = false;
        if (_objectCracks != null) _objectCracks.enabled = false;
        if (_shield != null) _shield.enabled = false;
    }

    void OnDestroy()
    {
        if (_shieldTexture != null) Destroy(_shieldTexture);
        if (_shield != null && _shield.sprite != null) Destroy(_shield.sprite);
        if (_texture != null) Destroy(_texture);
        if (_wax != null && _wax.sprite != null) Destroy(_wax.sprite);
        if (_crackTexture != null) Destroy(_crackTexture);
        if (_objectCracks != null && _objectCracks.sprite != null) Destroy(_objectCracks.sprite);
    }

    private void HandleDamaged(int current, int max)
    {
        if (max <= 0) return;
        if (current >= max && _broken) Recoat(); // 다시 살아나 체력이 꽉 찼으면 새 왁스로 덮음
        else if (current >= max && _crack != null) { System.Array.Clear(_crack, 0, _crack.Length); _dirty = _cracksDirty = _noCracks = true; } // 체력이 다시 꽉 차면(존을 다시 볼 때) 금도 없어짐
        Redraw(1f - (float)current / max);
    }

    private void HandleDied()
    {
        if (!Covers || _broken) return;
        _broken = true;
        _wax.enabled = false;
        if (!(ZoneManager.Instance != null && ZoneManager.Instance.IsHidden(transform))) // 안 보는 존이면 연출 생략
            Shatter();
    }

    void LateUpdate()
    {
        bool track = Cracks && !_broken && _object.enabled; // 금/왁스 모양을 지금 그림에 맞춰 둘지
        if (track && (_shape == null || _object.sprite != _shapeSprite)) // 클릭 펀치로 크기가 잠깐 바뀌는 건 무시 (다시 만들면 금 배치가 매번 바뀜)
            Recoat();
        bool show = Covers && track && _shape != null; // 지금 왁스를 보여줄지

        // 오브젝트 금은 왁스가 부서진 뒤에도 오브젝트가 사라질 때까지 남음
        bool showCracks = Cracks && _object.enabled && _shape != null && _objectCracks.sprite != null; // 오브젝트 금을 보여줄지
        _objectCracks.enabled = showCracks;
        if (showCracks)
        {
            _objectCracks.sortingLayerID = _object.sortingLayerID;
            _objectCracks.sortingOrder = _object.sortingOrder + 1;
            _objectCracks.forceRenderingOff = _object.forceRenderingOff;
            _objectCracks.flipX = _object.flipX;
            _objectCracks.flipY = _object.flipY;
            _objectCracks.color = new Color(1f, 1f, 1f, _object.color.a);
        }

        // 금/왁스 그림을 지금 체력에 맞게 그림 (왁스 없는 오브젝트도 금 그림이 비어 있게 처음에 한 번은 그려야 함)
        if (track && _shape != null && _health.MaxHP > 0) Redraw(1f - (float)_health.CurrentHP / _health.MaxHP);

        _wax.enabled = show;
        _shield.enabled = show && _shieldOn && _shield.sprite != null;
        if (!show) return;

        // 오브젝트 그림을 그대로 따라감: 정렬 순서는 오브젝트 금 위, 투명도(죽을 때/살아날 때 흐려짐)와 존 숨김도 같이
        _wax.sortingLayerID = _object.sortingLayerID;
        _wax.sortingOrder = _object.sortingOrder + 2;
        _wax.forceRenderingOff = _object.forceRenderingOff;
        _wax.flipX = _object.flipX;
        _wax.flipY = _object.flipY;
        _wax.color = new Color(1f, 1f, 1f, _object.color.a);

        if (!_shield.enabled) return;
        _shield.sortingLayerID = _object.sortingLayerID;
        _shield.sortingOrder = _object.sortingOrder + 3; // 왁스 위에 덧바름
        _shield.forceRenderingOff = _object.forceRenderingOff;
        _shield.flipX = _object.flipX;
        _shield.flipY = _object.flipY;
        _shield.color = _wax.color;
    }

    // 보스전 보호막: 연결별 색(colors)과 지금 이어져 있는지(on). 이어진 연결마다 보스 왁스 모서리/끝부분에 그 색 왁스가 조금 덧발림. null이면 다 지움
    public void SetShield(Color[] colors, bool[] on)
    {
        _shieldOn = colors != null && colors.Length > 0 && _shape != null && _pixels != null;
        if (!_shieldOn) { _shieldShown = null; return; }

        bool rebuild = _shieldShape != _shape || _shieldAnchors == null || _shieldAnchors.Length != colors.Length; // 덧칠 자리를 새로 정할지
        if (!rebuild && _shieldShown != null && System.Linq.Enumerable.SequenceEqual(_shieldShown, on)) return; // 그대로면 다시 안 그림

        int w = _shape.width, h = _shape.height; // 칸 수
        if (rebuild)
        {
            _shieldShape = _shape;
            _shieldAnchors = PickShieldAnchors(colors.Length);
            if (_shieldTexture == null || _shieldTexture.width != w || _shieldTexture.height != h)
            {
                if (_shieldTexture != null) Destroy(_shieldTexture);
                _shieldTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                _shieldPixels = new Color32[w * h];
            }
            if (_shield.sprite != null) Destroy(_shield.sprite);
            _shield.sprite = Sprite.Create(_shieldTexture, new Rect(0, 0, w, h), _shape.pivot, _shape.pixelsPerUnit, 0, SpriteMeshType.FullRect); // 왁스와 같은 칸/자리
            _shield.transform.localScale = _wax.transform.localScale;
        }
        _shieldShown = (bool[])on.Clone();

        System.Array.Clear(_shieldPixels, 0, _shieldPixels.Length);
        float radius = Mathf.Max(4f, Mathf.Min(w, h) * ShieldRadius); // 덧칠 한 덩이 반지름(칸)
        Vector3 half = (LightDir + Vector3.forward).normalized; // 반짝임 계산용
        for (int i = 0; i < colors.Length; i++)
        {
            if (!on[i]) continue;
            Vector2Int anchor = _shieldAnchors[i]; // 이 덩이 중심
            int reach = Mathf.CeilToInt(radius * 1.25f); // 울퉁불퉁한 테두리까지 포함한 최대 거리
            for (int y = Mathf.Max(0, anchor.y - reach); y <= Mathf.Min(h - 1, anchor.y + reach); y++)
                for (int x = Mathf.Max(0, anchor.x - reach); x <= Mathf.Min(w - 1, anchor.x + reach); x++)
                {
                    int cell = y * w + x; // 이 칸
                    if (!_shape.mask[cell]) continue; // 보스 왁스 위에만
                    float dx = x - anchor.x, dy = y - anchor.y; // 중심에서 떨어진 칸 수
                    float r = radius * (0.75f + 0.5f * Mathf.PerlinNoise(x * 0.15f + i * 13.7f, y * 0.15f + i * 7.3f)); // 울퉁불퉁한 덩이 테두리
                    float t = Mathf.Sqrt(dx * dx + dy * dy) / r; // 0 가운데 ~ 1 테두리
                    if (t > 1f) continue;
                    // 일반 오브젝트 왁스와 같은 방식으로 칠함 - 보스 왁스 면의 음영/광을 그대로 따라가서 왁스 위에 다른 색 왁스를 한 겹 더 바른 느낌.
                    // 덧바른 덩이의 둥근 두께도 살짝 섞어 도톰하게
                    Vector3 normal = new Vector3(dx / r, dy / r, Mathf.Sqrt(1f - t * t)).normalized; // 덧바른 덩이 면 방향
                    float patchLight = Mathf.Clamp01(Vector3.Dot(normal, LightDir) * 0.5f + 0.5f); // 덩이 자체가 빛 받는 정도
                    float diffuse = Mathf.Lerp(_shape.diffuse[cell], patchLight, 0.3f); // 아래 왁스 면 음영 위주 + 덩이 두께 조금
                    float thickness = _shape.thickness[cell]; // 아래 왁스 두께
                    float mottle = 1f + (Mathf.PerlinNoise(x * 0.1f + 50f, y * 0.1f) - 0.5f) * 0.05f; // 왁스 표면의 옅은 결
                    Color color = colors[i] * Mathf.Lerp(0.65f, 1.12f, diffuse) * mottle; // 빛 받는 면은 밝게, 반대쪽은 어둡게
                    color = Color.Lerp(color, Color.white, thickness * 0.12f); // 볼록한 가운데는 살짝 밝게
                    if (t > 0.85f) color *= 0.9f; // 덧바른 가장자리는 아주 살짝 진하게
                    float spec = Mathf.Max(_shape.specular[cell], Mathf.Pow(Mathf.Max(0f, Vector3.Dot(normal, half)), 12f) * 0.25f); // 은은한 광
                    color = Color.Lerp(color, Color.white, spec * 0.6f);
                    color.a = Mathf.Max(Mathf.Lerp(ThinAlpha, ThickAlpha, thickness), spec); // 일반 왁스처럼 아주 살짝 비침
                    _shieldPixels[cell] = color;
                }
        }
        _shieldTexture.SetPixels32(_shieldPixels);
        _shieldTexture.Apply();
    }

    // 덧칠 중심을 왁스 가장자리에서 고름: 네 모서리 → 네 변 가운데 → 그 뒤는 아무 방향 끝
    private Vector2Int[] PickShieldAnchors(int count)
    {
        var directions = new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1),
                                 new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0) }; // 앞쪽부터 쓰는 방향
        int w = _shape.width, h = _shape.height; // 칸 수
        var anchors = new Vector2Int[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = i < directions.Length ? directions[i] : Random.insideUnitCircle.normalized; // 이 덩이를 놓을 방향
            float best = float.MinValue; // 그 방향으로 가장 멀리 나간 정도
            for (int cell = 0; cell < _shape.mask.Length; cell++)
            {
                if (!_shape.mask[cell]) continue;
                float score = ((float)(cell % w) / w - 0.5f) * direction.x + ((float)(cell / w) / h - 0.5f) * direction.y; // 가로세로 비율을 맞춘 방향 거리
                if (score > best) { best = score; anchors[i] = new Vector2Int(cell % w, cell / w); }
            }
        }
        return anchors;
    }

    // 오브젝트 그림 픽셀 몇 개를 왁스 한 칸으로 묶을지(가로, 세로) - 화면에서 도트 크기가 오브젝트마다 같고 정사각형이게.
    // 컨베이어 벨트처럼 한쪽으로만 늘린 오브젝트는 가로/세로 픽셀 수가 달라짐
    private Vector2Int CellPixels()
    {
        Sprite sprite = _object.sprite; // 안쪽 오브젝트 그림
        Vector2 scale = Vector2.Max(_baseScale, new Vector2(0.0001f, 0.0001f)); // 월드에서 커진 배율 (등장/타격 연출로 잠깐 바뀐 크기 말고 원래 크기)
        return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(PixelWorldSize / scale.x * sprite.pixelsPerUnit)),
                              Mathf.Max(1, Mathf.RoundToInt(PixelWorldSize / scale.y * sprite.pixelsPerUnit)));
    }

    // 새 왁스로 덮음 - 모양을 맞추고, 금/조각 배치를 새로 정함
    private void Recoat()
    {
        _broken = false;
        _shownDamage = -1f;
        _color = WaxColor();

        Sprite sprite = _object.sprite; // 안쪽 오브젝트 그림
        _shapeSprite = sprite;
        if (sprite == null) { _shape = null; return; }
        Shape shape = GetShape(sprite, CellPixels()); // 지금 그림에 맞는 왁스 모양
        bool sameShape = shape == _shape && _wax.sprite != null && _crack != null; // 다시 살아난 것처럼 모양이 그대로면 그림/배열을 그대로 씀
        _shape = shape;
        if (_shape == null) return;
        _dirty = _cracksDirty = _noCracks = true;
        if (sameShape)
        {
            System.Array.Clear(_crack, 0, _crack.Length);
            System.Array.Clear(_rayStamp, 0, _rayStamp.Length);
            PlanChunks();
            return;
        }

        int count = _shape.width * _shape.height; // 칸 수
        if (_texture == null || _texture.width != _shape.width || _texture.height != _shape.height)
        {
            if (_texture != null) Destroy(_texture);
            _texture = new Texture2D(_shape.width, _shape.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _pixels = new Color32[count];
            if (_crackTexture != null) Destroy(_crackTexture);
            _crackTexture = new Texture2D(_shape.width, _shape.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _crackPixels = new Color32[count];
        }
        if (_wax.sprite != null) Destroy(_wax.sprite);
        _wax.sprite = Sprite.Create(_texture, new Rect(0, 0, _shape.width, _shape.height), _shape.pivot, _shape.pixelsPerUnit, 0, SpriteMeshType.FullRect); // 그림이 계속 바뀌므로 투명 부분을 잘라내는 Tight 모양을 쓰면 안 됨
        if (_objectCracks.sprite != null) Destroy(_objectCracks.sprite);
        _objectCracks.sprite = Sprite.Create(_crackTexture, new Rect(0, 0, _shape.width, _shape.height), _shape.pivot, _shape.pixelsPerUnit, 0, SpriteMeshType.FullRect); // 왁스와 같은 칸/자리
        _wax.transform.localScale = _objectCracks.transform.localScale = new Vector3(1f, _shape.scaleY, 1f); // 칸이 가로세로 다른 픽셀 수면 세로를 맞춰 늘림

        _crack = new byte[count];
        _rayStamp = new int[count];
        _pristine = null;
        PlanChunks();
    }

    // 맞은 자리(월드 좌표)에서 금이 사방으로 뻗어나감 - Click이 때릴 때마다 부름. hitFraction은 이번 한 방이 최대 체력의 몇 %였는지.
    // 세게 맞을수록, 많이 깎였을수록 길게 뻗고, 뻗다가 다른 금을 만나면 대부분 거기서 멈춰 금끼리 이어짐
    public void Hit(Vector2 worldPoint, float hitFraction)
    {
        if (!Cracks || _broken || _shape == null || _crack == null || _health.MaxHP <= 0) return;

        Vector3 local = _wax.transform.InverseTransformPoint(worldPoint); // 왁스 그림 기준 위치(월드 단위)
        if (_wax.flipX) local.x = -local.x;
        if (_wax.flipY) local.y = -local.y;
        int start = NearestMaskCell(Mathf.FloorToInt(local.x * _shape.pixelsPerUnit + _shape.pivot.x * _shape.width),
                                    Mathf.FloorToInt(local.y * _shape.pixelsPerUnit + _shape.pivot.y * _shape.height)); // 금이 시작되는 칸
        if (start < 0) return;

        int w = _shape.width; // 가로 칸 수
        float damage = 1f - (float)_health.CurrentHP / _health.MaxHP; // 지금까지 깎인 비율
        float reach = Mathf.Lerp(5f, Mathf.Max(_shape.width, _shape.height) * 0.45f, Mathf.Clamp01(hitFraction * 3f + damage * 0.5f)); // 금이 뻗는 길이(칸)
        bool open = damage >= OpenCracksAt; // 많이 깎인 뒤라 금 시작 부분이 벌어지는지
        int rays = Random.Range(3, 6); // 뻗어나가는 줄기 수
        float baseAngle = Random.Range(0f, Mathf.PI * 2f); // 첫 줄기 방향
        for (int r = 0; r < rays; r++)
        {
            float angle = baseAngle + r * Mathf.PI * 2f / rays + Random.Range(-0.4f, 0.4f); // 고르게 퍼지되 살짝 흐트러진 방향
            CrackRay(start % w + 0.5f, start / w + 0.5f, angle, Mathf.RoundToInt(reach * Random.Range(0.5f, 1.1f)), open, 1);
        }
        // 맞은 점은 둥글게 패임
        int sx = start % w, sy = start / w; // 맞은 칸 위치
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = sx + dx, ny = sy + dy; // 둘레 칸
                if (nx >= 0 && ny >= 0 && nx < w && ny < _shape.height && _shape.mask[ny * w + nx])
                    _crack[ny * w + nx] = (byte)Mathf.Max(_crack[ny * w + nx], open ? 2 : 1);
            }
        int radius = Mathf.CeilToInt(reach * 1.1f) + 3; // 이번 금이 닿을 수 있는 최대 거리(칸) - 가지/굵기 포함
        var rect = new RectInt(sx - radius, sy - radius, radius * 2 + 1, radius * 2 + 1); // 이번 금 범위
        if (_hasHitRect)
        {
            int xMin = Mathf.Min(_hitRect.xMin, rect.xMin), yMin = Mathf.Min(_hitRect.yMin, rect.yMin); // 합친 범위
            rect = new RectInt(xMin, yMin, Mathf.Max(_hitRect.xMax, rect.xMax) - xMin, Mathf.Max(_hitRect.yMax, rect.yMax) - yMin);
        }
        _hitRect = rect;
        _hasHitRect = true;
        _noCracks = false;
    }

    // 금 한 줄기를 (x, y)에서 angle 방향으로 length칸 그림. 조금씩 휘고 가끔 가지를 치며, 다른 금을 만나면 대부분 멈춤
    private void CrackRay(float x, float y, float angle, int length, bool open, int branches)
    {
        int w = _shape.width, h = _shape.height; // 칸 수
        int ray = ++_rayId; // 이 줄기 번호
        for (int step = 1; step <= length; step++)
        {
            angle += Random.Range(-0.35f, 0.35f);
            x += Mathf.Cos(angle);
            y += Mathf.Sin(angle);
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int cell = (int)y * w + (int)x; // 지금 칸
            if (!_shape.mask[cell]) return;
            if (_crack[cell] > 0 && _rayStamp[cell] != ray && step > 2 && Random.value < 0.75f) return; // 다른 금에 닿으면 거기서 이어지고 멈춤

            bool wide = open && step < length * 0.4f; // 시작 부분은 벌어진 틈
            float progress = (float)step / length; // 줄기 끝으로 갈수록 1
            int thickness = progress > 0.8f ? 1 : wide ? 3 : 2; // 금 굵기(칸) - 끝으로 갈수록 가늘어짐
            float px = -Mathf.Sin(angle), py = Mathf.Cos(angle); // 진행 방향에 수직인 방향 (굵기를 이쪽으로 칠함)
            for (int k = 0; k < thickness; k++)
            {
                float offset = k - (thickness - 1) * 0.5f; // 가운데 기준 옆으로 몇 칸
                int bx = Mathf.FloorToInt(x + px * offset), by = Mathf.FloorToInt(y + py * offset); // 칠할 칸
                if (bx < 0 || by < 0 || bx >= w || by >= h) continue;
                int brush = by * w + bx; // 칠할 칸 번호
                if (!_shape.mask[brush]) continue;
                _crack[brush] = (byte)Mathf.Max(_crack[brush], wide ? 2 : 1);
                _rayStamp[brush] = ray;
            }
            if (branches > 0 && step > 2 && Random.value < 0.08f)
                CrackRay(x, y, angle + Random.Range(0.5f, 1.1f) * (Random.value < 0.5f ? -1f : 1f), (length - step) / 2, false, branches - 1);
        }
    }

    // (x, y) 근처에서 왁스가 덮인 가장 가까운 칸 (가장자리 밖을 때렸으면 안쪽으로 당김). 없으면 -1
    private int NearestMaskCell(int x, int y)
    {
        int w = _shape.width, h = _shape.height; // 칸 수
        for (int radius = 0; radius <= 10; radius++)
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue; // 이번 반경의 테두리만
                    int nx = x + dx, ny = y + dy; // 볼 칸
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && _shape.mask[ny * w + nx]) return ny * w + nx;
                }
        return -1;
    }

    // 왁스를 몇 조각으로 나눠(가장 가까운 기준점끼리 한 조각, 경계는 울퉁불퉁하게) 조각마다 떨어지는 시점을 정함. 한 조각은 끝까지 남음.
    // 조각 경계는 떨어지기 조금 전에 금으로 먼저 보여서, 금이 간 자리대로 깨져 나가는 것처럼 보임
    private void PlanChunks()
    {
        if (_shape.plans.Count >= MaxChunkPlans) // 이미 충분히 만들어 뒀으면 그중 하나를 골라 씀
        {
            (_chunkAt, _crackAt, _fallAt) = _shape.plans[Random.Range(0, _shape.plans.Count)];
            return;
        }

        int w = _shape.width, h = _shape.height; // 칸 수
        var seeds = new Vector2[ChunkCount]; // 조각 기준점
        var fallAt = new float[ChunkCount]; // 조각별 떨어지는 깎인 비율
        for (int i = 0; i < ChunkCount; i++)
        {
            int cell = RandomMaskCell(); // 기준점 칸
            seeds[i] = cell < 0 ? Vector2.zero : new Vector2(cell % w, cell / w);
            fallAt[i] = i == 0 ? 2f : Random.Range(ChunkStart, 0.95f);
        }

        var owner = new int[_shape.mask.Length]; // 칸별로 속한 조각
        float noiseSeed = Random.Range(0f, 1000f); // 경계를 울퉁불퉁하게 흔드는 노이즈 위치
        for (int cell = 0; cell < owner.Length; cell++)
        {
            if (!_shape.mask[cell]) continue;
            float px = cell % w, py = cell / w; // 이 칸 위치
            var p = new Vector2(px + (Mathf.PerlinNoise(px * 0.12f + noiseSeed, py * 0.12f) - 0.5f) * 8f,
                                py + (Mathf.PerlinNoise(px * 0.12f, py * 0.12f + noiseSeed) - 0.5f) * 8f); // 흔든 위치로 가장 가까운 기준점을 찾음
            int nearest = 0; // 가장 가까운 기준점
            for (int i = 1; i < ChunkCount; i++)
                if ((seeds[i] - p).sqrMagnitude < (seeds[nearest] - p).sqrMagnitude) nearest = i;
            owner[cell] = nearest;
        }

        _chunkAt = new float[_shape.mask.Length];
        _crackAt = new float[_shape.mask.Length];
        for (int i = 0; i < _crackAt.Length; i++) _crackAt[i] = 2f;
        for (int cell = 0; cell < owner.Length; cell++)
        {
            if (!_shape.mask[cell]) continue;
            _chunkAt[cell] = fallAt[owner[cell]];
            int x = cell % w, y = cell / w; // 이 칸 위치
            float breakAt = 2f; // 이 칸이 옆 조각과 갈라지는 시점 (경계가 아니면 2)
            if (x + 1 < w && _shape.mask[cell + 1] && owner[cell + 1] != owner[cell])
                breakAt = Mathf.Min(fallAt[owner[cell]], fallAt[owner[cell + 1]]);
            if (y + 1 < h && _shape.mask[cell + w] && owner[cell + w] != owner[cell])
                breakAt = Mathf.Min(breakAt, Mathf.Min(fallAt[owner[cell]], fallAt[owner[cell + w]]));
            if (breakAt < 2f) _crackAt[cell] = Mathf.Min(_crackAt[cell], breakAt - 0.15f);
        }
        _fallAt = fallAt;
        _shape.plans.Add((_chunkAt, _crackAt, _fallAt));
    }

    private int RandomMaskCell()
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int cell = Random.Range(0, _shape.mask.Length); // 아무 칸
            if (_shape.mask[cell]) return cell;
        }
        return -1;
    }

    // 깎인 비율(0~1)에 맞게 왁스 그림을 다시 그림. 볼록한 면 음영 + 반짝임 + 두께에 따른 투명도,
    // 금은 하얗게 질린 선과 그 아래 그림자, 떨어져 나간 자리 둘레는 왁스 단면처럼 진하게
    private void Redraw(float damage)
    {
        if (_shape == null || _broken || _pixels == null) return;
        damage = Mathf.Round(damage * 50f) / 50f; // 2% 단위
        // 체력은 조각이 떨어지거나 경계 금이 보이기 시작하는 값을 지날 때만 그림을 바꿈 - 그 외엔 새 금 둘레만 다시 그림
        bool full = _dirty || _shownDamage < 0f || ThresholdCrossed(_shownDamage, damage); // 전체를 다시 그릴지
        _shownDamage = damage;
        if (!full && !_hasHitRect && !_cracksDirty) return;

        int w = _shape.width, h = _shape.height; // 칸 수
        var all = new RectInt(0, 0, w, h); // 그림 전체
        RectInt region = all; // 다시 그릴 범위
        if (!full)
        {
            region = new RectInt(_hitRect.xMin - 2, _hitRect.yMin - 2, _hitRect.width + 4, _hitRect.height + 4); // 둘레 음영까지
            region.ClampToBounds(all);
        }
        if (Covers && (full || _hasHitRect)) RedrawWax(damage, w, h, region); // 왁스 없는 오브젝트는 금만
        if (_cracksDirty) RedrawCracks(w, h, all);
        else if (_hasHitRect) RedrawCracks(w, h, region);
        _dirty = _cracksDirty = _hasHitRect = false;
    }

    // 깎인 비율이 old에서 now로 바뀌는 사이에 조각이 떨어지거나 경계 금이 보이기 시작하는지
    private bool ThresholdCrossed(float old, float now)
    {
        if (_fallAt == null) return true;
        foreach (float fall in _fallAt)
        {
            if ((fall <= old) != (fall <= now)) return true;
            float crack = fall - 0.15f; // 경계 금이 보이기 시작하는 값 (PlanChunks와 같은 계산)
            if ((crack <= old) != (crack <= now)) return true;
        }
        return false;
    }

    // 칸별 왁스 기본 색/불투명도를 미리 계산 (모양이나 색이 바뀔 때만) - 매번 노이즈/음영을 다시 계산하지 않게
    private void BuildBase()
    {
        _baseShape = _shape;
        _baseColor = _color;
        int count = _shape.width * _shape.height; // 칸 수
        _baseColors = new Color[count];
        _baseAlphas = new float[count];
        for (int cell = 0; cell < count; cell++)
        {
            if (!_shape.mask[cell]) continue;
            int x = cell % _shape.width, y = cell / _shape.width; // 이 칸 위치
            float t = _shape.thickness[cell]; // 두께
            float mottle = 1f + (Mathf.PerlinNoise(x * 0.1f, y * 0.1f) - 0.5f) * 0.05f; // 왁스 표면의 아주 옅은 결
            Color color = _color * Mathf.Lerp(0.65f, 1.12f, _shape.diffuse[cell]) * mottle; // 빛 받는 면은 밝게, 반대쪽은 부드럽게 어둡게
            color = Color.Lerp(color, Color.white, t * 0.12f); // 볼록한 가운데는 살짝 밝게 (크림처럼)
            Color inside = _shape.inside[cell]; // 왁스 너머로 흐릿하게 비치는 안쪽 오브젝트
            color = Color.Lerp(color, (Color)inside * _color * 1.1f, inside.a * ShowThrough); // 안쪽 색이 왁스 색에 물들어 흐릿하게 보임
            color.a = 1f;
            _baseColors[cell] = color;
            _baseAlphas[cell] = Mathf.Lerp(ThinAlpha, ThickAlpha, t); // 두꺼울수록 덜 비침
        }
    }

    private void RedrawWax(float damage, int w, int h, RectInt region)
    {
        if (_baseColors == null || _baseShape != _shape || _baseColor != _color) { BuildBase(); _pristine = null; }
        bool pristine = _noCracks && damage <= 0f && region.width == w && region.height == h; // 금 없는 온전한 왁스 전체
        if (pristine && _pristine != null)
        {
            System.Array.Copy(_pristine, _pixels, _pixels.Length);
            _texture.SetPixels32(_pixels);
            _texture.Apply();
            return;
        }
        for (int y = region.yMin; y < region.yMax; y++)
        for (int x = region.xMin; x < region.xMax; x++)
        {
            int cell = y * w + x; // 이 칸
            if (!Visible(cell, damage)) { _pixels[cell] = default; continue; }
            Color color = _baseColors[cell]; // 기본 색
            float alpha = _baseAlphas[cell]; // 기본 불투명도

            bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                     || !Visible(cell - 1, damage) || !Visible(cell + 1, damage) || !Visible(cell - w, damage) || !Visible(cell + w, damage); // 바깥/떨어진 자리와 맞닿은 칸
            bool brokenEdge = edge && (Fallen(cell - 1, damage) || Fallen(cell + 1, damage) || Fallen(cell - w, damage) || Fallen(cell + w, damage)); // 조각이 떨어져 나간 둘레
            if (brokenEdge) { color *= 0.78f; alpha = 1f; } // 깨진 단면은 두께가 보이게 조금 진하게
            else if (edge) { color *= 0.9f; } // 가장자리는 아주 살짝만 진하게 (딱딱한 윤곽선 없이 부드럽게)

            int crack = CrackAt(cell, damage); // 이 칸의 금 (0 없음, 1 실금, 2 벌어진 틈)
            // 왁스 금은 하얗게 질린 선 + 그 아래/오른쪽에 살짝 진 그림자로 갈라진 단차가 보이게 그림
            if (crack == 2) { color = Color.Lerp(_color, Color.white, 0.55f); alpha = 1f; } // 벌어진 틈: 하얗게 질린 넓은 금
            else if (crack == 1) { color = Color.Lerp(_color, Color.white, 0.68f); alpha = 1f; } // 실금: 더 하얗고 가는 금
            else
            {
                bool shadow = (x > 0 && CrackAt(cell - 1, damage) > 0) || (y < h - 1 && CrackAt(cell + w, damage) > 0); // 금의 오른쪽/아래 칸 - 갈라진 턱이 그늘짐
                bool rim = (x < w - 1 && CrackAt(cell + 1, damage) > 0) || (y > 0 && CrackAt(cell - w, damage) > 0); // 금의 왼쪽/위 칸 - 빛 받는 턱
                if (shadow) color *= 0.8f;
                else if (rim) color = Color.Lerp(color, Color.white, 0.22f);
            }

            float spec = _shape.specular[cell]; // 반짝임
            color = Color.Lerp(color, Color.white, spec * 0.6f); // 은은한 광 (버터 왁스는 번들거리지 않음)
            alpha = Mathf.Max(alpha, spec);
            color.a = alpha;
            _pixels[cell] = color;
        }
        if (pristine) _pristine = (Color32[])_pixels.Clone();
        _texture.SetPixels32(_pixels);
        _texture.Apply();
    }

    private void RedrawCracks(int w, int h, RectInt region)
    {
        if (_noCracks) // 금이 없으면 다 투명
        {
            System.Array.Clear(_crackPixels, 0, _crackPixels.Length);
            _crackTexture.SetPixels32(_crackPixels);
            _crackTexture.Apply();
            return;
        }
        // 오브젝트 금: 왁스 금과 같은 자리, 같은 방식 - 오브젝트 색이 하얗게 질린 선 + 아래/오른쪽 그림자 + 위/왼쪽 빛 받는 턱
        for (int y = region.yMin; y < region.yMax; y++)
        for (int x = region.xMin; x < region.xMax; x++)
        {
            int cell = y * w + x; // 이 칸
            if (!_shape.solid[cell]) { _crackPixels[cell] = default; continue; }
            Color baseColor = _shape.objectColor[cell]; // 이 칸의 오브젝트 색
            Color color; // 칠할 색 (금이 아니고 턱도 아니면 투명)
            byte crack = _crack[cell]; // 맞아서 생긴 금 (조각 경계에 미리 보이는 금은 왁스에만)
            if (crack == 2) color = Color.Lerp(baseColor, Color.white, 0.55f); // 벌어진 틈
            else if (crack == 1) color = Color.Lerp(baseColor, Color.white, 0.68f); // 실금
            else if ((x > 0 && _crack[cell - 1] > 0) || (y < h - 1 && _crack[cell + w] > 0)) color = baseColor * 0.8f; // 금의 오른쪽/아래 - 그늘진 턱
            else if ((x < w - 1 && _crack[cell + 1] > 0) || (y > 0 && _crack[cell - w] > 0)) color = Color.Lerp(baseColor, Color.white, 0.22f); // 금의 왼쪽/위 - 빛 받는 턱
            else { _crackPixels[cell] = default; continue; }
            color.a = 1f;
            _crackPixels[cell] = color;
        }
        _crackTexture.SetPixels32(_crackPixels);
        _crackTexture.Apply();
    }

    // 이 칸의 금 상태 - 맞아서 생긴 금, 또는 곧 떨어질 조각 경계에 미리 보이는 금
    private int CrackAt(int cell, float damage) =>
        _crack[cell] > 0 ? _crack[cell] : _crackAt[cell] <= damage ? 1 : 0;

    // 이 칸에 왁스가 남아 있는지 (범위 밖은 없음)
    private bool Visible(int cell, float damage) =>
        cell >= 0 && cell < _pixels.Length && _shape.mask[cell] && _chunkAt[cell] > damage;

    // 이 칸은 왁스가 있었는데 떨어져 나갔는지
    private bool Fallen(int cell, float damage) =>
        cell >= 0 && cell < _pixels.Length && _shape.mask[cell] && _chunkAt[cell] <= damage;

    // 부서질 때 왁스 색 파편을 사방으로 뿌림
    private void Shatter()
    {
        if (_shatter == null) CreateShatterSystem(_wax.sharedMaterial);
        Bounds bounds = _object.bounds; // 오브젝트가 차지하는 범위
        for (int i = 0; i < ShatterPieces; i++)
        {
            Vector3 position = bounds.center + Vector3.Scale(bounds.extents, new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.8f, 0.8f), 0f)); // 파편이 나오는 자리
            Vector3 velocity = (position - bounds.center).normalized * Random.Range(2f, 5f) + Vector3.up * Random.Range(1f, 3f); // 바깥쪽으로 튀어나감
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = Color.Lerp(_color, Color.white, Random.Range(0f, 0.3f)),
                startSize = PixelWorldSize * Random.Range(3f, 7f),
                startLifetime = Random.Range(0.4f, 0.7f),
                rotation = Random.Range(0f, 360f),
            };
            _shatter.Emit(emit, 1);
        }
    }

    private static void CreateShatterSystem(Material spriteMaterial)
    {
        var systemObject = new GameObject("WaxShatter"); // 공용 왁스 파편 파티클
        DontDestroyOnLoad(systemObject);
        _shatter = systemObject.AddComponent<ParticleSystem>();
        _shatter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 설정을 바꾸려면 멈춰 있어야 함

        ParticleSystem.MainModule main = _shatter.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.5f; // 떨어지는 느낌
        main.maxParticles = 500;
        ParticleSystem.EmissionModule emission = _shatter.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = _shatter.shape;
        shape.enabled = false;
        ParticleSystem.ColorOverLifetimeModule fade = _shatter.colorOverLifetime; // 끝으로 갈수록 흐려짐
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var renderer = systemObject.GetComponent<ParticleSystemRenderer>(); // 네모 도트 파편으로 그림
        renderer.material = new Material(spriteMaterial) { mainTexture = Texture2D.whiteTexture };
        renderer.sortingOrder = 25; // 오브젝트/왁스 위, 데미지 숫자 아래
        _shatter.Play();
    }

    // 이 오브젝트의 왁스 색 (ObjectManager에서 오브젝트별로 정함)
    private Color WaxColor() =>
        ObjectManager.Instance != null && ObjectIndex >= 0 ? ObjectManager.Instance.WaxColorOf(ObjectIndex) : new Color(1f, 0.75f, 0.8f);

    // 오브젝트 그림을 cell픽셀 칸으로 줄여 왁스 모양을 만듦: 그림에서 Bulge칸까지 둥글게 부풀린 범위 + 볼록한 두께에 따른 음영/반짝임
    private static Shape GetShape(Sprite sprite, Vector2Int cell)
    {
        if (_shapes.TryGetValue((sprite, cell), out Shape cached)) return cached;

        Rect rect = sprite.rect; // 그림 전체 범위(픽셀) - textureRect는 투명 테두리를 잘라낸 범위라 기준점(pivot)과 안 맞음
        int inner = Mathf.CeilToInt(rect.width / cell.x), innerH = Mathf.CeilToInt(rect.height / cell.y); // 그림이 차지하는 칸 수

        // 읽기 허용이 꺼진 그림도 읽을 수 있게 작은 렌더 텍스처에 줄여 그린 뒤 픽셀을 읽음
        var rt = RenderTexture.GetTemporary(inner, innerH, 0, RenderTextureFormat.ARGB32);
        Texture source = sprite.texture; // 원본 그림
        Vector2 scale = new Vector2(rect.width / source.width, rect.height / source.height); // 그림 범위만 잘라 그리기
        Vector2 offset = new Vector2(rect.x / source.width, rect.y / source.height);
        Graphics.Blit(source, rt, scale, offset);
        RenderTexture previous = RenderTexture.active; // 원래 렌더 대상 (되돌리기용)
        RenderTexture.active = rt;
        var read = new Texture2D(inner, innerH, TextureFormat.RGBA32, false); // 줄인 그림
        read.ReadPixels(new Rect(0, 0, inner, innerH), 0, 0);
        read.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] small = read.GetPixels32(); // 줄인 그림 픽셀
        Destroy(read);

        // 그림이 실제로 그려진 범위만 씀 (투명한 여백까지 칸으로 만들면 칸 수가 몇 배로 늘어 금/조각/그리기가 다 느려짐)
        int minX = inner, minY = innerH, maxX = -1, maxY = -1; // 그려진 범위 (줄인 그림 기준)
        for (int y = 0; y < innerH; y++)
            for (int x = 0; x < inner; x++)
                if (small[y * inner + x].a > 100)
                {
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
        if (maxX < 0) { minX = minY = 0; maxX = inner - 1; maxY = innerH - 1; } // 다 투명하면 전체
        int w = maxX - minX + 1 + Bulge * 2, h = maxY - minY + 1 + Bulge * 2; // 부풀림 여유까지 포함한 칸 수

        var solid = new bool[w * h]; // 오브젝트 그림이 있는 칸
        var shape = new Shape { width = w, height = h, mask = new bool[w * h], thickness = new float[w * h], diffuse = new float[w * h], specular = new float[w * h], inside = new Color[w * h], solid = solid, objectColor = new Color[w * h] };
        // 안쪽 오브젝트 색을 칸 그림에 옮긴 뒤 여러 번 번지게 해서, 왁스 너머로 흐릿하게 비치는 모습을 만듦
        var tint = new Color[w * h]; // 번지기 전 안쪽 색 (rgb는 투명도를 곱한 값)
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Color32 c32 = small[y * inner + x]; // 오브젝트 그림 한 칸
                int i = (y - minY + Bulge) * w + x - minX + Bulge; // 왁스 칸 번호
                solid[i] = c32.a > 100;
                shape.objectColor[i] = c32;
                Color c = c32; // 계산용
                tint[i] = new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
            }
        for (int pass = 0; pass < BlurCells; pass++) tint = Blur(tint, w, h);
        for (int i = 0; i < tint.Length; i++)
            shape.inside[i] = tint[i].a > 0.001f ? new Color(tint[i].r / tint[i].a, tint[i].g / tint[i].a, tint[i].b / tint[i].a, tint[i].a) : Color.clear;

        // 그림에서 Bulge칸까지 둥글게 부풀린 범위가 왁스
        float[] fromSolid = Distance(solid, w, h); // 칸별로 가장 가까운 그림 칸까지 거리
        for (int i = 0; i < shape.mask.Length; i++)
            shape.mask[i] = fromSolid[i] <= Bulge + 0.01f;

        // 가장자리에서 멀수록 두꺼움(둥근 돔 모양) - 그 기울기로 면 방향을 구해 빛과 반짝임을 계산
        var outside = new bool[w * h]; // 왁스 밖 칸
        for (int i = 0; i < outside.Length; i++) outside[i] = !shape.mask[i];
        float[] fromEdge = Distance(outside, w, h); // 칸별로 왁스 밖까지 거리
        var height = new float[w * h]; // 칸별 왁스 높이 0~1
        for (int i = 0; i < height.Length; i++)
        {
            if (!shape.mask[i]) continue;
            float t = Mathf.Clamp01(fromEdge[i] / DomeRadius); // 가장자리 0 ~ 안쪽 1
            shape.thickness[i] = t;
            height[i] = Mathf.Sqrt(t * (2f - t)); // 가장자리는 가파르고 안쪽은 평평한 둥근 단면
        }
        Vector3 half = (LightDir + Vector3.forward).normalized; // 반짝임 계산용 (빛과 보는 방향 사이)
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x; // 이 칸
                if (!shape.mask[i]) continue;
                float dx = (height[y * w + Mathf.Min(x + 1, w - 1)] - height[y * w + Mathf.Max(x - 1, 0)]) * 0.5f; // 가로 기울기
                float dy = (height[Mathf.Min(y + 1, h - 1) * w + x] - height[Mathf.Max(y - 1, 0) * w + x]) * 0.5f; // 세로 기울기
                Vector3 normal = new Vector3(-dx * 8f, -dy * 8f, 1f).normalized; // 면 방향 (가장자리 경사를 키워 둥글게 보이게)
                shape.diffuse[i] = Mathf.Clamp01(Vector3.Dot(normal, LightDir) * 0.5f + 0.5f);
                float facing = Mathf.Max(0f, Vector3.Dot(normal, half)); // 반짝임 방향과 면이 얼마나 맞는지
                float rimBand = Mathf.Clamp01(1f - Mathf.Abs(shape.thickness[i] - 0.3f) / 0.25f); // 가장자리에서 조금 들어온 띠 (둥근 면이 빛을 받는 자리)
                float glint = Mathf.Clamp01((shape.diffuse[i] - 0.75f) / 0.2f) * rimBand; // 빛 쪽을 향한 그 띠의 부드러운 광
                shape.specular[i] = Mathf.Clamp01(Mathf.Pow(facing, 12f) * 0.25f + glint * 0.35f); // 매트하게 퍼지는 은은한 광
            }

        float perPixelX = inner / rect.width, perPixelY = innerH / rect.height; // 그림 픽셀 하나가 왁스 몇 칸인지 (칸 수를 올림해서 1/cell보다 살짝 큼)
        shape.pixelsPerUnit = sprite.pixelsPerUnit * perPixelX;
        shape.pivot = new Vector2((sprite.pivot.x * perPixelX - minX + Bulge) / w, (sprite.pivot.y * perPixelY - minY + Bulge) / h);
        shape.scaleY = perPixelX / perPixelY; // 그림은 가로 기준 PPU로 만들어서 세로는 이만큼 늘려야 원래 크기
        _shapes[(sprite, cell)] = shape;
        return shape;
    }

    // 칸별로 가장 가까운 seed 칸까지의 거리 (두 번 훑는 근사 거리 - 옆 칸 1, 대각선 1.41)
    private static float[] Distance(bool[] seed, int w, int h)
    {
        var d = new float[w * h]; // 거리
        for (int i = 0; i < d.Length; i++) d[i] = seed[i] ? 0f : 1e6f;
        const float Straight = 1f, Diagonal = 1.4142f; // 옆/대각선 칸까지 거리
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x; // 이 칸
                if (x > 0) d[i] = Mathf.Min(d[i], d[i - 1] + Straight);
                if (y > 0)
                {
                    d[i] = Mathf.Min(d[i], d[i - w] + Straight);
                    if (x > 0) d[i] = Mathf.Min(d[i], d[i - w - 1] + Diagonal);
                    if (x < w - 1) d[i] = Mathf.Min(d[i], d[i - w + 1] + Diagonal);
                }
            }
        for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x; // 이 칸
                if (x < w - 1) d[i] = Mathf.Min(d[i], d[i + 1] + Straight);
                if (y < h - 1)
                {
                    d[i] = Mathf.Min(d[i], d[i + w] + Straight);
                    if (x < w - 1) d[i] = Mathf.Min(d[i], d[i + w + 1] + Diagonal);
                    if (x > 0) d[i] = Mathf.Min(d[i], d[i + w - 1] + Diagonal);
                }
            }
        return d;
    }

    // 상하좌우 칸과 평균내서 한 번 번지게 함
    private static Color[] Blur(Color[] source, int w, int h)
    {
        var result = new Color[source.Length]; // 번진 결과
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x; // 이 칸
                Color sum = source[i] * 2f; // 자기 칸은 두 배로
                sum += x > 0 ? source[i - 1] : source[i];
                sum += x < w - 1 ? source[i + 1] : source[i];
                sum += y > 0 ? source[i - w] : source[i];
                sum += y < h - 1 ? source[i + w] : source[i];
                result[i] = sum / 6f;
            }
        return result;
    }
}
