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
    public StartCorner startCorner = StartCorner.TopLeft;

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

    [Header("Auto-Fit To Camera")]
    [Tooltip("Scale width automatically to match the screen bounds.")]
    public bool autoFitToScreen = true;

    [Tooltip("Manual scale multiplier for fine-tuning.")]
    [Range(0.1f, 5f)] public float manualGridScale = 1f;

    public Camera mainCamera;

    [Tooltip("The Y-axis level where the grid sits (should match your floor height).")]
    public float floorHeight = 0f;

    [Tooltip("Percentage of empty space to leave on the Left/Right edges (0.0 to 0.5)")]
    [Range(0f, 0.5f)] public float screenPadding = 0.05f;

    [Header("Safe Area Boundary")]
    [Tooltip("The upper boundary percentage for the bottom grid (Set to 0.35 to align directly below TopGridManager). Grid grows DOWNWARDS from this line.")]
    [Range(0.05f, 0.95f)] public float topBoundaryReserved = 0.35f;

    public static BottomGridManager Instance;

    private Grid grid;
    private Vector3 lastCellSize;
    private Vector3 lastCellGap;

    // Screen/Camera tracking fields for dynamic runtime & editor updates
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastCameraAspect;
    private float lastCameraFOV;
    private float lastCameraOrthoSize;

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

    private void CheckScreenAndCameraChanges()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

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

        if (hasChanged)
        {
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
        if (currentChildCount == 0 && centerObject == null) return;

        Vector3 safeAreaAnchorOffset = Vector3.zero;
        Vector3 horizontalCenterOffset = Vector3.zero;

        // 1. Determine horizontal center offset
        if (width > 0)
        {
            Vector3 rightmostCellPos = grid.CellToLocal(new Vector3Int(width - 1, 0, 0));
            horizontalCenterOffset = rightmostCellPos / 2f;
        }

        // 2. Auto-Fit Width & Align Top Edge to Upper Border
        if (mainCamera == null) mainCamera = Camera.main;

        if (mainCamera != null)
        {
            Plane floorPlane = new Plane(Vector3.up, new Vector3(0, floorHeight, 0));

            float targetY = topBoundaryReserved;
            float minX = screenPadding;
            float maxX = 1f - screenPadding;

            Vector3 topLeft = GetFloorIntersection(new Vector2(minX, targetY), floorPlane);
            Vector3 topRight = GetFloorIntersection(new Vector2(maxX, targetY), floorPlane);

            float frustumWidth = Vector3.Distance(topLeft, topRight);
            float gridUnscaledWidth = (width * grid.cellSize.x) + ((width - 1) * grid.cellGap.x);

            if (autoFitToScreen && gridUnscaledWidth > 0f)
            {
                float calculatedScale = (frustumWidth / gridUnscaledWidth) * manualGridScale;
                transform.localScale = new Vector3(calculatedScale, calculatedScale, calculatedScale);
            }
            else
            {
                transform.localScale = new Vector3(manualGridScale, manualGridScale, manualGridScale);
            }

            // Align top edge of grid to topCenterWorld
            Vector3 topCenterWorld = (topLeft + topRight) * 0.5f;
            Vector3 targetTopLocal = transform.InverseTransformPoint(topCenterWorld);

            Vector3 topRowCellPos = grid.CellToLocal(new Vector3Int(0, height - 1, 0));
            Vector3 cellUpDirection = grid.CellToLocal(new Vector3Int(0, 1, 0)).normalized;
            Vector3 rowTopEdgePos = topRowCellPos + (cellUpDirection * (grid.cellSize.y / 2f));

            safeAreaAnchorOffset = targetTopLocal - rowTopEdgePos;
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

                SpriteRenderer sr = centerObject.GetComponent<SpriteRenderer>();
                if (sr == null) sr = centerObject.GetComponentInChildren<SpriteRenderer>();

                MeshFilter mf = centerObject.GetComponent<MeshFilter>();
                if (mf == null) mf = centerObject.GetComponentInChildren<MeshFilter>();

                if (sr != null && sr.drawMode != SpriteDrawMode.Simple)
                {
                    sr.size = new Vector2(targetLocalSize.x, targetLocalSize.y);
                    if (borderPadding.z != 0f)
                    {
                        Vector3 currentScale = centerObject.transform.localScale;
                        centerObject.transform.localScale = new Vector3(currentScale.x, currentScale.y, targetLocalSize.z);
                    }
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
}