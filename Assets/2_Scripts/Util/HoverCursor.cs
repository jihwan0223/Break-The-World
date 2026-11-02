using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// 버튼, 부수는 오브젝트, 파편 위에 올리면 커서를 손가락으로 바꿈 (모든 씬에서 하나만 살아 있음)
public class HoverCursor : MonoBehaviour
{
    private static readonly Vector2 HandHotspot = new Vector2(5f, 0f); // 손가락 끝 픽셀 (왼쪽 위 기준)

    private Texture2D _hand; // Resources/CursorHand.png
    private bool _shown; // 지금 손가락 커서인지

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("HoverCursor");
        DontDestroyOnLoad(go);
        go.AddComponent<HoverCursor>();
    }

    void Awake() => _hand = Resources.Load<Texture2D>("CursorHand");

    void Update()
    {
        bool over = _hand != null && Mouse.current != null && IsOverInteractable(Mouse.current.position.ReadValue()); // 손가락을 보일지
        if (over == _shown) return;
        _shown = over;
        UnityEngine.Cursor.SetCursor(over ? _hand : null, over ? HandHotspot : Vector2.zero, CursorMode.Auto); // null = 플레이어 설정의 기본 커서
    }

    private static bool IsOverInteractable(Vector2 screenPos)
    {
        // ponytail: 매 프레임 UIDocument 검색 + 물리 조회, 느려지면 마우스가 움직였을 때만 검사
        if (DebrisPool.IsOverButton(screenPos) || IsOverSlider(screenPos)) return true;
        if (UIPointerGuard.IsPointerOverUI || Camera.main == null) return false; // UI 위나 멈춘 창이 떠 있으면 월드는 안 봄

        Vector2 worldPos = Camera.main.ScreenToWorldPoint(screenPos); // 커서 밑 월드 좌표
        if (DebrisPool.Instance != null && DebrisPool.Instance.IsPieceAt(worldPos)) return true;
        foreach (Collider2D hit in Physics2D.OverlapPointAll(worldPos))
        {
            Click click = hit.GetComponent<Click>(); // 부술 수 있는 오브젝트
            if (click != null && click.enabled && !click.IsZoneHidden) return true;
        }
        return false;
    }

    // 볼륨 슬라이더 위거나, 슬라이더를 잡고 끄는 중이면 (밖으로 끌어도 유지)
    private static bool IsOverSlider(Vector2 screenPos)
    {
        foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            IPanel panel = document.rootVisualElement?.panel;
            if (panel == null) continue;
            if (panel.GetCapturingElement(PointerId.mousePointerId) is GlassCylinderSlider) return true;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y)); // UI 좌표는 위가 0
            for (VisualElement element = panel.Pick(panelPos); element != null; element = element.parent)
                if (element is GlassCylinderSlider && element.enabledInHierarchy) return true;
        }
        return false;
    }
}
