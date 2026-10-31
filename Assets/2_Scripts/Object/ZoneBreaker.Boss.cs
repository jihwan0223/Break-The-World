using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class ZoneBreaker
{
    [Header("보스전")]
    [SerializeField] private float bossTimeLimit = 30f; // 초
    [SerializeField] private float bossPieceRewardMultiplier = 10f; // 기본 보상 × 이 값
    [SerializeField] private int bossCrystalReward = 3;
    [SerializeField] private float shieldPerLink = 0.15f;
    [SerializeField] private float maxShield = 0.75f;
    [SerializeField] private float[] shieldSpawnTimes = { 25f, 10f }; // 남은 시간(초)이 이 값이 될 때마다 보호막 오브젝트 등장
    [SerializeField] private bool repeatBossForTest = false; // 테스트용 - 출시 전에 끌 것
    [SerializeField] private float shieldFadeSeconds = 0.8f; // 초

    public static bool FinalBossCleared; // 저장됨
    public static ZoneBreaker ActiveBoss { get; private set; }
    public static event System.Action<ZoneBreaker> BossFailed;
    private static readonly List<ZoneBreaker> _bosses = new List<ZoneBreaker>();

    private readonly List<Health> _links = new List<Health>();
    private Color[] _linkColors;
    private bool[] _linkOn;
    private float _bossTimeLeft; // 초
    private bool _bossWon; // 다음 맵 해금 연출이 끝나기 전에도 클리어로 치려고
    private WaxCoating _wax;
    private readonly List<Health> _hiddenObjects = new List<Health>();
    private int _nextSpawn;
    private readonly HashSet<Health> _removing = new HashSet<Health>();

    // 플레이 시작 때 도메인 리로드를 안 해서 직접 비움
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
        if (ActiveBoss == this) ActiveBoss = null;
    }

    public bool BossActive => ActiveBoss == this;
    public static bool IsShieldObject(Health h) => ActiveBoss != null && h != null && ActiveBoss._links.Contains(h);
    private bool IsFinalBoss => ZoneManager.Instance != null && unlockZone >= ZoneManager.Instance.ZoneCount;
    public bool BossCleared => _bossWon || (IsFinalBoss ? FinalBossCleared : ZoneManager.Instance == null || ZoneManager.Instance.UnlockedMaxZone >= unlockZone);
    public bool BossAllowsDamage => BossCleared || BossActive;
    public float BossTimeLeft => _bossTimeLeft;
    public float BossTimeRatio => bossTimeLimit > 0f ? Mathf.Clamp01(_bossTimeLeft / bossTimeLimit) : 0f;
    public float BossHpRatio => _health != null && _health.MaxHP > 0 ? (float)_health.CurrentHP / _health.MaxHP : 0f;
    public float ShieldRatio { get; private set; }
    public float ShieldFill { get; private set; }
    public float BossDamageMultiplier => 1f - ShieldRatio;

    public static ZoneBreaker ChallengeableBoss()
    {
        if (ActiveBoss != null) return null;
        foreach (ZoneBreaker boss in _bosses)
            if (boss != null && boss.CanChallenge) return boss;
        return null;
    }

    private bool CanChallenge => _health != null && _health.enabled && !_health.IsDead && (!BossCleared || repeatBossForTest)
        && !(ZoneManager.Instance != null && ZoneManager.Instance.IsHidden(transform));

    public static void ResetBosses()
    {
        FinalBossCleared = false;
        foreach (ZoneBreaker boss in _bosses)
            if (boss != null) boss._bossWon = false;
    }

    public void StartBoss()
    {
        if (!CanChallenge || ActiveBoss != null) return;

        ActiveBoss = this;
        _bossTimeLeft = bossTimeLimit;
        _health.ResetToFull();

        _links.Clear();
        _hiddenObjects.Clear();
        _nextSpawn = 0;
        if (zoneRoot != null)
            foreach (Health h in zoneRoot.GetComponentsInChildren<Health>())
                if (h != _health) _hiddenObjects.Add(h);
        foreach (Health h in _hiddenObjects) h.gameObject.SetActive(false);
        RebuildLinkColors();

        if (_wax == null) _wax = GetComponent<WaxCoating>();
        if (_wax != null) _wax.ForceCover = true;
        UpdateShield();
    }

    public void GiveUpBoss()
    {
        if (BossActive) LoseBoss();
    }

    private void UpdateBoss()
    {
        if (!BossActive)
        {
            // 부서지는 순간엔 왁스가 흩어져야 해서 다음 프레임에 끔
            if (_wax != null && _wax.ForceCover && _health != null && _health.IsDead) _wax.ForceCover = false;
            return;
        }
        if (ZoneManager.Instance != null && ZoneManager.Instance.IsHidden(transform)) { LoseBoss(); return; }

        _bossTimeLeft -= Time.deltaTime; // 시간이 멈추면 같이 멈춤
        if (_bossTimeLeft <= 0f) { LoseBoss(); BossFailed?.Invoke(this); return; }

        while (shieldSpawnTimes != null && _nextSpawn < shieldSpawnTimes.Length && _bossTimeLeft <= shieldSpawnTimes[_nextSpawn])
        {
            _nextSpawn++;
            SpawnShieldObject();
        }
        UpdateShield();
    }

    private void UpdateShield()
    {
        float strength = 0f;
        for (int i = 0; i < _links.Count; i++)
        {
            Health link = _links[i];
            _linkOn[i] = link != null && link.gameObject.activeInHierarchy && link.enabled && !link.IsDead;
            if (link != null && link.IsDead && _removing.Add(link)) StartCoroutine(RemoveWhenFaded(link));
            if (_linkOn[i] && link.MaxHP > 0) strength += (float)link.CurrentHP / link.MaxHP;
        }
        ShieldRatio = Mathf.Min(maxShield, strength * shieldPerLink);
        ShieldFill = Mathf.Clamp01(strength / Mathf.Max(1, shieldSpawnTimes != null ? shieldSpawnTimes.Length : 1));
        if (_wax != null) _wax.SetShield(_linkColors, _linkOn);
    }

    private IEnumerator RemoveWhenFaded(Health link)
    {
        while (link != null && link.IsDead && link.RespawnRemaining <= 0f) yield return null;
        if (link != null && BossActive && _links.Contains(link)) link.gameObject.SetActive(false);
        _removing.Remove(link);
    }

    private void SpawnShieldObject()
    {
        var candidates = new List<Health>();
        foreach (Health h in _hiddenObjects)
            if (h != null && h.enabled && !h.gameObject.activeSelf && h.GetComponent<ConveyorBelt>() == null) candidates.Add(h);

        while (candidates.Count > 0)
        {
            int pick = Random.Range(0, candidates.Count);
            Health chosen = candidates[pick];
            candidates.RemoveAt(pick);
            chosen.gameObject.SetActive(true);
            if (!chosen.gameObject.activeInHierarchy) { chosen.gameObject.SetActive(false); continue; }
            if (chosen.GetComponent<ConveyorRider>() != null) PlaceAtRandomSpot(chosen);
            Reveal(chosen);
            ShieldGlow.Attach(chosen);
            _links.Add(chosen);
            RebuildLinkColors();
            return;
        }
    }

    private void PlaceAtRandomSpot(Health h)
    {
        const float Margin = 0.5f; // 월드 유닛
        const float TopReserve = 1.5f; // 월드 유닛, 체력바 자리
        Camera cam = Camera.main;
        var sr = h.GetComponent<SpriteRenderer>();
        if (cam == null || sr == null) return;

        var taken = new List<Bounds>();
        if (TryGetComponent(out Renderer bossRenderer)) taken.Add(bossRenderer.bounds);
        foreach (Health link in _links)
            if (link != null && link.gameObject.activeInHierarchy && link.TryGetComponent(out Renderer r)) taken.Add(r.bounds);

        Vector3 offset = sr.bounds.center - h.transform.position;
        Vector3 extents = sr.bounds.extents;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;
        for (int i = 0; i < 40; i++)
        {
            var center = new Vector3(
                Random.Range(c.x - halfW + extents.x + Margin, c.x + halfW - extents.x - Margin),
                Random.Range(c.y - halfH + extents.y + Margin, c.y + halfH - extents.y - TopReserve),
                sr.bounds.center.z);
            var spot = new Bounds(center, sr.bounds.size);
            if (taken.Exists(t => t.Intersects(spot))) continue;
            h.transform.position = center - offset;
            return;
        }
    }

    private void Reveal(Health h)
    {
        h.ResetToFull();
        h.FadeIn(shieldFadeSeconds);
    }

    private void RebuildLinkColors()
    {
        _linkColors = new Color[_links.Count];
        _linkOn = new bool[_links.Count];
        for (int i = 0; i < _links.Count; i++)
        {
            Click click = _links[i] != null ? _links[i].GetComponent<Click>() : null;
            _linkColors[i] = ObjectManager.Instance != null && click != null ? ObjectManager.Instance.WaxColorOf(click.ObjectIndex) : Color.white;
        }
    }

    private void WinBoss()
    {
        _bossWon = true;
        GameStats.BossClears++;
        if (IsFinalBoss) FinalBossCleared = true;
        EndBoss(true);

        int objectIndex = _click != null ? _click.ObjectIndex : -1;
        if (CurrencyManager.Instance != null)
        {
            if (ObjectManager.Instance != null && objectIndex >= 0)
                CurrencyManager.Instance.AddPieces((long)(ObjectManager.Instance.GetBaseReward(objectIndex) * (double)bossPieceRewardMultiplier));
            if (bossCrystalReward > 0) CurrencyManager.Instance.AddCrystals(bossCrystalReward);
        }
        SaveManager.Instance?.SaveNow();
        BossChallengeUI.PlaySlowMotion();
        if (IsFinalBoss && GameSettings.IsDemo) SidePanelUI.ShowDemoComplete();
    }

    private void LoseBoss()
    {
        EndBoss(false);
        _health.ResetToFull();
    }

    private void EndBoss(bool won)
    {
        if (ActiveBoss == this) ActiveBoss = null;
        foreach (Health h in _hiddenObjects)
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
