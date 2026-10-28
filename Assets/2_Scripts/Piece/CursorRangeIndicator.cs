using UnityEngine;
using UnityEngine.InputSystem;

public class CursorRangeIndicator : MonoBehaviour
{
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private int sortingOrder = 40; // 오브젝트/파편보다 앞

    private const int TextureSize = 64;

    private SpriteRenderer _circle;

    void Awake()
    {
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        float center = (TextureSize - 1) * 0.5f;
        float radius = TextureSize * 0.5f;
        for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
                texture.SetPixel(x, y, (x - center) * (x - center) + (y - center) * (y - center) <= radius * radius ? Color.white : Color.clear);
        texture.Apply();

        // 존 전환에 안 꺼지게 씬 최상위에 둠
        var circleObject = new GameObject("CursorRange");
        _circle = circleObject.AddComponent<SpriteRenderer>();
        _circle.sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize); // 크기 1 = 지름 1 유닛
        _circle.color = color;
        _circle.sortingOrder = sortingOrder;
        _circle.enabled = false;
    }

    void OnDestroy()
    {
        if (_circle != null) Destroy(_circle.gameObject);
    }

    void LateUpdate()
    {
        float radius = UpgradeManager.Instance != null ? UpgradeManager.Instance.CursorRadius : 0f;
        bool show = radius > 0f && Mouse.current != null && Camera.main != null && !UIPointerGuard.IsPointerOverUI;
        _circle.enabled = show;
        if (!show) return;

        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        worldPos.z = 0f;
        _circle.transform.position = worldPos;
        _circle.transform.localScale = Vector3.one * (radius * 2f);
    }
}
