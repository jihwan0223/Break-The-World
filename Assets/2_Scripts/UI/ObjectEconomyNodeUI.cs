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
    [SerializeField] private Image doneOverlay; // (미사용) 예전엔 해금 완료/최대 레벨이면 켜두던 초록 오버레이 - 지금은 항상 꺼둠

    // 선행(부모) 노드는 이 노드로 들어오는 UpgradeTreeLink로 정함 (선 하나 = 선행 하나). 일반 업그레이드 노드에서 오든
    // 다른 해금/획득 노드에서 오든 상관없음. 아래 anchor 필드는 링크가 없을 때만 쓰는 옛날 방식 폴백.
    [SerializeField] private UpgradeNodeUI anchorMainNode;       // (폴백) 들어오는 링크가 없을 때 선행으로 볼 일반 업그레이드 노드
    [SerializeField] private ObjectEconomyNodeUI anchorEconomyNode; // (폴백) 들어오는 링크가 없을 때 선행으로 볼 해금/획득 노드

    private Button _button;
    private RectTransform _rect;
    private Coroutine _shakeRoutine; // 지금 재생 중인 흔들림 (중첩 방지)
    private bool _wasRevealed; // 직전 Refresh 때 이 노드가 공개 상태였는지 - hidden→revealed로 바뀌는 "등장 순간"을 한 번만 잡으려고 추적
    private bool _refreshedOnce; // Refresh가 최소 한 번 돌았는지 - 첫 호출 때는 등장 흔들림을 재생 안 함(이미 열려있던 노드로 취급)

    private MonoBehaviour _prereqNode;   // 들어오는 링크의 시작 노드 (UpgradeNodeUI 또는 ObjectEconomyNodeUI). null이면 링크 없음
    private bool _prereqResolved;        // _prereqNode를 한 번 찾았는지 (씬의 링크는 안 바뀌니 최초 1회만 탐색)

    public int ObjectIndex => objectIndex;
    public bool IsGain => isGain;
    public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

    // 이 노드를 다른 노드의 anchor로 쓸 때 확인함: Gain 노드는 1레벨 이상 올렸는지, Unlock 노드는 해금 완료됐는지
    public bool IsLeveled() => ObjectManager.Instance != null &&
        (isGain ? ObjectManager.Instance.GetGainLevel(objectIndex) >= 1 : ObjectManager.Instance.IsUnlocked(objectIndex));

    // 선행 노드가 충족됐는지 - 이 노드로 들어오는 UpgradeTreeLink의 시작 노드가 1레벨 이상/해금 완료면 true.
    // 들어오는 링크가 없으면 옛날 anchor 필드로 폴백, 그것도 없으면 루트로 보고 항상 true.
    private bool PrerequisiteSatisfied()
    {
        if (!_prereqResolved)
        {
            _prereqResolved = true;
            foreach (UpgradeTreeLink link in FindObjectsByType<UpgradeTreeLink>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (link.FromNode == null || link.ToNode != Rect) continue;
                _prereqNode = (MonoBehaviour)link.FromNode.GetComponent<UpgradeNodeUI>()
                              ?? link.FromNode.GetComponent<ObjectEconomyNodeUI>();
                break;
            }
        }

        if (_prereqNode is UpgradeNodeUI upgradeNode) return upgradeNode.IsLeveled();
        if (_prereqNode is ObjectEconomyNodeUI economyNode) return economyNode.IsLeveled();

        // 링크 없음 - 옛날 방식 폴백
        if (anchorMainNode != null) return anchorMainNode.IsLeveled();
        if (anchorEconomyNode != null) return anchorEconomyNode.IsLeveled();
        return true; // 선행 자체가 없으면 루트
    }

    void Awake()
    {
        _rect = (RectTransform)transform;
        _button = GetComponent<Button>();
        _button.onClick.AddListener(HandleClicked);

        // 미사용 오버레이 - 클릭을 가로채지 않도록만 처리
        if (doneOverlay != null)
        {
            doneOverlay.raycastTarget = false;
            doneOverlay.gameObject.SetActive(false);
        }
    }

    private void HandleClicked()
    {
        if (ObjectManager.Instance == null) return;

        bool success = isGain ? ObjectManager.Instance.TryUpgradeGain(objectIndex) : ObjectManager.Instance.TryUnlock(objectIndex);
        if (!success) return; // 실패(조각 부족 등) 시 아무 반응 없음 - 일반 업그레이드 노드와 동일

        PlayShake(); // 색 반짝임 없이 흔들림만 (일반 업그레이드 노드와 동일)
        UpgradeTooltip.Instance?.PlayShake(); // 호버로 떠있는 툴팁도 같이 흔들림

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

        // 공개 조건: Unlock 노드는 선행(들어오는 링크의 시작) 노드가 1레벨 이상(또는 해금 완료)이면 나타남.
        // Gain 노드는 자기 오브젝트가 실제로 해금 완료됐을 때 나타남 (선행이 자기 오브젝트라 링크 불필요)
        bool isRevealed = isGain ? ObjectManager.Instance.IsUnlocked(objectIndex) : PrerequisiteSatisfied();

        // hidden→revealed로 처음 바뀌는 순간 + 구매로 인한 새로고침(animateReveal)일 때만 등장 흔들림.
        // 첫 Refresh(_refreshedOnce=false)는 제외 - 페이지를 처음 열 때 이미 공개돼있던 노드는 흔들지 않음
        bool justRevealed = animateReveal && _refreshedOnce && isRevealed && !_wasRevealed;
        _wasRevealed = isRevealed;
        _refreshedOnce = true;

        gameObject.SetActive(isRevealed);
        if (!isRevealed) return;

        if (justRevealed) PlayShake();

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
                doneOverlay.gameObject.SetActive(false); // 해금 완료돼도 초록 오버레이는 안 켬 (요청)
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
                doneOverlay.gameObject.SetActive(false); // 최대 레벨이어도 초록 오버레이는 안 켬 (요청)
        }
    }

    // 구매 성공 / 노드 등장 공용 흔들림 - 색 반짝임 없이 좌우로 살짝 기울었다 돌아옴 (일반 업그레이드 노드와 동일)
    private void PlayShake()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _rect.localRotation = Quaternion.identity;
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        const float duration = 0.25f;            // 흔들림 총 시간(초)
        const float amplitudeDegrees = 7f;       // 최대 각도
        const float oscillations = 1.5f;         // 좌우 왕복 횟수

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;   // 업그레이드 화면이 timeScale 0이라 unscaled
            float progress = Mathf.Clamp01(elapsed / duration);

            float angle = amplitudeDegrees * Mathf.Sin(progress * oscillations * Mathf.PI * 2f) * (1f - progress);
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);

            yield return null;
        }

        _rect.localRotation = Quaternion.identity;
        _shakeRoutine = null;
    }
}
