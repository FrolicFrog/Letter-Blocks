using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Grid))]
public class BottomGridManager : MonoBehaviour
{
    public enum StartCorner
    {
        BottomLeft,
        TopLeft
    }

    [Header("Grid Layout")]
    [Min(1)] public int height = 8;
    [Min(1)] public int width = 8;

    [Tooltip("Where should the first child (Child 0) be placed? Both options will always read Left-to-Right.")]
    public StartCorner startCorner = StartCorner.TopLeft;

    [Header("Slot & Tray References")]
    public GameObject emptySlot;
    public GameObject cell1, cell2, outline, letter, blockWall;
    public List<WallDirectionPair> wallsDirection;

    [Header("Center Border Settings")]
    [Tooltip("Assign a GameObject here to automatically place and scale it around the grid.")]
    public GameObject centerObject;

    [Tooltip("If true, automatically resizes the border object to fit the outer bounds of the grid.")]
    public bool autoScaleBorder = true;

    [Tooltip("Extra padding around the outer edge of the grid for the border (X, Y, Z).")]
    public Vector3 borderPadding = Vector3.zero;

    [Header("Auto-Fit To Camera")]
    [Tooltip("Scale width to match the screen and anchor the top row to the Top Boundary line.")]
    public bool autoFitToScreen = true;
    public Camera mainCamera;

    [Tooltip("The Y-axis level where the grid sits (should match your floor height).")]
    public float floorHeight = 0f;

    [Tooltip("Percentage of empty space to leave on the Left/Right edges (0.0 to 0.5)")]
    [Range(0f, 0.5f)] public float screenPadding = 0.05f;

    [Tooltip("Percentage of extra empty space to leave on the Bottom edge (0.0 to 0.5) to prevent border clipping.")]
    [Range(0f, 0.5f)] public float bottomScreenPadding = 0.02f;

    [Header("Safe Area Boundary")]
    [Tooltip("The upper boundary percentage for the bottom grid (Set to 0.35 to align directly below TopGridManager). Grid grows DOWNWARDS from this line.")]
    [Range(0.05f, 0.95f)] public float topBoundaryReserved = 0.35f;

    [Header("Tray Mesh Settings")]
    [Tooltip("Size of the chamfer/bevel applied to the outer top edge of the procedural tray walls.")]
    [Min(0f)] public float wallBevelSize = 0.04f;

    [Tooltip("Number of steps to round out the bevel. 1 = flat chamfer. 3+ = smooth rounded corner.")]
    [Min(1)] public int wallBevelSmoothness = 3;

    [Header("Corner Bevel Settings")]
    [Tooltip("Size of the rounded corners on the outer boundary of the tray.")]
    [Min(0f)] public float cornerBevelSize = 0.1f;
    [Tooltip("Number of steps to smooth the tray's corner curves.")]
    [Min(1)] public int cornerBevelSmoothness = 5;

    [Header("Tray Mesh Settings")]
    [Tooltip("Thickness of the procedural tray walls.")]
    [Min(0.01f)] public float wallThickness = 0.15f;



    public static BottomGridManager Instance;

    private Grid grid;

    // Camera Tracking
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastCameraAspect;
    private float lastCameraFOV;
    private float lastCameraOrthoSize;

    // Parameter Tracking for Update Loop
    private int lastHeight;
    private int lastWidth;
    private Vector3 lastBorderPadding;
    private float lastScreenPadding;
    private float lastBottomScreenPadding;
    private float lastTopBoundaryReserved;
    private float lastFloorHeight;
    private Vector3 lastCellSize;
    private Vector3 lastCellGap;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        Instance = this;
        grid = GetComponent<Grid>();

        CacheInitialValues();
        ArrangeChildren();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            bool screenChanged = CheckScreenAndCameraChanges();
            bool paramsChanged = CheckParameterChanges();

            if (screenChanged || paramsChanged)
            {
                ArrangeChildren();
            }
        }
    }

    private void OnValidate()
    {
        if (grid == null) grid = GetComponent<Grid>();
    }

    private void CacheInitialValues()
    {
        if (grid != null)
        {
            lastCellSize = grid.cellSize;
            lastCellGap = grid.cellGap;
        }

        lastHeight = height;
        lastWidth = width;
        lastBorderPadding = borderPadding;
        lastScreenPadding = screenPadding;
        lastBottomScreenPadding = bottomScreenPadding;
        lastTopBoundaryReserved = topBoundaryReserved;
        lastFloorHeight = floorHeight;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastCameraAspect = mainCamera.aspect;
            lastCameraOrthoSize = mainCamera.orthographicSize;
            lastCameraFOV = mainCamera.fieldOfView;
        }
    }

    private bool CheckParameterChanges()
    {
        bool changed = false;

        if (height != lastHeight) { lastHeight = height; changed = true; }
        if (width != lastWidth) { lastWidth = width; changed = true; }
        if (borderPadding != lastBorderPadding) { lastBorderPadding = borderPadding; changed = true; }
        if (screenPadding != lastScreenPadding) { lastScreenPadding = screenPadding; changed = true; }
        if (bottomScreenPadding != lastBottomScreenPadding) { lastBottomScreenPadding = bottomScreenPadding; changed = true; }
        if (topBoundaryReserved != lastTopBoundaryReserved) { lastTopBoundaryReserved = topBoundaryReserved; changed = true; }
        if (floorHeight != lastFloorHeight) { lastFloorHeight = floorHeight; changed = true; }

        if (grid != null)
        {
            if (grid.cellSize != lastCellSize) { lastCellSize = grid.cellSize; changed = true; }
            if (grid.cellGap != lastCellGap) { lastCellGap = grid.cellGap; changed = true; }
        }

        return changed;
    }

    private bool CheckScreenAndCameraChanges()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return false;

        bool hasChanged = false;

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            hasChanged = true;
        }

        if (!Mathf.Approximately(mainCamera.aspect, lastCameraAspect))
        {
            lastCameraAspect = mainCamera.aspect;
            hasChanged = true;
        }

        float currentSize = mainCamera.orthographic ? mainCamera.orthographicSize : mainCamera.fieldOfView;
        float lastSize = mainCamera.orthographic ? lastCameraOrthoSize : lastCameraFOV;

        if (!Mathf.Approximately(currentSize, lastSize))
        {
            if (mainCamera.orthographic) lastCameraOrthoSize = currentSize;
            else lastCameraFOV = currentSize;

            hasChanged = true;
        }

        return hasChanged;
    }

    [ContextMenu("Generate Grid Tiles")]
    public void CreateChildren()
    {
        if (grid == null) grid = GetComponent<Grid>();

        int childCount = height * width;
        DestroyAllChildren();

        Vector3 targetScale = Vector3.one;
        if (grid != null)
        {
            targetScale = grid.cellSize;
        }

        for (int i = 0; i < childCount; i++)
        {
            if (emptySlot != null)
            {
                GameObject newSlot = Instantiate(emptySlot, transform);
                newSlot.transform.localScale = targetScale;
            }
        }

        ArrangeChildren();
    }

    [ContextMenu("Clear Grid")]
    public void DestroyAllChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
            else DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    [ContextMenu("Arrange Grid Now")]
    public void ArrangeChildren()
    {
        if (grid == null) grid = GetComponent<Grid>();
        if (grid == null) return;

        int currentChildCount = transform.childCount;
        if (currentChildCount == 0 && centerObject == null) return;

        Vector3 safeAreaAnchorOffset = Vector3.zero;
        Vector3 horizontalCenterOffset = Vector3.zero;

        // 1. Determine horizontal center offset
        // 1. Determine horizontal center offset
        // Grid.CellToLocal returns a cell's corner, not its center — add half a cell width so
        // centering is based on the true outer edge of the last column, not its near corner.
        // 1. Determine horizontal center offset
        if (width > 0)
        {
            Vector3 rightmostCellPos = grid.CellToLocal(new Vector3Int(width - 1, 0, 0));
            horizontalCenterOffset = rightmostCellPos / 2f;
        }

        // 2. Auto-Fit to Width & Calculate Top Anchor
        if (autoFitToScreen)
        {
            if (mainCamera == null) mainCamera = Camera.main;

            if (mainCamera != null)
            {
                Plane floorPlane = new Plane(Vector3.up, new Vector3(0, floorHeight, 0));

                float maxY = topBoundaryReserved - screenPadding;
                float minX = screenPadding;
                float maxX = 1f - screenPadding;

                Vector3 topLeft = GetFloorIntersection(new Vector2(minX, maxY), floorPlane);
                Vector3 topRight = GetFloorIntersection(new Vector2(maxX, maxY), floorPlane);

                float frustumWidth = Vector3.Distance(topLeft, topRight);
                float gridUnscaledWidth = (width * grid.cellSize.x) + ((width - 1) * grid.cellGap.x);

                if (gridUnscaledWidth > 0)
                {
                    float finalScale = frustumWidth / gridUnscaledWidth;
                    float gridUnscaledHeight = (height * grid.cellSize.y) + ((height - 1) * grid.cellGap.y);

                    if (gridUnscaledHeight > 0)
                    {
                        if (autoScaleBorder && centerObject != null)
                        {
                            gridUnscaledHeight += (borderPadding.y / 2f);
                        }

                        float safeBottomViewportY = bottomScreenPadding;
                        if (Screen.height > 0)
                        {
                            safeBottomViewportY = Mathf.Clamp01((Screen.safeArea.yMin / Screen.height) + bottomScreenPadding);
                        }

                        Vector3 bottomCenterAtBoundary = GetFloorIntersection(new Vector2((minX + maxX) / 2f, maxY), floorPlane);
                        Vector3 bottomCenterAtFloor = GetFloorIntersection(new Vector2((minX + maxX) / 2f, safeBottomViewportY), floorPlane);

                        float availableFrustumHeight = Vector3.Distance(bottomCenterAtBoundary, bottomCenterAtFloor);
                        float scaleToFitHeight = availableFrustumHeight / gridUnscaledHeight;

                        finalScale = Mathf.Min(finalScale, scaleToFitHeight);
                    }

                    transform.localScale = new Vector3(finalScale, finalScale, finalScale);
                }

                Vector3 safeTopCenterWorld = (topLeft + topRight) / 2f;
                Vector3 targetTopLocal = transform.InverseTransformPoint(safeTopCenterWorld);

                Vector3 cellUpDirection = grid.CellToLocal(new Vector3Int(0, 1, 0)).normalized;
                Vector3 topRowCellPos = grid.CellToLocal(new Vector3Int(0, height - 1, 0));
                Vector3 rowTopEdgePos = topRowCellPos + (cellUpDirection * (grid.cellSize.y / 2f));

                safeAreaAnchorOffset = targetTopLocal - rowTopEdgePos;

                Vector3 predictedTopLocal = rowTopEdgePos - horizontalCenterOffset + safeAreaAnchorOffset;
                Vector3 predictedTopWorld = transform.TransformPoint(predictedTopLocal);

                Vector3 worldUpDir = transform.TransformDirection(cellUpDirection);
                if (worldUpDir.sqrMagnitude > 0.0001f)
                {
                    Vector3 worldError = Vector3.Project(safeTopCenterWorld - predictedTopWorld, worldUpDir.normalized);
                    Vector3 localErrorCorrection = transform.InverseTransformDirection(worldError);
                    safeAreaAnchorOffset += localErrorCorrection;
                }
            }
        }

        // 3. Map children Left-to-Right
        int tileIndex = 0;
        int maxTiles = height * width;

        for (int i = 0; i < currentChildCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (centerObject != null && child == centerObject.transform)
                continue;

            if (tileIndex >= maxTiles) break;

            int physical_col = tileIndex % width;
            int physical_row;

            if (startCorner == StartCorner.TopLeft)
            {
                physical_row = (height - 1) - (tileIndex / width);
            }
            else
            {
                physical_row = tileIndex / width;
            }

            Vector3 baseLocalPos = grid.CellToLocal(new Vector3Int(physical_col, physical_row, 0));
            baseLocalPos -= horizontalCenterOffset;
            baseLocalPos += safeAreaAnchorOffset;

            child.localPosition = baseLocalPos;
            tileIndex++;
        }

        // 4. Position & Auto-Size Center Border Object
        if (centerObject != null)
        {
            // Use the SAME corrected reference frame the tiles are placed in (horizontalCenterOffset +
            // safeAreaAnchorOffset), then pad by half a cell each side. This ties the border directly
            // to where the tiles actually render, instead of re-deriving corner math independently —
            // which is what caused the size/position mismatch (and the asymmetric gap) before.
            Vector3 firstTileRef = grid.CellToLocal(new Vector3Int(0, 0, 0)) - horizontalCenterOffset + safeAreaAnchorOffset;
            Vector3 lastTileRef = grid.CellToLocal(new Vector3Int(width - 1, height - 1, 0)) - horizontalCenterOffset + safeAreaAnchorOffset;

            Vector3 halfCell = grid.cellSize / 2f;
            Vector3 tileBoundsMin = Vector3.Min(firstTileRef, lastTileRef) - halfCell;
            Vector3 tileBoundsMax = Vector3.Max(firstTileRef, lastTileRef) + halfCell;

            Vector3 gridLocalCenter = (tileBoundsMin + tileBoundsMax) / 2f;

            if (centerObject.transform.parent == transform)
            {
                centerObject.transform.localPosition = gridLocalCenter;
            }
            else
            {
                centerObject.transform.position = transform.TransformPoint(gridLocalCenter);
            }

            if (autoScaleBorder)
            {
                // diff already spans the full outer extent (padded by halfCell on both sides above),
                // so do NOT add cellSize again here like the old formula did.
                Vector3 diff = tileBoundsMax - tileBoundsMin;

                float totalOuterWidth = Mathf.Abs(diff.x) + borderPadding.x;
                float totalOuterHeight = Mathf.Abs(diff.y) + borderPadding.y;
                float totalOuterDepth = Mathf.Abs(diff.z) + borderPadding.z;

                bool isChild = centerObject.transform.parent == transform;
                Vector3 gridScale = transform.localScale;

                Vector3 targetLocalSize = new Vector3(
                    totalOuterWidth,
                    totalOuterHeight,
                    totalOuterDepth > 0f ? totalOuterDepth : centerObject.transform.localScale.z
                );

                // ... everything below this point (the isChild scale conversion, SpriteRenderer /
                // MeshFilter / fallback scaling branches) stays exactly as it was — unchanged.
                if (!isChild)
                {
                    Vector3 parentScale = centerObject.transform.parent != null ? centerObject.transform.parent.lossyScale : Vector3.one;
                    Vector3 targetWorldSize = Vector3.Scale(targetLocalSize, gridScale);
                    targetLocalSize = new Vector3(
                        parentScale.x != 0 ? targetWorldSize.x / parentScale.x : targetWorldSize.x,
                        parentScale.y != 0 ? targetWorldSize.y / parentScale.y : targetWorldSize.y,
                        parentScale.z != 0 ? targetWorldSize.z / parentScale.z : targetWorldSize.z
                    );
                }

                SpriteRenderer sr = centerObject.GetComponent<SpriteRenderer>();
                MeshFilter mf = centerObject.GetComponent<MeshFilter>();

                if (sr != null && sr.drawMode != SpriteDrawMode.Simple)
                {
                    centerObject.transform.localScale = new Vector3(1f, 1f, targetLocalSize.z > 0 ? targetLocalSize.z : centerObject.transform.localScale.z);
                    sr.size = new Vector2(targetLocalSize.x, targetLocalSize.y);
                }
                else if (sr != null && sr.sprite != null)
                {
                    Vector2 spriteSize = sr.sprite.rect.size / sr.sprite.pixelsPerUnit;

                    if (spriteSize.x > 0 && spriteSize.y > 0)
                    {
                        centerObject.transform.localScale = new Vector3(
                            targetLocalSize.x / spriteSize.x,
                            targetLocalSize.y / spriteSize.y,
                            targetLocalSize.z > 0 ? targetLocalSize.z : centerObject.transform.localScale.z
                        );
                    }
                }
                else if (mf != null && mf.sharedMesh != null)
                {
                    Vector3 meshSize = mf.sharedMesh.bounds.size;

                    float scaleX = meshSize.x > 0f ? targetLocalSize.x / meshSize.x : targetLocalSize.x;
                    float scaleY = meshSize.y > 0f ? targetLocalSize.y / meshSize.y : targetLocalSize.y;
                    float scaleZ = (meshSize.z > 0f && totalOuterDepth > 0f) ? targetLocalSize.z / meshSize.z : centerObject.transform.localScale.z;

                    centerObject.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
                }
                else
                {
                    centerObject.transform.localScale = targetLocalSize;
                }
            }
        }
    }

    private Vector3 GetFloorIntersection(Vector2 viewportPos, Plane floorPlane)
    {
        Ray ray = mainCamera.ViewportPointToRay(viewportPos);
        if (floorPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return ray.GetPoint(mainCamera.farClipPlane);
    }
    #region Procedural Tray Generation

    private struct CrossSection
    {
        public Vector2 Outer, Inner;
        public CrossSection(Vector2 outer, Vector2 inner) { Outer = outer; Inner = inner; }
    }

    private float GetDistanceToBoundary(Vector2 point, List<(Vector2, Vector2)> boundaries)
    {
        float minDist = float.MaxValue;
        foreach (var seg in boundaries)
        {
            Vector2 pa = point - seg.Item1;
            Vector2 ba = seg.Item2 - seg.Item1;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            float dist = (pa - ba * h).magnitude;
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }

    public GameObject CreateTray(List<Vector2Int> gridPos, float wallHeight, Material trayMaterial, Vector3 scale, bool openTray = true, Dictionary<Vector2Int, string> charcter = null)
    {
        ArrangeChildren();

        if (grid == null) grid = GetComponent<Grid>();
        if (grid == null || gridPos == null || gridPos.Count == 0) return null;

        float floorThickness = 0.1f;
        float maxBevel = Mathf.Min(wallThickness / 2f, wallHeight / 2f) * 0.95f;
        float bevel = Mathf.Clamp(wallBevelSize, 0f, maxBevel);

        float cellWidth = grid.cellSize.x + grid.cellGap.x;
        float cellDepth = grid.cellSize.y + grid.cellGap.y;
        float safeCornerRadius = Mathf.Clamp(cornerBevelSize, 0.001f, Mathf.Min(cellWidth, cellDepth) / 2.1f);

        Vector3 totalAlignmentOffset = Vector3.zero;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (centerObject != null && child == centerObject.transform) continue;
            if (child.name.StartsWith("Procedural_Tray")) continue;

            int physical_col = 0;
            int physical_row = (startCorner == StartCorner.TopLeft) ? (height - 1) : 0;
            Vector3 rawPos = grid.CellToLocal(new Vector3Int(physical_col, physical_row, 0));
            totalAlignmentOffset = child.localPosition - rawPos;
            break;
        }

        HashSet<Vector2Int> shape = new HashSet<Vector2Int>();
        foreach (Vector2Int pos in gridPos)
        {
            int gridX = pos.y;
            int gridY = (startCorner == StartCorner.TopLeft) ? (height - 1 - pos.x) : pos.x;
            shape.Add(new Vector2Int(gridX, gridY));
        }

        Vector3 minBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 maxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (Vector2Int p in shape)
        {
            Vector3 cellWorld = grid.CellToLocal(new Vector3Int(p.x, p.y, 0)) + totalAlignmentOffset;
            minBounds = Vector3.Min(minBounds, cellWorld);
            maxBounds = Vector3.Max(maxBounds, cellWorld);
        }
        Vector3 shapeCenter = (minBounds + maxBounds) / 2f;

        GameObject trayObj = new GameObject("Procedural_Tray");
        trayObj.transform.SetParent(this.transform);
        trayObj.transform.localPosition = shapeCenter;
        trayObj.transform.localRotation = Quaternion.identity;
        trayObj.transform.localScale = scale;
        trayObj.layer = LayerMask.NameToLayer("Tray") != -1 ? LayerMask.NameToLayer("Tray") : 0;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        Vector3 CellLocalPos(int col, int row) =>
            grid.CellToLocal(new Vector3Int(col, row, 0)) + totalAlignmentOffset - shapeCenter;

        // --- Outer Perimeter Line Segments for Distance Calculation ---
        List<(Vector2, Vector2)> boundarySegments = new List<(Vector2, Vector2)>();
        Vector3 cell00 = grid.CellToLocal(Vector3Int.zero) + totalAlignmentOffset - shapeCenter;
        Vector2 basePos = new Vector2(cell00.x - cellWidth / 2f, cell00.z - cellDepth / 2f);

        foreach (Vector2Int p in shape)
        {
            if (!shape.Contains(p + Vector2Int.up))
                boundarySegments.Add((basePos + new Vector2(p.x * cellWidth, (p.y + 1) * cellDepth), basePos + new Vector2((p.x + 1) * cellWidth, (p.y + 1) * cellDepth)));
            if (!shape.Contains(p + Vector2Int.right))
                boundarySegments.Add((basePos + new Vector2((p.x + 1) * cellWidth, (p.y + 1) * cellDepth), basePos + new Vector2((p.x + 1) * cellWidth, p.y * cellDepth)));
            if (!shape.Contains(p + Vector2Int.down))
                boundarySegments.Add((basePos + new Vector2((p.x + 1) * cellWidth, p.y * cellDepth), basePos + new Vector2(p.x * cellWidth, p.y * cellDepth)));
            if (!shape.Contains(p + Vector2Int.left))
                boundarySegments.Add((basePos + new Vector2(p.x * cellWidth, p.y * cellDepth), basePos + new Vector2(p.x * cellWidth, (p.y + 1) * cellDepth)));
        }

        // --- 1. Base Floor Generation ---
        float floorCornerCut = safeCornerRadius;

        foreach (Vector2Int p in shape)
        {
            Vector3 cellCenter = CellLocalPos(p.x, p.y);

            bool cutTL = !shape.Contains(p + Vector2Int.left) && !shape.Contains(p + Vector2Int.up);
            bool cutTR = !shape.Contains(p + Vector2Int.right) && !shape.Contains(p + Vector2Int.up);
            bool cutBR = !shape.Contains(p + Vector2Int.right) && !shape.Contains(p + Vector2Int.down);
            bool cutBL = !shape.Contains(p + Vector2Int.left) && !shape.Contains(p + Vector2Int.down);

            AddFloorTileMesh(
                cellCenter + new Vector3(0, floorThickness / 2f, 0),
                cellWidth / 2f, cellDepth / 2f, floorThickness,
                cutTL ? floorCornerCut : 0f, cutTR ? floorCornerCut : 0f,
                cutBR ? floorCornerCut : 0f, cutBL ? floorCornerCut : 0f,
                vertices, triangles, uvs, boundarySegments
            );
        }

        // --- 2. Generate Continuous Swept Walls ---
        GenerateContinuousWalls(shape, cellWidth, cellDepth, wallThickness, wallHeight, bevel, wallBevelSmoothness, safeCornerRadius, cornerBevelSmoothness, totalAlignmentOffset, shapeCenter, vertices, triangles, uvs, openTray);

        // --- 3. Flat Seamless Roof Generation (If Closed) ---
        if (!openTray)
        {
            foreach (Vector2Int p in shape)
            {
                Vector3 cellCenter = CellLocalPos(p.x, p.y);

                bool outT = !shape.Contains(p + Vector2Int.up);
                bool outR = !shape.Contains(p + Vector2Int.right);
                bool outB = !shape.Contains(p + Vector2Int.down);
                bool outL = !shape.Contains(p + Vector2Int.left);

                bool convexTL = outL && outT;
                bool concaveTL = !outL && !outT && !shape.Contains(p + new Vector2Int(-1, 1));

                bool convexTR = outR && outT;
                bool concaveTR = !outR && !outT && !shape.Contains(p + new Vector2Int(1, 1));

                bool convexBR = outR && outB;
                bool concaveBR = !outR && !outB && !shape.Contains(p + new Vector2Int(1, -1));

                bool convexBL = outL && outB;
                bool concaveBL = !outL && !outB && !shape.Contains(p + new Vector2Int(-1, -1));

                AddFlatTopMesh(
                    cellCenter + new Vector3(0, wallHeight, 0),
                    cellWidth / 2f, cellDepth / 2f, bevel, safeCornerRadius, cornerBevelSmoothness,
                    outT, outR, outB, outL,
                    convexTL, concaveTL,
                    convexTR, concaveTR,
                    convexBR, concaveBR,
                    convexBL, concaveBL,
                    vertices, triangles, uvs, boundarySegments
                );
            }
        }

        // --- 4. Build and Apply Mesh ---
        Mesh proceduralMesh = new Mesh { name = "Procedural_Tray_Mesh" };
        proceduralMesh.vertices = vertices.ToArray();
        proceduralMesh.triangles = triangles.ToArray();
        proceduralMesh.uv = uvs.ToArray();
        proceduralMesh.RecalculateNormals();
        proceduralMesh.RecalculateBounds();

        MeshFilter mf = trayObj.AddComponent<MeshFilter>();
        mf.mesh = proceduralMesh;

        MeshRenderer mr = trayObj.AddComponent<MeshRenderer>();
        if (trayMaterial != null) mr.sharedMaterial = trayMaterial;
        else if (cell1 != null && cell1.GetComponentInChildren<MeshRenderer>() != null) mr.sharedMaterial = cell1.GetComponentInChildren<MeshRenderer>().sharedMaterial;
        else mr.sharedMaterial = new Material(Shader.Find("Standard"));

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);

        Vector4[] boundaryArray = new Vector4[64];
        int boundCount = Mathf.Min(boundarySegments.Count, 64);

        for (int i = 0; i < boundCount; i++)
        {
            // Pack Start (x,y) and End (z,w) of the segment into a Vector4
            boundaryArray[i] = new Vector4(
                boundarySegments[i].Item1.x,
                boundarySegments[i].Item1.y,
                boundarySegments[i].Item2.x,
                boundarySegments[i].Item2.y
            );
        }

        mpb.SetFloat("_BoundaryCount", boundCount);
        mpb.SetVectorArray("_Boundaries", boundaryArray);
        mr.SetPropertyBlock(mpb);

        MeshCollider mc = trayObj.AddComponent<MeshCollider>();
        mc.sharedMesh = proceduralMesh;

        // Reset tray to perfectly match the cell size boundaries on the floor
        trayObj.transform.localScale = scale;
        // --- 5. Spawn Letters Based on Dictionary (Final Uniform Container Fix) ---
        // --- 5. Spawn Letters Based on Dictionary (Contiguous Neighbor Scaling) ---
        if (charcter != null && letter != null)
        {
            GameObject lettersContainer = new GameObject("Letters_Container");
            lettersContainer.transform.SetParent(trayObj.transform);

            // Lift the container up so letters sit on the tray floor
            lettersContainer.transform.localPosition = new Vector3(0, floorThickness, 0);

            // Keep the container at a 1:1 scale
            lettersContainer.transform.localScale = Vector3.one;

            // Safe buffer to prevent z-fighting with the wall
            float buffer = wallThickness * 1.025f;

            foreach (Vector2Int pos in gridPos)
            {
                if (charcter.TryGetValue(pos, out string textValue))
                {
                    int gridX = pos.y;
                    int gridY = (startCorner == StartCorner.TopLeft) ? (height - 1 - pos.x) : pos.x;
                    Vector2Int physicalPos = new Vector2Int(gridX, gridY);

                    // 1. Check neighbors using the physical grid shape
                    bool hasTop = shape.Contains(physicalPos + Vector2Int.up);
                    bool hasBottom = shape.Contains(physicalPos + Vector2Int.down);
                    bool hasRight = shape.Contains(physicalPos + Vector2Int.right);
                    bool hasLeft = shape.Contains(physicalPos + Vector2Int.left);

                    // 2. Base cell position
                    Vector3 rawCenter = CellLocalPos(gridX, gridY);

                    // 3. Calculate internal bounds for this specific block
                    float halfW = cellWidth / 2f;
                    float halfD = cellDepth / 2f;

                    // If a neighbor exists, extend all the way to the edge (0 offset). 
                    // Otherwise, pull back by the wall buffer.
                    float minX = -halfW + (hasLeft ? 0f : buffer);
                    float maxX = halfW - (hasRight ? 0f : buffer);

                    float minZ = -halfD + (hasBottom ? 0f : buffer);
                    float maxZ = halfD - (hasTop ? 0f : buffer);

                    // 4. Determine the new center offset & scale factor
                    float offsetX = (minX + maxX) / 2f;
                    float offsetZ = (minZ + maxZ) / 2f;

                    float scaleX = (maxX - minX) / cellWidth;
                    float scaleZ = (maxZ - minZ) / cellDepth;

                    // 5. Final positioning
                    Vector3 finalLocalPos = new Vector3(rawCenter.x + offsetX, 0, rawCenter.z + offsetZ);

                    GameObject instantiatedLetter = Instantiate(letter, lettersContainer.transform);
                    instantiatedLetter.transform.localPosition = finalLocalPos;

                    // Apply the non-uniform scale so it stretches to touch neighbors but shrinks from walls
                    instantiatedLetter.transform.localScale = new Vector3(scaleX, 2.5f, scaleZ);

                    TMP_Text textMesh = instantiatedLetter.GetComponentInChildren<TMP_Text>();
                    if (textMesh != null)
                    {
                        textMesh.text = textValue;

                        // Optional: Counter-scale the text so it doesn't look stretched when the block becomes rectangular
                        Vector3 baseTextScale = textMesh.transform.localScale;
                        textMesh.transform.localScale = new Vector3(
                            baseTextScale.x / scaleX,
                            baseTextScale.y,
                            baseTextScale.z / scaleZ
                        );
                    }
                }
            }
        }
        return trayObj;
    }

    private void GenerateContinuousWalls(HashSet<Vector2Int> shape, float cellW, float cellD, float wallThickness, float height, float bevel, int wallSmoothness, float cornerRadius, int cornerSmoothness, Vector3 offset, Vector3 center, List<Vector3> verts, List<int> tris, List<Vector2> uvs, bool openTray)
    {
        Dictionary<Vector2Int, List<Vector2Int>> edgeMap = new Dictionary<Vector2Int, List<Vector2Int>>();
        void AddEdge(Vector2Int from, Vector2Int to) { if (!edgeMap.ContainsKey(from)) edgeMap[from] = new List<Vector2Int>(); edgeMap[from].Add(to); }

        foreach (Vector2Int p in shape)
        {
            if (!shape.Contains(p + Vector2Int.up)) AddEdge(new Vector2Int(p.x, p.y + 1), new Vector2Int(p.x + 1, p.y + 1));
            if (!shape.Contains(p + Vector2Int.right)) AddEdge(new Vector2Int(p.x + 1, p.y + 1), new Vector2Int(p.x + 1, p.y));
            if (!shape.Contains(p + Vector2Int.down)) AddEdge(new Vector2Int(p.x + 1, p.y), new Vector2Int(p.x, p.y));
            if (!shape.Contains(p + Vector2Int.left)) AddEdge(new Vector2Int(p.x, p.y), new Vector2Int(p.x, p.y + 1));
        }

        List<List<Vector2Int>> loops = new List<List<Vector2Int>>();
        while (edgeMap.Count > 0)
        {
            Vector2Int startNode = edgeMap.Keys.First();
            List<Vector2Int> loop = new List<Vector2Int>();
            Vector2Int curr = startNode;

            while (edgeMap.ContainsKey(curr))
            {
                loop.Add(curr);
                List<Vector2Int> nextList = edgeMap[curr];
                Vector2Int next = nextList[0];
                nextList.RemoveAt(0);
                if (nextList.Count == 0) edgeMap.Remove(curr);
                curr = next;
                if (curr == startNode) break;
            }
            loops.Add(loop);
        }

        List<Vector2> profile = new List<Vector2>();
        profile.Add(new Vector2(0, 0));
        profile.Add(new Vector2(0, height - bevel));

        wallSmoothness = Mathf.Max(1, wallSmoothness);
        for (int s = 1; s <= wallSmoothness; s++)
        {
            float t = s / (float)wallSmoothness;
            profile.Add(new Vector2(bevel - bevel * Mathf.Cos(t * Mathf.PI / 2f), height - bevel + bevel * Mathf.Sin(t * Mathf.PI / 2f)));
        }

        if (openTray)
        {
            for (int s = 0; s <= wallSmoothness; s++)
            {
                float t = s / (float)wallSmoothness;
                float angle = Mathf.PI / 2f * (1f - t);
                profile.Add(new Vector2(wallThickness - bevel + bevel * Mathf.Cos(angle), height - bevel + bevel * Mathf.Sin(angle)));
            }
            profile.Add(new Vector2(wallThickness, 0));
        }
        else
        {
            profile.Add(new Vector2(bevel, 0));
        }

        int P = profile.Count;
        cornerSmoothness = Mathf.Max(1, cornerSmoothness);

        Vector2 GetIntersection(Vector2 p1, Vector2 d1, Vector2 p2, Vector2 d2)
        {
            float det = d1.x * d2.y - d1.y * d2.x;
            if (Mathf.Abs(det) < 0.0001f) return p1;
            float t = ((p2.x - p1.x) * d2.y - (p2.y - p1.y) * d2.x) / det;
            return p1 + d1 * t;
        }

        foreach (var loop in loops)
        {
            int N = loop.Count;
            if (N < 2) continue;

            List<CrossSection> sections = new List<CrossSection>();

            for (int i = 0; i < N; i++)
            {
                Vector2Int prev = loop[(i - 1 + N) % N];
                Vector2Int curr = loop[i];
                Vector2Int next = loop[(i + 1) % N];

                Vector3 cell00 = grid.CellToLocal(Vector3Int.zero) + offset - center;
                Vector2 v0 = new Vector2(cell00.x + prev.x * cellW - cellW / 2f, cell00.z + prev.y * cellD - cellD / 2f);
                Vector2 v1 = new Vector2(cell00.x + curr.x * cellW - cellW / 2f, cell00.z + curr.y * cellD - cellD / 2f);
                Vector2 v2 = new Vector2(cell00.x + next.x * cellW - cellW / 2f, cell00.z + next.y * cellD - cellD / 2f);

                Vector2 dirIn = (v1 - v0).normalized;
                Vector2 dirOut = (v2 - v1).normalized;
                Vector2 normIn = new Vector2(dirIn.y, -dirIn.x);
                Vector2 normOut = new Vector2(dirOut.y, -dirOut.x);

                float cross = dirIn.x * dirOut.y - dirIn.y * dirOut.x;

                Vector2 startOut = v1 - dirIn * cornerRadius;
                Vector2 endOut = v1 + dirOut * cornerRadius;
                Vector2 startIn = startOut + normIn * wallThickness;
                Vector2 endIn = endOut + normOut * wallThickness;

                if (cross < -0.01f) // Convex Corners
                {
                    Vector2 pivot = startOut + normIn * cornerRadius;
                    float rOut = cornerRadius;
                    float rIn = cornerRadius - wallThickness;
                    Vector2 miter = GetIntersection(startIn, dirIn, endIn, dirOut);

                    for (int s = 0; s <= cornerSmoothness; s++)
                    {
                        float t = s / (float)cornerSmoothness;
                        Vector3 slerpDir = Vector3.Slerp(new Vector3(startOut.x - pivot.x, startOut.y - pivot.y, 0).normalized,
                                                         new Vector3(endOut.x - pivot.x, endOut.y - pivot.y, 0).normalized, t);
                        Vector2 dir = new Vector2(slerpDir.x, slerpDir.y);

                        Vector2 O = pivot + dir * rOut;
                        Vector2 I = (rIn <= 0f) ? miter : (pivot + dir * rIn);
                        sections.Add(new CrossSection(O, I));
                    }
                }
                else // Concave Corners & Straights
                {
                    Vector2 miter = GetIntersection(startIn, dirIn, endIn, dirOut);
                    sections.Add(new CrossSection(v1, miter));
                }
            }

            int baseIndex = verts.Count;
            for (int i = 0; i < sections.Count; i++)
            {
                CrossSection sec = sections[i];
                float sectionLength = Vector2.Distance(sec.Outer, sec.Inner);
                Vector2 sectionNorm = (sec.Inner - sec.Outer).normalized;

                for (int p = 0; p < P; p++)
                {
                    float mappedX = (profile[p].x / wallThickness) * sectionLength;
                    Vector2 pt2D = sec.Outer + sectionNorm * mappedX;

                    verts.Add(new Vector3(pt2D.x, profile[p].y, pt2D.y));
                    uvs.Add(new Vector2(mappedX, profile[p].y));
                }

                int next_i = (i + 1) % sections.Count;
                for (int p = 0; p < P - 1; p++)
                {
                    int v0 = baseIndex + i * P + p;
                    int v1 = baseIndex + i * P + p + 1;
                    int v2 = baseIndex + next_i * P + p;
                    int v3 = baseIndex + next_i * P + p + 1;

                    tris.Add(v0); tris.Add(v2); tris.Add(v1);
                    tris.Add(v1); tris.Add(v2); tris.Add(v3);
                }
            }
        }
    }
    private void AddFlatTopMesh(Vector3 cellCenter, float exX, float exZ, float inset, float cornerRadius, int cornerSteps,
        bool outT, bool outR, bool outB, bool outL,
        bool convexTL, bool concaveTL, bool convexTR, bool concaveTR,
        bool convexBR, bool concaveBR, bool convexBL, bool concaveBL,
        List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<(Vector2, Vector2)> boundarySegments)
    {
        float top = exZ - (outT ? inset : 0);
        float bot = -exZ + (outB ? inset : 0);
        float right = exX - (outR ? inset : 0);
        float left = -exX + (outL ? inset : 0);

        List<Vector2> pts = new List<Vector2>();

        // TL
        if (convexTL)
        {
            Vector2 center = new Vector2(-exX + cornerRadius, exZ - cornerRadius);
            float r = Mathf.Max(0f, cornerRadius - inset);
            for (int i = 0; i <= cornerSteps; i++)
            {
                float angle = Mathf.Lerp(Mathf.PI, Mathf.PI / 2f, i / (float)cornerSteps);
                pts.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
            }
        }
        else if (concaveTL)
        {
            pts.Add(new Vector2(-exX, exZ - inset));
            pts.Add(new Vector2(-exX + inset, exZ - inset));
            pts.Add(new Vector2(-exX + inset, exZ));
        }
        else
        {
            pts.Add(new Vector2(left, top));
        }

        // TR
        if (convexTR)
        {
            Vector2 center = new Vector2(exX - cornerRadius, exZ - cornerRadius);
            float r = Mathf.Max(0f, cornerRadius - inset);
            for (int i = 0; i <= cornerSteps; i++)
            {
                float angle = Mathf.Lerp(Mathf.PI / 2f, 0f, i / (float)cornerSteps);
                pts.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
            }
        }
        else if (concaveTR)
        {
            pts.Add(new Vector2(exX - inset, exZ));
            pts.Add(new Vector2(exX - inset, exZ - inset));
            pts.Add(new Vector2(exX, exZ - inset));
        }
        else
        {
            pts.Add(new Vector2(right, top));
        }

        // BR
        if (convexBR)
        {
            Vector2 center = new Vector2(exX - cornerRadius, -exZ + cornerRadius);
            float r = Mathf.Max(0f, cornerRadius - inset);
            for (int i = 0; i <= cornerSteps; i++)
            {
                float angle = Mathf.Lerp(0f, -Mathf.PI / 2f, i / (float)cornerSteps);
                pts.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
            }
        }
        else if (concaveBR)
        {
            pts.Add(new Vector2(exX, -exZ + inset));
            pts.Add(new Vector2(exX - inset, -exZ + inset));
            pts.Add(new Vector2(exX - inset, -exZ));
        }
        else
        {
            pts.Add(new Vector2(right, bot));
        }

        // BL
        if (convexBL)
        {
            Vector2 center = new Vector2(-exX + cornerRadius, -exZ + cornerRadius);
            float r = Mathf.Max(0f, cornerRadius - inset);
            for (int i = 0; i <= cornerSteps; i++)
            {
                float angle = Mathf.Lerp(-Mathf.PI / 2f, -Mathf.PI, i / (float)cornerSteps);
                pts.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
            }
        }
        else if (concaveBL)
        {
            pts.Add(new Vector2(-exX + inset, -exZ));
            pts.Add(new Vector2(-exX + inset, -exZ + inset));
            pts.Add(new Vector2(-exX, -exZ + inset));
        }
        else
        {
            pts.Add(new Vector2(left, bot));
        }

        int start = verts.Count;
        int n = pts.Count;
        for (int k = 0; k < n; k++)
        {
            verts.Add(cellCenter + new Vector3(pts[k].x, 0, pts[k].y));

            Vector2 globalPt = new Vector2(cellCenter.x + pts[k].x, cellCenter.z + pts[k].y);
            float distInward = GetDistanceToBoundary(globalPt, boundarySegments);
            uvs.Add(new Vector2(distInward, 1.0f));
        }

        for (int k = 1; k < n - 1; k++)
        {
            tris.Add(start);
            tris.Add(start + k);
            tris.Add(start + k + 1);
        }
    }

    private void AddFloorTileMesh(Vector3 cellCenter, float exX, float exZ, float thickness, float cutTL, float cutTR, float cutBR, float cutBL, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<(Vector2, Vector2)> boundarySegments)
    {
        List<Vector2> pts = new List<Vector2>
        {
            new Vector2(-exX + cutTL, exZ),
            new Vector2( exX - cutTR, exZ),
            new Vector2( exX,  exZ - cutTR),
            new Vector2( exX, -exZ + cutBR),
            new Vector2( exX - cutBR, -exZ),
            new Vector2(-exX + cutBL, -exZ),
            new Vector2(-exX, -exZ + cutBL),
            new Vector2(-exX,  exZ - cutTL)
        };

        float topY = thickness / 2f;
        float botY = -thickness / 2f;

        void AddFace(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br)
        {
            int i = verts.Count;
            verts.AddRange(new[] { bl, tl, tr, br });
            uvs.AddRange(new[] { new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f) });
            tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }

        void AddNGon(List<Vector2> p, float y, bool isTop)
        {
            int start = verts.Count;
            int n = p.Count;
            for (int k = 0; k < n; k++)
            {
                int idx = isTop ? (n - 1 - k) : k;
                Vector2 pt = p[idx];
                verts.Add(cellCenter + new Vector3(pt.x, y, pt.y));

                if (isTop)
                {
                    Vector2 globalPt = new Vector2(cellCenter.x + pt.x, cellCenter.z + pt.y);
                    float distInward = GetDistanceToBoundary(globalPt, boundarySegments);
                    uvs.Add(new Vector2(distInward, 1.0f));
                }
                else
                {
                    uvs.Add(new Vector2(0f, 0f));
                }
            }
            for (int k = 1; k < n - 1; k++)
            {
                tris.AddRange(new[] { start, start + k + 1, start + k });
            }
        }

        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[(i + 1) % pts.Count];
            if ((a - b).sqrMagnitude < 1e-8f) continue;

            AddFace(cellCenter + new Vector3(a.x, botY, a.y), cellCenter + new Vector3(a.x, topY, a.y),
                    cellCenter + new Vector3(b.x, topY, b.y), cellCenter + new Vector3(b.x, botY, b.y));
        }

        AddNGon(pts, topY, true);
        AddNGon(pts, botY, false);
    }
}
    #endregion
