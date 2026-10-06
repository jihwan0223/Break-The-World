using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 오브젝트를 부수면 얻은 조각만큼 파편이 부서진 지점에서 떨어져 바닥(spawnArea)에 쌓이고, 파편을 클릭해서 주워야 조각(돈)이 들어오는 시스템.
// 파편 하나가 여러 조각 값을 들고 있을 수 있음 (한 번에 떨어뜨리는 개수에 상한이 있어서 총액을 나눠 담음).
// 파편은 존별로 따로 쌓이고, 지금 보는 존의 파편만 보이고 주울 수 있음. 주울 때까지 사라지지 않음
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
    [SerializeField] private float initialSpread = 1f; // 처음엔 오브젝트 바로 아래 이 반경(월드 유닛) 안으로만 떨어지고, 쌓일수록 spawnArea 폭까지 점점 넓어짐
    [SerializeField] private float fallDuration = 0.4f; // 부서진 지점에서 바닥까지 떨어지는 데 걸리는 시간(초)
    [SerializeField] private Color crystalColor = new Color(0.45f, 0.9f, 1f); // 결정 색 (전용 이미지가 생기기 전까지 파편 그림에 이 색을 입혀 구분)
    [SerializeField] private float crystalSizeMultiplier = 1.6f; // 결정을 파편보다 이 배율만큼 크게
    [SerializeField] private float hoverPickupRadiusScale = 0.7f; // 호버 줍기 판정 크기 - 파편 그림 대비 배율 (작을수록 커서가 파편 한가운데에 정확히 올라가야 주워짐)

    // 바닥에 떨어져 있는(또는 떨어지는 중인) 파편 하나
    private class Piece
    {
        public SpriteRenderer renderer; // 파편 그림
        public CircleCollider2D collider; // 줍기 판정 (착지 후에만 켜짐)
        public long value; // 주웠을 때 들어오는 조각(결정이면 결정) 개수
        public bool isCrystal; // 결정인지 (결정은 바닥 최대 개수에 안 들어감)
        public int zone; // 떨어진 존 번호 (그 존을 보고 있을 때만 보이고 주울 수 있음)
        public bool landed; // 착지를 마쳤는지
        public bool expiring; // 바닥 최대 개수를 넘어서 새 파편에 덮여 교체될 예정인지 (최대 개수 계산에서 빠짐)
        public int generation; // 풀에서 꺼낼 때마다 1씩 늘어남 - 교체 대기 중에 주워져서 다른 파편으로 재사용됐는지 구분용
        public Piece replaces; // 이 파편이 착지하면서 덮어 없앨 오래된 파편 (없으면 null)
        public bool sweepMarked; // 이번 자동 줍기가 쓸어 담을 대상인지 (자동 줍기가 출발할 때 위층부터 정해짐)
        public int replacesGeneration; // 그 오래된 파편의 generation - 떨어지는 사이 주워졌는지 확인용
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
        foreach (Collider2D hit in Physics2D.OverlapPointAll(worldPos))
        {
            if (!_pieceByCollider.TryGetValue(hit, out Piece piece) || !piece.collider.enabled) continue;

            Transform pieceTransform = piece.renderer.transform; // 이 파편의 위치/크기
            float radius = piece.collider.radius / pickupColliderScale * hoverPickupRadiusScale * pieceTransform.lossyScale.x; // 호버 판정 반경(월드)
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
    }

    // 오브젝트를 부쉈을 때 호출 - reward 조각을 파편 여러 개(최대 maxPiecesPerBreak개)에 나눠 담아 fromPosition에서 떨어뜨림.
    // 이 존 바닥이 꽉 차 있어도 파편은 계속 떨어지고, 넘친 만큼은 가장 먼저 떨어진 파편 자리로 떨어져 착지하는 순간
    // 그 파편을 덮어 교체하면서 값을 이어받음 - 사라지는 모습이 안 보이고, 줍기 전에 교체된 값도 남아있어서 놓치는 게 없음
    public void DropPieces(Vector3 fromPosition, int objectIndex, long reward)
    {
        if (reward <= 0) return;

        int zone = CurrentZone; // 떨어뜨릴 존
        int count = (int)System.Math.Min(reward, maxPiecesPerBreak); // 나눠 담을 파편 수
        long baseValue = reward / count; // 파편 하나당 기본 값
        long remainder = reward % count; // 나누고 남은 조각 - 앞쪽 파편들에 1개씩 더 얹음
        Color color = PileColorFor(objectIndex);
        int excess = CountInZone(zone) + count - MaxGroundPieces; // 이번 파편들로 바닥 최대 개수를 넘는 수 = 교체할 오래된 파편 수

        var dropped = new List<Transform>(count); // 이번에 떨어뜨린 파편들
        for (int i = 0; i < count; i++)
        {
            Piece oldest = i < excess ? OldestLandedPieceInZone(zone) : null; // 이 파편이 덮어 교체할 오래된 파편
            if (oldest != null) oldest.expiring = true;
            dropped.Add(Spawn(fromPosition, zone, baseValue + (i < remainder ? 1 : 0), false, color, pieceSize, oldest));
        }

        OnPiecesDropped?.Invoke(dropped);
    }

    // 이 존에서 가장 먼저 떨어진, 착지했고 아직 교체될 예정이 아닌 파편 (결정 제외). 없으면 null
    private Piece OldestLandedPieceInZone(int zone)
    {
        foreach (Piece piece in _groundPieces) // 떨어진 순서대로 들어있음
            if (piece.zone == zone && piece.landed && !piece.isCrystal && !piece.expiring) return piece;
        return null;
    }

    // 결정 하나를 떨어뜨림 (amount개짜리 한 덩어리). 바닥이 꽉 차 있어도 떨어짐
    public void DropCrystal(Vector3 fromPosition, long amount)
    {
        if (amount <= 0) return;
        Spawn(fromPosition, CurrentZone, amount, true, crystalColor, pieceSize * crystalSizeMultiplier);
    }

    // replaces가 있으면 그 파편 자리로 떨어져서 착지할 때 덮어 교체함
    private Transform Spawn(Vector3 fromPosition, int zone, long value, bool isCrystal, Color color, float size, Piece replaces = null)
    {
        Piece piece = _pool.Count > 0 ? _pool.Pop() : CreatePiece();
        piece.value = value;
        piece.isCrystal = isCrystal;
        piece.zone = zone;
        piece.landed = false;
        piece.expiring = false;
        piece.sweepMarked = false;
        piece.generation++;
        piece.replaces = replaces;
        piece.replacesGeneration = replaces != null ? replaces.generation : 0;
        piece.renderer.sprite = GetRandomPieceSprite();
        piece.renderer.color = color;
        piece.renderer.sortingOrder = isCrystal ? 11 : 10; // 결정이 파편 더미에 묻히지 않게 위에 그림 (클릭도 우선)
        piece.renderer.forceRenderingOff = false;
        piece.renderer.transform.localScale = Vector3.one * size;
        piece.collider.enabled = false;

        // 스프라이트가 파편마다 달라서 콜라이더 반경을 매번 그림 크기에 맞춤 (로컬 기준이라 스케일은 자동 반영)
        Vector3 extents = piece.renderer.sprite.bounds.extents;
        piece.collider.radius = Mathf.Max(extents.x, extents.y) * pickupColliderScale;

        _groundPieces.Add(piece);
        Vector3 landingPoint = replaces != null ? replaces.renderer.transform.position : GetLandingPoint(fromPosition, zone); // 교체할 파편이 있으면 정확히 그 위로
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
                Collect(piece, true);
        }
    }

    // bySweeper면 빗자루가 주운 것 - "+N"을 근처 것끼리 합쳐서 띄움 (안 보이는 존에서 주운 건 안 띄움)
    private void Collect(Piece piece, bool bySweeper)
    {
        if (piece.zone == CurrentZone)
            _popup.Show(piece.renderer.transform.position, piece.value, piece.isCrystal, bySweeper);
        if (piece.isCrystal)
            CurrencyManager.Instance?.AddCrystals(piece.value);
        else
            CurrencyManager.Instance?.AddPieces(piece.value);
        OnPieceCollected?.Invoke(piece.renderer.transform);
        ReturnToPool(piece);
    }

    // 테스트용 - 모든 존의 바닥 파편을 조각 지급 없이 전부 치움
    public void ClearAll()
    {
        StopAllCoroutines();
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
            if (piece.zone == zone && !piece.isCrystal && !piece.expiring) count++;
        return count;
    }

    // objectIndex 오브젝트의 pileColor - 인덱스가 이상하면 마지막으로 알던 색으로 폴백
    private Color PileColorFor(int objectIndex)
    {
        if (ObjectManager.Instance != null && objectIndex >= 0 && objectIndex < ObjectManager.StaticObjectCount)
            return ObjectManager.Instance.GetObjectAt(objectIndex).pileColor;

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

        // 덮어 교체할 파편이 아직 그대로 있으면(떨어지는 사이 안 주워졌으면) 값을 이어받고 없앰
        Piece replaced = piece.replaces; // 이 파편 밑에 깔린 오래된 파편
        piece.replaces = null;
        if (replaced != null && replaced.generation == piece.replacesGeneration && _groundPieces.Contains(replaced))
        {
            piece.value += replaced.value;
            ReturnToPool(replaced);
        }
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

    // 조각이 떨어질 지점 - 부순 오브젝트(fromPosition) 바로 아래에서 시작해서, 그 존 바닥이 찰수록 좌우로 넓게 퍼짐.
    // x는 오브젝트 중심에서 뽑고, 폭은 initialSpread에서 spawnArea 절반 폭까지 채움 비율에 따라 늘어남. y는 spawnArea 세로 범위 안 랜덤.
    private Vector3 GetLandingPoint(Vector3 fromPosition, int zone)
    {
        Bounds bounds = spawnArea.bounds;

        float fill = (float)CountInZone(zone) / Mathf.Max(1, MaxGroundPieces); // 0(빔) ~ 1(꽉 참)
        float halfWidth = Mathf.Lerp(initialSpread, bounds.extents.x, Mathf.Clamp01(fill)); // 쌓일수록 넓게

        for (int attempt = 0; attempt < 10; attempt++)
        {
            float x = Mathf.Clamp(fromPosition.x + Random.Range(-halfWidth, halfWidth), bounds.min.x, bounds.max.x);
            float y = Random.Range(bounds.min.y, bounds.max.y);
            Vector2 point = new Vector2(x, y);

            if (spawnArea.OverlapPoint(point))
                return point;
        }

        // 10번 시도해도 콜라이더 안쪽을 못 찾으면 오브젝트 바로 아래(바닥 높이)로
        return new Vector3(Mathf.Clamp(fromPosition.x, bounds.min.x, bounds.max.x), bounds.min.y, 0f);
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
