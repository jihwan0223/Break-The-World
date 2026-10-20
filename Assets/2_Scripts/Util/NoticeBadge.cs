using UnityEngine;
using UnityEngine.UI;

// "지금 살 수 있음"을 알리는 느낌표 표시. 이미지 파일 없이 코드로 16x16 도트 그림(색 원 + 흰 느낌표)을 만들어
// 우상단 버튼(UI 툴킷)과 업그레이드 트리 노드(Canvas)가 같은 그림을 씀. 조각으로 살 수 있으면 빨강, 결정으로 살 수 있으면 노랑
public static class NoticeBadge
{
    private const int Size = 16; // 그림 한 변(px)
    private const float NodeSizeRatio = 0.4f; // 노드에 붙일 때 크기 (노드 한 변 기준 비율)

    private static Sprite _sprite; // 한 번 만들어 캐시해두는 빨간 느낌표 그림 (조각)
    private static Sprite _crystalSprite; // 한 번 만들어 캐시해두는 노란 느낌표 그림 (결정)

    // 조각으로 살 수 있을 때 - 빨간 원
    public static Sprite Sprite => _sprite != null ? _sprite : (_sprite = Create(new Color(0.9f, 0.2f, 0.2f), new Color(0.45f, 0.05f, 0.08f)));

    // 결정으로 살 수 있을 때 - 노란 원
    public static Sprite CrystalSprite => _crystalSprite != null ? _crystalSprite : (_crystalSprite = Create(new Color(1f, 0.8f, 0.15f), new Color(0.6f, 0.4f, 0.02f)));

    // fill 색 원 + rim 색 테두리 + 흰 느낌표 도트 그림
    private static Sprite Create(Color fill, Color rim)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp }; // 도트가 또렷하게 Point
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
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
    }

    // 트리 노드의 느낌표를 켜고 끔. 오른쪽 위 모서리에 붙이고, 처음 켤 때 한 번만 만듦 (badge는 노드가 들고 있는 필드).
    // crystal이면 노란 느낌표 (결정으로 사는 노드)
    public static void Set(ref GameObject badge, RectTransform node, bool on, bool crystal = false)
    {
        if (badge == null)
        {
            if (!on) return;

            badge = new GameObject("NoticeBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(node, false);

            Image image = badge.GetComponent<Image>(); // 느낌표 그림
            image.raycastTarget = false; // 노드 클릭/호버를 가로채지 않게

            float size = node.rect.width * NodeSizeRatio; // 느낌표 한 변
            var rect = (RectTransform)badge.transform; // 위치/크기를 정할 RectTransform
            rect.anchorMin = rect.anchorMax = Vector2.one; // 노드 오른쪽 위 모서리 기준
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(-size * 0.25f, -size * 0.25f); // 모서리에 걸치되 살짝 안쪽으로
        }
        if (on) badge.GetComponent<Image>().sprite = crystal ? CrystalSprite : Sprite;
        badge.SetActive(on);
    }
}
