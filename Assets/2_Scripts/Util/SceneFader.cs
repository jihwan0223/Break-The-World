using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class SceneFader : MonoBehaviour
{
    private const float FadeOutTime = 0.4f; // 초
    private const float FadeInTime = 0.6f; // 초
    private const int SettleFrames = 3; // 새 씬 초기화 끊김 동안 기다릴 프레임 수

    private VisualElement _cover;

    public static void FadeToScene(string sceneName)
    {
        var go = new GameObject("SceneFader");
        DontDestroyOnLoad(go);
        var document = go.AddComponent<UIDocument>();
        var settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
        settings.sortingOrder = 10000;
        document.panelSettings = settings;
        var fader = go.AddComponent<SceneFader>();
        fader.StartCoroutine(fader.Run(document, sceneName));
    }

    private IEnumerator Run(UIDocument document, string sceneName)
    {
        _cover = new VisualElement();
        _cover.style.position = Position.Absolute;
        _cover.style.left = _cover.style.top = _cover.style.right = _cover.style.bottom = 0;
        _cover.style.backgroundColor = Color.black;
        _cover.style.opacity = 0f;
        _cover.pickingMode = PickingMode.Position;
        document.rootVisualElement.Add(_cover);

        yield return Fade(0f, 1f, FadeOutTime);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        while (!load.isDone) yield return null;
        for (int i = 0; i < SettleFrames; i++) yield return null;

        yield return Fade(1f, 0f, FadeInTime);
        Destroy(document.panelSettings);
        Destroy(gameObject);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f)) // 긴 프레임은 1/30초씩 나눠 진행
        {
            _cover.style.opacity = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        _cover.style.opacity = to;
    }
}
