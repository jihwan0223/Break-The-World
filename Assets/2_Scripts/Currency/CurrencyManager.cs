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
    private bool _debugRefill; // 디버그 "돈 최대"로 켜지는 자동 충전 모드 - 켜져 있으면 매 프레임 조각을 구매 가능한 최고가만큼 채움

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
        _debugRefill = false;
        _pieces = 0;
        OnPiecesChanged?.Invoke(0);
        SetCrystals(0);
    }

    // 테스트용 - 켜두면 조각/결정이 "지금 살 수 있는 것 중 가장 비싼 가격"보다 적을 때마다 그만큼 다시 채워짐 (ResetAll로 꺼짐)
    public void StartDebugRefill() => _debugRefill = true;

    void LateUpdate()
    {
        if (!_debugRefill) return;

        var (pieces, crystals) = MaxAvailableCost(); // 지금 구매 가능한 것 중 가장 비싼 가격
        if (_pieces < pieces) SetPieces(pieces);
        if (_crystals < crystals) SetCrystals(crystals);
    }

    // 공개된 업그레이드 노드 + 해금 가능한 오브젝트/무기 + 획득량 업그레이드 중 가장 비싼 다음 가격 (조각, 결정 따로)
    private (long pieces, long crystals) MaxAvailableCost()
    {
        long maxPieces = 0, maxCrystals = 0; // 지금까지 찾은 가장 비싼 조각/결정 가격

        UpgradeManager upgrades = UpgradeManager.Instance;
        if (upgrades != null)
        {
            foreach (UpgradeManager.UpgradeNode node in upgrades.Nodes)
            {
                if (!upgrades.IsRevealed(node.id) || upgrades.IsTargetLocked(node.id)) continue;

                PieceCost[] costs = node.CostForLevel(upgrades.GetLevel(node.id)); // 최대 레벨이면 null
                if (costs == null) continue;

                var (pieces, crystals) = Sum(costs);
                maxPieces = Math.Max(maxPieces, pieces);
                maxCrystals = Math.Max(maxCrystals, crystals);
            }
        }

        ObjectManager objects = ObjectManager.Instance;
        if (objects != null)
        {
            for (int i = 1; i < objects.ObjectCount; i++)
            {
                if (objects.IsUnlocked(i))
                    maxPieces = Math.Max(maxPieces, objects.GetNextGainCost(i)); // 최대 레벨이면 -1이라 자동으로 무시됨
                else if (objects.IsUnlockOrderMet(i))
                    maxPieces = Math.Max(maxPieces, objects.GetUnlockCost(i));
            }
        }

        WeaponManager weapons = WeaponManager.Instance;
        if (weapons != null)
        {
            for (int i = 1; i < weapons.WeaponCount; i++)
                if (!weapons.IsUnlocked(i) && weapons.IsUnlockOrderMet(i))
                    maxPieces = Math.Max(maxPieces, weapons.GetUnlockCost(i));
        }

        return (maxPieces, maxCrystals);
    }
}
