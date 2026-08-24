using UnityEngine;

// 오브젝트를 부술 때마다 지정된 영역(콜라이더) 안 랜덤한 위치에 조각을 하나씩 쌓는 시스템.
// 조각들은 재생성과 무관하게 계속 유지되고, 선택된 오브젝트가 바뀔 때만 전부 리셋됨.
public class DebrisPool : MonoBehaviour
{
    // 씬 어디서든 DebrisPool.Instance로 접근하기 위한 싱글톤
    public static DebrisPool Instance { get; private set; }

    [SerializeField] private Collider2D spawnArea; // 조각이 생성될 영역을 정해주는 콜라이더 (이 안에서 랜덤한 위치에 생성됨)
    [SerializeField] private int maxPieces = 20; // 최대 조각 개수
    [SerializeField] private float pieceSize = 1f; // 조각 하나의 크기 (월드 유닛)
    [SerializeField] private Sprite[] pieceSprites; // 조각으로 쓸 스프라이트들 (Object-Break.png의 서브 스프라이트 20개를 넣으면 조각마다 랜덤한 모양이 나옴)

    private SpriteRenderer[] _pieces; // 미리 만들어둔 조각 오브젝트들 (개수 = maxPieces)
    private int _filledCount; // 지금까지 채워진(활성화된) 조각 개수
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
        _pieces = new SpriteRenderer[maxPieces];

        for (int i = 0; i < maxPieces; i++)
        {
            var pieceObject = new GameObject("Piece");
            pieceObject.transform.SetParent(transform);
            pieceObject.transform.localScale = Vector3.one * pieceSize;

            var spriteRenderer = pieceObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 10; // 파괴 대상 오브젝트들보다 위에 그려지도록

            pieceObject.SetActive(false);
            _pieces[i] = spriteRenderer;
        }
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

    // 오브젝트를 부술 때마다 호출 - 최대 개수의 2/10(20%)만큼 조각을 한 번에 채움 (최대 개수 도달하면 더 이상 안 늘어남)
    public void AddPiece()
    {
        int amount = Mathf.Max(1, maxPieces * 2 / 10); // 한 번에 튀어나올 조각 개수

        for (int i = 0; i < amount && _filledCount < _pieces.Length; i++)
        {
            _pieces[_filledCount].transform.position = GetRandomPointInSpawnArea();
            _pieces[_filledCount].sprite = GetRandomPieceSprite();
            _pieces[_filledCount].color = _currentColor;
            _pieces[_filledCount].gameObject.SetActive(true);
            _filledCount++;
        }
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
        for (int i = 0; i < _filledCount; i++)
            _pieces[i].gameObject.SetActive(false);

        _filledCount = 0;
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
