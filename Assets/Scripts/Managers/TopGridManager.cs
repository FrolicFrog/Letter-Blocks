using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Grid))]
public class TopGridManager : MonoBehaviour
{
    public enum StartCorner
    {
        BottomLeft,
        TopLeft
    }

    [Header("Grid Layout")]
    [Min(1)] public int rows = 29;
    [Min(1)] public int columns = 8;
    public int height, width;

    [Tooltip("Where should the first child (Child 0) be placed? Both options will always read Left-to-Right.")]
    public StartCorner startCorner = StartCorner.BottomLeft;

    public GameObject squareTile, squareSlot, emptyTile;
    public Transform queueParent;
    public List<Transform> queueSlots;

    [Header("Auto-Fit To Camera")]
    [Tooltip("Scale width to match the screen and anchor the bottom to the Safe Area.")]
    public bool autoFitToScreen = true;
    public Camera mainCamera;

    [Tooltip("The Y-axis level where the grid sits (should match your floor height).")]
    public float floorHeight = 0f;

    [Tooltip("Percentage of empty space to leave on the Left/Right edges (0.0 to 0.5)")]
    [Range(0f, 0.5f)] public float screenPadding = 0.05f;

    [Header("Safe Area")]
    [Tooltip("Reserve the bottom percentage of the screen. The grid will start exactly above this line and grow UPWARDS.")]
    [Range(0f, 0.8f)] public float bottomScreenReserved = 0.35f;

    public static TopGridManager instance;

    private Grid grid;

    // Camera Tracking
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastCameraAspect;
    private float lastCameraFOV;
    private float lastCameraOrthoSize;
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;

    // Parameter Tracking for Update Loop
    private int lastRows;
    private int lastColumns;
    private int lastChildCount;
    private float lastScreenPadding;
    private float lastBottomScreenReserved;
    private float lastFloorHeight;
    private Vector3 lastCellSize;
    private Vector3 lastCellGap;

    private void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        instance = this;
        grid = GetComponent<Grid>();

        CacheInitialValues();
        ArrangeChildren();
    }

    private void LateUpdate()
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

        lastRows = rows;
        lastColumns = columns;
        lastChildCount = transform.childCount;
        lastScreenPadding = screenPadding;
        lastBottomScreenReserved = bottomScreenReserved;
        lastFloorHeight = floorHeight;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastCameraAspect = mainCamera.aspect;
            lastCameraOrthoSize = mainCamera.orthographicSize;
            lastCameraFOV = mainCamera.fieldOfView;
            lastCameraPosition = mainCamera.transform.position;
            lastCameraRotation = mainCamera.transform.rotation;
        }
    }

    private bool CheckParameterChanges()
    {
        bool changed = false;

        // Auto-detect when new children are instantiated or destroyed
        if (transform.childCount != lastChildCount) { lastChildCount = transform.childCount; changed = true; }

        if (rows != lastRows) { lastRows = rows; changed = true; }
        if (columns != lastColumns) { lastColumns = columns; changed = true; }
        if (screenPadding != lastScreenPadding) { lastScreenPadding = screenPadding; changed = true; }
        if (bottomScreenReserved != lastBottomScreenReserved) { lastBottomScreenReserved = bottomScreenReserved; changed = true; }
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

        if (mainCamera.transform.position != lastCameraPosition)
        {
            lastCameraPosition = mainCamera.transform.position;
            hasChanged = true;
        }

        if (mainCamera.transform.rotation != lastCameraRotation)
        {
            lastCameraRotation = mainCamera.transform.rotation;
            hasChanged = true;
        }

        return hasChanged;
    }

    [ContextMenu("Generate Grid Tiles")]
    public void CreateChildren()
    {
        if (grid == null) grid = GetComponent<Grid>();

        int childCount = rows * columns;
        DestroyAllChildren();

        Vector3 targetScale = Vector3.one;
        if (grid != null)
        {
            targetScale = grid.cellSize;
        }

        for (int i = 0; i < childCount; i++)
        {
            if (squareTile != null)
            {
                GameObject newTile = Instantiate(squareTile, transform);
                newTile.transform.localScale = targetScale;
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
        if (currentChildCount == 0) return;

        int actualTileCount = currentChildCount;

        // Auto-expand rows if there are more tiles than the current grid allows.
        if (columns > 0)
        {
            int requiredRows = Mathf.CeilToInt((float)actualTileCount / columns);
            if (requiredRows > rows)
            {
                rows = requiredRows;
                lastRows = rows; // Prevent triggering an unnecessary update loop
            }
        }

        Vector3 safeAreaAnchorOffset = Vector3.zero;
        Vector3 horizontalCenterOffset = Vector3.zero;

        // 1. Determine horizontal center offset
        if (columns > 0)
        {
            Vector3 rightmostCellPos = grid.CellToLocal(new Vector3Int(columns - 1, 0, 0));
            horizontalCenterOffset = rightmostCellPos / 2f;
        }

        // 2. Auto-Fit to Width & Calculate Bottom Anchor
        if (autoFitToScreen)
        {
            if (mainCamera == null) mainCamera = Camera.main;

            if (mainCamera != null)
            {
                Plane floorPlane = new Plane(Vector3.up, new Vector3(0, floorHeight, 0));

                float minY = bottomScreenReserved + screenPadding;
                float minX = screenPadding;
                float maxX = 1f - screenPadding;

                Vector3 bottomLeft = GetFloorIntersection(new Vector2(minX, minY), floorPlane);
                Vector3 bottomRight = GetFloorIntersection(new Vector2(maxX, minY), floorPlane);

                float frustumWidth = Vector3.Distance(bottomLeft, bottomRight);
                float gridUnscaledWidth = (columns * grid.cellSize.x) + ((columns - 1) * grid.cellGap.x);

                if (gridUnscaledWidth > 0)
                {
                    // Strictly scale based on the screen width
                    float finalScale = frustumWidth / gridUnscaledWidth;
                    transform.localScale = new Vector3(finalScale, finalScale, finalScale);
                }

                Vector3 safeBottomCenterWorld = (bottomLeft + bottomRight) / 2f;
                Vector3 targetBottomLocal = transform.InverseTransformPoint(safeBottomCenterWorld);

                Vector3 cellUpDirection = grid.CellToLocal(new Vector3Int(0, 1, 0)).normalized;
                Vector3 row0BottomEdgePos = -cellUpDirection * (grid.cellSize.y / 2f);

                safeAreaAnchorOffset = targetBottomLocal - row0BottomEdgePos;
            }
        }

        // 3. Map children Left-to-Right perfectly
        int validChildCount = Mathf.Max(actualTileCount, rows * columns);
        int tileIndex = 0;

        for (int i = 0; i < currentChildCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (tileIndex >= validChildCount) break;

            int physical_col = tileIndex % columns;

            int physical_row;
            if (startCorner == StartCorner.TopLeft)
            {
                physical_row = (rows - 1) - (tileIndex / columns);
            }
            else
            {
                physical_row = tileIndex / columns;
            }

            Vector3 baseLocalPos = grid.CellToLocal(new Vector3Int(physical_col, physical_row, 0));

            baseLocalPos -= horizontalCenterOffset;
            baseLocalPos += safeAreaAnchorOffset;

            child.localPosition = baseLocalPos;
            tileIndex++;
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