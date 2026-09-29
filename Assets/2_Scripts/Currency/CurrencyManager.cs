using System;
using System.Collections.Generic;
using UnityEngine;

// 모든 오브젝트가 공통으로 쌓는 단일 통합 화폐("조각")를 관리하는 지갑.
// 예전엔 오브젝트별로 조각이 따로 쌓였지만, 이제는 뭘 부수든 같은 조각으로 합쳐짐
public class CurrencyManager : MonoBehaviour
{
    // 씬 어디서든 CurrencyManager.Instance로 접근하기 위한 싱글톤
    public static CurrencyManager Instance { get; private set; }

    // 테스트용: 켜두면 조각이 항상 최대치로 유지됨 (업그레이드 테스트할 때 조각 모으는 시간 아끼려고).
    // 실제 재화 밸런스를 테스트할 땐 꺼두면 됨
    [SerializeField] private bool debugAlwaysMaxPieces = false;
    private const long DebugMaxPieceAmount = 999_999_999_999_999L; // 테스트용 최대 조각 값 (Q 단위 비용도 감당할 만큼 넉넉하게)
    private const long DebugMaxCrystalAmount = 999_999L; // 디버그 "돈 최대"로 채워줄 결정 개수

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

    // 여러 항목의 비용을 한 번에 확인 + 차감함 (전부 같은 조각이라 amount를 그냥 합산함).
    // 부족하면 아무것도 차감하지 않고 false를 반환
    public bool TrySpendPieces(IReadOnlyList<PieceCost> costs)
    {
        if (debugAlwaysMaxPieces)
            return true; // 테스트 모드에서는 항상 성공 - 잔액이 어차피 최대치로 유지되니 차감할 필요도 없음

        long total = 0; // 요구되는 조각 총합
        foreach (PieceCost cost in costs)
            total += cost.amount;

        if (_pieces < total)
        {
            Debug.Log($"조각 부족: {total}개 필요, 현재 {_pieces}개 보유 (부족분 {total - _pieces}개)");
            return false;
        }

        _pieces -= total;
        OnPiecesChanged?.Invoke(_pieces);
        return true;
    }

    // 테스트용 - 보유 중인 조각과 결정을 0으로
    public void ResetAll()
    {
        _pieces = 0;
        OnPiecesChanged?.Invoke(0);
        SetCrystals(0);
    }

    // 테스트용 - 모든 업그레이드 노드/오브젝트 해금/획득량 업그레이드를 전부 살 수 있을 만큼 조각을 채워줌
    public void MaxAllDebug()
    {
        if (ObjectManager.Instance == null) return;

        long required = 0; // 모든 비용의 합계

        if (UpgradeManager.Instance != null)
        {
            foreach (UpgradeManager.UpgradeNode node in UpgradeManager.Instance.Nodes)
            {
                for (int level = 0; level < node.maxLevel; level++)
                {
                    PieceCost[] costs = node.CostForLevel(level);
                    if (costs == null) continue;
                    foreach (PieceCost cost in costs)
                        required += cost.amount;
                }
            }
        }

        for (int i = 1; i < ObjectManager.Instance.ObjectCount; i++)
        {
            required += ObjectManager.Instance.GetUnlockCost(i);
            required += ObjectManager.Instance.GetTotalGainCost(i);
        }

        if (required > GetPieces())
            SetPieces(required);

        // 결정은 아직 쓰는 곳이 없어서 테스트용으로 넉넉하게만 채움
        if (_crystals < DebugMaxCrystalAmount)
            SetCrystals(DebugMaxCrystalAmount);
    }
}
