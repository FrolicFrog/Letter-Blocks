using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

public class GlobalTrayDragger : MonoBehaviour
{
    public enum PlaneAxisMode
    {
        XZ_GroundPlane_3D, // Standard 3D Board (Lock World Y Height, Drag on X & Z)
        XY_FrontalPlane_2D  // 2D Board (Lock World Z Depth, Drag on X & Y)
    }

    [Header("Raycast Settings")]
    [Tooltip("Target layer for raycasting. Set this to 'Tray' in the Inspector.")]
    public LayerMask trayLayer;

    [Tooltip("Target layer for static blockers. Set this to 'Block' in the Inspector.")]
    public LayerMask blockLayer;

    [Header("Drag Plane Mode")]
    [Tooltip("Select XZ for standard 3D top-down boards, or XY for 2D boards.")]
    public PlaneAxisMode planeMode = PlaneAxisMode.XZ_GroundPlane_3D;

    [Header("Grid Snapping Settings")]
    [Tooltip("Enable step-by-step grid sliding during drag (as seen in reference video).")]
    public bool snapToGridWhileDragging = true;

    [Tooltip("Distance of one grid cell tile. Auto-detected from BottomGridManager if available.")]
    public float gridCellSize = 1.0f;

    [Tooltip("Speed at which the piece slides toward target grid steps while dragging.")]
    public float slideSpeed = 30f;

    [Header("Jump Trigger Settings")]
    [Tooltip("Distance from the top limit to trigger the piece jumping.")]
    public float topWallTriggerOffset = 0.2f;
    [Tooltip("Delay in seconds between each letter jumping off the tray.")]
    public float staggeredJumpDelay = 0.15f;

    [Header("Auto Boundaries from BottomGridManager")]
    [Tooltip("Extra padding to keep pieces slightly away from the exact visual edge.")]
    public float boundaryPadding = 0.05f;

    [Header("Height / Depth Offset Settings")]
    [Tooltip("Offset applied along the locked axis while dragging.")]
    public float dragOffset = 0f;

    [Header("Scale Settings")]
    [Tooltip("Multiplier applied to the scale while dragging (e.g. 1.2 = 20% larger).")]
    public float dragScaleMultiplier = 1.2f;

    [Tooltip("Duration in seconds for the piece to scale up on click.")]
    public float scaleUpDuration = 0.15f;

    [Header("DOTween Snapback Settings")]
    [Tooltip("Duration in seconds for the piece to animate back to its starting position and scale.")]
    public float snapBackDuration = 0.2f;

    [Tooltip("Easing function for the return animation.")]
    public Ease snapBackEase = Ease.OutBack;

    private Camera mainCam;
    private Transform currentlyDraggedParent;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Vector3 clickOffset;
    private Plane dragPlane;
    private float lockedAxisValue;

    // Dynamically calculated boundaries
    private float bMinX, bMaxX, bMinAxis, bMaxAxis, bTopWallTriggerThreshold;
    private bool hasTriggeredTopWall = false;

    // --- COLLISION & JUMP TRACKING ---
    private struct BlockColliderData
    {
        public Vector3 unscaledLocalOffset;
        public Vector3 unscaledExtents;
        public Quaternion rotation;
    }

    private List<BlockColliderData> pieceColliders = new List<BlockColliderData>();

    // Prevents double-queueing if the player wiggles the tray during the stagger delay
    private HashSet<Transform> blocksAlreadyJumping = new HashSet<Transform>();

    private void Start()
    {
        mainCam = Camera.main;

        if (trayLayer == 0) trayLayer = LayerMask.GetMask("Tray");
        if (blockLayer == 0) blockLayer = LayerMask.GetMask("Block");
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) TrySelectTray();
        if (currentlyDraggedParent != null && Input.GetMouseButton(0)) DragSelectedParent();
        if (Input.GetMouseButtonUp(0) && currentlyDraggedParent != null) ReleaseAndSnapBack();
    }

    private void TrySelectTray()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, trayLayer))
        {
            if (hit.transform.parent != null)
            {
                currentlyDraggedParent = hit.transform.parent;
                hasTriggeredTopWall = false;

                // Stop animations
                currentlyDraggedParent.DOKill();
                ToggleLastChildren(currentlyDraggedParent, true);

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

                // Calculate dynamic boundaries & auto-detect grid size
                CalculateDynamicBoundaries();

                // Scale up the object smoothly when picked up for visual feedback
                Vector3 targetScale = originalScale * dragScaleMultiplier;
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

            // Strict mathematical clamping keeps the piece visually inside the board at all times
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
                // Move cell-by-cell along the grid tracks
                targetStepPos = ResolveGridStepMovement(currentlyDraggedParent.position, targetPosition);
            }
            else
            {
                targetStepPos = ResolveTrayCollisions(currentlyDraggedParent.position, targetPosition);
            }

            // Smoothly slide piece toward the target grid step
            currentlyDraggedParent.position = Vector3.Lerp(currentlyDraggedParent.position, targetStepPos, Time.deltaTime * slideSpeed);

            // Check top wall jump trigger
            if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
            {
                if (currentlyDraggedParent.position.z >= bTopWallTriggerThreshold && !hasTriggeredTopWall) TriggerJumpLogic();
                else if (currentlyDraggedParent.position.z < bTopWallTriggerThreshold - 0.1f) hasTriggeredTopWall = false;
            }
            else
            {
                if (currentlyDraggedParent.position.y >= bTopWallTriggerThreshold && !hasTriggeredTopWall) TriggerJumpLogic();
                else if (currentlyDraggedParent.position.y < bTopWallTriggerThreshold - 0.1f) hasTriggeredTopWall = false;
            }
        }
    }

    private Vector3 ResolveGridStepMovement(Vector3 currentPos, Vector3 targetPos)
    {
        Vector3 diff = targetPos - currentPos;

        // --- RESOLVE X AXIS GRID STEPS ---
        if (Mathf.Abs(diff.x) >= gridCellSize * 0.35f)
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

                if (!IsOverlappingTray(nextStep, true))
                {
                    testPos = nextStep;
                }
                else
                {
                    break; // Blocked by wall or tray
                }
            }
            currentPos.x = testPos.x;
        }

        // --- RESOLVE Z or Y AXIS GRID STEPS ---
        if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
        {
            if (Mathf.Abs(diff.z) >= gridCellSize * 0.35f)
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

                    if (!IsOverlappingTray(nextStep, true))
                    {
                        testPos = nextStep;
                    }
                    else
                    {
                        break; // Blocked by wall or tray
                    }
                }
                currentPos.z = testPos.z;
            }
        }
        else
        {
            if (Mathf.Abs(diff.y) >= gridCellSize * 0.35f)
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

                    if (!IsOverlappingTray(nextStep, true))
                    {
                        testPos = nextStep;
                    }
                    else
                    {
                        break; // Blocked by wall or tray
                    }
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

        // Test X Axis Movement
        testPos.x = targetPos.x;
        if (!IsOverlappingTray(testPos, true)) finalPos.x = targetPos.x;

        // Test Z or Y Axis Movement
        testPos = finalPos;
        if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
        {
            testPos.z = targetPos.z;
            if (!IsOverlappingTray(testPos, true)) finalPos.z = targetPos.z;
        }
        else
        {
            testPos.y = targetPos.y;
            if (!IsOverlappingTray(testPos, true)) finalPos.y = targetPos.y;
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
                // 0.90x shrink keeps grid steps clear of false-positive adjacent wall collisions
                Vector3 checkExtents = block.unscaledExtents * 0.90f;

                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                {
                    checkCenter.y -= dragOffset;
                    checkExtents.y += 5f;
                }
                else
                {
                    checkCenter.z -= dragOffset;
                    checkExtents.z += 5f;
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

    private void CalculateDynamicBoundaries()
    {
        bMinX = -10f; bMaxX = 10f;
        bMinAxis = -10f; bMaxAxis = 10f;

        Bounds? boardBounds = null;
        if (BottomGridManager.Instance != null && BottomGridManager.Instance.centerObject != null)
        {
            Renderer borderRenderer = BottomGridManager.Instance.centerObject.GetComponentInChildren<Renderer>();
            if (borderRenderer != null) boardBounds = borderRenderer.bounds;

            // Auto-detect grid cell size from adjacent slots if available
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

    private void TriggerJumpLogic()
    {
        if (currentlyDraggedParent == null || currentlyDraggedParent.childCount == 0) return;
        hasTriggeredTopWall = true;

        Transform activeTray = currentlyDraggedParent;
        List<System.Action> jumpActions = new List<System.Action>();
        int totalLettersInTray = 0;

        // 1. Gather all blocks that qualify to jump
        foreach (Transform wall in activeTray)
        {
            if (wall.childCount != 1)
            {
                totalLettersInTray++; // Treat >1 child as a letter-bearing wall
            }

            if (wall.childCount > 0)
            {
                Transform childToJump = wall.GetChild(0);

                // Skip if null or if it's already in the queue from a previous frame
                if (childToJump == null || blocksAlreadyJumping.Contains(childToJump)) continue;

                var textMesh = childToJump.GetComponentInChildren<TextMeshPro>();
                if (textMesh != null)
                {
                    string letter = textMesh.text;

                    // Instantly find the slot to reserve it, but queue the actual visual jump
                    if (WordChecker.instance.TryFindGridSlotForLetter(letter, out Transform slotTransform, out Vector2Int matchedKey))
                    {
                        blocksAlreadyJumping.Add(childToJump);

                        jumpActions.Add(() =>
                        {
                            if (childToJump != null)
                            {
                                blocksAlreadyJumping.Remove(childToJump);
                                WordChecker.instance.AnimateTrayBlockToGrid(childToJump, slotTransform, matchedKey);
                            }
                        });
                    }
                }
            }
        }

        if (jumpActions.Count == 0) return;

        // 2. Check if the tray will be completely emptied
        bool isFullyCleared = (jumpActions.Count == totalLettersInTray);

        // If the entire tray is jumping, unassign it so the player instantly drops the invisible base
        if (isFullyCleared)
        {
            currentlyDraggedParent = null;
        }

        // 3. Execute staggered jumps over time using DOTween
        for (int i = 0; i < jumpActions.Count; i++)
        {
            int index = i;
            float delay = index * staggeredJumpDelay;

            DOVirtual.DelayedCall(delay, () =>
            {
                if (jumpActions[index] != null) jumpActions[index].Invoke();

                // Destroy the empty tray base only after the final queued piece physically jumps
                if (index == jumpActions.Count - 1 && isFullyCleared && activeTray != null)
                {
                    activeTray.DOScale(Vector3.zero, snapBackDuration).SetEase(Ease.InBack).OnComplete(() =>
                    {
                        if (activeTray != null) Destroy(activeTray.gameObject);
                    });
                }
            });
        }
    }

    private void ReleaseAndSnapBack()
    {
        hasTriggeredTopWall = false;
        Vector3 finalTargetPos = originalPosition;

        if (BottomGridManager.Instance != null && currentlyDraggedParent.childCount > 0)
        {
            currentlyDraggedParent.localScale = originalScale;
            Transform anchorChunk = currentlyDraggedParent.GetChild(0);

            finalTargetPos = GetForgivingSnapPosition(anchorChunk);

            currentlyDraggedParent.localScale = originalScale * dragScaleMultiplier;
        }

        if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) finalTargetPos.y = originalPosition.y;
        else finalTargetPos.z = originalPosition.z;

        currentlyDraggedParent.DOMove(finalTargetPos, snapBackDuration).SetEase(snapBackEase);
        currentlyDraggedParent.DOScale(originalScale, snapBackDuration).SetEase(snapBackEase);

        ToggleLastChildren(currentlyDraggedParent, false);
        currentlyDraggedParent = null;
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

    private void ToggleLastChildren(Transform parent, bool isActive)
    {
        foreach (Transform child in parent)
        {
            if (child.childCount > 0)
            {
                Transform lastSubChild = child.GetChild(child.childCount - 1);
                lastSubChild.gameObject.SetActive(isActive);
            }
        }
    }
}