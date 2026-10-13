using UnityEngine;

// 어느 목록에서 이름을 고를지 - 오브젝트(캐는 대상, ObjectManager) or 무기(장착 도구, WeaponManager)
public enum ObjectNameFieldSource
{
    Object,
    Weapon,
}

// 이 붙은 string 필드는 인스펙터에서 텍스트 입력 대신 드롭다운으로 뜸 (오타 방지용).
// emptyOptionLabel을 주면 그 이름으로 "빈 값" 옵션도 목록 맨 앞에 추가됨 (null이면 항상 하나를 골라야 함)
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
