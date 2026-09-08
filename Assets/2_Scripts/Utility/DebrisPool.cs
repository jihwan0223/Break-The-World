using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 오브젝트를 부술 때마다, 그때 획득한 조각 수만큼 조각이 부서진 지점에서 떨어져 바닥(spawnArea) 안 랜덤한 위치로 쌓이는 시스템.
// 최대 개수에 도달한 상태에서 새 조각이 착지하면, 가장 먼저 쌓였던 조각이 자연스럽게 페이드아웃되며 사라짐.
// 업그레이드/무기/오브젝트 탭이 열리면 바닥에 쌓인 조각을 전부 즉시 치움. 선택된 오브젝트가 바뀔 때도 전부 리셋됨.
public class DebrisPool : MonoBehaviour
{
    // 씬 어디서든 DebrisPool.Instance로 접근하기 위한 싱글톤
    public static DebrisPool Instance { get; private set; }

    [SerializeField] private Collider2D spawnArea; // 조각이 떨어질 바닥 범위 콜라이더 (이 안 랜덤한 위치로 떨어짐)
    [SerializeField] private int maxPieces = 2000; // 바닥에 동시에 쌓일 수 있는 최대 조각 개수
    [SerializeField] private int maxPiecesPerBreak = 300; // 한 번 부술 때 떨어뜨릴 조각 수 상한 (더 많이 획득해도 연출만 이 수로 제한, 조각 지급은 정상)
    [SerializeField] private float pieceSize = 1f; // 조각 하나의 크기 (월드 유닛)
    [SerializeField] private Sprite[] pieceSprites; // 조각으로 쓸 스프라이트들 (Object-Break.png의 서브 스프라이트들). 색은 부순 오브젝트의 pileColor로 입힘
    [SerializeField] private float initialSpread = 1f; // 처음엔 오브젝트 바로 아래 이 반경(월드 유닛) 안으로만 떨어지고, 쌓일수록 spawnArea 폭까지 점점 넓어짐
    [SerializeField] private float fallDuration = 0.4f; // 부서진 지점에서 바닥까지 떨어지는 데 걸리는 시간(초)
    [SerializeField] private float fadeOutDuration = 0.5f; // 가장 오래된 조각이 사라질 때 페이드아웃되는 시간(초)

    private readonly List<SpriteRenderer> _pilePieces = new List<SpriteRenderer>(); // 지금 쌓여있는 조각들 (0번이 가장 오래됨)
    private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>(); // 재사용 가능한(비활성) 조각 오브젝트들
    private Color _currentColor = Color.white; // ObjectManager를 못 찾을 때 폴백으로 쓸 색
    private Sprite _fallbackSprite; // pieceSprites가 비어있을 때 대신 쓸 흰색 정사각형 스프라이트

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
        // ObjectManager가 이미 씬에 있으면 현재 선택된 오브젝트 색으로 시작
        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.OnObjectChanged += HandleObjectChanged;
            _currentColor = ObjectManager.Instance.CurrentObject.pileColor;
        }

        // 어떤 탭(업그레이드 화면 / 무기·오브젝트 팝업)이든 열리면 바닥 조각을 전부 치움
        UpgradeTreeUI.OnTreeToggled += HandleTabToggled;
        SidePanelUI.OnSelectorPanelToggled += HandleTabToggled;

        // 첫 몇 번 부술 때 조각 GameObject를 한꺼번에 만드느라 프레임이 튀는 걸 막으려고 풀을 미리 채워둠
        for (int i = 0; i < maxPiecesPerBreak; i++)
            _pool.Push(CreatePiece());
    }

    void OnDestroy()
    {
        if (ObjectManager.Instance != null)
            ObjectManager.Instance.OnObjectChanged -= HandleObjectChanged;

        UpgradeTreeUI.OnTreeToggled -= HandleTabToggled;
        SidePanelUI.OnSelectorPanelToggled -= HandleTabToggled;
    }

    // 오브젝트를 부술 때 호출 - objectIndex 오브젝트의 색으로, 획득한 조각 수(count)만큼 조각이 fromPosition에서 떨어짐
    public void AddPiece(Vector3 fromPosition, int objectIndex, int count)
    {
        int amount = Mathf.Clamp(count, 1, maxPiecesPerBreak); // 연출용 상한 (조각 지급 자체는 Click에서 이미 끝난 상태)
        Color color = PileColorFor(objectIndex);

        for (int i = 0; i < amount; i++)
        {
            SpriteRenderer piece = GetPieceFromPool();
            piece.sprite = GetRandomPieceSprite();
            piece.color = color; // 알파 포함 원래 색으로 초기화 (재사용된 조각이 이전에 페이드아웃됐을 수 있으므로)

            Vector3 landingPoint = GetLandingPoint(fromPosition);
            StartCoroutine(FallThenSettle(piece, fromPosition, landingPoint));
        }
    }

    // objectIndex 오브젝트의 pileColor - 인덱스가 이상하면 마지막으로 알던 색으로 폴백
    private Color PileColorFor(int objectIndex)
    {
        if (ObjectManager.Instance != null && objectIndex >= 0 && objectIndex < ObjectManager.StaticObjectCount)
            return ObjectManager.Instance.GetObjectAt(objectIndex).pileColor;

        return _currentColor;
    }

    // fromPosition에서 landingPoint까지 중력처럼 가속하며 떨어진 뒤, 쌓여있는 조각 더미에 합류시킴
    private IEnumerator FallThenSettle(SpriteRenderer piece, Vector3 fromPosition, Vector3 landingPoint)
    {
        piece.transform.position = fromPosition;
        piece.gameObject.SetActive(true);

        float elapsed = 0f; // 코루틴 시작 후 흐른 시간

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fallDuration);
            float easedProgress = progress * progress; // 중력처럼 갈수록 빨라지는 가속 느낌 (ease-in)

            piece.transform.position = Vector3.Lerp(fromPosition, landingPoint, easedProgress);
            yield return null;
        }

        piece.transform.position = landingPoint;

        // 착지 완료 - 더미가 꽉 차있다면 가장 오래된 조각을 자연스럽게 페이드아웃시켜 자리를 비움
        if (_pilePieces.Count >= maxPieces)
        {
            SpriteRenderer oldest = _pilePieces[0];
            _pilePieces.RemoveAt(0);
            StartCoroutine(FadeOutAndReturnToPool(oldest));
        }

        _pilePieces.Add(piece);
    }

    // 조각의 알파값을 서서히 0으로 낮춘 뒤 비활성화하고 풀로 반납
    private IEnumerator FadeOutAndReturnToPool(SpriteRenderer piece)
    {
        Color startColor = piece.color;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeOutDuration);

            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, progress);
            piece.color = color;

            yield return null;
        }

        piece.gameObject.SetActive(false);
        _pool.Push(piece);
    }

    // 재사용 가능한 조각을 풀에서 꺼내거나, 없으면 새로 만듦
    private SpriteRenderer GetPieceFromPool()
    {
        return _pool.Count > 0 ? _pool.Pop() : CreatePiece();
    }

    // 비활성 상태의 새 조각 GameObject 하나를 만듦 (풀 채우기 / 부족할 때 공용)
    private SpriteRenderer CreatePiece()
    {
        var pieceObject = new GameObject("Piece");
        pieceObject.transform.SetParent(transform);
        pieceObject.transform.localScale = Vector3.one * pieceSize;

        var spriteRenderer = pieceObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 10; // 파괴 대상 오브젝트들보다 위에 그려지도록

        pieceObject.SetActive(false);
        return spriteRenderer;
    }

    // pieceSprites 중 하나를 랜덤으로 골라줌 (비어있으면 흰색 정사각형으로 대체)
    private Sprite GetRandomPieceSprite()
    {
        if (pieceSprites == null || pieceSprites.Length == 0)
            return _fallbackSprite;

        return pieceSprites[Random.Range(0, pieceSprites.Length)];
    }

    // 조각이 떨어질 지점 - 부순 오브젝트(fromPosition) 바로 아래에서 시작해서, 더미가 쌓일수록 좌우로 넓게 퍼짐.
    // x는 오브젝트 중심에서 뽑고, 폭은 initialSpread에서 spawnArea 절반 폭까지 채움 비율에 따라 늘어남. y는 spawnArea 세로 범위 안 랜덤.
    private Vector3 GetLandingPoint(Vector3 fromPosition)
    {
        Bounds bounds = spawnArea.bounds;

        float fill = maxPieces > 0 ? (float)_pilePieces.Count / maxPieces : 1f; // 0(빔) ~ 1(꽉 참)
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

    // 업그레이드/무기/오브젝트 탭이 열리면(open=true) 바닥 조각을 전부 즉시 치움
    private void HandleTabToggled(bool open)
    {
        if (open) ReturnAllPieces();
    }

    private void HandleObjectChanged(ObjectData newObject)
    {
        ReturnAllPieces();
        _currentColor = newObject.pileColor;
    }

    // 쌓여있거나, 떨어지는 중이거나, 페이드아웃 중인 조각을 전부 즉시 풀로 되돌림
    private void ReturnAllPieces()
    {
        StopAllCoroutines(); // 낙하/페이드 연출 중단

        _pilePieces.Clear();

        // _pilePieces에 아직 안 들어간(낙하 중) 조각까지 잡으려고 자식 전체를 훑음
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf) continue; // 이미 풀에 있는 비활성 조각은 건너뜀

            child.gameObject.SetActive(false);
            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (sr != null) _pool.Push(sr);
        }
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
