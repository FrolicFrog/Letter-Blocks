using UnityEngine;
using TMPro;
using DG.Tweening;

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
                // Clamp Z Axis strictly inside padded borders
                targetPosition.z = Mathf.Clamp(targetPosition.z, bMinAxis, bMaxAxis);
                targetPosition.y = lockedAxisValue;

                if (targetPosition.z >= bTopWallTriggerThreshold && !hasTriggeredTopWall)
                {
                   // Debug.Log("Jumping");
                    TriggerJumpLogic();
                }
                else if (targetPosition.z < bTopWallTriggerThreshold - 0.1f)
                {
                    hasTriggeredTopWall = false;
                }
            }
            else
            {
                // Clamp Y Axis strictly inside padded borders
                targetPosition.y = Mathf.Clamp(targetPosition.y, bMinAxis, bMaxAxis);
                targetPosition.z = lockedAxisValue;

                if (targetPosition.y >= bTopWallTriggerThreshold && !hasTriggeredTopWall)
                {
                    TriggerJumpLogic();
                }
                else if (targetPosition.y < bTopWallTriggerThreshold - 0.1f)
                {
                    hasTriggeredTopWall = false;
                }
            }

            currentlyDraggedParent.position = targetPosition;
        }
    }

    private void CalculateDynamicBoundaries()
    {
        // 1. Setup Defaults
        bMinX = -5f; bMaxX = 5f;
        bMinAxis = -5f; bMaxAxis = 5f;

        // 2. Read exact World Space Boundaries from the Grid Manager
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

        // 3. Calculate the bounding size (extents) of the piece we just picked up
        float pMinX = 0, pMaxX = 0, pMinAxis = 0, pMaxAxis = 0;

        if (currentlyDraggedParent != null)
        {
            Renderer[] renderers = currentlyDraggedParent.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds pieceBounds = renderers[0].bounds;
                foreach (Renderer r in renderers)
                {
                    pieceBounds.Encapsulate(r.bounds);
                }

                Vector3 pivotPos = currentlyDraggedParent.position;

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

        // 4. Multiply the piece boundaries by the dragScaleMultiplier 
        // to account for the object being larger while it is picked up
        pMinX *= dragScaleMultiplier;
        pMaxX *= dragScaleMultiplier;
        pMinAxis *= dragScaleMultiplier;
        pMaxAxis *= dragScaleMultiplier;

        // 5. Shrink the drag area by the SCALED size of the piece + your visual padding
        bMinX += (pMinX + boundaryPadding);
        bMaxX -= (pMaxX + boundaryPadding);
        bMinAxis += (pMinAxis + boundaryPadding);
        bMaxAxis -= (pMaxAxis + boundaryPadding);

        // 6. Establish the threshold based on the newly clamped upper limit
        bTopWallTriggerThreshold = bMaxAxis - topWallTriggerOffset;
    }

    private void TriggerJumpLogic()
    {
        if (currentlyDraggedParent.childCount == 0) return;

        hasTriggeredTopWall = true;

        int piecesJumpedCount = 0;
        int totalValidPieces = 0;

        // Loop through all children of the tray (the "Walls")
        foreach (Transform wall in currentlyDraggedParent)
        {
            if (wall.childCount > 0)
            {
                totalValidPieces++;

                // Grab the first child of the wall (the "Tile letter(Clone)")
                Transform childToJump = wall.GetChild(0);

                if (childToJump == null) continue;

                // TMP_Text safely covers both 3D TextMeshPro and UI TextMeshProUGUI
                var textMesh = childToJump.GetComponentInChildren<TextMeshPro>();

                if (textMesh != null)
                {
                    // Trim invisible characters like zero-width spaces and ensure casing matches WordChecker data
                    string letter = textMesh.text;
                  //  Debug.Log(letter);
                    // Let WordChecker check if there's an open slot for this letter
                    if (WordChecker.instance.TryFindGridSlotForLetter(letter, out Transform slotTransform, out Vector2Int matchedKey))
                    {
                        // Reparent and animate the specific tile
                        WordChecker.instance.AnimateTrayBlockToGrid(childToJump, slotTransform, matchedKey);
                        piecesJumpedCount++;
                    }
                }
            }
        }

        // If ALL the blocks jumped, destroy the empty shell and stop dragging
        if (piecesJumpedCount > 0 && piecesJumpedCount == totalValidPieces)
        {
            Destroy(currentlyDraggedParent.gameObject);
            currentlyDraggedParent = null;
        }
    }

    private void ReleaseAndSnapBack()
    {
        hasTriggeredTopWall = false;

        // Animate position back to original
        currentlyDraggedParent.DOMove(originalPosition, snapBackDuration)
            .SetEase(snapBackEase);

        // Animate scale back to original
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