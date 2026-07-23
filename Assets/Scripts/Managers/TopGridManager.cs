using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
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
    private Vector3 lastCellSize;
    private Vector3 lastCellGap;

    private void Awake()
    {
        instance = this;
      
    }
    private void OnEnable()
    {
        grid = GetComponent<Grid>();
        UpdateGridCache();
        ArrangeChildren();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update += EditorUpdate;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorUpdate;
#endif
    }

    private void OnValidate()
    {
        if (grid == null) grid = GetComponent<Grid>();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) ArrangeChildren();
        };
#endif
    }
#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (Application.isPlaying) return;

        if (grid != null)
        {
            if (grid.cellGap != lastCellGap || grid.cellSize != lastCellSize)
            {
                UpdateGridCache();
                ArrangeChildren();
                UnityEditor.SceneView.RepaintAll();
            }
        }
    }
#endif

    private void UpdateGridCache()
    {
        if (grid != null)
        {
            lastCellSize = grid.cellSize;
            lastCellGap = grid.cellGap;
        }
    }

    [ContextMenu("Generate Grid Tiles")]
    public void CreateChildren()
    {
        if (grid == null) grid = GetComponent<Grid>();

        int childCount = rows * columns;
        DestroyAllChildren();

        // Fallback to absolute local scale 1 if grid settings aren't set up yet
        Vector3 targetScale = Vector3.one;
        if (grid != null)
        {
            // Sets the tile's base size to match the Grid component's Cell Size dimensions
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

        int validChildCount = Mathf.Min(currentChildCount, rows * columns);

        // 3. Map children Left-to-Right perfectly
        for (int i = 0; i < validChildCount; i++)
        {
            // ALWAYS flows left to right
            int physical_col = i % columns;

            int physical_row;
            if (startCorner == StartCorner.TopLeft)
            {
                // Child 0 starts at the very top row and flows downwards
                physical_row = (rows - 1) - (i / columns);
            }
            else
            {
                // Child 0 starts at the Safe Area line and flows upwards
                physical_row = i / columns;
            }

            Vector3 baseLocalPos = grid.CellToLocal(new Vector3Int(physical_col, physical_row, 0));

            baseLocalPos -= horizontalCenterOffset;
            baseLocalPos += safeAreaAnchorOffset;

            transform.GetChild(i).localPosition = baseLocalPos;
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