using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;

// 오브젝트를 부수면 얻은 조각만큼 파편이 부서진 지점에서 떨어져 바닥(spawnArea)에 쌓이고, 파편을 클릭해서 주워야 조각(돈)이 들어오는 시스템.
// 파편 하나가 여러 조각 값을 들고 있을 수 있음 (한 번에 떨어뜨리는 개수에 상한이 있어서 총액을 나눠 담음).
// 파편은 부서진 오브젝트가 있는 존 바닥에 따로 쌓이고(안 보는 존에서 부서져도), 지금 보는 존의 파편만 보이고 주울 수 있음.
// 존을 옮겨도 그대로 남아 있고 주울 때까지 사라지지 않음.
// 한 존의 파편 수는 바닥 최대 개수를 절대 넘지 않음 - 꽉 찬 뒤에 떨어지는 건 줍기 판정 없는 연출용 파티클이고, 값은 바닥에 있는 파편들에 더해짐
public class DebrisPool : MonoBehaviour
{
    // 씬 어디서든 DebrisPool.Instance로 접근하기 위한 싱글톤
    public static DebrisPool Instance { get; private set; }

    public const int LayerCount = 5; // 바닥 파편을 나누는 층 수 - 나중에 떨어진 파편일수록 위층. 한 층 = 바닥 최대 개수의 1/5

    [SerializeField] private Collider2D spawnArea; // 조각이 떨어질 바닥 범위 콜라이더 (이 안 랜덤한 위치로 떨어짐)
    [SerializeField] private int maxPiecesPerBreak = 50; // 한 번 부술 때 떨어뜨릴 파편 수의 상한 (렉 방지 - 넘치는 조각은 파편 하나에 합쳐 담음)
    [SerializeField] private float pieceSize = 1f; // 조각 하나의 크기 (월드 유닛)
    [SerializeField] private float pickupColliderScale = 1.4f; // 줍기 판정 콜라이더를 파편 그림보다 이 배율만큼 크게 (잘 주워지게)
    [SerializeField] private Sprite[] pieceSprites; // 조각으로 쓸 스프라이트들 (BreakPieces 폴더의 파편 이미지들). 색은 부순 오브젝트의 pileColor로 입힘
    [SerializeField] private float initialSpread = 1f; // 처음엔 바닥 가운데에서 좌우 이 거리(월드 유닛) 안으로만 떨어지고, 쌓일수록 spawnArea 폭까지 점점 넓어짐
    [SerializeField] private float fallDuration = 0.4f; // 부서진 지점에서 바닥까지 떨어지는 데 걸리는 시간(초)
    [SerializeField] private Color crystalColor = new Color(0.45f, 0.9f, 1f); // 결정 색 (전용 이미지가 생기기 전까지 파편 그림에 이 색을 입혀 구분)
    [SerializeField] private float crystalSizeMultiplier = 1.6f; // 결정을 파편보다 이 배율만큼 크게
    [SerializeField] private int maxFallingShards = 2000; // 연출용 파편(파티클)이 동시에 떠 있을 수 있는 수
    [SerializeField] private float hoverPickupRadiusScale = 0.7f; // 호버 줍기 판정 크기 - 파편 그림 대비 배율 (작을수록 커서가 파편 한가운데에 정확히 올라가야 주워짐)
    [SerializeField] private int maxCarriedPieces = 80; // 빗자루가 앞에 모아서 밀고 갈 수 있는 파편 수 - 넘치는 건 주운 자리에서 바로 사라짐
    [SerializeField] private float carryPileWidth = 1.2f; // 빗자루 앞 더미가 꽉 찼을 때의 가로 길이(월드 유닛) - 쓸어 담을수록 이 길이까지 앞으로 길어짐
    [SerializeField] private float carryGatherSpeed = 12f; // 쓸린 파편이 더미 안 제자리로 끌려가는 속도 (클수록 빨리 붙음)
    [SerializeField] private AudioClip pickupSound; // 지금 보는 존에서 파편/결정을 주울 때 소리
    [SerializeField] private AudioClip mouseClickSound; // 버튼을 누를 때 소리

    // 바닥에 떨어져 있는(또는 떨어지는 중인) 파편 하나
    private class Piece
    {
        public SpriteRenderer renderer; // 파편 그림
        public CircleCollider2D collider; // 줍기 판정 (착지 후에만 켜짐)
        public long value; // 주웠을 때 들어오는 조각(결정이면 결정) 개수
        public bool isCrystal; // 결정인지 (결정은 바닥 최대 개수에 안 들어감)
        public int zone; // 떨어진 존 번호 (그 존을 보고 있을 때만 보이고 주울 수 있음)
        public bool landed; // 착지를 마쳤는지
        public bool sweepMarked; // 이번 자동 줍기가 쓸어 담을 대상인지 (자동 줍기가 출발할 때 위층부터 정해짐)
        public Vector2 carryOffset; // 빗자루에 쓸려 밀려갈 때 더미 안에서 놓일 자리 (빗자루 x, 바닥 아래끝 기준)
        public Vector2 carryCurrent; // 지금 빗자루 기준으로 있는 자리 - 쓸린 자리에서 carryOffset으로 끌려감
    }

    private readonly List<Piece> _groundPieces = new List<Piece>(); // 지금 바닥에 있거나 떨어지는 중인 파편들
    private readonly Dictionary<Collider2D, Piece> _pieceByCollider = new Dictionary<Collider2D, Piece>(); // 클릭된 콜라이더로 파편을 찾기 위한 표
    private readonly Stack<Piece> _pool = new Stack<Piece>(); // 재사용 가능한(비활성) 파편들
    private Color _currentColor = Color.white; // ObjectManager를 못 찾을 때 폴백으로 쓸 색
    private Sprite _fallbackSprite; // pieceSprites가 비어있을 때 대신 쓸 흰색 정사각형 스프라이트
    private readonly HashSet<Piece> _hovered = new HashSet<Piece>(); // 직전 프레임에 커서가 올라가 있던 파편들 - 커서가 "새로" 올라간 파편만 줍기 위해 기억
    private readonly List<Piece> _hoverHits = new List<Piece>(); // 이번 프레임에 커서가 올라가 있는 파편들 (매 프레임 재사용)
    private Vector2 _lastMouseScreenPos; // 직전 프레임 마우스 화면 위치 - 마우스를 움직였는지 확인용
    private int _spawnSequence; // 지금까지 떨어뜨린 파편 수 - 나중에 떨어진 파편이 위에 그려지도록 깊이(z)를 정하는 데 씀
    private PickupPopup _popup; // 주운 자리에 "+N"을 띄우는 연출 (같은 오브젝트에 없으면 자동으로 붙임)
    // 연출용 파편을 그리는 파티클 (파편 그림마다 하나씩, 처음 쓸 때 만듦). 에디터가 private 배열을 빈 배열로 채워두는 일이 있어서 저장 대상에서 뺌
    [System.NonSerialized] private ParticleSystem[] _shardSystems;
    private readonly List<Piece> _mergeTargets = new List<Piece>(); // 바닥이 꽉 찼을 때 값을 나눠 받을 파편들 (매번 재사용)
    private readonly List<Piece> _carried = new List<Piece>(); // 빗자루가 쓸어서 앞에 모아 밀고 가는 파편들 - 값은 이미 지급됐고 그림만 남은 것
    private AudioSource _audioSource; // 줍기/마우스 클릭 소리를 내는 소스 (없으면 자동으로 붙임)

    // 오브젝트를 부숴 파편을 새로 떨어뜨릴 때마다 그 파편들을 전달 (결정 제외) - 첫 파편 튜토리얼 등이 구독
    public event System.Action<IReadOnlyList<Transform>> OnPiecesDropped;
    // 파편(결정 포함)을 주울 때마다 그 파편을 전달
    public event System.Action<Transform> OnPieceCollected;

    public float FallDuration => fallDuration; // 부서진 지점에서 바닥까지 떨어지는 시간(초)

    private int CurrentZone => ZoneManager.Instance != null ? ZoneManager.Instance.CurrentZone : 0; // 지금 보고 있는 존
    private int MaxGroundPieces => UpgradeManager.Instance != null ? UpgradeManager.Instance.MaxGroundPieces : 400; // 한 존 바닥에 동시에 있을 수 있는 파편 수

    void Awake()
    {
        // 씬에 DebrisPool이 중복으로 존재하지 않도록 방지
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
        if (GetComponent<CursorRangeIndicator>() == null) gameObject.AddComponent<CursorRangeIndicator>(); // 커서 범위 표시도 같은 식으로 자동으로 붙임
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

        // 첫 몇 번 부술 때 파편 GameObject를 한꺼번에 만드느라 프레임이 튀는 걸 막으려고 풀을 미리 채워둠
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

        Vector2 screenPos = Mouse.current.position.ReadValue(); // 마우스 화면 위치
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

        // 버튼(업그레이드 노드, 메뉴/무기/존 화살표 등)을 누를 때만 클릭 소리
        if (Mouse.current.leftButton.wasPressedThisFrame && IsOverButton(screenPos))
            SfxPlayer.Play(_audioSource, mouseClickSound, priority: true);
        UpdateHoverPickup(worldPos, screenPos != _lastMouseScreenPos);
        _lastMouseScreenPos = screenPos;

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (UIPointerGuard.IsPointerOverUI) return;

        // 파편은 오브젝트보다 앞에 그려져서 Click.TopmostAt도 파편을 맨 위로 봄 - 파편을 누르면 뒤 오브젝트는 안 맞음.
        // 줍기 판정 원이 그림보다 커서 여러 파편이 겹칠 수 있음 - 그중 중심이 마우스에 가장 가까운 것 하나만 주움
        Piece nearest = null; // 지금까지 찾은 가장 가까운 파편
        float nearestDistance = float.MaxValue; // 그 파편까지 거리(제곱)
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

    // 호버 줍기(업그레이드로 해금) - 클릭 없이 커서가 파편 위로 "새로 올라간" 순간, 그 자리의 맨 위 파편 하나만 주움.
    // 겹쳐 쌓인 아래 파편은 커서가 벗어났다가 다시 올라와야 주워져서, 더미는 여러 번 왔다갔다 해야 다 주워짐.
    // 자동 줍기보다 좋아지지 않게 판정은 클릭 줍기보다 좁고(파편 그림 안쪽), 마우스를 움직이지 않으면 안 주워짐
    private void UpdateHoverPickup(Vector2 worldPos, bool mouseMoved)
    {
        if (UIPointerGuard.IsPointerOverUI || PieceTutorial.Active
            || UpgradeManager.Instance == null || !UpgradeManager.Instance.HoverPickupIsUnlocked)
        {
            _hovered.Clear();
            return;
        }

        _hoverHits.Clear();
        Piece top = null; // 이번에 새로 올라간 파편 중 맨 위(가장 나중에 떨어진) 것
        int topOrder = -1; // 그 파편의 쌓인 순서
        float cursorRadius = UpgradeManager.Instance.CursorRadius; // 커서 범위 업그레이드로 넓어진 반지름 (없으면 0)
        Collider2D[] hits = cursorRadius > 0f ? Physics2D.OverlapCircleAll(worldPos, cursorRadius) : Physics2D.OverlapPointAll(worldPos); // 커서(범위)에 걸친 콜라이더들
        foreach (Collider2D hit in hits)
        {
            if (!_pieceByCollider.TryGetValue(hit, out Piece piece) || !piece.collider.enabled) continue;

            Transform pieceTransform = piece.renderer.transform; // 이 파편의 위치/크기
            float radius = piece.collider.radius / pickupColliderScale * hoverPickupRadiusScale * pieceTransform.lossyScale.x + cursorRadius; // 호버 판정 반경(월드)
            if (((Vector2)pieceTransform.position - worldPos).sqrMagnitude > radius * radius) continue;

            _hoverHits.Add(piece);
            if (_hovered.Contains(piece)) continue; // 이미 커서 밑에 있던 파편은 다시 올라올 때까지 대상 아님

            int order = _groundPieces.IndexOf(piece) + (piece.isCrystal ? _groundPieces.Count : 0); // 나중에 떨어진 것일수록 위, 결정은 항상 맨 위에 그려지니 최우선
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

        // 커서 줍기 층 업그레이드: 커서 밑 파편 중 위쪽 그 층 수 안에 드는 건 한 번에 전부 주움 (층 나누는 법은 자동 줍기와 같음)
        int layers = UpgradeManager.Instance.HoverPickupLayerCount; // 한 번에 줍는 층 수 (0이면 위 1개만)
        if (layers <= 0) return;
        int layerSize = Mathf.Max(1, Mathf.CeilToInt(MaxGroundPieces / (float)LayerCount)); // 한 층에 들어가는 파편 수
        long quota = layers >= LayerCount ? long.MaxValue : (long)layers * layerSize; // 위에서부터 이 순위 안의 파편까지 주움
        int zone = CurrentZone; // 지금 보는 존 (커서로는 이 존 파편만 주움)
        int rank = 0; // 위에서부터 센 순위 (나중에 떨어진 것이 위)
        for (int i = _groundPieces.Count - 1; i >= 0; i--)
        {
            Piece piece = _groundPieces[i]; // 위에서부터 차례로
            if (piece.zone != zone) continue;
            bool inLayers = piece.isCrystal || rank < quota; // 결정은 층과 상관없이 대상
            if (!piece.isCrystal) rank++;
            if (!inLayers || !_hoverHits.Contains(piece)) continue;
            _hovered.Remove(piece);
            Collect(piece, true);
        }
    }

    // 오브젝트를 부쉈을 때 호출 - reward 조각을 파편 여러 개(최대 maxPiecesPerBreak개)에 나눠 담아 fromPosition에서 zone 바닥으로 떨어뜨림
    // (zone이 -1이면 보고 있는 존. 안 보이는 존이면 떨어지는 모습 없이 그 존 바닥에 쌓이기만 함).
    // 이 존 바닥에 남은 자리만큼만 실제 파편이 되고, 자리가 없어서 못 만든 만큼은 줍기 판정 없는 연출용 파편으로 떨어짐 - 눈에는 똑같이 쏟아지지만
    // 파편 수는 바닥 최대 개수를 넘지 않음. 값은 실제 파편들이 전부 나눠 갖고, 실제 파편을 하나도 못 만들면 바닥에 있던 파편들에 더해져서 놓치는 게 없음
    public void DropPieces(Vector3 fromPosition, int objectIndex, long reward, int zone = -1)
    {
        if (reward <= 0) return;

        if (zone < 0) zone = CurrentZone; // 떨어뜨릴 존
        bool visible = zone == CurrentZone; // 지금 보는 존에 떨어지는지 - 아니면 연출(파티클/튜토리얼)은 생략
        int count = (int)System.Math.Min(reward, maxPiecesPerBreak); // 눈에 보이게 떨어뜨릴 파편 수
        int onFloor = CountInZone(zone); // 이 존에 지금 있는 파편 수 (떨어지는 중인 것 포함)
        int real = Mathf.Clamp(MaxGroundPieces - onFloor, 0, count); // 그중 실제 파편으로 만들 수 있는 수 (남은 자리만큼)
        float fill = (float)onFloor / Mathf.Max(1, MaxGroundPieces); // 바닥이 찬 정도 0~1 - 찰수록 넓게 퍼져 떨어짐
        Color color = PileColorFor(objectIndex);

        if (real > 0)
        {
            long baseValue = reward / real; // 파편 하나당 기본 값
            long remainder = reward % real; // 나누고 남은 조각 - 앞쪽 파편들에 1개씩 더 얹음
            var dropped = new List<Transform>(real); // 이번에 떨어뜨린 실제 파편들
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

    // 바닥이 꽉 차서 새 파편을 못 만들 때 - 이 존에 있는 파편 중 고르게 뽑은 최대 count개에 reward를 나눠 더함
    private void AddToFloorPieces(int zone, long reward, int count)
    {
        _mergeTargets.Clear();
        foreach (Piece piece in _groundPieces)
            if (piece.zone == zone && !piece.isCrystal) _mergeTargets.Add(piece);

        if (_mergeTargets.Count == 0) // 바닥 최대 개수가 0 이하로 잡힌 경우 - 쌓을 곳이 없으니 바로 지급
        {
            CurrencyManager.Instance?.AddPieces(reward);
            return;
        }

        int targets = Mathf.Min(count, _mergeTargets.Count); // 값을 나눠 받을 파편 수
        long share = reward / targets; // 파편 하나가 받는 값
        long remainder = reward % targets; // 나누고 남은 조각 - 앞쪽 파편들에 1개씩 더 얹음
        int offset = Random.Range(0, _mergeTargets.Count); // 매번 같은 파편만 커지지 않게 시작 자리를 바꿈
        for (int i = 0; i < targets; i++)
        {
            int index = (offset + (int)((long)i * _mergeTargets.Count / targets)) % _mergeTargets.Count; // 아래층부터 위층까지 고르게
            _mergeTargets[index].value += share + (i < remainder ? 1 : 0);
        }
    }

    // 줍기 판정 없이 fromPosition에서 landingPoint로 떨어지다 사라지는 연출용 파편 하나 (실제 파편과 같은 그림/색/속도).
    // 게임 오브젝트를 만들지 않는 파티클이라 수백 개가 한꺼번에 떨어져도 가벼움
    private void EmitShard(Vector3 fromPosition, Vector3 landingPoint, Color color)
    {
        if (_shardSystems == null || _shardSystems.Length == 0) CreateShardSystems();

        Vector3 velocity = (landingPoint - fromPosition) / fallDuration; // 평균 속도 - 속도 배율 곡선(0 -> 2)이 곱해져 실제 파편처럼 점점 빨라짐
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

    // 파편 그림마다 파티클 시스템을 하나씩 만듦 (파티클 하나는 그림 하나만 쓸 수 있어서). 재질은 실제 파편과 같은 것을 써서 똑같이 보이게 함
    private void CreateShardSystems()
    {
        Sprite[] sprites = pieceSprites != null && pieceSprites.Length > 0 ? pieceSprites : new[] { _fallbackSprite }; // 파편으로 쓰는 그림들
        Piece sample = _groundPieces.Count > 0 ? _groundPieces[0] : _pool.Count > 0 ? _pool.Peek() : CreatePiece(); // 재질을 가져올 실제 파편
        if (!_groundPieces.Contains(sample) && !_pool.Contains(sample)) _pool.Push(sample);

        _shardSystems = new ParticleSystem[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            var systemObject = new GameObject("FallingShards"); // 이 그림 전용 파티클
            systemObject.transform.SetParent(transform, false);
            ParticleSystem system = systemObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 설정을 바꾸려면 멈춰 있어야 함

            ParticleSystem.MainModule main = system.main;
            main.loop = true; // 스스로 멈추지 않게 (내보내는 건 Emit으로만 함)
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = Mathf.Max(1, maxFallingShards / sprites.Length);
            Vector3 size = sprites[i].bounds.size * pieceSize; // 실제 파편과 같은 크기
            main.startSize3D = true;
            main.startSizeX = size.x;
            main.startSizeY = size.y;
            main.startSizeZ = 1f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            // 실제 파편은 떨어질수록 빨라짐(진행률의 제곱) - 속도 배율을 0에서 2로 올리면 같은 움직임이 됨
            ParticleSystem.VelocityOverLifetimeModule speed = system.velocityOverLifetime;
            speed.enabled = true;
            speed.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 2f));

            var shardRenderer = systemObject.GetComponent<ParticleSystemRenderer>(); // 파티클 그리기 설정
            shardRenderer.material = new Material(sample.renderer.sharedMaterial) { mainTexture = sprites[i].texture };
            shardRenderer.sortingOrder = 10; // 실제 파편과 같은 순서

            system.Play();
            _shardSystems[i] = system;
        }
    }

    // 결정 하나를 zone 바닥에 떨어뜨림 (amount개짜리 한 덩어리, zone이 -1이면 보고 있는 존). 바닥이 꽉 차 있어도 떨어짐
    public void DropCrystal(Vector3 fromPosition, long amount, int zone = -1)
    {
        if (amount <= 0) return;
        if (zone < 0) zone = CurrentZone; // 떨어뜨릴 존
        Spawn(fromPosition, zone, amount, true, crystalColor, pieceSize * crystalSizeMultiplier, (float)CountInZone(zone) / Mathf.Max(1, MaxGroundPieces));
    }

    // fill = 이 존 바닥이 찬 정도(0~1) - 떨어질 자리를 얼마나 넓게 퍼뜨릴지 정함
    private Transform Spawn(Vector3 fromPosition, int zone, long value, bool isCrystal, Color color, float size, float fill)
    {
        Piece piece = _pool.Count > 0 ? _pool.Pop() : CreatePiece();
        piece.value = value;
        piece.isCrystal = isCrystal;
        piece.zone = zone;
        piece.landed = false;
        piece.sweepMarked = false;
        piece.renderer.sprite = GetRandomPieceSprite();
        piece.renderer.color = color;
        piece.renderer.sortingOrder = isCrystal ? 11 : 10; // 결정이 파편 더미에 묻히지 않게 위에 그림 (클릭도 우선)
        piece.renderer.forceRenderingOff = zone != CurrentZone; // 안 보이는 존에 떨어지는 건 처음부터 숨김
        piece.renderer.transform.localScale = Vector3.one * size;
        piece.collider.enabled = false;

        // 스프라이트가 파편마다 달라서 콜라이더 반경을 매번 그림 크기에 맞춤 (로컬 기준이라 스케일은 자동 반영)
        Vector3 extents = piece.renderer.sprite.bounds.extents;
        piece.collider.radius = Mathf.Max(extents.x, extents.y) * pickupColliderScale;

        _groundPieces.Add(piece);
        Vector3 landingPoint = GetLandingPoint(fill);
        // 나중에 떨어진 파편일수록 카메라에 조금 더 가깝게 놓아서 항상 위에 그려지게 함 (층 순서 = 떨어진 순서 = 보이는 순서)
        // ponytail: 100만 개마다 깊이가 처음으로 돌아가서 그 순간 한 번 순서가 뒤집힘 - 문제가 되면 바닥 파편 깊이를 다시 매기는 식으로 바꿀 것
        landingPoint.z = -(_spawnSequence++ % 1000000) * 0.000001f;
        StartCoroutine(FallThenSettle(piece, fromPosition, landingPoint));
        return piece.renderer.transform;
    }

    // 바닥 범위 (자동 줍기가 이 폭을 가로질러 지나감)
    public Bounds FloorBounds => spawnArea.bounds;

    // 어느 존이든 착지한 파편이 하나라도 있는지 (자동 줍기가 빈 바닥이면 안 나오게)
    public bool HasLandedPieces()
    {
        foreach (Piece piece in _groundPieces)
            if (piece.landed) return true;
        return false;
    }

    // 자동 줍기가 출발할 때 호출 - 존마다 맨 위층부터 layers개 층의 파편을 "이번에 쓸어 담을 대상"으로 표시함 (개수 제한 없음).
    // 한 층은 바닥 최대 개수의 1/LayerCount개이고, 나중에 떨어진 파편부터 위층으로 침. 결정은 층과 상관없이 항상 대상.
    // 출발한 뒤에 떨어진 파편은 대상이 아니라서 다음 자동 줍기 때 쓸림
    public void MarkTopLayersForSweep(int layers)
    {
        int layerSize = Mathf.Max(1, Mathf.CeilToInt(MaxGroundPieces / (float)LayerCount)); // 한 층에 들어가는 파편 수
        long quota = layers >= LayerCount ? long.MaxValue : (long)layers * layerSize; // 존 하나에서 쓸어 담을 파편 수 (전 층이면 전부)
        var taken = new Dictionary<int, long>(); // 존 번호 -> 지금까지 대상으로 표시한 파편 수

        for (int i = _groundPieces.Count - 1; i >= 0; i--) // 나중에 떨어진 것(위층)부터
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

    // fromX 초과 ~ toX 이하 구간에 있는, 이번 자동 줍기 대상으로 표시된 착지한 파편을 전부 주움 - 자동 줍기가 이번 프레임에 지나간 구간.
    // 이미 지나간 자리(fromX 이하)에 있는 건 안 주움. 존마다 바닥 위치가 같아서 안 보이는 존도 같은 구간으로 주움
    public void CollectMarkedBetween(float fromX, float toX)
    {
        for (int i = _groundPieces.Count - 1; i >= 0; i--)
        {
            Piece piece = _groundPieces[i];
            float pieceX = piece.renderer.transform.position.x; // 이 파편의 가로 위치
            if (piece.sweepMarked && piece.landed && pieceX > fromX && pieceX <= toX)
                Collect(piece, true, carry: true);
        }
    }

    // 버튼처럼 쓰는 UI 툴킷 요소(Button이 아닌데 클릭을 받는 것)에 붙이면 누를 때 클릭 소리가 남
    public const string ClickSoundClass = "click-sound";

    private static readonly List<RaycastResult> _uiHits = new List<RaycastResult>(); // 캔버스 UI 레이캐스트 결과 (매번 재사용)

    // 화면 좌표 아래에 누를 수 있는 버튼이 있는지 - 캔버스 UI(업그레이드 트리 노드 등)와 UI 툴킷(우측 패널, 메뉴 등) 둘 다 봄
    private static bool IsOverButton(Vector2 screenPos)
    {
        if (EventSystem.current != null)
        {
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPos }, _uiHits);
            if (_uiHits.Count > 0)
            {
                Selectable selectable = _uiHits[0].gameObject.GetComponentInParent<Selectable>(); // 맨 위에 맞은 UI의 버튼
                // 업그레이드 트리 판(ScrollRect)에도 Button이 붙어 있어서, 스크롤 판에 붙은 버튼은 배경으로 봄
                if (selectable != null && selectable.IsInteractable() && selectable.GetComponent<ScrollRect>() == null) return true;
            }
        }

        foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            IPanel panel = document.rootVisualElement?.panel; // 이 문서의 패널
            if (panel == null) continue;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y)); // 패널 좌표 (위가 0)
            for (VisualElement element = panel.Pick(panelPos); element != null; element = element.parent)
                if ((element is UnityEngine.UIElements.Button || element.ClassListContains(ClickSoundClass)) && element.enabledInHierarchy) return true;
        }
        return false;
    }

    // mergePopup이면 "+N"을 근처 것끼리 합쳐서 띄움 (빗자루/호버 줍기처럼 연달아 줍는 경우. 안 보이는 존에서 주운 건 안 띄움).
    // carry는 빗자루가 주울 때만 켬 - 그림을 바로 지우지 않고 빗자루 앞 더미에 넣음
    private void Collect(Piece piece, bool mergePopup, bool carry = false)
    {
        if (piece.zone == CurrentZone)
        {
            _popup.Show(piece.renderer.transform.position, piece.value, piece.isCrystal, mergePopup);
            SfxPlayer.Play(_audioSource, pickupSound); // 같은 소리 최소 간격이 있어서 한꺼번에 주워도 겹쳐 쌓이지 않음
        }
        if (piece.isCrystal)
            CurrencyManager.Instance?.AddCrystals(piece.value);
        else
            CurrencyManager.Instance?.AddPieces(piece.value);
        OnPieceCollected?.Invoke(piece.renderer.transform);

        // 빗자루가 주운 건 바로 지우지 않고 빗자루 앞 더미에 넣어서, 쓸려 가는 게 눈에 보이게 함
        if (carry && piece.zone == CurrentZone && _carried.Count < maxCarriedPieces)
            Carry(piece);
        else
            ReturnToPool(piece);
    }

    // 쓸린 파편을 바닥 목록에서 빼고 빗자루 앞 더미로 옮김. 더미는 쌓일수록 앞으로 길어지고, 앞쪽일수록 낮게 깔림
    private void Carry(Piece piece)
    {
        _groundPieces.Remove(piece);
        piece.collider.enabled = false;
        piece.landed = false;

        Bounds floor = spawnArea.bounds; // 더미 높이를 바닥 범위에 맞추려고 읽음
        float pileLength = carryPileWidth * Mathf.Sqrt((_carried.Count + 1f) / Mathf.Max(1, maxCarriedPieces)); // 지금 더미의 가로 길이
        float forward = Random.Range(0f, pileLength); // 더미 안에서 빗자루로부터 앞으로 떨어진 거리
        float height = floor.size.y * (1f - 0.6f * forward / Mathf.Max(0.01f, carryPileWidth)); // 그 자리에서 쌓일 수 있는 높이
        piece.carryOffset = new Vector2(forward, Random.Range(0f, height));
        piece.carryCurrent = new Vector2(float.NaN, piece.renderer.transform.position.y - floor.min.y); // 쓸린 자리에서 출발 (가로는 빗자루 위치를 아는 MoveCarried에서 채움)
        _carried.Add(piece);
    }

    // 자동 줍기가 매 프레임 호출 - 더미에 든 파편을 빗자루(broomX)와 같이 옮기면서 제자리로 끌어모음
    public void MoveCarried(float broomX)
    {
        float floorY = spawnArea.bounds.min.y; // 더미가 놓이는 바닥 높이
        float blend = 1f - Mathf.Exp(-carryGatherSpeed * Time.deltaTime); // 이번 프레임에 제자리로 다가가는 비율
        foreach (Piece piece in _carried)
        {
            Transform pieceTransform = piece.renderer.transform; // 옮길 파편
            if (float.IsNaN(piece.carryCurrent.x))
                piece.carryCurrent.x = pieceTransform.position.x - broomX; // 방금 쓸린 파편 - 빗자루 앞쪽 범위에서 주웠으면 그 자리에서 끌려오게
            piece.carryCurrent = Vector2.Lerp(piece.carryCurrent, piece.carryOffset, blend);
            pieceTransform.position = new Vector3(broomX + piece.carryCurrent.x, floorY + piece.carryCurrent.y, pieceTransform.position.z);
            piece.renderer.forceRenderingOff = piece.zone != CurrentZone; // 쓰는 도중 다른 존으로 넘어가면 숨김
        }
    }

    // 자동 줍기가 끝나면(화면 밖으로 나가면) 호출 - 밀고 가던 파편을 전부 치움
    public void ReleaseCarried()
    {
        foreach (Piece piece in _carried)
            ReturnToPool(piece);
        _carried.Clear();
    }

    // 테스트용 - 모든 존의 바닥 파편을 조각 지급 없이 전부 치움
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

    // objectIndex 오브젝트의 파편 색 = 그 오브젝트 왁스 색 - 인덱스가 이상하면 마지막으로 알던 색으로 폴백
    private Color PileColorFor(int objectIndex)
    {
        if (ObjectManager.Instance != null && objectIndex >= 0 && objectIndex < ObjectManager.StaticObjectCount)
            return ObjectManager.Instance.WaxColorOf(objectIndex);

        return _currentColor;
    }

    // fromPosition에서 landingPoint까지 중력처럼 가속하며 떨어진 뒤 줍기 판정을 켬
    private IEnumerator FallThenSettle(Piece piece, Vector3 fromPosition, Vector3 landingPoint)
    {
        Transform pieceTransform = piece.renderer.transform;
        pieceTransform.position = fromPosition;
        piece.renderer.gameObject.SetActive(true);

        float elapsed = 0f; // 코루틴 시작 후 흐른 시간

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fallDuration);
            float easedProgress = progress * progress; // 중력처럼 갈수록 빨라지는 가속 느낌 (ease-in)

            pieceTransform.position = Vector3.Lerp(fromPosition, landingPoint, easedProgress);
            yield return null;
        }

        pieceTransform.position = landingPoint;
        piece.landed = true;
        ApplyVisibility(piece);
    }

    // 존을 넘기면 그 존의 파편만 보이고 주울 수 있게 함 (다른 존 파편은 그대로 남아있음)
    private void RefreshVisibility()
    {
        foreach (Piece piece in _groundPieces)
            ApplyVisibility(piece);
    }

    private void ApplyVisibility(Piece piece)
    {
        bool visible = piece.zone == CurrentZone; // 지금 보는 존의 파편인지
        piece.renderer.forceRenderingOff = !visible;
        piece.collider.enabled = visible && piece.landed;
    }

    // 비활성 상태의 새 파편 GameObject 하나를 만듦 (풀 채우기 / 부족할 때 공용)
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
        piece.renderer.sortingOrder = 10; // 파괴 대상 오브젝트들보다 위에 그려지도록 (클릭도 오브젝트보다 우선)
        piece.collider.isTrigger = true;
        piece.collider.enabled = false;
        _pieceByCollider[piece.collider] = piece;

        pieceObject.SetActive(false);
        return piece;
    }

    // pieceSprites 중 하나를 랜덤으로 골라줌 (비어있으면 흰색 정사각형으로 대체)
    private Sprite GetRandomPieceSprite()
    {
        if (pieceSprites == null || pieceSprites.Length == 0)
            return _fallbackSprite;

        return pieceSprites[Random.Range(0, pieceSprites.Length)];
    }

    // 조각이 떨어질 지점 - 어느 오브젝트를 부쉈든 바닥 가운데부터 쌓이기 시작해서, 그 존 바닥이 찰수록(fill 0~1) 좌우로 넓게 퍼짐.
    // 부순 오브젝트 밑으로 떨어뜨리면 한쪽 오브젝트만 부술 때 파편이 그쪽에만 몰려서, 가운데를 기준으로 삼음.
    // x는 바닥 가운데에서 뽑고, 폭은 initialSpread에서 spawnArea 절반 폭까지 채움 비율에 따라 늘어남. y는 spawnArea 세로 범위 안 랜덤.
    private Vector3 GetLandingPoint(float fill)
    {
        Bounds bounds = spawnArea.bounds;

        float halfWidth = Mathf.Lerp(initialSpread, bounds.extents.x, Mathf.Clamp01(fill)); // 쌓일수록 넓게

        for (int attempt = 0; attempt < 10; attempt++)
        {
            float x = Mathf.Clamp(bounds.center.x + Random.Range(-halfWidth, halfWidth), bounds.min.x, bounds.max.x);
            float y = Random.Range(bounds.min.y, bounds.max.y);
            Vector2 point = new Vector2(x, y);

            if (spawnArea.OverlapPoint(point))
                return point;
        }

        // 10번 시도해도 콜라이더 안쪽을 못 찾으면 바닥 가운데(바닥 높이)로
        return new Vector3(bounds.center.x, bounds.min.y, 0f);
    }

    private void HandleObjectChanged(ObjectData newObject)
    {
        _currentColor = newObject.pileColor;
    }

    // 모든 조각이 공유할 4x4 흰색 정사각형 스프라이트를 코드로 생성 (별도 이미지 에셋 불필요)
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
