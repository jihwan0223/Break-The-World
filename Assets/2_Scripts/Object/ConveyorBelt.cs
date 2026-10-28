using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ConveyorBelt : MonoBehaviour
{
    [SerializeField] private float speed = 1.2f; // 월드 유닛/초 - ConveyorRider 속도와 맞춤
    [SerializeField] private int stepPixels = 4;

    private SpriteRenderer _base;
    private SpriteRenderer _scroll;
    private Sprite _source;
    private Texture2D _texture; // 원본을 가로로 두 번 이어붙임 - 보는 위치를 옮기면 끝없이 흐름
    private Sprite[] _frames;
    private float _offset;

    void Start()
    {
        _base = GetComponent<SpriteRenderer>();
        var child = new GameObject("BeltScroll");
        child.transform.SetParent(transform, false);
        child.transform.localPosition = new Vector3(0f, 0f, 0.01f); // WaxCoating 금보다 살짝 뒤
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
        bool show = _frames != null && _base.enabled;
        _scroll.enabled = show;
        if (!show) return;

        _scroll.sortingLayerID = _base.sortingLayerID;
        _scroll.sortingOrder = _base.sortingOrder + 1;
        _scroll.forceRenderingOff = _base.forceRenderingOff;
        _scroll.flipX = _base.flipX;
        _scroll.flipY = _base.flipY;
        _scroll.color = _base.color;

        float pixelsPerWorld = _source.pixelsPerUnit / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float width = _source.rect.width;
        _offset = Mathf.Repeat(_offset + speed * pixelsPerWorld * Time.deltaTime, width);
        int frame = Mathf.Min(_frames.Length - 1, Mathf.FloorToInt(_offset / stepPixels));
        if (_frames[frame] == null)
        {
            Vector2 pivot = new Vector2(_source.pivot.x / width, _source.pivot.y / _source.rect.height);
            _frames[frame] = Sprite.Create(_texture, new Rect(frame * stepPixels, 0, width, _source.rect.height), pivot, _source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
        _scroll.sprite = _frames[frame];
    }

    // 읽기 허용이 꺼진 그림도 읽게 렌더 텍스처를 거침
    private void Build(Sprite sprite)
    {
        _source = sprite;
        if (_frames != null) foreach (Sprite frame in _frames) if (frame != null) Destroy(frame);
        if (_texture != null) Destroy(_texture);
        _frames = null;
        _texture = null;
        if (sprite == null) return;

        Rect rect = sprite.rect;
        int w = Mathf.RoundToInt(rect.width), h = Mathf.RoundToInt(rect.height);
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        Texture source = sprite.texture;
        Graphics.Blit(source, rt, new Vector2(rect.width / source.width, rect.height / source.height), new Vector2(rect.x / source.width, rect.y / source.height));
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        read.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] pixels = read.GetPixels32();
        Destroy(read);
        var doubled = new Color32[w * 2 * h];
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
