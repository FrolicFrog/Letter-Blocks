using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "Frolic Frog/Level Data", order = 1)]
public class LevelData : ScriptableObject
{
    public int hearts = 3;
    public int LevelNumber,rows,columns,height,width;
    public float bottomGridSize=.6f;
    public Material categoryMaterial;
    public List<string> words = new();
    public List<Vector2Int> excludedChar = new();
    public List<KeyValueGroup<string,List<string>>> wordCategory = new();
    public List<KeyValueGroup<Vector2Int, string>> cellCategory = new(), cellTexts = new();
    public List<KeyValueGroup<string,Material>> categoryColors =new();
    public List<KeyValueGroup<string,List<Vector2Int>>> wordPositions = new();

}