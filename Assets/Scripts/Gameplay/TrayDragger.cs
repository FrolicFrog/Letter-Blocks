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

    [Header("Drag Plane Mode")]
    [Tooltip("Select XZ for standard 3D top-down boards, or XY for 2D boards.")]
    public PlaneAxisMode planeMode = PlaneAxisMode.XZ_GroundPlane_3D;

    [Header("Auto Boundaries from BottomGridManager")]
    [Tooltip("Extra padding to keep pieces slightly away from the exact visual edge.")]
    public float boundaryPadding = 0.1f;
    [Tooltip("Distance from the top limit to trigger the piece jumping.")]
    public float topWallTriggerOffset = 0.2f;

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

    // --- NEW FOR COLLISION ---
    private struct BlockColliderData
    {
        public Vector3 localOffset;
        public Vector3 halfExtents;
    }
    // Stores the individual bounds of each block in the dragged piece
    private List<BlockColliderData> pieceColliders = new List<BlockColliderData>();
    // -------------------------

    private void Start()
    {
        mainCam = Camera.main;

        if (trayLayer == 0)
        {
            trayLayer = LayerMask.GetMask("Tray");
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TrySelectTray();
        }

        if (currentlyDraggedParent != null && Input.GetMouseButton(0))
        {
            DragSelectedParent();
        }

        if (Input.GetMouseButtonUp(0) && currentlyDraggedParent != null)
        {
            ReleaseAndSnapBack();
        }
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

                // Stop any running DOTween animations on position and scale
                currentlyDraggedParent.DOKill();

                // Enable necessary visuals before calculating bounds
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

                // Dynamically fetch the borders and adjust for the piece's specific width/height
                CalculateDynamicBoundaries();

                // Scale up the object smoothly when picked up
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

            // Clamp X Axis strictly inside padded borders
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

            // --- RESOLVE COLLISIONS ---
            targetPosition = ResolveTrayCollisions(currentlyDraggedParent.position, targetPosition);

            if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
            {
                if (targetPosition.z >= bTopWallTriggerThreshold && !hasTriggeredTopWall)
                {
                    TriggerJumpLogic();
                }
                else if (targetPosition.z < bTopWallTriggerThreshold - 0.1f)
                {
                    hasTriggeredTopWall = false;
                }
            }
            else
            {
                if (targetPosition.y >= bTopWallTriggerThreshold && !hasTriggeredTopWall)
                {
                    TriggerJumpLogic();
                }
                else if (targetPosition.y < bTopWallTriggerThreshold - 0.1f)
                {
                    hasTriggeredTopWall = false;
                }
            }

            // Only update position if it hasn't been destroyed by jumping
            if (currentlyDraggedParent != null)
            {
                currentlyDraggedParent.position = targetPosition;
            }
        }
    }

    private Vector3 ResolveTrayCollisions(Vector3 currentPos, Vector3 targetPos)
    {
        Vector3 finalPos = currentPos;
        Vector3 testPos = currentPos;

        // Test X Axis Movement
        testPos.x = targetPos.x;
        if (!IsOverlappingTray(testPos))
        {
            finalPos.x = targetPos.x;
        }

        // Test Z or Y Axis Movement
        testPos = finalPos; // reset to the validated X
        if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
        {
            testPos.z = targetPos.z;
            if (!IsOverlappingTray(testPos)) finalPos.z = targetPos.z;
        }
        else
        {
            testPos.y = targetPos.y;
            if (!IsOverlappingTray(testPos)) finalPos.y = targetPos.y;
        }

        return finalPos;
    }

    private bool IsOverlappingTray(Vector3 testPos)
    {
        foreach (var block in pieceColliders)
        {
            Vector3 boxCenter = testPos + block.localOffset;
            Vector3 checkExtents = block.halfExtents;

            // Push the test box back down to the ground plane to check against the resting trays
            if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
            {
                boxCenter.y -= dragOffset;
                checkExtents.y += 5f; // Exaggerate thickness to guarantee it intersects resting colliders
            }
            else
            {
                boxCenter.z -= dragOffset;
                checkExtents.z += 5f;
            }

            Collider[] hits = Physics.OverlapBox(boxCenter, checkExtents, Quaternion.identity, trayLayer);

            foreach (Collider hit in hits)
            {
                // If the hit collider is NOT part of the tray we are dragging, it's a collision!
                if (!hit.transform.IsChildOf(currentlyDraggedParent))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void CalculateDynamicBoundaries()
    {
        bMinX = -5f; bMaxX = 5f;
        bMinAxis = -5f; bMaxAxis = 5f;

        if (BottomGridManager.Instance != null && BottomGridManager.Instance.centerObject != null)
        {
            Renderer borderRenderer = BottomGridManager.Instance.centerObject.GetComponentInChildren<Renderer>();

            if (borderRenderer != null)
            {
                Bounds bounds = borderRenderer.bounds;
                bMinX = bounds.min.x;
                bMaxX = bounds.max.x;

                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                {
                    bMinAxis = bounds.min.z;
                    bMaxAxis = bounds.max.z;
                }
                else
                {
                    bMinAxis = bounds.min.y;
                    bMaxAxis = bounds.max.y;
                }
            }
        }

        float pMinX = 0, pMaxX = 0, pMinAxis = 0, pMaxAxis = 0;
        pieceColliders.Clear();

        if (currentlyDraggedParent != null)
        {
            Renderer[] renderers = currentlyDraggedParent.GetComponentsInChildren<Renderer>();
            Collider[] colliders = currentlyDraggedParent.GetComponentsInChildren<Collider>();
            Vector3 pivotPos = currentlyDraggedParent.position;

            foreach (Collider col in colliders)
            {
                if (((1 << col.gameObject.layer) & trayLayer) != 0)
                {
                    BlockColliderData blockData = new BlockColliderData();
                    blockData.localOffset = col.bounds.center - pivotPos;
                    blockData.halfExtents = col.bounds.extents * dragScaleMultiplier * 0.85f;
                    pieceColliders.Add(blockData);
                }
            }

            if (renderers.Length > 0)
            {
                Bounds pieceBounds = renderers[0].bounds;
                foreach (Renderer r in renderers)
                {
                    pieceBounds.Encapsulate(r.bounds);
                }

                pMinX = pivotPos.x - pieceBounds.min.x;
                pMaxX = pieceBounds.max.x - pivotPos.x;

                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                {
                    pMinAxis = pivotPos.z - pieceBounds.min.z;
                    pMaxAxis = pieceBounds.max.z - pivotPos.z;
                }
                else
                {
                    pMinAxis = pivotPos.y - pieceBounds.min.y;
                    pMaxAxis = pieceBounds.max.y - pivotPos.y;
                }
            }
        }

        pMinX *= dragScaleMultiplier;
        pMaxX *= dragScaleMultiplier;
        pMinAxis *= dragScaleMultiplier;
        pMaxAxis *= dragScaleMultiplier;

        bMinX += (pMinX + boundaryPadding);
        bMaxX -= (pMaxX + boundaryPadding);
        bMinAxis += (pMinAxis + boundaryPadding);
        bMaxAxis -= (pMaxAxis + boundaryPadding);

        bTopWallTriggerThreshold = bMaxAxis - topWallTriggerOffset;
    }

    private void TriggerJumpLogic()
    {
        if (currentlyDraggedParent == null || currentlyDraggedParent.childCount == 0) return;

        hasTriggeredTopWall = true;

        // 1. Process all jumping pieces
        foreach (Transform wall in currentlyDraggedParent)
        {
            if (wall.childCount > 0)
            {
                Transform childToJump = wall.GetChild(0);
                if (childToJump == null) continue;

                var textMesh = childToJump.GetComponentInChildren<TextMeshPro>();

                if (textMesh != null)
                {
                    string letter = textMesh.text;
                    if (WordChecker.instance.TryFindGridSlotForLetter(letter, out Transform slotTransform, out Vector2Int matchedKey))
                    {
                        WordChecker.instance.AnimateTrayBlockToGrid(childToJump, slotTransform, matchedKey);
                    }
                }
            }
        }

        // 2. Check the new condition: Does every wall have EXACTLY one child gameobject left?
        bool isTrayEmpty = true;
        foreach (Transform wall in currentlyDraggedParent)
        {
            if (wall.childCount != 1)
            {
                isTrayEmpty = false;
                break; // If any wall has 0 or >1 children, the tray isn't empty yet
            }
        }

        // 3. Scale and destroy if the empty condition is met
        if (isTrayEmpty)
        {
            Transform trayToDestroy = currentlyDraggedParent;
            currentlyDraggedParent = null; // Unassign immediately so we stop tracking it

            trayToDestroy.DOScale(Vector3.zero, snapBackDuration).SetEase(Ease.InBack).OnComplete(() =>
            {
                Destroy(trayToDestroy.gameObject);
            });
        }
    }

    private void ReleaseAndSnapBack()
    {
        hasTriggeredTopWall = false;

        currentlyDraggedParent.DOMove(originalPosition, snapBackDuration)
            .SetEase(snapBackEase);

        currentlyDraggedParent.DOScale(originalScale, snapBackDuration)
            .SetEase(snapBackEase);

        ToggleLastChildren(currentlyDraggedParent, false);
        currentlyDraggedParent = null;
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