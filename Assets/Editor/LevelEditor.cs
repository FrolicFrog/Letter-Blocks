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
    private float bottomGridSize = 0.6f; // Bottom Grid Scale Factor
    private int hearts = 3;

    private float gap = 2f;
    private float horizontalMargin = 20f;

    [StringDropdown("Data/Categories.txt")]
    public string categoriesDropdown;
    private string inputCategory;

    private Color gridCellColor;
    private Color unassignedCellColor; // Color for cells with letters but no category assigned
    private Material categoryMaterial;

    private HashSet<string> words = new();
    private HashSet<Vector2Int> excludedChar = new();

    private Dictionary<Vector2Int, string> cellCategory = new();
    private Dictionary<Vector2Int, string> cellTexts = new();
    private Dictionary<string, Material> categoryColors = new();
    private Dictionary<string, List<Vector2Int>> wordPositions = new();
    private Dictionary<string, List<string>> wordCategory = new();

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
        gridCellColor = new Color(0.3f, 0.3f, 0.3f);
        unassignedCellColor = new Color(0.2f, 0.4f, 0.55f); // Distinct dark slate blue
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
                bottomGridSize = 0.6f;
                categoryMaterial = null;
                hearts = 3;
                words.Clear();
                excludedChar.Clear();
                cellCategory.Clear();
                cellTexts.Clear();
                categoryColors.Clear();
                wordPositions.Clear();
                wordCategory.Clear();
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
        currentData.bottomGridSize = bottomGridSize;
        currentData.hearts = hearts;
        currentData.words = words.ToList();
        currentData.excludedChar = excludedChar.ToList();

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
        bottomGridSize = CurLvlData.bottomGridSize;
        hearts = CurLvlData.hearts;
        words = CurLvlData.words.ToHashSet();
        excludedChar = CurLvlData.excludedChar.ToHashSet();

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
        categoryMaterial = (!string.IsNullOrEmpty(categoriesDropdown) && categoryColors.ContainsKey(categoriesDropdown)) ? categoryColors[categoriesDropdown] : null;
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

        HandleMouseClicks(primaryGridArea);
        HandleKeyStrokes(primaryGridArea);

        GUILayout.Space(30);

        // --- Bottom Grid Controls & Display ---
        bottomGridSize = EditorGUILayout.Slider("Bottom Grid Size", bottomGridSize, 0.05f, 1f);
        height = EditorGUILayout.IntSlider("Bottom Grid Rows", height, 1, 20);
        width = EditorGUILayout.IntSlider("Bottom Grid Columns", width, 1, 20);

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
                    bool hasCategory = cellCategory.ContainsKey(gridPos) &&
                                       !string.IsNullOrEmpty(cellCategory[gridPos]) &&
                                       categoryColors.ContainsKey(cellCategory[gridPos]);

                    bool hasText = cellTexts.ContainsKey(gridPos) && !string.IsNullOrEmpty(cellTexts[gridPos]);

                    if (hasCategory)
                    {
                        cellBgColor = excludedChar.Contains(gridPos) ? Color.white * 0.64f : categoryColors[cellCategory[gridPos]].color;
                    }
                    else if (hasText)
                    {
                        // Cell has a letter but no assigned category/color
                        cellBgColor = unassignedCellColor;
                    }
                }

                GUI.color = cellBgColor;
                GUI.DrawTexture(cellRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
                GUI.color = Color.white;

                string cellText = $"{r},{c}";
                GUIStyle activeStyle = defaultLabelStyle;

                if (isPrimary && cellTexts.ContainsKey(gridPos))
                {
                    cellText = cellTexts[gridPos];
                    activeStyle = boldLabelStyle;
                }

                Color previousContentColor = GUI.contentColor;
                GUI.contentColor = isPrimary && cellTexts.ContainsKey(gridPos) ? GetContrastColor(cellBgColor) : Color.white;

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

    private void HandleMouseClicks(Rect gridArea)
    {
        Event e = Event.current;

        // Left Click or Click-and-Drag to paint Category & Color
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
        {
            if (TryGetGridPosFromMouse(e.mousePosition, gridArea, rows, columns, out Vector2Int gridPos))
            {
                GUIUtility.keyboardControl = 0;
                GUI.FocusControl(null);

                // Check if the target cell actually contains a letter before painting
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
        }
        // Right Click to toggle Excluded Character
        else if (e.type == EventType.MouseDown && e.button == 1)
        {
            if (TryGetGridPosFromMouse(e.mousePosition, gridArea, rows, columns, out Vector2Int gridPos))
            {
                if (excludedChar.Contains(gridPos))
                    excludedChar.Remove(gridPos);
                else
                    excludedChar.Add(gridPos);

                e.Use();
                Repaint();
            }
        }
    }

    private void HandleKeyStrokes(Rect gridArea)
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown)
        {
            if (TryGetGridPosFromMouse(e.mousePosition, gridArea, rows, columns, out Vector2Int gridPos))
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
                    cellTexts[gridPos] = e.character.ToString().ToUpper();
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
            }
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();

        // Orders grid positions starting from the highest row index (bottom row) down to 0 (top row),
        // and left to right (columns 0 to N) within each row.
        string excludedString = string.Join(", ", excludedChar
            .Where(pos => cellTexts.ContainsKey(pos) && !string.IsNullOrEmpty(cellTexts[pos]))
            .OrderByDescending(pos => pos.x)
            .ThenBy(pos => pos.y)
            .Select(pos => cellTexts[pos]));

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("Excluded Chars", excludedString);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);
    }
}