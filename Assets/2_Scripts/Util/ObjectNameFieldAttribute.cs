using UnityEngine;

public enum ObjectNameFieldSource
{
    Object,
    Weapon,
}

// 인스펙터에서 드롭다운으로 표시
public class ObjectNameFieldAttribute : PropertyAttribute
{
    public readonly ObjectNameFieldSource source;
    public readonly string emptyOptionLabel;

    public ObjectNameFieldAttribute(ObjectNameFieldSource source = ObjectNameFieldSource.Object, string emptyOptionLabel = "전체 (전역)")
    {
        this.source = source;
        this.emptyOptionLabel = emptyOptionLabel;
    }
}
