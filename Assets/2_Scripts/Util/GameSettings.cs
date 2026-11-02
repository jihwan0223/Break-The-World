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
    private const string NumberUnitsKey = "NumberUnits";

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

    public static int FpsIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(FpsKey, 1), 0, FpsOptions.Length - 1);
        set { PlayerPrefs.SetInt(FpsKey, Mathf.Clamp(value, 0, FpsOptions.Length - 1)); ApplyFrameRate(); }
    }

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
        ApplyFrameRate();
#if !UNITY_EDITOR
        ApplyDisplay();
#endif
        UpdateBgm();
        SceneManager.sceneLoaded += (_, __) => UpdateBgm();
    }

    private static void ApplyDisplay()
    {
#if !UNITY_EDITOR
        Vector2Int size = Resolution;
        Screen.SetResolution(size.x, size.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
    }

    private static void ApplyFrameRate()
    {
        int fps = FpsOptions[FpsIndex];
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps;
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
