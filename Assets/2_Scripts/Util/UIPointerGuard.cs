using System.Collections.Generic;

// 포인터가 UI 위면 월드 클릭을 막음 (창이 떠 있거나 게임이 멈춰 있어도 true)
public static class UIPointerGuard
{
    private static bool _overUI;
    private static readonly HashSet<string> _popups = new HashSet<string>(); // 열려 있는 창 (게임은 안 멈추지만 월드 클릭은 막음)

    public static bool PopupOpen => _popups.Count > 0;

    public static void SetPopup(string key, bool open)
    {
        if (open) _popups.Add(key);
        else _popups.Remove(key);
    }

    public static bool IsPointerOverUI
    {
        get => _overUI || PopupOpen || UnityEngine.Time.timeScale == 0f;
        set => _overUI = value;
    }
}
