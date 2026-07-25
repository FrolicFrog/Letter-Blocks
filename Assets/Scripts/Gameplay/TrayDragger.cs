using UnityEngine;
using DG.Tweening; // Ensure DOTween is imported in your project!

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

    [Header("Height / Depth Offset Settings")]
    [Tooltip("Offset applied along the locked axis while dragging. Set to 0 if you want the piece to stay flat on the board.")]
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
    private Vector3 originalScale; // Cached original scale
    private Vector3 clickOffset;
    private Plane dragPlane;
    private float lockedAxisValue;

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
        // 1. Mouse Down: Lock target height, store scale, scale up object
        if (Input.GetMouseButtonDown(0))
        {
            TrySelectTray();
        }

        // 2. Mouse Hold: Maintain 1:1 exact cursor tracking along the plane
        if (currentlyDraggedParent != null && Input.GetMouseButton(0))
        {
            DragSelectedParent();
        }

        // 3. Mouse Up: Smoothly animate back position AND scale using DOTween
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

                // Stop any running DOTween animations on position and scale
                currentlyDraggedParent.DOKill();

                // Enable the last child of each child object
                ToggleLastChildren(currentlyDraggedParent, true);

                // Store exact resting position and scale on the board
                originalPosition = currentlyDraggedParent.position;
                originalScale = currentlyDraggedParent.localScale;

                Vector3 elevatedStartPos = originalPosition;

                // Setup drag plane based on chosen orientation
                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                {
                    lockedAxisValue = originalPosition.y + dragOffset;
                    elevatedStartPos.y = lockedAxisValue;
                    dragPlane = new Plane(Vector3.up, new Vector3(0, lockedAxisValue, 0));
                }
                else // XY_FrontalPlane_2D
                {
                    lockedAxisValue = originalPosition.z + dragOffset;
                    elevatedStartPos.z = lockedAxisValue;
                    dragPlane = new Plane(Vector3.back, new Vector3(0, 0, lockedAxisValue));
                }

                // Immediately move the parent to the elevated plane on Frame 0
                currentlyDraggedParent.position = elevatedStartPos;

                // Scale up the object smoothly when picked up
                Vector3 targetScale = originalScale * dragScaleMultiplier;
                currentlyDraggedParent.DOScale(targetScale, scaleUpDuration).SetEase(Ease.OutQuad);

                // Calculate exact click offset on the drag plane
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

            // Enforce strict constant height on the locked axis
            if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
            {
                targetPosition.y = lockedAxisValue;
            }
            else
            {
                targetPosition.z = lockedAxisValue;
            }

            currentlyDraggedParent.position = targetPosition;
        }
    }

    private void ReleaseAndSnapBack()
    {
        // Animate position back to original
        currentlyDraggedParent.DOMove(originalPosition, snapBackDuration)
            .SetEase(snapBackEase);

        // Animate scale back to original
        currentlyDraggedParent.DOScale(originalScale, snapBackDuration)
            .SetEase(snapBackEase);

        // Disable the last child of each child object
        ToggleLastChildren(currentlyDraggedParent, false);

        currentlyDraggedParent = null;
    }

    /// <summary>
    /// Loops through every direct child of the dragged parent and toggles the active state of its last sub-child.
    /// </summary>
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