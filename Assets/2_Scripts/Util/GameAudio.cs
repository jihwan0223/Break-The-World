using UnityEngine;

[CreateAssetMenu(menuName = "Break The World/Game Audio", fileName = "GameAudio")]
public class GameAudio : ScriptableObject
{
    public AudioClip bgm;
    public AudioClip click;

    private static GameAudio _instance;

    public static GameAudio Instance => _instance != null ? _instance : (_instance = Resources.Load<GameAudio>("GameAudio"));
}
