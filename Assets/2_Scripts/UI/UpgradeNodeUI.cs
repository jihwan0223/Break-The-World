using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 업그레이드 트리 노드 하나. 이름/설명/횟수/효과/비용을 전부 이 컴포넌트에서 직접 인스펙터로 설정함
// (템플릿/CSV 없음). 선행조건은 별도 필드가 아니라, 이 노드로 들어오는 UpgradeTreeLink의 시작 노드로
// UpgradeManager가 자동으로 판단함 - 선 하나 그으면 그게 곧 선행 설정이 됨.
// id는 이 오브젝트의 이름을 그대로 씀 (세이브 키로도 쓰이니 씬 안에서 겹치지 않게 할 것).
[RequireComponent(typeof(Button))]
public class UpgradeNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("업그레이드 설정")]
    [SerializeField] private string displayName;                   // 표시용 이름 (지금은 노드 자체엔 안 그림, 참고용)
    [TextArea] [SerializeField] private string description;        // 호버 툴팁에 뜨는 설명. 예: "공격력이 +1 증가합니다."
    [SerializeField] private UpgradeManager.UpgradeEffect effect;   // 이 업그레이드가 건드리는 수치
    [Tooltip("Click Damage 효과 전용 - 특정 무기 전용이면 그 무기, 전역이면 \"전체 (전역)\"")]
    [ObjectNameField(ObjectNameFieldSource.Weapon)] [SerializeField] private string targetWeaponName;
    [Tooltip("AutoClick 계열(해금/속도/횟수) 효과 전용 - 특정 오브젝트 장착 중일 때만 작동, 전역이면 \"전체 (전역)\"")]
    [ObjectNameField] [SerializeField] private string targetObjectName;
    [Min(1)] [SerializeField] private int maxLevel = 5;             // 업그레이드 가능 횟수
    [SerializeField] private float[] valuePerLevel = { 1f };        // 레벨별 효과값 (배열이 짧으면 마지막 값 반복)

    [Tooltip("이 업그레이드 한 번 올리는 데 드는 조각들. 여러 종류를 동시에 요구할 수 있음 (전부 있어야 구매됨)")]
    [SerializeField] private NodeCost[] costs = { new NodeCost() };

    // 조각 비용 한 줄 = 조각 한 종류 + 레벨별 증가 공식. 노드 하나가 여러 줄을 가질 수 있음
    [Serializable]
    public class NodeCost
    {
        [Tooltip("비우면 Target Object Name 걸로 따라감, 그것도 비었으면 0번 오브젝트 조각")]
        [ObjectNameField(emptyOptionLabel: "비움 (대상 오브젝트 따라감)")]
        public string objectName;
        [Min(1)] public long baseAmount = 10;   // 0->1레벨 비용
        public float levelGrowth = 2f;          // 레벨 오를 때마다 이 조각 요구량에 곱해지는 배율
    }

    private Button _button;
    private RectTransform _rect;
    private Coroutine _shakeRoutine; // 지금 재생 중인 흔들림 (중첩 방지)
    private bool _wasRevealed;   // 직전 Refresh 때 공개 상태였는지 - 등장 순간 1회 감지용
    private bool _refreshedOnce; // Refresh가 최소 한 번 돌았는지 - 첫 호출 땐 등장 흔들림 안 함

    public string Id => name; // 세이브/트리 조회 키 - 오브젝트 이름을 그대로 씀
    public string DisplayName => displayName;
    public string Description => description;
    public UpgradeManager.UpgradeEffect Effect => effect;
    public string TargetWeaponName => targetWeaponName;
    public string TargetObjectName => targetObjectName;
    public int MaxLevel => maxLevel;
    public float[] ValuePerLevel => valuePerLevel;
    public NodeCost[] Costs => costs;
    public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

    // ObjectEconomyNodeUI 등이 "부모가 1레벨 이상인지" 확인할 때 씀
    public bool IsLeveled() => UpgradeManager.Instance != null && UpgradeManager.Instance.GetLevel(Id) >= 1;

    void Awake()
    {
        _rect = (RectTransform)transform;
        _button = GetComponent<Button>();
        _button.onClick.AddListener(HandleClicked);
    }

    private void HandleClicked()
    {
        // 성공하면 대각선 흔들림만. 실패(최대레벨/조각부족/미공개)면 아무 반응 없음 (툴팁은 호버로 이미 떠있음)
        if (UpgradeManager.Instance != null && UpgradeManager.Instance.TryUpgrade(Id))
        {
            PlayShake();
            UpgradeTreeUI.Instance?.RefreshAll(animateReveals: true);
        }
    }

    // 공개 여부만 갱신 (노드 자체엔 표시할 게 없음). UpgradeTreeUI가 새로고침할 때마다 호출.
    // animateReveal: 구매로 인한 새로고침이면 true - 이번에 처음 공개됐으면 등장 흔들림 재생
    public void Refresh(bool animateReveal = false)
    {
        if (UpgradeManager.Instance == null || UpgradeManager.Instance.GetNode(Id) == null)
        {
            gameObject.SetActive(false);
            return;
        }

        bool isRevealed = UpgradeManager.Instance.IsRevealed(Id);

        bool justRevealed = animateReveal && _refreshedOnce && isRevealed && !_wasRevealed;
        _wasRevealed = isRevealed;
        _refreshedOnce = true;

        gameObject.SetActive(isRevealed);
        if (isRevealed && justRevealed) PlayShake();
    }

    // ---- 호버 툴팁 ----

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UpgradeManager.Instance == null) return;

        int level = UpgradeManager.Instance.GetLevel(Id);
        UpgradeTooltip.Instance?.Show($"{description}\n{level}/{maxLevel}", _rect);
    }

    public void OnPointerExit(PointerEventData eventData) => UpgradeTooltip.Instance?.Hide();

    // ---- 대각선 흔들림 (구매 성공 / 등장 공용) ----

    private void PlayShake()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _rect.localRotation = Quaternion.identity;
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        const float duration = 0.22f;            // 흔들림 총 시간(초)
        const float amplitudeDegrees = 5f;       // 최대 각도
        const float oscillations = 1.5f;         // 좌우 왕복 횟수

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;   // 업그레이드 화면이 timeScale 0이라 unscaled
            float p = Mathf.Clamp01(elapsed / duration);
            float angle = amplitudeDegrees * Mathf.Sin(p * oscillations * Mathf.PI * 2f) * (1f - p);
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        _rect.localRotation = Quaternion.identity;
        _shakeRoutine = null;
    }
}
