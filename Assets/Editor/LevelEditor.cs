using Codice.Client.BaseCommands;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class LevelEditor : EditorWindow
{
    private int rows = 5;
    private int columns = 8;
    private int height = 8;
    private int width = 8;
    private int hearts = 3;

    private int drawMode = 0;
    private float bottomGridSize = .6f;
    private float gap = 2f;
    private float horizontalMargin = 20f;


    [StringDropdown("Data/Categories.txt")]
    public string categoriesDropdown;
    private string inputCategory;


    private Color gridCellColor, charColor;

    private Material categoryMaterial;

    private HashSet<string> words = new();
    private HashSet<Vector2Int> excludedChar = new(), hiddenChar = new();
    private Dictionary<Vector2Int, int> freezedChar = new();

    private Dictionary<Vector2Int, string> cellCategory = new();
    private Dictionary<Vector2Int, string> cellTexts = new();
    private Dictionary<string, Material> categoryColors = new();
    private Dictionary<string, List<Vector2Int>> wordPositions = new();
    private Dictionary<string, List<string>> wordCategory = new();
    private Dictionary<Vector2Int, KeyValueGroup<string, string>> charDirection = new(), charStorage = new();
    private Dictionary<Vector2Int, List<Vector2Int>> chainedLetters = new();

    // Tracks which root we are currently manually linking
    private Vector2Int activeRoot = new Vector2Int(-1, -1);

    // Cohesive Palette for assigning unique colors to individual chains
    private Color[] chainPalette = new Color[]
    {
        new Color(0.25f, 0.55f, 0.85f), // Blue
        new Color(0.85f, 0.35f, 0.35f), // Red
        new Color(0.25f, 0.65f, 0.45f), // Green
        new Color(0.85f, 0.60f, 0.20f), // Orange
        new Color(0.55f, 0.35f, 0.75f), // Purple
        new Color(0.20f, 0.65f, 0.65f), // Teal
        new Color(0.85f, 0.45f, 0.65f)  // Pink
    };

    SerializedObject window;
    SerializedProperty categoryList;
    private EditorSection levelSection;
    private LevelData CachedLvlData = null;
    private LevelData CurLvlData
    {
        get
        {
            CachedLvlData = CachedLvlData != null ? CachedLvlData : Resources.Load<LevelData>("Levels/" + CurLvlNum);
            return CachedLvlData;
        }
    }

    private int CurLvlNum = 1;

    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private GUIStyle expandableStyle;
    private GUIStyle buttonStyle;
    private GUIStyle counterButtonStyle;
    private GUIStyle rowHeaderStyle;
    private Vector2 EditorScrollPos = Vector2.zero;
    private Color editorColor = new Color(0, 0, .24f, .58f);

    [MenuItem("Frolic Frog/Level Editor #p")]
    public static void ShowWindow()
    {
        GetWindow<LevelEditor>("Level Editor");
    }

    private void OnEnable()
    {
        gridCellColor = new Color(0.3f, .3f, .3f);
        charColor = new Color(1, .94f, .77f, 1);
        wantsMouseMove = true;
        window = new SerializedObject(this);
        categoryList = window.FindProperty("categoriesDropdown");

        EnsureCategoryFileExists();
    }

    private void EnsureCategoryFileExists()
    {
        string directoryPath = Path.Combine(Application.dataPath, "Data");
        string filePath = Path.Combine(directoryPath, "Categories.txt");

        bool needsRefresh = false;

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            needsRefresh = true;
        }

        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, "");
            needsRefresh = true;
        }

        if (needsRefresh)
        {
            AssetDatabase.Refresh();
        }
    }

    private void InitStyles()
    {
        headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            margin = new RectOffset(0, 0, 10, 10)
        };
        boxStyle ??= new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 10, 10),
            margin = new RectOffset(7, 7, 7, 7),
        };
        labelStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            margin = new RectOffset(0, 0, 4, 4)
        };
        expandableStyle ??= new GUIStyle(EditorStyles.foldout)
        {
            fontSize = 14,
            margin = new RectOffset(0, 0, 0, 0)
        };
        buttonStyle ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            padding = new RectOffset(5, 5, 5, 5),
            normal = new GUIStyleState()
            {
                textColor = Color.white,
                background = Texture2D.whiteTexture
            },
            margin = new RectOffset(0, 0, 0, 0)
        };
        counterButtonStyle ??= new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
        };
        rowHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
    }

    void OnGUI()
    {
        // Deselect active input fields/sliders when clicking anywhere
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);
        }

        EditorScrollPos = EditorGUILayout.BeginScrollView(EditorScrollPos);
        InitStyles();
        LevelSettings();
        GetGridWord();
        EditorGridSerializationCheck();
        EditorGUILayout.EndScrollView();
    }

    private void LevelSettings()
    {
        levelSection ??= new EditorSection("Level Settings", true, boxStyle, expandableStyle);
        levelSection.Draw(() =>
        {
            GUILayout.BeginVertical();

            EditorGUI.BeginChangeCheck();
            CurLvlNum = EditorGUILayout.IntSlider("Level Number :", CurLvlNum, 1, 100);
            if (EditorGUI.EndChangeCheck())
            {
                bottomGridSize = .6f;
                categoryMaterial = null;
                hearts = 3;
                words.Clear();
                excludedChar.Clear();
                hiddenChar.Clear();
                freezedChar.Clear();
                cellCategory.Clear();
                cellTexts.Clear();
                categoryColors.Clear();
                wordPositions.Clear();
                charDirection.Clear();
                charStorage.Clear();
                chainedLetters.Clear();
                wordCategory.Clear();
                activeRoot = new Vector2Int(-1, -1);
                CachedLvlData = null;
            }

            hearts = EditorGUILayout.IntField("Heart Count:", hearts);
            GUILayout.EndVertical();
            Actions();
            GridSystem();
        }, editorColor);
    }

    private void Actions()
    {
        GUILayout.BeginHorizontal(boxStyle);

        if (GUILayout.Button("Load Level", GUILayout.Height(35)))
        {
            LoadLvl();
        }
        if (GUILayout.Button(CurLvlData == null ? "Create Level" : "Update Level", GUILayout.Height(35)))
        {
            UpdateLvl();
        }
        if (GUILayout.Button("Play Level", GUILayout.Height(35)))
        {
            LevelManager LM = FindAnyObjectByType<LevelManager>();
            if (LM == null)
            {
                Debug.LogWarning("No Level Manager Found in Scene");
            }
            else
            {
                LM.TestLevelToLoad = CurLvlNum;
                EditorUtility.SetDirty(LM);
                EditorApplication.isPlaying = true;
            }
        }

        GUILayout.EndHorizontal();
    }

    private void UpdateLvl()
    {
        LevelData currentData = CurLvlData;

        if (currentData == null)
        {
            currentData = CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(currentData, $"Assets/Resources/Levels/{CurLvlNum}.asset");
        }

        currentData.LevelNumber = CurLvlNum;
        currentData.columns = columns;
        currentData.rows = rows;
        currentData.height = height;
        currentData.width = width;
        currentData.hearts = hearts;
        currentData.bottomGridSize = bottomGridSize;
        currentData.words = words.ToList();
        currentData.excludedChar = excludedChar.ToList();

        currentData.hiddenChar = hiddenChar.ToList();
        currentData.freezedChar = freezedChar.Select(kvp => new KeyValueGroup<Vector2Int, int>(kvp.Key, kvp.Value)).ToList();

        currentData.categoryMaterial = categoryMaterial;

        currentData.cellCategory = cellCategory.Select(kvp => new KeyValueGroup<Vector2Int, string>(kvp.Key, kvp.Value)).ToList();
        currentData.cellTexts = cellTexts.Select(kvp => new KeyValueGroup<Vector2Int, string>(kvp.Key, kvp.Value)).ToList();
        currentData.categoryColors = categoryColors.Select(kvp => new KeyValueGroup<string, Material>(kvp.Key, kvp.Value)).ToList();

        var list = new List<KeyValueGroup<string, List<Vector2Int>>>();
        foreach (var kvp in wordPositions)
        {
            list.Add(new KeyValueGroup<string, List<Vector2Int>>(kvp.Key, new List<Vector2Int>(kvp.Value)));
        }
        currentData.wordPositions = list;

        var list1 = new List<KeyValueGroup<Vector2Int, KeyValueGroup<string, string>>>();
        foreach (var kvp in charDirection)
        {
            list1.Add(new KeyValueGroup<Vector2Int, KeyValueGroup<string, string>>(kvp.Key, kvp.Value));
        }
        currentData.charDirection = list1;

        var listStorage = new List<KeyValueGroup<Vector2Int, KeyValueGroup<string, string>>>();
        foreach (var kvp in charStorage)
        {
            listStorage.Add(new KeyValueGroup<Vector2Int, KeyValueGroup<string, string>>(kvp.Key, kvp.Value));
        }
        currentData.charStorage = listStorage;

        var list2 = new List<KeyValueGroup<Vector2Int, List<Vector2Int>>>();
        foreach (var kvg in chainedLetters)
        {
            list2.Add(new KeyValueGroup<Vector2Int, List<Vector2Int>>(kvg.Key, new List<Vector2Int>(kvg.Value)));
        }
        currentData.chainedLetters = list2;

        var list3 = new List<KeyValueGroup<string, List<string>>>();
        foreach (var kvg in wordCategory)
        {
            list3.Add(new KeyValueGroup<string, List<string>>(kvg.Key, new List<string>(kvg.Value)));
        }
        currentData.wordCategory = list3;

        EditorUtility.SetDirty(currentData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void LoadLvl()
    {
        if (CurLvlData == null) return;
        CurLvlNum = CurLvlData.LevelNumber;

        rows = CurLvlData.rows;
        columns = CurLvlData.columns;
        height = CurLvlData.height;
        width = CurLvlData.width;
        hearts = CurLvlData.hearts;
        words = CurLvlData.words.ToHashSet();
        excludedChar = CurLvlData.excludedChar.ToHashSet();

        hiddenChar = CurLvlData.hiddenChar != null ? CurLvlData.hiddenChar.ToHashSet() : new HashSet<Vector2Int>();
        freezedChar = CurLvlData.freezedChar != null ? CurLvlData.freezedChar.ToDictionary(item => item.Key, item => item.Value) : new Dictionary<Vector2Int, int>();

        categoryMaterial = CurLvlData.categoryMaterial;
        bottomGridSize = CurLvlData.bottomGridSize;
        cellCategory = CurLvlData.cellCategory.ToDictionary(item => item.Key, item => item.Value);
        cellTexts = CurLvlData.cellTexts.ToDictionary(item => item.Key, item => item.Value);
        categoryColors = CurLvlData.categoryColors.ToDictionary(item => item.Key, item => item.Value);
        wordPositions = new Dictionary<string, List<Vector2Int>>();

        foreach (var item in CurLvlData.wordPositions)
        {
            wordPositions[item.Key] = new List<Vector2Int>(item.Value);
        }

        charDirection.Clear();
        foreach (var item in CurLvlData.charDirection)
        {
            charDirection[item.Key] = item.Value;
        }

        charStorage.Clear();
        if (CurLvlData.charStorage != null)
        {
            foreach (var item in CurLvlData.charStorage)
            {
                charStorage[item.Key] = item.Value;
            }
        }

        chainedLetters.Clear();
        if (CurLvlData.chainedLetters != null)
        {
            foreach (var item in CurLvlData.chainedLetters)
            {
                chainedLetters[item.Key] = new List<Vector2Int>(item.Value);
            }
        }
        wordCategory.Clear();
        foreach (var item in CurLvlData.wordCategory)
        {
            wordCategory[item.Key] = item.Value;
        }
        categoryMaterial = (!string.IsNullOrEmpty(categoriesDropdown) && categoryColors.ContainsKey(categoriesDropdown)) ? categoryColors[categoriesDropdown] : null;

        activeRoot = new Vector2Int(-1, -1);
    }

    void GridSystem()
    {
        GUILayout.Space(10);

        rows = EditorGUILayout.IntSlider("Rows", rows, 1, 50);
        columns = EditorGUILayout.IntSlider("Columns", columns, 1, 50);
        bottomGridSize = EditorGUILayout.Slider("Bottom Grid Size", bottomGridSize, 0.05f, 1f);
        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        inputCategory = EditorGUILayout.TextField("New Category", inputCategory);

        if (GUILayout.Button("Add to Dropdown"))
        {
            AddCategoryToFile(inputCategory);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        window.Update();

        EditorGUILayout.PropertyField(categoryList);

        window.ApplyModifiedProperties();
        if (EditorGUI.EndChangeCheck())
        {
            if (!string.IsNullOrEmpty(categoriesDropdown) && categoryColors.ContainsKey(categoriesDropdown))
            {
                categoryMaterial = categoryColors[categoriesDropdown];
            }
            else
            {
                categoryMaterial = null;
            }
        }
        if (GUILayout.Button("Remove from Dropdown"))
        {
            DeleteCategory(categoriesDropdown);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        categoryMaterial = EditorGUILayout.ObjectField("Category Material", categoryMaterial, typeof(Material), false) as Material;
        if (EditorGUI.EndChangeCheck())
        {
            if (!string.IsNullOrEmpty(categoriesDropdown))
            {
                categoryColors[categoriesDropdown] = categoryMaterial;
            }
        }

        EditorGUILayout.EndHorizontal();

        if (string.IsNullOrEmpty(categoriesDropdown) || !categoryColors.ContainsKey(categoriesDropdown))
        {
            EditorGUILayout.HelpBox("Color is not applied to Category, or no category is selected!", MessageType.Error);
        }
        GUILayout.Space(10);

        float totalGapWidth = (columns - 1) * gap;
        float totalGapHeight = (rows - 1) * gap;

        float availableWidth = position.width - (horizontalMargin * 2) - 25f;
        float cellSize = (availableWidth - totalGapWidth) / columns;

        cellSize = Mathf.Max(cellSize, 5f);

        float requiredGridHeight = (cellSize * rows) + totalGapHeight;

        Rect gridArea = GUILayoutUtility.GetRect(0, 10000, requiredGridHeight, requiredGridHeight);

        if (Event.current.type == EventType.Repaint)
        {
            DrawGrid(gridArea, rows, columns, true);
        }

        HandleMouseClicks(gridArea, true);
        HandleKeyStrokes(gridArea, true);

        GUILayout.Space(30);

        height = EditorGUILayout.IntSlider("Secondary Rows", height, 1, 20);
        width = EditorGUILayout.IntSlider("Secondary Columns", width, 1, 20);

        totalGapWidth = (width - 1) * gap;
        totalGapHeight = (height - 1) * gap;
        string[] Modes = { "Place Letters", "Hide Letters", "Freeze Letters" };
        drawMode = GUILayout.SelectionGrid(drawMode, Modes, 3, GUILayout.ExpandWidth(true));
        availableWidth = position.width - (horizontalMargin * 2) - 25f;
        cellSize = (availableWidth - totalGapWidth) / width;

        cellSize = Mathf.Max(cellSize, 5f);

        requiredGridHeight = (cellSize * height) + totalGapHeight;

        gridArea = GUILayoutUtility.GetRect(0, 10000, requiredGridHeight, requiredGridHeight);


        if (Event.current.type == EventType.Repaint)
        {
            DrawGrid(gridArea, height, width, false);
        }

        HandleMouseClicks(gridArea, false);
        HandleKeyStrokes(gridArea, false);
    }

    private void DrawGrid(Rect gridArea, int rows, int columns, bool primary)
    {
        float totalGapWidth = (columns - 1) * gap;
        float totalGapHeight = (rows - 1) * gap;

        float availableWidth = gridArea.width - (horizontalMargin * 2);
        float cellSize = (availableWidth - totalGapWidth) / columns;

        float totalGridWidth = (cellSize * columns) + totalGapWidth;
        float totalGridHeight = (cellSize * rows) + totalGapHeight;

        float startX = gridArea.x + (gridArea.width - totalGridWidth) / 2f;
        float startY = gridArea.y;

        GUIStyle defaultLabelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        GUIStyle boldLabelStyle = new GUIStyle(defaultLabelStyle)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 16
        };

        Dictionary<Vector2Int, Rect> cellRects = new Dictionary<Vector2Int, Rect>();
        Dictionary<Vector2Int, Color> bgColors = new Dictionary<Vector2Int, Color>();

        Dictionary<Vector2Int, Color> rootColors = new Dictionary<Vector2Int, Color>();
        if (!primary)
        {
            var orderedRoots = chainedLetters.Keys.OrderBy(v => v.x).ThenBy(v => v.y).ToList();
            for (int i = 0; i < orderedRoots.Count; i++)
            {
                rootColors[orderedRoots[i]] = chainPalette[i % chainPalette.Length];
            }
        }

        // --- PASS 1: Calculate Rects and Draw Backgrounds ---
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                float xPos = startX + (c * (cellSize + gap));
                float yPos = startY + (r * (cellSize + gap));
                Rect cellRect = new Rect(xPos, yPos, cellSize, cellSize);
                Vector2Int gridPos = new Vector2Int(r, c);

                cellRects[gridPos] = cellRect;
                Color cellBgColor = gridCellColor;

                if (primary)
                {
                    if (cellCategory.ContainsKey(gridPos) && !string.IsNullOrEmpty(cellCategory[gridPos]) && categoryColors.ContainsKey(cellCategory[gridPos]))
                    {
                        cellBgColor = excludedChar.Contains(gridPos) ? Color.white * 0.64f : categoryColors[cellCategory[gridPos]].color;
                    }
                }
                else
                {
                    // charStorage cells are strictly isolated: fixed color, cannot be hidden or frozen
                    if (charStorage.ContainsKey(gridPos))
                    {
                        cellBgColor = charColor;
                    }
                    else if (charDirection.ContainsKey(gridPos))
                    {
                        bool isRoot = chainedLetters.ContainsKey(gridPos) && !string.IsNullOrEmpty(charDirection[gridPos].Value);
                        bool isActiveRoot = (gridPos == activeRoot);
                        var parentRoots = chainedLetters.Where(kvp => kvp.Value.Contains(gridPos)).Select(kvp => kvp.Key).ToList();
                        int chainCount = parentRoots.Count;

                        if (isActiveRoot)
                        {
                            cellBgColor = Color.Lerp(rootColors[gridPos], Color.white, 0.2f);
                        }
                        else if (isRoot)
                        {
                            cellBgColor = rootColors[gridPos];
                        }
                        else if (chainCount == 1)
                        {
                            cellBgColor = Color.Lerp(rootColors[parentRoots[0]], Color.white, 0.7f);
                        }
                        else if (chainCount > 1)
                        {
                            cellBgColor = new Color(0.85f, 0.85f, 0.85f);
                        }
                        else
                        {
                            cellBgColor = charColor;
                        }

                        // BOTH MODIFICATIONS VISIBLE SIMULTANEOUSLY IN MODE 1 AND MODE 2
                        if (drawMode == 1 || drawMode == 2)
                        {
                            if (freezedChar.ContainsKey(gridPos))
                            {
                                cellBgColor = new Color(0.6f, 0.82f, 1f, 1f); // Glacial Ice Light Blue
                            }
                            else if (hiddenChar.Contains(gridPos))
                            {
                                cellBgColor = new Color(0.18f, 0.18f, 0.18f, 1f); // Charcoal blackish
                            }
                        }
                    }
                }

                bgColors[gridPos] = cellBgColor;

                GUI.color = cellBgColor;
                GUI.DrawTexture(cellRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
                GUI.color = Color.white;

                if (!primary && gridPos == activeRoot)
                {
                    Handles.color = Color.yellow;
                    Handles.DrawSolidRectangleWithOutline(cellRect, Color.clear, Color.yellow);
                    Handles.color = Color.white;
                }
            }
        }

        // --- PASS 2: Draw Connecting Lines (Secondary Grid Only) ---
        if (!primary && Event.current.type == EventType.Repaint)
        {
            foreach (var kvp in chainedLetters)
            {
                Vector2Int rootPos = kvp.Key;
                if (!cellRects.ContainsKey(rootPos)) continue;

                Color lineColor = rootColors.ContainsKey(rootPos) ? rootColors[rootPos] : Color.white;
                float thickness = 2.5f;

                if (rootPos == activeRoot)
                {
                    thickness = 5f;
                    lineColor = Color.Lerp(lineColor, Color.yellow, 0.3f);
                }
                else
                {
                    lineColor.a = 0.5f;
                }

                Handles.color = lineColor;
                Vector2 rootCenter = cellRects[rootPos].center;

                foreach (var childPos in kvp.Value)
                {
                    if (!cellRects.ContainsKey(childPos)) continue;

                    Vector2 childCenter = cellRects[childPos].center;
                    Handles.DrawAAPolyLine(thickness, new Vector3[] { rootCenter, childCenter });
                }
            }
            Handles.color = Color.white;
        }

        // --- PASS 3: Draw Text Labels on Top ---
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector2Int gridPos = new Vector2Int(r, c);
                Rect cellRect = cellRects[gridPos];

                string cellText = "";
                bool hasLetter = false;

                GUIStyle activeStyle = boldLabelStyle;

                if (primary)
                {
                    hasLetter = cellTexts.ContainsKey(gridPos);
                    cellText = hasLetter ? cellTexts[gridPos] : $"{r},{c}";
                    activeStyle = hasLetter ? boldLabelStyle : defaultLabelStyle;
                }
                else
                {
                    if (charStorage.ContainsKey(gridPos))
                    {
                        hasLetter = true;
                        string arrow = charStorage[gridPos].Value;
                        string keyStr = charStorage[gridPos].Key ?? "";

                        // Parse string into individual characters for display
                        List<string> letterList = keyStr.Select(ch => ch.ToString()).ToList();

                        if (letterList.Count > 4)
                        {
                            // Create a 4-column layout with commas
                            List<string> lines = new List<string>();
                            for (int i = 0; i < letterList.Count; i += 4)
                            {
                                var chunk = letterList.Skip(i).Take(4);
                                lines.Add(string.Join(",", chunk));
                            }

                            // Show arrow first on line 1
                            if (lines.Count > 0)
                            {
                                lines[0] = string.IsNullOrEmpty(arrow) ? lines[0] : $"{arrow} {lines[0]}";
                            }
                            cellText = string.Join("\n", lines);

                            int lineCount = lines.Count;
                            int maxLineLen = lines.Max(l => l.Length);

                            int fontByWidth = Mathf.FloorToInt((cellRect.width - 4f) / (maxLineLen * 0.55f));
                            int fontByHeight = Mathf.FloorToInt((cellRect.height - 4f) / (lineCount * 1.15f));

                            int dynamicFontSize = Mathf.Clamp(Mathf.Min(fontByWidth, fontByHeight), 6, 14);

                            activeStyle = new GUIStyle(boldLabelStyle)
                            {
                                fontSize = dynamicFontSize,
                                alignment = TextAnchor.MiddleCenter
                            };
                        }
                        else
                        {
                            // <= 4 letters: Show arrow first, then comma-separated letters
                            string lettersFormatted = letterList.Count > 0 ? string.Join(",", letterList) : "";
                            cellText = string.IsNullOrEmpty(lettersFormatted) ? arrow : (string.IsNullOrEmpty(arrow) ? lettersFormatted : $"{arrow} {lettersFormatted}");

                            int textLen = Mathf.Max(1, cellText.Length);
                            int dynamicFontSize = Mathf.Clamp(Mathf.FloorToInt((cellRect.width - 6f) / (textLen * 0.55f)), 7, 16);

                            activeStyle = new GUIStyle(boldLabelStyle)
                            {
                                fontSize = dynamicFontSize,
                                alignment = TextAnchor.MiddleCenter
                            };
                        }
                    }
                    else if (charDirection.ContainsKey(gridPos))
                    {
                        hasLetter = true;
                        cellText = charDirection[gridPos].Key + charDirection[gridPos].Value;
                        if ((drawMode == 1 || drawMode == 2) && freezedChar.ContainsKey(gridPos))
                        {
                            cellText += $" ({freezedChar[gridPos]})";
                        }
                        activeStyle = boldLabelStyle;
                    }
                    else
                    {
                        hasLetter = false;
                        cellText = $"{r},{c}";
                        activeStyle = defaultLabelStyle;
                    }
                }

                Color previousContentColor = GUI.contentColor;
                GUI.contentColor = hasLetter ? GetContrastColor(bgColors[gridPos]) : Color.white;

                GUI.Label(cellRect, cellText, activeStyle);

                GUI.contentColor = previousContentColor;
            }
        }
    }

    private Color GetContrastColor(Color bgColor)
    {
        float luminance = 0.299f * bgColor.r + 0.587f * bgColor.g + 0.114f * bgColor.b;
        return luminance > 0.5f ? Color.black : Color.white;
    }

    private bool TryGetGridPosFromMouse(Vector2 mousePosition, Rect gridArea, int gridRows, int gridCols, out Vector2Int gridPos)
    {
        gridPos = Vector2Int.zero;

        float totalGapWidth = (gridCols - 1) * gap;
        float availableWidth = gridArea.width - (horizontalMargin * 2);
        float cellSize = (availableWidth - totalGapWidth) / gridCols;
        float totalGridWidth = (cellSize * gridCols) + totalGapWidth;

        float startX = gridArea.x + (gridArea.width - totalGridWidth) / 2f;
        float startY = gridArea.y;

        float localX = mousePosition.x - startX;
        float localY = mousePosition.y - startY;

        float stepSize = cellSize + gap;

        int col = Mathf.FloorToInt(localX / stepSize);
        int row = Mathf.FloorToInt(localY / stepSize);

        if (row >= 0 && row < gridRows && col >= 0 && col < gridCols)
        {
            float cellLocalX = localX - (col * stepSize);
            float cellLocalY = localY - (row * stepSize);

            if (cellLocalX <= cellSize && cellLocalY <= cellSize)
            {
                gridPos = new Vector2Int(row, col);
                return true;
            }
        }
        return false;
    }

    private void HandleMouseClicks(Rect gridArea, bool primary)
    {
        Event e = Event.current;
        int currentRows = primary ? rows : height;
        int currentCols = primary ? columns : width;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            // Clear focused fields on click
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);

            if (TryGetGridPosFromMouse(e.mousePosition, gridArea, currentRows, currentCols, out Vector2Int gridPos))
            {
                if (primary)
                {
                    if (excludedChar.Contains(gridPos))
                        excludedChar.Remove(gridPos);
                    else
                        excludedChar.Add(gridPos);
                }
                else
                {
                    if (drawMode == 0)
                    {
                        // charStorage cells cannot create or join chains
                        if (charDirection.ContainsKey(gridPos) && !charStorage.ContainsKey(gridPos))
                        {
                            bool isRoot = !string.IsNullOrEmpty(charDirection[gridPos].Value);

                            if (isRoot)
                            {
                                activeRoot = (activeRoot == gridPos) ? new Vector2Int(-1, -1) : gridPos;
                            }
                            else if (activeRoot != new Vector2Int(-1, -1) && chainedLetters.ContainsKey(activeRoot))
                            {
                                if (chainedLetters[activeRoot].Contains(gridPos))
                                {
                                    chainedLetters[activeRoot].Remove(gridPos);
                                }
                                else
                                {
                                    foreach (var chainList in chainedLetters.Values)
                                    {
                                        if (chainList.Contains(gridPos))
                                        {
                                            chainList.Remove(gridPos);
                                        }
                                    }

                                    chainedLetters[activeRoot].Add(gridPos);
                                }
                            }
                        }
                        else
                        {
                            activeRoot = new Vector2Int(-1, -1);
                        }
                    }
                    else if (drawMode == 1)
                    {
                        // charStorage cells cannot be hidden
                        if (charDirection.ContainsKey(gridPos) && !charStorage.ContainsKey(gridPos))
                        {
                            bool isChildInChain = chainedLetters.Values.Any(list => list.Contains(gridPos));
                            if (!isChildInChain)
                            {
                                if (hiddenChar.Contains(gridPos))
                                {
                                    hiddenChar.Remove(gridPos);
                                    if (chainedLetters.ContainsKey(gridPos))
                                    {
                                        foreach (var child in chainedLetters[gridPos])
                                        {
                                            hiddenChar.Remove(child);
                                        }
                                    }
                                }
                                else
                                {
                                    hiddenChar.Add(gridPos);
                                    if (chainedLetters.ContainsKey(gridPos))
                                    {
                                        foreach (var child in chainedLetters[gridPos])
                                        {
                                            hiddenChar.Add(child);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                e.Use();
                Repaint();
            }
        }
    }

    private void HandleKeyStrokes(Rect gridArea, bool primary)
    {
        Event e = Event.current;
        int currentRows = primary ? rows : height;
        int currentCols = primary ? columns : width;

        if (e.type == EventType.KeyDown)
        {
            if (TryGetGridPosFromMouse(e.mousePosition, gridArea, currentRows, currentCols, out Vector2Int gridPos))
            {
                if (primary)
                {
                    if (e.keyCode == KeyCode.Backspace)
                    {
                        if (cellTexts.ContainsKey(gridPos))
                        {
                            cellTexts.Remove(gridPos);
                            cellCategory.Remove(gridPos);
                            excludedChar.Remove(gridPos);
                            e.Use();
                            Repaint();
                        }
                    }
                    else if (char.IsLetter(e.character))
                    {
                        if (!string.IsNullOrEmpty(categoriesDropdown) && categoryColors.ContainsKey(categoriesDropdown))
                        {
                            cellTexts[gridPos] = e.character.ToString().ToUpper();
                            cellCategory[gridPos] = categoriesDropdown;
                        }
                        else
                        {
                            Debug.LogWarning("Apply color to category or select a valid category first.");
                        }
                        e.Use();
                        Repaint();
                    }
                }
                else
                {
                    bool keyHandled = false;
                    bool becameRoot = false;

                    if (drawMode == 0)
                    {
                        if (charStorage.ContainsKey(gridPos))
                        {
                            if (e.keyCode == KeyCode.Backspace)
                            {
                                string currentKey = charStorage[gridPos].Key;
                                if (!string.IsNullOrEmpty(currentKey))
                                {
                                    string newKey = currentKey.Substring(0, currentKey.Length - 1);

                                    if (string.IsNullOrEmpty(newKey))
                                    {
                                        charStorage.Remove(gridPos);
                                    }
                                    else
                                    {
                                        charStorage[gridPos] = new KeyValueGroup<string, string>(newKey, charStorage[gridPos].Value);
                                    }
                                }
                                else
                                {
                                    charStorage.Remove(gridPos);
                                }
                                keyHandled = true;
                            }
                            else if (char.IsLetter(e.character))
                            {
                                string newChar = e.character.ToString().ToUpper();
                                string currentKey = charStorage[gridPos].Key ?? "";
                                string updatedKey = currentKey + newChar;
                                charStorage[gridPos] = new KeyValueGroup<string, string>(updatedKey, charStorage[gridPos].Value);
                                keyHandled = true;
                            }
                            else if (e.keyCode == KeyCode.LeftArrow)
                            {
                                charStorage[gridPos] = new KeyValueGroup<string, string>(charStorage[gridPos].Key, "←");
                                keyHandled = true;
                            }
                            else if (e.keyCode == KeyCode.UpArrow)
                            {
                                charStorage[gridPos] = new KeyValueGroup<string, string>(charStorage[gridPos].Key, "↑");
                                keyHandled = true;
                            }
                            else if (e.keyCode == KeyCode.RightArrow)
                            {
                                charStorage[gridPos] = new KeyValueGroup<string, string>(charStorage[gridPos].Key, "→");
                                keyHandled = true;
                            }
                            else if (e.keyCode == KeyCode.DownArrow)
                            {
                                charStorage[gridPos] = new KeyValueGroup<string, string>(charStorage[gridPos].Key, "↓");
                                keyHandled = true;
                            }
                        }
                        else
                        {
                            bool hasNoDirectionLetter = !charDirection.ContainsKey(gridPos) || string.IsNullOrEmpty(charDirection[gridPos].Key);

                            if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.DownArrow)
                            {
                                string arrowSymbol = e.keyCode switch
                                {
                                    KeyCode.LeftArrow => "←",
                                    KeyCode.UpArrow => "↑",
                                    KeyCode.RightArrow => "→",
                                    KeyCode.DownArrow => "↓",
                                    _ => ""
                                };

                                if (hasNoDirectionLetter)
                                {
                                    charDirection.Remove(gridPos);
                                    charStorage[gridPos] = new KeyValueGroup<string, string>("", arrowSymbol);

                                    // Ensure clean isolation state
                                    hiddenChar.Remove(gridPos);
                                    freezedChar.Remove(gridPos);
                                    chainedLetters.Remove(gridPos);
                                    foreach (var chain in chainedLetters.Values)
                                    {
                                        chain.Remove(gridPos);
                                    }
                                    if (activeRoot == gridPos) activeRoot = new Vector2Int(-1, -1);

                                    keyHandled = true;
                                }
                                else
                                {
                                    charDirection[gridPos] = new KeyValueGroup<string, string>(charDirection[gridPos].Key, arrowSymbol);
                                    keyHandled = true;
                                    becameRoot = true;
                                }
                            }
                            else if (e.keyCode == KeyCode.Backspace)
                            {
                                if (chainedLetters.TryGetValue(gridPos, out var children))
                                {
                                    foreach (var child in children)
                                    {
                                        hiddenChar.Remove(child);
                                        freezedChar.Remove(child);
                                    }
                                }

                                charDirection.Remove(gridPos);
                                hiddenChar.Remove(gridPos);
                                freezedChar.Remove(gridPos);

                                chainedLetters.Remove(gridPos);
                                foreach (var chain in chainedLetters.Values)
                                {
                                    chain.Remove(gridPos);
                                }

                                if (activeRoot == gridPos)
                                    activeRoot = new Vector2Int(-1, -1);

                                keyHandled = true;
                            }
                            else if (char.IsLetter(e.character))
                            {
                                string newChar = e.character.ToString().ToUpper();
                                if (!charDirection.ContainsKey(gridPos))
                                    charDirection[gridPos] = new KeyValueGroup<string, string>(newChar, "");
                                else
                                    charDirection[gridPos] = new KeyValueGroup<string, string>(newChar, charDirection[gridPos].Value);

                                keyHandled = true;
                            }
                        }

                        if (becameRoot)
                        {
                            if (!chainedLetters.ContainsKey(gridPos))
                                chainedLetters[gridPos] = new List<Vector2Int>();

                            activeRoot = gridPos;
                        }
                    }
                    else if (drawMode == 2)
                    {
                        // charStorage cells cannot be frozen
                        if (charDirection.ContainsKey(gridPos) && !charStorage.ContainsKey(gridPos))
                        {
                            bool isChildInChain = chainedLetters.Values.Any(list => list.Contains(gridPos));
                            if (!isChildInChain)
                            {
                                if (e.keyCode == KeyCode.Backspace)
                                {
                                    freezedChar.Remove(gridPos);
                                    if (chainedLetters.ContainsKey(gridPos))
                                    {
                                        foreach (var child in chainedLetters[gridPos])
                                        {
                                            freezedChar.Remove(child);
                                        }
                                    }
                                    keyHandled = true;
                                }
                                else if (char.IsDigit(e.character))
                                {
                                    if (int.TryParse(e.character.ToString(), out int val))
                                    {
                                        freezedChar[gridPos] = val;
                                        if (chainedLetters.ContainsKey(gridPos))
                                        {
                                            foreach (var child in chainedLetters[gridPos])
                                            {
                                                freezedChar[child] = val;
                                            }
                                        }
                                        keyHandled = true;
                                    }
                                }
                            }
                        }
                    }

                    if (keyHandled)
                    {
                        e.Use();
                        Repaint();
                    }
                }
            }
        }
    }

    void GetGridWord()
    {
        words.Clear();
        wordPositions.Clear();
        wordCategory.Clear();

        for (int row = 0; row < rows; row++)
        {
            string currentWord = "";
            string currentCategory = null;
            List<Vector2Int> currentCoords = new List<Vector2Int>();

            for (int col = 0; col < columns; col++)
            {
                var gridPos = new Vector2Int(row, col);

                if (cellCategory.TryGetValue(gridPos, out string category) &&
                    cellTexts.TryGetValue(gridPos, out string letter))
                {
                    if (currentCategory == category)
                    {
                        currentWord += letter;
                        currentCoords.Add(gridPos);
                    }
                    else
                    {
                        SaveWordData(currentWord, currentCoords, currentCategory);
                        currentCategory = category;
                        currentWord = letter;

                        currentCoords.Clear();
                        currentCoords.Add(gridPos);
                    }
                }
                else
                {
                    SaveWordData(currentWord, currentCoords, currentCategory);
                    currentCategory = null;
                    currentWord = "";
                    currentCoords.Clear();
                }
            }

            SaveWordData(currentWord, currentCoords, currentCategory);
        }
    }

    private void SaveWordData(string word, List<Vector2Int> coords, string category)
    {
        if (word.Length > 1)
        {
            words.Add(word);
            string uniqueKey = word;
            int occurrence = 1;
            while (wordPositions.ContainsKey(uniqueKey))
            {
                occurrence++;
                uniqueKey = $"{word}#{occurrence}";
            }

            wordPositions[uniqueKey] = new List<Vector2Int>(coords);

            if (!string.IsNullOrEmpty(category))
            {
                if (!wordCategory.ContainsKey(category))
                {
                    wordCategory[category] = new List<string>();
                }

                if (!wordCategory[category].Contains(word))
                {
                    wordCategory[category].Add(word);
                }
            }
        }
    }

    private void AddCategoryToFile(string newCategory)
    {
        if (string.IsNullOrWhiteSpace(newCategory)) return;

        string directoryPath = Path.Combine(Application.dataPath, "Data");
        string filePath = Path.Combine(directoryPath, "Categories.txt");

        EnsureCategoryFileExists();

        List<string> lines = new List<string>(File.ReadAllLines(filePath));

        if (!lines.Contains(newCategory))
        {
            lines.Add(newCategory);
            File.WriteAllLines(filePath, lines);
            AssetDatabase.Refresh();
            Debug.Log($"Successfully added '{newCategory}'.");
        }
    }

    private void DeleteCategory(string targetString)
    {
        if (string.IsNullOrEmpty(targetString)) return;

        string filePath = Path.Combine(Application.dataPath, "Data/Categories.txt");

        if (!File.Exists(filePath)) return;

        List<string> lines = new List<string>(File.ReadAllLines(filePath));

        int removedCount = lines.RemoveAll(line => line.Trim().Equals(targetString.Trim(), System.StringComparison.OrdinalIgnoreCase));

        if (removedCount > 0)
        {
            File.WriteAllLines(filePath, lines);
            AssetDatabase.Refresh();
            Debug.Log($"Successfully deleted '{targetString}'.");
        }
    }

    private Color HexToColor(string hexCode)
    {
        Color parsedColor = Color.white;

        if (!string.IsNullOrEmpty(hexCode) && !hexCode.StartsWith("#"))
        {
            hexCode = "#" + hexCode;
        }

        if (ColorUtility.TryParseHtmlString(hexCode, out parsedColor))
        {
            return parsedColor;
        }

        Debug.LogWarning($"Invalid hex code provided: {hexCode}. Defaulting to white.");
        return Color.white;
    }

    private void EditorGridSerializationCheck()
    {
        // 1. BOUNDS CHECK: Clean arrays if dimensions downscale
        hiddenChar.RemoveWhere(pos => pos.x >= height || pos.y >= width);
        var outOfBoundsFreeze = freezedChar.Keys.Where(pos => pos.x >= height || pos.y >= width).ToList();
        foreach (var pos in outOfBoundsFreeze) freezedChar.Remove(pos);

        var outOfBoundsChar = charDirection.Keys.Where(pos => pos.x >= height || pos.y >= width).ToList();
        foreach (var pos in outOfBoundsChar) charDirection.Remove(pos);

        var outOfBoundsStorage = charStorage.Keys.Where(pos => pos.x >= height || pos.y >= width).ToList();
        foreach (var pos in outOfBoundsStorage) charStorage.Remove(pos);

        var outOfBoundsChains = chainedLetters.Keys.Where(pos => pos.x >= height || pos.y >= width).ToList();
        foreach (var pos in outOfBoundsChains) chainedLetters.Remove(pos);

        // 2. BLANK LETTER PURGE: Only remove from charDirection if the letter (Key) is completely missing or empty.
        var completelyEmptyLetters = charDirection.Keys
            .Where(pos => string.IsNullOrEmpty(charDirection[pos].Key))
            .ToList();
        foreach (var pos in completelyEmptyLetters)
        {
            charDirection.Remove(pos);
        }

        var emptyStorage = charStorage.Keys
            .Where(pos => string.IsNullOrEmpty(charStorage[pos].Key) && string.IsNullOrEmpty(charStorage[pos].Value))
            .ToList();
        foreach (var pos in emptyStorage)
        {
            charStorage.Remove(pos);
        }

        // Predicate defining what constitutes an invalid letter entry cell
        System.Predicate<Vector2Int> isInvalidLetterCell = pos =>
            (!charDirection.ContainsKey(pos) || string.IsNullOrEmpty(charDirection[pos].Key)) &&
            !charStorage.ContainsKey(pos);

        // 3. STATE CLEANUP FOR ERASED CELLS: Wipe configurations if the node has no text data
        hiddenChar.RemoveWhere(isInvalidLetterCell);

        var invalidFreeze = freezedChar.Keys.Where(pos => isInvalidLetterCell(pos)).ToList();
        foreach (var pos in invalidFreeze) freezedChar.Remove(pos);

        // 4. CHILD PRUNING CASCADE: Remove deleted/cleared letters from child chain arrays 
        foreach (var kvp in chainedLetters)
        {
            var childrenList = kvp.Value;
            var brokenChildren = childrenList.Where(childPos => isInvalidLetterCell(childPos)).ToList();

            foreach (var child in brokenChildren)
            {
                childrenList.Remove(child);
                hiddenChar.Remove(child);
                freezedChar.Remove(child);
            }
        }

        // 5. ISOLATION PURGE: Strip all chain, hide, or freeze connections from charStorage keys
        foreach (var pos in charStorage.Keys.ToList())
        {
            hiddenChar.Remove(pos);
            freezedChar.Remove(pos);
            chainedLetters.Remove(pos);
            foreach (var chain in chainedLetters.Values)
            {
                chain.Remove(pos);
            }
            if (activeRoot == pos)
            {
                activeRoot = new Vector2Int(-1, -1);
            }
        }

        // 6. FORCED SYNC REGISTER: Force creation of a chain list ONLY when BOTH letter and arrow string exist
        foreach (var kvp in charDirection)
        {
            Vector2Int pos = kvp.Key;
            bool hasBoth = !string.IsNullOrEmpty(kvp.Value.Key) && !string.IsNullOrEmpty(kvp.Value.Value);

            if (hasBoth && !chainedLetters.ContainsKey(pos) && !charStorage.ContainsKey(pos))
            {
                chainedLetters[pos] = new List<Vector2Int>();
            }
        }

        var invalidHeads = chainedLetters.Keys
            .Where(pos => !charDirection.ContainsKey(pos) ||
                          string.IsNullOrEmpty(charDirection[pos].Key) ||
                          string.IsNullOrEmpty(charDirection[pos].Value) ||
                          charStorage.ContainsKey(pos))
            .ToList();

        foreach (var head in invalidHeads)
        {
            // Reset modifications mapped to children linked to this broken root head
            if (chainedLetters.TryGetValue(head, out var children))
            {
                foreach (var child in children)
                {
                    hiddenChar.Remove(child);
                    freezedChar.Remove(child);
                }
            }

            chainedLetters.Remove(head);

            if (activeRoot == head)
            {
                activeRoot = new Vector2Int(-1, -1);
            }
        }
    }
}