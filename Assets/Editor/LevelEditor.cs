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
    private int height = 8; // Bottom Grid Rows
    private int width = 8;  // Bottom Grid Columns
   
    private int minutes = 1, seconds = 30;
    private bool timer = true;
    private float gap = 2f;
    private float horizontalMargin = 20f;
    private float screenPadding = 0.08f;

    public List<string> tray = new List<string> { "Tray 1" };
    [StringDropdown("Data/Categories.txt")]
    public string categoriesDropdown;
    [StringDropdown(listFieldName = nameof(tray))]
    public string trayDropdown;

    private string inputCategory, extraChar;

    private Color gridCellColor;
    private Color unassignedCellColor; // Color for cells with letters but no category/tray assigned
    private Color blockedCellColor;    // Color for blocked cells in the bottom grid

    private Material categoryMaterial;

    private HashSet<string> words = new();
    private HashSet<Vector2Int> excludedChar = new(), blockedCells = new();

    private Dictionary<Vector2Int, string> cellCategory = new(), trayName = new();
    private Dictionary<Vector2Int, string> cellTexts = new(), trayCells = new();
    private Dictionary<string, Material> categoryColors = new();

    // Auto-generated colors for the trays
    private Dictionary<string, Color> trayDisplayColors = new();

    private Dictionary<string, List<Vector2Int>> wordPositions = new();
    private Dictionary<string, List<string>> wordCategory = new();

    SerializedObject window;
    SerializedProperty categoryList, trayList;
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
        gridCellColor = new Color(0.3f, 0.3f, 0.3f);
        unassignedCellColor = new Color(0.2f, 0.4f, 0.55f); // Distinct dark slate blue
        blockedCellColor = new Color(0.6f, 0.15f, 0.15f);   // Dark Red for blocked cells
        wantsMouseMove = true;
        window = new SerializedObject(this);
        categoryList = window.FindProperty("categoriesDropdown");
        trayList = window.FindProperty("trayDropdown");

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
        ShowExcludedLetters();
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
                screenPadding = 0.08f;
                categoryMaterial = null;
                minutes = 1;
                seconds = 30;
                timer = true;
                words.Clear();
                excludedChar.Clear();
                blockedCells.Clear(); // Cleared when switching levels
                cellCategory.Clear();
                cellTexts.Clear();
                categoryColors.Clear();
                wordPositions.Clear();
                trayCells.Clear();
                trayName.Clear();
                trayDisplayColors.Clear();
                wordCategory.Clear();
                tray.Clear();
                tray.Add("Tray 1");
                trayDropdown = "Tray 1";
                CachedLvlData = null;
            }
            GUILayout.BeginHorizontal();

            // Use ExpandWidth(false) so the toggle only takes up the space it needs
            timer = EditorGUILayout.Toggle("Timer", timer, GUILayout.ExpandWidth(false));

            if (timer)
            {
                // 1. Cache the original label width so we don't break the rest of the inspector
                float originalLabelWidth = EditorGUIUtility.labelWidth;

                // 2. Shrink the label width to fit "Min:" and "Sec:" snugly
                EditorGUIUtility.labelWidth = 35f;

                // 3. Constrain the total width of each field so they sit nicely next to each other
                minutes = EditorGUILayout.IntField("Min:", minutes, GUILayout.Width(80f));
                seconds = EditorGUILayout.IntField("Sec:", seconds, GUILayout.Width(80f));

                // 4. Restore the original label width for the rest of your custom editor
                EditorGUIUtility.labelWidth = originalLabelWidth;
            }

            GUILayout.EndHorizontal();
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
        currentData.bottomGridSize = screenPadding;
        currentData.minutes = minutes;
        currentData.seconds = seconds;
        currentData.timer = timer;
        currentData.words = words.ToList();
        currentData.excludedChar = excludedChar.ToList();

        // Save blocked cells data
        currentData.blockedCells = blockedCells.ToList();

        currentData.tray = new List<string>(tray);
        currentData.categoryMaterial = categoryMaterial;

        currentData.cellCategory = cellCategory.Select(kvp => new KeyValueGroup<Vector2Int, string>(kvp.Key, kvp.Value)).ToList();
        currentData.cellTexts = cellTexts.Select(kvp => new KeyValueGroup<Vector2Int, string>(kvp.Key, kvp.Value)).ToList();
        currentData.categoryColors = categoryColors.Select(kvp => new KeyValueGroup<string, Material>(kvp.Key, kvp.Value)).ToList();

        currentData.trayName = trayName.Select(kvp => new KeyValueGroup<Vector2Int, string>(kvp.Key, kvp.Value)).ToList();
        currentData.trayCells = trayCells.Select(kvp => new KeyValueGroup<Vector2Int, string>(kvp.Key, kvp.Value)).ToList();

        var list = new List<KeyValueGroup<string, List<Vector2Int>>>();
        foreach (var kvp in wordPositions)
        {
            list.Add(new KeyValueGroup<string, List<Vector2Int>>(kvp.Key, new List<Vector2Int>(kvp.Value)));
        }
        currentData.wordPositions = list;

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
        screenPadding = CurLvlData.bottomGridSize;
        minutes = CurLvlData.minutes;
        seconds = CurLvlData.seconds;
        timer = CurLvlData.timer;
        words = CurLvlData.words.ToHashSet();
        excludedChar = CurLvlData.excludedChar.ToHashSet();

        // Load blocked cells data
        blockedCells = CurLvlData.blockedCells != null ? CurLvlData.blockedCells.ToHashSet() : new HashSet<Vector2Int>();

        tray = new List<string>(CurLvlData.tray);
        categoryMaterial = CurLvlData.categoryMaterial;
        cellCategory = CurLvlData.cellCategory.ToDictionary(item => item.Key, item => item.Value);
        cellTexts = CurLvlData.cellTexts.ToDictionary(item => item.Key, item => item.Value);
        categoryColors = CurLvlData.categoryColors.ToDictionary(item => item.Key, item => item.Value);
        wordPositions = new Dictionary<string, List<Vector2Int>>();

        foreach (var item in CurLvlData.wordPositions)
        {
            wordPositions[item.Key] = new List<Vector2Int>(item.Value);
        }

        wordCategory.Clear();
        foreach (var item in CurLvlData.wordCategory)
        {
            wordCategory[item.Key] = item.Value;
        }

        if (CurLvlData.trayName != null)
            trayName = CurLvlData.trayName.ToDictionary(item => item.Key, item => item.Value);
        else
            trayName.Clear();

        if (CurLvlData.trayCells != null)
            trayCells = CurLvlData.trayCells.ToDictionary(item => item.Key, item => item.Value);
        else
            trayCells.Clear();

        categoryMaterial = (!string.IsNullOrEmpty(categoriesDropdown) && categoryColors.ContainsKey(categoriesDropdown)) ? categoryColors[categoriesDropdown] : null;

        // Auto-assign random colors for loaded trays
        trayDisplayColors.Clear();
        foreach (var name in tray)
        {
            if (!trayDisplayColors.ContainsKey(name))
                trayDisplayColors[name] = Random.ColorHSV(0f, 1f, 0.4f, 0.8f, 0.7f, 1f);
        }
    }

    void GridSystem()
    {
        GUILayout.Space(10);

        // --- Primary Grid Controls ---
        rows = EditorGUILayout.IntSlider("Rows", rows, 1, 50);
        columns = EditorGUILayout.IntSlider("Columns", columns, 1, 50);
        GUILayout.Space(5);

        if (string.IsNullOrEmpty(categoriesDropdown) || !categoryColors.ContainsKey(categoriesDropdown))
        {
            EditorGUILayout.HelpBox("Color is not applied to Category, or no category is selected!", MessageType.Error);
        }
        GUILayout.Space(10);

        // --- Render Primary Grid ---
        float totalGapWidth = (columns - 1) * gap;
        float totalGapHeight = (rows - 1) * gap;
        float availableWidth = position.width - (horizontalMargin * 2) - 25f;
        float cellSize = Mathf.Max((availableWidth - totalGapWidth) / columns, 5f);
        float requiredGridHeight = (cellSize * rows) + totalGapHeight;

        Rect primaryGridArea = GUILayoutUtility.GetRect(0, 10000, requiredGridHeight, requiredGridHeight);

        if (Event.current.type == EventType.Repaint)
        {
            DrawGrid(primaryGridArea, rows, columns, true);
        }

        HandleMouseClicks(primaryGridArea, rows, columns, true);
        HandleKeyStrokes(primaryGridArea, rows, columns, true);

        GUILayout.Space(30);

        // --- Bottom Grid Controls & Display ---
        
        height = EditorGUILayout.IntSlider("Bottom Grid Rows", height, 1, 20);
        width = EditorGUILayout.IntSlider("Bottom Grid Columns", width, 1, 20);
        screenPadding = EditorGUILayout.Slider("Screen Padding", screenPadding, 0, 0.5f);
        window.Update();
        GUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(trayList);
        if (EditorGUI.EndChangeCheck())
        {
            window.ApplyModifiedProperties();
        }
        else
        {
            window.ApplyModifiedProperties();
        }

        if (GUILayout.Button("New Tray"))
        {
            int trayCount = tray.Count + 1;
            while (tray.Contains("Tray " + trayCount)) trayCount++;
            string newTrayName = "Tray " + trayCount;
            tray.Add(newTrayName);
            trayDropdown = newTrayName;

            // Assign a random HSV color to the new tray
            trayDisplayColors[newTrayName] = Random.ColorHSV(0f, 1f, 0.4f, 0.8f, 0.7f, 1f);

            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();

        totalGapWidth = (width - 1) * gap;
        totalGapHeight = (height - 1) * gap;

        availableWidth = position.width - (horizontalMargin * 2) - 25f;
        cellSize = Mathf.Max((availableWidth - totalGapWidth) / width, 5f);
        requiredGridHeight = (cellSize * height) + totalGapHeight;

        Rect bottomGridArea = GUILayoutUtility.GetRect(0, 10000, requiredGridHeight, requiredGridHeight);

        if (Event.current.type == EventType.Repaint)
        {
            DrawGrid(bottomGridArea, height, width, false);
        }

        HandleMouseClicks(bottomGridArea, height, width, false);
        HandleKeyStrokes(bottomGridArea, height, width, false);
    }

    private Color GetMaterialColor(Material mat)
    {
        if (mat == null) return gridCellColor;
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        return mat.color;
    }

    private void DrawGrid(Rect gridArea, int gridRows, int gridCols, bool isPrimary)
    {
        float totalGapWidth = (gridCols - 1) * gap;
        float availableWidth = gridArea.width - (horizontalMargin * 2);
        float cellSize = (availableWidth - totalGapWidth) / gridCols;

        float totalGridWidth = (cellSize * gridCols) + totalGapWidth;
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

        // Track bounding boxes for bottom grid tray names
        Dictionary<string, Rect> trayBounds = new Dictionary<string, Rect>();

        for (int r = 0; r < gridRows; r++)
        {
            for (int c = 0; c < gridCols; c++)
            {
                float xPos = startX + (c * (cellSize + gap));
                float yPos = startY + (r * (cellSize + gap));
                Rect cellRect = new Rect(xPos, yPos, cellSize, cellSize);
                Vector2Int gridPos = new Vector2Int(r, c);

                Color cellBgColor = gridCellColor;

                if (isPrimary)
                {
                    bool hasCategory = cellCategory.TryGetValue(gridPos, out string cat) &&
                                       !string.IsNullOrEmpty(cat) &&
                                       categoryColors.TryGetValue(cat, out Material catMat) &&
                                       catMat != null;

                    bool hasText = cellTexts.TryGetValue(gridPos, out string txt) && !string.IsNullOrEmpty(txt);

                    if (hasCategory)
                    {
                        cellBgColor = excludedChar.Contains(gridPos)
                            ? Color.white * 0.64f
                            : GetMaterialColor(categoryColors[cat]);
                    }
                    else if (hasText)
                    {
                        cellBgColor = unassignedCellColor;
                    }
                }
                else
                {
                    bool hasTray = trayName.TryGetValue(gridPos, out string tName) &&
                                   !string.IsNullOrEmpty(tName);

                    bool hasText = trayCells.TryGetValue(gridPos, out string txt) && !string.IsNullOrEmpty(txt);
                    bool isBlocked = blockedCells.Contains(gridPos);

                    if (isBlocked)
                    {
                        cellBgColor = blockedCellColor;
                    }
                    else if (hasTray)
                    {
                        // Fallback to assign random color if missing
                        if (!trayDisplayColors.ContainsKey(tName))
                        {
                            trayDisplayColors[tName] = Random.ColorHSV(0f, 1f, 0.4f, 0.8f, 0.7f, 1f);
                        }

                        cellBgColor = trayDisplayColors[tName];

                        // Accumulate bounding box for the tray overlay label
                        if (trayBounds.ContainsKey(tName))
                        {
                            Rect currentRect = trayBounds[tName];
                            float xMin = Mathf.Min(currentRect.xMin, cellRect.xMin);
                            float yMin = Mathf.Min(currentRect.yMin, cellRect.yMin);
                            float xMax = Mathf.Max(currentRect.xMax, cellRect.xMax);
                            float yMax = Mathf.Max(currentRect.yMax, cellRect.yMax);
                            trayBounds[tName] = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
                        }
                        else
                        {
                            trayBounds[tName] = cellRect;
                        }
                    }
                    else if (hasText)
                    {
                        cellBgColor = unassignedCellColor;
                    }
                }

                GUI.color = cellBgColor;
                GUI.DrawTexture(cellRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
                GUI.color = Color.white;

                string cellText = $"{r},{c}";
                GUIStyle activeStyle = defaultLabelStyle;

                bool cellHasText = isPrimary ? cellTexts.ContainsKey(gridPos) : (trayCells.ContainsKey(gridPos) || (!isPrimary && blockedCells.Contains(gridPos)));

                if (isPrimary && cellTexts.ContainsKey(gridPos))
                {
                    cellText = cellTexts[gridPos];
                    activeStyle = boldLabelStyle;
                }
                else if (!isPrimary && trayCells.ContainsKey(gridPos))
                {
                    cellText = trayCells[gridPos];
                    activeStyle = boldLabelStyle;
                }
                else if (!isPrimary && blockedCells.Contains(gridPos))
                {
                    cellText = "Blocked";
                    activeStyle = new GUIStyle(boldLabelStyle) { fontSize = 11 };
                }

                Color previousContentColor = GUI.contentColor;
                GUI.contentColor = cellHasText ? GetContrastColor(cellBgColor) : Color.white;

                GUI.Label(cellRect, cellText, activeStyle);

                GUI.contentColor = previousContentColor;
            }
        }

        // Overlay pass: Draw Tray Name tag anchored to the top edge of all combined cells for each tray
        if (!isPrimary && trayBounds.Count > 0)
        {
            GUIStyle overlayTrayStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            foreach (var kvp in trayBounds)
            {
                string tName = kvp.Key;
                Rect combinedRect = kvp.Value;

                // Anchor label to the top edge of the tray region so it doesn't overlap centered cell letters
                Rect tagRect = new Rect(combinedRect.x, combinedRect.y + 2f, combinedRect.width, 16f);

                // Draw Drop Shadow
                Rect shadowRect = new Rect(tagRect.x + 1, tagRect.y + 1, tagRect.width, tagRect.height);
                overlayTrayStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
                GUI.Label(shadowRect, tName, overlayTrayStyle);

                // Draw Main Label
                overlayTrayStyle.normal.textColor = Color.white;
                GUI.Label(tagRect, tName, overlayTrayStyle);
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

    private void HandleMouseClicks(Rect gridArea, int gridRows, int gridCols, bool isPrimary)
    {
        Event e = Event.current;

        // Left Click or Click-and-Drag to paint Category / Tray
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
        {
            if (TryGetGridPosFromMouse(e.mousePosition, gridArea, gridRows, gridCols, out Vector2Int gridPos))
            {
                GUIUtility.keyboardControl = 0;
                GUI.FocusControl(null);

                if (isPrimary)
                {
                    if (cellTexts.ContainsKey(gridPos) && !string.IsNullOrEmpty(cellTexts[gridPos]))
                    {
                        if (!string.IsNullOrEmpty(categoriesDropdown) && categoryColors.ContainsKey(categoriesDropdown))
                        {
                            if (!cellCategory.ContainsKey(gridPos) || cellCategory[gridPos] != categoriesDropdown)
                            {
                                cellCategory[gridPos] = categoriesDropdown;
                                e.Use();
                                Repaint();
                            }
                        }
                        else if (e.type == EventType.MouseDown)
                        {
                            Debug.LogWarning("Select a valid category with an assigned color before painting.");
                        }
                    }
                }
                else
                {
                    if (trayCells.ContainsKey(gridPos) && !string.IsNullOrEmpty(trayCells[gridPos]))
                    {
                        if (!string.IsNullOrEmpty(trayDropdown))
                        {
                            if (!trayName.ContainsKey(gridPos) || trayName[gridPos] != trayDropdown)
                            {
                                trayName[gridPos] = trayDropdown;
                                e.Use();
                                Repaint();
                            }
                        }
                    }
                }
            }
        }
        // Right Click to toggle Excluded Character (Primary) or Toggle Blocked State (Bottom)
        else if (e.type == EventType.MouseDown && e.button == 1)
        {
            if (TryGetGridPosFromMouse(e.mousePosition, gridArea, gridRows, gridCols, out Vector2Int gridPos))
            {
                if (isPrimary)
                {
                    if (excludedChar.Contains(gridPos))
                        excludedChar.Remove(gridPos);
                    else
                        excludedChar.Add(gridPos);
                }
                else
                {
                    // Bottom grid: only works on empty cells (no letter assigned)
                    bool isEmpty = !trayCells.ContainsKey(gridPos) || string.IsNullOrEmpty(trayCells[gridPos]);

                    if (isEmpty)
                    {
                        if (blockedCells.Contains(gridPos))
                            blockedCells.Remove(gridPos);
                        else
                            blockedCells.Add(gridPos);
                    }
                }

                e.Use();
                Repaint();
            }
        }
    }

    private void HandleKeyStrokes(Rect gridArea, int gridRows, int gridCols, bool isPrimary)
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown)
        {
            if (TryGetGridPosFromMouse(e.mousePosition, gridArea, gridRows, gridCols, out Vector2Int gridPos))
            {
                if (e.keyCode == KeyCode.Backspace)
                {
                    if (isPrimary)
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
                    else
                    {
                        if (trayCells.ContainsKey(gridPos))
                        {
                            trayCells.Remove(gridPos);
                            trayName.Remove(gridPos);
                            e.Use();
                            Repaint();
                        }
                    }
                }
                else if (char.IsLetter(e.character))
                {
                    if (isPrimary)
                    {
                        cellTexts[gridPos] = e.character.ToString().ToUpper();
                        if (!string.IsNullOrEmpty(categoriesDropdown) && categoryColors.ContainsKey(categoriesDropdown) && categoryColors[categoriesDropdown] != null)
                        {
                            cellCategory[gridPos] = categoriesDropdown;
                        }
                    }
                    else
                    {
                        if (!blockedCells.Contains(gridPos))
                        {
                            trayCells[gridPos] = e.character.ToString().ToUpper();
                            if (!string.IsNullOrEmpty(trayDropdown))
                            {
                                trayName[gridPos] = trayDropdown;
                            }
                        }
                    }
                    e.Use();
                    Repaint();
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

    private void EditorGridSerializationCheck()
    {
        var outOfBoundsCategory = cellCategory.Keys.Where(pos => pos.x >= rows || pos.y >= columns).ToList();
        foreach (var pos in outOfBoundsCategory) cellCategory.Remove(pos);

        var outOfBoundsTexts = cellTexts.Keys.Where(pos => pos.x >= rows || pos.y >= columns).ToList();
        foreach (var pos in outOfBoundsTexts) cellTexts.Remove(pos);

        excludedChar.RemoveWhere(pos => pos.x >= rows || pos.y >= columns);

        var outOfBoundsTrayName = trayName.Keys.Where(pos => pos.x >= height || pos.y >= width).ToList();
        foreach (var pos in outOfBoundsTrayName) trayName.Remove(pos);

        var outOfBoundsTrayCells = trayCells.Keys.Where(pos => pos.x >= height || pos.y >= width).ToList();
        foreach (var pos in outOfBoundsTrayCells) trayCells.Remove(pos);

        blockedCells.RemoveWhere(pos => pos.x >= height || pos.y >= width);
    }

    private void ShowExcludedLetters()
    {
        GUILayout.Space(10);
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
                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);

        // 1. Collect excluded characters from the primary grid (ordered bottom-to-top, left-to-right)
        List<string> remainingExcluded = excludedChar
            .Where(pos => cellTexts.ContainsKey(pos) && !string.IsNullOrEmpty(cellTexts[pos]))
            .OrderByDescending(pos => pos.x)
            .ThenBy(pos => pos.y)
            .Select(pos => cellTexts[pos])
            .ToList();

        // 2. Collect characters typed in the bottom grid
        List<string> bottomChars = trayCells
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .OrderBy(kvp => kvp.Key.x)
            .ThenBy(kvp => kvp.Key.y)
            .Select(kvp => kvp.Value)
            .ToList();

        List<string> extraCharsList = new List<string>();

        // 3. Pop matched letters from remainingExcluded, and collect overflow/unmatched in extraCharsList
        foreach (string bChar in bottomChars)
        {
            int matchIndex = remainingExcluded.FindIndex(c => string.Equals(c, bChar, System.StringComparison.OrdinalIgnoreCase));
            if (matchIndex >= 0)
            {
                remainingExcluded.RemoveAt(matchIndex);
            }
            else
            {
                extraCharsList.Add(bChar);
            }
        }

        string excludedString = string.Join(", ", remainingExcluded);
        extraChar = string.Join(", ", extraCharsList);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("Excluded Chars", excludedString);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("ExtraChar", extraChar);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
    }
}