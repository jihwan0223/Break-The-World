using System;

// 조각 하나의 종류+개수 쌍. 업그레이드/오브젝트 해금 비용이 한 번에 여러 종류의 조각을 동시에 요구할 수 있어서
// 비용을 항상 이 배열로 표현함 (조각 1종류만 필요하면 배열 길이가 1)
[Serializable]
public class PieceCost
{
    public int objectIndex; // 어떤 오브젝트의 조각인지 (ObjectManager 리스트 인덱스)
    public long amount; // 필요한 개수

    public PieceCost(int objectIndex, long amount)
    {
        this.objectIndex = objectIndex;
        this.amount = amount;
    }
}
