using System;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    // 테스트용 - 조각을 항상 최대로
    [SerializeField] private bool debugAlwaysMaxPieces = false;
    private const long DebugMaxPieceAmount = 999_999_999_999_999L;

    private long _pieces;
    private long _crystals;

    public event Action<long> OnPiecesChanged;

    public event Action<long> OnCrystalsChanged;

    [SerializeField] private long starterCrystalPieces = 50; // 누적 이만큼 얻으면 첫 결정 확정 드랍

    private long _totalPieces;
    private bool _everHadCrystal;
    private bool _starterCrystalDropped; // 저장 안 함

    [SerializeField] private int crystalPityKills = 200; // 결정 없이 이만큼 부수면 확정 드랍
    private int _killsSinceCrystal;
    [SerializeField] private long pityStopCrystals = 5; // 누적 이만큼 얻으면 천장 끔
    private long _totalCrystals;

    public long TotalPieces => _totalPieces;
    public bool EverHadCrystal => _everHadCrystal;
    public int KillsSinceCrystal => _killsSinceCrystal;
    public long TotalCrystals => _totalCrystals;

    public void SetProgress(long totalPieces, bool everHadCrystal, int killsSinceCrystal, long totalCrystals)
    {
        _totalCrystals = totalCrystals;
        _totalPieces = totalPieces;
        _everHadCrystal = everHadCrystal;
        _killsSinceCrystal = killsSinceCrystal;
    }

    public bool CountKillForPity() => ++_killsSinceCrystal >= crystalPityKills && _totalCrystals < pityStopCrystals;

    public void ResetCrystalPity() => _killsSinceCrystal = 0;

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
        if (amount > 0) { _everHadCrystal = true; _totalCrystals += amount; }
        SetCrystals(_crystals + amount);
    }

    public void SetCrystals(long amount)
    {
        _crystals = amount;
        OnCrystalsChanged?.Invoke(_crystals);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public long GetPieces()
    {
        return debugAlwaysMaxPieces ? DebugMaxPieceAmount : _pieces;
    }

    public void AddPieces(long amount)
    {
        if (amount > 0) _totalPieces += amount;
        _pieces = debugAlwaysMaxPieces ? DebugMaxPieceAmount : _pieces + amount;
        OnPiecesChanged?.Invoke(_pieces);
    }

    public void SetPieces(long amount)
    {
        _pieces = debugAlwaysMaxPieces ? DebugMaxPieceAmount : amount;
        OnPiecesChanged?.Invoke(_pieces);
    }

    public bool CanAfford(IReadOnlyList<PieceCost> costs)
    {
        if (costs == null) return true;
        var (pieces, crystals) = Sum(costs);
        return (debugAlwaysMaxPieces || _pieces >= pieces) && _crystals >= crystals;
    }

    // 하나라도 부족하면 아무것도 차감하지 않음
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

        // 테스트 모드에선 조각 차감 안 함
        if (!debugAlwaysMaxPieces && pieces > 0)
        {
            _pieces -= pieces;
            OnPiecesChanged?.Invoke(_pieces);
        }
        if (crystals > 0)
            SetCrystals(_crystals - crystals);
        return true;
    }

    private static (long pieces, long crystals) Sum(IReadOnlyList<PieceCost> costs)
    {
        long pieces = 0, crystals = 0;
        foreach (PieceCost cost in costs)
        {
            if (cost.isCrystal) crystals += cost.amount;
            else pieces += cost.amount;
        }
        return (pieces, crystals);
    }

    public void ResetAll()
    {
        _pieces = 0;
        OnPiecesChanged?.Invoke(0);
        _totalPieces = 0;
        _everHadCrystal = false;
        _starterCrystalDropped = false;
        _killsSinceCrystal = 0;
        _totalCrystals = 0;
        SetCrystals(0);
    }
}
