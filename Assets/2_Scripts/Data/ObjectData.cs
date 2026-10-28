using System;
using UnityEngine;

[Serializable]
public class ObjectData
{
    public int tier; // 1부터
    public int indexInTier; // 티어 안 순번, 1부터
    public float hpMultiplier = 1f;
    public string objectName;
    public Color pileColor;
    public AudioClip[] clickSounds;
    public AudioClip breakSound;
    public Sprite[] healthStages;

    public ObjectData(int tier, int indexInTier, string objectName, Color pileColor)
    {
        this.tier = tier;
        this.indexInTier = indexInTier;
        this.objectName = objectName;
        this.pileColor = pileColor;
    }
}
