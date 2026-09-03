using System;
using System.Collections.Generic;
using UnityEngine;

// 한 판(런)의 흐름을 관리하는 매니저. 씬이 켜지면 "시작 전" 상태로 대기하다가, 게임 시작 버튼을 누르면
// 3-2-1 카운트다운 후 고정 시간짜리 타이머가 돌기 시작하고, 타이머가 다 닳거나 중도포기를 누르면 판이 끝남.
// 조각/업그레이드 진행도는 판이 끝나도 그대로 유지됨 (판 종료 시 SaveManager로 저장만 함) -
// 타이머는 그냥 "이 시간 안에 얼마나 벌었나"를 가르는 장치일 뿐, 재시작해도 손해가 없음.
public class GameSessionManager : MonoBehaviour
{
    // 씬 어디서든 GameSessionManager.Instance로 접근하기 위한 싱글톤
    public static GameSessionManager Instance { get; private set; }

    // 런 상태
    public enum State
    {
        PreStart,  // 시작 전 - 화면 중앙 시작 버튼, 오브젝트 클릭 비활성
        Countdown, // 3-2-1 카운트다운 중 - HUD 숨김, 클릭은 아직 비활성
        Running,   // 진행 중 - 타이머가 닳는 중, 클릭 활성
        Ended,     // 종료 - 결과창 표시 중
    }

    [SerializeField] private float baseRunDurationSeconds = 10f; // 한 판의 기본 제한시간(초) - 나중에 업그레이드로 연장할 수 있게 RunDurationSeconds에서 따로 계산함
    [SerializeField] private float countdownSeconds = 3f; // 게임 시작 버튼을 누른 뒤 타이머가 실제로 시작되기까지의 카운트다운 길이(초)

    private State _state = State.PreStart; // 현재 런 상태
    private float _timeRemaining; // 이번 판에 남은 시간(초) - Running 상태에서만 줄어듦
    private float _countdownRemaining; // 카운트다운에 남은 시간(초) - Countdown 상태에서만 줄어듦
    private readonly Dictionary<int, long> _piecesGainedThisRun = new Dictionary<int, long>(); // 이번 판에 오브젝트별로 새로 얻은 조각 누적량 (결과창 표시용)

    // 상태가 바뀔 때마다 새 상태를 전달 - GameSessionUI가 구독해서 화면(시작 버튼/카운트다운/타이머/결과창)을 전환함
    public event Action<State> OnStateChanged;

    // 런이 진행/카운트다운 중이면 true, 시작 전이면 false - 조각 표시(PieceUI)/좌상단 버튼 줄(SidePanelUI) 같은
    // "판과 무관한 HUD"가 스스로 숨고 보이는 데 사용. static이라 구독자가 Instance 생성 시점을 신경 안 써도 됨
    // (UpgradeTreeUI.OnTreeToggled와 같은 방식)
    public static event Action<bool> OnHudHiddenChanged;

    public State CurrentState => _state;
    public bool IsRunActive => _state == State.Running; // Click.cs가 클릭/자동클릭/자동채굴을 돌릴지 판단할 때 사용 (카운트다운 중엔 false)
    public float RunTimeRemaining => _timeRemaining; // 결과창/타이머 UI가 읽어감
    public float RunDurationSeconds => baseRunDurationSeconds; // TODO(B단계): UpgradeManager의 "시간 연장" 업그레이드 값을 여기서 더할 예정
    public float RunTimeNormalized => RunDurationSeconds > 0f ? Mathf.Clamp01(_timeRemaining / RunDurationSeconds) : 0f; // 타이머 바 fill 비율(0~1)
    public int CountdownNumber => Mathf.Max(1, Mathf.CeilToInt(_countdownRemaining)); // 카운트다운에 표시할 숫자 (3 -> 2 -> 1)

    void Awake()
    {
        // 씬에 GameSessionManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        // 이번 판 획득량 집계를 위해 조각이 새로 들어올 때마다 알림을 받음 (CurrencyManager가 이 매니저보다 늦게 Awake될 수 있어 Start에서 구독)
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnPiecesGained += HandlePiecesGained;

        SetState(State.PreStart); // 명시적으로 한 번 발행해서 UI가 초기 화면(시작 버튼)을 그리게 함
    }

    void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnPiecesGained -= HandlePiecesGained;
    }

    void Update()
    {
        if (_state == State.Countdown)
        {
            // 카운트다운은 Time.timeScale과 무관하게 항상 흐르도록 unscaled 사용
            _countdownRemaining -= Time.unscaledDeltaTime;

            if (_countdownRemaining <= 0f)
                SetState(State.Running); // 카운트다운 끝 -> 타이머 시작 + 클릭 활성

            return;
        }

        if (_state != State.Running)
            return;

        // 업그레이드 창이 열려 Time.timeScale이 0이면 deltaTime도 0이라 타이머가 자연스럽게 멈춤 (의도된 동작 - 창 보는 동안은 시간이 안 감)
        _timeRemaining -= Time.deltaTime;

        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            EndRun();
        }
    }

    // 게임 시작 버튼 - 시작 전 화면에서도, 결과창의 "재시작"에서도 이걸 호출해서 새 판을 시작함 (먼저 카운트다운으로 진입)
    public void StartRun()
    {
        _timeRemaining = RunDurationSeconds;
        _countdownRemaining = countdownSeconds;
        _piecesGainedThisRun.Clear();
        SetState(State.Countdown);
    }

    // 중도포기 버튼 - 타이머를 즉시 끝내고 결과창으로 넘어감 (지금까지 얻은 조각은 그대로 저장됨)
    public void GiveUp()
    {
        if (_state != State.Running)
            return;

        _timeRemaining = 0f;
        EndRun();
    }

    // 결과창 "나가기" - 시작 전 상태로 돌아감 (조각은 이미 EndRun에서 저장됨)
    public void ExitToPreStart()
    {
        SetState(State.PreStart);
    }

    // 타이머 종료(0초 도달 또는 중도포기) 공통 처리 - 결과창을 띄우고 진행도를 즉시 저장
    private void EndRun()
    {
        SetState(State.Ended);
        SaveManager.Instance?.SaveNow();
    }

    // CurrencyManager.AddPieces가 조각을 새로 지급할 때마다 호출됨 - 진행 중일 때만 이번 판 집계에 더함
    private void HandlePiecesGained(int objectIndex, long amount)
    {
        if (_state != State.Running || amount <= 0)
            return;

        _piecesGainedThisRun.TryGetValue(objectIndex, out long prev);
        _piecesGainedThisRun[objectIndex] = prev + amount;
    }

    // 결과창에서 "이번 판에 얻은 조각"의 오브젝트별 내역을 그릴 때 사용 - (오브젝트 인덱스, 이번 판 획득량) 목록
    public IEnumerable<KeyValuePair<int, long>> GetPiecesGainedThisRun() => _piecesGainedThisRun;

    // 이번 판에 얻은 조각 총합 (오브젝트 종류 구분 없이 개수만 합산)
    public long GetTotalPiecesGainedThisRun()
    {
        long total = 0; // 누적 합
        foreach (KeyValuePair<int, long> entry in _piecesGainedThisRun)
            total += entry.Value;
        return total;
    }

    private void SetState(State newState)
    {
        _state = newState;
        OnStateChanged?.Invoke(_state);

        // 시작 전이 아닌 모든 상태(카운트다운/진행/결과)에서는 판과 무관한 HUD를 숨김
        OnHudHiddenChanged?.Invoke(newState != State.PreStart);
    }
}
