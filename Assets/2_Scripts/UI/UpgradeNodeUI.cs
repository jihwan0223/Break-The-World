using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 업그레이드 트리(메인 트리) 노드 하나. Canvas에 손으로 배치하는 프리팹에 붙여서 씀 -
// upgradeType 하나만 인스펙터에서 지정하면, 나머지(텍스트/공개여부/구매)는 알아서 처리됨.
// 부모-자식 연결선은 이 스크립트가 아니라 UILineConnector가 따로 담당함 (이 노드의 RectTransform을 참조해서 그림).
[RequireComponent(typeof(Button))]
public class UpgradeNodeUI : MonoBehaviour
{
    [SerializeField] private UpgradeManager.UpgradeType upgradeType; // 이 노드가 나타내는 업그레이드 종류
    [SerializeField] private TextMeshProUGUI label; // 이름/레벨/비용을 표시할 텍스트 (3줄: 이름 / N-Max / 비용)
    [SerializeField] private Image flashOverlay; // 구매 성공(초록)/실패(빨강) 시 잠깐 반짝이는 오버레이 - 평소엔 알파 0
    [SerializeField] private Image maxedOverlay; // 최대 레벨이면 계속 켜두는 초록 오버레이 (선택 사항, 없으면 비워둬도 됨)

    private Button _button;
    private RectTransform _rect;
    private Coroutine _flashRoutine; // 지금 재생 중인 반짝임 연출 (중첩 재생 방지용)

    public UpgradeManager.UpgradeType UpgradeType => upgradeType; // UILineConnector 등이 부모 조회에 쓸 수 있게 공개
    public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform); // 연결선 스크립트가 참조할 RectTransform

    // 이 노드를 anchor로 삼는 다른 노드(ObjectEconomyNodeUI 등)가 "부모가 1레벨 이상인지" 확인할 때 씀
    public bool IsLeveled() => UpgradeManager.Instance != null && UpgradeManager.Instance.GetLevel(upgradeType) >= 1;

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
    }

    private void HandleClicked()
    {
        bool success = UpgradeManager.Instance != null && UpgradeManager.Instance.TryUpgrade(upgradeType);

        // 이번 구매로 최대 레벨(N/Max 또는 1/1)까지 다 채웠으면 흔들지 않고 초록 반짝임만 재생
        bool justMaxed = success && UpgradeManager.Instance.GetLevel(upgradeType) >= UpgradeManager.Instance.GetMaxLevel(upgradeType);
        PlayFlash(success, playShake: !justMaxed);

        UpgradeTreeUI.Instance?.RefreshAll(); // 방금 해금된 자식 노드들이 나타나도록 트리 전체를 새로고침
    }

    // 현재 상태(공개 여부/텍스트/최대 레벨 표시)를 갱신 - UpgradeTreeUI가 전체를 새로고침할 때마다 호출함
    public void Refresh()
    {
        if (UpgradeManager.Instance == null)
        {
            gameObject.SetActive(false);
            return;
        }

        bool isRevealed = !UpgradeManager.Instance.IsLocked(upgradeType); // 부모 업그레이드가 1레벨 이상이면 공개됨
        gameObject.SetActive(isRevealed);
        if (!isRevealed) return;

        int level = UpgradeManager.Instance.GetLevel(upgradeType);
        int maxLevel = UpgradeManager.Instance.GetMaxLevel(upgradeType);
        bool maxed = level >= maxLevel;

        if (label != null)
        {
            string name = UpgradeManager.Instance.GetDisplayName(upgradeType);
            string costLine = maxed ? "최대" : FormatCost(UpgradeManager.Instance.GetNextCost(upgradeType));
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

    private void SetOverlayAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
