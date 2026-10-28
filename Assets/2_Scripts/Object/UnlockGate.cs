using UnityEngine;

public class UnlockGate : MonoBehaviour
{
    [SerializeField] private int objectIndex; // 0 = 항상 해금

    // 해금 전에도 그림은 보임 (배경 오브젝트용)
    [SerializeField] private bool keepVisibleWhenLocked;

    // 1 이상이면 추가 배치 업그레이드로 나오는 n번째 추가분
    [SerializeField] private int requiredExtraCopies;

    [SerializeField] private Behaviour[] extraBehaviours;
    [SerializeField] private Renderer[] extraRenderers;

    private Renderer[] _renderers;
    private Collider2D[] _colliders;
    private Behaviour[] _behaviours;

    private bool _applied;
    private bool _shown;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider2D>(true);
        var all = GetComponentsInChildren<MonoBehaviour>(true);
        var list = new System.Collections.Generic.List<Behaviour>();
        foreach (var m in all)
            if (m != null && m != this && (m is Click || m is Health || m is HealthSpriteSwitcher || m is HitFeedback || m is SpriteColliderSync))
                list.Add(m);
        if (extraBehaviours != null) list.AddRange(extraBehaviours);
        _behaviours = list.ToArray();
    }

    void OnEnable() => _applied = false;

    void Update()
    {
        bool show = ObjectManager.Instance == null || ObjectManager.Instance.IsUnlocked(objectIndex);
        if (show && requiredExtraCopies > 0)
            show = UpgradeManager.Instance != null && UpgradeManager.Instance.ExtraObjectCopies(objectIndex) >= requiredExtraCopies;
        if (_applied && show == _shown) return;

        _applied = true;
        _shown = show;

        if (!keepVisibleWhenLocked)
        {
            foreach (Renderer r in _renderers) if (r != null) r.enabled = show;
            if (extraRenderers != null) foreach (Renderer r in extraRenderers) if (r != null) r.enabled = show;
        }
        foreach (Collider2D c in _colliders) if (c != null) c.enabled = show;
        foreach (Behaviour b in _behaviours) if (b != null) b.enabled = show;
    }
}
