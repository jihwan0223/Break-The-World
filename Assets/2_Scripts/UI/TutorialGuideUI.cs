using UnityEngine;
using UnityEngine.UIElements;

// 처음 플레이 퀘스트 안내: 보스 도전 버튼 자리 아래 카드에 두루마리 아이콘 + 다음 할 일을 보여줌 (책상 부수고 맵2 가면 끝)
// 단계는 매 프레임 상태를 보고 넘어감 (이벤트 구독 없음), 저장은 Step 하나
public class TutorialGuideUI : MonoBehaviour
{
    private static TutorialGuideUI _instance; // 하나만

    public static int Step { get; set; } // 진행 단계 (저장됨). 0 = 시작, Finished = 끝

    private const float Left = 23f; // 조각 상자 / 보스 도전 버튼과 같은 왼쪽 (둘 다 left 20 + unity-button 기본 여백 3px)
    private const float Top = 172f; // 조각 표시 아래 (보스 도전 버튼 자리 142보다 조금 아래)
    private const float BelowButton = 142f + 80f + 12f; // 보스 도전 버튼(142, 높이 80)이 떠 있을 때 그 아래
    private const int FontSize = 36; // px - 도트 폰트 배수 (UiColored는 굵기 없음)
    private const float FinalSeconds = 5f; // 마지막 안내가 떠 있는 시간 (초, 실제 시간)
    private const float PanelAlpha = 0.7f; // 카드 투명도 (보스전 패널과 같음)

    private const int StepBreakEgg = 0; // 달걀 부수기
    private const int StepPick = 1; // 조각 줍기
    private const int StepUpgrade = 2; // 첫 업그레이드
    private const int StepCrystal = 3; // 결정으로 커서 줍기 해금
    private const int StepApple = 4; // 사과 해금
    private const int StepButter = 5; // 버터 해금
    private const int StepBread = 6; // 식빵 해금
    private const int StepDesk = 7; // 책상 부수기 (맵2 해금)
    private const int StepNextMap = 8; // 맵2로 이동
    private const int StepFinal = 9; // 새 맵 보상 안내
    public const int Finished = 10; // 끝

    private static readonly string[] Texts = // 단계별 안내 글 (한국어 키, Localization.csv에 번역)
    {
        "달걀 부수기",
        "조각 줍기",
        "조각 {0}개로 업그레이드",
        "결정으로 커서 줍기 해금",
        "사과 해금",
        "버터 해금",
        "식빵 해금",
        "책상 부수기",
        "다음 맵 가기",
        "새 지역일수록 보상이 커집니다.",
    };

    private const float PopSeconds = 0.25f; // 새 글이 뜰 때 커졌다 돌아오는 시간
    private const float PopScale = 1.12f; // 새 글 커지는 배율
    private const int IconDot = 3; // 아이콘 한 도트 = 3px (16도트 = 48px)

    private VisualElement _panel; // 카드 전체
    private VisualElement _icon; // 두루마리
    private Label _label; // 안내 글
    private Texture2D _scrollTexture; // 두루마리 그림
    private int _shownStep = -1; // 지금 글자로 보여주는 단계
    private float _finalElapsed; // 마지막 안내가 떠 있던 시간
    private float _popElapsed = PopSeconds; // 새 글 연출 진행 시간

    public static void Ensure()
    {
        if (_instance != null) return;
        var uiObject = new GameObject("TutorialGuideUI");
        uiObject.SetActive(false); // 설정을 마친 뒤 켜야 처음부터 제대로 그려짐
        var document = uiObject.AddComponent<UIDocument>();
        PieceUI pieceUI = FindFirstObjectByType<PieceUI>();
        PanelSettings settings = pieceUI != null ? pieceUI.GetComponent<UIDocument>().panelSettings : null; // 조각 표시와 같은 판
        if (settings == null)
        {
            settings = PixelStyle.NewPanelSettings();
            settings.sortingOrder = 100;
        }
        document.panelSettings = settings;
        document.sortingOrder = 9; // 보스 도전 버튼(10) 바로 아래
        _instance = uiObject.AddComponent<TutorialGuideUI>();
        uiObject.SetActive(true);
    }

    void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        root.Clear();
        GameFonts.Apply(root);
        if (GameFonts.UiColored != null)
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(GameFonts.UiColored);
        root.pickingMode = PickingMode.Ignore; // 클릭은 게임으로

        if (_scrollTexture == null) _scrollTexture = PixelTexture(Scroll);

        _panel = new VisualElement(); // 카드: 아이콘 + 글 한 줄
        _panel.style.position = Position.Absolute;
        _panel.style.left = Left;
        _panel.style.top = Top;
        _panel.style.flexDirection = FlexDirection.Row;
        _panel.style.alignItems = Align.Center;
        _panel.style.paddingLeft = 16;
        _panel.style.paddingRight = 22;
        _panel.style.paddingTop = 10;
        _panel.style.paddingBottom = 12;
        _panel.style.transformOrigin = new TransformOrigin(0, 0); // 왼쪽 위 기준으로 커짐
        PixelStyle.Frame(_panel, PixelStyle.Box);
        _panel.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, PanelAlpha);
        _panel.pickingMode = PickingMode.Ignore;

        _icon = new VisualElement();
        _icon.style.width = 16 * IconDot;
        _icon.style.height = 16 * IconDot;
        _icon.style.marginRight = 10;
        _icon.style.backgroundImage = new StyleBackground(_scrollTexture);
        _icon.pickingMode = PickingMode.Ignore;
        _panel.Add(_icon);

        _label = new Label();
        _label.style.fontSize = FontSize;
        _label.style.color = PixelStyle.Text.linear;
        _label.pickingMode = PickingMode.Ignore;
        _panel.Add(_label);

        root.Add(_panel);
        _shownStep = -1;
        Loc.Changed += Redraw;
    }

    void OnDisable()
    {
        Loc.Changed -= Redraw;
    }

    private void Redraw() => _shownStep = -1; // 언어 바뀌면 글자 다시 넣음

    void Update()
    {
        if (_panel == null) return;
        Advance();
        if (Step == StepFinal)
        {
            _finalElapsed += Time.unscaledDeltaTime;
            if (_finalElapsed >= FinalSeconds) Step = Finished;
        }

        bool windowOpen = UIPointerGuard.PopupOpen || Time.timeScale == 0f; // 업그레이드/설정/도감 등 창이 열려 있음 (보스 도전 버튼과 같은 기준)
        bool show = Step >= 0 && Step < Finished && !windowOpen;
        _panel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (!show) return;

        _panel.style.top = BossChallengeUI.ChallengeShown ? BelowButton : Top; // 보스 도전 버튼이 있으면 그 아래로
        _popElapsed += Time.unscaledDeltaTime;
        float pop = Mathf.Lerp(PopScale, 1f, Mathf.Clamp01(_popElapsed / PopSeconds)); // 새 글이 뜰 때 살짝 커졌다 돌아옴
        _panel.style.scale = new Scale(new Vector3(pop, pop, 1f));

        if (_shownStep == Step) return;
        if (_shownStep >= 0) _popElapsed = 0f; // 처음 뜰 때(불러오기 / 언어 바꿈)는 연출 없음
        _shownStep = Step;
        _icon.style.display = Step == StepFinal ? DisplayStyle.None : DisplayStyle.Flex; // 마지막 안내는 할 일이 아니라 아이콘 없음
        _label.text = Step == StepUpgrade ? Loc.T(Texts[Step], NumberFormatUtil.Format(NextCost(UpgradeManager.FirstNodeId))) : Loc.T(Texts[Step]);
    }

    // 이미 한 일은 건너뜀 (불러온 세이브 포함). 뒤 단계를 먼저 해도(예: 커서 줍기 안 사고 사과 해금) 그 다음 단계로 넘어감
    private static void Advance()
    {
        ZoneManager zones = ZoneManager.Instance; // 맵 상태
        ObjectManager objects = ObjectManager.Instance; // 해금 상태
        if (zones == null || objects == null) return;
        if (Step == 0 && zones.UnlockedMaxZone >= 1) { Step = Finished; return; } // 튜토리얼 생기기 전에 맵2를 연 옛 세이브

        for (int s = StepFinal - 1; s >= Step; s--) // 끝낸 단계 중 가장 뒤 단계의 다음으로
            if (StepDone(s, zones, objects)) { Step = s + 1; break; }
    }

    private static bool StepDone(int step, ZoneManager zones, ObjectManager objects)
    {
        switch (step)
        {
            case StepBreakEgg: return GameStats.ObjectsBroken > 0;
            case StepPick: return CurrencyManager.Instance != null && CurrencyManager.Instance.TotalPieces > 0;
            case StepUpgrade: return UpgradeManager.Instance != null && UpgradeManager.Instance.FirstUpgradeBought;
            case StepCrystal: return UpgradeManager.Instance != null && UpgradeManager.Instance.HoverPickupIsUnlocked;
            case StepApple: return objects.IsUnlocked(1);
            case StepButter: return objects.IsUnlocked(2);
            case StepBread: return objects.IsUnlocked(3);
            case StepDesk: return zones.UnlockedMaxZone >= 1;
            case StepNextMap: return zones.CurrentZone >= 1;
            default: return false;
        }
    }

    private static long NextCost(string nodeId)
    {
        PieceCost[] costs = UpgradeManager.Instance != null ? UpgradeManager.Instance.GetNextCost(nodeId) : null; // 다음 레벨 가격
        return costs != null && costs.Length > 0 ? costs[0].amount : 0;
    }

    // --- 16x16 도트 아이콘 (문자 = 색, . = 투명) ---
    private static readonly string[] Scroll = // 두루마리
    {
        "................", "..OOOOOOOOOOO...", ".OLPPPPPPPPPpO..", ".OPPPPPPPPPPpO..", "..OPPOOOOOPpO...", "..OPPPPPPPPpO...",
        "..OPPOOOOPPpO...", "..OPPPPPPPPpO...", "..OPPOOOOOPpO...", "..OPPPPPPPPpO...", "..OPPPPPPPPpO...", ".OLPPPPPPPPPpO..",
        ".OPPPPPPPPPPpO..", "..OOOOOOOOOOO...", "................", "................",
    };

    private static Texture2D PixelTexture(string[] rows)
    {
        const int size = 16; // 도트 수
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, size - 1 - y, PixelColor(rows[y][x])); // 문자열 첫 줄이 위
        texture.Apply();
        return texture;
    }

    private static Color PixelColor(char c)
    {
        switch (c)
        {
            case 'O': return new Color32(58, 42, 32, 255); // 외곽선
            case 'P': return new Color32(245, 230, 200, 255); // 종이
            case 'p': return new Color32(214, 190, 150, 255); // 종이 그늘
            case 'L': return new Color32(255, 250, 235, 255); // 종이 밝은 곳
            default: return Color.clear;
        }
    }
}
