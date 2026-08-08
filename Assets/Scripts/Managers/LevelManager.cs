using System.Collections.Generic;
using System.ComponentModel;
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
    [SerializeField] private ResultManager resultManager;
    [SerializeField] private GameObject categoryHeading,arrow;
    [SerializeField] private Transform categoryHeadingParent,trayList;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Material borderMaterial,trayMaterial,boxMaterial,trayOutlineMat,freezeTray,freezeBox;
    [SerializeField] private List<KeyValueGroup<Material, Sprite>> colorSprite;

    [HideInInspector] public int TestLevelToLoad = 1;
    [HideInInspector] public List<GameObject> ticks = new();

    private Dictionary<Material, Sprite> _colorSprite = new(); //Do not clear
    private Dictionary<Direction, GameObject> wallsDirectionDict = new();
    private Dictionary<string, List<GameObject>> trayChunks = new();
    private Dictionary<string, int> freezedTray = new();

    private LevelData _LevelData;

    public HashSet<Vector2Int> excludedChar = new(),blockedCells = new();
  
   

    public Dictionary<Vector2Int, string> cellCategory = new(), cellTexts = new(),trayCells=new(),trayName = new();
    public Dictionary<string, Material> categoryColors = new(),trayColors = new();
    public Dictionary<string, List<Vector2Int>> wordPositions = new();
    public Dictionary<string, List<string>> wordsCategory = new();


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
        excludedChar = _LevelData.excludedChar.ToHashSet();
        blockedCells = _LevelData.blockedCells.ToHashSet();
        cellCategory = _LevelData.cellCategory.ToDictionary(item => item.Key, item => item.Value);
        cellTexts = _LevelData.cellTexts.ToDictionary(item => item.Key, item => item.Value);
        categoryColors = _LevelData.categoryColors.ToDictionary(item => item.Key, item => item.Value);
        trayCells = _LevelData.trayCells.ToDictionary(item=>item.Key, item => item.Value);
        trayName = _LevelData.trayName.ToDictionary(item=>item.Key,item => item.Value);
        trayColors =_LevelData.trayColors.ToDictionary(item=>item.Key,item=>item.Value);
        resultManager.timer = _LevelData.timer;
        resultManager.time = _LevelData.minutes*60+_LevelData.seconds;
        letterGridManager.screenPadding = _LevelData.bottomGridSize;
        freezedTray = _LevelData.freezedTray.ToDictionary(item => item.Key, item => item.Value);
        foreach (var item in CurLvlData.wordPositions)
        {
            wordPositions[item.Key] = new List<Vector2Int>( item.Value);
        }
        foreach (var item in _LevelData.wordCategory)
        {
            // Create a NEW list using the values from the ScriptableObject
            wordsCategory[item.Key] = new List<string>(item.Value);
        }



        foreach (var item in colorSprite)
        {
            _colorSprite[item.Key] = item.Value;
        }


        foreach (var wallDirection in letterGridManager.wallsDirection)
        {
            wallsDirectionDict[wallDirection.facing] = wallDirection.mesh;
        }

        FreezeManager.box = boxMaterial;
        FreezeManager.tray = trayMaterial;
    }
    void LoadInScene()
    {
        gridManager.CreateChildren();
        gridManager.ArrangeChildren();
        letterGridManager.CreateChildren();
        letterGridManager.ArrangeChildren();
        SetUIElements();
        ManageWords();
        ManageAlphabets();
       
    }

    void SetUIElements()
    {
        foreach (var category in categoryColors.Keys)
        {
       
            // Debug.Log(wordsCategory[category].Count);
            if (wordsCategory.ContainsKey(category))
            {
                var heading = Instantiate(categoryHeading, categoryHeadingParent);
                heading.GetComponent<Image>().sprite = _colorSprite[categoryColors[category]];
                heading.GetComponentsInChildren<TextMeshProUGUI>()[0].text = category;
                heading.GetComponentsInChildren<TextMeshProUGUI>()[1].text = wordsCategory[category].Count.ToString();
                heading.transform.GetChild(1).GetComponent<Image>().color = categoryColors[category].color;
                ticks.Add(heading.transform.GetChild(heading.transform.childCount - 1).gameObject);
            }

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
       
      
        for (int height = 0; height < letterGridManager.height; height++)
        {
            for (int width = 0; width < letterGridManager.width; width++)
            {
                Vector2Int key = new Vector2Int(height, width);
                int linearIndex = key.x * letterGridManager.width + key.y;
                var gridChild = letterGridManager.transform.GetChild(linearIndex);
                if((height+width)%2 ==0)
                {
                    Instantiate(letterGridManager.cell1, gridChild).transform.localPosition = Vector3.zero;
                }
                else
                {
                    Instantiate(letterGridManager.cell2, gridChild).transform.localPosition = Vector3.zero; 
                }

                if (blockedCells.Contains(key))
                {
                    var blockWall = Instantiate(letterGridManager.blockWall, gridChild);
                    blockWall.transform.localPosition = Vector3.zero;
                    blockWall.transform.localScale = new Vector3(1.5f, 2, 1.4f);
                }

            }
        }

        for (int height = 0; height < letterGridManager.height; height++)
        {
            for (int width = 0; width < letterGridManager.width; width++)
            {
                Vector2Int key = new Vector2Int(height, width);
                int linearIndex = key.x * letterGridManager.width + key.y;
                var gridChild = letterGridManager.transform.GetChild(linearIndex);
               if(trayCells.ContainsKey(key))
                {
                    Direction dir = new();
                    List<Vector2Int> keys = new();
                    keys.Add(key - new Vector2Int(1, 0));
                    keys.Add(key + new Vector2Int(1, 0));
                    keys.Add(key + new Vector2Int(0, 1));
                    keys.Add(key - new Vector2Int(0, 1));

                    for (int i = 0; i < keys.Count; i++)
                    {
                       
                        if (!trayCells.ContainsKey(keys[i]) || trayName[keys[i]] != trayName[key])
                        {
                            switch (i)
                            {
                                
                                case 0:
                                    dir.front = true;
                                    break;
                                case 1:
                                    dir.back = true;
                                    break;
                                case 2:
                                    dir.right = true;
                                    break;
                                case 3:
                                    dir.left = true;
                                    break;
                            }


                        }
                       
                    }
                   // Debug.Log((dir.front, dir.left, dir.back, dir.right));
                    var trayChunk = Instantiate(wallsDirectionDict[dir], gridChild);
                    trayChunk.transform.localPosition = Vector3.zero;
                    if (freezedTray.ContainsKey(trayName[key]))
                    {
                        trayChunk.GetComponent<MeshRenderer>().material = freezeTray;
                        trayChunk.layer = LayerMask.NameToLayer("Default");
                    }
                    else
                    {
                        trayChunk.GetComponent<MeshRenderer>().material = trayMaterial;
                    }
                    if (!trayChunks.ContainsKey(trayName[key]))
                    {
                        trayChunks[trayName[key]] = new List<GameObject>();
                    }
                    trayChunks[trayName[key]].Add(trayChunk);

                    var letterBox = Instantiate(letterGridManager.letter, trayChunk.transform);
                    letterBox.GetComponentInChildren<TextMeshPro>().text = trayCells[key];
                    if (freezedTray.ContainsKey(trayName[key]))
                    {
                        letterBox.GetComponent<MeshRenderer>().material = freezeBox;
                       var count = Instantiate(letterBox.GetComponentInChildren<TextMeshPro>(), letterBox.transform).text = freezedTray[trayName[key]].ToString();
                        letterBox.GetComponentInChildren<TextMeshPro>().gameObject.SetActive(false);


                    }
                    else
                    {
                        letterBox.GetComponent<MeshRenderer>().material = boxMaterial;
                    }
                    letterBox.transform.localPosition = Vector3.zero;
                
                    Vector3 size = new Vector3(1, 2.4f, 1);
                    Vector3 pos = Vector3.zero;

                  

                    if (dir.left)
                    {
                        size.x -= .058f;
                        pos.x += .078f;
                    }
                    if (dir.right)
                    {
                        size.x -= .058f;
                        pos.x -= .078f;
                    }
                    if (dir.front)
                    {
                        size.z -= 0.058f;
                        pos.z -= 0.078f;
                    }
                    if (dir.back)
                    {
                        size.z -= 0.058f;
                        pos.z += 0.078f;
                    }
                    letterBox.transform.localScale = size;
                    letterBox.transform.localPosition = pos;


                   
                }
            }
        }
        if(trayList == null)
        {
            Debug.LogError("Tray List is Null!");
        }
        foreach (var key in trayChunks.Keys)
        {
            var trayParent = new GameObject(key);
            
            List<Vector3> positions = new List<Vector3>();
            Vector3 avgPosition = Vector3.zero;
            foreach(var chunk in trayChunks[key] )
            {
                positions.Add(chunk.transform.position);
            }
            foreach(var pos in positions)
            {
                avgPosition += pos;
            }
            avgPosition /= positions.Count;
            trayParent.transform.position = avgPosition;
       
            foreach (var chunk in trayChunks[key])
            {
                chunk.transform.SetParent(trayParent.transform);
            }
         trayParent.transform.SetParent(trayList);
            if(freezedTray.ContainsKey(key))
            {
              var fm =  trayParent.AddComponent<FreezeManager>();
                fm.totalCount = freezedTray[key];
            }
            trayParent.AddComponent<TrayCubeScaler>();
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

    public void UnloadInScene()
    {
        ResetUIElements(); 
        ResetWords();
        ResetAlphabets();
        ResetData();
    }

    void ResetUIElements()
    {
        for(int i = categoryHeadingParent.childCount-1; i>=0;i--)
        {
            Destroy(categoryHeadingParent.GetChild(i).gameObject);
        }
    }

    void ResetWords()
    {
        for(int i = TopGridManager.instance.transform.childCount-1; i>=0;i--)
        {
            Destroy(TopGridManager.instance.transform.GetChild(i).gameObject);
        }
    }

    void ResetAlphabets()
    {
        for(int i = BottomGridManager.Instance.transform.childCount-1; i>=0;i--)
        {
            Destroy(BottomGridManager.Instance.transform.GetChild(i).gameObject);
        }
        for (int i = trayList.childCount - 1; i >= 0; i--)
        {
            Debug.Log("Destroying");
            Destroy (trayList.GetChild(i).gameObject);
        }

    }
    void ResetData()
    {
    
        excludedChar.Clear();
        blockedCells.Clear();
        cellCategory.Clear();// = _LevelData.cellCategory.ToDictionary(item => item.Key, item => item.Value);
        cellTexts.Clear();// = _LevelData.cellTexts.ToDictionary(item => item.Key, item => item.Value);
        categoryColors.Clear();// = _LevelData.categoryColors.ToDictionary(item => item.Key, item => item.Value);
        trayCells.Clear();// = _LevelData.trayCells.ToDictionary(item => item.Key, item => item.Value);
        trayName.Clear();// = _LevelData.trayName.ToDictionary(item => item.Key, item => item.Value);
        trayColors.Clear();// = _LevelData.trayColors.ToDictionary(item => item.Key, item => item.Value);
        wordPositions.Clear();
        wordsCategory.Clear();
        trayChunks.Clear();
        
    }
}
