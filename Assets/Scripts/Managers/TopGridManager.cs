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

    [Header("Center Border Settings - Object 1")]
    [Tooltip("Assign a GameObject here to automatically place and scale it around the grid.")]
    public GameObject centerObject1;

    [Tooltip("If true, automatically resizes centerObject1 to fit the outer bounds of the grid.")]
    public bool autoScaleBorder1 = true;

    [Tooltip("Extra padding around the outer edge of the grid for centerObject1's border (X, Y, Z).")]
    public Vector3 borderPadding1 = Vector3.zero;

    [Tooltip("Extra positional offset for centerObject1 applied along the grid's local X, Y, and Z axes.")]
    public Vector3 centerObject1Offset = Vector3.zero;

    [Header("Center Border Settings - Object 2")]
    [Tooltip("Assign a second GameObject here to automatically place and scale it around the grid.")]
    public GameObject centerObject2;

    [Tooltip("If true, automatically resizes centerObject2 to fit the outer bounds of the grid.")]
    public bool autoScaleBorder2 = true;

    [Tooltip("Extra padding around the outer edge of the grid for centerObject2's border (X, Y, Z).")]
    public Vector3 borderPadding2 = Vector3.zero;

    [Tooltip("Extra positional offset for centerObject2 applied along the grid's local X, Y, and Z axes.")]
    public Vector3 centerObject2Offset = Vector3.zero;

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
    private Vector3 lastCameraPosition;      // NEW: tracks camera movement
    private Quaternion lastCameraRotation;   // NEW: tracks camera rotation

    // Parameter Tracking for Update Loop
    private int lastRows;
    private int lastColumns;
    private Vector3 lastBorderPadding1;
    private Vector3 lastCenterObject1Offset;
    private Vector3 lastBorderPadding2;
    private Vector3 lastCenterObject2Offset;
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

    // Changed from Update -> LateUpdate.
    // This guarantees we arrange AFTER any camera-follow/camera-rig scripts have
    // finished moving the camera for this frame. Previously, if a camera controller
    // moved/positioned the camera in its own Awake/Start/Update *after* this script's
    // OnEnable ran (order is not guaranteed across sessions/builds), the grid would be
    // anchored to a stale camera position and never get corrected, since camera
    // position/rotation weren't part of the change-detection. That's what caused the
    // border/tile positions to look "different" or "inconsistent" between play sessions.
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
        lastBorderPadding1 = borderPadding1;
        lastCenterObject1Offset = centerObject1Offset;
        lastBorderPadding2 = borderPadding2;
        lastCenterObject2Offset = centerObject2Offset;
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
            lastCameraPosition = mainCamera.transform.position;   // NEW
            lastCameraRotation = mainCamera.transform.rotation;   // NEW
        }
    }

    private bool CheckParameterChanges()
    {
        bool changed = false;

        if (rows != lastRows) { lastRows = rows; changed = true; }
        if (columns != lastColumns) { lastColumns = columns; changed = true; }
        if (borderPadding1 != lastBorderPadding1) { lastBorderPadding1 = borderPadding1; changed = true; }
        if (centerObject1Offset != lastCenterObject1Offset) { lastCenterObject1Offset = centerObject1Offset; changed = true; }
        if (borderPadding2 != lastBorderPadding2) { lastBorderPadding2 = borderPadding2; changed = true; }
        if (centerObject2Offset != lastCenterObject2Offset) { lastCenterObject2Offset = centerObject2Offset; changed = true; }
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

        // NEW: camera position/rotation must be tracked too, since ArrangeChildren()
        // raycasts from the camera's viewport corners onto the floor plane. Without
        // this check, any camera movement/positioning that happens after our own
        // OnEnable (e.g. a camera rig settling into place, orientation changes,
        // safe-area adjustments) would silently be ignored and the grid would stay
        // anchored to the camera's earlier position for the rest of the session.
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
        if (currentChildCount == 0 && centerObject1 == null && centerObject2 == null) return;

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

        int validChildCount = Mathf.Min(currentChildCount, rows * columns);

        // 3. Map children Left-to-Right perfectly
        int tileIndex = 0;

        for (int i = 0; i < currentChildCount; i++)
        {
            Transform child = transform.GetChild(i);

            if ((centerObject1 != null && child == centerObject1.transform) ||
                (centerObject2 != null && child == centerObject2.transform))
                continue;

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

        // 4. Position & Auto-Size the Center Border Objects
        if (centerObject1 != null || centerObject2 != null)
        {
            Vector3 posMin = grid.CellToLocal(new Vector3Int(0, 0, 0));
            Vector3 posMax = grid.CellToLocal(new Vector3Int(columns - 1, rows - 1, 0));

            Vector3 gridLocalCenterBase = (posMin + posMax) / 2f;
            gridLocalCenterBase -= horizontalCenterOffset;
            gridLocalCenterBase += safeAreaAnchorOffset;

            Vector3 diff = posMax - posMin;
            Vector3 gridScale = transform.localScale;

            PositionAndScaleCenterObject(centerObject1, autoScaleBorder1, borderPadding1, centerObject1Offset, gridLocalCenterBase, diff, gridScale);
            PositionAndScaleCenterObject(centerObject2, autoScaleBorder2, borderPadding2, centerObject2Offset, gridLocalCenterBase, diff, gridScale);
        }
    }

    private void PositionAndScaleCenterObject(GameObject centerObject, bool autoScaleBorder, Vector3 borderPadding, Vector3 positionOffset, Vector3 gridLocalCenterBase, Vector3 diff, Vector3 gridScale)
    {
        if (centerObject == null) return;

        Vector3 gridLocalCenter = gridLocalCenterBase + positionOffset;
        bool isChild = centerObject.transform.parent == transform;

        // Position Logic
        if (isChild)
        {
            centerObject.transform.localPosition = gridLocalCenter;
        }
        else
        {
            centerObject.transform.position = transform.TransformPoint(gridLocalCenter);
        }

        // Scale Logic
        if (autoScaleBorder)
        {
            float cellZSize = grid.cellSize.z > 0 ? grid.cellSize.z : 0f;

            float totalOuterWidth = Mathf.Abs(diff.x) + grid.cellSize.x + borderPadding.x;
            float totalOuterHeight = Mathf.Abs(diff.y) + grid.cellSize.y + borderPadding.y;
            float totalOuterDepth = Mathf.Abs(diff.z) + cellZSize + borderPadding.z;

            float targetX = totalOuterWidth;
            float targetY = totalOuterHeight;
            float targetZ = totalOuterDepth > 0f ? totalOuterDepth : 0f;

            if (!isChild)
            {
                // Use lossyScale to accurately get the true world scale of the grid and parent
                Vector3 parentScale = centerObject.transform.parent != null ? centerObject.transform.parent.lossyScale : Vector3.one;
                Vector3 gridLossyScale = transform.lossyScale;

                targetX = (targetX * gridLossyScale.x) / (parentScale.x != 0 ? parentScale.x : 1f);
                targetY = (targetY * gridLossyScale.y) / (parentScale.y != 0 ? parentScale.y : 1f);

                if (targetZ > 0f)
                {
                    targetZ = (targetZ * gridLossyScale.z) / (parentScale.z != 0 ? parentScale.z : 1f);
                }
            }

            Vector3 currentLocalScale = centerObject.transform.localScale;

            // Create target size. If targetZ is 0, we strictly preserve the exact current Z scale to prevent update loops.
            Vector3 targetLocalSize = new Vector3(
                targetX,
                targetY,
                targetZ > 0f ? targetZ : currentLocalScale.z
            );

            SpriteRenderer sr = centerObject.GetComponent<SpriteRenderer>();
            if (sr == null) sr = centerObject.GetComponentInChildren<SpriteRenderer>();

            MeshFilter mf = centerObject.GetComponent<MeshFilter>();
            if (mf == null) mf = centerObject.GetComponentInChildren<MeshFilter>();

            if (sr != null && sr.drawMode != SpriteDrawMode.Simple)
            {
                // Sliced/Tiled sprites size must be controlled purely via sr.size. 
                // We force localScale X/Y to 1 to prevent double-scaling distortion.
                sr.size = new Vector2(targetLocalSize.x, targetLocalSize.y);
                centerObject.transform.localScale = new Vector3(1f, 1f, targetLocalSize.z);
            }
            else if (sr != null && sr.sprite != null)
            {
                // Simple Sprites
                Vector2 spriteSize = sr.sprite.rect.size / sr.sprite.pixelsPerUnit;
                if (spriteSize.x > 0 && spriteSize.y > 0)
                {
                    centerObject.transform.localScale = new Vector3(
                        targetLocalSize.x / spriteSize.x,
                        targetLocalSize.y / spriteSize.y,
                        targetLocalSize.z
                    );
                }
            }
            else if (mf != null && mf.sharedMesh != null)
            {
                // 3D Meshes
                Vector3 meshSize = mf.sharedMesh.bounds.size;
                float scaleX = meshSize.x > 0f ? targetLocalSize.x / meshSize.x : targetLocalSize.x;
                float scaleY = meshSize.y > 0f ? targetLocalSize.y / meshSize.y : targetLocalSize.y;
                float scaleZ = (meshSize.z > 0f && targetZ > 0f) ? targetLocalSize.z / meshSize.z : targetLocalSize.z;

                centerObject.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
            }
            else
            {
                // Raw GameObjects / Fallback
                centerObject.transform.localScale = targetLocalSize;
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