using UnityEngine;

// 어느 씬에서든 쓰는 공용 소리 묶음 (배경음악, 버튼 클릭). Resources/GameAudio 에셋 하나에 연결해 두고 불러 씀
[CreateAssetMenu(menuName = "Break The World/Game Audio", fileName = "GameAudio")]
public class GameAudio : ScriptableObject
{
    public AudioClip bgm; // 배경음악 (반복 재생)
    public AudioClip click; // 메뉴/버튼 클릭 소리

    private static GameAudio _instance; // 처음 쓸 때 한 번 불러와 캐시

    public static GameAudio Instance => _instance != null ? _instance : (_instance = Resources.Load<GameAudio>("GameAudio"));
}
