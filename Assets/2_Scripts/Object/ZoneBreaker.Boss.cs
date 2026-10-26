using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 보스전: 지형(책상/도로/회사)은 "보스 도전" 버튼을 눌러 제한시간 안에 부숴야 처음 부서짐 (그 전엔 때려도 안 깎임).
// 성공하면 다음 맵 해금(지금 있던 첫 파괴 연출 그대로) + 조각/결정 보상 + 슬로우 모션. 실패/포기하면 체력이 다시 차고 바로 재도전 가능.
// 보스전 중엔 같은 존의 기본 오브젝트들이 왁스 보호막으로 연결돼, 살아 있는 수만큼 보스가 받는 데미지가 줄어듦 (부수면 연결이 끊기고 다시 생기면 다시 연결)
public partial class ZoneBreaker
{
    [Header("보스전")]
    [SerializeField] private float bossTimeLimit = 30f; // 보스전 제한시간(초)
    [SerializeField] private float bossPieceRewardMultiplier = 10f; // 성공 보상 조각 = 이 오브젝트 한 번 부술 때 기본 보상 × 이 값
    [SerializeField] private int bossCrystalReward = 3; // 성공 보상 결정 개수
    [SerializeField] private float shieldPerLink = 0.15f; // 보호막 연결 하나당 줄어드는 데미지 비율
    [SerializeField] private float maxShield = 0.75f; // 보호막으로 줄어드는 데미지 최대 비율
    [SerializeField] private float[] shieldSpawnTimes = { 25f, 10f }; // 남은 시간이 이 값(초)이 될 때마다 같은 존 오브젝트가 하나씩(랜덤) 나타나 보호막으로 연결됨
    [SerializeField] private bool repeatBossForTest = true; // 테스트용 - 켜두면 잡은 보스도 도전 버튼이 계속 뜸 (출시 전에 끌 것)
    [SerializeField] private float shieldFadeSeconds = 0.8f; // 보호막 오브젝트가 투명한 상태에서 서서히 나타나는 시간(초)

    public static bool FinalBossCleared; // 다음 맵이 없는 마지막 보스(회사)를 잡았는지 - 저장됨
    public static ZoneBreaker ActiveBoss { get; private set; } // 지금 보스전 중인 보스 (없으면 null)
    public static event System.Action<ZoneBreaker> BossFailed; // 시간 안에 못 부숴서 실패했을 때 - UI가 재도전/나가기 창을 띄움
    private static readonly List<ZoneBreaker> _bosses = new List<ZoneBreaker>(); // 켜져 있는 보스들 (UI가 도전 버튼을 띄울 보스를 찾을 때)

    private readonly List<Health> _links = new List<Health>(); // 보호막으로 연결된 기본 오브젝트들 (보스전 시작 때 정함)
    private Color[] _linkColors; // 연결별 보호막 색 (그 오브젝트의 왁스 색)
    private bool[] _linkOn; // 연결별로 지금 이어져 있는지 (살아 있는지)
    private float _bossTimeLeft; // 남은 시간(초)
    private bool _bossWon; // 이번 플레이에서 이 보스를 잡았는지 - 다음 맵 해금 연출이 끝나기 전에도 클리어로 치려고
    private WaxCoating _wax; // 보스를 덮는 왁스 (보호막 덧칠도 여기에 그림)
    private readonly List<Health> _hiddenObjects = new List<Health>(); // 보스전 동안 숨겨둔 같은 존 오브젝트들 (끝나면 다시 나타남)
    private int _nextSpawn; // 다음에 쓸 shieldSpawnTimes 번호
    private readonly HashSet<Health> _removing = new HashSet<Health>(); // 부서져서 사라지는 중인 보호막 오브젝트 (다시 생기지 않게 숨김)

    // 플레이를 새로 시작할 때 정적 상태를 비움 (플레이 시작 때 스크립트를 다시 불러오지 않는 설정이라)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveBoss = null;
        FinalBossCleared = false;
        _bosses.Clear();
    }

    void OnEnable() => _bosses.Add(this);

    void OnDisable()
    {
        _bosses.Remove(this);
        if (ActiveBoss == this) ActiveBoss = null; // 씬이 닫히는 중일 수 있어서 숨긴 오브젝트를 되살리진 않음
    }

    public bool BossActive => ActiveBoss == this;
    // 이 오브젝트가 지금 보스전 보호막 오브젝트인지 (부숴도 조각/결정이 안 나옴)
    public static bool IsShieldObject(Health h) => ActiveBoss != null && h != null && ActiveBoss._links.Contains(h);
    // 다음 맵이 없는 마지막 보스인지
    private bool IsFinalBoss => ZoneManager.Instance != null && unlockZone >= ZoneManager.Instance.ZoneCount;
    // 이미 잡은 보스인지 - 맵1/2 보스는 다음 맵이 열려 있으면, 마지막 보스는 저장된 값으로
    public bool BossCleared => _bossWon || (IsFinalBoss ? FinalBossCleared : ZoneManager.Instance == null || ZoneManager.Instance.UnlockedMaxZone >= unlockZone);
    // 때리면 체력이 깎이는지 - 잡기 전에는 보스전 중에만
    public bool BossAllowsDamage => BossCleared || BossActive;
    public float BossTimeLeft => _bossTimeLeft;
    public float BossTimeRatio => bossTimeLimit > 0f ? Mathf.Clamp01(_bossTimeLeft / bossTimeLimit) : 0f; // 타이머 바 길이
    public float BossHpRatio => _health != null && _health.MaxHP > 0 ? (float)_health.CurrentHP / _health.MaxHP : 0f;
    public float ShieldRatio { get; private set; } // 지금 보호막으로 줄어드는 데미지 비율
    public float ShieldFill { get; private set; } // 체력바에 겹쳐 그릴 보호막 양 0~1 (보호막 오브젝트를 다 꽉 찬 채로 다 꺼내면 1, 때릴수록 줄어듦)
    public float BossDamageMultiplier => 1f - ShieldRatio;

    // 지금 보이는 존에서 도전할 수 있는 보스 (없거나 보스전 중이면 null)
    public static ZoneBreaker ChallengeableBoss()
    {
        if (ActiveBoss != null) return null;
        foreach (ZoneBreaker boss in _bosses)
            if (boss != null && boss.CanChallenge) return boss;
        return null;
    }

    // 해금돼서(UnlockGate가 Health를 켜둠) 보고 있는 존에 있고, 아직 안 잡은 보스
    private bool CanChallenge => _health != null && _health.enabled && !_health.IsDead && (!BossCleared || repeatBossForTest)
        && !(ZoneManager.Instance != null && ZoneManager.Instance.IsHidden(transform));

    // 디버그 초기화용 - 모든 보스를 안 잡은 상태로
    public static void ResetBosses()
    {
        FinalBossCleared = false;
        foreach (ZoneBreaker boss in _bosses)
            if (boss != null) boss._bossWon = false;
    }

    // 보스 도전 버튼
    public void StartBoss()
    {
        if (!CanChallenge || ActiveBoss != null) return;

        ActiveBoss = this;
        _bossTimeLeft = bossTimeLimit;
        _health.ResetToFull();

        // 보스만 놓인 채 시작 - 같은 존 오브젝트는 숨겨뒀다가 정해진 시간에 하나씩 나타남
        _links.Clear();
        _hiddenObjects.Clear();
        _nextSpawn = 0;
        if (zoneRoot != null)
            foreach (Health h in zoneRoot.GetComponentsInChildren<Health>())
                if (h != _health) _hiddenObjects.Add(h);
        foreach (Health h in _hiddenObjects) h.gameObject.SetActive(false);
        RebuildLinkColors();

        if (_wax == null) _wax = GetComponent<WaxCoating>();
        if (_wax != null) _wax.ForceCover = true; // 보스는 왁스를 입은 모습으로 등장
        UpdateShield();
    }

    // 포기하기 버튼
    public void GiveUpBoss()
    {
        if (BossActive) LoseBoss();
    }

    // 보스전 중 매 프레임: 시간이 다 되거나 다른 맵으로 가면 실패, 아니면 보호막 갱신
    private void UpdateBoss()
    {
        if (!BossActive)
        {
            // 이겨서 부서진 뒤 다음 프레임에 강제 왁스를 끔 (부서지는 순간엔 왁스가 흩어지는 연출이 나와야 해서 켜둠)
            if (_wax != null && _wax.ForceCover && _health != null && _health.IsDead) _wax.ForceCover = false;
            return;
        }
        if (ZoneManager.Instance != null && ZoneManager.Instance.IsHidden(transform)) { LoseBoss(); return; }

        _bossTimeLeft -= Time.deltaTime; // 업그레이드 창 등으로 시간이 멈추면 같이 멈춤
        if (_bossTimeLeft <= 0f) { LoseBoss(); BossFailed?.Invoke(this); return; } // 시간 초과만 실패 창을 띄움 (포기/맵 이동은 바로 끝)

        while (shieldSpawnTimes != null && _nextSpawn < shieldSpawnTimes.Length && _bossTimeLeft <= shieldSpawnTimes[_nextSpawn])
        {
            _nextSpawn++;
            SpawnShieldObject();
        }
        UpdateShield();
    }

    // 살아 있는 연결 수로 데미지 감소율을 정하고, 보스 왁스에 연결된 오브젝트 색 덧칠을 그림
    private void UpdateShield()
    {
        float strength = 0f; // 살아 있는 보호막 오브젝트들의 남은 체력 비율 합 - 때릴수록 보호막이 조금씩 약해짐
        for (int i = 0; i < _links.Count; i++)
        {
            Health link = _links[i]; // 연결된 오브젝트
            _linkOn[i] = link != null && link.gameObject.activeInHierarchy && link.enabled && !link.IsDead;
            if (link != null && link.IsDead && _removing.Add(link)) StartCoroutine(RemoveWhenFaded(link)); // 부수면 사라지고 보호막도 끊김
            if (_linkOn[i] && link.MaxHP > 0) strength += (float)link.CurrentHP / link.MaxHP;
        }
        ShieldRatio = Mathf.Min(maxShield, strength * shieldPerLink);
        ShieldFill = Mathf.Clamp01(strength / Mathf.Max(1, shieldSpawnTimes != null ? shieldSpawnTimes.Length : 1));
        if (_wax != null) _wax.SetShield(_linkColors, _linkOn);
    }

    // 부서진 보호막 오브젝트가 흐려지며 사라지는 연출이 끝나면 숨김 - 보스전 동안 다시 생기지 않음 (끝나면 EndBoss가 되살림)
    private IEnumerator RemoveWhenFaded(Health link)
    {
        while (link != null && link.IsDead && link.RespawnRemaining <= 0f) yield return null;
        if (link != null && BossActive && _links.Contains(link)) link.gameObject.SetActive(false);
        _removing.Remove(link);
    }

    // 숨겨둔 오브젝트 중 해금된 것 하나를 랜덤으로 골라 서서히 나타나게 하고 보호막으로 연결
    private void SpawnShieldObject()
    {
        var candidates = new List<Health>(); // 나타날 수 있는 오브젝트
        foreach (Health h in _hiddenObjects)
            if (h != null && h.enabled && !h.gameObject.activeSelf && h.GetComponent<ConveyorBelt>() == null) candidates.Add(h); // 벨트는 보호막으로 안 나옴

        while (candidates.Count > 0)
        {
            int pick = Random.Range(0, candidates.Count); // 이번에 고른 번호
            Health chosen = candidates[pick]; // 나타날 오브젝트
            candidates.RemoveAt(pick);
            chosen.gameObject.SetActive(true);
            if (!chosen.gameObject.activeInHierarchy) { chosen.gameObject.SetActive(false); continue; } // 부모가 숨겨져 있어서 안 보이면 다른 걸로
            if (chosen.GetComponent<ConveyorRider>() != null) PlaceAtRandomSpot(chosen); // 벨트 없이 나온 왁뿌볼은 맵 아무 데나
            Reveal(chosen);
            ShieldGlow.Attach(chosen); // 누가 봐도 부숴야 할 것처럼 깜빡이며 빛남
            _links.Add(chosen);
            RebuildLinkColors();
            return;
        }
    }

    // 화면 안 빈자리 아무 곳에 놓음 - 보스나 이미 나온 보호막 오브젝트와 안 겹치게 (못 찾으면 원래 자리)
    private void PlaceAtRandomSpot(Health h)
    {
        const float Margin = 0.5f; // 화면 가장자리 여백(유닛)
        const float TopReserve = 1.5f; // 위쪽 체력바 UI 자리(유닛)
        Camera cam = Camera.main; // 화면 범위 기준
        var sr = h.GetComponent<SpriteRenderer>(); // 오브젝트 크기
        if (cam == null || sr == null) return;

        var taken = new List<Bounds>(); // 피해야 할 자리
        if (TryGetComponent(out Renderer bossRenderer)) taken.Add(bossRenderer.bounds);
        foreach (Health link in _links)
            if (link != null && link.gameObject.activeInHierarchy && link.TryGetComponent(out Renderer r)) taken.Add(r.bounds);

        Vector3 offset = sr.bounds.center - h.transform.position; // 위치 -> 그림 중심 차이
        Vector3 extents = sr.bounds.extents; // 그림 반 크기
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect; // 화면 반 크기
        Vector3 c = cam.transform.position; // 화면 중심
        for (int i = 0; i < 40; i++)
        {
            var center = new Vector3( // 그림 중심 후보
                Random.Range(c.x - halfW + extents.x + Margin, c.x + halfW - extents.x - Margin),
                Random.Range(c.y - halfH + extents.y + Margin, c.y + halfH - extents.y - TopReserve),
                sr.bounds.center.z);
            var spot = new Bounds(center, sr.bounds.size); // 놓였을 때 차지할 범위
            if (taken.Exists(t => t.Intersects(spot))) continue;
            h.transform.position = center - offset;
            return;
        }
    }

    // 체력을 채우고 투명한 상태에서 서서히 나타남
    private void Reveal(Health h)
    {
        h.ResetToFull();
        h.FadeIn(shieldFadeSeconds);
    }

    // 연결별 보호막 색(그 오브젝트의 왁스 색)을 다시 채움
    private void RebuildLinkColors()
    {
        _linkColors = new Color[_links.Count];
        _linkOn = new bool[_links.Count];
        for (int i = 0; i < _links.Count; i++)
        {
            Click click = _links[i] != null ? _links[i].GetComponent<Click>() : null; // 왁스 색을 정할 오브젝트 번호
            _linkColors[i] = ObjectManager.Instance != null && click != null ? ObjectManager.Instance.WaxColorOf(click.ObjectIndex) : Color.white;
        }
    }

    // 보스전 중에 부숨 - 보상 주고 슬로우 모션 (다음 맵 해금/균열 연출은 HandleDied가 그대로 이어서 함)
    private void WinBoss()
    {
        _bossWon = true;
        GameStats.BossClears++;
        if (IsFinalBoss) FinalBossCleared = true;
        EndBoss(true);

        int objectIndex = _click != null ? _click.ObjectIndex : -1; // 보상 기준 오브젝트
        if (CurrencyManager.Instance != null)
        {
            if (ObjectManager.Instance != null && objectIndex >= 0)
                CurrencyManager.Instance.AddPieces((long)(ObjectManager.Instance.GetBaseReward(objectIndex) * (double)bossPieceRewardMultiplier));
            if (bossCrystalReward > 0) CurrencyManager.Instance.AddCrystals(bossCrystalReward);
        }
        SaveManager.Instance?.SaveNow();
        BossChallengeUI.PlaySlowMotion();
    }

    // 시간 초과/포기/맵 이동 - 체력이 다시 차고 버튼이 다시 뜸
    private void LoseBoss()
    {
        EndBoss(false);
        _health.ResetToFull();
    }

    // won이면 왁스는 부서지며 흩어지도록 강제 덮기를 잠깐 더 켜둠 (UpdateBoss가 다음 프레임에 끔)
    private void EndBoss(bool won)
    {
        if (ActiveBoss == this) ActiveBoss = null;
        foreach (Health h in _hiddenObjects) // 숨겨뒀던 오브젝트를 다시 서서히 나타나게
        {
            if (h == null || h.gameObject.activeSelf) continue;
            h.gameObject.SetActive(true);
            if (h.enabled) Reveal(h);
        }
        _hiddenObjects.Clear();
        foreach (Health link in _links) ShieldGlow.Detach(link);
        _links.Clear();
        ShieldRatio = 0f;
        ShieldFill = 0f;
        if (_wax != null)
        {
            _wax.SetShield(null, null);
            if (!won) _wax.ForceCover = false;
        }
    }
}
