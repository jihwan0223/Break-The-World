using System;
using System.Collections.Generic;
using UnityEngine;

// 모든 오브젝트가 공통으로 쌓는 단일 통합 화폐("조각")와 희귀 화폐("결정")를 관리하는 지갑.
public class CurrencyManager : MonoBehaviour
{
    // 씬 어디서든 CurrencyManager.Instance로 접근하기 위한 싱글톤
    public static CurrencyManager Instance { get; private set; }

    // 테스트용: 켜두면 조각이 항상 최대치로 유지됨 (업그레이드 테스트할 때 조각 모으는 시간 아끼려고).
    // 실제 재화 밸런스를 테스트할 땐 꺼두면 됨
    [SerializeField] private bool debugAlwaysMaxPieces = false;
    private const long DebugMaxPieceAmount = 999_999_999_999_999L; // 테스트용 최대 조각 값 (Q 단위 비용도 감당할 만큼 넉넉하게)

    private long _pieces; // 보유 중인 통합 조각 개수
    private long _crystals; // 보유 중인 결정 개수 (오브젝트 처치 시 확률로 나오는 희귀 재화)

    // 조각 보유량이 바뀔 때마다 새 보유량 전달 - UI 등이 구독
    public event Action<long> OnPiecesChanged;

    // 결정 보유량이 바뀔 때마다 새 보유량 전달 - UI 등이 구독
    public event Action<long> OnCrystalsChanged;

    [SerializeField] private long starterCrystalPieces = 50; // 조각을 누적 이만큼 얻으면 다음에 직접 부순 오브젝트가 첫 결정을 확정으로 떨어뜨림 (커서 줍기를 일찍 열어주려고)

    private long _totalPieces; // 지금까지 얻은 조각 누적 (써도 줄지 않음) - 첫 결정 확정 드랍 시점 판단용
    private bool _everHadCrystal; // 결정을 한 번이라도 얻었는지 - 얻기 전에는 결정 표시를 숨기고, 첫 결정 확정 드랍 대상
    private bool _starterCrystalDropped; // 이번 실행에서 첫 결정을 이미 떨어뜨렸는지 (저장 안 함 - 안 줍고 껐으면 다음 실행에서 다시 떨어짐)

    [SerializeField] private int crystalPityKills = 200; // 결정 천장 - 오브젝트를 이만큼 부수는 동안 결정이 한 번도 안 나오면 이번 처치에 확정으로 나옴
    private int _killsSinceCrystal; // 마지막으로 결정이 나온 뒤 부순 오브젝트 수 (천장 계산용, 저장됨)
    [SerializeField] private long pityStopCrystals = 5; // 결정을 누적 이만큼 얻으면 천장(확정 드랍)을 끔 - 그 뒤로는 확률로만 나옴
    private long _totalCrystals; // 지금까지 얻은 결정 누적 (써도 줄지 않음, 저장됨)

    public long TotalPieces => _totalPieces;
    public bool EverHadCrystal => _everHadCrystal;
    public int KillsSinceCrystal => _killsSinceCrystal;
    public long TotalCrystals => _totalCrystals;

    // 저장 파일을 불러올 때 누적 조각 / 결정 획득 여부 / 천장 카운트를 복원
    public void SetProgress(long totalPieces, bool everHadCrystal, int killsSinceCrystal, long totalCrystals)
    {
        _totalCrystals = totalCrystals;
        _totalPieces = totalPieces;
        _everHadCrystal = everHadCrystal;
        _killsSinceCrystal = killsSinceCrystal;
    }

    // 오브젝트를 하나 부술 때마다 호출 - 결정 없이 천장 횟수를 채웠으면 true (이번에 확정으로 나와야 함)
    // 결정을 pityStopCrystals개 이상 얻은 뒤로는 천장 없음
    public bool CountKillForPity() => ++_killsSinceCrystal >= crystalPityKills && _totalCrystals < pityStopCrystals;

    // 결정이 나왔을 때 호출 - 천장 카운트를 처음부터
    public void ResetCrystalPity() => _killsSinceCrystal = 0;

    // 첫 결정을 지금 확정으로 떨어뜨릴 차례면 true (실행당 한 번만 true)
    public bool TakeStarterCrystal()
    {
        if (_everHadCrystal || _starterCrystalDropped || _totalPieces < starterCrystalPieces)
            return false;
        _starterCrystalDropped = true;
        return true;
    }

    public long GetCrystals() => _crystals;

    public void AddCrystals(long amount)
    {
        if (amount > 0) { _everHadCrystal = true; _totalCrystals += amount; } // 불러오기(SetCrystals)가 아니라 실제로 얻었을 때만
        SetCrystals(_crystals + amount);
    }

    // 저장 파일을 불러올 때 값을 직접 세팅하기 위한 함수
    public void SetCrystals(long amount)
    {
        _crystals = amount;
        OnCrystalsChanged?.Invoke(_crystals);
    }

    void Awake()
    {
        // 씬에 CurrencyManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public long GetPieces()
    {
        // 테스트 모드에서는 보유량 "표시"가 실제 구매 판정(TrySpendPieces)과 일치하도록 항상 최대치로 봄
        return debugAlwaysMaxPieces ? DebugMaxPieceAmount : _pieces;
    }

    public void AddPieces(long amount)
    {
        if (amount > 0) _totalPieces += amount;
        _pieces = debugAlwaysMaxPieces ? DebugMaxPieceAmount : _pieces + amount;
        OnPiecesChanged?.Invoke(_pieces);
    }

    // 저장 파일을 불러올 때 값을 직접 세팅하기 위한 함수 (증감이 아니라 절대값 지정)
    public void SetPieces(long amount)
    {
        _pieces = debugAlwaysMaxPieces ? DebugMaxPieceAmount : amount;
        OnPiecesChanged?.Invoke(_pieces);
    }

    // 차감 없이 이 비용을 지금 낼 수 있는지만 확인 (느낌표 표시 등) - 비용이 없으면 true
    public bool CanAfford(IReadOnlyList<PieceCost> costs)
    {
        if (costs == null) return true;
        var (pieces, crystals) = Sum(costs);
        return (debugAlwaysMaxPieces || _pieces >= pieces) && _crystals >= crystals;
    }

    // 여러 항목의 비용(조각/결정)을 한 번에 확인 + 차감함.
    // 하나라도 부족하면 아무것도 차감하지 않고 false를 반환
    public bool TrySpendPieces(IReadOnlyList<PieceCost> costs)
    {
        var (pieces, crystals) = Sum(costs);

        if (!debugAlwaysMaxPieces && _pieces < pieces)
        {
            Debug.Log($"조각 부족: {pieces}개 필요, 현재 {_pieces}개 보유 (부족분 {pieces - _pieces}개)");
            return false;
        }
        if (_crystals < crystals)
        {
            Debug.Log($"결정 부족: {crystals}개 필요, 현재 {_crystals}개 보유 (부족분 {crystals - _crystals}개)");
            return false;
        }

        // 테스트 모드(debugAlwaysMaxPieces)에서는 조각 잔액이 어차피 최대치로 유지되니 조각은 차감 안 함
        if (!debugAlwaysMaxPieces && pieces > 0)
        {
            _pieces -= pieces;
            OnPiecesChanged?.Invoke(_pieces);
        }
        if (crystals > 0)
            SetCrystals(_crystals - crystals);
        return true;
    }

    // 비용 목록을 조각 합 / 결정 합으로 나눔
    private static (long pieces, long crystals) Sum(IReadOnlyList<PieceCost> costs)
    {
        long pieces = 0, crystals = 0; // 조각 합, 결정 합
        foreach (PieceCost cost in costs)
        {
            if (cost.isCrystal) crystals += cost.amount;
            else pieces += cost.amount;
        }
        return (pieces, crystals);
    }

    // 테스트용 - 보유 중인 조각과 결정을 0으로
    public void ResetAll()
    {
        _pieces = 0;
        OnPiecesChanged?.Invoke(0);
        _totalPieces = 0;
        _everHadCrystal = false;
        _starterCrystalDropped = false;
        _killsSinceCrystal = 0;
        SetCrystals(0);
    }
}
