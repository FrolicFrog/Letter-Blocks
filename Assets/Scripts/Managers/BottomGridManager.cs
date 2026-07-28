using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
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
    public StartCorner startCorner = StartCorner.BottomLeft;

    [Header("Slot & Tray References")]
    public GameObject emptySlot;
    public GameObject cell1, cell2, outline, letter;
    public List<WallDirectionPair> wallsDirection;

    [Header("Center Border Settings")]
    [Tooltip("Assign a GameObject here to automatically place and scale it around the grid.")]
    public GameObject centerObject;

    [Tooltip("If true, automatically resizes the border object to fit the outer bounds of the grid.")]
    public bool autoScaleBorder = true;

    [Tooltip("Extra padding around the outer edge of the grid for the border (X, Y, Z).")]
    public Vector3 borderPadding = Vector3.zero;

    [Header("Auto-Fit & Scaling")]
    [Tooltip("Scale width automatically to match the screen bounds.")]
    public bool autoFitToScreen = true;

    [Tooltip("Manual scale value. When Auto-Fit is OFF, this sets the exact scale. When Auto-Fit is ON, this acts as a fine-tuning multiplier.")]
    [Range(0.1f, 5f)] public float manualGridScale = 1f;

    public Camera mainCamera;

    [Tooltip("The Y-axis level where the grid sits (should match your floor height).")]
    public float floorHeight = 0f;

    [Tooltip("Percentage of empty space to leave on the Left/Right edges (0.0 to 0.5)")]
    [Range(0f, 0.5f)] public float screenPadding = 0.05f;

    [Header("Safe Area")]
    [Tooltip("Reserve the bottom percentage of the screen. The grid will start at this line.")]
    [Range(-0.8f, 0.8f)] public float bottomScreenReserved = 0.05f;

    public static BottomGridManager Instance;

    private Grid grid;
    private Vector3 lastCellSize;
    private Vector3 lastCellGap;

    // Screen/Camera tracking fields for real-time Simulator resizing
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastCameraAspect;

    private void Awake()
    {
        Instance = this;
    }

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

    private void Update()
    {
        CheckScreenAndCameraChanges();
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

    /// <summary>
    /// Detects changes in screen aspect ratio or resolution and immediately updates grid arrangement.
    /// </summary>
    private void CheckScreenAndCameraChanges()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight || !Mathf.Approximately(mainCamera.aspect, lastCameraAspect))
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastCameraAspect = mainCamera.aspect;
            ArrangeChildren();
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
        if (currentChildCount == 0) return;

        Vector3 safeAreaAnchorOffset = Vector3.zero;
        Vector3 horizontalCenterOffset = Vector3.zero;

        // 1. Determine horizontal center offset
        if (width > 0)
        {
            Vector3 rightmostCellPos = grid.CellToLocal(new Vector3Int(width - 1, 0, 0));
            horizontalCenterOffset = rightmostCellPos / 2f;
        }

        // 2. Calculate Scale & Bottom Safe Area Anchor Offset
        if (mainCamera == null) mainCamera = Camera.main;

        if (mainCamera != null)
        {
            Plane floorPlane = new Plane(Vector3.up, new Vector3(0, floorHeight, 0));

            float minY = bottomScreenReserved + screenPadding;
            float minX = screenPadding;
            float maxX = 1f - screenPadding;

            Vector3 bottomLeft = GetFloorIntersection(new Vector2(minX, minY), floorPlane);
            Vector3 bottomRight = GetFloorIntersection(new Vector2(maxX, minY), floorPlane);

            // Set Grid Scale
            if (autoFitToScreen)
            {
                float frustumWidth = Vector3.Distance(bottomLeft, bottomRight);
                float gridUnscaledWidth = (width * grid.cellSize.x) + ((width - 1) * grid.cellGap.x);

                if (gridUnscaledWidth > 0)
                {
                    // Scale auto-fits screen width, multiplied by manual scale for fine-tuning
                    float calculatedScale = (frustumWidth / gridUnscaledWidth) * manualGridScale;
                    transform.localScale = new Vector3(calculatedScale, calculatedScale, calculatedScale);
                }
            }
            else
            {
                // Strict manual scale maintained across all aspect ratios
                transform.localScale = new Vector3(manualGridScale, manualGridScale, manualGridScale);
            }

            // Always calculate safe area anchor offset so placement stays relative across aspect ratios
            Vector3 safeBottomCenterWorld = (bottomLeft + bottomRight) / 2f;
            Vector3 targetBottomLocal = transform.InverseTransformPoint(safeBottomCenterWorld);

            Vector3 cellUpDirection = grid.CellToLocal(new Vector3Int(0, 1, 0)).normalized;
            Vector3 row0BottomEdgePos = -cellUpDirection * (grid.cellSize.y / 2f);

            safeAreaAnchorOffset = targetBottomLocal - row0BottomEdgePos;
        }

        int validChildCount = Mathf.Min(currentChildCount, height * width);

        // 3. Map children Left-to-Right perfectly
        for (int i = 0; i < validChildCount; i++)
        {
            int physical_col = i % width;

            int physical_row;
            if (startCorner == StartCorner.TopLeft)
            {
                // Child 0 starts at the very top row and flows downwards
                physical_row = (height - 1) - (i / width);
            }
            else
            {
                // Child 0 starts at the Safe Area line and flows upwards
                physical_row = i / width;
            }

            Vector3 baseLocalPos = grid.CellToLocal(new Vector3Int(physical_col, physical_row, 0));

            baseLocalPos -= horizontalCenterOffset;
            baseLocalPos += safeAreaAnchorOffset;

            transform.GetChild(i).localPosition = baseLocalPos;
        }

        // 4. Position & Auto-Size Center Border Object around outer edges
        if (centerObject != null)
        {
            Vector3 posMin = grid.CellToLocal(new Vector3Int(0, 0, 0));
            Vector3 posMax = grid.CellToLocal(new Vector3Int(width - 1, height - 1, 0));

            Vector3 gridLocalCenter = (posMin + posMax) / 2f;
            gridLocalCenter -= horizontalCenterOffset;
            gridLocalCenter += safeAreaAnchorOffset;

            // Position centerObject
            if (centerObject.transform.parent == transform)
            {
                centerObject.transform.localPosition = gridLocalCenter;
            }
            else
            {
                centerObject.transform.position = transform.TransformPoint(gridLocalCenter);
            }

            // Auto-Scale border object to fit outer grid bounds
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

                // If centerObject is NOT a child of the grid, adjust target size so it scales properly in world space
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
                if (sr == null) sr = centerObject.GetComponentInChildren<SpriteRenderer>();

                MeshFilter mf = centerObject.GetComponent<MeshFilter>();
                if (mf == null) mf = centerObject.GetComponentInChildren<MeshFilter>();

                if (sr != null && sr.drawMode != SpriteDrawMode.Simple)
                {
                    // Sliced or Tiled Sprite Border
                    sr.size = new Vector2(targetLocalSize.x, targetLocalSize.y);
                    if (borderPadding.z != 0f)
                    {
                        Vector3 currentScale = centerObject.transform.localScale;
                        centerObject.transform.localScale = new Vector3(currentScale.x, currentScale.y, targetLocalSize.z);
                    }
                }
                else if (sr != null && sr.sprite != null)
                {
                    // Simple Sprite
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
                    // 3D Mesh Object (e.g. BG_Wall)
                    Vector3 meshSize = mf.sharedMesh.bounds.size;
                    float scaleX = meshSize.x > 0f ? targetLocalSize.x / meshSize.x : targetLocalSize.x;
                    float scaleY = meshSize.y > 0f ? targetLocalSize.y / meshSize.y : targetLocalSize.y;
                    float scaleZ = (meshSize.z > 0f && totalOuterDepth > 0f) ? targetLocalSize.z / meshSize.z : centerObject.transform.localScale.z;

                    centerObject.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
                }
                else
                {
                    // Generic Transform Scaling
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