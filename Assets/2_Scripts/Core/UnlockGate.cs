using UnityEngine;

// objectIndex 오브젝트가 ObjectManager에서 해금됐을 때만 이 GameObject가 보이고 부술 수 있게 한다.
// 씬에 파괴 대상 오브젝트(가운데 스왑 오브젝트를 복붙해서 위치만 잡은 것)를 미리 배치해두고 이 컴포넌트를 붙인 뒤
// objectIndex를 그 오브젝트 번호로 맞추면 됨 (Click/Health/HealthSpriteSwitcher의 Fixed Object Index도 같은 번호로).
// 해금 전에는 이 오브젝트의 렌더러/콜라이더/Click/Health/HitFeedback을 자동으로 꺼서 안 보이고 클릭도 안 되게 한다.
public class UnlockGate : MonoBehaviour
{
    [SerializeField] private int objectIndex; // ObjectManager objects 리스트 인덱스 (0 = 첫 오브젝트, 항상 해금)

    // 자동으로 못 찾는 걸 추가로 끄고 싶을 때만 채움 (보통 비워둠)
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
        // 이 스크립트 자신은 빼고, 게이팅 대상이 될 만한 컴포넌트만 (Click / Health / HealthSpriteSwitcher / HitFeedback / SpriteColliderSync)
        var all = GetComponentsInChildren<MonoBehaviour>(true);
        var list = new System.Collections.Generic.List<Behaviour>();
        foreach (var m in all)
            if (m != null && m != this && (m is Click || m is Health || m is HealthSpriteSwitcher || m is HitFeedback || m is SpriteColliderSync))
                list.Add(m);
        if (extraBehaviours != null) list.AddRange(extraBehaviours);
        _behaviours = list.ToArray();
    }

    void OnEnable() => _applied = false; // 다시 켜질 때 강제 재적용

    void Update()
    {
        bool show = ObjectManager.Instance == null || ObjectManager.Instance.IsUnlocked(objectIndex);
        if (_applied && show == _shown) return; // 상태 안 바뀌었으면 아무것도 안 함 (대부분의 프레임)

        _applied = true;
        _shown = show;

        foreach (Renderer r in _renderers) if (r != null) r.enabled = show;
        if (extraRenderers != null) foreach (Renderer r in extraRenderers) if (r != null) r.enabled = show;
        foreach (Collider2D c in _colliders) if (c != null) c.enabled = show;
        foreach (Behaviour b in _behaviours) if (b != null) b.enabled = show;
    }
}
