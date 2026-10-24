using UnityEngine;

// 보스전 보호막 오브젝트 뒤에서 깜빡이며 빛나는 도트 후광 - "이걸 부숴야 한다"가 바로 보이게.
// ZoneBreaker 보스전이 보호막 오브젝트를 꺼낼 때 붙이고, 보스전이 끝나면 뗌
public class ShieldGlow : MonoBehaviour
{
    private const float SizeRatio = 1.3f; // 후광 크기 (오브젝트 그림 크기 대비)
    private const float BlinkSpeed = 2.5f; // 깜빡이는 빠르기
    private const int TextureSize = 32; // 후광 그림 크기(픽셀)
    private static readonly Color GlowColor = Color.white; // 흰 빛

    private static Sprite _sprite; // 공용 후광 그림 (가운데가 밝고 바깥으로 계단처럼 흐려지는 원)

    private SpriteRenderer _object; // 빛낼 오브젝트 그림
    private Health _health; // 부서져 있으면 후광을 숨김
    private SpriteRenderer _glow; // 후광 (자식)

    public static void Attach(Health target)
    {
        if (target == null || target.GetComponent<SpriteRenderer>() == null) return;
        if (target.GetComponent<ShieldGlow>() == null) target.gameObject.AddComponent<ShieldGlow>();
    }

    public static void Detach(Health target)
    {
        ShieldGlow glow = target != null ? target.GetComponent<ShieldGlow>() : null; // 붙어 있던 후광
        if (glow != null) Destroy(glow);
    }

    void Awake()
    {
        _object = GetComponent<SpriteRenderer>();
        _health = GetComponent<Health>();

        var glowObject = new GameObject("ShieldGlow"); // 후광을 그릴 자식
        glowObject.transform.SetParent(transform, false);
        _glow = glowObject.AddComponent<SpriteRenderer>();
        _glow.sprite = GlowSprite();
        _glow.sharedMaterial = _object.sharedMaterial;

        Bounds local = _object.sprite != null ? _object.sprite.bounds : new Bounds(Vector3.zero, Vector3.one); // 오브젝트 그림 범위 (자기 좌표)
        glowObject.transform.localPosition = local.center;
        float size = Mathf.Max(local.size.x, local.size.y) * SizeRatio; // 후광 지름 (자기 좌표)
        glowObject.transform.localScale = new Vector3(size, size, 1f); // 후광 그림은 1x1 유닛
    }

    void OnDestroy()
    {
        if (_glow != null) Destroy(_glow.gameObject);
    }

    void LateUpdate()
    {
        bool shown = _object.enabled && !_object.forceRenderingOff && (_health == null || !_health.IsDead); // 오브젝트가 보일 때만
        _glow.enabled = shown;
        if (!shown) return;

        _glow.sortingLayerID = _object.sortingLayerID;
        _glow.sortingOrder = _object.sortingOrder - 1; // 오브젝트 뒤
        float blink = 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * BlinkSpeed)); // 밝아졌다 어두워졌다
        Color color = GlowColor;
        color.a = blink * _object.color.a; // 오브젝트가 서서히 나타나면 후광도 같이
        _glow.color = color;
    }

    // 가운데가 밝고 바깥으로 4단계로 흐려지는 도트 원 그림 (한 번만 만듦)
    private static Sprite GlowSprite()
    {
        if (_sprite != null) return _sprite;
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[TextureSize * TextureSize]; // 후광 픽셀
        float half = TextureSize * 0.5f; // 중심
        for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half; // 0 가운데 ~ 1 테두리
                float alpha = distance >= 1f ? 0f : Mathf.Ceil((1f - distance) * 4f) / 4f * 0.8f; // 계단식으로 흐려짐
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        _sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize); // 1x1 유닛
        return _sprite;
    }
}
