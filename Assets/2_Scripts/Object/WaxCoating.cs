using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Health))]
public class WaxCoating : MonoBehaviour
{
    private const float PixelWorldSize = 0.03f; // 월드 유닛 - 오브젝트 크기와 상관없이 도트 크기 일정
    private const int Bulge = 5; // 칸
    private const float DomeRadius = 12f; // 칸
    private const float ThinAlpha = 0.86f;
    private const float ThickAlpha = 0.95f;
    private const int BlurCells = 10; // 칸
    private const float ShowThrough = 0.2f; // 0이면 안 보임
    private static readonly Vector3 LightDir = new Vector3(-0.45f, 0.65f, 0.6f).normalized;
    private const float ChunkStart = 0.5f; // 깎인 비율
    private const float OpenCracksAt = 0.45f; // 깎인 비율
    private const int ChunkCount = 9;
    private const int ShatterPieces = 16;
    private const int MaxChunkPlans = 4; // 넘으면 만든 것 중에서 재사용

    private class Shape
    {
        public int width, height;
        public bool[] mask;
        public float[] thickness; // 0 가장자리 ~ 1 안쪽
        public float[] diffuse;
        public float[] specular;
        public Color[] inside; // a = 비치는 양
        public bool[] solid;
        public Color[] objectColor;
        public float pixelsPerUnit;
        public Vector2 pivot; // 0~1
        public readonly List<(float[] chunkAt, float[] crackAt, float[] fallAt)> plans = new List<(float[] chunkAt, float[] crackAt, float[] fallAt)>();
        public float scaleY; // 칸의 가로세로 픽셀 수가 다를 때 세로 배율
    }

    private static readonly Dictionary<(Sprite, Vector2Int), Shape> _shapes = new Dictionary<(Sprite, Vector2Int), Shape>();
    private static ParticleSystem _shatter;

    private SpriteRenderer _object;
    private Health _health;
    private Click _click;
    private bool _isTerrain;
    private SpriteRenderer _wax;
    private Texture2D _texture;
    private Color32[] _pixels;
    private SpriteRenderer _objectCracks;
    private Texture2D _crackTexture;
    private Color32[] _crackPixels;
    private Shape _shape;
    private Sprite _shapeSprite;
    private float[] _crackAt; // 경계가 아니면 2
    private byte[] _crack; // 0 없음, 1 실금, 2 벌어진 틈
    private bool _dirty;
    private bool _cracksDirty;
    private RectInt _hitRect; // 맞을 때 이 둘레만 다시 그림
    private bool _hasHitRect;
    private float[] _fallAt;
    private bool _noCracks = true;
    private Color32[] _pristine;
    private Color[] _baseColors;
    private float[] _baseAlphas;
    private Shape _baseShape;
    private Color _baseColor;
    private int[] _rayStamp;
    private int _rayId;
    private float[] _chunkAt; // 안 떨어지면 2
    private float _shownDamage = -1f;
    private bool _broken;
    private Color _color = Color.white;
    private Vector2 _baseScale;
    private bool _forceCover;
    private SpriteRenderer _shield;
    private Texture2D _shieldTexture;
    private Color32[] _shieldPixels;
    private Vector2Int[] _shieldAnchors;
    private Shape _shieldShape;
    private bool[] _shieldShown;
    private bool _shieldOn;
    private const float ShieldRadius = 0.12f; // 왁스 그림 짧은 변 대비

    // 플레이 시작 때 도메인 리로드를 안 해서 직접 비움
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => _shapes.Clear();

    public bool Covers => enabled && (_forceCover || (!NoWax && (!_isTerrain || TerrainUnlocked)));

    public bool ForceCover
    {
        get => _forceCover;
        set
        {
            if (_forceCover == value) return;
            _forceCover = value;
            _dirty = true;
        }
    }

    private bool NoWax => ObjectManager.Instance != null && ObjectManager.Instance.NoWaxOf(ObjectIndex);
    private bool Cracks => enabled && (NoWax || Covers);

    private int ObjectIndex => _click != null ? _click.ObjectIndex : -1;

    private static readonly int[] WaxOnUnlockTerrain = { 4, 9, 14 }; // 해금되면 바로 왁스가 덮이는 지형 (책상, 도로, 회사)
    private bool TerrainUnlocked =>
        (UpgradeManager.Instance != null && UpgradeManager.Instance.WaxIsUnlockedFor(ObjectIndex))
        || (System.Array.IndexOf(WaxOnUnlockTerrain, ObjectIndex) >= 0 && ObjectManager.Instance != null && ObjectManager.Instance.IsUnlocked(ObjectIndex));

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

        var waxObject = new GameObject("Wax");
        waxObject.transform.SetParent(transform, false);
        _wax = waxObject.AddComponent<SpriteRenderer>();
        _wax.sharedMaterial = _object.sharedMaterial;
        _wax.enabled = false;

        var cracksObject = new GameObject("ObjectCracks");
        cracksObject.transform.SetParent(transform, false);
        _objectCracks = cracksObject.AddComponent<SpriteRenderer>();
        _objectCracks.sharedMaterial = _object.sharedMaterial;
        _objectCracks.enabled = false;

        var shieldObject = new GameObject("WaxShield");
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
        if (current >= max && _broken) Recoat();
        else if (current >= max && _crack != null) { System.Array.Clear(_crack, 0, _crack.Length); _dirty = _cracksDirty = _noCracks = true; }
        Redraw(1f - (float)current / max);
    }

    private void HandleDied()
    {
        if (!Covers || _broken) return;
        _broken = true;
        _wax.enabled = false;
        if (!(ZoneManager.Instance != null && ZoneManager.Instance.IsHidden(transform)))
            Shatter();
    }

    void LateUpdate()
    {
        bool track = Cracks && !_broken && _object.enabled;
        if (track && (_shape == null || _object.sprite != _shapeSprite)) // 클릭 펀치로 크기가 잠깐 바뀌는 건 무시
            Recoat();
        bool show = Covers && track && _shape != null;

        bool showCracks = Cracks && _object.enabled && _shape != null && _objectCracks.sprite != null;
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

        if (track && _shape != null && _health.MaxHP > 0) Redraw(1f - (float)_health.CurrentHP / _health.MaxHP);

        _wax.enabled = show;
        _shield.enabled = show && _shieldOn && _shield.sprite != null;
        if (!show) return;

        _wax.sortingLayerID = _object.sortingLayerID;
        _wax.sortingOrder = _object.sortingOrder + 2;
        _wax.forceRenderingOff = _object.forceRenderingOff;
        _wax.flipX = _object.flipX;
        _wax.flipY = _object.flipY;
        _wax.color = new Color(1f, 1f, 1f, _object.color.a);

        if (!_shield.enabled) return;
        _shield.sortingLayerID = _object.sortingLayerID;
        _shield.sortingOrder = _object.sortingOrder + 3;
        _shield.forceRenderingOff = _object.forceRenderingOff;
        _shield.flipX = _object.flipX;
        _shield.flipY = _object.flipY;
        _shield.color = _wax.color;
    }

    public void SetShield(Color[] colors, bool[] on)
    {
        _shieldOn = colors != null && colors.Length > 0 && _shape != null && _pixels != null;
        if (!_shieldOn) { _shieldShown = null; return; }

        bool rebuild = _shieldShape != _shape || _shieldAnchors == null || _shieldAnchors.Length != colors.Length;
        if (!rebuild && _shieldShown != null && System.Linq.Enumerable.SequenceEqual(_shieldShown, on)) return;

        int w = _shape.width, h = _shape.height;
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
            _shield.sprite = Sprite.Create(_shieldTexture, new Rect(0, 0, w, h), _shape.pivot, _shape.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            _shield.transform.localScale = _wax.transform.localScale;
        }
        _shieldShown = (bool[])on.Clone();

        System.Array.Clear(_shieldPixels, 0, _shieldPixels.Length);
        float radius = Mathf.Max(4f, Mathf.Min(w, h) * ShieldRadius);
        Vector3 half = (LightDir + Vector3.forward).normalized;
        for (int i = 0; i < colors.Length; i++)
        {
            if (!on[i]) continue;
            Vector2Int anchor = _shieldAnchors[i];
            int reach = Mathf.CeilToInt(radius * 1.25f);
            for (int y = Mathf.Max(0, anchor.y - reach); y <= Mathf.Min(h - 1, anchor.y + reach); y++)
                for (int x = Mathf.Max(0, anchor.x - reach); x <= Mathf.Min(w - 1, anchor.x + reach); x++)
                {
                    int cell = y * w + x;
                    if (!_shape.mask[cell]) continue;
                    float dx = x - anchor.x, dy = y - anchor.y;
                    float r = radius * (0.75f + 0.5f * Mathf.PerlinNoise(x * 0.15f + i * 13.7f, y * 0.15f + i * 7.3f));
                    float t = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (t > 1f) continue;
                    Vector3 normal = new Vector3(dx / r, dy / r, Mathf.Sqrt(1f - t * t)).normalized;
                    float patchLight = Mathf.Clamp01(Vector3.Dot(normal, LightDir) * 0.5f + 0.5f);
                    float diffuse = Mathf.Lerp(_shape.diffuse[cell], patchLight, 0.3f);
                    float thickness = _shape.thickness[cell];
                    float mottle = 1f + (Mathf.PerlinNoise(x * 0.1f + 50f, y * 0.1f) - 0.5f) * 0.05f;
                    Color color = colors[i] * Mathf.Lerp(0.65f, 1.12f, diffuse) * mottle;
                    color = Color.Lerp(color, Color.white, thickness * 0.12f);
                    if (t > 0.85f) color *= 0.9f;
                    float spec = Mathf.Max(_shape.specular[cell], Mathf.Pow(Mathf.Max(0f, Vector3.Dot(normal, half)), 12f) * 0.25f);
                    color = Color.Lerp(color, Color.white, spec * 0.6f);
                    color.a = Mathf.Max(Mathf.Lerp(ThinAlpha, ThickAlpha, thickness), spec);
                    _shieldPixels[cell] = color;
                }
        }
        _shieldTexture.SetPixels32(_shieldPixels);
        _shieldTexture.Apply();
    }

    private Vector2Int[] PickShieldAnchors(int count)
    {
        var directions = new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1),
                                 new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0) };
        int w = _shape.width, h = _shape.height;
        var anchors = new Vector2Int[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = i < directions.Length ? directions[i] : Random.insideUnitCircle.normalized;
            float best = float.MinValue;
            for (int cell = 0; cell < _shape.mask.Length; cell++)
            {
                if (!_shape.mask[cell]) continue;
                float score = ((float)(cell % w) / w - 0.5f) * direction.x + ((float)(cell / w) / h - 0.5f) * direction.y;
                if (score > best) { best = score; anchors[i] = new Vector2Int(cell % w, cell / w); }
            }
        }
        return anchors;
    }

    private Vector2Int CellPixels()
    {
        Sprite sprite = _object.sprite;
        Vector2 scale = Vector2.Max(_baseScale, new Vector2(0.0001f, 0.0001f));
        return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(PixelWorldSize / scale.x * sprite.pixelsPerUnit)),
                              Mathf.Max(1, Mathf.RoundToInt(PixelWorldSize / scale.y * sprite.pixelsPerUnit)));
    }

    private void Recoat()
    {
        _broken = false;
        _shownDamage = -1f;
        _color = WaxColor();

        Sprite sprite = _object.sprite;
        _shapeSprite = sprite;
        if (sprite == null) { _shape = null; return; }
        Shape shape = GetShape(sprite, CellPixels());
        bool sameShape = shape == _shape && _wax.sprite != null && _crack != null;
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

        int count = _shape.width * _shape.height;
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
        _wax.sprite = Sprite.Create(_texture, new Rect(0, 0, _shape.width, _shape.height), _shape.pivot, _shape.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        if (_objectCracks.sprite != null) Destroy(_objectCracks.sprite);
        _objectCracks.sprite = Sprite.Create(_crackTexture, new Rect(0, 0, _shape.width, _shape.height), _shape.pivot, _shape.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        _wax.transform.localScale = _objectCracks.transform.localScale = new Vector3(1f, _shape.scaleY, 1f);

        _crack = new byte[count];
        _rayStamp = new int[count];
        _pristine = null;
        PlanChunks();
    }

    public void Hit(Vector2 worldPoint, float hitFraction)
    {
        if (!Cracks || _broken || _shape == null || _crack == null || _health.MaxHP <= 0) return;

        Vector3 local = _wax.transform.InverseTransformPoint(worldPoint);
        if (_wax.flipX) local.x = -local.x;
        if (_wax.flipY) local.y = -local.y;
        int start = NearestMaskCell(Mathf.FloorToInt(local.x * _shape.pixelsPerUnit + _shape.pivot.x * _shape.width),
                                    Mathf.FloorToInt(local.y * _shape.pixelsPerUnit + _shape.pivot.y * _shape.height));
        if (start < 0) return;

        int w = _shape.width;
        float damage = 1f - (float)_health.CurrentHP / _health.MaxHP;
        float reach = Mathf.Lerp(5f, Mathf.Max(_shape.width, _shape.height) * 0.45f, Mathf.Clamp01(hitFraction * 3f + damage * 0.5f));
        bool open = damage >= OpenCracksAt;
        int rays = Random.Range(3, 6);
        float baseAngle = Random.Range(0f, Mathf.PI * 2f);
        for (int r = 0; r < rays; r++)
        {
            float angle = baseAngle + r * Mathf.PI * 2f / rays + Random.Range(-0.4f, 0.4f);
            CrackRay(start % w + 0.5f, start / w + 0.5f, angle, Mathf.RoundToInt(reach * Random.Range(0.5f, 1.1f)), open, 1);
        }
        int sx = start % w, sy = start / w;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = sx + dx, ny = sy + dy;
                if (nx >= 0 && ny >= 0 && nx < w && ny < _shape.height && _shape.mask[ny * w + nx])
                    _crack[ny * w + nx] = (byte)Mathf.Max(_crack[ny * w + nx], open ? 2 : 1);
            }
        int radius = Mathf.CeilToInt(reach * 1.1f) + 3;
        var rect = new RectInt(sx - radius, sy - radius, radius * 2 + 1, radius * 2 + 1);
        if (_hasHitRect)
        {
            int xMin = Mathf.Min(_hitRect.xMin, rect.xMin), yMin = Mathf.Min(_hitRect.yMin, rect.yMin);
            rect = new RectInt(xMin, yMin, Mathf.Max(_hitRect.xMax, rect.xMax) - xMin, Mathf.Max(_hitRect.yMax, rect.yMax) - yMin);
        }
        _hitRect = rect;
        _hasHitRect = true;
        _noCracks = false;
    }

    private void CrackRay(float x, float y, float angle, int length, bool open, int branches)
    {
        int w = _shape.width, h = _shape.height;
        int ray = ++_rayId;
        for (int step = 1; step <= length; step++)
        {
            angle += Random.Range(-0.35f, 0.35f);
            x += Mathf.Cos(angle);
            y += Mathf.Sin(angle);
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int cell = (int)y * w + (int)x;
            if (!_shape.mask[cell]) return;
            if (_crack[cell] > 0 && _rayStamp[cell] != ray && step > 2 && Random.value < 0.75f) return;

            bool wide = open && step < length * 0.4f;
            float progress = (float)step / length;
            int thickness = progress > 0.8f ? 1 : wide ? 3 : 2;
            float px = -Mathf.Sin(angle), py = Mathf.Cos(angle);
            for (int k = 0; k < thickness; k++)
            {
                float offset = k - (thickness - 1) * 0.5f;
                int bx = Mathf.FloorToInt(x + px * offset), by = Mathf.FloorToInt(y + py * offset);
                if (bx < 0 || by < 0 || bx >= w || by >= h) continue;
                int brush = by * w + bx;
                if (!_shape.mask[brush]) continue;
                _crack[brush] = (byte)Mathf.Max(_crack[brush], wide ? 2 : 1);
                _rayStamp[brush] = ray;
            }
            if (branches > 0 && step > 2 && Random.value < 0.08f)
                CrackRay(x, y, angle + Random.Range(0.5f, 1.1f) * (Random.value < 0.5f ? -1f : 1f), (length - step) / 2, false, branches - 1);
        }
    }

    private int NearestMaskCell(int x, int y)
    {
        int w = _shape.width, h = _shape.height;
        for (int radius = 0; radius <= 10; radius++)
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && _shape.mask[ny * w + nx]) return ny * w + nx;
                }
        return -1;
    }

    private void PlanChunks()
    {
        if (_shape.plans.Count >= MaxChunkPlans)
        {
            (_chunkAt, _crackAt, _fallAt) = _shape.plans[Random.Range(0, _shape.plans.Count)];
            return;
        }

        int w = _shape.width, h = _shape.height;
        var seeds = new Vector2[ChunkCount];
        var fallAt = new float[ChunkCount];
        for (int i = 0; i < ChunkCount; i++)
        {
            int cell = RandomMaskCell();
            seeds[i] = cell < 0 ? Vector2.zero : new Vector2(cell % w, cell / w);
            fallAt[i] = i == 0 ? 2f : Random.Range(ChunkStart, 0.95f);
        }

        var owner = new int[_shape.mask.Length];
        float noiseSeed = Random.Range(0f, 1000f);
        for (int cell = 0; cell < owner.Length; cell++)
        {
            if (!_shape.mask[cell]) continue;
            float px = cell % w, py = cell / w;
            var p = new Vector2(px + (Mathf.PerlinNoise(px * 0.12f + noiseSeed, py * 0.12f) - 0.5f) * 8f,
                                py + (Mathf.PerlinNoise(px * 0.12f, py * 0.12f + noiseSeed) - 0.5f) * 8f);
            int nearest = 0;
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
            int x = cell % w, y = cell / w;
            float breakAt = 2f;
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
            int cell = Random.Range(0, _shape.mask.Length);
            if (_shape.mask[cell]) return cell;
        }
        return -1;
    }

    private void Redraw(float damage)
    {
        if (_shape == null || _broken || _pixels == null) return;
        damage = Mathf.Round(damage * 50f) / 50f; // 2% 단위
        // 임계값을 지날 때만 전체를 다시 그림
        bool full = _dirty || _shownDamage < 0f || ThresholdCrossed(_shownDamage, damage);
        _shownDamage = damage;
        if (!full && !_hasHitRect && !_cracksDirty) return;

        int w = _shape.width, h = _shape.height;
        var all = new RectInt(0, 0, w, h);
        RectInt region = all;
        if (!full)
        {
            region = new RectInt(_hitRect.xMin - 2, _hitRect.yMin - 2, _hitRect.width + 4, _hitRect.height + 4);
            region.ClampToBounds(all);
        }
        if (Covers && (full || _hasHitRect)) RedrawWax(damage, w, h, region);
        if (_cracksDirty) RedrawCracks(w, h, all);
        else if (_hasHitRect) RedrawCracks(w, h, region);
        _dirty = _cracksDirty = _hasHitRect = false;
    }

    private bool ThresholdCrossed(float old, float now)
    {
        if (_fallAt == null) return true;
        foreach (float fall in _fallAt)
        {
            if ((fall <= old) != (fall <= now)) return true;
            float crack = fall - 0.15f; // PlanChunks와 같은 계산
            if ((crack <= old) != (crack <= now)) return true;
        }
        return false;
    }

    private void BuildBase()
    {
        _baseShape = _shape;
        _baseColor = _color;
        int count = _shape.width * _shape.height;
        _baseColors = new Color[count];
        _baseAlphas = new float[count];
        for (int cell = 0; cell < count; cell++)
        {
            if (!_shape.mask[cell]) continue;
            int x = cell % _shape.width, y = cell / _shape.width;
            float t = _shape.thickness[cell];
            float mottle = 1f + (Mathf.PerlinNoise(x * 0.1f, y * 0.1f) - 0.5f) * 0.05f;
            Color color = _color * Mathf.Lerp(0.65f, 1.12f, _shape.diffuse[cell]) * mottle;
            color = Color.Lerp(color, Color.white, t * 0.12f);
            Color inside = _shape.inside[cell];
            color = Color.Lerp(color, (Color)inside * _color * 1.1f, inside.a * ShowThrough);
            color.a = 1f;
            _baseColors[cell] = color;
            _baseAlphas[cell] = Mathf.Lerp(ThinAlpha, ThickAlpha, t);
        }
    }

    private void RedrawWax(float damage, int w, int h, RectInt region)
    {
        if (_baseColors == null || _baseShape != _shape || _baseColor != _color) { BuildBase(); _pristine = null; }
        bool pristine = _noCracks && damage <= 0f && region.width == w && region.height == h;
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
            int cell = y * w + x;
            if (!Visible(cell, damage)) { _pixels[cell] = default; continue; }
            Color color = _baseColors[cell];
            float alpha = _baseAlphas[cell];

            bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                     || !Visible(cell - 1, damage) || !Visible(cell + 1, damage) || !Visible(cell - w, damage) || !Visible(cell + w, damage);
            bool brokenEdge = edge && (Fallen(cell - 1, damage) || Fallen(cell + 1, damage) || Fallen(cell - w, damage) || Fallen(cell + w, damage));
            if (brokenEdge) { color *= 0.78f; alpha = 1f; }
            else if (edge) { color *= 0.9f; }

            int crack = CrackAt(cell, damage);
            if (crack == 2) { color = Color.Lerp(_color, Color.white, 0.55f); alpha = 1f; }
            else if (crack == 1) { color = Color.Lerp(_color, Color.white, 0.68f); alpha = 1f; }
            else
            {
                bool shadow = (x > 0 && CrackAt(cell - 1, damage) > 0) || (y < h - 1 && CrackAt(cell + w, damage) > 0);
                bool rim = (x < w - 1 && CrackAt(cell + 1, damage) > 0) || (y > 0 && CrackAt(cell - w, damage) > 0);
                if (shadow) color *= 0.8f;
                else if (rim) color = Color.Lerp(color, Color.white, 0.22f);
            }

            float spec = _shape.specular[cell];
            color = Color.Lerp(color, Color.white, spec * 0.6f);
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
        if (_noCracks)
        {
            System.Array.Clear(_crackPixels, 0, _crackPixels.Length);
            _crackTexture.SetPixels32(_crackPixels);
            _crackTexture.Apply();
            return;
        }
        for (int y = region.yMin; y < region.yMax; y++)
        for (int x = region.xMin; x < region.xMax; x++)
        {
            int cell = y * w + x;
            if (!_shape.solid[cell]) { _crackPixels[cell] = default; continue; }
            Color baseColor = _shape.objectColor[cell];
            Color color;
            byte crack = _crack[cell];
            if (crack == 2) color = Color.Lerp(baseColor, Color.white, 0.55f);
            else if (crack == 1) color = Color.Lerp(baseColor, Color.white, 0.68f);
            else if ((x > 0 && _crack[cell - 1] > 0) || (y < h - 1 && _crack[cell + w] > 0)) color = baseColor * 0.8f;
            else if ((x < w - 1 && _crack[cell + 1] > 0) || (y > 0 && _crack[cell - w] > 0)) color = Color.Lerp(baseColor, Color.white, 0.22f);
            else { _crackPixels[cell] = default; continue; }
            color.a = 1f;
            _crackPixels[cell] = color;
        }
        _crackTexture.SetPixels32(_crackPixels);
        _crackTexture.Apply();
    }

    private int CrackAt(int cell, float damage) =>
        _crack[cell] > 0 ? _crack[cell] : _crackAt[cell] <= damage ? 1 : 0;

    private bool Visible(int cell, float damage) =>
        cell >= 0 && cell < _pixels.Length && _shape.mask[cell] && _chunkAt[cell] > damage;

    private bool Fallen(int cell, float damage) =>
        cell >= 0 && cell < _pixels.Length && _shape.mask[cell] && _chunkAt[cell] <= damage;

    private void Shatter()
    {
        if (_shatter == null) CreateShatterSystem(_wax.sharedMaterial);
        Bounds bounds = _object.bounds;
        for (int i = 0; i < ShatterPieces; i++)
        {
            Vector3 position = bounds.center + Vector3.Scale(bounds.extents, new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.8f, 0.8f), 0f));
            Vector3 velocity = (position - bounds.center).normalized * Random.Range(2f, 5f) + Vector3.up * Random.Range(1f, 3f);
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
        var systemObject = new GameObject("WaxShatter");
        DontDestroyOnLoad(systemObject);
        _shatter = systemObject.AddComponent<ParticleSystem>();
        _shatter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = _shatter.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.5f;
        main.maxParticles = 500;
        ParticleSystem.EmissionModule emission = _shatter.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = _shatter.shape;
        shape.enabled = false;
        ParticleSystem.ColorOverLifetimeModule fade = _shatter.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var renderer = systemObject.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(spriteMaterial) { mainTexture = Texture2D.whiteTexture };
        renderer.sortingOrder = 25;
        _shatter.Play();
    }

    private Color WaxColor() =>
        ObjectManager.Instance != null && ObjectIndex >= 0 ? ObjectManager.Instance.WaxColorOf(ObjectIndex) : new Color(1f, 0.75f, 0.8f);

    private static Shape GetShape(Sprite sprite, Vector2Int cell)
    {
        if (_shapes.TryGetValue((sprite, cell), out Shape cached)) return cached;

        Rect rect = sprite.rect; // textureRect는 투명 테두리를 잘라낸 범위라 rect를 씀
        int inner = Mathf.CeilToInt(rect.width / cell.x), innerH = Mathf.CeilToInt(rect.height / cell.y);

        // 읽기 허용이 꺼진 그림도 읽게 렌더 텍스처를 거침
        var rt = RenderTexture.GetTemporary(inner, innerH, 0, RenderTextureFormat.ARGB32);
        Texture source = sprite.texture;
        Vector2 scale = new Vector2(rect.width / source.width, rect.height / source.height);
        Vector2 offset = new Vector2(rect.x / source.width, rect.y / source.height);
        Graphics.Blit(source, rt, scale, offset);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var read = new Texture2D(inner, innerH, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, inner, innerH), 0, 0);
        read.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] small = read.GetPixels32();
        Destroy(read);

        // 투명 여백은 빼서 칸 수를 줄임
        int minX = inner, minY = innerH, maxX = -1, maxY = -1;
        for (int y = 0; y < innerH; y++)
            for (int x = 0; x < inner; x++)
                if (small[y * inner + x].a > 100)
                {
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
        if (maxX < 0) { minX = minY = 0; maxX = inner - 1; maxY = innerH - 1; }
        int w = maxX - minX + 1 + Bulge * 2, h = maxY - minY + 1 + Bulge * 2;

        var solid = new bool[w * h];
        var shape = new Shape { width = w, height = h, mask = new bool[w * h], thickness = new float[w * h], diffuse = new float[w * h], specular = new float[w * h], inside = new Color[w * h], solid = solid, objectColor = new Color[w * h] };
        var tint = new Color[w * h];
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Color32 c32 = small[y * inner + x];
                int i = (y - minY + Bulge) * w + x - minX + Bulge;
                solid[i] = c32.a > 100;
                shape.objectColor[i] = c32;
                Color c = c32;
                tint[i] = new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
            }
        for (int pass = 0; pass < BlurCells; pass++) tint = Blur(tint, w, h);
        for (int i = 0; i < tint.Length; i++)
            shape.inside[i] = tint[i].a > 0.001f ? new Color(tint[i].r / tint[i].a, tint[i].g / tint[i].a, tint[i].b / tint[i].a, tint[i].a) : Color.clear;

        float[] fromSolid = Distance(solid, w, h);
        for (int i = 0; i < shape.mask.Length; i++)
            shape.mask[i] = fromSolid[i] <= Bulge + 0.01f;

        var outside = new bool[w * h];
        for (int i = 0; i < outside.Length; i++) outside[i] = !shape.mask[i];
        float[] fromEdge = Distance(outside, w, h);
        var height = new float[w * h];
        for (int i = 0; i < height.Length; i++)
        {
            if (!shape.mask[i]) continue;
            float t = Mathf.Clamp01(fromEdge[i] / DomeRadius);
            shape.thickness[i] = t;
            height[i] = Mathf.Sqrt(t * (2f - t));
        }
        Vector3 half = (LightDir + Vector3.forward).normalized;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!shape.mask[i]) continue;
                float dx = (height[y * w + Mathf.Min(x + 1, w - 1)] - height[y * w + Mathf.Max(x - 1, 0)]) * 0.5f;
                float dy = (height[Mathf.Min(y + 1, h - 1) * w + x] - height[Mathf.Max(y - 1, 0) * w + x]) * 0.5f;
                Vector3 normal = new Vector3(-dx * 8f, -dy * 8f, 1f).normalized;
                shape.diffuse[i] = Mathf.Clamp01(Vector3.Dot(normal, LightDir) * 0.5f + 0.5f);
                float facing = Mathf.Max(0f, Vector3.Dot(normal, half));
                float rimBand = Mathf.Clamp01(1f - Mathf.Abs(shape.thickness[i] - 0.3f) / 0.25f);
                float glint = Mathf.Clamp01((shape.diffuse[i] - 0.75f) / 0.2f) * rimBand;
                shape.specular[i] = Mathf.Clamp01(Mathf.Pow(facing, 12f) * 0.25f + glint * 0.35f);
            }

        float perPixelX = inner / rect.width, perPixelY = innerH / rect.height;
        shape.pixelsPerUnit = sprite.pixelsPerUnit * perPixelX;
        shape.pivot = new Vector2((sprite.pivot.x * perPixelX - minX + Bulge) / w, (sprite.pivot.y * perPixelY - minY + Bulge) / h);
        shape.scaleY = perPixelX / perPixelY; // 가로 기준 PPU라 세로를 이만큼 늘림
        _shapes[(sprite, cell)] = shape;
        return shape;
    }

    // 두 번 훑는 근사 거리 (옆 1, 대각선 1.41)
    private static float[] Distance(bool[] seed, int w, int h)
    {
        var d = new float[w * h];
        for (int i = 0; i < d.Length; i++) d[i] = seed[i] ? 0f : 1e6f;
        const float Straight = 1f, Diagonal = 1.4142f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
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
                int i = y * w + x;
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

    private static Color[] Blur(Color[] source, int w, int h)
    {
        var result = new Color[source.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color sum = source[i] * 2f;
                sum += x > 0 ? source[i - 1] : source[i];
                sum += x < w - 1 ? source[i + 1] : source[i];
                sum += y > 0 ? source[i - w] : source[i];
                sum += y < h - 1 ? source[i + w] : source[i];
                result[i] = sum / 6f;
            }
        return result;
    }
}
