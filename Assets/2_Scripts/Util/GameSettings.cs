using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSettings
{
    private const string MasterVolumeKey = "MasterVolume";
    private const string BgmVolumeKey = "BgmVolume";
    private const float BgmLoudness = 1f; // 음량 100% = 원본 그대로
    private const string FullscreenKey = "Fullscreen"; // 1 = 전체화면, 0 = 창
    private const string ResolutionKey = "Resolution"; // "가로x세로"
    private const string FpsKey = "FpsLimit";
    private const string ShakeKey = "ScreenShake";
    private const string DamageNumbersKey = "DamageNumbers";
    private const string HoverClickKey = "HoverClick"; // 커서 타격 켜기/끄기
    private const string NumberUnitsKey = "NumberUnits";
    private const string RunInBackgroundKey = "RunInBackground"; // 1 = 다른 창으로 가거나 최소화해도 게임 진행
    private const string VSyncKey = "VSync"; // 1 = 모니터 주사율에 맞춤 (프레임 제한 무시)

    public static readonly int[] FpsOptions = { 30, 60, 120, 144, -1 }; // -1 = 무제한

    private const string MenuSceneName = "MenuScene";
    private static AudioSource _bgm; // 씬이 바뀌어도 유지

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
            AudioListener.volume = MasterVolume;
        }
    }

    public static float BgmVolume
    {
        get => PlayerPrefs.GetFloat(BgmVolumeKey, 0.5f);
        set
        {
            PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
            if (_bgm != null) _bgm.volume = BgmVolume * BgmLoudness;
        }
    }

    public static bool ScreenShake
    {
        get => PlayerPrefs.GetInt(ShakeKey, 1) == 1;
        set => PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0);
    }

    // 데모면 맵3(회사)까지만 - 디버그에서 전환
    public static bool IsDemo
    {
        get => PlayerPrefs.GetInt("FullVersion", 0) == 0;
        set => PlayerPrefs.SetInt("FullVersion", value ? 0 : 1);
    }
    public const int DemoZoneCount = 3;
    public const int DemoObjectCount = 15; // 데모는 회사(14)까지
    public const int DemoWeaponCount = 6; // 데모는 다이너마이트(5)까지 - 나중에 추가하는 무기는 풀버전 전용
    public static bool IsDemoLockedObject(int objectIndex) => IsDemo && objectIndex >= DemoObjectCount;
    public static bool IsDemoLockedWeapon(int weaponIndex) => IsDemo && weaponIndex >= DemoWeaponCount;

    // 커서 타격(커서만 올려도 자동으로 때림) 켜기/끄기 - 업그레이드를 산 뒤에만 설정에 보임
    public static bool HoverClick
    {
        get => PlayerPrefs.GetInt(HoverClickKey, 1) == 1;
        set => PlayerPrefs.SetInt(HoverClickKey, value ? 1 : 0);
    }

    public static bool DamageNumbers
    {
        get => PlayerPrefs.GetInt(DamageNumbersKey, 1) == 1;
        set => PlayerPrefs.SetInt(DamageNumbersKey, value ? 1 : 0);
    }

    public static bool NumberUnits
    {
        get => PlayerPrefs.GetInt(NumberUnitsKey, 1) == 1; // 기본은 언어 상관없이 K/M/B 단위
        set { PlayerPrefs.SetInt(NumberUnitsKey, value ? 1 : 0); OnNumberFormatChanged?.Invoke(); }
    }

    public static event System.Action OnNumberFormatChanged;

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
        set { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); ApplyDisplay(); }
    }

    public static bool RunInBackground
    {
        get => PlayerPrefs.GetInt(RunInBackgroundKey, 1) == 1;
        set { PlayerPrefs.SetInt(RunInBackgroundKey, value ? 1 : 0); Application.runInBackground = value; }
    }

    public static bool VSync
    {
        get => PlayerPrefs.GetInt(VSyncKey, 0) == 1;
        set { PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0); ApplyFrameRate(); }
    }

    public static int FpsIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(FpsKey, DefaultFpsIndex), 0, FpsOptions.Length - 1);
        set { PlayerPrefs.SetInt(FpsKey, Mathf.Clamp(value, 0, FpsOptions.Length - 1)); ApplyFrameRate(); }
    }

    // 기본 프레임: 모니터가 144Hz 이상이면 144, 아니면 60
    private static int DefaultFpsIndex =>
        System.Array.IndexOf(FpsOptions, Screen.currentResolution.refreshRateRatio.value >= 143.5 ? 144 : 60);

    // 고를 수 있는 해상도: 16:9 표준 크기 전부 (작은 것부터)
    private static readonly Vector2Int[] StandardResolutions =
        { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160) };

    public static List<Vector2Int> Resolutions => StandardResolutions.ToList();

    public static Vector2Int Resolution
    {
        get
        {
            string saved = PlayerPrefs.GetString(ResolutionKey, "");
            string[] parts = saved.Split('x');
            List<Vector2Int> allowed = Resolutions; // 고를 수 있는 해상도
            if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) && allowed.Contains(new Vector2Int(w, h)))
                return new Vector2Int(w, h);
            // 저장값이 없으면 모니터 이하 중 가장 큰 것
            UnityEngine.Resolution screen = Screen.currentResolution;
            Vector2Int fit = allowed[0];
            foreach (Vector2Int r in allowed) if (r.x <= screen.width && r.y <= screen.height) fit = r;
            return fit;
        }
        set { PlayerPrefs.SetString(ResolutionKey, $"{value.x}x{value.y}"); ApplyDisplay(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Startup()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        Debug.unityLogger.filterLogType = LogType.Warning; // 출시 빌드는 경고/에러만 기록
#endif
        AudioListener.volume = MasterVolume;
        Application.runInBackground = RunInBackground;
        ApplyFrameRate();
#if !UNITY_EDITOR
        ApplyDisplay();
        var watcher = new GameObject("DisplayWatcher"); // 창 모드에서 유저가 창을 키우는지 봄
        Object.DontDestroyOnLoad(watcher);
        watcher.AddComponent<DisplayWatcher>();
#endif
        UpdateBgm();
        SceneManager.sceneLoaded += (_, __) => UpdateBgm();
    }

    private static float _displayAppliedAt; // 마지막으로 화면 모드를 바꾼 시각 (바뀌는 동안은 감시 안 함)
    private const float DisplaySettleTime = 1f; // 초

    private static void ApplyDisplay()
    {
        _displayAppliedAt = Time.unscaledTime;
#if !UNITY_EDITOR
        Vector2Int size = Resolution;
        Screen.SetResolution(size.x, size.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
    }

    // 창 모드인데 유저가 창을 직접 키우거나(최대화) Alt+Enter로 전체 화면을 만들면 설정도 전체 화면으로
    private class DisplayWatcher : MonoBehaviour
    {
        void Update()
        {
            LockWindowFrame();
            if (Fullscreen || Time.unscaledTime - _displayAppliedAt < DisplaySettleTime) return;
            Vector2Int size = Resolution; // 설정한 창 크기
            if (Screen.fullScreen || Screen.width > size.x || Screen.height > size.y) Fullscreen = true;
        }

#if UNITY_STANDALONE_WIN
        // 창 모드: 최대화(ㅁ) 버튼과 테두리 끌어서 크기 조절을 막음 (Unity가 화면 모드를 바꿀 때마다 되살려서 매 프레임 확인)
        private const int GwlStyle = -16; // 창 스타일 번호
        private const long WsMaximizeBox = 0x00010000L; // 최대화 버튼
        private const long WsThickFrame = 0x00040000L; // 끌어서 크기 조절되는 테두리
        private const int GwlExStyle = -20; // 확장 창 스타일 번호
        private const uint SwpFrameChanged = 0x0020, SwpNoMove = 0x0002, SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010; // 테두리 다시 그리기 (위치는 그대로)

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Rect { public int left, top, right, bottom; } // Win32 사각형
        private System.IntPtr _window; // 게임 창 핸들

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern System.IntPtr GetWindowLongPtr(System.IntPtr hWnd, int index);
        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern System.IntPtr SetWindowLongPtr(System.IntPtr hWnd, int index, System.IntPtr value);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(System.IntPtr hWnd, System.IntPtr after, int x, int y, int w, int h, uint flags);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetClientRect(System.IntPtr hWnd, out Rect rect);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool AdjustWindowRectEx(ref Rect rect, uint style, bool menu, uint exStyle);

        private void LockWindowFrame()
        {
            if (Screen.fullScreen) return;
            if (_window == System.IntPtr.Zero) _window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (_window == System.IntPtr.Zero) return;
            long style = GetWindowLongPtr(_window, GwlStyle).ToInt64(); // 지금 창 스타일
            if ((style & (WsMaximizeBox | WsThickFrame)) == 0) return;
            GetClientRect(_window, out Rect client); // 게임 화면 크기 (테두리가 얇아져도 이대로 유지)
            long locked = style & ~(WsMaximizeBox | WsThickFrame); // 바꿀 스타일
            SetWindowLongPtr(_window, GwlStyle, new System.IntPtr(locked));
            Rect outer = client; // 같은 게임 화면 크기에 맞는 바깥 창 크기
            AdjustWindowRectEx(ref outer, (uint)locked, false, (uint)GetWindowLongPtr(_window, GwlExStyle).ToInt64());
            SetWindowPos(_window, System.IntPtr.Zero, 0, 0, outer.right - outer.left, outer.bottom - outer.top, SwpFrameChanged | SwpNoMove | SwpNoZOrder | SwpNoActivate);
        }
#else
        private void LockWindowFrame() { }
#endif
    }

    private static void ApplyFrameRate()
    {
        int fps = FpsOptions[FpsIndex]; // 프레임 제한 값 (-1 = 무제한)
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = VSync ? -1 : fps; // 수직 동기화 중엔 제한 무시
    }

    private static void UpdateBgm()
    {
        if (SceneManager.GetActiveScene().name != MenuSceneName)
        {
            if (_bgm != null) _bgm.Stop();
            return;
        }
        if (_bgm != null)
        {
            if (!_bgm.isPlaying) _bgm.Play();
            return;
        }
        if (GameAudio.Instance == null || GameAudio.Instance.bgm == null) return;
        var go = new GameObject("BGM");
        Object.DontDestroyOnLoad(go);
        _bgm = go.AddComponent<AudioSource>();
        _bgm.clip = GameAudio.Instance.bgm;
        _bgm.loop = true;
        _bgm.playOnAwake = false;
        _bgm.volume = BgmVolume * BgmLoudness;
        _bgm.ignoreListenerPause = true;
        _bgm.Play();
    }
}
