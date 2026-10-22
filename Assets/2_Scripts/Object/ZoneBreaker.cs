using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 책상처럼 "존 하나를 대표하는" 오브젝트. 부수면 같은 존에 놓인 다른 오브젝트도 전부 같이 부서져 조각이 들어오고,
// 처음 부술 때는 다음 존을 해금하는 연출(ZoneUnlockEffect)을 재생한다. 이후엔 부술 때마다 존 전체 수확만 함.
// 결정 업그레이드(존 자동 파괴 해금)를 사면 이 오브젝트가 갈라진 상태가 되어, 직접 부수지 않아도 autoBreakInterval마다 존 전체가 계속 부서진다.
// shockwaveSpeed를 넣으면 이 오브젝트 중심에서 균열이 뻗어나가며 가까운 오브젝트부터 차례로 부서짐 (0이면 전부 동시에)
[RequireComponent(typeof(Health))]
public class ZoneBreaker : MonoBehaviour
{
    [SerializeField] private Transform zoneRoot; // 이 존의 루트 - 이 아래에 있는 오브젝트를 전부 같이 부숨
    [SerializeField] private int unlockZone = 1; // 처음 부술 때 해금할 존 번호 (ZoneManager에 그 존이 없으면 해금/연출 안 함)
    [SerializeField] private ZoneUnlockEffect unlockEffect; // 첫 파괴 연출 (비워두면 연출 없이 바로 해금)
    [SerializeField] private float shockwaveSpeed = 0f; // 균열이 뻗어나가는 속도(월드 유닛/초) - 0이면 균열 없이 전부 동시에 부서짐
    [SerializeField] private Color crackColor = new Color(1f, 1f, 1f, 0.9f); // 균열 색 (흰 금)
    [SerializeField] private float crackWidth = 0.08f; // 균열 두께(월드 유닛)
    [SerializeField] private int crackBranches = 7; // 중심에서 뻗어나가는 큰 균열 갈래 수
    [SerializeField] private float shakeAmount = 0.15f; // 균열이 퍼질 때 카메라 흔들림 최대 세기(월드 유닛) - 존 해금 연출이 나올 땐 그쪽 흔들림을 씀
    [SerializeField] private float shakeDuration = 0.3f; // 카메라 흔들림 시간(초)
    [SerializeField] private float autoBreakInterval = 5f; // 존 자동 파괴를 해금하면 이 주기(초)마다 존 안의 다른 오브젝트가 전부 부서짐
    [SerializeField] private GameObject unlockedVisual; // 존 자동 파괴를 해금하면 켜지는 연출 오브젝트 (책상 균열 / 도로 싱크홀 그림 - 비워둬도 됨)

    private const float CrackStep = 0.35f; // 균열 한 마디 길이(월드 유닛)
    private const float CrackJitter = 30f; // 마디마다 꺾일 수 있는 최대 각도
    private const float CrackDrift = 25f; // 원래 뻗던 방향에서 벗어날 수 있는 최대 각도 (이 이상 휘지 않게)
    private const float SubCrackChance = 0.6f; // 큰 균열마다 잔가지가 하나 더 갈라질 확률
    private const float CrackFadeTime = 0.5f; // 다 뻗은 뒤 사라지는 시간(초)
    private const int CrackSortingAbove = 3; // 이 오브젝트(책상/도로)보다 몇 단계 위에 그릴지 (+1 금, +2 왁스 위, 존 오브젝트들보다는 아래)

    // 균열 한 갈래 - 점들과 각 점이 보이기 시작하는 퍼짐 거리
    private class CrackPath
    {
        public readonly List<Vector3> points = new List<Vector3>(); // 꺾이는 점들
        public readonly List<float> reach = new List<float>(); // 각 점이 보이기 시작하는 퍼짐 거리
    }

    private Health _health; // 이 오브젝트(책상/도로)의 체력
    private readonly List<LineRenderer> _crackLines = new List<LineRenderer>(); // 균열 선들 (필요한 만큼 만들어 재사용)
    private Click _click; // 이 오브젝트의 클릭 처리 - 오브젝트 번호를 알아내는 데 씀
    private float _autoBreakTimer; // 마지막 자동 파괴 이후 흐른 시간(초)
    private int _waveId; // 가장 최근 균열 번호 - 연타로 겹치면 균열은 최신 것만 그림

    // 오브젝트 번호로 그 오브젝트(책상/도로)의 존 자동 파괴 간격(초)을 찾음 - 업그레이드 설명에 "N초마다"를 넣는 데 씀 (못 찾으면 -1)
    public static float AutoBreakIntervalFor(int objectIndex)
    {
        foreach (ZoneBreaker breaker in FindObjectsByType<ZoneBreaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Click click = breaker.GetComponent<Click>(); // 이 존 파괴 오브젝트의 번호를 알아내려고
            if (click != null && click.ObjectIndex == objectIndex) return breaker.autoBreakInterval;
        }
        return -1f;
    }

    void Start()
    {
        _health = GetComponent<Health>();
        _health.OnDied += HandleDied;
        _click = GetComponent<Click>();
    }

    void Update()
    {
        // 존 자동 파괴(결정 업그레이드)를 해금했으면 갈라진 연출을 켜고, 주기마다 존 전체를 부숨. 안 보는 존에서도 계속 돎
        bool unlocked = _click != null && UpgradeManager.Instance != null
            && UpgradeManager.Instance.ZoneAutoBreakIsUnlockedFor(_click.ObjectIndex); // 이 오브젝트의 자동 파괴를 해금했는지
        if (unlockedVisual != null && unlockedVisual.activeSelf != unlocked) unlockedVisual.SetActive(unlocked);
        if (!unlocked)
        {
            _autoBreakTimer = 0f;
            return;
        }

        _autoBreakTimer += Time.deltaTime; // 업그레이드 화면이 열려 timeScale이 0이면 같이 멈춤
        if (_autoBreakTimer < autoBreakInterval) return;

        _autoBreakTimer = 0f;
        if (shockwaveSpeed > 0f) StartCoroutine(Shockwave(false)); // 주기마다 흔들리면 거슬려서 카메라 흔들림은 뺌
        else HarvestZone();
    }

    void OnDestroy()
    {
        if (_health != null)
            _health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        bool unlocking = ZoneManager.Instance != null
            && ZoneManager.Instance.UnlockedMaxZone < unlockZone
            && unlockZone < ZoneManager.Instance.ZoneCount; // 이번 파괴로 다음 존을 새로 여는지

        if (shockwaveSpeed > 0f) StartCoroutine(Shockwave(!unlocking));
        else HarvestZone();

        if (!unlocking) return;

        if (unlockEffect != null) unlockEffect.Play(unlockZone);
        else ZoneManager.Instance.UnlockZone(unlockZone);
    }

    // 존 안의 다른 오브젝트를 전부 부숨. 각자의 Click이 조각 지급/파편 연출을 알아서 처리함.
    // 해금 전(UnlockGate가 Health를 꺼둔 것)이나 이미 부서져 있는 것(TakeDamage가 무시)은 건너뜀
    private void HarvestZone()
    {
        if (zoneRoot == null) return;

        foreach (Health h in zoneRoot.GetComponentsInChildren<Health>())
            Break(h);
    }

    private void Break(Health h)
    {
        if (h == _health || !h.enabled) return;

        Click click = h.GetComponent<Click>(); // 대상의 클릭 처리 - 있으면 "직접 부순 것"으로 쳐서 보상이 파편으로 떨어지게 함
        if (click != null && click.enabled) click.BreakByZone();
        else h.TakeDamage(h.CurrentHP);
    }

    // 이 오브젝트 중심에서 번개 모양 균열이 여러 갈래로 뻗어나가고, 균열이 퍼진 거리 안에 들어온 오브젝트부터 부서짐
    private IEnumerator Shockwave(bool shake)
    {
        if (zoneRoot == null) yield break;

        Vector3 center = transform.position; // 균열 시작점
        Health[] targets = zoneRoot.GetComponentsInChildren<Health>(); // 부술 대상들
        bool[] done = new bool[targets.Length]; // 이미 균열이 닿은 대상인지
        float maxRadius = 0f; // 가장 먼 대상까지의 거리 - 균열이 여기까지 퍼지면 끝
        foreach (Health h in targets)
            maxRadius = Mathf.Max(maxRadius, DistanceTo(h, center));
        maxRadius += 1f; // 마지막 대상을 지나 조금 더 뻗다가 멈추게

        bool visible = ZoneManager.Instance == null || !ZoneManager.Instance.IsHidden(transform); // 안 보이는 존(자동클릭)이면 균열/흔들림 없이 부수기만
        int waveId = ++_waveId; // 이 균열 번호
        var paths = visible ? MakeCracks(center, maxRadius) : null; // 이번에 그릴 균열들
        if (visible && shake) CameraShake.Shake(shakeAmount, shakeDuration);

        for (float t = 0f; ; t += Time.deltaTime)
        {
            float radius = t * shockwaveSpeed; // 지금 균열이 퍼진 거리
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

        // 다 뻗은 균열은 잠깐 남았다가 흐려지며 사라짐
        for (float t = 0f; paths != null && t < CrackFadeTime; t += Time.deltaTime)
        {
            if (waveId != _waveId) yield break; // 새 균열이 시작됐으면 그쪽이 선을 씀
            DrawCracks(paths, maxRadius, 1f - t / CrackFadeTime);
            yield return null;
        }
        if (waveId == _waveId) foreach (LineRenderer line in _crackLines) line.enabled = false;
    }

    // 대상 그림의 중심까지 거리 (피벗이 아래쪽인 오브젝트도 있어서 그림 기준)
    private static float DistanceTo(Health h, Vector3 center)
    {
        Renderer renderer = h.GetComponent<Renderer>(); // 대상 그림
        Vector3 position = renderer != null ? renderer.bounds.center : h.transform.position; // 대상 중심
        return Vector2.Distance(position, center);
    }

    // 중심에서 crackBranches 갈래로 뻗는 들쭉날쭉한 균열 경로를 만듦 (이 오브젝트 그림 범위 밖으로는 안 나감). 갈래마다 잔가지가 생길 수 있음
    private List<CrackPath> MakeCracks(Vector3 center, float maxRadius)
    {
        var paths = new List<CrackPath>(); // 결과
        Surface area = new Surface(GetComponent<SpriteRenderer>()); // 균열이 생길 수 있는 범위 = 이 오브젝트(책상/도로)의 실제로 그려진 부분
        int branches = Mathf.Max(1, crackBranches); // 큰 갈래 수
        for (int i = 0; i < branches; i++)
        {
            float heading = i * 360f / branches + Random.Range(-15f, 15f); // 이 갈래가 뻗는 방향
            CrackPath main = Walk(center, 0f, heading, maxRadius, area); // 큰 균열
            paths.Add(main);
            if (main.points.Count < 4 || Random.value > SubCrackChance) continue;

            int from = Random.Range(1, main.points.Count - 2); // 잔가지가 갈라지는 점
            float side = Random.value < 0.5f ? -1f : 1f; // 왼쪽/오른쪽
            float subLength = (maxRadius - main.reach[from]) * 0.5f; // 잔가지 길이 (남은 거리의 절반)
            paths.Add(Walk(main.points[from], main.reach[from], heading + side * Random.Range(30f, 60f), main.reach[from] + subLength, area));
        }
        return paths;
    }

    // start에서 heading 방향으로 마디마다 조금씩 꺾이며 걸어감. startReach: 시작점이 보이기 시작하는 퍼짐 거리, endReach까지 감
    private static CrackPath Walk(Vector3 start, float startReach, float heading, float endReach, Surface area)
    {
        var path = new CrackPath(); // 결과
        path.points.Add(start);
        path.reach.Add(startReach);
        Vector3 position = start; // 지금 위치
        float angle = heading; // 지금 진행 방향
        float reach = startReach; // 지금까지 퍼진 거리
        while (reach < endReach)
        {
            angle = Mathf.Clamp(angle + Random.Range(-CrackJitter, CrackJitter), heading - CrackDrift, heading + CrackDrift);
            position += new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * CrackStep;
            if (!area.Contains(position)) break; // 그림(투명 부분 제외) 밖으로 나가면 멈춤
            reach += CrackStep;
            path.points.Add(position);
            path.reach.Add(reach);
        }
        return path;
    }

    // 스프라이트의 타이트 메시 삼각형으로 "실제로 그려진 부분 안인지"를 판정 (투명 테두리/빈 곳 제외)
    private class Surface
    {
        private readonly Transform _transform; // 그림 위치/크기
        private readonly Vector2[] _vertices; // 메시 꼭짓점 (스프라이트 로컬)
        private readonly ushort[] _triangles; // 삼각형 꼭짓점 번호 3개씩
        private readonly bool _flipX, _flipY; // 뒤집힘

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
            if (_vertices == null) return true; // 그림 정보가 없으면 막지 않음
            Vector2 p = _transform.InverseTransformPoint(world); // 스프라이트 로컬 좌표
            if (_flipX) p.x = -p.x;
            if (_flipY) p.y = -p.y;
            for (int i = 0; i + 2 < _triangles.Length; i += 3)
                if (InTriangle(p, _vertices[_triangles[i]], _vertices[_triangles[i + 1]], _vertices[_triangles[i + 2]])) return true;
            return false;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y); // 변 ab 기준 어느 쪽인지
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y); // 변 bc 기준
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y); // 변 ca 기준
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f; // 하나라도 음수
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f; // 하나라도 양수
            return !(negative && positive); // 셋 다 같은 쪽이면 안
        }
    }

    // 균열마다 지금 퍼진 거리(radius)까지만 선을 그림 (마지막 마디는 중간까지). alpha로 흐려짐
    private void DrawCracks(List<CrackPath> paths, float radius, float alpha)
    {
        Color color = crackColor; // 흐려지는 정도를 반영한 색
        color.a *= Mathf.Clamp01(alpha);
        for (int p = 0; p < paths.Count; p++)
        {
            LineRenderer line = GetCrackLine(p); // 이 갈래를 그릴 선
            CrackPath path = paths[p];
            int count = 0; // 보이는 점 수
            while (count < path.points.Count && path.reach[count] <= radius) count++;
            if (count == 0) { line.enabled = false; continue; }

            bool partial = count < path.points.Count; // 마지막 마디를 중간까지만 그리는지
            line.positionCount = partial ? count + 1 : count;
            for (int i = 0; i < count; i++) line.SetPosition(i, path.points[i]);
            if (partial)
            {
                float t = Mathf.InverseLerp(path.reach[count - 1], path.reach[count], radius); // 마지막 마디 진행률
                line.SetPosition(count, Vector3.Lerp(path.points[count - 1], path.points[count], t));
            }
            line.startColor = color;
            line.endColor = color;
            line.enabled = line.positionCount >= 2;
        }
        for (int p = paths.Count; p < _crackLines.Count; p++) _crackLines[p].enabled = false; // 지난번에 더 많았던 선은 끔
    }

    // index번째 균열 선 (없으면 만듦)
    private LineRenderer GetCrackLine(int index)
    {
        while (_crackLines.Count <= index)
        {
            var lineObject = new GameObject("Crack"); // 균열 선 하나를 담을 오브젝트
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>(); // 균열 선
            line.useWorldSpace = true;
            line.widthMultiplier = crackWidth;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            SpriteRenderer own = GetComponent<SpriteRenderer>(); // 이 오브젝트 그림 - 머티리얼/정렬 기준
            line.sharedMaterial = own != null ? own.sharedMaterial : null; // 스프라이트 기본 머티리얼을 같이 씀
            line.sortingLayerID = own != null ? own.sortingLayerID : 0;
            line.sortingOrder = (own != null ? own.sortingOrder : 0) + CrackSortingAbove;
            line.enabled = false;
            _crackLines.Add(line);
        }
        return _crackLines[index];
    }
}
