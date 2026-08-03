using UnityEngine;

[RequireComponent(typeof(TopGridManager))]
[DefaultExecutionOrder(100)] // Ensures this runs AFTER TopGridManager's LateUpdate finishes positioning tiles
public class AdaptiveGridBackgroundPlacer : MonoBehaviour
{
    [Header("Core References")]
    public Camera targetCamera;
    public TopGridManager gridManager;
    public Grid grid;

    [Header("Top Background (Object 1)")]
    public Transform topBackground;
    [Tooltip("The Top Background will cover everything from the top of the screen down to the TOP of this row index.")]
    [Min(0)] public int topSnapRowIndex = 3;

    [Tooltip("Offsets the snap edge vertically along the grid.")]
    public float topBackgroundYOffset = 0f;

    [Tooltip("Final position adjustment (Relative to the Grid's Normal). Use Z or Y to push it behind the grid.")]
    public Vector3 positionOffset1 = Vector3.zero;

    [Space(5)]
    public bool autoScaleX1 = true;
    public bool autoScaleY1 = true;
    [Tooltip("How much of the screen width (in percentage) the object should cover.")]
    public float scaleWidthPercent1 = 100f;

    [Header("Bottom Background (Object 2)")]
    public Transform bottomBackground;
    [Tooltip("The Bottom Background will cover everything from the bottom of the screen up to the BOTTOM of this row index.")]
    [Min(0)] public int bottomSnapRowIndex = 0;

    [Tooltip("Offsets the snap edge vertically along the grid.")]
    public float bottomBackgroundYOffset = 0f;

    [Tooltip("Final position adjustment (Relative to the Grid's Normal). Use Z or Y to push it behind the grid.")]
    public Vector3 positionOffset2 = Vector3.zero;

    [Space(5)]
    public bool autoScaleX2 = true;
    public bool autoScaleY2 = true;
    [Tooltip("How much of the screen width (in percentage) the object should cover.")]
    public float scaleWidthPercent2 = 100f;

    [Header("Scaling Behavior")]
    [Tooltip("Check this if your background is a 2D Sprite/UI Quad (uses local Y for height).")]
    public bool scaleLocalY = true;
    [Tooltip("Check this if your background is a standard Unity 3D Plane lying flat (uses local Z for height).")]
    public bool scaleLocalZ = false;

    private void OnEnable()
    {
        if (gridManager == null) gridManager = GetComponent<TopGridManager>();
        if (grid == null) grid = GetComponent<Grid>();
        if (targetCamera == null) targetCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (targetCamera == null || gridManager == null || grid == null) return;

        // 1. Recreate the exact floor plane used by TopGridManager
        Plane floorPlane = new Plane(Vector3.up, new Vector3(0, gridManager.floorHeight, 0));

        // 2. Find the absolute World Boundaries of the screen on that plane
        Vector3 screenTop = GetFloorIntersection(new Vector2(0.5f, 1f), floorPlane);
        Vector3 screenBottom = GetFloorIntersection(new Vector2(0.5f, 0f), floorPlane);
        Vector3 screenLeft = GetFloorIntersection(new Vector2(0f, 0.5f), floorPlane);
        Vector3 screenRight = GetFloorIntersection(new Vector2(1f, 0.5f), floorPlane);

        float screenWidth = Vector3.Distance(screenLeft, screenRight);

        // 3. Position and stretch the Top Background
        if (topBackground != null)
        {
            GetRowWorldBounds(topSnapRowIndex, out Vector3 rowTopEdge, out _);

            // Apply snap offset along the grid's up direction
            Vector3 gridUp = gridManager.transform.TransformDirection(grid.CellToLocal(new Vector3Int(0, 1, 0)).normalized);
            rowTopEdge += gridUp * topBackgroundYOffset;

            PlaceAndScaleBackground(topBackground, screenTop, rowTopEdge, screenWidth, autoScaleX1, autoScaleY1, scaleWidthPercent1, positionOffset1);
        }

        // 4. Position and stretch the Bottom Background
        if (bottomBackground != null)
        {
            GetRowWorldBounds(bottomSnapRowIndex, out _, out Vector3 rowBottomEdge);

            // Apply snap offset
            Vector3 gridUp = gridManager.transform.TransformDirection(grid.CellToLocal(new Vector3Int(0, 1, 0)).normalized);
            rowBottomEdge += gridUp * bottomBackgroundYOffset;

            PlaceAndScaleBackground(bottomBackground, rowBottomEdge, screenBottom, screenWidth, autoScaleX2, autoScaleY2, scaleWidthPercent2, positionOffset2);
        }
    }

    /// <summary>
    /// Stretches the object perfectly between a top and bottom world point, applying specific scaling and position rules.
    /// </summary>
    private void PlaceAndScaleBackground(Transform bg, Vector3 topEdge, Vector3 bottomEdge, float targetWidth, bool scaleX, bool scaleY, float widthPercent, Vector3 finalOffset)
    {
        // Calculate base center position
        Vector3 basePosition = (topEdge + bottomEdge) / 2f;

        // Apply the final offset relative to the GRID'S orientation, ensuring it moves exactly along normal axes
        bg.position = basePosition + gridManager.transform.TransformDirection(finalOffset);

        Renderer rend = bg.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Vector3 currentScale = bg.localScale;

            // Auto Width
            if (scaleX)
            {
                float currentBoundsWidth = rend.bounds.size.x;
                if (currentBoundsWidth > 0.001f)
                {
                    float finalTargetWidth = targetWidth * (widthPercent / 100f);
                    currentScale.x *= (finalTargetWidth / currentBoundsWidth);
                }
            }

            // Auto Height
            if (scaleY)
            {
                float targetHeight = Vector3.Distance(topEdge, bottomEdge);
                float currentBoundsHeight = scaleLocalZ ? rend.bounds.size.z : rend.bounds.size.y;

                if (currentBoundsHeight > 0.001f)
                {
                    float heightMultiplier = targetHeight / currentBoundsHeight;
                    if (scaleLocalY) currentScale.y *= heightMultiplier;
                    if (scaleLocalZ) currentScale.z *= heightMultiplier;
                }
            }

            bg.localScale = currentScale;
        }
    }

    /// <summary>
    /// Calculates the exact world top and bottom limits of a specific physical row in the grid.
    /// </summary>
    private void GetRowWorldBounds(int rowIndex, out Vector3 topEdge, out Vector3 bottomEdge)
    {
        Vector3 worldPos = GetRowCenterWorldPosition(rowIndex);

        // Calculate the physical height of one cell after TopGridManager has scaled it
        float cellWorldHeight = grid.cellSize.y * gridManager.transform.localScale.y;
        Vector3 gridUp = gridManager.transform.TransformDirection(grid.CellToLocal(new Vector3Int(0, 1, 0)).normalized);

        topEdge = worldPos + (gridUp * (cellWorldHeight / 2f));
        bottomEdge = worldPos - (gridUp * (cellWorldHeight / 2f));
    }

    /// <summary>
    /// Looks at the actual instantiated child objects to find exactly where TopGridManager put the center of the row.
    /// </summary>
    private Vector3 GetRowCenterWorldPosition(int rowIndex)
    {
        if (gridManager.transform.childCount == 0 || gridManager.columns == 0) return gridManager.transform.position;

        rowIndex = Mathf.Clamp(rowIndex, 0, gridManager.rows - 1);
        int startChildIndex = 0;

        // Account for TopGridManager's build direction
        if (gridManager.startCorner == TopGridManager.StartCorner.BottomLeft)
        {
            startChildIndex = rowIndex * gridManager.columns;
        }
        else
        {
            startChildIndex = ((gridManager.rows - 1) - rowIndex) * gridManager.columns;
        }

        // Get the start (left) and end (right) indices of this specific row
        int endChildIndex = startChildIndex + (gridManager.columns - 1);

        startChildIndex = Mathf.Clamp(startChildIndex, 0, gridManager.transform.childCount - 1);
        endChildIndex = Mathf.Clamp(endChildIndex, 0, gridManager.transform.childCount - 1);

        // Get world positions of the first and last tile in the row
        Vector3 rowStartPos = gridManager.transform.GetChild(startChildIndex).position;
        Vector3 rowEndPos = gridManager.transform.GetChild(endChildIndex).position;

        // Return the exact horizontal center point of the row
        return (rowStartPos + rowEndPos) / 2f;
    }

    /// <summary>
    /// Matches TopGridManager's raycast logic exactly to prevent misalignment.
    /// </summary>
    private Vector3 GetFloorIntersection(Vector2 viewportPos, Plane floorPlane)
    {
        Ray ray = targetCamera.ViewportPointToRay(viewportPos);
        if (floorPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return ray.GetPoint(targetCamera.farClipPlane);
    }
}