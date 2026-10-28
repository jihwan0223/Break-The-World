using UnityEngine;
using UnityEngine.UI;

public static class NoticeBadge
{
    private const int Size = 16; // px
    private const float NodeSizeRatio = 0.28f; // 노드 한 변 대비

    private static Sprite _sprite;
    private static Sprite _crystalSprite;

    public static Sprite Sprite => _sprite != null ? _sprite : (_sprite = Create(new Color(0.9f, 0.2f, 0.2f), new Color(0.45f, 0.05f, 0.08f)));

    public static Sprite CrystalSprite => _crystalSprite != null ? _crystalSprite : (_crystalSprite = Create(new Color(1f, 0.8f, 0.15f), new Color(0.6f, 0.4f, 0.02f)));

    private static Sprite Create(Color fill, Color rim)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float distance = new Vector2(x - 7.5f, y - 7.5f).magnitude;
                bool mark = (x == 7 || x == 8) && ((y >= 6 && y <= 12) || y == 3 || y == 4);
                texture.SetPixel(x, y, distance > 7.9f ? Color.clear : mark ? Color.white : distance > 6.6f ? rim : fill);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
    }

    public static void Set(ref GameObject badge, RectTransform node, bool on, bool crystal = false)
    {
        if (badge == null)
        {
            if (!on) return;

            badge = new GameObject("NoticeBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(node, false);

            Image image = badge.GetComponent<Image>();
            image.raycastTarget = false; // 노드 클릭을 가로채지 않게

            float size = node.rect.width * NodeSizeRatio;
            var rect = (RectTransform)badge.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(size * 0.15f, size * 0.15f);
        }
        if (on) badge.GetComponent<Image>().sprite = crystal ? CrystalSprite : Sprite;
        badge.SetActive(on);
    }
}
