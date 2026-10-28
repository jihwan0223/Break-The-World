// 포인터가 UI 위면 월드 클릭을 막음 (게임을 멈추는 창이 떠 있어도 true)
public static class UIPointerGuard
{
    private static bool _overUI;

    public static bool IsPointerOverUI
    {
        get => _overUI || UnityEngine.Time.timeScale == 0f;
        set => _overUI = value;
    }
}
