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

    [SerializeField] private Collider2D spawnArea; // 조각이 떨어질 바닥 범위 콜라이더 (이 안 랜덤한 위치로 떨어짐)
    [SerializeField] private int maxPiecesPerBreak = 50; // 한 번 부술 때 떨어뜨릴 파편 수의 상한 (렉 방지 - 넘치는 조각은 파편 하나에 합쳐 담음)
    [SerializeField] private float pieceSize = 1f; // 조각 하나의 크기 (월드 유닛)
    [SerializeField] private float pickupColliderScale = 1.4f; // 줍기 판정 콜라이더를 파편 그림보다 이 배율만큼 크게 (잘 주워지게)
    [SerializeField] private Sprite[] pieceSprites; // 조각으로 쓸 스프라이트들 (Object-Break.png의 서브 스프라이트들). 색은 부순 오브젝트의 pileColor로 입힘
    [SerializeField] private float initialSpread = 1f; // 처음엔 오브젝트 바로 아래 이 반경(월드 유닛) 안으로만 떨어지고, 쌓일수록 spawnArea 폭까지 점점 넓어짐
    [SerializeField] private float fallDuration = 0.4f; // 부서진 지점에서 바닥까지 떨어지는 데 걸리는 시간(초)
    [SerializeField] private Color crystalColor = new Color(0.45f, 0.9f, 1f); // 결정 색 (전용 이미지가 생기기 전까지 파편 그림에 이 색을 입혀 구분)
    [SerializeField] private float crystalSizeMultiplier = 1.6f; // 결정을 파편보다 이 배율만큼 크게

    // 바닥에 떨어져 있는(또는 떨어지는 중인) 파편 하나
    private class Piece
    {
        public SpriteRenderer renderer; // 파편 그림
        public CircleCollider2D collider; // 줍기 판정 (착지 후에만 켜짐)
        public long value; // 주웠을 때 들어오는 조각(결정이면 결정) 개수
        public bool isCrystal; // 결정인지 (결정은 바닥 최대 개수에 안 들어감)
        public int zone; // 떨어진 존 번호 (그 존을 보고 있을 때만 보이고 주울 수 있음)
        public bool landed; // 착지를 마쳤는지
    }

    private readonly List<Piece> _groundPieces = new List<Piece>(); // 지금 바닥에 있거나 떨어지는 중인 파편들
    private readonly Dictionary<Collider2D, Piece> _pieceByCollider = new Dictionary<Collider2D, Piece>(); // 클릭된 콜라이더로 파편을 찾기 위한 표
    private readonly Stack<Piece> _pool = new Stack<Piece>(); // 재사용 가능한(비활성) 파편들
    private Color _currentColor = Color.white; // ObjectManager를 못 찾을 때 폴백으로 쓸 색
    private Sprite _fallbackSprite; // pieceSprites가 비어있을 때 대신 쓸 흰색 정사각형 스프라이트

    private int CurrentZone => ZoneManager.Instance != null ? ZoneManager.Instance.CurrentZone : 0; // 지금 보고 있는 존
    private int MaxGroundPieces => UpgradeManager.Instance != null ? UpgradeManager.Instance.MaxGroundPieces : 10; // 한 존 바닥에 동시에 있을 수 있는 파편 수

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
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (UIPointerGuard.IsPointerOverUI) return;

        Vector2 worldPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        // 파편은 오브젝트보다 앞에 그려져서 Click.TopmostAt도 파편을 맨 위로 봄 - 파편을 누르면 뒤 오브젝트는 안 맞음
        foreach (Collider2D hit in Physics2D.OverlapPointAll(worldPos))
        {
            if (_pieceByCollider.TryGetValue(hit, out Piece piece) && piece.collider.enabled)
            {
                Collect(piece);
                return; // 한 번 클릭에 하나만 주움
            }
        }
    }

    // 오브젝트를 부쉈을 때 호출 - reward 조각을 파편 여러 개에 나눠 담아 fromPosition에서 떨어뜨림.
    // 이 존 바닥이 이미 꽉 찼으면 아무것도 안 떨어짐(조각도 못 얻음)
    public void DropPieces(Vector3 fromPosition, int objectIndex, long reward)
    {
        int zone = CurrentZone; // 떨어뜨릴 존
        int freeSlots = MaxGroundPieces - CountInZone(zone); // 이 존 바닥에 남은 자리
        if (freeSlots <= 0 || reward <= 0) return;

        int count = (int)System.Math.Min(reward, System.Math.Min(maxPiecesPerBreak, freeSlots)); // 실제로 떨어뜨릴 파편 수
        long baseValue = reward / count; // 파편 하나당 기본 값
        long remainder = reward % count; // 나누고 남은 조각 - 앞쪽 파편들에 1개씩 더 얹음
        Color color = PileColorFor(objectIndex);

        for (int i = 0; i < count; i++)
            Spawn(fromPosition, zone, baseValue + (i < remainder ? 1 : 0), false, color, pieceSize);
    }

    // 결정 하나를 떨어뜨림 (amount개짜리 한 덩어리). 바닥이 꽉 차 있어도 떨어짐
    public void DropCrystal(Vector3 fromPosition, long amount)
    {
        if (amount <= 0) return;
        Spawn(fromPosition, CurrentZone, amount, true, crystalColor, pieceSize * crystalSizeMultiplier);
    }

    private void Spawn(Vector3 fromPosition, int zone, long value, bool isCrystal, Color color, float size)
    {
        Piece piece = _pool.Count > 0 ? _pool.Pop() : CreatePiece();
        piece.value = value;
        piece.isCrystal = isCrystal;
        piece.zone = zone;
        piece.landed = false;
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
        StartCoroutine(FallThenSettle(piece, fromPosition, GetLandingPoint(fromPosition, zone)));
    }

    private void Collect(Piece piece)
    {
        if (piece.isCrystal)
            CurrencyManager.Instance?.AddCrystals(piece.value);
        else
            CurrencyManager.Instance?.AddPieces(piece.value);
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
            if (piece.zone == zone && !piece.isCrystal) count++;
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
