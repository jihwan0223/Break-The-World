using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public partial class ZoneBreaker : MonoBehaviour
{
    [SerializeField] private Transform zoneRoot;
    [SerializeField] private int unlockZone = 1; // ZoneManager에 없는 존이면 해금/연출 안 함
    [SerializeField] private ZoneUnlockEffect unlockEffect;
    [SerializeField] private float shockwaveSpeed = 0f; // 월드 유닛/초, 0이면 동시에 부서짐
    [SerializeField] private Color crackColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private float crackWidth = 0.08f; // 월드 유닛
    [SerializeField] private int crackBranches = 7;
    [SerializeField] private float shakeAmount = 0.15f; // 월드 유닛
    [SerializeField] private float shakeDuration = 0.3f; // 초
    [SerializeField] private float autoBreakInterval = 5f; // 초
    [SerializeField] private GameObject unlockedVisual;

    private const float CrackStep = 0.35f; // 월드 유닛
    private const float CrackJitter = 30f; // 도
    private const float CrackDrift = 25f; // 도
    private const float SubCrackChance = 0.6f;
    private const float CrackFadeTime = 0.5f; // 초
    private const int CrackSortingAbove = 3; // +1 금, +2 왁스 위

    private class CrackPath
    {
        public readonly List<Vector3> points = new List<Vector3>();
        public readonly List<float> reach = new List<float>();
    }

    private Health _health;
    private readonly List<LineRenderer> _crackLines = new List<LineRenderer>();
    private Click _click;
    private float _autoBreakTimer;
    private int _waveId;

    public static float AutoBreakIntervalFor(int objectIndex)
    {
        foreach (ZoneBreaker breaker in FindObjectsByType<ZoneBreaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Click click = breaker.GetComponent<Click>();
            if (click != null && click.ObjectIndex == objectIndex) return breaker.autoBreakInterval;
        }
        return -1f;
    }

    void Start()
    {
        _health = GetComponent<Health>();
        _health.OnDied += HandleDied;
        _click = GetComponent<Click>();
        BossChallengeUI.Ensure();
    }

    void Update()
    {
        UpdateBoss();

        bool unlocked = _click != null && UpgradeManager.Instance != null
            && UpgradeManager.Instance.ZoneAutoBreakIsUnlockedFor(_click.ObjectIndex);
        if (unlockedVisual != null && unlockedVisual.activeSelf != unlocked) unlockedVisual.SetActive(unlocked);
        if (!unlocked)
        {
            _autoBreakTimer = 0f;
            return;
        }

        _autoBreakTimer += Time.deltaTime; // timeScale 0이면 같이 멈춤
        if (_autoBreakTimer < autoBreakInterval) return;

        _autoBreakTimer = 0f;
        if (shockwaveSpeed > 0f) StartCoroutine(Shockwave(false));
        else HarvestZone();
    }

    void OnDestroy()
    {
        if (_health != null)
            _health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        if (BossActive) WinBoss();

        bool unlocking = ZoneManager.Instance != null
            && ZoneManager.Instance.UnlockedMaxZone < unlockZone
            && unlockZone < ZoneManager.Instance.ZoneCount;

        if (shockwaveSpeed > 0f) StartCoroutine(Shockwave(!unlocking));
        else HarvestZone();

        if (!unlocking) return;

        if (unlockEffect != null) unlockEffect.Play(unlockZone);
        else ZoneManager.Instance.UnlockZone(unlockZone);
    }

    private void HarvestZone()
    {
        if (zoneRoot == null) return;

        foreach (Health h in zoneRoot.GetComponentsInChildren<Health>())
            Break(h);
    }

    private void Break(Health h)
    {
        if (h == _health || !h.enabled) return;

        Click click = h.GetComponent<Click>(); // 있으면 직접 부순 것으로 쳐서 보상이 파편으로 떨어짐
        if (click != null && click.enabled) click.BreakByZone();
        else h.TakeDamage(h.CurrentHP);
    }

    private IEnumerator Shockwave(bool shake)
    {
        if (zoneRoot == null) yield break;

        Renderer ownRenderer = GetComponent<Renderer>();
        Vector3 center = ownRenderer != null ? ownRenderer.bounds.center : transform.position; // 기준점이 아래인 오브젝트도 있어 그림 중심에서
        center.z = transform.position.z;
        Health[] targets = zoneRoot.GetComponentsInChildren<Health>();
        bool[] done = new bool[targets.Length];
        float maxRadius = 0f;
        foreach (Health h in targets)
            maxRadius = Mathf.Max(maxRadius, DistanceTo(h, center));
        maxRadius += 1f;

        bool visible = ZoneManager.Instance == null || !ZoneManager.Instance.IsHidden(transform);
        int waveId = ++_waveId;
        var paths = visible ? MakeCracks(center, maxRadius) : null;
        if (visible && shake) CameraShake.Shake(shakeAmount, shakeDuration);

        for (float t = 0f; ; t += Time.deltaTime)
        {
            float radius = t * shockwaveSpeed;
            for (int i = 0; i < targets.Length; i++)
            {
                if (done[i] || targets[i] == null || DistanceTo(targets[i], center) > radius) continue;
                done[i] = true;
                Break(targets[i]);
            }

            if (paths != null && waveId == _waveId) DrawCracks(paths, radius, 1f);

            if (radius >= maxRadius) break;
            yield return null;
        }

        for (float t = 0f; paths != null && t < CrackFadeTime; t += Time.deltaTime)
        {
            if (waveId != _waveId) yield break; // 새 균열이 시작됐으면 그쪽이 선을 씀
            DrawCracks(paths, maxRadius, 1f - t / CrackFadeTime);
            yield return null;
        }
        if (waveId == _waveId) foreach (LineRenderer line in _crackLines) line.enabled = false;
    }

    private static float DistanceTo(Health h, Vector3 center)
    {
        Renderer renderer = h.GetComponent<Renderer>();
        Vector3 position = renderer != null ? renderer.bounds.center : h.transform.position;
        return Vector2.Distance(position, center);
    }

    private List<CrackPath> MakeCracks(Vector3 center, float maxRadius)
    {
        var paths = new List<CrackPath>();
        Surface area = new Surface(GetComponent<SpriteRenderer>());
        int branches = Mathf.Max(1, crackBranches);
        for (int i = 0; i < branches; i++)
        {
            float heading = i * 360f / branches + Random.Range(-15f, 15f);
            CrackPath main = Walk(center, 0f, heading, maxRadius, area);
            paths.Add(main);
            if (main.points.Count < 4 || Random.value > SubCrackChance) continue;

            int from = Random.Range(1, main.points.Count - 2);
            float side = Random.value < 0.5f ? -1f : 1f;
            float subLength = (maxRadius - main.reach[from]) * 0.5f;
            paths.Add(Walk(main.points[from], main.reach[from], heading + side * Random.Range(30f, 60f), main.reach[from] + subLength, area));
        }
        return paths;
    }

    private static CrackPath Walk(Vector3 start, float startReach, float heading, float endReach, Surface area)
    {
        var path = new CrackPath();
        path.points.Add(start);
        path.reach.Add(startReach);
        Vector3 position = start;
        float angle = heading;
        float reach = startReach;
        while (reach < endReach)
        {
            angle = Mathf.Clamp(angle + Random.Range(-CrackJitter, CrackJitter), heading - CrackDrift, heading + CrackDrift);
            position += new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * CrackStep;
            if (!area.Contains(position)) break;
            reach += CrackStep;
            path.points.Add(position);
            path.reach.Add(reach);
        }
        return path;
    }

    // 타이트 메시 삼각형으로 실제로 그려진 부분인지 판정
    private class Surface
    {
        private readonly Transform _transform;
        private readonly Vector2[] _vertices;
        private readonly ushort[] _triangles;
        private readonly bool _flipX, _flipY;

        public Surface(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null) return;
            _transform = renderer.transform;
            _vertices = renderer.sprite.vertices;
            _triangles = renderer.sprite.triangles;
            _flipX = renderer.flipX;
            _flipY = renderer.flipY;
        }

        public bool Contains(Vector3 world)
        {
            if (_vertices == null) return true;
            Vector2 p = _transform.InverseTransformPoint(world);
            if (_flipX) p.x = -p.x;
            if (_flipY) p.y = -p.y;
            for (int i = 0; i + 2 < _triangles.Length; i += 3)
                if (InTriangle(p, _vertices[_triangles[i]], _vertices[_triangles[i + 1]], _vertices[_triangles[i + 2]])) return true;
            return false;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }
    }

    private void DrawCracks(List<CrackPath> paths, float radius, float alpha)
    {
        Color color = crackColor;
        color.a *= Mathf.Clamp01(alpha);
        for (int p = 0; p < paths.Count; p++)
        {
            LineRenderer line = GetCrackLine(p);
            CrackPath path = paths[p];
            int count = 0;
            while (count < path.points.Count && path.reach[count] <= radius) count++;
            if (count == 0) { line.enabled = false; continue; }

            bool partial = count < path.points.Count;
            line.positionCount = partial ? count + 1 : count;
            for (int i = 0; i < count; i++) line.SetPosition(i, path.points[i]);
            if (partial)
            {
                float t = Mathf.InverseLerp(path.reach[count - 1], path.reach[count], radius);
                line.SetPosition(count, Vector3.Lerp(path.points[count - 1], path.points[count], t));
            }
            line.startColor = color;
            line.endColor = color;
            line.enabled = line.positionCount >= 2;
        }
        for (int p = paths.Count; p < _crackLines.Count; p++) _crackLines[p].enabled = false;
    }

    private LineRenderer GetCrackLine(int index)
    {
        while (_crackLines.Count <= index)
        {
            var lineObject = new GameObject("Crack");
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = crackWidth;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            SpriteRenderer own = GetComponent<SpriteRenderer>();
            line.sharedMaterial = own != null ? own.sharedMaterial : null;
            line.sortingLayerID = own != null ? own.sortingLayerID : 0;
            line.sortingOrder = (own != null ? own.sortingOrder : 0) + CrackSortingAbove;
            line.enabled = false;
            _crackLines.Add(line);
        }
        return _crackLines[index];
    }
}
