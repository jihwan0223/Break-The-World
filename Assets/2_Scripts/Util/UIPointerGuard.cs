// 마우스 포인터가 지금 UI Toolkit 요소 위에 있는지 알려주는 공용 플래그.
// UI 패널들이 PointerEnter/Leave로 이 값을 갱신하고, Click.cs는 클릭 처리 전에 이 값을 확인해서
// UI 위를 클릭했을 때 그 뒤의 월드 오브젝트가 같이 맞지 않도록 막는다.
// 업그레이드 화면/무기·도감 창/실패 창처럼 게임을 멈추는(timeScale 0) 창이 떠 있는 동안에도 항상 true -
// 창의 빈 곳이나 가장자리를 눌러도 뒤의 오브젝트/파편이 눌리지 않게
public static class UIPointerGuard
{
    private static bool _overUI; // 포인터가 UI 요소 위에 있는지 (UI들이 직접 켜고 끔)

    public static bool IsPointerOverUI // true면 지금 월드 클릭을 막아야 함
    {
        get => _overUI || UnityEngine.Time.timeScale == 0f;
        set => _overUI = value;
    }
}
