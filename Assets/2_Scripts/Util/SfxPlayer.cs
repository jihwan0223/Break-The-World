using System.Collections.Generic;
using UnityEngine;

// 효과음(클릭/파괴) 재생 창구. 빠르게 연타하면 PlayOneShot이 계속 겹쳐 쌓여 시끄러워지는 걸 막음:
// - 같은 소리는 최소 간격 안에 다시 울리면 건너뜀
// - 동시에 울리는 소리 개수 제한 (파괴 소리는 우선이라 제한 무시)
// - 최근에 많이 울렸을수록 볼륨을 낮춤 + 피치를 살짝 흔들어 기계적인 반복감을 줄임
// 효과음 볼륨(설정 화면)도 여기서 곱해짐
public static class SfxPlayer
{
    private const float MinIntervalPerClip = 0.05f; // 같은 소리를 다시 울릴 수 있는 최소 간격(초)
    private const int MaxVoices = 5; // 동시에 울릴 수 있는 효과음 수
    private const float VoiceHoldSeconds = 0.3f; // 소리 하나가 "울리는 중"으로 세어지는 최대 시간 - 뒤쪽 여운까지 다 세면 긴 소리 몇 개만으로 한도가 차서 다음 클릭 소리가 통째로 빠짐
    private const float BusyWindow = 0.5f; // 최근 몇 초 동안의 재생 횟수로 "연타 중"을 판단할지
    private const int QuietStart = 4; // 최근 재생 횟수가 이 이상이면 볼륨을 줄이기 시작
    private const int QuietFull = 14; // 이 이상이면 최소 볼륨
    private const float MinBusyVolume = 0.45f; // 연타가 가장 심할 때 볼륨 배율
    private const float PitchJitter = 0.06f; // 피치를 ±이만큼 랜덤으로 흔듦
    private const float BaseVolume = 0.6f; // 오브젝트 타격/부서짐 소리 전체 크기 (설정의 효과음 볼륨에 곱해짐)

    public const string VolumeKey = "SfxVolume"; // 효과음 볼륨을 저장할 PlayerPrefs 키 (메인 메뉴 설정과 공유)

    private static float? _volume; // 효과음 볼륨 캐시 (처음 쓸 때 PlayerPrefs에서 읽음)
    private static readonly Dictionary<AudioClip, float> _lastPlayTime = new Dictionary<AudioClip, float>(); // 소리별 마지막 재생 시각
    private static readonly List<float> _voiceEndTimes = new List<float>(); // 지금 울리고 있는 소리들이 끝나는 시각
    private static readonly Queue<float> _recentPlayTimes = new Queue<float>(); // 최근 재생 시각들 (연타 정도 판단용)
    private static readonly Dictionary<AudioClip, AudioClip> _trimmed = new Dictionary<AudioClip, AudioClip>(); // 원본 소리 -> 앞쪽 무음을 잘라낸 소리 (처음 울릴 때 한 번 만듦)
    private const float SilenceLevel = 0.02f; // 최대 음량의 이 비율보다 작은 앞부분은 무음으로 봄 (생성 사운드 앞에 50~200ms씩 붙어 있어서 늦게 들림)
    private const float KeepBeforeAttack = 0.003f; // 소리가 시작되는 지점 바로 앞은 이만큼(초) 남겨서 첫 타격음이 뭉개지지 않게

    // 플레이를 새로 시작할 때마다 기록을 비움. 이 프로젝트는 플레이 시작 때 스크립트를 다시 불러오지 않게 설정돼 있어서(Enter Play Mode 옵션)
    // 그냥 두면 지난 플레이의 재생 기록이 남는데, 시간은 0부터 다시 세서 그 기록이 "아직 울리는 중"으로 남아 동시 재생 한도를 막아버림 (클릭 소리가 안 나던 원인)
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
    }

    // 앞쪽 무음을 잘라낸 소리. 읽을 수 없는 소리(스트리밍 등)는 원본 그대로
    private static AudioClip Trimmed(AudioClip clip)
    {
        if (_trimmed.TryGetValue(clip, out AudioClip trimmed)) return trimmed;
        trimmed = clip;
        if (clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.LoadAudioData())
        {
            var data = new float[clip.samples * clip.channels]; // 원본 샘플 (채널 섞여 있음)
            if (clip.GetData(data, 0))
            {
                float peak = 0f; // 가장 큰 소리
                foreach (float sample in data) peak = Mathf.Max(peak, Mathf.Abs(sample));
                int first = 0; // 소리가 처음 나는 샘플
                while (first < data.Length && Mathf.Abs(data[first]) < peak * SilenceLevel) first++;
                int startFrame = Mathf.Max(0, first / clip.channels - Mathf.RoundToInt(KeepBeforeAttack * clip.frequency)); // 잘라낼 앞부분 길이(프레임)
                if (startFrame > 0 && startFrame < clip.samples)
                {
                    int frames = clip.samples - startFrame; // 남는 길이(프레임)
                    var rest = new float[frames * clip.channels]; // 남는 샘플
                    System.Array.Copy(data, startFrame * clip.channels, rest, 0, rest.Length);
                    trimmed = AudioClip.Create(clip.name, frames, clip.channels, clip.frequency, false);
                    trimmed.SetData(rest, 0);
                }
            }
        }
        _trimmed[clip] = trimmed;
        return trimmed;
    }

    // 효과음 볼륨 (0~1) - 설정에서 바꾸면 바로 저장됨
    public static float Volume
    {
        get => _volume ??= PlayerPrefs.GetFloat(VolumeKey, 1f);
        set
        {
            _volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, _volume.Value);
        }
    }

    // priority: 파괴 소리처럼 꼭 들려야 하는 소리 - 동시 재생 수 제한을 무시함 (최소 간격은 그대로)
    public static void Play(AudioSource source, AudioClip clip, bool priority = false)
    {
        if (source == null || clip == null) return;

        float now = Time.unscaledTime; // 업그레이드 창 등으로 timeScale이 0이어도 간격 계산이 맞게
        if (_lastPlayTime.TryGetValue(clip, out float last) && now - last < MinIntervalPerClip) return;

        _voiceEndTimes.RemoveAll(end => end <= now);
        if (!priority && _voiceEndTimes.Count >= MaxVoices) return;

        while (_recentPlayTimes.Count > 0 && now - _recentPlayTimes.Peek() > BusyWindow)
            _recentPlayTimes.Dequeue();

        float busy = Mathf.InverseLerp(QuietStart, QuietFull, _recentPlayTimes.Count); // 0(한가) ~ 1(엄청 연타 중)
        source.pitch = 1f + Random.Range(-PitchJitter, PitchJitter);
        source.PlayOneShot(Trimmed(clip), BaseVolume * Volume * Mathf.Lerp(1f, MinBusyVolume, busy));

        _lastPlayTime[clip] = now;
        _voiceEndTimes.Add(now + Mathf.Min(clip.length / source.pitch, VoiceHoldSeconds));
        _recentPlayTimes.Enqueue(now);
    }
}
