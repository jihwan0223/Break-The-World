using System;

// 비용 한 줄 = 조각 또는 결정 몇 개. 업그레이드 노드는 여러 줄을 동시에 요구할 수 있어서 비용을 항상 이 배열로 표현함
[Serializable]
public class PieceCost
{
    public long amount; // 필요한 개수
    public bool isCrystal; // true면 결정, false면 조각으로 결제

    public PieceCost(long amount, bool isCrystal = false)
    {
        this.amount = amount;
        this.isCrystal = isCrystal;
    }
}
