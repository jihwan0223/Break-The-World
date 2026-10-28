using System;

[Serializable]
public class PieceCost
{
    public long amount;
    public bool isCrystal;

    public PieceCost(long amount, bool isCrystal = false)
    {
        this.amount = amount;
        this.isCrystal = isCrystal;
    }
}
