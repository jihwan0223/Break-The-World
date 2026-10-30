using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class DebrisPool : MonoBehaviour
{
    public static DebrisPool Instance { get; private set; }

    public const int LayerCount = 5; // 나중에 떨어진 파편일수록 위층

    [SerializeField] private Collider2D spawnArea;
    [SerializeField] private int maxPiecesPerBreak = 50; // 렉 방지 - 넘치는 조각은 파편 하나에 합쳐 담음
    [SerializeField] private float pieceSize = 1f; // 월드 유닛
    [SerializeField] private float pickupColliderScale = 1.4f;
    [SerializeField] private Sprite[] pieceSprites;
    [SerializeField] private Sprite[] crystalSprites; // 이미 색칠된 결정 그림 - 비우면 조각 그림에 crystalColor
    [SerializeField] private float initialSpread = 1f; // 월드 유닛
    [SerializeField] private float fallDuration = 0.4f; // 초
    [SerializeField] private Color crystalColor = new Color(0.45f, 0.9f, 1f);
    [SerializeField] private float crystalSizeMultiplier = 1.6f;
    [SerializeField] private int maxFallingShards = 2000;
    [SerializeField] private float hoverPickupRadiusScale = 0.7f;
    [SerializeField] private int maxCarriedPieces = 80;
    [SerializeField] private float carryPileWidth = 1.2f; // 월드 유닛
    [SerializeField] private float carryGatherSpeed = 12f;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip mouseClickSound;

    private class Piece
    {
        public SpriteRenderer renderer;
        public CircleCollider2D collider;
        public long value;
        public bool isCrystal; // 결정은 바닥 최대 개수에 안 셈
        public int zone;
        public bool landed;
        public bool sweepMarked;
        public bool sweepOnLand;
        public Vector2 carryOffset;
        public Vector2 carryCurrent;
    }

    private readonly List<Piece> _groundPieces = new List<Piece>();
    private readonly Dictionary<Collider2D, Piece> _pieceByCollider = new Dictionary<Collider2D, Piece>();
    private readonly Stack<Piece> _pool = new Stack<Piece>();
    private Color _currentColor = Color.white;
    private Sprite _fallbackSprite;
    private readonly HashSet<Piece> _hovered = new HashSet<Piece>();
    private readonly List<Piece> _hoverHits = new List<Piece>();
    private Vector2 _lastMouseScreenPos;
    private int _spawnSequence;
    private PickupPopup _popup;
    // 에디터가 private 배열을 빈 배열로 채우는 일이 있어 저장 대상에서 뺌
    [System.NonSerialized] private ParticleSystem[] _shardSystems;
    private readonly List<Piece> _mergeTargets = new List<Piece>();
    private readonly List<Piece> _carried = new List<Piece>();
    private AudioSource _audioSource;

    public event System.Action<IReadOnlyList<Transform>> OnPiecesDropped;
    public event System.Action<Transform> OnPieceCollected;

    public float FallDuration => fallDuration;

    private int CurrentZone => ZoneManager.Instance != null ? ZoneManager.Instance.CurrentZone : 0;
    private bool _sweepAll;
    private int MaxGroundPieces => UpgradeManager.Instance != null ? UpgradeManager.Instance.MaxGroundPieces : 400;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _fallbackSprite = CreateDotSprite();
        _popup = GetComponent<PickupPopup>();
        if (_popup == null) _popup = gameObject.AddComponent<PickupPopup>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        if (GetComponent<CursorRangeIndicator>() == null) gameObject.AddComponent<CursorRangeIndicator>();
    }

    void Start()
    {
        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.OnObjectChanged += HandleObjectChanged;
            _currentColor = ObjectManager.Instance.CurrentObject.pileColor;
        }

        if (ZoneManager.Instance != null)
            ZoneManager.Instance.OnZoneChanged += RefreshVisibility;

        // 첫 파괴 때 프레임이 튀지 않게 풀을 미리 채움
        for (int i = 0; i < maxPiecesPerBreak; i++)
            _pool.Push(CreatePiece());
    }

    void OnDestroy()
    {
        if (ObjectManager.Instance != null)
            ObjectManager.Instance.OnObjectChanged -= HandleObjectChanged;

        if (ZoneManager.Instance != null)
            ZoneManager.Instance.OnZoneChanged -= RefreshVisibility;
    }

    void Update()
    {
        if (Mouse.current == null || Camera.main == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        if (Mouse.current.leftButton.wasPressedThisFrame && IsOverButton(screenPos))
            SfxPlayer.Play(_audioSource, mouseClickSound, priority: true);
        UpdateHoverPickup(worldPos, screenPos != _lastMouseScreenPos);
        _lastMouseScreenPos = screenPos;

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (UIPointerGuard.IsPointerOverUI) return;

        // 파편이 오브젝트보다 앞이라 파편을 누르면 뒤 오브젝트는 안 맞음
        Piece nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Collider2D hit in Physics2D.OverlapPointAll(worldPos))
        {
            if (!_pieceByCollider.TryGetValue(hit, out Piece piece) || !piece.collider.enabled) continue;

            float distance = ((Vector2)piece.renderer.transform.position - worldPos).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = piece;
                nearestDistance = distance;
            }
        }
        if (nearest != null) Collect(nearest, false);
    }

    // 자동 줍기보다 좋아지지 않게 판정이 클릭보다 좁음
    private void UpdateHoverPickup(Vector2 worldPos, bool mouseMoved)
    {
        if (UIPointerGuard.IsPointerOverUI || PieceTutorial.Active
            || UpgradeManager.Instance == null || !UpgradeManager.Instance.HoverPickupIsUnlocked)
        {
            _hovered.Clear();
            return;
        }

        _hoverHits.Clear();
        Piece top = null;
        int topOrder = -1;
        float cursorRadius = UpgradeManager.Instance.CursorRadius;
        Collider2D[] hits = cursorRadius > 0f ? Physics2D.OverlapCircleAll(worldPos, cursorRadius) : Physics2D.OverlapPointAll(worldPos);
        foreach (Collider2D hit in hits)
        {
            if (!_pieceByCollider.TryGetValue(hit, out Piece piece) || !piece.collider.enabled) continue;

            Transform pieceTransform = piece.renderer.transform;
            float radius = piece.collider.radius / pickupColliderScale * hoverPickupRadiusScale * pieceTransform.lossyScale.x + cursorRadius;
            if (((Vector2)pieceTransform.position - worldPos).sqrMagnitude > radius * radius) continue;

            _hoverHits.Add(piece);
            if (_hovered.Contains(piece)) continue;

            int order = _groundPieces.IndexOf(piece) + (piece.isCrystal ? _groundPieces.Count : 0);
            if (order > topOrder)
            {
                top = piece;
                topOrder = order;
            }
        }

        _hovered.Clear();
        foreach (Piece piece in _hoverHits) _hovered.Add(piece);

        if (!mouseMoved || top == null) return; // 가만히 둔 커서 밑으로 떨어진 파편은 안 주움
        _hovered.Remove(top);
        Collect(top, true);

        int layers = UpgradeManager.Instance.HoverPickupLayerCount;
        if (layers <= 0) return;
        int layerSize = Mathf.Max(1, Mathf.CeilToInt(MaxGroundPieces / (float)LayerCount));
        long quota = layers >= LayerCount ? long.MaxValue : (long)layers * layerSize;
        int zone = CurrentZone;
        int rank = 0;
        for (int i = _groundPieces.Count - 1; i >= 0; i--)
        {
            Piece piece = _groundPieces[i];
            if (piece.zone != zone) continue;
            bool inLayers = piece.isCrystal || rank < quota; // 결정은 층과 상관없이
            if (!piece.isCrystal) rank++;
            if (!inLayers || !_hoverHits.Contains(piece)) continue;
            _hovered.Remove(piece);
            Collect(piece, true);
        }
    }

    public void DropPieces(Vector3 fromPosition, int objectIndex, long reward, int zone = -1)
    {
        if (reward <= 0) return;

        if (zone < 0) zone = CurrentZone;
        bool visible = zone == CurrentZone;
        int count = (int)System.Math.Min(reward, maxPiecesPerBreak);
        int onFloor = CountInZone(zone);
        int real = Mathf.Clamp(MaxGroundPieces - onFloor, 0, count);
        float fill = (float)onFloor / Mathf.Max(1, MaxGroundPieces);
        Color color = PileColorFor(objectIndex);

        if (real > 0)
        {
            long baseValue = reward / real;
            long remainder = reward % real;
            var dropped = new List<Transform>(real);
            for (int i = 0; i < real; i++)
                dropped.Add(Spawn(fromPosition, zone, baseValue + (i < remainder ? 1 : 0), false, color, pieceSize, fill));
            if (visible) OnPiecesDropped?.Invoke(dropped);
        }
        else
        {
            AddToFloorPieces(zone, reward, count);
        }

        if (visible)
            for (int i = real; i < count; i++)
                EmitShard(fromPosition, GetLandingPoint(fill), color);
    }

    private void AddToFloorPieces(int zone, long reward, int count)
    {
        _mergeTargets.Clear();
        foreach (Piece piece in _groundPieces)
            if (piece.zone == zone && !piece.isCrystal) _mergeTargets.Add(piece);

        if (_mergeTargets.Count == 0)
        {
            CurrencyManager.Instance?.AddPieces(reward);
            return;
        }

        int targets = Mathf.Min(count, _mergeTargets.Count);
        long share = reward / targets;
        long remainder = reward % targets;
        int offset = Random.Range(0, _mergeTargets.Count);
        for (int i = 0; i < targets; i++)
        {
            int index = (offset + (int)((long)i * _mergeTargets.Count / targets)) % _mergeTargets.Count;
            _mergeTargets[index].value += share + (i < remainder ? 1 : 0);
        }
    }

    // GameObject 없는 파티클이라 수백 개도 가벼움
    private void EmitShard(Vector3 fromPosition, Vector3 landingPoint, Color color)
    {
        if (_shardSystems == null || _shardSystems.Length == 0) CreateShardSystems();

        Vector3 velocity = (landingPoint - fromPosition) / fallDuration;
        velocity.z = 0f;
        var emit = new ParticleSystem.EmitParams
        {
            position = fromPosition,
            velocity = velocity,
            startLifetime = fallDuration,
            startColor = color,
        };
        _shardSystems[Random.Range(0, _shardSystems.Length)].Emit(emit, 1);
    }

    private void CreateShardSystems()
    {
        Sprite[] sprites = pieceSprites != null && pieceSprites.Length > 0 ? pieceSprites : new[] { _fallbackSprite };
        Piece sample = _groundPieces.Count > 0 ? _groundPieces[0] : _pool.Count > 0 ? _pool.Peek() : CreatePiece();
        if (!_groundPieces.Contains(sample) && !_pool.Contains(sample)) _pool.Push(sample);

        _shardSystems = new ParticleSystem[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            var systemObject = new GameObject("FallingShards");
            systemObject.transform.SetParent(transform, false);
            ParticleSystem system = systemObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 설정을 바꾸려면 멈춰 있어야 함

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = Mathf.Max(1, maxFallingShards / sprites.Length);
            Vector3 size = sprites[i].bounds.size * pieceSize;
            main.startSize3D = true;
            main.startSizeX = size.x;
            main.startSizeY = size.y;
            main.startSizeZ = 1f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            // 실제 파편은 진행률 제곱으로 가속 - 속도 배율 0→2로 맞춤
            ParticleSystem.VelocityOverLifetimeModule speed = system.velocityOverLifetime;
            speed.enabled = true;
            speed.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 2f));

            var shardRenderer = systemObject.GetComponent<ParticleSystemRenderer>();
            shardRenderer.material = new Material(sample.renderer.sharedMaterial) { mainTexture = sprites[i].texture };
            shardRenderer.sortingOrder = 10;

            system.Play();
            _shardSystems[i] = system;
        }
    }

    private const int MaxGroundCrystals = 50; // 이 이상이면 새 결정은 바닥 결정에 합침

    public void DropCrystal(Vector3 fromPosition, long amount, int zone = -1)
    {
        if (amount <= 0) return;
        if (zone < 0) zone = CurrentZone;
        Piece mergeInto = null;
        int crystals = 0;
        foreach (Piece piece in _groundPieces)
        {
            if (piece.zone != zone || !piece.isCrystal) continue;
            crystals++;
            if (Random.Range(0, crystals) == 0) mergeInto = piece;
        }
        if (crystals >= MaxGroundCrystals && mergeInto != null)
        {
            mergeInto.value += amount;
            if (zone == CurrentZone) EmitShard(fromPosition, GetLandingPoint(0f), crystalColor);
            return;
        }
        Spawn(fromPosition, zone, amount, true, crystalColor, pieceSize * crystalSizeMultiplier, (float)CountInZone(zone) / Mathf.Max(1, MaxGroundPieces));
    }

    private Transform Spawn(Vector3 fromPosition, int zone, long value, bool isCrystal, Color color, float size, float fill)
    {
        Piece piece = _pool.Count > 0 ? _pool.Pop() : CreatePiece();
        piece.value = value;
        piece.isCrystal = isCrystal;
        piece.zone = zone;
        piece.landed = false;
        piece.sweepMarked = false;
        piece.sweepOnLand = false;
        bool ownSprite = isCrystal && crystalSprites != null && crystalSprites.Length > 0; // 결정 전용 그림이 있으면 색칠 안 함
        piece.renderer.sprite = ownSprite ? crystalSprites[Random.Range(0, crystalSprites.Length)] : GetRandomPieceSprite();
        piece.renderer.color = ownSprite ? Color.white : color;
        piece.renderer.sortingOrder = isCrystal ? 11 : 10; // 결정이 파편에 묻히지 않게 위에
        piece.renderer.forceRenderingOff = zone != CurrentZone;
        piece.renderer.transform.localScale = Vector3.one * size;
        piece.collider.enabled = false;

        Vector3 extents = piece.renderer.sprite.bounds.extents;
        piece.collider.radius = Mathf.Max(extents.x, extents.y) * pickupColliderScale;

        _groundPieces.Add(piece);
        Vector3 landingPoint = GetLandingPoint(fill);
        // 100만 개마다 깊이가 처음으로 돌아가 순서가 한 번 뒤집힘
        landingPoint.z = -(_spawnSequence++ % 1000000) * 0.000001f;
        StartCoroutine(FallThenSettle(piece, fromPosition, landingPoint));
        return piece.renderer.transform;
    }

    public Bounds FloorBounds => spawnArea.bounds;

    public bool HasLandedPieces()
    {
        foreach (Piece piece in _groundPieces)
            if (piece.landed) return true;
        return false;
    }

    public void MarkTopLayersForSweep(int layers)
    {
        _sweepAll = layers >= LayerCount;
        int layerSize = Mathf.Max(1, Mathf.CeilToInt(MaxGroundPieces / (float)LayerCount));
        long quota = layers >= LayerCount ? long.MaxValue : (long)layers * layerSize;
        var taken = new Dictionary<int, long>();

        for (int i = _groundPieces.Count - 1; i >= 0; i--)
        {
            Piece piece = _groundPieces[i];
            if (piece.isCrystal)
            {
                piece.sweepMarked = true;
                continue;
            }

            taken.TryGetValue(piece.zone, out long count);
            piece.sweepMarked = count < quota;
            if (piece.sweepMarked) taken[piece.zone] = count + 1;
        }
    }

    public void CollectMarkedBetween(float fromX, float toX)
    {
        for (int i = _groundPieces.Count - 1; i >= 0; i--)
        {
            Piece piece = _groundPieces[i];
            float pieceX = piece.renderer.transform.position.x;
            if (!(piece.sweepMarked || _sweepAll) || pieceX <= fromX || pieceX > toX) continue;
            if (piece.landed) Collect(piece, true, carry: true);
            else piece.sweepOnLand = true;
        }
    }

    public const string ClickSoundClass = "click-sound";

    private static readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

    private static bool IsOverButton(Vector2 screenPos)
    {
        if (EventSystem.current != null)
        {
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPos }, _uiHits);
            if (_uiHits.Count > 0)
            {
                Selectable selectable = _uiHits[0].gameObject.GetComponentInParent<Selectable>();
                // 트리 판(ScrollRect)의 Button은 배경으로 봄
                if (selectable != null && selectable.IsInteractable() && selectable.GetComponent<ScrollRect>() == null) return true;
            }
        }

        foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            IPanel panel = document.rootVisualElement?.panel;
            if (panel == null) continue;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            for (VisualElement element = panel.Pick(panelPos); element != null; element = element.parent)
                if ((element is UnityEngine.UIElements.Button || element.ClassListContains(ClickSoundClass)) && element.enabledInHierarchy) return true;
        }
        return false;
    }

    private void Collect(Piece piece, bool mergePopup, bool carry = false)
    {
        if (piece.zone == CurrentZone)
        {
            _popup.Show(piece.renderer.transform.position, piece.value, piece.isCrystal, mergePopup);
            SfxPlayer.Play(_audioSource, pickupSound);
        }
        if (piece.isCrystal)
            CurrencyManager.Instance?.AddCrystals(piece.value);
        else
            CurrencyManager.Instance?.AddPieces(piece.value);
        OnPieceCollected?.Invoke(piece.renderer.transform);

        if (carry && piece.zone == CurrentZone && _carried.Count < maxCarriedPieces)
            Carry(piece);
        else
            ReturnToPool(piece);
    }

    private void Carry(Piece piece)
    {
        _groundPieces.Remove(piece);
        piece.collider.enabled = false;
        piece.landed = false;

        Bounds floor = spawnArea.bounds;
        float pileLength = carryPileWidth * Mathf.Sqrt((_carried.Count + 1f) / Mathf.Max(1, maxCarriedPieces));
        float forward = Random.Range(0f, pileLength);
        float height = floor.size.y * (1f - 0.6f * forward / Mathf.Max(0.01f, carryPileWidth));
        piece.carryOffset = new Vector2(forward, Random.Range(0f, height));
        piece.carryCurrent = new Vector2(float.NaN, piece.renderer.transform.position.y - floor.min.y);
        _carried.Add(piece);
    }

    public void MoveCarried(float broomX)
    {
        float floorY = spawnArea.bounds.min.y;
        float blend = 1f - Mathf.Exp(-carryGatherSpeed * Time.deltaTime);
        foreach (Piece piece in _carried)
        {
            Transform pieceTransform = piece.renderer.transform;
            if (float.IsNaN(piece.carryCurrent.x))
                piece.carryCurrent.x = pieceTransform.position.x - broomX;
            piece.carryCurrent = Vector2.Lerp(piece.carryCurrent, piece.carryOffset, blend);
            pieceTransform.position = new Vector3(broomX + piece.carryCurrent.x, floorY + piece.carryCurrent.y, pieceTransform.position.z);
            piece.renderer.forceRenderingOff = piece.zone != CurrentZone;
        }
    }

    public void ReleaseCarried()
    {
        _sweepAll = false;
        foreach (Piece piece in _carried)
            ReturnToPool(piece);
        _carried.Clear();
    }

    public void ClearAll()
    {
        StopAllCoroutines();
        ReleaseCarried();
        for (int i = _groundPieces.Count - 1; i >= 0; i--)
            ReturnToPool(_groundPieces[i]);
    }

    private void ReturnToPool(Piece piece)
    {
        _groundPieces.Remove(piece);
        piece.collider.enabled = false;
        piece.renderer.gameObject.SetActive(false);
        _pool.Push(piece);
    }

    private int CountInZone(int zone)
    {
        int count = 0;
        foreach (Piece piece in _groundPieces)
            if (piece.zone == zone && !piece.isCrystal) count++;
        return count;
    }

    private Color PileColorFor(int objectIndex)
    {
        if (ObjectManager.Instance != null && objectIndex >= 0 && objectIndex < ObjectManager.StaticObjectCount)
            return ObjectManager.Instance.WaxColorOf(objectIndex);

        return _currentColor;
    }

    private IEnumerator FallThenSettle(Piece piece, Vector3 fromPosition, Vector3 landingPoint)
    {
        Transform pieceTransform = piece.renderer.transform;
        pieceTransform.position = fromPosition;
        piece.renderer.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fallDuration);
            float easedProgress = progress * progress;

            pieceTransform.position = Vector3.Lerp(fromPosition, landingPoint, easedProgress);
            yield return null;
        }

        pieceTransform.position = landingPoint;
        piece.landed = true;
        ApplyVisibility(piece);
        if (piece.sweepOnLand) Collect(piece, true);
    }

    private void RefreshVisibility()
    {
        foreach (Piece piece in _groundPieces)
            ApplyVisibility(piece);
    }

    private void ApplyVisibility(Piece piece)
    {
        bool visible = piece.zone == CurrentZone;
        piece.renderer.forceRenderingOff = !visible;
        piece.collider.enabled = visible && piece.landed;
    }

    private Piece CreatePiece()
    {
        var pieceObject = new GameObject("Piece");
        pieceObject.transform.SetParent(transform);
        pieceObject.transform.localScale = Vector3.one * pieceSize;

        var piece = new Piece
        {
            renderer = pieceObject.AddComponent<SpriteRenderer>(),
            collider = pieceObject.AddComponent<CircleCollider2D>(),
        };
        piece.renderer.sortingOrder = 10;
        piece.collider.isTrigger = true;
        piece.collider.enabled = false;
        _pieceByCollider[piece.collider] = piece;

        pieceObject.SetActive(false);
        return piece;
    }

    private Sprite GetRandomPieceSprite()
    {
        if (pieceSprites == null || pieceSprites.Length == 0)
            return _fallbackSprite;

        return pieceSprites[Random.Range(0, pieceSprites.Length)];
    }

    // 한쪽에만 몰리지 않게 부순 위치 대신 바닥 가운데부터 쌓음
    private Vector3 GetLandingPoint(float fill)
    {
        Bounds bounds = spawnArea.bounds;

        float halfWidth = Mathf.Lerp(initialSpread, bounds.extents.x, Mathf.Clamp01(fill));

        for (int attempt = 0; attempt < 10; attempt++)
        {
            float x = Mathf.Clamp(bounds.center.x + Random.Range(-halfWidth, halfWidth), bounds.min.x, bounds.max.x);
            float y = Random.Range(bounds.min.y, bounds.max.y);
            Vector2 point = new Vector2(x, y);

            if (spawnArea.OverlapPoint(point))
                return point;
        }

        return new Vector3(bounds.center.x, bounds.min.y, 0f);
    }

    private void HandleObjectChanged(ObjectData newObject)
    {
        _currentColor = newObject.pileColor;
    }

    private Sprite CreateDotSprite()
    {
        var texture = new Texture2D(4, 4);
        var pixels = new Color[16];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;

        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }
}
