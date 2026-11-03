using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 업그레이드 트리의 환생 칸 (임시: 누르면 노드가 빨려 들어간 뒤 데모 감사창)
[RequireComponent(typeof(Image), typeof(Button))]
public class RebirthNodeUI : MonoBehaviour
{
    [SerializeField] private long pieceCost = 100_000_000; // 조각 100M
    [SerializeField] private long crystalCost = 1_000_000; // 결정 1M
    [SerializeField] private float suckSeconds = 1.4f; // 노드가 빨려 들어가는 시간 (초)
    [SerializeField] private Color readyColor = new Color(1f, 0.82f, 0.35f); // 누를 수 있을 때
    [SerializeField] private Color lockedColor = new Color(0.55f, 0.55f, 0.55f); // 조건이 모자랄 때

    private Image _image; // 네모 배경
    private TextMeshProUGUI _title; // "환생"
    private TextMeshProUGUI _conditions; // 조건 줄
    private UpgradeNodeUI[] _upgradeNodes; // 트리의 업그레이드 노드
    private ObjectEconomyNodeUI[] _economyNodes; // 트리의 오브젝트 해금 노드
    private float _nextRefresh; // 다음 조건 갱신 시각 (unscaled)
    private bool _busy; // 빨아들이는 중
    private readonly List<(RectTransform rect, Vector3 position, Vector3 scale)> _saved = new List<(RectTransform, Vector3, Vector3)>(); // 원래 자리

    void Awake()
    {
        _image = GetComponent<Image>();
        GetComponent<Button>().onClick.AddListener(HandleClicked);
        var tree = GetComponentInParent<UpgradeTreeUI>(true); // 노드를 찾을 트리 루트
        Transform root = tree != null ? tree.transform : transform.parent;
        _upgradeNodes = root.GetComponentsInChildren<UpgradeNodeUI>(true);
        _economyNodes = root.GetComponentsInChildren<ObjectEconomyNodeUI>(true);
        _title = MakeText("Title", 96f, new Vector2(0f, 0.55f), new Vector2(1f, 1f));
        _conditions = MakeText("Conditions", 44f, new Vector2(0f, 0f), new Vector2(1f, 0.55f));
    }

    private TextMeshProUGUI MakeText(string objectName, float size, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(objectName, typeof(RectTransform)); // 글자 오브젝트
        go.transform.SetParent(transform, false);
        var rect = (RectTransform)go.transform; // 글자 칸
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(24f, 24f);
        rect.offsetMax = new Vector2(-24f, -24f);
        var text = go.AddComponent<TextMeshProUGUI>(); // 글자
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = PixelStyle.Text;
        text.raycastTarget = false;
        return text;
    }

    void OnEnable() => Refresh();

    void OnDisable()
    {
        // 트리가 중간에 닫히면 노드를 제자리로
        if (_busy) Restore();
        _busy = false;
    }

    void Update()
    {
        if (_busy || Time.unscaledTime < _nextRefresh) return;
        Refresh();
    }

    private void Refresh()
    {
        _nextRefresh = Time.unscaledTime + 0.25f;
        CountNodes(out int done, out int total);
        long pieces = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetPieces() : 0; // 가진 조각
        long crystals = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCrystals() : 0; // 가진 결정
        _title.text = Loc.T("환생");
        _conditions.text =
            Line(Loc.T("업그레이드 {0}/{1}", done, total), done >= total) + "\n" +
            Line(Loc.T("조각 {0}", NumberFormatUtil.Format(pieceCost)), pieces >= pieceCost) + "\n" +
            Line(Loc.T("결정 {0}", NumberFormatUtil.Format(crystalCost)), crystals >= crystalCost);
        _image.color = IsReady() ? readyColor : lockedColor;
    }

    // 다 채운 조건은 초록, 아니면 빨강
    private static string Line(string text, bool met) =>
        $"<color=#{ColorUtility.ToHtmlStringRGB(met ? PixelStyle.Good : PixelStyle.Bad)}>{text}</color>";

    // 이번 버전(데모/풀)에 나오는 노드만 셈
    private void CountNodes(out int done, out int total)
    {
        done = total = 0;
        UpgradeManager upgrades = UpgradeManager.Instance;
        if (upgrades != null)
            foreach (UpgradeNodeUI node in _upgradeNodes)
            {
                if (node == null || !upgrades.IsInThisVersion(node.Id)) continue;
                total++;
                if (upgrades.GetLevel(node.Id) >= 1) done++;
            }
        foreach (ObjectEconomyNodeUI node in _economyNodes)
        {
            if (node == null || !node.IsInThisVersion) continue;
            total++;
            if (node.IsLeveled()) done++;
        }
    }

    private bool IsReady()
    {
        CountNodes(out int done, out int total);
        CurrencyManager currency = CurrencyManager.Instance;
        return done >= total && currency != null && currency.GetPieces() >= pieceCost && currency.GetCrystals() >= crystalCost;
    }

    private void HandleClicked()
    {
        if (_busy || !IsReady()) return;
        var cost = new[] { new PieceCost(pieceCost), new PieceCost(crystalCost, true) }; // 환생 비용
        if (!CurrencyManager.Instance.TrySpendPieces(cost)) return;
        StartCoroutine(SuckRoutine());
    }

    private IEnumerator SuckRoutine()
    {
        _busy = true;
        _saved.Clear();
        foreach (UpgradeNodeUI node in _upgradeNodes) if (node != null) _saved.Add((node.Rect, node.Rect.position, node.Rect.localScale));
        foreach (ObjectEconomyNodeUI node in _economyNodes) if (node != null) _saved.Add((node.Rect, node.Rect.position, node.Rect.localScale));

        Vector3 target = transform.position; // 빨려 들어갈 곳
        for (float t = 0f; t < suckSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / suckSeconds;
            k = k * k * k; // 처음엔 천천히, 끝으로 갈수록 빨리
            foreach (var (rect, position, scale) in _saved)
            {
                rect.position = Vector3.Lerp(position, target, k);
                rect.localScale = Vector3.Lerp(scale, Vector3.zero, k);
            }
            yield return null;
        }
        foreach (var (rect, _, _) in _saved) rect.localScale = Vector3.zero;
        yield return new WaitForSecondsRealtime(0.4f);

        Restore();
        _busy = false;
        SaveManager.Instance?.SaveNow();
        UpgradeTreeUI.Instance?.Close();
        SidePanelUI.ShowDemoComplete(0f); // 임시: 환생 대신 데모 감사창
    }

    private void Restore()
    {
        foreach (var (rect, position, scale) in _saved)
        {
            if (rect == null) continue;
            rect.position = position;
            rect.localScale = scale;
        }
        _saved.Clear();
    }
}
