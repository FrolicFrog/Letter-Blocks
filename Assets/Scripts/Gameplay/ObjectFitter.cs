using UnityEngine;

public class CenterObjectsManager : MonoBehaviour
{
    public enum FitMode
    {
        MatchGridTiles,
        MatchCameraFrustum
    }

    [Header("Core Settings")]
    [Tooltip("Should the borders wrap around the Grid Tiles, or stretch to perfectly frame the Camera screen?")]
    public FitMode fitMode = FitMode.MatchGridTiles;

    [Tooltip("Required to find the baseline depth and center.")]
    public TopGridManager targetGridManager;

    [Tooltip("Required if using MatchCameraFrustum.")]
    public Camera mainCamera;

    [Header("Center Object 1")]
    public GameObject centerObject1;
    public bool autoScaleBorder1 = true;
    public Vector3 borderPadding1 = Vector3.zero;
    public Vector3 centerObject1Offset = Vector3.zero;

    [Header("Center Object 2")]
    public GameObject centerObject2;
    public bool autoScaleBorder2 = true;
    public Vector3 borderPadding2 = Vector3.zero;
    public Vector3 centerObject2Offset = Vector3.zero;

    // Tracking variables for Grid changes
    private Vector3 lastFirstTilePos;
    private int lastChildCount;
    private Vector3 lastGridScale;

    // Tracking variables for Camera changes (Crucial for Aspect Ratio fixes)
    private Vector3 lastCamPos;
    private Quaternion lastCamRot;
    private float lastCamSize;
    private float lastCamAspect;
    private int lastScreenWidth;
    private int lastScreenHeight;

    // Tracking variables for Inspector Offset/Padding changes
    private FitMode lastFitMode;
    private bool lastAuto1, lastAuto2;
    private Vector3 lastPadding1, lastPadding2;
    private Vector3 lastOffset1, lastOffset2;

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        // Strictly only run when the game is actively playing
        if (!Application.isPlaying) return;

        bool shouldUpdate = false;

        // 1. Detect if any settings or padding were changed in the inspector
        if (fitMode != lastFitMode) { lastFitMode = fitMode; shouldUpdate = true; }

        if (autoScaleBorder1 != lastAuto1) { lastAuto1 = autoScaleBorder1; shouldUpdate = true; }
        if (borderPadding1 != lastPadding1) { lastPadding1 = borderPadding1; shouldUpdate = true; }
        if (centerObject1Offset != lastOffset1) { lastOffset1 = centerObject1Offset; shouldUpdate = true; }

        if (autoScaleBorder2 != lastAuto2) { lastAuto2 = autoScaleBorder2; shouldUpdate = true; }
        if (borderPadding2 != lastPadding2) { lastPadding2 = borderPadding2; shouldUpdate = true; }
        if (centerObject2Offset != lastOffset2) { lastOffset2 = centerObject2Offset; shouldUpdate = true; }

        // 2. Detect Camera changes (Fixes Aspect Ratio switching on devices)
        if (mainCamera != null)
        {
            if (mainCamera.aspect != lastCamAspect) { lastCamAspect = mainCamera.aspect; shouldUpdate = true; }
            if (mainCamera.transform.position != lastCamPos) { lastCamPos = mainCamera.transform.position; shouldUpdate = true; }
            if (mainCamera.transform.rotation != lastCamRot) { lastCamRot = mainCamera.transform.rotation; shouldUpdate = true; }

            float currentSize = mainCamera.orthographic ? mainCamera.orthographicSize : mainCamera.fieldOfView;
            if (currentSize != lastCamSize) { lastCamSize = currentSize; shouldUpdate = true; }

            if (Screen.width != lastScreenWidth) { lastScreenWidth = Screen.width; shouldUpdate = true; }
            if (Screen.height != lastScreenHeight) { lastScreenHeight = Screen.height; shouldUpdate = true; }
        }

        // 3. Detect Grid changes
        if (targetGridManager != null && targetGridManager.transform.childCount > 0)
        {
            if (targetGridManager.transform.childCount != lastChildCount)
            {
                lastChildCount = targetGridManager.transform.childCount;
                shouldUpdate = true;
            }

            Transform firstTile = targetGridManager.transform.GetChild(0);
            if (firstTile.localPosition != lastFirstTilePos)
            {
                lastFirstTilePos = firstTile.localPosition;
                shouldUpdate = true;
            }

            if (targetGridManager.transform.localScale != lastGridScale)
            {
                lastGridScale = targetGridManager.transform.localScale;
                shouldUpdate = true;
            }
        }

        // Apply updates ONLY if something was flagged as changed
        if (shouldUpdate)
        {
            UpdateCenterObjects();
        }
    }

    public void UpdateCenterObjects()
    {
        if (targetGridManager == null) return;
        Transform refTrans = targetGridManager.transform;

        Grid grid = targetGridManager.GetComponent<Grid>();
        Vector3 cellSize = grid != null ? grid.cellSize : Vector3.zero;

        // Base Grid calculations (Provides a stable depth anchor point)
        Vector3 gridWorldCenter = refTrans.position;
        float gridWorldWidth = 0f;
        float gridWorldHeight = 0f;
        float gridWorldDepth = 0f;

        if (targetGridManager.transform.childCount > 0)
        {
            Bounds gridBounds = new Bounds(targetGridManager.transform.GetChild(0).localPosition, Vector3.zero);
            for (int i = 0; i < targetGridManager.transform.childCount; i++)
            {
                Transform child = targetGridManager.transform.GetChild(i);
                if ((centerObject1 != null && child == centerObject1.transform) ||
                    (centerObject2 != null && child == centerObject2.transform))
                {
                    continue;
                }
                gridBounds.Encapsulate(child.localPosition);
            }
            gridWorldCenter = refTrans.TransformPoint(gridBounds.center);

            // Compute actual World dimensions of the Grid
            Vector3 refScale = refTrans.lossyScale;
            gridWorldWidth = (gridBounds.size.x + cellSize.x) * Mathf.Abs(refScale.x);
            gridWorldHeight = (gridBounds.size.y + cellSize.y) * Mathf.Abs(refScale.y);
            gridWorldDepth = (cellSize.z > 0 ? cellSize.z : 0f) * Mathf.Abs(refScale.z);
        }

        ApplyToCenterObject(centerObject1, autoScaleBorder1, borderPadding1, centerObject1Offset, gridWorldCenter, gridWorldWidth, gridWorldHeight, gridWorldDepth, refTrans);
        ApplyToCenterObject(centerObject2, autoScaleBorder2, borderPadding2, centerObject2Offset, gridWorldCenter, gridWorldWidth, gridWorldHeight, gridWorldDepth, refTrans);
    }

    private void ApplyToCenterObject(GameObject centerObject, bool autoScaleBorder, Vector3 borderPadding, Vector3 positionOffset, Vector3 gridWorldCenter, float gridWorldWidth, float gridWorldHeight, float gridWorldDepth, Transform refTrans)
    {
        if (centerObject == null) return;

        Vector3 finalWorldCenter;
        float targetWorldX, targetWorldY, targetWorldZ;

        if (fitMode == FitMode.MatchGridTiles)
        {
            // Center exactly on the Grid
            finalWorldCenter = gridWorldCenter + refTrans.TransformVector(positionOffset);
            targetWorldX = gridWorldWidth + borderPadding.x;
            targetWorldY = gridWorldHeight + borderPadding.y;
            targetWorldZ = gridWorldDepth + borderPadding.z;
        }
        else // MatchCameraFrustum
        {
            if (mainCamera == null) return;

            // 1. Calculate intended depth point by applying the Z offset to the Grid's base depth
            Vector3 depthOffsetWorld = refTrans.TransformVector(new Vector3(0, 0, positionOffset.z));
            Vector3 objectDepthPoint = gridWorldCenter + depthOffsetWorld;

            // 2. Exact distance from camera to this specific depth plane
            float distanceToCam = Vector3.Dot(objectDepthPoint - mainCamera.transform.position, mainCamera.transform.forward);

            // 3. Project screen center exactly to this distance
            Ray centerRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 screenCenterWorld = centerRay.GetPoint(Mathf.Abs(distanceToCam));

            // 4. Calculate Mathematical Frustum dimensions at this distance
            float frustumHeight, frustumWidth;
            if (mainCamera.orthographic)
            {
                frustumHeight = mainCamera.orthographicSize * 2f;
            }
            else
            {
                frustumHeight = 2.0f * Mathf.Abs(distanceToCam) * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            }
            frustumWidth = frustumHeight * mainCamera.aspect;

            // 5. Apply XY offsets relative to the screen center
            Vector3 xyOffsetWorld = refTrans.TransformVector(new Vector3(positionOffset.x, positionOffset.y, 0));
            finalWorldCenter = screenCenterWorld + xyOffsetWorld;

            targetWorldX = frustumWidth + borderPadding.x;
            targetWorldY = frustumHeight + borderPadding.y;
            targetWorldZ = borderPadding.z;
        }

        // Apply final locked position
        centerObject.transform.position = finalWorldCenter;

        // Apply Scale Logic
        if (autoScaleBorder)
        {
            // Compute needed local scale by dividing world size by parent's lossy scale (prevents grid scaling from stretching it)
            Vector3 parentScale = centerObject.transform.parent != null ? centerObject.transform.parent.lossyScale : Vector3.one;

            float targetLocalX = targetWorldX / (parentScale.x != 0 ? Mathf.Abs(parentScale.x) : 1f);
            float targetLocalY = targetWorldY / (parentScale.y != 0 ? Mathf.Abs(parentScale.y) : 1f);

            Vector3 currentLocalScale = centerObject.transform.localScale;
            float targetLocalZ = targetWorldZ > 0f ? (targetWorldZ / (parentScale.z != 0 ? Mathf.Abs(parentScale.z) : 1f)) : currentLocalScale.z;

            Vector3 targetLocalSize = new Vector3(targetLocalX, targetLocalY, targetLocalZ);

            SpriteRenderer sr = centerObject.GetComponent<SpriteRenderer>();
            if (sr == null) sr = centerObject.GetComponentInChildren<SpriteRenderer>();

            MeshFilter mf = centerObject.GetComponent<MeshFilter>();
            if (mf == null) mf = centerObject.GetComponentInChildren<MeshFilter>();

            if (sr != null && sr.drawMode != SpriteDrawMode.Simple)
            {
                sr.size = new Vector2(targetLocalSize.x, targetLocalSize.y);
                centerObject.transform.localScale = new Vector3(1f, 1f, targetLocalSize.z);
            }
            else if (sr != null && sr.sprite != null)
            {
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
                Vector3 meshSize = mf.sharedMesh.bounds.size;
                float scaleX = meshSize.x > 0f ? targetLocalSize.x / meshSize.x : targetLocalSize.x;
                float scaleY = meshSize.y > 0f ? targetLocalSize.y / meshSize.y : targetLocalSize.y;
                float scaleZ = (meshSize.z > 0f && targetWorldZ > 0f) ? targetLocalSize.z / meshSize.z : targetLocalSize.z;

                centerObject.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
            }
            else
            {
                centerObject.transform.localScale = targetLocalSize;
            }
        }
    }
}