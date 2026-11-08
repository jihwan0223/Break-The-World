using System.Collections.Generic;
using UnityEngine;

public static class SfxPlayer
{
    private const float MinIntervalPerClip = 0.05f; // 초
    private const int MaxVoices = 5;
    private const float VoiceHoldSeconds = 0.3f; // 초
    private const float BusyWindow = 0.5f; // 초
    private const int QuietStart = 4;
    private const int QuietFull = 14;
    private const float MinBusyVolume = 0.45f;
    private const float PitchJitter = 0.06f;
    private const float BaseVolume = 0.9f;
    private const float WindowOpenVolume = 0.3f; // 창이 열려 있는 동안 게임 소리 배율

    public const string VolumeKey = "SfxVolume";

    private static float? _volume;
    private static readonly Dictionary<AudioClip, float> _lastPlayTime = new Dictionary<AudioClip, float>();
    private static readonly List<float> _voiceEndTimes = new List<float>();
    private static readonly Queue<float> _recentPlayTimes = new Queue<float>();
    private static readonly Dictionary<AudioClip, AudioClip> _trimmed = new Dictionary<AudioClip, AudioClip>();
    private const float SilenceLevel = 0.02f; // 최대 음량 대비 - 이보다 작은 앞부분은 무음으로 보고 잘라냄
    private const float KeepBeforeAttack = 0.003f; // 초

    // 플레이 시작 때 도메인 리로드를 안 해서 직접 비움
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        _volume = null;
        _lastPlayTime.Clear();
        _voiceEndTimes.Clear();
        _recentPlayTimes.Clear();
        foreach (var pair in _trimmed)
            if (pair.Value != pair.Key && pair.Value != null) Object.Destroy(pair.Value);
        _trimmed.Clear();
        foreach (var pair in _tailCut)
            if (pair.Value != pair.Key && pair.Value != null) Object.Destroy(pair.Value);
        _tailCut.Clear();
    }

    private static AudioClip Trimmed(AudioClip clip)
    {
        if (_trimmed.TryGetValue(clip, out AudioClip trimmed)) return trimmed;
        trimmed = clip;
        if (clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.LoadAudioData())
        {
            var data = new float[clip.samples * clip.channels];
            if (clip.GetData(data, 0))
            {
                float peak = 0f;
                foreach (float sample in data) peak = Mathf.Max(peak, Mathf.Abs(sample));
                int first = 0;
                while (first < data.Length && Mathf.Abs(data[first]) < peak * SilenceLevel) first++;
                int startFrame = Mathf.Max(0, first / clip.channels - Mathf.RoundToInt(KeepBeforeAttack * clip.frequency));
                if (startFrame > 0 && startFrame < clip.samples)
                {
                    int frames = clip.samples - startFrame;
                    var rest = new float[frames * clip.channels];
                    System.Array.Copy(data, startFrame * clip.channels, rest, 0, rest.Length);
                    trimmed = AudioClip.Create(clip.name, frames, clip.channels, clip.frequency, false);
                    trimmed.SetData(rest, 0);
                }
            }
        }
        _trimmed[clip] = trimmed;
        return trimmed;
    }

    private static readonly Dictionary<AudioClip, AudioClip> _tailCut = new Dictionary<AudioClip, AudioClip>();
    private const float TailLevel = 0.2f; // 최대 음량 대비 - 큰 타격 뒤 이보다 작아지면 거기서 자름
    private const float TailWindow = 0.01f; // 초 - 음량을 재는 칸
    private const float TailFade = 0.03f; // 초 - 자른 끝을 부드럽게 줄임

    // 피치를 낮추면 소리가 늘어나 뒤쪽 잔소리가 두 번째 타격처럼 들려서, 첫 타격만 남김
    private static AudioClip TailCut(AudioClip clip)
    {
        if (_tailCut.TryGetValue(clip, out AudioClip cut)) return cut;
        cut = clip;
        var data = new float[clip.samples * clip.channels];
        if (clip.GetData(data, 0))
        {
            int peakIndex = 0;
            for (int i = 1; i < data.Length; i++) if (Mathf.Abs(data[i]) > Mathf.Abs(data[peakIndex])) peakIndex = i;
            float peak = Mathf.Abs(data[peakIndex]);
            int window = Mathf.Max(1, Mathf.RoundToInt(TailWindow * clip.frequency)) * clip.channels; // 칸 크기 (샘플)
            int end = data.Length;
            for (int start = peakIndex; start < data.Length; start += window)
            {
                float loud = 0f;
                for (int i = start; i < Mathf.Min(start + window, data.Length); i++) loud = Mathf.Max(loud, Mathf.Abs(data[i]));
                if (loud < peak * TailLevel) { end = start; break; }
            }
            int fade = Mathf.RoundToInt(TailFade * clip.frequency) * clip.channels; // 페이드 길이 (샘플)
            int length = Mathf.Min(data.Length, end + fade) / clip.channels * clip.channels;
            if (length < data.Length)
            {
                var kept = new float[length];
                System.Array.Copy(data, kept, length);
                for (int i = Mathf.Max(0, length - fade); i < length; i++) kept[i] *= (length - i) / (float)fade;
                cut = AudioClip.Create(clip.name, length / clip.channels, clip.channels, clip.frequency, false);
                cut.SetData(kept, 0);
            }
        }
        _tailCut[clip] = cut;
        return cut;
    }

    public static float Volume
    {
        get => _volume ??= PlayerPrefs.GetFloat(VolumeKey, 1f);
        set
        {
            _volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, _volume.Value);
        }
    }

    public static void Play(AudioSource source, AudioClip clip, bool priority = false, float pitch = 1f)
    {
        if (source == null || clip == null) return;

        float now = Time.unscaledTime; // timeScale 0에서도 동작
        if (_lastPlayTime.TryGetValue(clip, out float last) && now - last < MinIntervalPerClip) return;

        _voiceEndTimes.RemoveAll(end => end <= now);
        if (!priority && _voiceEndTimes.Count >= MaxVoices) return;

        while (_recentPlayTimes.Count > 0 && now - _recentPlayTimes.Peek() > BusyWindow)
            _recentPlayTimes.Dequeue();

        float busy = Mathf.InverseLerp(QuietStart, QuietFull, _recentPlayTimes.Count); // 0 = 한가, 1 = 심한 연타
        source.pitch = pitch + Random.Range(-PitchJitter, PitchJitter);
        AudioClip played = Trimmed(clip);
        if (pitch < 0.95f) played = TailCut(played); // 연타 피치(1 이상)는 그대로
        source.PlayOneShot(played,BaseVolume * Volume * Mathf.Lerp(1f, MinBusyVolume, busy) * (UIPointerGuard.PopupOpen ? WindowOpenVolume : 1f));

        _lastPlayTime[clip] = now;
        _voiceEndTimes.Add(now + Mathf.Min(clip.length / source.pitch, VoiceHoldSeconds));
        _recentPlayTimes.Enqueue(now);
    }
}
