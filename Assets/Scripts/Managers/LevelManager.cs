using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


[RequireComponent(typeof(HintManager))]
public class LevelManager : Manager<LevelManager>
{
    [Header("LEVELS")]
    [SerializeField] private MinMax<int> OriginalLvls = new MinMax<int>(1, 100);
    [SerializeField] private MinMax<int> FakeLvls = new MinMax<int>(1, 100);

    [Header("REFERENCES")]
    [SerializeField] private TopGridManager gridManager;
    [SerializeField] private BottomGridManager letterGridManager;
    [SerializeField] private ResultManager resultManager;
    [SerializeField] private GameObject categoryHeading,arrow,freezeCount;
    [SerializeField] private Transform categoryHeadingParent,trayList;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Material borderMaterial,trayMaterial,boxMaterial,trayOutlineMat,freezeTray,freezeBox;
    [SerializeField] private List<KeyValueGroup<Material, Sprite>> colorSprite;
    [SerializeField] private FocusCutOut panel;
    [SerializeField] private Renderer halfArea;
    [SerializeField] private GameObject hand;
    [SerializeField] private TutorialPopup tutorialPopup;

    [HideInInspector] public int TestLevelToLoad = 1;
    [HideInInspector] public List<GameObject> ticks = new();
    
    private Dictionary<Material, Sprite> _colorSprite = new(); //Do not clear
    private Dictionary<Direction, GameObject> wallsDirectionDict = new();
    private Dictionary<string, int> freezedTray = new();

    private LevelData _LevelData;

    public HashSet<Vector2Int> excludedChar = new(),blockedCells = new();
  
   

    public Dictionary<Vector2Int, string> cellCategory = new(), cellTexts = new(),trayCells=new(),trayName = new();
    public Dictionary<string, Material> categoryColors = new(),trayColors = new();
    public Dictionary<string, List<Vector2Int>> wordPositions = new(),trayPos = new();
    public Dictionary<string, List<string>> wordsCategory = new();
    public HashSet<string> horizontal = new();

    [HideInInspector] public int hearts;

    private HintManager hintManager;

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
        hintManager = GetComponent<HintManager>();
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
        horizontal = _LevelData.horizontal.ToHashSet();
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
        FreezeManager.trayMat = trayMaterial;
        FreezeManager.arrow = arrow;
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
        tutorialPopup.Setup();
        tutorialPopup.ShowTutorial();
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
        var firstCharPos = CurLvlData.firstCharPos.ToDictionary(item => item.Key, item => item.Value);
        
        foreach (var word in wordPositions.Keys)
        {
            hintManager.wordChain[word] = new();
            hintManager.wordChain[word] = new(new(),new());

            foreach (var key in wordPositions[word])
            {
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
                        if (key.x > gridManager.rows - 4)
                        {
                            letterbox.GetComponent<MeshRenderer>().materials[1].color = Color.white;
                        }
                        else
                        {
                            letterbox.GetComponent<MeshRenderer>().materials[1].color = mats[0].color;
                        }

                        int i = key.y - firstCharPos[word];

                        hintManager.wordChain[word].Value.Add(i);
                  
                    }
                    letterbox.transform.localPosition = Vector3.zero;
                    hintManager.wordChain[word].Key.Add(letterbox);
                 
                }
               

            }
        }
      if (_CurrentLevelNumber == 1)
        {
            var group = new FocusCutOut.CutoutGroup();
            group.renderers = hintManager.wordChain["BANANA"].Key.Select(x => x.GetComponent<Renderer>()).ToList();
            panel.cutoutGroups.Add(group);
            group = new FocusCutOut.CutoutGroup();
            group.renderers.Add(halfArea);
            panel.cutoutGroups.Add(group);
            halfArea.gameObject.SetActive(true);
            panel.gameObject.SetActive(true);
        }
      else
        {
            halfArea.gameObject.SetActive (false);
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

         foreach(var pos in trayName.Keys)
          {
              if(!trayPos.ContainsKey(trayName[pos]))
              {
                  trayPos.Add(trayName[pos], new List<Vector2Int> { pos});
              }
              else
              {
                  trayPos[trayName[pos]].Add(pos);
              }
          }

        foreach (var tray in trayPos.Keys)
        {
            if (freezedTray.ContainsKey(tray))
            {
                var trayMesh = letterGridManager.CreateTray(trayPos[tray], 2.4f, freezeTray, new Vector3(.995f, .988f, .988f), false);
                trayMesh.layer = LayerMask.NameToLayer("Block");
                var tmp = Instantiate(freezeCount, trayMesh.transform);
                tmp.transform.localPosition = new Vector3(0, 2.75f, 0);
                tmp.transform.localScale = Vector3.one;
                tmp.GetComponentInChildren<TextMeshPro>().text = freezedTray[tray].ToString();
                var FM = trayMesh.AddComponent<FreezeManager>();
                FM.totalCount = freezedTray[tray];
                FM.trayCells = trayCells;
                FM.trayPos = trayPos[tray];
                if (horizontal.Contains(tray))
                {
                    FM.horizontalLock=true;
                }

               
            }
            else
            {
              
                if (horizontal.Contains(tray))
                {
                    letterGridManager.CreateTray(trayPos[tray], 2.4f, trayMaterial, new Vector3(.995f, .988f, .988f), true, trayCells,true).tag = "Vertical";
                  //  Instantiate(arrow,trayMesh.transform);
                }
                else
                {
                   var let = letterGridManager.CreateTray(trayPos[tray], 2.4f, trayMaterial, new Vector3(.995f, .988f, .988f), true, trayCells);
                    if (_CurrentLevelNumber == 1)
                    {
                        if (trayCells[trayPos[tray][0]] == "N")
                        {
                            hand.gameObject.SetActive(true);
                            let.AddComponent<MoveBack>().objectToMove = hand.transform;
                        }
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
        hintManager.wordChain.Clear();
        freezedTray.Clear();
        trayPos.Clear();
        horizontal.Clear();
    }
}
