using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(StringDropdownAttribute))]
public class StringDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "StringDropdown only works on strings.");
            return;
        }

        StringDropdownAttribute attr = (StringDropdownAttribute)attribute;
        string[] values = null;

        // --- Option A: Read from Script Field/Property ---
        if (!string.IsNullOrEmpty(attr.listFieldName))
        {
            object targetObject = property.serializedObject.targetObject;
            System.Type targetType = targetObject.GetType();

            FieldInfo fieldInfo = targetType.GetField(attr.listFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (fieldInfo != null)
            {
                object fieldValue = fieldInfo.GetValue(targetObject);

                if (fieldValue is IEnumerable<string> stringEnumerable)
                {
                    values = stringEnumerable.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                }
            }
            else
            {
                PropertyInfo propInfo = targetType.GetProperty(attr.listFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (propInfo != null)
                {
                    object propValue = propInfo.GetValue(targetObject, null);
                    if (propValue is IEnumerable<string> stringEnumerable)
                    {
                        values = stringEnumerable.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                    }
                }
            }

            if (values == null)
            {
                EditorGUI.LabelField(position, label.text, $"Field '{attr.listFieldName}' not found or empty.");
                return;
            }
        }
        // --- Option B: Read from File Path ---
        else if (!string.IsNullOrEmpty(attr.filePath))
        {
            string fullPath = Path.Combine(Application.dataPath, attr.filePath);

            if (!File.Exists(fullPath))
            {
                EditorGUI.LabelField(position, label.text, $"Missing file: {attr.filePath}");
                return;
            }

            values = File.ReadAllLines(fullPath)
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .ToArray();
        }

        // Safety check if no values were populated
        if (values == null || values.Length == 0)
        {
            EditorGUI.LabelField(position, label.text, "Dropdown list is empty.");
            return;
        }

        // Draw the Popup
        int index = System.Array.IndexOf(values, property.stringValue);
        if (index < 0) index = 0;

        int newIndex = EditorGUI.Popup(position, label.text, index, values);
        property.stringValue = values[newIndex];
    }
}