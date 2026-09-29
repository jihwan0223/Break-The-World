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
    private const float BusyWindow = 0.5f; // 최근 몇 초 동안의 재생 횟수로 "연타 중"을 판단할지
    private const int QuietStart = 4; // 최근 재생 횟수가 이 이상이면 볼륨을 줄이기 시작
    private const int QuietFull = 14; // 이 이상이면 최소 볼륨
    private const float MinBusyVolume = 0.45f; // 연타가 가장 심할 때 볼륨 배율
    private const float PitchJitter = 0.06f; // 피치를 ±이만큼 랜덤으로 흔듦

    public const string VolumeKey = "SfxVolume"; // 효과음 볼륨을 저장할 PlayerPrefs 키 (메인 메뉴 설정과 공유)

    private static float? _volume; // 효과음 볼륨 캐시 (처음 쓸 때 PlayerPrefs에서 읽음)
    private static readonly Dictionary<AudioClip, float> _lastPlayTime = new Dictionary<AudioClip, float>(); // 소리별 마지막 재생 시각
    private static readonly List<float> _voiceEndTimes = new List<float>(); // 지금 울리고 있는 소리들이 끝나는 시각
    private static readonly Queue<float> _recentPlayTimes = new Queue<float>(); // 최근 재생 시각들 (연타 정도 판단용)

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
        source.PlayOneShot(clip, Volume * Mathf.Lerp(1f, MinBusyVolume, busy));

        _lastPlayTime[clip] = now;
        _voiceEndTimes.Add(now + clip.length / source.pitch);
        _recentPlayTimes.Enqueue(now);
    }
}
