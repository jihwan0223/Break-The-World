using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSettings
{
    private const string MasterVolumeKey = "MasterVolume";
    private const string BgmVolumeKey = "BgmVolume";
    private const float BgmLoudness = 0.2f; // 음량 100%에서도 원본의 20%
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

    public static bool DamageNumbers
    {
        get => PlayerPrefs.GetInt(DamageNumbersKey, 1) == 1;
        set => PlayerPrefs.SetInt(DamageNumbersKey, value ? 1 : 0);
    }

    public static bool NumberUnits
    {
        get => PlayerPrefs.GetInt(NumberUnitsKey, DefaultNumberUnits ? 1 : 0) == 1;
        set { PlayerPrefs.SetInt(NumberUnitsKey, value ? 1 : 0); OnNumberFormatChanged?.Invoke(); }
    }

    public static event System.Action OnNumberFormatChanged;

    private static bool DefaultNumberUnits
    {
        get
        {
            SystemLanguage lang = Application.systemLanguage;
            return lang != SystemLanguage.Korean && lang != SystemLanguage.Japanese && lang != SystemLanguage.Chinese
                && lang != SystemLanguage.ChineseSimplified && lang != SystemLanguage.ChineseTraditional;
        }
    }

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

    // 고를 수 있는 해상도: UI가 깨지지 않게 16:9이고 1920x1080 이상인 것만 (같은 크기는 하나만, 작은 것부터).
    // 모니터가 그보다 작으면 1920x1080 하나만
    public static List<Vector2Int> Resolutions
    {
        get
        {
            List<Vector2Int> list = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct()
                .Where(r => r.x >= 1920 && r.y >= 1080 && r.x * 9 == r.y * 16).OrderBy(r => r.x * r.y).ToList(); // 허용 해상도
            if (list.Count == 0) list.Add(new Vector2Int(1920, 1080));
            return list;
        }
    }

    public static Vector2Int Resolution
    {
        get
        {
            string saved = PlayerPrefs.GetString(ResolutionKey, "");
            string[] parts = saved.Split('x');
            List<Vector2Int> allowed = Resolutions; // 고를 수 있는 해상도
            if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) && allowed.Contains(new Vector2Int(w, h)))
                return new Vector2Int(w, h);
            return allowed[allowed.Count - 1]; // 저장값이 없거나 허용 밖(예전 4:3 등)이면 가장 큰 16:9
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
