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

    // 오브젝트 그림 하나에서 뽑아낸 왁스 모양 (같은 그림을 쓰는 오브젝트끼리 공유)
    private class Shape
    {
        public int width, height; // 왁스 그림 칸 수
        public bool[] mask; // 왁스가 덮는 칸
        public float[] thickness; // 칸별 왁스 두께 0(가장자리)~1(안쪽) - 두꺼울수록 덜 비침
        public float[] diffuse; // 칸별 빛 받는 정도 (볼록한 면의 방향으로 계산)
        public float[] specular; // 칸별 반짝이는 정도 (매끈한 왁스 표면의 하이라이트)
        public Color[] inside; // 칸별로 왁스 너머 흐릿하게 비치는 안쪽 오브젝트 색 (a = 비치는 양)
        public float pixelsPerUnit; // 왁스 그림의 Pixels Per Unit (칸 하나 = 오브젝트 그림 몇 픽셀)
        public Vector2 pivot; // 오브젝트 그림과 같은 자리에 겹치도록 맞춘 기준점 (0~1)
    }

    private static readonly Dictionary<(Sprite, int), Shape> _shapes = new Dictionary<(Sprite, int), Shape>(); // 그림+칸 크기별 왁스 모양 캐시
    private static ParticleSystem _shatter; // 부서질 때 왁스 파편을 뿌리는 공용 파티클

    private SpriteRenderer _object; // 안쪽 오브젝트 그림
    private Health _health; // 체력 - 금/조각 진행도 기준
    private Click _click; // 오브젝트 번호를 알아내는 데 씀
    private bool _isTerrain; // 지형(책상/도로/아파트)인지 - 업그레이드로 해금해야 덮임
    private SpriteRenderer _wax; // 왁스 그림 (자식 오브젝트)
    private Texture2D _texture; // 왁스 그림 픽셀
    private Color32[] _pixels; // 왁스 그림 픽셀 작업 공간
    private Shape _shape; // 지금 오브젝트 그림에 맞춘 왁스 모양
    private Sprite _shapeSprite; // 왁스 모양을 만든 오브젝트 그림 - 그림이 바뀌면(가운데 오브젝트 교체 등) 다시 만듦
    private float[] _crackAt; // 조각 경계 칸이 미리 금으로 보이기 시작하는 깎인 비율 (경계가 아니면 2)
    private byte[] _crack; // 칸별 금 상태 - 0 없음, 1 실금, 2 벌어진 틈 (맞은 자리에서 뻗어나가며 쌓임)
    private bool _dirty; // 새 금이 생겨서 다시 그려야 하는지
    private float[] _chunkAt; // 칸별로 떨어져 나가는 깎인 비율 (안 떨어지면 2)
    private float _shownDamage = -1f; // 마지막으로 그린 깎인 비율 - 같으면 다시 안 그림
    private bool _broken; // 부서져서 왁스가 사라진 상태인지
    private Color _color = Color.white; // 이 오브젝트의 왁스 색
    private float _baseScale; // 시작할 때의 월드 크기 배율 - 도트 크기 계산 기준

    // 플레이를 새로 시작할 때 모양 캐시를 비움 (플레이 시작 때 스크립트를 다시 불러오지 않는 설정이라, 그 사이 그림을 다시 불러왔으면 옛 모양이 남음)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => _shapes.Clear();

    // 지금 왁스가 오브젝트를 덮고 있는지 (HealthSpriteSwitcher가 깨진 단계 그림을 안 쓰는 데 씀)
    public bool Covers => enabled && (!_isTerrain || TerrainUnlocked);

    private int ObjectIndex => _click != null ? _click.ObjectIndex : -1;
    private bool TerrainUnlocked => UpgradeManager.Instance != null && UpgradeManager.Instance.WaxIsUnlockedFor(ObjectIndex);

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
        _baseScale = Mathf.Abs(transform.lossyScale.y);

        var waxObject = new GameObject("Wax"); // 왁스 그림을 그릴 자식
        waxObject.transform.SetParent(transform, false);
        _wax = waxObject.AddComponent<SpriteRenderer>();
        _wax.sharedMaterial = _object.sharedMaterial;
        _wax.enabled = false;
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
    }

    void OnDestroy()
    {
        if (_texture != null) Destroy(_texture);
        if (_wax != null && _wax.sprite != null) Destroy(_wax.sprite);
    }

    private void HandleDamaged(int current, int max)
    {
        if (max <= 0) return;
        if (current >= max && _broken) Recoat(); // 다시 살아나 체력이 꽉 찼으면 새 왁스로 덮음
        else if (current >= max && _crack != null) { System.Array.Clear(_crack, 0, _crack.Length); _dirty = true; } // 체력이 다시 꽉 차면(존을 다시 볼 때) 금도 없어짐
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
        bool show = Covers && !_broken && _object.enabled; // 지금 왁스를 보여줄지
        if (show && (_shape == null || _object.sprite != _shapeSprite)) // 클릭 펀치로 크기가 잠깐 바뀌는 건 무시 (다시 만들면 금 배치가 매번 바뀜)
            Recoat();
        if (show && _shape == null) show = false;

        _wax.enabled = show;
        if (!show) return;

        // 오브젝트 그림을 그대로 따라감: 정렬 순서는 바로 위, 투명도(죽을 때/살아날 때 흐려짐)와 존 숨김도 같이
        _wax.sortingLayerID = _object.sortingLayerID;
        _wax.sortingOrder = _object.sortingOrder + 1;
        _wax.forceRenderingOff = _object.forceRenderingOff;
        _wax.flipX = _object.flipX;
        _wax.flipY = _object.flipY;
        _wax.color = new Color(1f, 1f, 1f, _object.color.a);

        if (_health.MaxHP > 0) Redraw(1f - (float)_health.CurrentHP / _health.MaxHP);
    }

    // 오브젝트 그림 픽셀 몇 개를 왁스 한 칸으로 묶을지 - 화면에서 도트 크기가 오브젝트마다 같게
    private int CellPixels()
    {
        Sprite sprite = _object.sprite; // 안쪽 오브젝트 그림
        float scale = Mathf.Max(0.0001f, _baseScale); // 월드에서 커진 배율 (등장/타격 연출로 잠깐 바뀐 크기 말고 원래 크기)
        return Mathf.Max(1, Mathf.RoundToInt(PixelWorldSize / scale * sprite.pixelsPerUnit));
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
        _shape = GetShape(sprite, CellPixels());
        if (_shape == null) return;

        int count = _shape.width * _shape.height; // 칸 수
        if (_texture == null || _texture.width != _shape.width || _texture.height != _shape.height)
        {
            if (_texture != null) Destroy(_texture);
            _texture = new Texture2D(_shape.width, _shape.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _pixels = new Color32[count];
        }
        if (_wax.sprite != null) Destroy(_wax.sprite);
        _wax.sprite = Sprite.Create(_texture, new Rect(0, 0, _shape.width, _shape.height), _shape.pivot, _shape.pixelsPerUnit, 0, SpriteMeshType.FullRect); // 그림이 계속 바뀌므로 투명 부분을 잘라내는 Tight 모양을 쓰면 안 됨

        _crack = new byte[count];
        _dirty = true;
        PlanChunks();
    }

    // 맞은 자리(월드 좌표)에서 금이 사방으로 뻗어나감 - Click이 때릴 때마다 부름. hitFraction은 이번 한 방이 최대 체력의 몇 %였는지.
    // 세게 맞을수록, 많이 깎였을수록 길게 뻗고, 뻗다가 다른 금을 만나면 대부분 거기서 멈춰 금끼리 이어짐
    public void Hit(Vector2 worldPoint, float hitFraction)
    {
        if (!Covers || _broken || _shape == null || _crack == null || _health.MaxHP <= 0) return;

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
        _crack[start] = (byte)(open ? 2 : 1); // 맞은 점
        _dirty = true;
    }

    // 금 한 줄기를 (x, y)에서 angle 방향으로 length칸 그림. 조금씩 휘고 가끔 가지를 치며, 다른 금을 만나면 대부분 멈춤
    private void CrackRay(float x, float y, float angle, int length, bool open, int branches)
    {
        int w = _shape.width, h = _shape.height; // 칸 수
        for (int step = 1; step <= length; step++)
        {
            angle += Random.Range(-0.35f, 0.35f);
            x += Mathf.Cos(angle);
            y += Mathf.Sin(angle);
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int cell = (int)y * w + (int)x; // 지금 칸
            if (!_shape.mask[cell]) return;
            if (_crack[cell] > 0 && step > 2 && Random.value < 0.75f) return; // 다른 금에 닿으면 거기서 이어지고 멈춤

            bool wide = open && step < length * 0.4f; // 시작 부분은 벌어진 틈
            _crack[cell] = (byte)Mathf.Max(_crack[cell], wide ? 2 : 1);
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
        damage = Mathf.Round(damage * 50f) / 50f; // 2% 단위로만 다시 그려서 연타 중 매 프레임 그리지 않게
        if (Mathf.Approximately(damage, _shownDamage) && !_dirty) return;
        _shownDamage = damage;
        _dirty = false;

        int w = _shape.width, h = _shape.height; // 칸 수
        for (int cell = 0; cell < _pixels.Length; cell++)
        {
            if (!Visible(cell, damage)) { _pixels[cell] = default; continue; }
            int x = cell % w, y = cell / w; // 이 칸 위치

            float t = _shape.thickness[cell]; // 두께
            float mottle = 1f + (Mathf.PerlinNoise(x * 0.1f, y * 0.1f) - 0.5f) * 0.05f; // 왁스 표면의 아주 옅은 결
            Color color = _color * Mathf.Lerp(0.65f, 1.12f, _shape.diffuse[cell]) * mottle; // 빛 받는 면은 밝게, 반대쪽은 부드럽게 어둡게
            color = Color.Lerp(color, Color.white, t * 0.12f); // 볼록한 가운데는 살짝 밝게 (크림처럼)
            Color inside = _shape.inside[cell]; // 왁스 너머로 흐릿하게 비치는 안쪽 오브젝트
            color = Color.Lerp(color, (Color)inside * _color * 1.1f, inside.a * ShowThrough); // 안쪽 색이 왁스 색에 물들어 흐릿하게 보임
            color.a = 1f;
            float alpha = Mathf.Lerp(ThinAlpha, ThickAlpha, t); // 두꺼울수록 덜 비침

            bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                     || !Visible(cell - 1, damage) || !Visible(cell + 1, damage) || !Visible(cell - w, damage) || !Visible(cell + w, damage); // 바깥/떨어진 자리와 맞닿은 칸
            bool brokenEdge = edge && (Fallen(cell - 1, damage) || Fallen(cell + 1, damage) || Fallen(cell - w, damage) || Fallen(cell + w, damage)); // 조각이 떨어져 나간 둘레
            if (brokenEdge) { color *= 0.78f; alpha = 1f; } // 깨진 단면은 두께가 보이게 조금 진하게
            else if (edge) { color *= 0.9f; } // 가장자리는 아주 살짝만 진하게 (딱딱한 윤곽선 없이 부드럽게)

            int crack = CrackAt(cell, damage); // 이 칸의 금 (0 없음, 1 실금, 2 벌어진 틈)
            if (crack == 2) { color = _color * 0.38f; alpha = 1f; } // 벌어진 틈: 안쪽 그늘이 보이게 진하게
            else if (crack == 1) { color = _color * 0.58f; alpha = 1f; } // 실금: 가늘고 어두운 선
            else
            {
                int near = Mathf.Max(Mathf.Max(x > 0 ? CrackAt(cell - 1, damage) : 0, x < w - 1 ? CrackAt(cell + 1, damage) : 0),
                                     Mathf.Max(y > 0 ? CrackAt(cell - w, damage) : 0, y < h - 1 ? CrackAt(cell + w, damage) : 0)); // 바로 옆 칸의 금
                if (near > 0) color = Color.Lerp(color, Color.white, near == 2 ? 0.4f : 0.28f); // 금 둘레는 하얗게 질림 (왁스가 휘면서 생기는 응력 자국)
            }

            float spec = _shape.specular[cell]; // 반짝임
            color = Color.Lerp(color, Color.white, spec * 0.6f); // 은은한 광 (버터 왁스는 번들거리지 않음)
            alpha = Mathf.Max(alpha, spec);
            color.a = alpha;
            _pixels[cell] = color;
        }
        _texture.SetPixels32(_pixels);
        _texture.Apply();
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
    private static Shape GetShape(Sprite sprite, int cell)
    {
        if (_shapes.TryGetValue((sprite, cell), out Shape cached)) return cached;

        Rect rect = sprite.rect; // 그림 전체 범위(픽셀) - textureRect는 투명 테두리를 잘라낸 범위라 기준점(pivot)과 안 맞음
        int inner = Mathf.CeilToInt(rect.width / cell), innerH = Mathf.CeilToInt(rect.height / cell); // 그림이 차지하는 칸 수
        int w = inner + Bulge * 2, h = innerH + Bulge * 2; // 부풀림 여유까지 포함한 칸 수

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

        var solid = new bool[w * h]; // 오브젝트 그림이 있는 칸
        for (int y = 0; y < innerH; y++)
            for (int x = 0; x < inner; x++)
                solid[(y + Bulge) * w + x + Bulge] = small[y * inner + x].a > 100;

        var shape = new Shape { width = w, height = h, mask = new bool[w * h], thickness = new float[w * h], diffuse = new float[w * h], specular = new float[w * h], inside = new Color[w * h] };

        // 안쪽 오브젝트 색을 칸 그림에 옮긴 뒤 여러 번 번지게 해서, 왁스 너머로 흐릿하게 비치는 모습을 만듦
        var tint = new Color[w * h]; // 번지기 전 안쪽 색 (rgb는 투명도를 곱한 값)
        for (int y = 0; y < innerH; y++)
            for (int x = 0; x < inner; x++)
            {
                Color c = small[y * inner + x]; // 오브젝트 그림 한 칸
                tint[(y + Bulge) * w + x + Bulge] = new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
            }
        for (int pass = 0; pass < BlurCells; pass++) tint = Blur(tint, w, h);
        for (int i = 0; i < tint.Length; i++)
            shape.inside[i] = tint[i].a > 0.001f ? new Color(tint[i].r / tint[i].a, tint[i].g / tint[i].a, tint[i].b / tint[i].a, tint[i].a) : Color.clear;

        // 그림에서 Bulge칸까지 둥글게 부풀린 범위가 왁스
        float[] fromSolid = Distance(solid, w, h); // 칸별로 가장 가까운 그림 칸까지 거리
        for (int i = 0; i < shape.mask.Length; i++) shape.mask[i] = fromSolid[i] <= Bulge + 0.01f;

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
        shape.pivot = new Vector2((sprite.pivot.x * perPixelX + Bulge) / w, (sprite.pivot.y * perPixelY + Bulge) / h);
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
