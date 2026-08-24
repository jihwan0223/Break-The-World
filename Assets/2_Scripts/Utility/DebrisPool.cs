using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 오브젝트를 부술 때마다 지정된 영역(콜라이더) 안 랜덤한 위치로 조각이 떨어져 쌓이는 시스템.
// 최대 개수에 도달한 상태에서 새 조각이 떨어져 착지하면, 가장 먼저 쌓였던 조각이 자연스럽게 페이드아웃되며 사라짐.
// 조각들은 재생성과 무관하게 계속 유지되고, 선택된 오브젝트가 바뀔 때만 전부 리셋됨.
public class DebrisPool : MonoBehaviour
{
    // 씬 어디서든 DebrisPool.Instance로 접근하기 위한 싱글톤
    public static DebrisPool Instance { get; private set; }

    [SerializeField] private Collider2D spawnArea; // 조각이 생성될 영역을 정해주는 콜라이더 (이 안에서 랜덤한 위치에 생성됨)
    [SerializeField] private int maxPieces = 50; // 최대 조각 개수
    [SerializeField] private float pieceSize = 1f; // 조각 하나의 크기 (월드 유닛)
    [SerializeField] private Sprite[] pieceSprites; // 조각으로 쓸 스프라이트들 (Object-Break.png의 서브 스프라이트들을 넣으면 조각마다 랜덤한 모양이 나옴)
    [SerializeField] private float fallDuration = 0.4f; // 부서진 지점에서 최종 자리까지 떨어지는 데 걸리는 시간(초)
    [SerializeField] private float fadeOutDuration = 0.5f; // 가장 오래된 조각이 사라질 때 페이드아웃되는 시간(초)

    private readonly List<SpriteRenderer> _pilePieces = new List<SpriteRenderer>(); // 지금 쌓여있는 조각들 (0번이 가장 오래됨)
    private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>(); // 재사용 가능한(비활성) 조각 오브젝트들
    private Color _currentColor = Color.white; // 지금 쌓이고 있는 조각들의 색상 (오브젝트가 바뀌면 갱신됨)
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
    }

    void OnDestroy()
    {
        if (ObjectManager.Instance != null)
            ObjectManager.Instance.OnObjectChanged -= HandleObjectChanged;
    }

    // 오브젝트를 부술 때마다 호출 - 최대 개수의 2/10(20%)만큼 조각이 부서진 지점(fromPosition)에서 떨어짐
    public void AddPiece(Vector3 fromPosition)
    {
        int amount = Mathf.Max(1, maxPieces * 2 / 10); // 한 번에 떨어질 조각 개수

        for (int i = 0; i < amount; i++)
        {
            SpriteRenderer piece = GetPieceFromPool();
            piece.sprite = GetRandomPieceSprite();

            Color color = _currentColor;
            piece.color = color; // 알파 포함 원래 색으로 초기화 (재사용된 조각이 이전에 페이드아웃됐을 수 있으므로)

            Vector3 landingPoint = GetRandomPointInSpawnArea();
            StartCoroutine(FallThenSettle(piece, fromPosition, landingPoint));
        }
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
        if (_pool.Count > 0)
            return _pool.Pop();

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

    // spawnArea 콜라이더 안쪽의 랜덤한 한 점을 반환 (사각 범위 안에서 뽑고, 콜라이더 모양 밖이면 다시 뽑기)
    private Vector3 GetRandomPointInSpawnArea()
    {
        Bounds bounds = spawnArea.bounds;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float y = Random.Range(bounds.min.y, bounds.max.y);
            Vector2 point = new Vector2(x, y);

            if (spawnArea.OverlapPoint(point))
                return point;
        }

        // 10번 시도해도 콜라이더 안쪽을 못 찾으면 안전하게 중앙에 생성
        return bounds.center;
    }

    private void HandleObjectChanged(ObjectData newObject)
    {
        ResetPieces(newObject.pileColor);
    }

    // 쌓인 조각을 전부 지우고, 새 색으로 다시 0개부터 쌓기 시작
    private void ResetPieces(Color newColor)
    {
        StopAllCoroutines(); // 떨어지는 중이거나 페이드아웃 중인 조각이 있다면 그 연출도 같이 중단

        foreach (SpriteRenderer piece in _pilePieces)
        {
            piece.gameObject.SetActive(false);
            _pool.Push(piece);
        }

        _pilePieces.Clear();
        _currentColor = newColor;
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
