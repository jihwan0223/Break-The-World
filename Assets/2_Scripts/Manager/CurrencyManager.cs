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

    public long GetCrystals() => _crystals;

    public void AddCrystals(long amount) => SetCrystals(_crystals + amount);

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
        _pieces = debugAlwaysMaxPieces ? DebugMaxPieceAmount : _pieces + amount;
        OnPiecesChanged?.Invoke(_pieces);
    }

    // 저장 파일을 불러올 때 값을 직접 세팅하기 위한 함수 (증감이 아니라 절대값 지정)
    public void SetPieces(long amount)
    {
        _pieces = debugAlwaysMaxPieces ? DebugMaxPieceAmount : amount;
        OnPiecesChanged?.Invoke(_pieces);
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
        SetCrystals(0);
    }
}
