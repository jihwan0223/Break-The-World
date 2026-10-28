using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ObjectNameFieldAttribute))]
public class ObjectNameFieldDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var attr = (ObjectNameFieldAttribute)attribute;
        bool isWeapon = attr.source == ObjectNameFieldSource.Weapon;
        int count = isWeapon ? WeaponManager.StaticWeaponCount : ObjectManager.StaticObjectCount;
        bool hasEmpty = !string.IsNullOrEmpty(attr.emptyOptionLabel);
        int offset = hasEmpty ? 1 : 0;

        var options = new string[count + offset];
        if (hasEmpty) options[0] = attr.emptyOptionLabel;
        for (int i = 0; i < count; i++)
            options[offset + i] = isWeapon ? WeaponManager.StaticWeaponNameAt(i) : ObjectManager.StaticObjectNameAt(i);

        string current = property.stringValue;
        int currentIndex = hasEmpty && string.IsNullOrEmpty(current) ? 0 : System.Array.IndexOf(options, current);
        if (currentIndex < 0) currentIndex = 0;

        EditorGUI.BeginProperty(position, label, property);
        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, options);
        if (newIndex != currentIndex)
            property.stringValue = (hasEmpty && newIndex == 0) ? "" : options[newIndex];
        EditorGUI.EndProperty();
    }
}
