using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class ObjectEconomyNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private int objectIndex; // 1부터 (0번은 처음부터 해금)
    [SerializeField] private bool isGain;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image doneOverlay; // 미사용
    [Tooltip("해금 완료(또는 획득량 1레벨 이상)되면 바뀔 스프라이트 (비워두면 기본 이미지 그대로 유지)")]
    [SerializeField] private Sprite upgradedSprite;
    [Tooltip("체크하면 데모 버전에서는 안 보임")] [SerializeField] private bool fullVersionOnly;
    [Tooltip("이 오브젝트를 해금해야 열리는 노드 (비우면 안 씀). 그 전엔 자물쇠 그림 + 제목/설명 ???, 해금하는 순간 트리 카메라가 와서 자물쇠가 깨지며 열림. 열리면 대상 오브젝트가 아직 없어도 살 수 있음")]
    [ObjectNameField] [SerializeField] private string gateObjectName;
    [SerializeField] private Sprite lockedSprite;

    // 선행은 들어오는 UpgradeTreeLink로 정해짐 - anchor 필드는 링크가 없을 때만 씀
    [SerializeField] private UpgradeNodeUI anchorMainNode;
    [SerializeField] private ObjectEconomyNodeUI anchorEconomyNode;

    private Button _button;
    private Image _icon;
    private Sprite _baseSprite;
    private RectTransform _rect;
    private Coroutine _denyRoutine;
    private Vector2 _denyBasePivot;
    private Coroutine _shakeRoutine;
    private bool _wasRevealed;
    private bool _refreshedOnce;
    private GameObject _badge;

    private readonly List<MonoBehaviour> _prereqNodes = new List<MonoBehaviour>();
    private bool _prereqResolved;
    private string _tooltipText;

    public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

    public bool IsLeveled() => ObjectManager.Instance != null &&
        (isGain ? ObjectManager.Instance.GetGainLevel(objectIndex) >= 1 : ObjectManager.Instance.IsUnlocked(objectIndex));

    public bool CanBuy()
    {
        ObjectManager objects = ObjectManager.Instance;
        if (objects == null || CurrencyManager.Instance == null || objectIndex <= 0 || IsGateClosed) return false;

        long cost;
        if (isGain)
        {
            if (!objects.IsUnlocked(objectIndex)) return false;
            cost = objects.GetNextGainCost(objectIndex);
            if (cost < 0) return false;
        }
        else
        {
            if (objects.IsUnlocked(objectIndex) || !objects.IsUnlockOrderMet(objectIndex) || !PrerequisiteSatisfied()) return false;
            cost = objects.GetUnlockCost(objectIndex);
        }
        return CurrencyManager.Instance.GetPieces() >= cost;
    }

    private bool PrerequisiteSatisfied()
    {
        if (!_prereqResolved)
        {
            _prereqResolved = true;
            foreach (UpgradeTreeLink link in FindObjectsByType<UpgradeTreeLink>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (link.ToNode != Rect) continue;
                foreach (RectTransform from in link.FromNodes)
                {
                    MonoBehaviour prereq = (MonoBehaviour)from.GetComponent<UpgradeNodeUI>()
                                           ?? from.GetComponent<ObjectEconomyNodeUI>();
                    if (prereq != null) _prereqNodes.Add(prereq);
                }
            }
        }

        if (_prereqNodes.Count > 0)
        {
            foreach (MonoBehaviour prereq in _prereqNodes)
            {
                bool leveled = prereq is UpgradeNodeUI upgradeNode ? upgradeNode.IsLeveled()
                             : prereq is ObjectEconomyNodeUI economyNode ? economyNode.IsLeveled()
                             : true;
                if (leveled) return true;
            }
            return false;
        }

        if (anchorMainNode != null) return anchorMainNode.IsLeveled();
        if (anchorEconomyNode != null) return anchorEconomyNode.IsLeveled();
        return true;
    }

    void Awake()
    {
        _rect = (RectTransform)transform;
        _button = GetComponent<Button>();
        _button.onClick.AddListener(HandleClicked);
        CacheIcon();

        if (doneOverlay != null)
        {
            doneOverlay.raycastTarget = false;
            doneOverlay.gameObject.SetActive(false);
        }
    }

    // 패널 Refresh가 이 Awake보다 먼저 불릴 수 있어 여기서도 잡음
    private void CacheIcon()
    {
        if (_icon != null) return;
        _icon = GetComponent<Image>();
        if (_icon != null) _baseSprite = _icon.sprite;
    }

    private void RefreshIconAfterGate() => UpdateIcon();

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

    private void UpdateIcon()
    {
        CacheIcon();
        if (_icon == null) return;
        if (lockedSprite != null && (IsGateClosed || _gateRevealPending)) { _icon.sprite = lockedSprite; return; }
        if (upgradedSprite == null) { _icon.sprite = _baseSprite; return; }
        bool maxed = ObjectManager.Instance != null && (isGain
            ? ObjectManager.Instance.GetGainLevel(objectIndex) >= ObjectManager.MaxGainLevel
            : ObjectManager.Instance.IsUnlocked(objectIndex));
        _icon.sprite = maxed ? upgradedSprite : _baseSprite;
    }

    private void HandleClicked()
    {
        if (ObjectManager.Instance == null) return;

        // 이미 해금됐으면 TryUnlock이 true라 따로 막음
        bool alreadyDone = !isGain && ObjectManager.Instance.IsUnlocked(objectIndex);
        bool success = !alreadyDone && !IsGateClosed && (isGain ? ObjectManager.Instance.TryUpgradeGain(objectIndex) : ObjectManager.Instance.TryUnlock(objectIndex));
        if (!success)
        {
            PlayDenyShake();
            return;
        }

        PlayShake();
        NodePurchaseEffect.Play(_rect, NodePurchaseEffect.NormalColor, !isGain);
        UpgradeTooltip.Instance?.PlayShake();

        UpgradeTreeUI.Instance?.RefreshAll(animateReveals: true);
    }

    public void Refresh(bool animateReveal = false)
    {
        if (ObjectManager.Instance == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // Gain 노드는 자기 오브젝트가 해금되면 나타남
        bool isRevealed = !(GameSettings.IsDemo && fullVersionOnly) && !GameSettings.IsDemoLockedObject(objectIndex) && (isGain ? ObjectManager.Instance.IsUnlocked(objectIndex) : PrerequisiteSatisfied());

        bool justRevealed = animateReveal && _refreshedOnce && isRevealed && !_wasRevealed;
        _wasRevealed = isRevealed;
        _refreshedOnce = true;

        gameObject.SetActive(isRevealed);
        if (!isRevealed) return; // 안 보이는 노드의 관문 연출은 공개 뒤로 미룸

        CheckGateOpened();
        UpdateIcon();
        NoticeBadge.Set(ref _badge, Rect, CanBuy());
        if (justRevealed)
        {
            PlayShake();
            NodePurchaseEffect.PlayReveal(_rect);
        }

        string objectName = ObjectManager.Instance.GetObjectAt(objectIndex).objectName;

        if (IsGateClosed)
        {
            _tooltipText = "???";
            if (label != null) label.text = _tooltipText;
        }
        else if (!isGain)
        {
            bool unlocked = ObjectManager.Instance.IsUnlocked(objectIndex);

            if (unlocked)
            {
                _tooltipText = $"해금\n{objectName}\n완료";
            }
            else if (!ObjectManager.Instance.IsUnlockOrderMet(objectIndex))
            {
                _tooltipText = "해금\n???";
            }
            else
            {
                long cost = ObjectManager.Instance.GetUnlockCost(objectIndex);
                _tooltipText = $"해금\n{objectName}\n{NumberFormatUtil.Format(cost)} 조각";
            }
            if (label != null) label.text = _tooltipText;

            if (doneOverlay != null)
                doneOverlay.gameObject.SetActive(false);
        }
        else
        {
            int level = ObjectManager.Instance.GetGainLevel(objectIndex);
            bool maxed = level >= 5;

            string costLine = maxed ? "최대" : $"{NumberFormatUtil.Format(ObjectManager.Instance.GetNextGainCost(objectIndex))} 조각";
            _tooltipText = $"{objectName} 획득량\n{level}/5\n{costLine}";
            if (label != null) label.text = _tooltipText;

            if (doneOverlay != null)
                doneOverlay.gameObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => UpgradeTooltip.Instance?.Show(BuildTooltipContent(), Rect);
    public void OnPointerExit(PointerEventData eventData) => UpgradeTooltip.Instance?.Hide();

    private UpgradeTooltip.Content BuildTooltipContent()
    {
        if (ObjectManager.Instance == null) return new UpgradeTooltip.Content { title = "" };

        string objectName = ObjectManager.Instance.GetObjectAt(objectIndex).objectName;
        if (IsGateClosed) return new UpgradeTooltip.Content { title = "???", description = "???", level = "" };

        if (!isGain)
        {
            bool unlocked = ObjectManager.Instance.IsUnlocked(objectIndex);
            string zoneHint = ObjectManager.Instance.OpensNextZone(objectIndex) && !ObjectManager.Instance.IsUnlocked(objectIndex + 1)
                ? "부수면 다음 맵이 열립니다." : null;
            if (unlocked)
                return new UpgradeTooltip.Content { title = $"{objectName} 해금", description = zoneHint ?? "이미 해금했습니다.", level = "완료" };

            if (!ObjectManager.Instance.IsUnlockOrderMet(objectIndex))
                return new UpgradeTooltip.Content { title = "???", description = ObjectManager.Instance.NextRequiredUnlockHintText(objectIndex), level = "0 / 1" };

            long cost = ObjectManager.Instance.GetUnlockCost(objectIndex);
            return new UpgradeTooltip.Content
            {
                title = $"{objectName} 해금",
                description = zoneHint ?? "새 오브젝트가 등장합니다.",
                level = "0 / 1",
                price = $"{NumberFormatUtil.Format(cost)} 조각",
            };
        }

        int level = ObjectManager.Instance.GetGainLevel(objectIndex);
        bool maxed = level >= 5;
        string gain = NumberFormatUtil.Format(ObjectManager.Instance.GainBonusPerLevel(objectIndex));
        return new UpgradeTooltip.Content
        {
            title = $"{objectName} 획득량",
            description = $"{KoreanJosa.EulReul(objectName)} 부술 때 얻는 조각이 +{gain}개 증가합니다.",
            level = $"{level} / 5",
            price = maxed ? "최대" : $"{NumberFormatUtil.Format(ObjectManager.Instance.GetNextGainCost(objectIndex))} 조각",
        };
    }

    private void PlayShake()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _rect.localRotation = Quaternion.identity;
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        const float duration = 0.25f;
        const float amplitudeDegrees = 7f;
        const float oscillations = 1.5f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // timeScale 0이라 unscaled
            float progress = Mathf.Clamp01(elapsed / duration);

            float angle = amplitudeDegrees * Mathf.Sin(progress * oscillations * Mathf.PI * 2f) * (1f - progress);
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
