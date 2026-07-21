using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class EditorSection
{
    private bool _isExpanded;
    private readonly string _title;
    private readonly GUIStyle _boxStyle;
    private readonly GUIStyle _headerStyle;

    public EditorSection(string title, bool defaultExpanded, GUIStyle boxStyle, GUIStyle headerStyle)
    {
        _title = title;
        _isExpanded = defaultExpanded;
        _boxStyle = boxStyle;
        _headerStyle = headerStyle;
    }

    public void Draw(Action content, Color setColor)
    {
        Rect rect = EditorGUILayout.BeginVertical(_boxStyle ?? GUIStyle.none);
        EditorGUI.DrawRect(rect, setColor);
        Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, _headerStyle, GUILayout.ExpandWidth(true));
        EditorGUI.LabelField(headerRect, _title, _headerStyle);
        if (Event.current.type == EventType.MouseDown && headerRect.Contains(Event.current.mousePosition))
        {
            _isExpanded = !_isExpanded;
            Event.current.Use();
        }

        if (!_isExpanded)
        {
            GUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space(10);

        content?.Invoke();

        GUILayout.EndVertical();
    }
}