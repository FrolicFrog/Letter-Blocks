using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelManager : Manager<LevelManager>
{
    [Header("LEVELS")]
    [SerializeField] private MinMax<int> OriginalLvls = new MinMax<int>(1, 100);
    [SerializeField] private MinMax<int> FakeLvls = new MinMax<int>(1, 100);

    [Header("REFERENCES")]
    [SerializeField] private TopGridManager gridManager;
    [SerializeField] private BottomGridManager letterGridManager;
    [SerializeField] private GameObject categoryHeading;
    [SerializeField] private Transform categoryHeadingParent;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private GameObject heartPrefab;
    [SerializeField] private Transform heartParent;
    [SerializeField] private Material borderMaterial,frostMaterial;
    [SerializeField] private List<KeyValueGroup<Material, Sprite>> colorSprite;

    [HideInInspector] public int TestLevelToLoad = 1;
    private LevelData _LevelData;

    public HashSet<Vector2Int> excludedChar = new(),hiddenChar = new();
    private Dictionary<Material, Sprite> _colorSprite = new(); //Do not clear
        
    public Dictionary<Vector2Int, int> freezedChar = new();
    public Dictionary<Vector2Int, string> cellCategory = new(), cellTexts = new();
    public Dictionary<string, Material> categoryColors = new();
    public Dictionary<string, List<Vector2Int>> wordPositions = new();
    public Dictionary<Vector2Int, KeyValueGroup<string, string>> charDirection = new(), charStorage = new();
    public Dictionary<Vector2Int, List<Vector2Int>> chainedLetters = new();
    public Dictionary<string, List<string>> wordCategory = new();


    [HideInInspector] public int hearts;
   
    private int _CurrentLevelNumber;
    public int CurLevelNumber => _CurrentLevelNumber;
    public LevelData CurLvlData => _LevelData;

    public override void Initialize()
    {
        int CurrentLevel = GameManager.Instance.IsTestMode ? TestLevelToLoad : PlayerPrefs.GetInt("LastLevel", 1);
        _LevelData = Resources.Load<LevelData>($"Levels/{CurrentLevel}");
        _CurrentLevelNumber = CurrentLevel;

        if ((_LevelData == null || _CurrentLevelNumber > OriginalLvls.Max) && !GameManager.Instance.IsTestMode)
        {
            int LevelNumber = FakeLvls.GetRandom();
            _LevelData = Resources.Load<LevelData>($"Levels/{LevelNumber}");
        }
        levelText.text = "Level: "+_LevelData.LevelNumber;
        DataSetup();
        LoadInScene();
        base.Initialize();
    }

    void DataSetup()
    {
        gridManager.rows = _LevelData.rows;
        gridManager.columns = _LevelData.columns;
        letterGridManager.height = _LevelData.height;
        letterGridManager.width = _LevelData.width;
        letterGridManager.manualGridScale = _LevelData.bottomGridSize;
        hearts = _LevelData.hearts;
        excludedChar = _LevelData.excludedChar.ToHashSet();
        cellCategory = _LevelData.cellCategory.ToDictionary(item => item.Key, item => item.Value);
        cellTexts = _LevelData.cellTexts.ToDictionary(item => item.Key, item => item.Value);
        categoryColors = _LevelData.categoryColors.ToDictionary(item => item.Key, item => item.Value);

        foreach (var item in CurLvlData.wordPositions)
        {
            wordPositions[item.Key] = item.Value;
        }
  
        foreach (var item in CurLvlData.wordCategory)
        {
            wordCategory[item.Key] = item.Value;
        }



        foreach (var item in colorSprite)
        {
            _colorSprite[item.Key] = item.Value;
        }
    }
    void LoadInScene()
    {
        gridManager.CreateChildren();
        gridManager.ArrangeChildren();
        letterGridManager.CreateChildren();
        letterGridManager.ArrangeChildren();
        ManageWords();
        ManageAlphabets();
        SetUIElements();
    }

    void SetUIElements()
    {
        foreach (var category in categoryColors.Keys)
        {
           var heading= Instantiate(categoryHeading, categoryHeadingParent);
            heading.GetComponent<Image>().sprite = _colorSprite[categoryColors[category]];
            heading.GetComponentInChildren<TextMeshProUGUI>().text = category;
        }
        for(int i = 0;i<hearts;i++)
        {
            Instantiate(heartPrefab, heartParent);
        }
    }
    void ManageWords()
    {
        for (int row = 0; row < gridManager.rows; row++)
        {
            for (int col = 0; col < gridManager.columns; col++)
            {
                Vector2Int key = new Vector2Int(row, col);
                int linearIndex = key.x * gridManager.columns + key.y;
                var gridChild = gridManager.transform.GetChild(linearIndex);

                if (cellTexts.ContainsKey(key))
                {
                    var letterbox = Instantiate(gridManager.squareSlot, gridChild);
                    letterbox.GetComponent<MeshRenderer>().material = categoryColors[cellCategory[key]];
                   

                    if (!excludedChar.Contains(key))
                    {
                        letterbox.GetComponentInChildren<TextMeshPro>().text = cellTexts[key];
                        letterbox.GetComponent<MeshRenderer>().material = categoryColors[cellCategory[key]];
                    }
                    else
                    {
                        letterbox = ReplaceGameObject(letterbox, gridManager.emptyTile);
                        var mats = letterbox.GetComponent<MeshRenderer>().materials;
                        mats[0] = categoryColors[cellCategory[key]];
                        mats[1] = borderMaterial;
                        letterbox.GetComponent<MeshRenderer>().materials = mats;
                        if (row > gridManager.rows-4)
                        {
                            letterbox.GetComponent<MeshRenderer>().materials[1].color = Color.white;
                        }
                        else
                        {
                            letterbox.GetComponent<MeshRenderer>().materials[1].color = mats[0].color;
                        }

                    }
                    letterbox.transform.localPosition = Vector3.zero;
                  
                }

            }
        }

    }
    void ManageAlphabets()
    {
        LetterController.column = letterGridManager.width;
        for (int height = 0; height < letterGridManager.height; height++)
        {
            for (int width = 0; width < letterGridManager.width; width++)
            {
                Vector2Int key = new Vector2Int(height, width);
                int linearIndex = key.x * letterGridManager.width + key.y;
                var gridChild = letterGridManager.transform.GetChild(linearIndex);
             
                if(charStorage.ContainsKey(key))
                {
                    var letterStorage = Instantiate(letterGridManager.squareGarage, gridChild);
                    var lc = letterStorage.AddComponent<LetterConstriant>();
                    letterStorage.transform.localPosition = Vector3.zero;
                    lc.constraint = ConstraintType.Storage;
                    lc.word = charStorage[key].Key;
                    if (charStorage[key].Value == "→")
                    {
                        lc.direction = Vector3.right;
                        lc.slotIndex = key.x * letterGridManager.width + key.y+1;
                    }
                    else if (charStorage[key].Value == "↑")
                    {
                        lc.direction = Vector3.forward;
                        lc.slotIndex = (key.x -1 )* letterGridManager.width + key.y;
                    }
                    else if (charStorage[key].Value == "↓")
                    {
                        lc.direction = Vector3.back;
                        lc.slotIndex = (key.x + 1) * letterGridManager.width + key.y;
                    }
                    else if (charStorage[key].Value == "←")
                    {
                        lc.direction = Vector3.left;
                        lc.slotIndex = key.x * letterGridManager.width + key.y-1;
                    }
                  
                }

                if (charDirection.ContainsKey(key))
                {
                    var letterbox = Instantiate(letterGridManager.squareSlot, gridChild);
                    var lc = letterbox.GetComponent<LetterController>();
                   lc.key = key;
                    lc.myLetter = charDirection[key].Key;
                    letterbox.transform.localPosition = Vector3.zero;

                    if (charDirection[key].Value == "→")
                    {
                       
                        letterbox.GetComponent<LetterController>().direction = Vector3.right;
                    }
                    else if (charDirection[key].Value == "↑")
                    {
                       
                        letterbox.GetComponent<LetterController>().direction = Vector3.forward;
                    }
                    else if (charDirection[key].Value == "↓")
                    {
                      
                        letterbox.GetComponent<LetterController>().direction = Vector3.back;
                    }
                    else if (charDirection[key].Value == "←")
                    {
                       
                        letterbox.GetComponent<LetterController>().direction = Vector3.left;
                    }

                    if (freezedChar.ContainsKey(key))
                    {
                        letterbox.GetComponent<MeshRenderer>().material = frostMaterial;
                        letterbox.GetComponentInChildren<TextMeshPro>().text = freezedChar[key].ToString();
              
                        letterbox.AddComponent<LetterConstriant>().count = freezedChar[key];
                    }
                    else if (hiddenChar.Contains(key))
                    {
                        letterbox.GetComponent<MeshRenderer>().material.color = Color.grey;
                        letterbox.GetComponentInChildren<TextMeshPro>().text = "?";
                
                        letterbox.AddComponent<LetterConstriant>().constraint = ConstraintType.Hidden;
                    }
                    else
                    {
                        letterbox.GetComponentInChildren<TextMeshPro>().text = charDirection[key].Key;
                    }
                }
               
            }
        }
    }
    public GameObject ReplaceGameObject(GameObject oldObject, GameObject prefab)
    {
        Transform parent = oldObject.transform.parent;
        int siblingIndex = oldObject.transform.GetSiblingIndex();

        GameObject newObject = UnityEngine.Object.Instantiate(prefab, parent);
        newObject.transform.localPosition = oldObject.transform.localPosition;
        newObject.transform.localRotation = oldObject.transform.localRotation;
        newObject.transform.SetSiblingIndex(siblingIndex);

        UnityEngine.Object.DestroyImmediate(oldObject);

        return newObject;
    }
    void ResetUIElements()
    {

    }
}
