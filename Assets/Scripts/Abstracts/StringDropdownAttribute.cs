using UnityEngine;

public class StringDropdownAttribute : PropertyAttribute
{
    public string filePath;
    public string listFieldName;

    /// <summary>
    /// Use filePath OR listFieldName, or specify explicitly.
    /// </summary>
    public StringDropdownAttribute(string filePath = null, string listFieldName = null)
    {
        this.filePath = filePath;
        this.listFieldName = listFieldName;
    }
}