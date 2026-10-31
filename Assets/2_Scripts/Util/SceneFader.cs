using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// 씬 전환 로딩 화면: 파스텔 배경 + 가운데 "불러오는 중" 리본 + 아래 랜덤 팁
public class SceneFader : MonoBehaviour
{
    private const float FadeOutTime = 0.4f; // 초
    private const float FadeInTime = 0.6f; // 초
    private const float MinShowTime = 2f; // 초 - 팁을 읽을 시간
    private const int SettleFrames = 3; // 새 씬 초기화 끊김 동안 기다릴 프레임 수

    private static readonly string[] Tips =
    {
        "빗자루는 안 보고 있는 맵의 파편도 같이 쓸어 담아요.",
        "자동클릭은 다른 맵을 보고 있어도 계속 부수고 있어요.",
        "맵의 보스를 잡으면 다음 맵이 열려요.",
        "결정은 업그레이드 트리의 하늘색 노드에 쓸 수 있어요.",
        "무기마다 따로 강화하는 업그레이드가 있어요.",
        "ESC를 누르면 설정을 열 수 있어요.",
        "숫자 표시는 설정에서 영어 단위(K, M, B)로 바꿀 수 있어요.",
        "뒤쪽 맵일수록 결정이 더 많이 나와요.",
    };

    private VisualElement _cover;
    private Label _loading;
    private Label _tip;
    private int _tipIndex;
    private VisualElement _barFill; // 진행 바 채움

    public static void FadeToScene(string sceneName)
    {
        var go = new GameObject("SceneFader");
        DontDestroyOnLoad(go);
        var document = go.AddComponent<UIDocument>();
        var settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
        settings.sortingOrder = 10000;
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        document.panelSettings = settings;
        var fader = go.AddComponent<SceneFader>();
        fader.StartCoroutine(fader.Run(document, sceneName));
    }

    private void Build(VisualElement root)
    {
        GameFonts.Apply(root);
        if (GameFonts.UiColored != null)
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiColored);

        _cover = new VisualElement();
        _cover.style.position = Position.Absolute;
        _cover.style.left = _cover.style.top = _cover.style.right = _cover.style.bottom = 0;
        int hour = System.DateTime.Now.Hour; // 컴퓨터 시계 기준 6~18시는 낮
        bool day = hour >= 6 && hour < 18;
        Texture2D background = Resources.Load<Texture2D>(day ? "LoadingDay" : "LoadingNight"); // 도트 하늘 + 왁스 언덕
        if (background != null) background.filterMode = FilterMode.Point;
        _cover.style.backgroundColor = day ? new Color(0.55f, 0.78f, 0.98f) : new Color(0.1f, 0.1f, 0.24f);
        _cover.style.backgroundImage = new StyleBackground(background);
        _cover.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
        _cover.style.alignItems = Align.Center;
        _cover.style.paddingBottom = 180; // 가운데보다 살짝 위
        _cover.style.justifyContent = Justify.Center;
        _cover.style.opacity = 0f;
        _cover.pickingMode = PickingMode.Position;
        root.Add(_cover);

        _loading = new Label("로딩 중");
        _loading.style.fontSize = 48;
        _loading.style.width = 420;
        _loading.style.unityTextAlign = TextAnchor.MiddleCenter;
        _loading.style.paddingTop = 12;
        _loading.style.paddingBottom = 20;
        _loading.style.marginBottom = 24;
        PixelStyle.Frame(_loading, PixelStyle.Ribbon);
        PixelStyle.OutlinedText(_loading, PixelStyle.Ribbon.text);
        _cover.Add(_loading);

        var barSlot = new VisualElement(); // 진행 바 홈
        barSlot.style.width = 900;
        barSlot.style.height = 56;
        barSlot.style.paddingLeft = barSlot.style.paddingRight = 14;
        barSlot.style.paddingTop = 14;
        barSlot.style.paddingBottom = 18;
        PixelStyle.Frame(barSlot, PixelStyle.Slot);
        _cover.Add(barSlot);
        _barFill = new VisualElement();
        _barFill.style.height = Length.Percent(100);
        _barFill.style.width = Length.Percent(0);
        _barFill.style.backgroundColor = new Color(1f, 0.84f, 0.47f);
        barSlot.Add(_barFill);

        var tipBox = new VisualElement();
        tipBox.style.position = Position.Absolute;
        tipBox.style.bottom = 70;
        tipBox.style.left = Length.Percent(50);
        tipBox.style.translate = new Translate(Length.Percent(-50), 0);
        tipBox.style.minWidth = 1100;
        tipBox.style.paddingTop = 34;
        tipBox.style.paddingBottom = 34;
        tipBox.style.paddingLeft = 48;
        tipBox.style.paddingRight = 48;
        tipBox.style.alignItems = Align.Center;
        PixelStyle.Frame(tipBox, PixelStyle.Window, 6f);
        _cover.Add(tipBox);

        var tipTitle = new Label("TIP");
        tipTitle.style.position = Position.Absolute;
        tipTitle.style.top = -30;
        tipTitle.style.left = 30;
        tipTitle.style.fontSize = 32;
        tipTitle.style.paddingLeft = 30;
        tipTitle.style.paddingRight = 30;
        tipTitle.style.paddingTop = 6;
        tipTitle.style.paddingBottom = 12;
        PixelStyle.Frame(tipTitle, PixelStyle.Ribbon);
        PixelStyle.OutlinedText(tipTitle, PixelStyle.Ribbon.text);
        tipBox.Add(tipTitle);

        _tipIndex = Random.Range(0, Tips.Length);
        var tip = _tip = new Label(Tips[_tipIndex]);
        tip.style.fontSize = 36;
        tip.style.color = PixelStyle.Text.linear;
        tip.style.unityTextAlign = TextAnchor.MiddleCenter;
        tipBox.Add(tip);
    }

    private IEnumerator Run(UIDocument document, string sceneName)
    {
        Build(document.rootVisualElement);
        float shownAt = Time.unscaledTime;
        _cover.RegisterCallback<PointerDownEvent>(_ => NextTip());
        StartCoroutine(AnimateDots());

        yield return Fade(0f, 1f, FadeOutTime);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        load.allowSceneActivation = false;
        while (load.progress < 0.9f || Time.unscaledTime - shownAt < MinShowTime)
        {
            float shown = Mathf.Min(load.progress / 0.9f, (Time.unscaledTime - shownAt) / MinShowTime); // 로딩과 최소 시간 중 느린 쪽
            _barFill.style.width = Length.Percent(shown * 100f);
            yield return null;
        }
        _barFill.style.width = Length.Percent(100);
        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;
        for (int i = 0; i < SettleFrames; i++) yield return null;

        yield return Fade(1f, 0f, FadeInTime);
        Destroy(document.panelSettings);
        Destroy(gameObject);
    }

    // 클릭하면 다음 팁 (로딩 시간은 그대로)
    private void NextTip()
    {
        _tipIndex = (_tipIndex + 1) % Tips.Length;
        _tip.text = Tips[_tipIndex];
    }

    private IEnumerator AnimateDots()
    {
        for (int n = 0; ; n = (n + 1) % 4)
        {
            _loading.text = "로딩 중" + new string('.', n);
            yield return new WaitForSecondsRealtime(0.35f);
        }
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
