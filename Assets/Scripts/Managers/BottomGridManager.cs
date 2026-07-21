using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Grid))]
public class BottomGridManager : MonoBehaviour
{
    public enum StartCorner
    {
        TopLeft,
        BottomLeft
    }

    [Header("Grid Layout")]
    [Tooltip("Number of rows (vertical size)")]
    [Min(1)] public int height = 29;

    [Tooltip("Number of columns (horizontal size)")]
    [Min(1)] public int width = 8;

    [Tooltip("Where should the first child (Child 0) be placed? Both options will always read Left-to-Right.")]
    public StartCorner startCorner = StartCorner.TopLeft;

    public GameObject squareTile, squareSlot,squareGarage;

    [Header("Scaling & Placement")]
    [Tooltip("If true, scales width to match the screen. If false, uses the Manual Grid Scale slider below.")]
    public bool autoFitToScreen = true;

    [Tooltip("Manually set the grid scale (Requires 'Auto Fit To Screen' to be false).")]
    [Range(0.1f, 5f)] public float manualGridScale = 1f;

    [Tooltip("If true, automatically moves the Grid's Transform to center it in the available screen area.")]
    public bool autoPositionToScreenCenter = true;

    public Camera mainCamera;

    [Tooltip("The Y-axis level where the grid sits (should match your floor height).")]
    public float floorHeight = 0f;

    [Tooltip("Percentage of empty space to leave on the Left/Right margins (0.0 to 0.5)")]
    [Range(0f, 0.5f)] public float screenPadding = 0.05f;

    [Header("Safe Area")]
    [Tooltip("Reserve the TOP percentage of the screen. The grid will center itself in the remaining space below this.")]
    [Range(0f, 0.8f)] public float topScreenReserved = 0.15f;

    private Grid grid;
    private Vector3 lastCellSize;
    private Vector3 lastCellGap;
    public static BottomGridManager Instance;

    private void OnEnable()
    {
        Instance = this;
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

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                
                var lc = hit.collider.GetComponent<LetterController>();
                var lCons = hit.collider.GetComponent<LetterConstriant>();
                if (lc != null && lCons == null)
                {
                   lc.StartMoving();
                }
            }
        }
    }

    private void UpdateGridCache()
    {
        if (grid != null)
        {
            lastCellSize = grid.cellSize;
            lastCellGap = grid.cellGap;
        }
    }

    public void CreateChildren()
    {
        int childCount = height * width;
        DestroyAllChildren();

        for (int i = 0; i < childCount; i++)
        {
            if (squareTile != null) Instantiate(squareTile, transform);
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

        // 1. Calculate the exact center of the grid in local space
        // Using Row 0 to Row -(height-1) and Col 0 to (width-1)
        Vector3 minCellPos = grid.CellToLocal(new Vector3Int(0, -(height - 1), 0));
        Vector3 maxCellPos = grid.CellToLocal(new Vector3Int(width - 1, 0, 0));
        Vector3 gridLocalCenter = (minCellPos + maxCellPos) / 2f;

        // 2. Position the Parent Transform and apply Scale
        if (mainCamera == null) mainCamera = Camera.main;

        if (mainCamera != null)
        {
            Plane floorPlane = new Plane(Vector3.up, new Vector3(0, floorHeight, 0));

            // Calculate available vertical space (leaving top reserved and bottom padding)
            float maxY = 1f - topScreenReserved;
            float minY = screenPadding;
            float centerY = (maxY + minY) / 2f;

            if (autoPositionToScreenCenter)
            {
                // Place the parent object precisely in the center of the available screen
                Vector3 screenCenterWorld = GetFloorIntersection(new Vector2(0.5f, centerY), floorPlane);
                transform.position = screenCenterWorld;
            }

            if (autoFitToScreen)
            {
                // Calculate frustum width specifically at the screen center
                Vector3 centerLeft = GetFloorIntersection(new Vector2(screenPadding, centerY), floorPlane);
                Vector3 centerRight = GetFloorIntersection(new Vector2(1f - screenPadding, centerY), floorPlane);
                float frustumWidth = Vector3.Distance(centerLeft, centerRight);

                float gridUnscaledWidth = (width * grid.cellSize.x) + ((width - 1) * grid.cellGap.x);

                if (gridUnscaledWidth > 0)
                {
                    float finalScale = frustumWidth / gridUnscaledWidth;
                    transform.localScale = new Vector3(finalScale, finalScale, finalScale);
                }
            }
            else
            {
                // Apply manual scale from the center
                transform.localScale = new Vector3(manualGridScale, manualGridScale, manualGridScale);
            }
        }

        int validChildCount = Mathf.Min(currentChildCount, height * width);

        // 3. Map children and offset them so the parent is always their absolute center
        for (int i = 0; i < validChildCount; i++)
        {
            int physical_col = i % width;
            int physical_row;

            if (startCorner == StartCorner.TopLeft)
            {
                physical_row = -(i / width);
            }
            else
            {
                physical_row = -(height - 1) + (i / width);
            }

            Vector3 baseLocalPos = grid.CellToLocal(new Vector3Int(physical_col, physical_row, 0));

            // Subtracting the center offset forces local (0,0,0) to be the exact middle of the grid.
            // Because the parent pivot is now in the center, transform.localScale expands uniformly outward.
            baseLocalPos -= gridLocalCenter;

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