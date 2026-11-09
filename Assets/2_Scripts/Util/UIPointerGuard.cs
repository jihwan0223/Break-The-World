using System.Collections.Generic;
using UnityEngine.SceneManagement;

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

    // 창을 연 채로 씬을 나가면(설정 > 메인 메뉴로) 닫는 처리가 안 돌아서 값이 남음 - 씬이 내려갈 때 비움
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        Clear();
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene) => Clear();

    private static void Clear()
    {
        _overUI = false;
        _popups.Clear();
    }
}
