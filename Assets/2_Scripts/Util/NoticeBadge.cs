using UnityEngine;
using UnityEngine.UI;

// "지금 살 수 있음"을 알리는 느낌표 표시. 이미지 파일 없이 코드로 16x16 도트 그림(빨간 원 + 흰 느낌표)을 만들어
// 우상단 버튼(UI 툴킷)과 업그레이드 트리 노드(Canvas)가 같은 그림을 씀
public static class NoticeBadge
{
    private const int Size = 16; // 그림 한 변(px)
    private const float NodeSizeRatio = 0.4f; // 노드에 붙일 때 크기 (노드 한 변 기준 비율)

    private static Sprite _sprite; // 한 번 만들어 캐시해두는 느낌표 그림

    public static Sprite Sprite
    {
        get
        {
            if (_sprite != null) return _sprite;

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp }; // 도트가 또렷하게 Point
            Color fill = new Color(0.9f, 0.2f, 0.2f); // 원 안쪽 빨강
            Color rim = new Color(0.45f, 0.05f, 0.08f); // 원 테두리 짙은 빨강
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float distance = new Vector2(x - 7.5f, y - 7.5f).magnitude; // 그림 중심에서 이 픽셀까지 거리
                    bool mark = (x == 7 || x == 8) && ((y >= 6 && y <= 12) || y == 3 || y == 4); // 느낌표 (위쪽 막대 + 아래 점, y는 아래에서부터 셈)
                    texture.SetPixel(x, y, distance > 7.9f ? Color.clear : mark ? Color.white : distance > 6.6f ? rim : fill);
                }
            }
            texture.Apply();

            _sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            return _sprite;
        }
    }

    // 트리 노드의 느낌표를 켜고 끔. 오른쪽 위 모서리에 붙이고, 처음 켤 때 한 번만 만듦 (badge는 노드가 들고 있는 필드)
    public static void Set(ref GameObject badge, RectTransform node, bool on)
    {
        if (badge == null)
        {
            if (!on) return;

            badge = new GameObject("NoticeBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(node, false);

            Image image = badge.GetComponent<Image>(); // 느낌표 그림
            image.sprite = Sprite;
            image.raycastTarget = false; // 노드 클릭/호버를 가로채지 않게

            float size = node.rect.width * NodeSizeRatio; // 느낌표 한 변
            var rect = (RectTransform)badge.transform; // 위치/크기를 정할 RectTransform
            rect.anchorMin = rect.anchorMax = Vector2.one; // 노드 오른쪽 위 모서리 기준
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(-size * 0.25f, -size * 0.25f); // 모서리에 걸치되 살짝 안쪽으로
        }
        badge.SetActive(on);
    }
}
