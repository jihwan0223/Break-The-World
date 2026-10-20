using UnityEngine;

// 컨베이어 벨트가 도는 연출. 벨트 그림 위에 같은 그림을 겹쳐 그리고, 그 그림을 계속 옆으로 밀어서 벨트 윗면이 흘러가는 것처럼 보이게 함.
// 원래 벨트 그림(콜라이더/체력 단계용)은 그대로 두고 겹친 그림만 바꿈
[RequireComponent(typeof(SpriteRenderer))]
public class ConveyorBelt : MonoBehaviour
{
    [SerializeField] private float speed = 1.2f; // 벨트 윗면이 왼쪽으로 흐르는 속도(월드 단위/초) - 위에 탄 왁뿌볼(ConveyorRider) 속도와 맞춤
    [SerializeField] private int stepPixels = 4; // 그림 한 장마다 밀리는 픽셀 수 (작을수록 부드럽지만 그림 수가 늘어남)

    private SpriteRenderer _base; // 원래 벨트 그림
    private SpriteRenderer _scroll; // 그 위에 겹쳐서 흐르는 벨트 그림 (자식)
    private Sprite _source; // 흐르는 그림을 만든 원본 벨트 그림
    private Texture2D _texture; // 원본을 가로로 두 번 이어붙인 그림 - 잘라 보는 위치를 옮기면 끝없이 흐름
    private Sprite[] _frames; // 밀린 위치별 그림 (처음 쓸 때 만듦)
    private float _offset; // 지금까지 밀린 거리(픽셀)

    void Start()
    {
        _base = GetComponent<SpriteRenderer>();
        var child = new GameObject("BeltScroll"); // 흐르는 그림을 그릴 자식
        child.transform.SetParent(transform, false);
        child.transform.localPosition = new Vector3(0f, 0f, 0.01f); // 정렬 순서가 같은 오브젝트 금(WaxCoating)보다 살짝 뒤 - 금이 벨트 위에 보이게
        _scroll = child.AddComponent<SpriteRenderer>();
        _scroll.sharedMaterial = _base.sharedMaterial;
    }

    void OnDestroy()
    {
        if (_texture != null) Destroy(_texture);
        if (_frames != null) foreach (Sprite frame in _frames) if (frame != null) Destroy(frame);
    }

    void LateUpdate()
    {
        if (_base.sprite != _source) Build(_base.sprite);
        bool show = _frames != null && _base.enabled; // 원래 그림이 보일 때만 (해금 전/부서져 사라졌을 때는 같이 숨김)
        _scroll.enabled = show;
        if (!show) return;

        // 원래 그림을 그대로 따라감: 바로 위에 그리고, 투명도(죽을 때 흐려짐)와 존 숨김도 같이
        _scroll.sortingLayerID = _base.sortingLayerID;
        _scroll.sortingOrder = _base.sortingOrder + 1;
        _scroll.forceRenderingOff = _base.forceRenderingOff;
        _scroll.flipX = _base.flipX;
        _scroll.flipY = _base.flipY;
        _scroll.color = _base.color;

        float pixelsPerWorld = _source.pixelsPerUnit / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x)); // 월드 1칸이 그림 몇 픽셀인지
        float width = _source.rect.width; // 원본 그림 너비(픽셀)
        _offset = Mathf.Repeat(_offset + speed * pixelsPerWorld * Time.deltaTime, width);
        int frame = Mathf.Min(_frames.Length - 1, Mathf.FloorToInt(_offset / stepPixels)); // 지금 보여줄 그림 번호
        if (_frames[frame] == null)
        {
            Vector2 pivot = new Vector2(_source.pivot.x / width, _source.pivot.y / _source.rect.height); // 원본과 같은 기준점 (0~1)
            _frames[frame] = Sprite.Create(_texture, new Rect(frame * stepPixels, 0, width, _source.rect.height), pivot, _source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
        _scroll.sprite = _frames[frame];
    }

    // 원본 그림을 읽어서 가로로 두 번 이어붙인 그림을 만듦 (읽기 허용이 꺼진 그림도 읽을 수 있게 렌더 텍스처를 거침)
    private void Build(Sprite sprite)
    {
        _source = sprite;
        if (_frames != null) foreach (Sprite frame in _frames) if (frame != null) Destroy(frame);
        if (_texture != null) Destroy(_texture);
        _frames = null;
        _texture = null;
        if (sprite == null) return;

        Rect rect = sprite.rect; // 원본 그림 범위(픽셀)
        int w = Mathf.RoundToInt(rect.width), h = Mathf.RoundToInt(rect.height); // 원본 크기
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        Texture source = sprite.texture; // 원본 텍스처
        Graphics.Blit(source, rt, new Vector2(rect.width / source.width, rect.height / source.height), new Vector2(rect.x / source.width, rect.y / source.height));
        RenderTexture previous = RenderTexture.active; // 원래 렌더 대상 (되돌리기용)
        RenderTexture.active = rt;
        var read = new Texture2D(w, h, TextureFormat.RGBA32, false); // 읽은 원본
        read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        read.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] pixels = read.GetPixels32(); // 원본 픽셀
        Destroy(read);
        var doubled = new Color32[w * 2 * h]; // 두 번 이어붙인 픽셀
        for (int y = 0; y < h; y++)
        {
            System.Array.Copy(pixels, y * w, doubled, y * w * 2, w);
            System.Array.Copy(pixels, y * w, doubled, y * w * 2 + w, w);
        }
        _texture = new Texture2D(w * 2, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        _texture.SetPixels32(doubled);
        _texture.Apply();
        _frames = new Sprite[Mathf.Max(1, Mathf.CeilToInt(rect.width / Mathf.Max(1, stepPixels)))];
    }
}
