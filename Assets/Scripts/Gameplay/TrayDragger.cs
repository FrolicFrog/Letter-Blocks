using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;

public class GlobalTrayDragger : MonoBehaviour
{
    public enum PlaneAxisMode
    {
        XZ_GroundPlane_3D, // Standard 3D Board (Lock World Y Height, Drag on X & Z)
        XY_FrontalPlane_2D  // 2D Board (Lock World Z Depth, Drag on X & Y)
    }

    #region Inspector Variables

    [Header("Raycast Settings")]
    [Tooltip("Target layer for raycasting. Set this to 'Tray' in the Inspector.")]
    public LayerMask trayLayer;

    [Tooltip("Target layer for static blockers. Set this to 'Block' in the Inspector.")]
    public LayerMask blockLayer;

    [Header("Snap Zone Settings")]
    [Tooltip("Target layer for the green Snap area at the top of the board.")]
    public LayerMask snapLayer;

    [Tooltip("Distance from the absolute top edge of the board when snapped.")]
    public float snapOffset = 0.05f;

    [Header("Drag Plane Mode")]
    [Tooltip("Select XZ for standard 3D top-down boards, or XY for 2D boards.")]
    public PlaneAxisMode planeMode = PlaneAxisMode.XZ_GroundPlane_3D;

    [Header("Grid Snapping Settings")]
    [Tooltip("Enable step-by-step grid sliding during drag.")]
    public bool snapToGridWhileDragging = true;

    [Tooltip("Distance of one grid cell tile. Auto-detected from BottomGridManager if available.")]
    public float gridCellSize = 1.0f;

    [Tooltip("Speed at which the piece slides toward target grid steps while dragging.")]
    public float slideSpeed = 45f;

    [Header("Jump Trigger Settings")]
    [Tooltip("Distance from the top limit to trigger the piece jumping.")]
    public float topWallTriggerOffset = 0.2f;

    [Tooltip("Delay in seconds between each letter jumping off the tray.")]
    public float staggeredJumpDelay = 0.15f;

    [Header("Shift Settings")]
    [Tooltip("Duration in seconds for the lower tiles to slide up into empty spaces.")]
    public float shiftDuration = 0.2f;

    [Header("Auto Boundaries from BottomGridManager")]
    [Tooltip("Extra padding to keep pieces slightly away from the exact visual edge.")]
    public float boundaryPadding = 0.02f;

    [Header("Height / Depth Offset Settings")]
    [Tooltip("Offset applied along the locked axis while dragging.")]
    public float dragOffset = 0f;

    [Header("Scale Settings")]
    [Tooltip("Multiplier applied to Y-axis scale while dragging.")]
    public float dragScaleMultiplier = 1.0f;

    [Tooltip("Duration in seconds for the piece to scale up on click.")]
    public float scaleUpDuration = 0.1f;

    [Header("DOTween Snapback Settings")]
    [Tooltip("Duration in seconds for the piece to animate back to its starting position and scale.")]
    public float snapBackDuration = 0.18f;

    [Tooltip("Easing function for the return animation.")]
    public Ease snapBackEase = Ease.OutBack;

    #endregion

    #region Private State Variables

    private Camera mainCam;
    private Transform currentlyDraggedParent;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Vector3 clickOffset;
    private Plane dragPlane;
    private float lockedAxisValue;

    private float lockedSnapValue;
    private bool isReadyToJump = false;
    private bool canAutoRelease = true; // Tracks if the piece is allowed to auto-drop
    private Dictionary<Transform, Coroutine> activeJumpRoutines = new Dictionary<Transform, Coroutine>();

    // Dynamically calculated boundaries
    private float bMinX, bMaxX, bMinAxis, bMaxAxis, bTopWallTriggerThreshold;

    // Snapshots to perfectly preserve tile scales across weirdly shaped walls
    private Dictionary<Transform, Vector3> wallDefaultPos = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Vector3> wallDefaultScale = new Dictionary<Transform, Vector3>();

    // --- COLLISION & JUMP TRACKING ---
    private struct BlockColliderData
    {
        public Vector3 unscaledLocalOffset;
        public Vector3 unscaledExtents;
        public Quaternion rotation;
    }

    private List<BlockColliderData> pieceColliders = new List<BlockColliderData>();

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        mainCam = Camera.main;

        if (trayLayer == 0) trayLayer = LayerMask.GetMask("Tray");
        if (blockLayer == 0) blockLayer = LayerMask.GetMask("Block");
        if (snapLayer == 0) snapLayer = LayerMask.GetMask("Snap");
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) TrySelectTray();
        if (currentlyDraggedParent != null && Input.GetMouseButton(0)) DragSelectedParent();
        if (Input.GetMouseButtonUp(0) && currentlyDraggedParent != null) ReleaseAndSnapBack();
    }

    #endregion

    #region Selection & Dragging

    private void TrySelectTray()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, trayLayer))
        {
            if (hit.transform.parent != null)
            {
                currentlyDraggedParent = hit.transform.parent;
                isReadyToJump = false;
                lockedSnapValue = 0f;

                if (activeJumpRoutines.ContainsKey(currentlyDraggedParent))
                {
                    if (activeJumpRoutines[currentlyDraggedParent] != null)
                        StopCoroutine(activeJumpRoutines[currentlyDraggedParent]);
                    activeJumpRoutines.Remove(currentlyDraggedParent);
                }

                wallDefaultPos.Clear();
                wallDefaultScale.Clear();

                foreach (Transform child in currentlyDraggedParent)
                {
                    if (child.name.Contains("Wall"))
                    {
                        Transform t = GetValidTile(child);
                        if (t != null)
                        {
                            wallDefaultPos[child] = t.localPosition;
                            wallDefaultScale[child] = t.localScale;
                        }
                    }
                }

                currentlyDraggedParent.DOKill();
                UpdateTrayLayers(currentlyDraggedParent, true);

                originalPosition = currentlyDraggedParent.position;
                originalScale = currentlyDraggedParent.localScale;

                Vector3 elevatedStartPos = originalPosition;

                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                {
                    lockedAxisValue = originalPosition.y + dragOffset;
                    elevatedStartPos.y = lockedAxisValue;
                    dragPlane = new Plane(Vector3.up, new Vector3(0, lockedAxisValue, 0));
                }
                else
                {
                    lockedAxisValue = originalPosition.z + dragOffset;
                    elevatedStartPos.z = lockedAxisValue;
                    dragPlane = new Plane(Vector3.back, new Vector3(0, 0, lockedAxisValue));
                }

                currentlyDraggedParent.position = elevatedStartPos;

                CalculateDynamicBoundaries();

                bool startedInSnapZone = IsInSnapZone(originalPosition, out _) ||
                                         (planeMode == PlaneAxisMode.XZ_GroundPlane_3D ? originalPosition.z >= bTopWallTriggerThreshold : originalPosition.y >= bTopWallTriggerThreshold);
                canAutoRelease = !startedInSnapZone;

                Vector3 targetScale = new Vector3(originalScale.x, originalScale.y * dragScaleMultiplier, originalScale.z);
                currentlyDraggedParent.DOScale(targetScale, scaleUpDuration).SetEase(Ease.OutQuad);

                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 clickWorldPoint = ray.GetPoint(enter);
                    clickOffset = elevatedStartPos - clickWorldPoint;
                }
            }
        }
    }

    private void DragSelectedParent()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 currentMouseWorldPoint = ray.GetPoint(enter);
            Vector3 targetPosition = currentMouseWorldPoint + clickOffset;

            if (ResultManager.Instance != null && !ResultManager.Instance.startTimer)
                ResultManager.Instance.startTimer = true;

            targetPosition.x = Mathf.Clamp(targetPosition.x, bMinX, bMaxX);

            if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
            {
                targetPosition.z = Mathf.Clamp(targetPosition.z, bMinAxis, bMaxAxis);
                targetPosition.y = lockedAxisValue;
            }
            else
            {
                targetPosition.y = Mathf.Clamp(targetPosition.y, bMinAxis, bMaxAxis);
                targetPosition.z = lockedAxisValue;
            }

            Vector3 targetStepPos;

            if (snapToGridWhileDragging && gridCellSize > 0.05f)
            {
                targetStepPos = ResolveGridStepMovement(currentlyDraggedParent.position, targetPosition);
            }
            else
            {
                targetStepPos = ResolveTrayCollisions(currentlyDraggedParent.position, targetPosition);
            }

            // --- GREEN AREA SNAP VISUAL LOCK & OVERLAP PREVENTION ---
            bool isTouchingSnapZone = IsInSnapZone(targetPosition, out Transform snapWall);
            bool shouldSnap = false;
            float proposedSnapValue = 0f;

            if (isTouchingSnapZone)
            {
                // Align top of the piece dynamically relative to the board boundaries so all pieces sit completely flush
                proposedSnapValue = bMaxAxis - snapOffset;
                shouldSnap = true;
            }
            else
            {
                bool pastThreshold = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                                     ? targetPosition.z >= bTopWallTriggerThreshold
                                     : targetPosition.y >= bTopWallTriggerThreshold;

                if (pastThreshold)
                {
                    proposedSnapValue = bMaxAxis - snapOffset;
                    shouldSnap = true;
                }
            }

            if (shouldSnap)
            {
                Vector3 testSnapPos = targetStepPos;

                // Force the X position to perfectly align with the grid columns below
                testSnapPos.x = GetNearestGridColumnX(testSnapPos.x);

                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) testSnapPos.z = proposedSnapValue;
                else testSnapPos.y = proposedSnapValue;

                if (!IsOverlappingTray(testSnapPos, true))
                {
                    lockedSnapValue = proposedSnapValue;
                    isReadyToJump = true;
                    targetStepPos.x = testSnapPos.x; // Apply the perfect alignment
                }
                else
                {
                    isReadyToJump = false;
                }
            }
            else
            {
                isReadyToJump = false;
                canAutoRelease = true;
            }

            if (isReadyToJump)
            {
                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                    targetStepPos.z = lockedSnapValue;
                else
                    targetStepPos.y = lockedSnapValue;
            }

            if (currentlyDraggedParent == null) return;
            currentlyDraggedParent.position = Vector3.Lerp(currentlyDraggedParent.position, targetStepPos, Time.deltaTime * slideSpeed);

            if (isReadyToJump && canAutoRelease)
            {
                currentlyDraggedParent.position = targetStepPos;
                ReleaseAndSnapBack();
            }
        }
    }

    #endregion

    #region Movement & Collision Resolution

    private Vector3 ResolveGridStepMovement(Vector3 currentPos, Vector3 targetPos)
    {
        Vector3 diff = targetPos - currentPos;

        if (Mathf.Abs(diff.x) >= gridCellSize * 0.15f)
        {
            int stepsX = Mathf.RoundToInt(diff.x / gridCellSize);
            int dirX = System.Math.Sign(stepsX);
            int maxStepsX = Mathf.Abs(stepsX);

            Vector3 testPos = currentPos;
            for (int i = 0; i < maxStepsX; i++)
            {
                Vector3 nextStep = testPos;
                nextStep.x += dirX * gridCellSize;
                nextStep.x = Mathf.Clamp(nextStep.x, bMinX, bMaxX);

                bool collisionFound = false;
                int subSteps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(nextStep.x - testPos.x) / 0.2f));
                for (int j = 1; j <= subSteps; j++)
                {
                    Vector3 subStepPos = Vector3.Lerp(testPos, nextStep, (float)j / subSteps);
                    if (IsOverlappingTray(subStepPos, true))
                    {
                        collisionFound = true;
                        break;
                    }
                }

                if (!collisionFound) testPos = nextStep;
                else break;
            }
            currentPos.x = testPos.x;
        }

        if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
        {
            if (Mathf.Abs(diff.z) >= gridCellSize * 0.15f)
            {
                int stepsZ = Mathf.RoundToInt(diff.z / gridCellSize);
                int dirZ = System.Math.Sign(stepsZ);
                int maxStepsZ = Mathf.Abs(stepsZ);

                Vector3 testPos = currentPos;
                for (int i = 0; i < maxStepsZ; i++)
                {
                    Vector3 nextStep = testPos;
                    nextStep.z += dirZ * gridCellSize;
                    nextStep.z = Mathf.Clamp(nextStep.z, bMinAxis, bMaxAxis);

                    bool collisionFound = false;
                    int subSteps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(nextStep.z - testPos.z) / 0.2f));
                    for (int j = 1; j <= subSteps; j++)
                    {
                        Vector3 subStepPos = Vector3.Lerp(testPos, nextStep, (float)j / subSteps);
                        if (IsOverlappingTray(subStepPos, true))
                        {
                            collisionFound = true;
                            break;
                        }
                    }

                    if (!collisionFound) testPos = nextStep;
                    else break;
                }
                currentPos.z = testPos.z;
            }
        }
        else
        {
            if (Mathf.Abs(diff.y) >= gridCellSize * 0.15f)
            {
                int stepsY = Mathf.RoundToInt(diff.y / gridCellSize);
                int dirY = System.Math.Sign(stepsY);
                int maxStepsY = Mathf.Abs(stepsY);

                Vector3 testPos = currentPos;
                for (int i = 0; i < maxStepsY; i++)
                {
                    Vector3 nextStep = testPos;
                    nextStep.y += dirY * gridCellSize;
                    nextStep.y = Mathf.Clamp(nextStep.y, bMinAxis, bMaxAxis);

                    bool collisionFound = false;
                    int subSteps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(nextStep.y - testPos.y) / 0.2f));
                    for (int j = 1; j <= subSteps; j++)
                    {
                        Vector3 subStepPos = Vector3.Lerp(testPos, nextStep, (float)j / subSteps);
                        if (IsOverlappingTray(subStepPos, true))
                        {
                            collisionFound = true;
                            break;
                        }
                    }

                    if (!collisionFound) testPos = nextStep;
                    else break;
                }
                currentPos.y = testPos.y;
            }
        }

        return currentPos;
    }

    private Vector3 ResolveTrayCollisions(Vector3 currentPos, Vector3 targetPos)
    {
        Vector3 finalPos = currentPos;
        Vector3 testPos = currentPos;

        float diffX = targetPos.x - currentPos.x;
        int subStepsX = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(diffX) / 0.2f));
        for (int j = 1; j <= subStepsX; j++)
        {
            testPos.x = currentPos.x + (diffX * ((float)j / subStepsX));
            if (!IsOverlappingTray(testPos, true)) finalPos.x = testPos.x;
            else break;
        }

        testPos = finalPos;

        if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
        {
            float diffZ = targetPos.z - currentPos.z;
            int subStepsZ = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(diffZ) / 0.2f));
            for (int j = 1; j <= subStepsZ; j++)
            {
                testPos.z = currentPos.z + (diffZ * ((float)j / subStepsZ));
                if (!IsOverlappingTray(testPos, true)) finalPos.z = testPos.z;
                else break;
            }
        }
        else
        {
            float diffY = targetPos.y - currentPos.y;
            int subStepsY = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(diffY) / 0.2f));
            for (int j = 1; j <= subStepsY; j++)
            {
                testPos.y = currentPos.y + (diffY * ((float)j / subStepsY));
                if (!IsOverlappingTray(testPos, true)) finalPos.y = testPos.y;
                else break;
            }
        }

        return finalPos;
    }

    private bool IsOverlappingTray(Vector3 testPos, bool isDragging)
    {
        LayerMask combinedCollisionLayers = trayLayer | blockLayer;

        foreach (var block in pieceColliders)
        {
            Vector3 checkCenter = testPos + block.unscaledLocalOffset;

            if (isDragging)
            {
                Vector3 checkExtents = block.unscaledExtents * 0.82f;

                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                {
                    checkCenter.y -= dragOffset;
                    checkExtents.y += 2f;
                }
                else
                {
                    checkCenter.z -= dragOffset;
                    checkExtents.z += 2f;
                }

                Collider[] hits = Physics.OverlapBox(checkCenter, checkExtents, block.rotation, combinedCollisionLayers);
                foreach (Collider hit in hits)
                {
                    if (!hit.transform.IsChildOf(currentlyDraggedParent)) return true;
                }
            }
            else
            {
                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) checkCenter.y -= dragOffset;
                else checkCenter.z -= dragOffset;

                Collider[] hits = Physics.OverlapSphere(checkCenter, 0.1f, combinedCollisionLayers);
                foreach (Collider hit in hits)
                {
                    if (!hit.transform.IsChildOf(currentlyDraggedParent)) return true;
                }
            }
        }
        return false;
    }

    private bool IsInSnapZone(Vector3 testPos, out Transform snapWall)
    {
        snapWall = null;
        if (snapLayer == 0 || pieceColliders.Count == 0) return false;

        foreach (var block in pieceColliders)
        {
            Vector3 checkCenter = testPos + block.unscaledLocalOffset;

            Collider[] hits = Physics.OverlapBox(checkCenter, block.unscaledExtents * 0.8f, block.rotation, snapLayer);
            if (hits.Length > 0)
            {
                snapWall = hits[0].transform;
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Boundary & Calculation Logic

    private void CalculateDynamicBoundaries()
    {
        bMinX = -10f; bMaxX = 10f;
        bMinAxis = -10f; bMaxAxis = 10f;

        Bounds? boardBounds = null;
        if (BottomGridManager.Instance != null && BottomGridManager.Instance.centerObject != null)
        {
            Renderer borderRenderer = BottomGridManager.Instance.centerObject.GetComponentInChildren<Renderer>();
            if (borderRenderer != null) boardBounds = borderRenderer.bounds;

            Transform gridTransform = BottomGridManager.Instance.transform;
            if (gridTransform.childCount >= 2)
            {
                float detectedDist = Vector3.Distance(gridTransform.GetChild(0).position, gridTransform.GetChild(1).position);
                if (detectedDist > 0.1f && detectedDist < 5f)
                {
                    gridCellSize = detectedDist;
                }
            }
        }

        pieceColliders.Clear();

        if (currentlyDraggedParent != null)
        {
            Renderer[] renderers = currentlyDraggedParent.GetComponentsInChildren<Renderer>();
            Collider[] colliders = currentlyDraggedParent.GetComponentsInChildren<Collider>();

            Vector3 pivotPos = currentlyDraggedParent.position;

            float extLeft = 0, extRight = 0, extBottom = 0, extTop = 0;
            if (renderers.Length > 0)
            {
                Bounds pieceBounds = renderers[0].bounds;
                foreach (Renderer r in renderers) pieceBounds.Encapsulate(r.bounds);

                extLeft = (pivotPos.x - pieceBounds.min.x);
                extRight = (pieceBounds.max.x - pivotPos.x);

                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                {
                    extBottom = (pivotPos.z - pieceBounds.min.z);
                    extTop = (pieceBounds.max.z - pivotPos.z);
                }
                else
                {
                    extBottom = (pivotPos.y - pieceBounds.min.y);
                    extTop = (pieceBounds.max.y - pivotPos.y);
                }
            }

            if (boardBounds.HasValue)
            {
                bMinX = boardBounds.Value.min.x + extLeft + boundaryPadding;
                bMaxX = boardBounds.Value.max.x - extRight - boundaryPadding;

                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                {
                    bMinAxis = boardBounds.Value.min.z + extBottom + boundaryPadding;
                    bMaxAxis = boardBounds.Value.max.z - extTop - boundaryPadding;
                }
                else
                {
                    bMinAxis = boardBounds.Value.min.y + extBottom + boundaryPadding;
                    bMaxAxis = boardBounds.Value.max.y - extTop - boundaryPadding;
                }
            }

            bTopWallTriggerThreshold = bMaxAxis - topWallTriggerOffset;

            foreach (Collider col in colliders)
            {
                if (((1 << col.gameObject.layer) & trayLayer) != 0)
                {
                    BlockColliderData blockData = new BlockColliderData();
                    blockData.unscaledLocalOffset = col.transform.position - pivotPos;
                    blockData.unscaledExtents = col.bounds.extents;
                    blockData.rotation = col.transform.rotation;
                    pieceColliders.Add(blockData);
                }
            }
        }
    }

    #endregion

    #region Jumping & Tray Collapse Logic

    private void TriggerJumpLogic(Transform trayToJump)
    {
        if (trayToJump == null || trayToJump.childCount == 0) return;

        if (activeJumpRoutines.ContainsKey(trayToJump) && activeJumpRoutines[trayToJump] != null)
        {
            StopCoroutine(activeJumpRoutines[trayToJump]);
        }
        activeJumpRoutines[trayToJump] = StartCoroutine(JumpRoutine(trayToJump));
    }

    private IEnumerator JumpRoutine(Transform activeTray)
    {
        while (activeTray != null)
        {
            while (WordChecker.instance != null && WordChecker.instance.isShifting)
            {
                yield return null;
            }

            if (activeTray == null) break;

            List<Transform> allWalls = new List<Transform>();
            foreach (Transform child in activeTray)
            {
                if (child.name.Contains("Wall")) allWalls.Add(child);
            }

            bool jumpedAny = false;
            List<Transform> availableTiles = new List<Transform>();

            foreach (Transform wall in allWalls)
            {
                Transform t = GetValidTile(wall);
                if (t != null) availableTiles.Add(t);
            }

            Transform bestTileToJump = null;
            Transform bestSlotTransform = null;
            Vector2Int bestMatchedKey = default;

            if (TopGridManager.instance != null && LevelManager.Instance != null)
            {
                var grid = TopGridManager.instance;
                var lvlManager = LevelManager.Instance;
                int cols = grid.columns;

                int startRow = grid.rows - 1;
                int endRow = Mathf.Max(0, grid.rows - 3);

                for (int r = startRow; r >= endRow; r--)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        Vector2Int key = new Vector2Int(r, c);
                        if (lvlManager.excludedChar.Contains(key))
                        {
                            string neededLetter = lvlManager.cellTexts[key];

                            foreach (Transform tile in availableTiles)
                            {
                                var textMesh = tile.GetComponentInChildren<TextMeshPro>();
                                if (textMesh != null && textMesh.text == neededLetter)
                                {
                                    bestTileToJump = tile;
                                    int index = key.x * cols + key.y;
                                    Transform cellContainer = grid.transform.GetChild(index);

                                    if (cellContainer.childCount > 1) bestSlotTransform = cellContainer.GetChild(1);
                                    else bestSlotTransform = cellContainer;

                                    bestMatchedKey = key;
                                    lvlManager.excludedChar.Remove(key);
                                    WordChecker.reservedGridSlots.Add(key);
                                    break;
                                }
                            }
                        }
                        if (bestTileToJump != null) break;
                    }
                    if (bestTileToJump != null) break;
                }
            }

            if (bestTileToJump != null && bestSlotTransform != null)
            {
                bestTileToJump.name = "JumpingTile";
                WordChecker.instance.AnimateTrayBlockToGrid(bestTileToJump, bestSlotTransform, bestMatchedKey);
                CollapseTray(allWalls);
                jumpedAny = true;
            }

            if (CheckAndDestroyEmptyTray(activeTray))
            {
                break;
            }

            if (jumpedAny)
            {
                yield return new WaitForSeconds(staggeredJumpDelay);
            }
            else
            {
                yield return null;
            }
        }
    }

    private bool CheckAndDestroyEmptyTray(Transform tray)
    {
        if (tray == null) return true;

        int totalRemaining = 0;
        foreach (Transform child in tray)
        {
            if (child.name.Contains("Wall") && GetValidTile(child) != null)
            {
                totalRemaining++;
            }
        }

        if (totalRemaining == 0)
        {
            if (currentlyDraggedParent == tray) currentlyDraggedParent = null;

            if (activeJumpRoutines.ContainsKey(tray))
            {
                activeJumpRoutines.Remove(tray);
            }

            tray.DOKill();
            tray.DOScale(Vector3.zero, snapBackDuration).SetEase(Ease.InBack).OnComplete(() =>
            {
                if (tray != null) Destroy(tray.gameObject);
            });

            return true;
        }

        return false;
    }

    private void CollapseTray(List<Transform> allWalls)
    {
        bool shiftedSomething = true;
        int maxIterations = 10;

        while (shiftedSomething && maxIterations > 0)
        {
            shiftedSomething = false;
            maxIterations--;

            float maxHeight = float.MinValue;
            foreach (Transform wall in allWalls)
            {
                float h = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? wall.localPosition.z : wall.localPosition.y;
                if (h > maxHeight) maxHeight = h;
            }

            List<Transform> emptyWalls = new List<Transform>();
            List<Transform> filledWalls = new List<Transform>();

            foreach (Transform wall in allWalls)
            {
                if (GetValidTile(wall) == null) emptyWalls.Add(wall);
                else filledWalls.Add(wall);
            }

            emptyWalls.Sort((a, b) => {
                float hA = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? a.localPosition.z : a.localPosition.y;
                float hB = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? b.localPosition.z : b.localPosition.y;
                return hB.CompareTo(hA);
            });

            foreach (Transform emptyWall in emptyWalls)
            {
                Transform bestTileWall = null;
                float minDistance = float.MaxValue;
                float emptyH = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? emptyWall.localPosition.z : emptyWall.localPosition.y;

                foreach (Transform filledWall in filledWalls)
                {
                    float filledH = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? filledWall.localPosition.z : filledWall.localPosition.y;

                    if (filledH >= maxHeight - 0.1f) continue;
                    if (filledH > emptyH + 0.1f) continue;

                    float dist = Vector3.Distance(emptyWall.localPosition, filledWall.localPosition);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestTileWall = filledWall;
                    }
                }

                if (bestTileWall != null)
                {
                    Transform tileToMove = GetValidTile(bestTileWall);
                    if (tileToMove != null)
                    {
                        tileToMove.SetParent(emptyWall, true);
                        tileToMove.SetAsFirstSibling();

                        Vector3 targetPos = wallDefaultPos.ContainsKey(emptyWall) ? wallDefaultPos[emptyWall] : Vector3.zero;
                        Vector3 targetScale = wallDefaultScale.ContainsKey(emptyWall) ? wallDefaultScale[emptyWall] : Vector3.one;

                        tileToMove.DOKill();
                        tileToMove.DOLocalMove(targetPos, shiftDuration).SetEase(Ease.OutQuad);
                        tileToMove.DOScale(targetScale, shiftDuration).SetEase(Ease.OutQuad);

                        shiftedSomething = true;
                        filledWalls.Remove(bestTileWall);
                    }
                }
            }
        }
    }

    private Transform GetValidTile(Transform wall)
    {
        foreach (Transform child in wall)
        {
            if (child.name.Contains("Tile letter")) return child;
        }
        return null;
    }

    #endregion

    #region Release & Snapback

    private void ReleaseAndSnapBack()
    {
        if (currentlyDraggedParent == null) return;

        if (isReadyToJump)
        {
            Transform trayToJump = currentlyDraggedParent;
            currentlyDraggedParent = null;
            isReadyToJump = false;

            trayToJump.DOScale(originalScale, snapBackDuration).SetEase(snapBackEase);

            UpdateTrayLayers(trayToJump, false);
            TriggerJumpLogic(trayToJump);
            return;
        }

        if (CheckAndDestroyEmptyTray(currentlyDraggedParent)) return;

        Vector3 finalTargetPos = originalPosition;

        if (BottomGridManager.Instance != null && currentlyDraggedParent.childCount > 0)
        {
            currentlyDraggedParent.localScale = originalScale;
            Transform anchorChunk = currentlyDraggedParent.GetChild(0);

            finalTargetPos = GetForgivingSnapPosition(anchorChunk);

            currentlyDraggedParent.localScale = new Vector3(originalScale.x, originalScale.y * dragScaleMultiplier, originalScale.z);
        }

        if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) finalTargetPos.y = originalPosition.y;
        else finalTargetPos.z = originalPosition.z;

        currentlyDraggedParent.DOMove(finalTargetPos, snapBackDuration).SetEase(snapBackEase);
        currentlyDraggedParent.DOScale(originalScale, snapBackDuration).SetEase(snapBackEase);

        UpdateTrayLayers(currentlyDraggedParent, false);
        currentlyDraggedParent = null;
    }

    private float GetNearestGridColumnX(float currentX)
    {
        if (BottomGridManager.Instance == null || currentlyDraggedParent == null || currentlyDraggedParent.childCount == 0)
            return currentX;

        Transform gridParent = BottomGridManager.Instance.transform;
        if (gridParent.childCount == 0) return currentX;

        Transform anchorChunk = currentlyDraggedParent.GetChild(0);
        float anchorOffsetX = anchorChunk.position.x - currentlyDraggedParent.position.x;

        float nearestX = currentX;
        float minDiff = float.MaxValue;

        for (int i = 0; i < gridParent.childCount; i++)
        {
            float slotX = gridParent.GetChild(i).position.x;
            float proposedX = slotX - anchorOffsetX;
            float diff = Mathf.Abs(proposedX - currentX);

            if (diff < minDiff)
            {
                minDiff = diff;
                nearestX = proposedX;
            }
        }

        return nearestX;
    }

    private Vector3 GetForgivingSnapPosition(Transform anchorChunk)
    {
        Transform gridParent = BottomGridManager.Instance.transform;

        Vector3 anchorPos = anchorChunk.position;
        Vector3 pivotToAnchorOffset = anchorPos - currentlyDraggedParent.position;
        List<Transform> validSlots = new List<Transform>();

        int totalTrueSlots = BottomGridManager.Instance.width * BottomGridManager.Instance.height;
        for (int i = 0; i < totalTrueSlots; i++)
        {
            if (i < gridParent.childCount) validSlots.Add(gridParent.GetChild(i));
        }

        validSlots.Sort((a, b) =>
        {
            float distA = GetDistanceToSlot(anchorPos, a.position);
            float distB = GetDistanceToSlot(anchorPos, b.position);
            return distA.CompareTo(distB);
        });

        int maxSlotsToTest = Mathf.Min(4, validSlots.Count);

        for (int i = 0; i < maxSlotsToTest; i++)
        {
            Transform slot = validSlots[i];
            Vector3 testSnapPos = slot.position - pivotToAnchorOffset;

            if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) testSnapPos.y = currentlyDraggedParent.position.y;
            else testSnapPos.z = currentlyDraggedParent.position.z;

            if (!IsOverlappingTray(testSnapPos, false)) return testSnapPos;
        }

        return originalPosition;
    }

    private float GetDistanceToSlot(Vector3 posA, Vector3 posB)
    {
        if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
            return Vector2.Distance(new Vector2(posA.x, posA.z), new Vector2(posB.x, posB.z));
        else
            return Vector2.Distance(new Vector2(posA.x, posA.y), new Vector2(posB.x, posB.y));
    }

    private void UpdateTrayLayers(Transform parent, bool isDragging)
    {
        int targetLayer = LayerMask.NameToLayer(isDragging ? "TraySelected" : "Tray");

        foreach (Transform child in parent)
        {
            child.gameObject.layer = targetLayer;
        }
    }

    #endregion
}