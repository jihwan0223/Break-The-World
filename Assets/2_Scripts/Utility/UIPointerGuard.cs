// 마우스 포인터가 지금 UI Toolkit 요소 위에 있는지 알려주는 공용 플래그.
// UI 패널들이 PointerEnter/Leave로 이 값을 갱신하고, Click.cs는 클릭 처리 전에 이 값을 확인해서
// UI 위를 클릭했을 때 그 뒤의 월드 오브젝트가 같이 맞지 않도록 막는다.
public static class UIPointerGuard
{
    public static bool IsPointerOverUI { get; set; } // true면 지금 포인터가 UI 위에 있다는 뜻
}
