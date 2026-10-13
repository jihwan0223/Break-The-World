using UnityEngine;
using UnityEngine.InputSystem;

// 커서 범위 업그레이드를 사면 커서를 따라다니는 범위 표시. 지금은 임시로 흰색 반투명 원
// (원 안에 걸친 오브젝트가 한 번에 클릭되고, 커서 줍기도 이 범위로 주움)
public class CursorRangeIndicator : MonoBehaviour
{
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.25f); // 원 색 (흰색, 약간 투명)
    [SerializeField] private int sortingOrder = 40; // 오브젝트/파편/줍기 숫자보다 앞에 그려지게

    private const int TextureSize = 64; // 원 그림 한 변의 픽셀 수

    private SpriteRenderer _circle; // 커서를 따라다니는 원

    void Awake()
    {
        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; // 원을 직접 그려 넣을 텍스처
        float center = (TextureSize - 1) * 0.5f; // 텍스처 중심 좌표
        float radius = TextureSize * 0.5f; // 원 반지름(픽셀)
        for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
                texture.SetPixel(x, y, (x - center) * (x - center) + (y - center) * (y - center) <= radius * radius ? Color.white : Color.clear);
        texture.Apply();

        // 부모 크기나 존 전환(그림 끄기)의 영향을 안 받게 씬 최상위에 따로 둠
        var circleObject = new GameObject("CursorRange"); // 원을 그리는 오브젝트
        _circle = circleObject.AddComponent<SpriteRenderer>();
        _circle.sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize); // 크기 1일 때 지름이 1 유닛
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
        float radius = UpgradeManager.Instance != null ? UpgradeManager.Instance.CursorRadius : 0f; // 지금 커서 범위 반지름 (업그레이드 전이면 0)
        bool show = radius > 0f && Mouse.current != null && Camera.main != null && !UIPointerGuard.IsPointerOverUI; // 범위가 있고 커서가 게임 화면 위일 때만 표시
        _circle.enabled = show;
        if (!show) return;

        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()); // 커서의 월드 위치
        worldPos.z = 0f;
        _circle.transform.position = worldPos;
        _circle.transform.localScale = Vector3.one * (radius * 2f);
    }
}
