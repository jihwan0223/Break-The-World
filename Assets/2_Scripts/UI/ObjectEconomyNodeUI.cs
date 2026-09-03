using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 오브젝트 해금(Unlock) 또는 획득량 증가(Gain) 노드 하나. Canvas에 손으로 배치하는 프리팹에 붙여서 씀 -
// objectIndex와 isGain만 인스펙터에서 지정하면 나머지는 알아서 처리됨.
// (예: objectIndex=2, isGain=false면 "2번 오브젝트 해금" 버튼, isGain=true면 "2번 오브젝트 획득량 증가" 버튼)
[RequireComponent(typeof(Button))]
public class ObjectEconomyNodeUI : MonoBehaviour
{
    [SerializeField] private int objectIndex; // 대상 오브젝트 인덱스 (1번부터 - 0번은 처음부터 해금이라 대상 아님)
    [SerializeField] private bool isGain; // false면 해금(Unlock) 노드, true면 획득량 증가(Gain) 노드
    [SerializeField] private TextMeshProUGUI label; // 이름/레벨(또는 완료)/비용을 표시할 텍스트
    [SerializeField] private Image flashOverlay; // 구매 성공(초록)/실패(빨강) 시 잠깐 반짝이는 오버레이 - 평소엔 알파 0
    [SerializeField] private Image doneOverlay; // 해금 완료(또는 획득량 최대 레벨)면 계속 켜두는 초록 오버레이 (선택 사항)

    // 이 노드(Unlock 노드에서만 씀 - Gain 노드는 자기 오브젝트 해금 여부로 공개되니 앵커가 필요 없음)가 매달릴 부모.
    // 둘 중 하나만 인스펙터에서 지정함: 부모가 일반 업그레이드 노드면 anchorMainNode, 다른 오브젝트의
    // 해금/획득량 노드면 anchorEconomyNode. (UILineConnector의 from도 같은 노드의 RectTransform으로 맞춰줄 것)
    [SerializeField] private UpgradeNodeUI anchorMainNode;
    [SerializeField] private ObjectEconomyNodeUI anchorEconomyNode;

    private Button _button;
    private RectTransform _rect;
    private Coroutine _flashRoutine;
    private bool _wasRevealed; // 직전 Refresh 때 이 노드가 공개 상태였는지 - hidden→revealed로 바뀌는 "등장 순간"을 한 번만 잡으려고 추적
    private bool _refreshedOnce; // Refresh가 최소 한 번 돌았는지 - 첫 호출 때는 등장 흔들림을 재생 안 함(이미 열려있던 노드로 취급)

    public int ObjectIndex => objectIndex;
    public bool IsGain => isGain;
    public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

    // 이 노드를 다른 노드의 anchor로 쓸 때 확인함: Gain 노드는 1레벨 이상 올렸는지, Unlock 노드는 해금 완료됐는지
    public bool IsLeveled() => ObjectManager.Instance != null &&
        (isGain ? ObjectManager.Instance.GetGainLevel(objectIndex) >= 1 : ObjectManager.Instance.IsUnlocked(objectIndex));

    void Awake()
    {
        _rect = (RectTransform)transform;
        _button = GetComponent<Button>();
        _button.onClick.AddListener(HandleClicked);

        if (flashOverlay != null)
        {
            flashOverlay.raycastTarget = false;
            SetOverlayAlpha(flashOverlay, 0f);
        }

        // 해금 완료/최대 레벨일 때 켜지는 오버레이도 클릭을 가로채면 안 됨
        if (doneOverlay != null)
            doneOverlay.raycastTarget = false;
    }

    private void HandleClicked()
    {
        if (ObjectManager.Instance == null) return;

        bool success = isGain ? ObjectManager.Instance.TryUpgradeGain(objectIndex) : ObjectManager.Instance.TryUnlock(objectIndex);

        // Unlock은 한 번 사면 그걸로 끝(항상 완료 상태)이고, Gain은 5/5를 찍은 순간이면 흔들지 않고 초록 반짝임만 재생
        bool justMaxed = success && (!isGain || ObjectManager.Instance.GetGainLevel(objectIndex) >= 5);
        PlayFlash(success, playShake: !justMaxed);

        UpgradeTreeUI.Instance?.RefreshAll(animateReveals: true); // 방금 해금된 자식 노드들이 나타나도록 트리 전체를 새로고침 (등장 흔들림 재생)
    }

    // UpgradeTreeUI가 전체를 새로고침할 때마다 호출함.
    // animateReveal: 구매로 인한 새로고침이면 true - 이 노드가 이번에 처음 공개됐다면 등장 흔들림을 재생함
    public void Refresh(bool animateReveal = false)
    {
        if (ObjectManager.Instance == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // 공개 조건: Unlock 노드는 매달린 anchor가 1레벨 이상(또는 해금 완료)이면 나타남.
        // Gain 노드는 자기 오브젝트가 실제로 해금 완료됐을 때 나타남 (부모가 자기 자신뿐이라 anchor 불필요)
        bool anchorLeveled = anchorMainNode != null ? anchorMainNode.IsLeveled() : (anchorEconomyNode != null && anchorEconomyNode.IsLeveled());
        bool isRevealed = isGain ? ObjectManager.Instance.IsUnlocked(objectIndex) : anchorLeveled;

        // hidden→revealed로 처음 바뀌는 순간 + 구매로 인한 새로고침(animateReveal)일 때만 등장 흔들림.
        // 첫 Refresh(_refreshedOnce=false)는 제외 - 페이지를 처음 열 때 이미 공개돼있던 노드는 흔들지 않음
        bool justRevealed = animateReveal && _refreshedOnce && isRevealed && !_wasRevealed;
        _wasRevealed = isRevealed;
        _refreshedOnce = true;

        gameObject.SetActive(isRevealed);
        if (!isRevealed) return;

        if (justRevealed) PlayRevealShake();

        string objectName = ObjectManager.Instance.GetObjectAt(objectIndex).objectName;

        if (!isGain)
        {
            bool unlocked = ObjectManager.Instance.IsUnlocked(objectIndex);

            if (label != null)
            {
                if (unlocked)
                {
                    label.text = $"해금\n{objectName}\n완료";
                }
                else
                {
                    long cost = ObjectManager.Instance.GetUnlockCost(objectIndex);
                    string prevName = ObjectManager.Instance.GetObjectAt(objectIndex - 1).objectName;
                    label.text = $"해금\n{objectName}\n{NumberFormatUtil.Format(cost)} {prevName}";
                }
            }

            if (doneOverlay != null)
                doneOverlay.gameObject.SetActive(unlocked);
        }
        else
        {
            int level = ObjectManager.Instance.GetGainLevel(objectIndex);
            bool maxed = level >= 5;

            if (label != null)
            {
                string costLine = maxed ? "최대" : $"{NumberFormatUtil.Format(ObjectManager.Instance.GetNextGainCost(objectIndex))} {ObjectManager.Instance.GetObjectAt(objectIndex - 1).objectName}";
                label.text = $"{objectName} 획득량\n{level}/5\n{costLine}";
            }

            if (doneOverlay != null)
                doneOverlay.gameObject.SetActive(maxed);
        }
    }

    private void PlayFlash(bool success, bool playShake)
    {
        if (flashOverlay == null) return;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(FlashRoutine(success, playShake));
    }

    private IEnumerator FlashRoutine(bool success, bool playShake)
    {
        const float duration = 0.2f;
        const float peakAlpha = 0.6f;
        const float shakeAmplitudeDegrees = 5f;
        const float shakeOscillations = 1.5f;

        Color color = success ? new Color(0.1f, 1f, 0.1f, peakAlpha) : new Color(1f, 0.1f, 0.1f, peakAlpha);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
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
