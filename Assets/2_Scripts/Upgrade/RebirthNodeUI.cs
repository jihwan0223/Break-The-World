using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 업그레이드 트리의 환생 칸 (임시: 누르면 노드가 소용돌이로 빨려 들어간 뒤 데모 감사창)
[RequireComponent(typeof(Image), typeof(Button))]
public class RebirthNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private long pieceCost = 100_000_000; // 첫 환생 조각 가격 (100M)
    [SerializeField] private long crystalCost = 1_000_000; // 첫 환생 결정 가격 (1M)
    [SerializeField] private float costGrowth = 5f; // 환생할 때마다 가격 배율
    [SerializeField] private long starShardReward = 1; // 환생할 때 받는 별조각

    [Header("소용돌이 연출")]
    [SerializeField] private float vortexSeconds = 2.6f; // 노드가 다 빨려 들어가는 시간 (초)
    [SerializeField] private float vortexZoom = 0.22f; // 연출 동안 카메라 배율
    [SerializeField] private Vector2 vortexTurns = new Vector2(1.2f, 2.8f); // 노드마다 도는 바퀴 수 (최소, 최대) - 서로 달라서 섞임
    [SerializeField] private int sparkleCount = 140; // 같이 빨려 드는 반짝이 개수
    [SerializeField] private Color readyTint = Color.white; // 누를 수 있을 때 그림 색
    [SerializeField] private Color lockedTint = new Color(0.45f, 0.45f, 0.45f); // 조건이 모자랄 때 그림 색 (어둡게)

    private static readonly Color[] SparkleColors =
    {
        new Color(0.75f, 0.45f, 1f), new Color(0.45f, 0.9f, 1f), new Color(1f, 1f, 1f), new Color(1f, 0.55f, 0.85f),
    }; // 반짝이 색
    private const string MetColor = "8CF5A8"; // 채운 조건 (초록)
    private const string UnmetColor = "FF8A8A"; // 모자란 조건 (빨강)

    public static bool Hovered { get; private set; } // 마우스가 환생 칸 위 (재화 칸에 별조각 보여줌)
    private Image _image; // 환생 그림
    private UpgradeNodeUI[] _upgradeNodes; // 트리의 업그레이드 노드
    private ObjectEconomyNodeUI[] _economyNodes; // 트리의 오브젝트 해금 노드
    private float _nextRefresh; // 다음 조건 갱신 시각 (unscaled)
    private bool _busy; // 빨아들이는 중
    private bool _hovered; // 마우스가 올라와 있음 (툴팁 갱신용)
    private readonly List<(RectTransform rect, Vector3 position, Vector3 scale, Quaternion rotation)> _saved =
        new List<(RectTransform, Vector3, Vector3, Quaternion)>(); // 원래 자리 (부모 기준)
    private readonly List<GameObject> _effects = new List<GameObject>(); // 연출용으로 만든 것 (끝나면 지움)
    private readonly List<GameObject> _hiddenLinks = new List<GameObject>(); // 연출 동안 숨긴 연결선 (섞이면 거미줄처럼 보여서)
    private Vector2 _savedCameraPosition; // 연출 전 카메라 위치
    private float _savedCameraZoom; // 연출 전 카메라 배율

    void Awake()
    {
        _image = GetComponent<Image>();
        GetComponent<Button>().onClick.AddListener(HandleClicked);
        var tree = GetComponentInParent<UpgradeTreeUI>(true); // 노드를 찾을 트리 루트
        Transform root = tree != null ? tree.transform : transform.parent;
        _upgradeNodes = root.GetComponentsInChildren<UpgradeNodeUI>(true);
        _economyNodes = root.GetComponentsInChildren<ObjectEconomyNodeUI>(true);
    }

    void OnEnable() => Refresh();

    void OnDisable()
    {
        // 트리가 중간에 닫히면 노드를 제자리로
        if (_busy) Restore();
        _busy = false;
        _hovered = Hovered = false;
    }

    void Update()
    {
        if (_busy || Time.unscaledTime < _nextRefresh) return;
        Refresh();
    }

    private void Refresh()
    {
        _nextRefresh = Time.unscaledTime + 0.25f;
        _image.color = IsReady() ? readyTint : lockedTint;
        if (_hovered) UpgradeTooltip.Instance?.UpdateContent(BuildTooltipContent());
    }

    // 일반 노드와 같은 툴팁: 이름 / 설명(조건) / 가격
    private UpgradeTooltip.Content BuildTooltipContent()
    {
        long pieceCost = CurrentCost(this.pieceCost); // 이번 환생 조각 가격
        long crystalCost = CurrentCost(this.crystalCost); // 이번 환생 결정 가격
        long pieces = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetPieces() : 0; // 가진 조각
        long crystals = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetCrystals() : 0; // 가진 결정
        string piecesText = Line(Loc.T("{0} 조각", NumberFormatUtil.Format(pieceCost)), pieces >= pieceCost); // 조각 가격
        string crystalsText = Line(Loc.T("{0} 결정", NumberFormatUtil.Format(crystalCost)), crystals >= crystalCost); // 결정 가격
        return new UpgradeTooltip.Content
        {
            title = Loc.T("환생"),
            description = Loc.T("환생 시 별조각 +{0}", NumberFormatUtil.Format(starShardReward)),
            price = $"{piecesText} + {crystalsText}",
        };
    }

    private static string Line(string text, bool met) => $"<color=#{(met ? MetColor : UnmetColor)}>{text}</color>";

    public void OnPointerEnter(PointerEventData eventData)
    {
        Hovered = true;
        if (_busy) return;
        _hovered = true;
        UpgradeTooltip.Instance?.Show(BuildTooltipContent(), (RectTransform)transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = Hovered = false;
        UpgradeTooltip.Instance?.Hide();
    }

    // 환생할 때마다 costGrowth배 (long 넘치지 않게 막음)
    private long CurrentCost(long baseCost) =>
        (long)System.Math.Min(baseCost * System.Math.Pow(costGrowth, GameStats.Rebirths), long.MaxValue / 2);

    private bool IsReady()
    {
        CurrencyManager currency = CurrencyManager.Instance;
        return currency != null && currency.GetPieces() >= CurrentCost(pieceCost) && currency.GetCrystals() >= CurrentCost(crystalCost);
    }

    private void HandleClicked()
    {
        if (_busy) return;
        var cost = new[] { new PieceCost(CurrentCost(pieceCost)), new PieceCost(CurrentCost(crystalCost), true) }; // 이번 환생 비용
        if (!IsReady() || !CurrencyManager.Instance.TrySpendPieces(cost))
        {
            UpgradeTooltip.Instance?.PlayShake(); // 일반 노드처럼 못 사면 툴팁이 흔들림
            return;
        }
        CurrencyManager.Instance.AddStarShards(starShardReward);
        GameStats.Rebirths++;
        _hovered = false;
        UpgradeTooltip.Instance?.Hide();
        StartCoroutine(VortexRoutine());
    }

    // 한 바퀴 도는 대상: 처음 거리/각도, 도는 바퀴 수, 출발 지연
    private struct Orbit
    {
        public float radius; // 처음 거리
        public float angle; // 처음 각도 (라디안)
        public float turns; // 빨려 들 때까지 도는 바퀴 수
        public float delay; // 출발 지연 (초)
    }

    private IEnumerator VortexRoutine()
    {
        _busy = true;
        UpgradeTreeUI tree = UpgradeTreeUI.Instance;
        RectTransform content = tree != null ? tree.Content : (RectTransform)transform.parent;
        _savedCameraPosition = content.anchoredPosition;
        _savedCameraZoom = content.localScale.x;
        Vector2 cameraTo = tree != null ? tree.CenteredAnchoredPosition((RectTransform)transform, vortexZoom) : _savedCameraPosition;

        // 연결선은 숨김
        _hiddenLinks.Clear();
        foreach (UpgradeTreeLink link in content.GetComponentsInChildren<UpgradeTreeLink>(false))
        {
            link.gameObject.SetActive(false);
            _hiddenLinks.Add(link.gameObject);
        }

        // 노드 원래 자리 저장 + 소용돌이 궤도
        _saved.Clear();
        foreach (UpgradeNodeUI node in _upgradeNodes) if (node != null) Save(node.Rect);
        foreach (ObjectEconomyNodeUI node in _economyNodes) if (node != null) Save(node.Rect);
        Vector2 center = content.InverseTransformPoint(transform.position); // 소용돌이 가운데 (판 기준)
        var orbits = new Orbit[_saved.Count]; // 노드별 궤도
        for (int i = 0; i < _saved.Count; i++)
        {
            Vector2 offset = (Vector2)content.InverseTransformPoint(_saved[i].rect.position) - center; // 가운데에서 노드까지
            orbits[i] = new Orbit
            {
                radius = offset.magnitude,
                angle = Mathf.Atan2(offset.y, offset.x),
                turns = Random.Range(vortexTurns.x, vortexTurns.y),
                delay = Random.Range(0f, vortexSeconds * 0.25f),
            };
        }

        // 반짝이: 가운데로 빙글빙글 빨려 드는 작은 네모
        var sparkles = new (RectTransform rect, Orbit orbit, float born, float life)[sparkleCount]; // 반짝이별 궤도
        for (int i = 0; i < sparkles.Length; i++)
            sparkles[i] = (MakeSparkle(content), RandomSparkleOrbit(), Random.Range(-1f, 0f), Random.Range(0.6f, 1.2f));

        Image flash = MakeFlash(tree != null ? (RectTransform)tree.transform : content); // 마지막 번쩍임
        const float cameraSeconds = 0.5f; // 카메라가 소용돌이로 가는 시간

        for (float t = 0f; t < vortexSeconds; t += Time.unscaledDeltaTime)
        {
            // 카메라: 환생 칸을 가운데에 두고 멀리서 봄
            float c = Mathf.SmoothStep(0f, 1f, t / cameraSeconds);
            if (tree != null) tree.SetZoom(Mathf.Lerp(_savedCameraZoom, vortexZoom, c));
            content.anchoredPosition = Vector2.Lerp(_savedCameraPosition, cameraTo, c);

            // 노드: 바퀴 수가 서로 달라 섞이면서 안쪽으로 감김
            for (int i = 0; i < _saved.Count; i++)
            {
                Orbit o = orbits[i];
                float k = Mathf.Clamp01((t - o.delay) / (vortexSeconds - o.delay));
                Place(content, _saved[i].rect, center, o, k);
                _saved[i].rect.localScale = _saved[i].scale * (1f - k * k * k * k);
                _saved[i].rect.localRotation = _saved[i].rotation * Quaternion.Euler(0f, 0f, -k * 720f);
            }

            // 반짝이: 수명이 다하면 바깥에서 다시 출발 (끝나기 직전엔 새로 안 남)
            for (int i = 0; i < sparkles.Length; i++)
            {
                var s = sparkles[i];
                float age = (t - s.born) / s.life;
                if (age >= 1f && t < vortexSeconds - 0.4f)
                {
                    s = (s.rect, RandomSparkleOrbit(), t, Random.Range(0.6f, 1.2f));
                    sparkles[i] = s;
                    age = 0f;
                }
                bool alive = age >= 0f && age < 1f;
                s.rect.gameObject.SetActive(alive);
                if (!alive) continue;
                Place(content, s.rect, center, s.orbit, age);
                s.rect.localScale = Vector3.one * (1f - age);
            }

            // 환생 칸: 점점 세게 떨림
            float shake = t / vortexSeconds; // 떨림 세기
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 40f) * 6f * shake);
            transform.localScale = Vector3.one * (1f + Mathf.Sin(t * 30f) * 0.06f * shake);

            // 마지막 0.4초 동안 화면이 하얗게
            flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01((t - (vortexSeconds - 0.4f)) / 0.4f) * 0.85f);
            yield return null;
        }
        foreach (var saved in _saved) saved.rect.localScale = Vector3.zero;
        flash.color = new Color(1f, 1f, 1f, 0.85f);
        yield return new WaitForSecondsRealtime(0.3f);

        Restore();
        _busy = false;
        SaveManager.Instance?.SaveNow();
        tree?.Close();
        SidePanelUI.ShowDemoComplete(0f); // 임시: 환생 대신 데모 감사창
    }

    // 판 기준 자리로 저장 - 화면 좌표로 저장하면 줌/이동이 바뀐 뒤 되돌릴 때 어긋남
    private void Save(RectTransform rect) => _saved.Add((rect, rect.localPosition, rect.localScale, rect.localRotation));

    // k(0~1)만큼 진행한 소용돌이 자리: 거리는 (1-k)^3으로 줄어 멀리 있던 것도 금방 화면 안으로 오고,
    // 각도는 안쪽으로 갈수록 빨리 돎 (시계 방향)
    private static void Place(RectTransform content, RectTransform rect, Vector2 center, Orbit o, float k)
    {
        float left = 1f - k; // 남은 비율
        float angle = o.angle - o.turns * Mathf.PI * 2f * k * k;
        float radius = o.radius * left * left * left;
        rect.position = content.TransformPoint(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
    }

    private Orbit RandomSparkleOrbit() => new Orbit
    {
        radius = Random.Range(1500f, 5000f),
        angle = Random.Range(0f, Mathf.PI * 2f),
        turns = Random.Range(0.8f, 1.6f),
    };

    private RectTransform MakeSparkle(RectTransform content)
    {
        var go = new GameObject("RebirthSparkle", typeof(RectTransform), typeof(Image)); // 반짝이
        go.transform.SetParent(content, false);
        go.transform.SetSiblingIndex(transform.GetSiblingIndex()); // 환생 칸 뒤로 빨려 들게
        var rect = (RectTransform)go.transform; // 반짝이 칸
        float size = Random.Range(60f, 180f); // 판 기준 크기
        rect.sizeDelta = new Vector2(size, size);
        var image = go.GetComponent<Image>(); // 반짝이 색
        image.color = SparkleColors[Random.Range(0, SparkleColors.Length)];
        image.raycastTarget = false;
        go.SetActive(false);
        _effects.Add(go);
        return rect;
    }

    private Image MakeFlash(RectTransform parent)
    {
        var go = new GameObject("RebirthFlash", typeof(RectTransform), typeof(Image)); // 화면 전체 번쩍임
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();
        var rect = (RectTransform)go.transform; // 화면 전체
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>(); // 하얀 판
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true; // 연출 중엔 트리를 못 누르게
        _effects.Add(go);
        return image;
    }

    private void Restore()
    {
        foreach (var (rect, position, scale, rotation) in _saved)
        {
            if (rect == null) continue;
            rect.localPosition = position;
            rect.localScale = scale;
            rect.localRotation = rotation;
        }
        _saved.Clear();
        foreach (GameObject link in _hiddenLinks) if (link != null) link.SetActive(true);
        _hiddenLinks.Clear();
        foreach (GameObject effect in _effects) if (effect != null) Destroy(effect);
        _effects.Clear();
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        UpgradeTreeUI tree = UpgradeTreeUI.Instance;
        if (tree != null && tree.Content != null)
        {
            tree.SetZoom(_savedCameraZoom);
            tree.Content.anchoredPosition = _savedCameraPosition;
        }
    }
}
