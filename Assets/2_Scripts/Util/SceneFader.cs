using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// 씬 전환 때 화면을 검게 덮었다가(페이드 아웃) 새 씬을 백그라운드로 불러온 뒤 다시 걷어냄(페이드 인).
// 씬이 바뀌어도 살아있도록 DontDestroyOnLoad, 다른 UI보다 위에 그리려고 별도 PanelSettings를 씀.
public class SceneFader : MonoBehaviour
{
    private const float FadeOutTime = 0.4f; // 검게 덮이는 시간(초)
    private const float FadeInTime = 0.6f;  // 새 씬에서 검은 화면이 걷히는 시간(초)
    private const int SettleFrames = 3;     // 새 씬 첫 프레임들의 끊김(초기화)이 지나갈 때까지 검은 채로 기다릴 프레임 수

    private VisualElement _cover; // 화면 전체를 덮는 검은 판

    // sceneName으로 부드럽게 넘어감
    public static void FadeToScene(string sceneName)
    {
        var go = new GameObject("SceneFader"); // 전환 동안만 사는 오브젝트
        DontDestroyOnLoad(go);
        var document = go.AddComponent<UIDocument>(); // 검은 판을 그릴 UI
        var settings = ScriptableObject.CreateInstance<PanelSettings>(); // 다른 UI와 따로 쓰는 화면 설정
        settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
        settings.sortingOrder = 10000; // 모든 UI보다 위
        document.panelSettings = settings;
        var fader = go.AddComponent<SceneFader>(); // 전환 진행기
        fader.StartCoroutine(fader.Run(document, sceneName));
    }

    private IEnumerator Run(UIDocument document, string sceneName)
    {
        _cover = new VisualElement();
        _cover.style.position = Position.Absolute;
        _cover.style.left = _cover.style.top = _cover.style.right = _cover.style.bottom = 0;
        _cover.style.backgroundColor = Color.black;
        _cover.style.opacity = 0f;
        _cover.pickingMode = PickingMode.Position; // 전환 중 클릭 막음
        document.rootVisualElement.Add(_cover);

        yield return Fade(0f, 1f, FadeOutTime);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName); // 백그라운드 로드
        while (!load.isDone) yield return null;
        for (int i = 0; i < SettleFrames; i++) yield return null;

        yield return Fade(1f, 0f, FadeInTime);
        Destroy(document.panelSettings);
        Destroy(gameObject);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f)) // 긴 프레임 하나에 확 건너뛰지 않게 한 번에 최대 1/30초만 진행
        {
            _cover.style.opacity = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        _cover.style.opacity = to;
    }
}
