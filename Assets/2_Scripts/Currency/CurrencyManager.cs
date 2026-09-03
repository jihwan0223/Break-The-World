using System;
using System.Collections.Generic;
using UnityEngine;

// 오브젝트별 조각을 각각 별개의 화폐로 관리하는 지갑. "골드/파편" 같은 단일 통합 화폐는 없고,
// 항상 "몇 번 오브젝트의 조각이 몇 개"인지로 관리됨 (업그레이드/오브젝트 해금 비용도 전부 이 단위)
public class CurrencyManager : MonoBehaviour
{
    // 씬 어디서든 CurrencyManager.Instance로 접근하기 위한 싱글톤
    public static CurrencyManager Instance { get; private set; }

    // 테스트용: 켜두면 모든 조각이 항상 최대치로 유지됨 (업그레이드 테스트할 때 조각 모으는 시간 아끼려고).
    // 실제 재화 밸런스를 테스트할 땐 꺼두면 됨
    [SerializeField] private bool debugAlwaysMaxPieces = true;
    private const long DebugMaxPieceAmount = 999_999_999_999_999L; // 테스트용 최대 조각 값 (Q 단위 비용도 감당할 만큼 넉넉하게)

    private readonly Dictionary<int, long> _pieces = new Dictionary<int, long>(); // objectIndex -> 보유 조각 개수

    // 특정 오브젝트의 조각 보유량이 바뀔 때마다 (objectIndex, 새 보유량) 전달 - UI 등이 구독
    public event Action<int, long> OnPiecesChanged;

    // 조각을 "새로 얻을" 때마다 (objectIndex, 더해진 양) 전달 - GameSessionManager가 이번 판 획득량을 집계할 때 사용.
    // OnPiecesChanged와 다른 점: (1) 지급(증가)일 때만 발행 (2) 새 잔액이 아니라 이번에 더해진 양을 넘김.
    // 그래서 debugAlwaysMaxPieces가 켜져 있어(잔액이 항상 최대치라 차이로 계산 불가)도 정확하게 잡힘
    public event Action<int, long> OnPiecesGained;

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

    public long GetPieces(int objectIndex)
    {
        // 테스트 모드에서는 한 번도 안 얻어본 조각 종류도 보유량 "표시"가 실제 구매 판정(TrySpendPieces)과 일치하도록 항상 최대치로 봄
        if (debugAlwaysMaxPieces) return DebugMaxPieceAmount;

        return _pieces.TryGetValue(objectIndex, out long amount) ? amount : 0L;
    }

    public void AddPieces(int objectIndex, long amount)
    {
        long newValue = debugAlwaysMaxPieces ? DebugMaxPieceAmount : GetPieces(objectIndex) + amount;
        _pieces[objectIndex] = newValue;
        OnPiecesChanged?.Invoke(objectIndex, newValue);

        if (amount > 0)
            OnPiecesGained?.Invoke(objectIndex, amount); // 이번 판 획득량 집계용 (GameSessionManager)
    }

    // 저장 파일을 불러올 때 값을 직접 세팅하기 위한 함수 (증감이 아니라 절대값 지정)
    public void SetPieces(int objectIndex, long amount)
    {
        long newValue = debugAlwaysMaxPieces ? DebugMaxPieceAmount : amount;
        _pieces[objectIndex] = newValue;
        OnPiecesChanged?.Invoke(objectIndex, newValue);
    }

    // 여러 종류의 조각을 동시에 요구하는 비용을 한 번에 확인 + 차감함.
    // 하나라도 부족하면 아무것도 차감하지 않고 false를 반환 (전부 충분할 때만 전부 차감)
    public bool TrySpendPieces(IReadOnlyList<PieceCost> costs)
    {
        if (debugAlwaysMaxPieces)
            return true; // 테스트 모드에서는 항상 성공 - 잔액이 어차피 최대치로 유지되니 차감할 필요도 없음

        bool enough = true; // 하나라도 부족하면 false로 바뀜 - 부족한 조각을 전부 로그로 남기기 위해 바로 return하지 않고 끝까지 훑음
        foreach (PieceCost cost in costs)
        {
            long have = GetPieces(cost.objectIndex);
            if (have < cost.amount)
            {
                string objectName = ObjectManager.Instance != null ? ObjectManager.Instance.GetObjectAt(cost.objectIndex).objectName : $"오브젝트 {cost.objectIndex}";
                Debug.Log($"조각 부족: {objectName} 조각 {cost.amount}개 필요, 현재 {have}개 보유 (부족분 {cost.amount - have}개)");
                enough = false;
            }
        }
        if (!enough) return false;

        foreach (PieceCost cost in costs)
        {
            long newValue = GetPieces(cost.objectIndex) - cost.amount;
            _pieces[cost.objectIndex] = newValue;
            OnPiecesChanged?.Invoke(cost.objectIndex, newValue);
        }

        return true;
    }

    // 지금 등록된 모든 조각 보유량을 (objectIndex, amount) 쌍으로 반환 - 저장할 때 사용
    public IEnumerable<KeyValuePair<int, long>> GetAllPieces() => _pieces;
}
