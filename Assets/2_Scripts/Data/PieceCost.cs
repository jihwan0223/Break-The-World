using System;

[Serializable]
public class PieceCost
{
    public long amount;
    public bool isCrystal;
    public bool isStarShard; // 별조각으로 결제 (환생 업그레이드)

    public PieceCost(long amount, bool isCrystal = false, bool isStarShard = false)
    {
        this.amount = amount;
        this.isCrystal = isCrystal;
        this.isStarShard = isStarShard;
    }
}
