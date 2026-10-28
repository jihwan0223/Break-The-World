using UnityEngine;

public class ShieldGlow : MonoBehaviour
{
    private const float SizeRatio = 1.3f;
    private const float BlinkSpeed = 2.5f;
    private const int TextureSize = 32; // px
    private static readonly Color GlowColor = Color.white;

    private static Sprite _sprite;

    private SpriteRenderer _object;
    private Health _health;
    private SpriteRenderer _glow;

    public static void Attach(Health target)
    {
        if (target == null || target.GetComponent<SpriteRenderer>() == null) return;
        if (target.GetComponent<ShieldGlow>() == null) target.gameObject.AddComponent<ShieldGlow>();
    }

    public static void Detach(Health target)
    {
        ShieldGlow glow = target != null ? target.GetComponent<ShieldGlow>() : null;
        if (glow != null) Destroy(glow);
    }

    void Awake()
    {
        _object = GetComponent<SpriteRenderer>();
        _health = GetComponent<Health>();

        var glowObject = new GameObject("ShieldGlow");
        glowObject.transform.SetParent(transform, false);
        _glow = glowObject.AddComponent<SpriteRenderer>();
        _glow.sprite = GlowSprite();
        _glow.sharedMaterial = _object.sharedMaterial;

        Bounds local = _object.sprite != null ? _object.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
        glowObject.transform.localPosition = local.center;
        float size = Mathf.Max(local.size.x, local.size.y) * SizeRatio;
        glowObject.transform.localScale = new Vector3(size, size, 1f);
    }

    void OnDestroy()
    {
        if (_glow != null) Destroy(_glow.gameObject);
    }

    void LateUpdate()
    {
        bool shown = _object.enabled && !_object.forceRenderingOff && (_health == null || !_health.IsDead);
        _glow.enabled = shown;
        if (!shown) return;

        _glow.sortingLayerID = _object.sortingLayerID;
        _glow.sortingOrder = _object.sortingOrder - 1;
        float blink = 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * BlinkSpeed));
        Color color = GlowColor;
        color.a = blink * _object.color.a;
        _glow.color = color;
    }

    private static Sprite GlowSprite()
    {
        if (_sprite != null) return _sprite;
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[TextureSize * TextureSize];
        float half = TextureSize * 0.5f;
        for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                float alpha = distance >= 1f ? 0f : Mathf.Ceil((1f - distance) * 4f) / 4f * 0.8f;
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        _sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        return _sprite;
    }
}
