using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 선행 노드는 들어오는 UpgradeTreeLink로 정해짐
// id = 오브젝트 이름 (세이브 키라 씬 안에서 겹치면 안 됨)
[RequireComponent(typeof(Button))]
public class UpgradeNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("업그레이드 설정")]
    [SerializeField] private string displayName;
    [TextArea] [SerializeField] private string description;
    [SerializeField] private UpgradeManager.UpgradeEffect effect;
    [Tooltip("무기 대상 효과(클릭 데미지 / 무기 처치 보너스)에서 씀. 특정 무기 전용이면 그 무기, 전역이면 \"전체 (전역)\"")]
    [ObjectNameField(ObjectNameFieldSource.Weapon)] [SerializeField] private string targetWeaponName;
    [Tooltip("오브젝트 대상 효과(자동클릭 계열 / 오브젝트 조각 획득 / 2배 드랍 확률)에서 씀. 그 오브젝트 장착 중일 때만 작동")]
    [ObjectNameField] [SerializeField] private string targetObjectName;
    [Min(1)] [SerializeField] private int maxLevel = 5;
    [SerializeField] private float[] valuePerLevel = { 1f }; // 배열이 짧으면 마지막 값 반복

    [Tooltip("이 업그레이드 한 번 올리는 데 드는 비용들. 조각과 결정을 동시에 요구할 수 있음 (전부 있어야 구매됨)")]
    [SerializeField] private NodeCost[] costs = { new NodeCost() };

    [Tooltip("1레벨 이상 찍으면 바뀔 스프라이트 (비워두면 기본 이미지 그대로 유지)")]
    [SerializeField] private Sprite upgradedSprite;
    [Tooltip("체크하면 데모 버전에서는 안 보임")] [SerializeField] private bool fullVersionOnly;
    public bool FullVersionOnly => fullVersionOnly;
    [Tooltip("이 오브젝트를 해금해야 열리는 노드 (비우면 안 씀). 그 전엔 자물쇠 그림 + 제목/설명 ???, 해금하는 순간 트리 카메라가 와서 자물쇠가 깨지며 열림. 열리면 대상 오브젝트가 아직 없어도 살 수 있음")]
    [ObjectNameField] [SerializeField] private string gateObjectName;
    [SerializeField] private Sprite lockedSprite;

    [Serializable]
    public class NodeCost
    {
        [Tooltip("체크하면 조각 대신 결정으로 결제")]
        public bool useCrystals;
        [Min(1)] public long baseAmount = 10;
        public float levelGrowth = 2f;
        [Tooltip("0보다 크면 0->1레벨 비용만 이 값으로 (1레벨만 싸게). 2레벨부터는 위 공식 그대로")]
        [Min(0)] public long firstLevelAmount; // 0이면 안 씀
        [Tooltip("비어있지 않으면 위 공식 대신 레벨별 비용을 그대로 씀 (0->1, 1->2, ... 순서, 짧으면 마지막 값 반복)")]
        public long[] levelAmounts = System.Array.Empty<long>(); // 있으면 공식 대신 레벨별 값
    }

    private Button _button;
    private Image _icon;
    private Sprite _baseSprite;
    private RectTransform _rect;
    private Coroutine _shakeRoutine;
    private Coroutine _denyRoutine;
    private Vector2 _denyBasePivot;
    private bool _wasRevealed;
    private bool _refreshedOnce;
    private GameObject _badge;

    public string Id => name;
    public string DisplayName => displayName;
    public string Description => description;
    public UpgradeManager.UpgradeEffect Effect => effect;
    public string TargetWeaponName => targetWeaponName;
    public string TargetObjectName => targetObjectName;
    public int MaxLevel => maxLevel;
    public float[] ValuePerLevel => valuePerLevel;
    public NodeCost[] Costs => costs;
    public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

    public bool IsLeveled() => UpgradeManager.Instance != null && UpgradeManager.Instance.GetLevel(Id) >= 1;

    void Awake()
    {
        _rect = (RectTransform)transform;
        _button = GetComponent<Button>();
        _button.onClick.AddListener(HandleClicked);
        CacheIcon();
    }

    // 패널 RefreshAll이 이 Awake보다 먼저 불릴 수 있어 여기서도 잡음
    private void CacheIcon()
    {
        if (_icon != null) return;
        _icon = GetComponent<Image>();
        if (_icon != null) _baseSprite = _icon.sprite;
    }

    private void UpdateIcon(int level)
    {
        CacheIcon();
        if (_icon == null) return;
        if (lockedSprite != null && (IsGateClosed || _gateRevealPending)) { _icon.sprite = lockedSprite; return; }
        if (upgradedSprite == null) { _icon.sprite = _baseSprite; return; }
        _icon.sprite = level >= maxLevel ? upgradedSprite : _baseSprite;
    }

    private void RefreshIconAfterGate() => UpdateIcon(UpgradeManager.Instance != null ? UpgradeManager.Instance.GetLevel(Id) : 0);

    public bool IsGateClosed => !string.IsNullOrEmpty(gateObjectName) && ObjectManager.Instance != null
        && !ObjectManager.Instance.IsUnlocked(ObjectManager.StaticIndexOfName(gateObjectName));
    public string GateObjectName => gateObjectName;
    private bool _gateRevealPending;

    private void CheckGateOpened()
    {
        if (string.IsNullOrEmpty(gateObjectName) || IsGateClosed) return;
        UpgradeTreeUI tree = UpgradeTreeUI.Instance;
        if (tree == null || !tree.isActiveAndEnabled || !GateReveals.TryMarkSeen(name)) return;
        if (SaveManager.Instance != null) SaveManager.Instance.MarkDirty(); // 본 연출을 세이브에 남김
        _gateRevealPending = true;
        tree.PlayGateReveal(Rect, FinishGateReveal);
    }

    private void FinishGateReveal()
    {
        _gateRevealPending = false;
        RefreshIconAfterGate();
        if (!isActiveAndEnabled) return; // 창을 닫아 꺼졌으면 연출 생략
        PlayShake();
        NodePurchaseEffect.Play(Rect, NodePurchaseEffect.NormalColor, true);
    }

    private bool IsMasked() => UpgradeManager.Instance != null && UpgradeManager.Instance.IsTargetLocked(Id);

    private void HandleClicked()
    {
        if (UpgradeManager.Instance == null) return;
        if (!UpgradeManager.Instance.TryUpgrade(Id))
        {
            PlayDenyShake();
            return;
        }
        UpgradeTooltip.Instance?.UpdateContent(BuildTooltipContent());
        PlayShake();
        bool crystal = Array.Exists(costs, cost => cost.useCrystals);
        bool maxed = UpgradeManager.Instance.GetLevel(Id) >= maxLevel;
        NodePurchaseEffect.Play(_rect, crystal ? NodePurchaseEffect.CrystalColor : NodePurchaseEffect.NormalColor, maxed);
        UpgradeTooltip.Instance?.PlayShake();
        UpgradeTreeUI.Instance?.RefreshAll(animateReveals: true);
    }

    private UpgradeTooltip.Content BuildTooltipContent()
    {
        int level = UpgradeManager.Instance != null ? UpgradeManager.Instance.GetLevel(Id) : 0;

        return new UpgradeTooltip.Content
        {
            title = IsMasked() ? "???" : displayName,
            description = IsMasked() ? UpgradeManager.Instance.LockedTargetHintText(Id) : AutoDescription(level) ?? description.Replace("{n}", CurrentSecondsText()),
            level = $"{level} / {maxLevel}",
            price = IsMasked() ? "" : NextCostText(level),
        };
    }

    private string CurrentSecondsText()
    {
        UpgradeManager upgrades = UpgradeManager.Instance;
        int objectIndex = ObjectManager.StaticIndexOfName(targetObjectName);
        float seconds;
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

    private string AutoDescription(int level)
    {
        if (valuePerLevel == null || valuePerLevel.Length == 0) return null;
        float value = valuePerLevel[Mathf.Clamp(level, 0, valuePerLevel.Length - 1)]; // UpgradeManager.ValueAtLevel(level + 1)과 같은 인덱싱
        string n = Round(value);
        string obj = KoreanJosa.EulReul(targetObjectName);
        string weapon = targetWeaponName;

        switch (effect)
        {
            case UpgradeManager.UpgradeEffect.ClickDamage:
            {
                int weaponIndex = WeaponManager.StaticIndexOfWeaponName(weapon);
                if (weaponIndex < 0 && WeaponManager.Instance != null)
                    weaponIndex = WeaponManager.Instance.EquippedIndex;
                int scale = ObjectHealthCalculator.ScaleOfTier(weaponIndex + 1);
                string amount = NumberFormatUtil.Format(Mathf.RoundToInt(value * scale));
                return string.IsNullOrEmpty(weapon) ? $"모든 무기의 데미지가 +{amount} 증가합니다." : $"{weapon}의 데미지가 +{amount} 증가합니다.";
            }
            case UpgradeManager.UpgradeEffect.CritChance: return $"치명타 확률이 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.CritDamage: return $"치명타 데미지가 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.LuckyClickChanceUp: return $"슈퍼 크리티컬 확률이 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.DoubleClickChanceUp: return $"더블 클릭 확률이 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.PieceGainGlobal: return $"모든 오브젝트를 부술 때 얻는 조각이 +{n}% 증가합니다.";
            case UpgradeManager.UpgradeEffect.PieceGainObject:
            {
                int objectIndex = ObjectManager.StaticIndexOfName(targetObjectName);
                if (objectIndex < 0 || ObjectManager.Instance == null) return $"{obj} 부술 때 얻는 조각이 +{n}% 증가합니다.";
                double pieces = UpgradeManager.ObjectPieceBonusPerLevel(ObjectManager.Instance.GetBaseReward(objectIndex), value);
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
            case UpgradeManager.UpgradeEffect.CursorRange: return "커서 범위가 더 증가합니다.";
            case UpgradeManager.UpgradeEffect.ObjectExtraCopies:
                return $"맵에 {KoreanJosa.IGa(targetObjectName)} +{n}개 더 놓입니다. 동시에 여러 개를 부술 수 있어 조각을 더 빨리 모읍니다.";
            default: return null;
        }
    }

    private static string Round(float value)
    {
        float abs = Mathf.Abs(value);
        if (abs >= 1f) return Mathf.RoundToInt(abs).ToString();
        string oneDecimal = abs.ToString("0.#");
        return oneDecimal != "0" ? oneDecimal : abs.ToString("0.##");
    }

    private string NextCostText(int level)
    {
        if (level >= maxLevel) return "최대";

        PieceCost[] cost = UpgradeManager.Instance?.GetNextCost(Id);
        if (cost == null || cost.Length == 0) return "";

        long pieces = 0;
        long crystals = 0;
        foreach (PieceCost c in cost)
        {
            if (c.isCrystal) crystals += c.amount;
            else pieces += c.amount;
        }

        string piecesText = pieces > 0 ? $"{NumberFormatUtil.Format(pieces)} 조각" : "";
        string crystalsText = crystals > 0 ? $"{NumberFormatUtil.Format(crystals)} 결정" : "";
        return pieces > 0 && crystals > 0 ? $"{piecesText} + {crystalsText}" : piecesText + crystalsText;
    }

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
        if (!isRevealed) return; // 안 보이는 노드의 관문 연출은 공개 뒤로 미룸

        CheckGateOpened();
        UpdateIcon(UpgradeManager.Instance.GetLevel(Id));
        NoticeBadge.Set(ref _badge, Rect, UpgradeManager.Instance.CanUpgrade(Id), UpgradeManager.Instance.UsesCrystals(Id));
        if (justRevealed)
        {
            PlayShake();
            NodePurchaseEffect.PlayReveal(_rect);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UpgradeManager.Instance == null) return;

        UpgradeTooltip.Instance?.Show(BuildTooltipContent(), _rect);
    }

    public void OnPointerExit(PointerEventData eventData) => UpgradeTooltip.Instance?.Hide();

    private void PlayShake()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _rect.localRotation = Quaternion.identity;
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        const float duration = 0.22f;
        const float amplitudeDegrees = 7f;
        const float oscillations = 1.5f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // timeScale 0이라 unscaled
            float p = Mathf.Clamp01(elapsed / duration);
            float angle = amplitudeDegrees * Mathf.Sin(p * oscillations * Mathf.PI * 2f) * (1f - p);
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        _rect.localRotation = Quaternion.identity;
        _shakeRoutine = null;
    }

    void OnDisable()
    {
        if (_shakeRoutine != null)
        {
            _rect.localRotation = Quaternion.identity;
            _shakeRoutine = null;
        }
        if (_denyRoutine == null) return;
        _rect.pivot = _denyBasePivot;
        _denyRoutine = null;
    }

    private void PlayDenyShake()
    {
        if (_denyRoutine != null) StopCoroutine(_denyRoutine);
        else _denyBasePivot = _rect.pivot;
        _denyRoutine = StartCoroutine(DenyShakeRoutine());
    }

    private IEnumerator DenyShakeRoutine()
    {
        const float duration = 0.18f;
        const float amplitudeRatio = 0.06f;
        // 선이 anchoredPosition을 읽어서 pivot만 밀어 그림만 흔듦
        const float oscillations = 2f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // timeScale 0이라 unscaled
            float progress = Mathf.Clamp01(elapsed / duration);
            float offset = amplitudeRatio * Mathf.Sin(progress * oscillations * Mathf.PI * 2f) * (1f - progress);
            _rect.pivot = _denyBasePivot - new Vector2(offset, 0f);
            yield return null;
        }

        _rect.pivot = _denyBasePivot;
        _denyRoutine = null;
    }
}
