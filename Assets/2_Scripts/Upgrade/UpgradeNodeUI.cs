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
    [Tooltip("무기 대상 효과(클릭 데미지 / 무기 처치 보너스)에서 씀. 특정 무기 전용이면 그 무기, 전역이면 \"전체 (전역)\"")]
    [ObjectNameField(ObjectNameFieldSource.Weapon)] [SerializeField] private string targetWeaponName;
    [Tooltip("오브젝트 대상 효과(자동클릭 계열 / 오브젝트 조각 획득 / 2배 드랍 확률)에서 씀. 그 오브젝트 장착 중일 때만 작동")]
    [ObjectNameField] [SerializeField] private string targetObjectName;
    [Min(1)] [SerializeField] private int maxLevel = 5;             // 업그레이드 가능 횟수
    [SerializeField] private float[] valuePerLevel = { 1f };        // 레벨별 효과값 (배열이 짧으면 마지막 값 반복)

    [Tooltip("이 업그레이드 한 번 올리는 데 드는 비용들. 조각과 결정을 동시에 요구할 수 있음 (전부 있어야 구매됨)")]
    [SerializeField] private NodeCost[] costs = { new NodeCost() };

    [Tooltip("1레벨 이상 찍으면 바뀔 스프라이트 (비워두면 기본 이미지 그대로 유지)")]
    [SerializeField] private Sprite upgradedSprite;
    [Tooltip("이 오브젝트를 해금해야 열리는 노드 (비우면 안 씀). 그 전엔 자물쇠 그림 + 제목/설명 ???, 해금하는 순간 트리 카메라가 와서 자물쇠가 깨지며 열림. 열리면 대상 오브젝트가 아직 없어도 살 수 있음")]
    [ObjectNameField] [SerializeField] private string gateObjectName;
    [SerializeField] private Sprite lockedSprite; // 열리기 전 자물쇠 그림

    // 비용 한 줄 = 조각 또는 결정 + 레벨별 증가 공식. 노드 하나가 여러 줄을 가질 수 있음
    [Serializable]
    public class NodeCost
    {
        [Tooltip("체크하면 조각 대신 결정으로 결제")]
        public bool useCrystals;
        [Min(1)] public long baseAmount = 10;   // 0->1레벨 비용
        public float levelGrowth = 2f;          // 레벨 오를 때마다 요구량에 곱해지는 배율
        [Tooltip("0보다 크면 0->1레벨 비용만 이 값으로 (1레벨만 싸게). 2레벨부터는 위 공식 그대로")]
        [Min(0)] public long firstLevelAmount;  // 첫 레벨만 따로 정한 비용 (0이면 안 씀)
        [Tooltip("비어있지 않으면 위 공식 대신 레벨별 비용을 그대로 씀 (0->1, 1->2, ... 순서, 짧으면 마지막 값 반복)")]
        public long[] levelAmounts = System.Array.Empty<long>(); // 레벨별로 직접 정한 비용
    }

    private Button _button;
    private Image _icon;
    private Sprite _baseSprite; // 원래(0레벨) 스프라이트 - Awake 시점에 저장해둠
    private RectTransform _rect;
    private Coroutine _shakeRoutine; // 지금 재생 중인 흔들림 (중첩 방지)
    private Coroutine _denyRoutine; // 지금 재생 중인 "못 삼" 흔들림 (중첩 방지)
    private Vector2 _denyBasePivot; // "못 삼" 흔들림 전 원래 pivot
    private bool _wasRevealed;   // 직전 Refresh 때 공개 상태였는지 - 등장 순간 1회 감지용
    private bool _refreshedOnce; // Refresh가 최소 한 번 돌았는지 - 첫 호출 땐 등장 흔들림 안 함
    private GameObject _badge;   // 지금 재화로 살 수 있을 때 오른쪽 위에 뜨는 느낌표 (처음 필요할 때 만듦)

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
        CacheIcon();
    }

    // 아이콘과 원래(0레벨) 스프라이트를 처음 한 번만 잡아둠. 트리를 처음 열 때는 패널 OnEnable의 RefreshAll이
    // 이 노드의 Awake보다 먼저 불려서, UpdateIcon에서도 불러야 저장된 레벨의 그림이 바로 반영됨
    private void CacheIcon()
    {
        if (_icon != null) return;
        _icon = GetComponent<Image>();
        if (_icon != null) _baseSprite = _icon.sprite;
    }

    // 최대 레벨(만렙)이면 upgradedSprite(테두리 색 바뀐 그림)로, 아니면 원래 스프라이트로. 관문이 닫혀 있으면 자물쇠
    private void UpdateIcon(int level)
    {
        CacheIcon();
        if (_icon == null) return;
        if (lockedSprite != null && (IsGateClosed || _gateRevealPending)) { _icon.sprite = lockedSprite; return; }
        if (upgradedSprite == null) { _icon.sprite = _baseSprite; return; }
        _icon.sprite = level >= maxLevel ? upgradedSprite : _baseSprite;
    }

    private void RefreshIconAfterGate() => UpdateIcon(UpgradeManager.Instance != null ? UpgradeManager.Instance.GetLevel(Id) : 0);

    // 관문 오브젝트(gateObjectName)를 아직 해금 못해서 자물쇠로 가려져 있는지
    public bool IsGateClosed => !string.IsNullOrEmpty(gateObjectName) && ObjectManager.Instance != null
        && !ObjectManager.Instance.IsUnlocked(ObjectManager.StaticIndexOfName(gateObjectName));
    public string GateObjectName => gateObjectName;
    private bool _gateRevealPending; // 카메라가 오는 동안은 자물쇠 그림을 유지

    // 관문이 열려 있는데 아직 열리는 연출을 안 봤으면(보스를 잡은 뒤 처음 창을 열 때) 트리 카메라를 이 노드로 옮긴 뒤 자물쇠가 깨지며 열림.
    // 트리 창이 켜져 있을 때만 - 꺼진 채로 불리면 연출을 아껴뒀다가 다음에 창을 열 때 보여줌
    private void CheckGateOpened()
    {
        if (string.IsNullOrEmpty(gateObjectName) || IsGateClosed) return;
        UpgradeTreeUI tree = UpgradeTreeUI.Instance; // 연출을 보여줄 트리 창
        if (tree == null || !tree.isActiveAndEnabled || !GateReveals.TryMarkSeen(name)) return;
        if (SaveManager.Instance != null) SaveManager.Instance.MarkDirty(); // 본 연출은 세이브에 남김 (다른 변경 없이 꺼도 다시 안 나오게)
        _gateRevealPending = true;
        tree.PlayGateReveal(Rect, FinishGateReveal);
    }

    // 카메라가 도착한 순간: 자물쇠 -> 원래 그림, 흔들리며 크게 터짐
    private void FinishGateReveal()
    {
        _gateRevealPending = false;
        RefreshIconAfterGate();
        if (!isActiveAndEnabled) return; // 카메라가 오는 중에 창을 닫음 - 꺼진 노드에선 코루틴/연출을 못 돌림
        PlayShake();
        NodePurchaseEffect.Play(Rect, NodePurchaseEffect.NormalColor, true);
    }

    // 이 노드가 아직 해금 안 된 오브젝트/무기 대상이라 툴팁을 "???"로 가려야 하는지 - 그걸 해금하면 원래 설명으로 돌아옴
    private bool IsMasked() => UpgradeManager.Instance != null && UpgradeManager.Instance.IsTargetLocked(Id);

    private void HandleClicked()
    {
        // 성공하면 대각선 흔들림 + 구매 이펙트. 실패(최대레벨/조각부족)면 좌우로 짧게만 흔들림 (툴팁은 호버로 이미 떠있음)
        if (UpgradeManager.Instance == null) return;
        if (!UpgradeManager.Instance.TryUpgrade(Id))
        {
            PlayDenyShake();
            return;
        }
        UpgradeTooltip.Instance?.UpdateContent(BuildTooltipContent()); // 마우스 계속 대고 있어도 레벨 숫자 즉시 갱신
        PlayShake();
        bool crystal = Array.Exists(costs, cost => cost.useCrystals); // 결정으로 사는 노드면 연출 색이 다름
        bool maxed = UpgradeManager.Instance.GetLevel(Id) >= maxLevel; // 방금 최대 레벨을 찍었으면 크게 터짐
        NodePurchaseEffect.Play(_rect, crystal ? NodePurchaseEffect.CrystalColor : NodePurchaseEffect.NormalColor, maxed);
        UpgradeTooltip.Instance?.PlayShake(); // 호버로 떠있는 툴팁도 같이 흔들림
        UpgradeTreeUI.Instance?.RefreshAll(animateReveals: true);
    }

    // 툴팁에 보여줄 내용 - 제목/설명/레벨/가격 4줄
    private UpgradeTooltip.Content BuildTooltipContent()
    {
        int level = UpgradeManager.Instance != null ? UpgradeManager.Instance.GetLevel(Id) : 0;

        return new UpgradeTooltip.Content
        {
            title = IsMasked() ? "???" : displayName,
            // 수치가 늘어나는 효과는 "무엇이 +n 증가합니다"로 자동 작성, 해금류는 인스펙터 설명 그대로
            description = IsMasked() ? UpgradeManager.Instance.LockedTargetHintText(Id) : AutoDescription(level) ?? description.Replace("{n}", CurrentSecondsText()),
            level = $"{level} / {maxLevel}",
            price = IsMasked() ? "" : NextCostText(level), // 가려진 노드는 가격도 숨김
        };
    }

    // 설명 문구의 {n} 자리에 넣을 지금 시간(초) - 자동클릭/자동 줍기처럼 시간이 있는 효과만. 업그레이드로 줄어든 현재 값
    private string CurrentSecondsText()
    {
        UpgradeManager upgrades = UpgradeManager.Instance; // 현재 수치 조회
        int objectIndex = ObjectManager.StaticIndexOfName(targetObjectName); // 대상 오브젝트 (자동클릭/존 자동 파괴)
        float seconds; // 표시할 시간(초)
        switch (effect)
        {
            case UpgradeManager.UpgradeEffect.AutoClickUnlock:
            case UpgradeManager.UpgradeEffect.AutoClickSpeed: seconds = upgrades.AutoClickIntervalSecondsFor(objectIndex); break;
            case UpgradeManager.UpgradeEffect.AutoSweepUnlock:
            case UpgradeManager.UpgradeEffect.AutoSweepSpeed: seconds = upgrades.AutoSweepIntervalSeconds; break;
            case UpgradeManager.UpgradeEffect.AutoMineUnlock:
            case UpgradeManager.UpgradeEffect.AutoMineSpeed: seconds = upgrades.AutoMineIntervalSeconds; break;
            case UpgradeManager.UpgradeEffect.HoverClickUnlock: seconds = upgrades.HoverClickIntervalSeconds; break;
            case UpgradeManager.UpgradeEffect.ZoneAutoBreakUnlock: seconds = ZoneBreaker.AutoBreakIntervalFor(objectIndex); break;
            default: return "?";
        }
        return seconds < 0f ? "?" : seconds.ToString("0.#");
    }

    // 수치가 늘어나는 효과의 설명을 "무엇이 +n 증가합니다" 형태로 만듦. n은 다음 레벨에서 늘어나는 값
    // (최대 레벨이면 마지막 레벨 값). 해금류(on/off라 수치 의미 없음)는 null -> 인스펙터 설명을 그대로 씀
    private string AutoDescription(int level)
    {
        if (valuePerLevel == null || valuePerLevel.Length == 0) return null; // 값 배열 없으면 수치를 모름
        float value = valuePerLevel[Mathf.Clamp(level, 0, valuePerLevel.Length - 1)]; // 다음 레벨에서 추가되는 원값 (UpgradeManager.ValueAtLevel(level+1)과 같은 인덱싱)
        string n = Round(value); // 반올림한 수치 문구
        string obj = KoreanJosa.EulReul(targetObjectName); // "사과를" - 대상 오브젝트 효과용
        string weapon = targetWeaponName; // 대상 무기 이름 (비면 전역)

        switch (effect)
        {
            case UpgradeManager.UpgradeEffect.ClickDamage:
            {
                int weaponIndex = WeaponManager.StaticIndexOfWeaponName(weapon); // 이 노드가 강화하는 무기 (전역이면 -1)
                if (weaponIndex < 0 && WeaponManager.Instance != null)
                    weaponIndex = WeaponManager.Instance.EquippedIndex; // 전역 강화는 지금 장착한 무기 기준으로 보여줌
                int scale = ObjectHealthCalculator.ScaleOfTier(weaponIndex + 1); // 그 무기 티어의 데미지 배율
                string amount = NumberFormatUtil.Format(Mathf.RoundToInt(value * scale)); // 실제로 붙는 데미지(배율 적용 후)
                return string.IsNullOrEmpty(weapon) ? $"모든 무기의 데미지가 +{amount} 증가합니다." : $"{weapon}의 데미지가 +{amount} 증가합니다.";
            }
            case UpgradeManager.UpgradeEffect.CritChance: return $"치명타 확률이 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.CritDamage: return $"치명타 데미지가 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.LuckyClickChanceUp: return $"슈퍼 크리티컬 확률이 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.DoubleClickChanceUp: return $"더블 클릭 확률이 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.PieceGainGlobal: return $"모든 오브젝트를 부술 때 얻는 조각이 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.PieceGainObject:
            {
                // %가 아니라 실제로 늘어나는 조각 수로 (기본 보상의 value%, 최소 1개)
                int objectIndex = ObjectManager.StaticIndexOfName(targetObjectName); // 이 노드가 강화하는 오브젝트
                if (objectIndex < 0 || ObjectManager.Instance == null) return $"{obj} 부술 때 얻는 조각이 +{n}% 증가합니다.";
                double pieces = UpgradeManager.ObjectPieceBonusPerLevel(ObjectManager.Instance.GetBaseReward(objectIndex), value); // 늘어나는 조각 수
                return $"{obj} 부술 때 얻는 조각이 +{NumberFormatUtil.Format((long)Math.Round(pieces))}개 증가합니다.";
            }
            case UpgradeManager.UpgradeEffect.ObjectDoubleDrop: return $"{obj} 부술 때 조각이 2배로 나올 확률이 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.WeaponKillBonus: return $"{KoreanJosa.EuroRo(weapon)} 부술 때 조각을 +{n}개 더 얻습니다.";
            case UpgradeManager.UpgradeEffect.CrystalChance: return $"오브젝트를 부술 때 결정이 나올 확률이 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.CrystalAmount: return $"결정이 나올 때 개수가 {1L << Mathf.Clamp(Mathf.RoundToInt(value), 0, 20)}배가 됩니다.";
            case UpgradeManager.UpgradeEffect.GroundPieceCap: return $"바닥에 쌓일 수 있는 조각 개수가 +{n}개 증가합니다.";
            case UpgradeManager.UpgradeEffect.AutoClickSpeed: return $"{targetObjectName} 자동 클릭 간격이 {n}초 줄어듭니다.";
            case UpgradeManager.UpgradeEffect.AutoClickCount: return $"{targetObjectName} 자동 클릭 한 번에 때리는 횟수가 +{n}회 증가합니다.";
            case UpgradeManager.UpgradeEffect.AutoMineSpeed: return $"자동 채굴 간격이 {n}초 줄어듭니다.";
            case UpgradeManager.UpgradeEffect.AutoMineYield: return $"자동 채굴 한 번에 얻는 조각이 +{n}배 증가합니다.";
            case UpgradeManager.UpgradeEffect.AutoSweepSpeed: return $"자동 줍기 간격이 {n}초 줄어듭니다.";
            case UpgradeManager.UpgradeEffect.AutoSweepLayers: return $"자동 줍기가 한 번에 쓸어 담는 조각 층이 +{n}층 증가합니다.";
            case UpgradeManager.UpgradeEffect.HoverPickupLayers: return $"커서 줍기가 한 번에 줍는 조각 층이 +{n}층 증가합니다.";
            case UpgradeManager.UpgradeEffect.CursorRange: return "커서 범위가 더 증가합니다."; // 월드 단위 숫자는 감이 안 와서 수치 없이
            case UpgradeManager.UpgradeEffect.ObjectExtraCopies:
                return $"맵에 {KoreanJosa.IGa(targetObjectName)} +{n}개 더 놓입니다. 동시에 여러 개를 부술 수 있어 조각을 더 빨리 모읍니다.";
            default: return null; // 해금류
        }
    }

    // 툴팁 수치 반올림 - 1 이상은 정수, 1 미만은 소수 첫째 자리 (0.09 -> 0.1). 그래도 0이 되면 둘째 자리까지
    private static string Round(float value)
    {
        float abs = Mathf.Abs(value); // 크기
        if (abs >= 1f) return Mathf.RoundToInt(abs).ToString();
        string oneDecimal = abs.ToString("0.#"); // 소수 첫째 자리
        return oneDecimal != "0" ? oneDecimal : abs.ToString("0.##");
    }

    // 다음 레벨 비용을 "123K 조각 + 5 결정" 형태 문구로. 최대 레벨이면 "최대"
    private string NextCostText(int level)
    {
        if (level >= maxLevel) return "최대";

        PieceCost[] cost = UpgradeManager.Instance?.GetNextCost(Id);
        if (cost == null || cost.Length == 0) return "";

        long pieces = 0; // 조각 비용 합
        long crystals = 0; // 결정 비용 합
        foreach (PieceCost c in cost)
        {
            if (c.isCrystal) crystals += c.amount;
            else pieces += c.amount;
        }

        string piecesText = pieces > 0 ? $"{NumberFormatUtil.Format(pieces)} 조각" : "";
        string crystalsText = crystals > 0 ? $"{NumberFormatUtil.Format(crystals)} 결정" : "";
        return pieces > 0 && crystals > 0 ? $"{piecesText} + {crystalsText}" : piecesText + crystalsText;
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
        if (!isRevealed) return; // 안 보이는 노드는 열리는 연출도 아껴둠 (공개된 뒤 처음 창을 열 때 보여줌)

        CheckGateOpened();
        UpdateIcon(UpgradeManager.Instance.GetLevel(Id));
        NoticeBadge.Set(ref _badge, Rect, UpgradeManager.Instance.CanUpgrade(Id), UpgradeManager.Instance.UsesCrystals(Id)); // 결정 노드는 노란 느낌표
        if (justRevealed)
        {
            PlayShake();
            NodePurchaseEffect.PlayReveal(_rect);
        }
    }

    // ---- 호버 툴팁 ----

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UpgradeManager.Instance == null) return;

        UpgradeTooltip.Instance?.Show(BuildTooltipContent(), _rect);
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
        const float amplitudeDegrees = 7f;       // 최대 각도
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

    // 흔드는 도중 트리를 닫으면 코루틴이 멈추니 원래 자리로 되돌림
    void OnDisable()
    {
        if (_shakeRoutine != null) // 흔드는 도중에 꺼지면 기운 채로 남지 않게
        {
            _rect.localRotation = Quaternion.identity;
            _shakeRoutine = null;
        }
        if (_denyRoutine == null) return;
        _rect.pivot = _denyBasePivot;
        _denyRoutine = null;
    }

    // 못 살 때 흔들림 - 터지는 이펙트 없이 좌우로 짧게 (해금/획득량 노드와 동일)
    private void PlayDenyShake()
    {
        if (_denyRoutine != null) StopCoroutine(_denyRoutine);
        else _denyBasePivot = _rect.pivot; // 흔드는 중이 아닐 때만 원래 pivot 기억 (연타해도 자리가 안 밀리게)
        _denyRoutine = StartCoroutine(DenyShakeRoutine());
    }

    private IEnumerator DenyShakeRoutine()
    {
        const float duration = 0.18f;      // 흔들림 총 시간(초)
        const float amplitudeRatio = 0.06f; // 최대 이동 거리 (노드 폭 기준 비율)
        // anchoredPosition은 선(UpgradeTreeLink)이 읽는 값이라 건드리지 않고 pivot만 밀어서 그림만 흔듦 - 선은 제자리
        const float oscillations = 2f;     // 좌우 왕복 횟수

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // 업그레이드 화면이 timeScale 0이라 unscaled
            float progress = Mathf.Clamp01(elapsed / duration);
            float offset = amplitudeRatio * Mathf.Sin(progress * oscillations * Mathf.PI * 2f) * (1f - progress); // 이번 프레임 이동 (폭 비율)
            _rect.pivot = _denyBasePivot - new Vector2(offset, 0f); // pivot을 줄이면 그림이 오른쪽으로 감
            yield return null;
        }

        _rect.pivot = _denyBasePivot;
        _denyRoutine = null;
    }
}
