using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// 설정창에서 바꾸는 값들 (PlayerPrefs에 저장). 게임을 켜면 저장된 화면/음량 설정을 바로 적용함. 배경음악은 메뉴 씬에서만 나옴.
// 마스터 음량은 MainMenuUI, 효과음 음량은 SfxPlayer가 따로 저장함 (예전부터 쓰던 키 그대로)
public static class GameSettings
{
    private const string MasterVolumeKey = "MasterVolume"; // 마스터 음량 (MainMenuUI와 같은 키)
    private const string BgmVolumeKey = "BgmVolume"; // 배경음악 음량
    private const string FullscreenKey = "Fullscreen"; // 1 = 전체화면, 0 = 창 모드
    private const string ResolutionKey = "Resolution"; // "가로x세로"
    private const string FpsKey = "FpsLimit"; // FpsOptions 번호
    private const string ShakeKey = "ScreenShake"; // 화면 흔들림 켜기(1)/끄기(0)
    private const string DamageNumbersKey = "DamageNumbers"; // 데미지 숫자 켜기(1)/끄기(0)

    public static readonly int[] FpsOptions = { 30, 60, 120, -1 }; // 프레임 제한 선택지 (-1 = 화면 주사율에 맞춤)

    private const string MenuSceneName = "MenuScene"; // 배경음악이 나오는 씬
    private static AudioSource _bgm; // 배경음악 재생기 (씬이 바뀌어도 남아 있음)

    public static float MasterVolume => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);

    public static float BgmVolume
    {
        get => PlayerPrefs.GetFloat(BgmVolumeKey, 0.5f);
        set
        {
            PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
            if (_bgm != null) _bgm.volume = BgmVolume;
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

    // 고를 수 있는 해상도 (같은 크기는 하나만, 작은 것부터)
    public static List<Vector2Int> Resolutions =>
        Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct().OrderBy(r => r.x * r.y).ToList();

    public static Vector2Int Resolution
    {
        get
        {
            string saved = PlayerPrefs.GetString(ResolutionKey, ""); // 저장된 "가로x세로"
            string[] parts = saved.Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h)) return new Vector2Int(w, h);
            return new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        }
        set { PlayerPrefs.SetString(ResolutionKey, $"{value.x}x{value.y}"); ApplyDisplay(); }
    }

    // 게임이 켜질 때 저장된 설정을 적용하고, 씬이 바뀔 때마다 배경음악을 켜거나 끔
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Startup()
    {
        AudioListener.volume = MasterVolume;
        ApplyFrameRate();
#if !UNITY_EDITOR
        ApplyDisplay(); // 에디터 게임 화면 크기는 건드리지 않음
#endif
        UpdateBgm();
        SceneManager.sceneLoaded += (_, __) => UpdateBgm();
    }

    private static void ApplyDisplay()
    {
#if !UNITY_EDITOR
        Vector2Int size = Resolution; // 적용할 해상도
        Screen.SetResolution(size.x, size.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
    }

    private static void ApplyFrameRate()
    {
        int fps = FpsOptions[FpsIndex]; // 제한 값
        QualitySettings.vSyncCount = fps < 0 ? 1 : 0; // 무제한 대신 화면 주사율에 맞춤
        Application.targetFrameRate = fps < 0 ? -1 : fps;
    }

    // 메뉴 씬이면 틀고(이미 나오는 중이면 그대로), 게임 씬이면 멈춤
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
        var go = new GameObject("BGM"); // 배경음악 재생기
        Object.DontDestroyOnLoad(go);
        _bgm = go.AddComponent<AudioSource>();
        _bgm.clip = GameAudio.Instance.bgm;
        _bgm.loop = true;
        _bgm.playOnAwake = false;
        _bgm.volume = BgmVolume;
        _bgm.ignoreListenerPause = true;
        _bgm.Play();
    }
}
