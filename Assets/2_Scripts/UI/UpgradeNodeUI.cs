using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 업그레이드 트리(메인 트리) 노드 하나. nodeId(UpgradeManager가 템플릿을 펼쳐 만든 노드의 id)만 지정하면
// 나머지(이름/레벨/비용/공개여부/구매)는 UpgradeManager를 조회해서 알아서 처리됨.
// 부모-자식 연결선은 이 스크립트가 아니라 UILineConnector가 따로 담당함 (이 노드의 RectTransform을 참조해서 그림).
[RequireComponent(typeof(Button))]
public class UpgradeNodeUI : MonoBehaviour
{
    [SerializeField] private string nodeId; // 이 노드가 나타내는 UpgradeManager 노드 id ("{템플릿id}#{tier}"). 트리 자동생성 시 Bind로 채워짐
    [SerializeField] private TextMeshProUGUI label; // 이름/레벨/비용을 표시할 텍스트 (3줄: 이름 / N-Max / 비용)
    [SerializeField] private Image flashOverlay; // 구매 성공(초록)/실패(빨강) 시 잠깐 반짝이는 오버레이 - 평소엔 알파 0
    [SerializeField] private Image maxedOverlay; // 최대 레벨이면 계속 켜두는 초록 오버레이 (선택 사항, 없으면 비워둬도 됨)

    private Button _button;
    private RectTransform _rect;
    private Coroutine _flashRoutine; // 지금 재생 중인 반짝임/흔들림 연출 (중첩 재생 방지용)
    private bool _wasRevealed; // 직전 Refresh 때 이 노드가 공개 상태였는지 - hidden→revealed로 바뀌는 "등장 순간"을 한 번만 잡으려고 추적
    private bool _refreshedOnce; // Refresh가 최소 한 번 돌았는지 - 첫 호출 때는 등장 흔들림을 재생 안 함(이미 열려있던 노드로 취급)

    public string NodeId => nodeId; // 트리 자동생성/연결선이 참조
    public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform); // 연결선 스크립트가 참조할 RectTransform

    // 트리 자동생성 시 어떤 업그레이드 노드를 나타내는지 지정 (에디터에서 직접 넣어도 됨)
    public void Bind(string id) => nodeId = id;

    // 이 노드를 anchor로 삼는 다른 노드(ObjectEconomyNodeUI 등)가 "부모가 1레벨 이상인지" 확인할 때 씀
    public bool IsLeveled() => UpgradeManager.Instance != null && UpgradeManager.Instance.GetLevel(nodeId) >= 1;

    void Awake()
    {
        _rect = (RectTransform)transform;
        _button = GetComponent<Button>();
        _button.onClick.AddListener(HandleClicked);

        if (flashOverlay != null)
        {
            flashOverlay.raycastTarget = false; // 클릭을 가로채면 안 됨
            SetOverlayAlpha(flashOverlay, 0f);
        }

        // 최대 레벨일 때 켜지는 오버레이도 클릭을 가로채면 안 됨 (활성화되는 순간 버튼 위를 덮어버리니까)
        if (maxedOverlay != null)
            maxedOverlay.raycastTarget = false;
    }

    private void HandleClicked()
    {
        bool success = UpgradeManager.Instance != null && UpgradeManager.Instance.TryUpgrade(nodeId);

        // 이번 구매로 최대 레벨(N/Max 또는 1/1)까지 다 채웠으면 흔들지 않고 초록 반짝임만 재생
        bool justMaxed = success && UpgradeManager.Instance.GetLevel(nodeId) >= UpgradeManager.Instance.GetMaxLevel(nodeId);
        PlayFlash(success, playShake: !justMaxed);

        UpgradeTreeUI.Instance?.RefreshAll(animateReveals: true); // 방금 해금된 자식 노드들이 나타나도록 트리 전체를 새로고침 (등장 흔들림 재생)
    }

    // 현재 상태(공개 여부/텍스트/최대 레벨 표시)를 갱신 - UpgradeTreeUI가 전체를 새로고침할 때마다 호출함.
    // animateReveal: 구매로 인한 새로고침이면 true - 이 노드가 이번에 처음 공개됐다면 등장 흔들림을 재생함
    public void Refresh(bool animateReveal = false)
    {
        if (UpgradeManager.Instance == null || string.IsNullOrEmpty(nodeId) || UpgradeManager.Instance.GetNode(nodeId) == null)
        {
            gameObject.SetActive(false);
            return;
        }

        bool isRevealed = UpgradeManager.Instance.IsRevealed(nodeId); // 선행 노드 조건을 만족하면 공개됨

        // hidden→revealed로 처음 바뀌는 순간 + 구매로 인한 새로고침(animateReveal)일 때만 등장 흔들림.
        // 첫 Refresh(_refreshedOnce=false)는 제외 - 페이지를 처음 열 때 이미 공개돼있던 노드는 흔들지 않음
        bool justRevealed = animateReveal && _refreshedOnce && isRevealed && !_wasRevealed;
        _wasRevealed = isRevealed;
        _refreshedOnce = true;

        gameObject.SetActive(isRevealed);
        if (!isRevealed) return;

        if (justRevealed) PlayRevealShake();

        int level = UpgradeManager.Instance.GetLevel(nodeId);
        int maxLevel = UpgradeManager.Instance.GetMaxLevel(nodeId);
        bool maxed = level >= maxLevel;

        if (label != null)
        {
            string name = UpgradeManager.Instance.GetDisplayName(nodeId);
            string costLine = maxed ? "최대" : FormatCost(UpgradeManager.Instance.GetNextCost(nodeId));
            label.text = $"{name}\n{level}/{maxLevel}\n{costLine}";
        }

        if (maxedOverlay != null)
            maxedOverlay.gameObject.SetActive(maxed);
    }

    // 조각 비용 배열(여러 종류일 수 있음)을 "{개수} {오브젝트 이름}"을 줄바꿈으로 나열한 문자열로 만듦
    private string FormatCost(PieceCost[] costs)
    {
        if (costs == null || costs.Length == 0) return "";

        var lines = new string[costs.Length];
        for (int i = 0; i < costs.Length; i++)
        {
            string objectName = ObjectManager.Instance != null ? ObjectManager.Instance.GetObjectAt(costs[i].objectIndex).objectName : $"오브젝트 {costs[i].objectIndex}";
            lines[i] = $"{NumberFormatUtil.Format(costs[i].amount)} {objectName}";
        }

        return string.Join("\n", lines);
    }

    private void PlayFlash(bool success, bool playShake)
    {
        if (flashOverlay == null) return;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(FlashRoutine(success, playShake));
    }

    // 성공하면 초록, 실패하면 빨강으로 잠깐 반짝임. 성공 + playShake일 때만 좌우로 살짝 흔들림
    private IEnumerator FlashRoutine(bool success, bool playShake)
    {
        const float duration = 0.2f; // 연출 총 시간(초)
        const float peakAlpha = 0.6f; // 반짝임 최대 밝기
        const float shakeAmplitudeDegrees = 5f; // 흔들림 최대 각도
        const float shakeOscillations = 1.5f; // 연출 시간 동안 좌우로 흔들리는 횟수

        Color color = success ? new Color(0.1f, 1f, 0.1f, peakAlpha) : new Color(1f, 0.1f, 0.1f, peakAlpha);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // 업그레이드 화면이 게임을 멈춰도(Time.timeScale=0) 재생되도록
            float progress = Mathf.Clamp01(elapsed / duration);

            color.a = Mathf.Lerp(peakAlpha, 0f, progress);
            flashOverlay.color = color;

            if (success && playShake)
            {
                float angle = shakeAmplitudeDegrees * Mathf.Sin(progress * shakeOscillations * Mathf.PI * 2f) * (1f - progress);
                _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            yield return null;
        }

        SetOverlayAlpha(flashOverlay, 0f);
        _rect.localRotation = Quaternion.identity;
        _flashRoutine = null;
    }

    // 노드가 새로 공개될 때 재생 - 색 반짝임 없이 좌우로 살짝 기울었다 돌아오는 흔들림만 (구매 성공 때와 같은 느낌)
    private void PlayRevealShake()
    {
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _rect.localRotation = Quaternion.identity;
        _flashRoutine = StartCoroutine(RevealShakeRoutine());
    }

    private IEnumerator RevealShakeRoutine()
    {
        const float duration = 0.25f; // 등장 흔들림 총 시간(초) - 구매(0.2)보다 살짝 길게
        const float shakeAmplitudeDegrees = 5f; // 흔들림 최대 각도 (구매 때와 동일)
        const float shakeOscillations = 1.5f; // 연출 시간 동안 좌우로 흔들리는 횟수

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // 업그레이드 화면이 게임을 멈춰도(Time.timeScale=0) 재생되도록
            float progress = Mathf.Clamp01(elapsed / duration);

            float angle = shakeAmplitudeDegrees * Mathf.Sin(progress * shakeOscillations * Mathf.PI * 2f) * (1f - progress);
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);

            yield return null;
        }

        _rect.localRotation = Quaternion.identity;
        _flashRoutine = null;
    }

    private void SetOverlayAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
