using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

[CustomPropertyDrawer(typeof(StringDropdownAttribute))]
public class StringDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "StringDropdown only works on strings");

            return;
        }

        StringDropdownAttribute attr =
            (StringDropdownAttribute)attribute;

        string fullPath = Path.Combine(Application.dataPath, attr.filePath);

        if (!File.Exists(fullPath))
        {
            EditorGUI.LabelField(position, label.text,
                $"Missing file: {attr.filePath}");
            return;
        }

        string[] values = File.ReadAllLines(fullPath)
                              .Where(x => !string.IsNullOrWhiteSpace(x))
                              .ToArray();

        int index = System.Array.IndexOf(values, property.stringValue);

        if (index < 0)
            index = 0;

        int newIndex = EditorGUI.Popup(position, label.text, index, values);

        if (values.Length > 0)
            property.stringValue = values[newIndex];
    }
}