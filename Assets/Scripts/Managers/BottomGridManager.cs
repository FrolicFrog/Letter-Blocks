using System.Collections.Generic;
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
        // Only run the checks if the game is actually playing
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
        // Strictly just to ensure the reference exists if working in the inspector
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
                        // Account for the border extending below the bottom cell
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
            Vector3 posMin = grid.CellToLocal(new Vector3Int(0, 0, 0));
            Vector3 posMax = grid.CellToLocal(new Vector3Int(width - 1, height - 1, 0));

            Vector3 gridLocalCenter = (posMin + posMax) / 2f;
            gridLocalCenter -= horizontalCenterOffset;
            gridLocalCenter += safeAreaAnchorOffset;

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
                Vector3 diff = posMax - posMin;

                float cellZSize = grid.cellSize.z > 0 ? grid.cellSize.z : 0f;

                float totalOuterWidth = Mathf.Abs(diff.x) + grid.cellSize.x + borderPadding.x;
                float totalOuterHeight = Mathf.Abs(diff.y) + grid.cellSize.y + borderPadding.y;
                float totalOuterDepth = Mathf.Abs(diff.z) + cellZSize + borderPadding.z;

                bool isChild = centerObject.transform.parent == transform;
                Vector3 gridScale = transform.localScale;

                Vector3 targetLocalSize = new Vector3(
                    totalOuterWidth,
                    totalOuterHeight,
                    totalOuterDepth > 0f ? totalOuterDepth : centerObject.transform.localScale.z
                );

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

                // STRICTLY ONLY checking the centerObject itself, ignoring children
                SpriteRenderer sr = centerObject.GetComponent<SpriteRenderer>();
                MeshFilter mf = centerObject.GetComponent<MeshFilter>();

                if (sr != null && sr.drawMode != SpriteDrawMode.Simple)
                {
                    // Reset the scale to 1 on X and Y, maintaining Z
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
                    // Fallback to directly setting local scale if no relevant visual component is found
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
}