using UnityEngine;

public class StringDropdownAttribute : PropertyAttribute
{
    public string filePath;

    public StringDropdownAttribute(string filePath)
    {
        this.filePath = filePath;
    }
}