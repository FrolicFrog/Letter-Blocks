using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    public GameObject Panel, Hand;


    public Toggle pHammer, pCleaner;
    public bool showPhysicsGizmos = true;
    public ParticleSystem effect;

    public AudioClip select, deselect, snap;
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
    private bool canAutoRelease = true;
    private Dictionary<Transform, Coroutine> activeJumpRoutines = new Dictionary<Transform, Coroutine>();

    // Dynamically calculated boundaries
    private float bMinX, bMaxX, bMinAxis, bMaxAxis, bTopWallTriggerThreshold;

    // --- COLLISION & JUMP TRACKING ---
    private struct BlockColliderData
    {
        public Vector3 unscaledLocalOffset;
        public Vector3 unscaledExtents;
        public Quaternion rotation;
    }

    private List<BlockColliderData> pieceColliders = new List<BlockColliderData>();
    public static GlobalTrayDragger Instance;
    public bool IsDragging => currentlyDraggedParent != null;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Instance = this;
    }
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

        if (!ResultManager.levelFailed && Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, trayLayer))
        {
            if (hit.transform.parent != null && (hit.transform.name.Contains("Tile letter") || hit.transform.name.Contains("Double Letter")))
            {

               
                currentlyDraggedParent = hit.transform.parent;
            }
            else
            {
                currentlyDraggedParent = hit.transform;
            }
           
            if(pHammer.isOn)
            {
                var ts = currentlyDraggedParent.GetComponent<TraySpliter>();
            
                PowerUpManager.Instance.HammerSmash(pHammer.transform, currentlyDraggedParent.gameObject,()=> { ts.Split(); PowerUpLockManager.Instance.UpdatePowerUpQuantity(7, -1); });
               
                currentlyDraggedParent = null;
                Debug.Log("Hammer Pressed");
                return;
            }
            else if(pCleaner.isOn)
            {
                PowerUpManager.Instance.SuckTrays(currentlyDraggedParent.gameObject,()=> { pCleaner.isOn = false;  PowerUpLockManager.Instance.UpdatePowerUpQuantity(13, -1); });
              
                currentlyDraggedParent = null;
                Debug.Log("Cleaner Pressed");
                return;
            }

            if (currentlyDraggedParent != null)
            {
                // ==== AUDIO PLAYBACK ====
                if (select != null)
                {
                    AudioSource.PlayClipAtPoint(select, currentlyDraggedParent.position);
                    Taptic.Medium();
                }
                // ========================

                isReadyToJump = false;
                lockedSnapValue = 0f;

                if (activeJumpRoutines.ContainsKey(currentlyDraggedParent))
                {
                    if (activeJumpRoutines[currentlyDraggedParent] != null)
                        StopCoroutine(activeJumpRoutines[currentlyDraggedParent]);
                    activeJumpRoutines.Remove(currentlyDraggedParent);
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
            if (Panel.activeSelf)
            {
                Panel.SetActive(false);
                Hand.SetActive(false);
            }
            Vector3 currentMouseWorldPoint = ray.GetPoint(enter);
            Vector3 targetPosition = currentMouseWorldPoint + clickOffset;

            if (ResultManager.Instance != null && !ResultManager.Instance.startTimer)
                ResultManager.Instance.startTimer = true;

            bool isVerticalOnly = currentlyDraggedParent.CompareTag("Vertical");

            if (isVerticalOnly)
            {
                targetPosition.x = originalPosition.x;
            }
            else
            {
                targetPosition.x = Mathf.Clamp(targetPosition.x, bMinX, bMaxX);
            }

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

            bool isTouchingSnapZone = IsInSnapZone(targetStepPos, out Transform snapWall);
            bool pastThreshold = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                                 ? targetStepPos.z >= bTopWallTriggerThreshold
                                 : targetStepPos.y >= bTopWallTriggerThreshold;

            bool shouldSnap = false;
            float proposedSnapValue = bMaxAxis - snapOffset;

            if (isTouchingSnapZone || pastThreshold)
            {
                bool isTryingToEscape = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                                        ? targetPosition.z < proposedSnapValue - (gridCellSize * 0.6f)
                                        : targetPosition.y < proposedSnapValue - (gridCellSize * 0.6f);

                if (!isTryingToEscape)
                {
                    shouldSnap = true;
                }
            }

            if (shouldSnap)
            {
                Vector3 testSnapPos = targetStepPos;

                if (isVerticalOnly)
                {
                    testSnapPos.x = originalPosition.x;
                }
                else
                {
                    testSnapPos.x = GetNearestGridColumnX(testSnapPos.x);
                }

                // Snap the arbitrary proposed value to the actual grid row alignment
                proposedSnapValue = GetNearestGridRowAxis(proposedSnapValue);

                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) testSnapPos.z = proposedSnapValue;
                else testSnapPos.y = proposedSnapValue;

                if (!IsOverlappingTray(testSnapPos, true))
                {
                    // ==== AUDIO PLAYBACK ====
                    // Play snap sound only when it transitions into the ready-to-jump snap state
                    if (!isReadyToJump && snap != null)
                    {
                        AudioSource.PlayClipAtPoint(snap, currentlyDraggedParent.position);
                        Taptic.Medium();
                    }
                    // ========================

                    lockedSnapValue = proposedSnapValue;
                    isReadyToJump = true;
                    targetStepPos.x = testSnapPos.x;
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
        if (Mathf.Abs(diffX) > 0.001f)
        {
            int subStepsX = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(diffX) / 0.2f));
            for (int j = 1; j <= subStepsX; j++)
            {
                testPos.x = currentPos.x + (diffX * ((float)j / subStepsX));
                if (!IsOverlappingTray(testPos, true)) finalPos.x = testPos.x;
                else break;
            }
            testPos = finalPos;
        }

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
                    if (!hit.transform.IsChildOf(currentlyDraggedParent) && hit.transform != currentlyDraggedParent) return true;
                }
            }
            else
            {
                if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) checkCenter.y -= dragOffset;
                else checkCenter.z -= dragOffset;

                Collider[] hits = Physics.OverlapSphere(checkCenter, 0.1f, combinedCollisionLayers);
                foreach (Collider hit in hits)
                {
                    if (!hit.transform.IsChildOf(currentlyDraggedParent) && hit.transform != currentlyDraggedParent) return true;
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
            Vector3 pivotPos = currentlyDraggedParent.position;

            Collider trayCollider = currentlyDraggedParent.GetComponent<Collider>();

            if (trayCollider != null)
            {
                Bounds b = trayCollider.bounds;
                float step = gridCellSize;
                float scannerHalfStep = step * 0.5f;

                int gridX = Mathf.Max(1, Mathf.RoundToInt(b.size.x / step));
                int gridAxis = Mathf.Max(1, Mathf.RoundToInt((planeMode == PlaneAxisMode.XZ_GroundPlane_3D ? b.size.z : b.size.y) / step));

                float startX = b.center.x - (gridX * scannerHalfStep) + scannerHalfStep;
                float startAxis = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D ? b.center.z : b.center.y) - (gridAxis * scannerHalfStep) + scannerHalfStep;

                for (int x = 0; x < gridX; x++)
                {
                    for (int a = 0; a < gridAxis; a++)
                    {
                        float probeX = startX + (x * step);
                        float probeAxis = startAxis + (a * step);

                        Ray ray;
                        if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                        {
                            ray = new Ray(new Vector3(probeX, b.max.y + 1f, probeAxis), Vector3.down);
                        }
                        else
                        {
                            ray = new Ray(new Vector3(probeX, probeAxis, b.min.z - 1f), Vector3.forward);
                        }

                        if (trayCollider.Raycast(ray, out RaycastHit hit, 5f))
                        {
                            BlockColliderData blockData = new BlockColliderData();
                            Vector3 centerBox;

                            if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                            {
                                centerBox = new Vector3(probeX, currentlyDraggedParent.position.y, probeAxis);
                                blockData.unscaledExtents = new Vector3(scannerHalfStep, 0.25f, scannerHalfStep);
                            }
                            else
                            {
                                centerBox = new Vector3(probeX, probeAxis, currentlyDraggedParent.position.z);
                                blockData.unscaledExtents = new Vector3(scannerHalfStep, scannerHalfStep, 0.25f);
                            }

                            blockData.unscaledLocalOffset = centerBox - pivotPos;
                            blockData.rotation = currentlyDraggedParent.rotation;
                            pieceColliders.Add(blockData);
                        }
                    }
                }
            }

            float extLeft = 0f, extRight = 0f, extBottom = 0f, extTop = 0f;
            float halfCell = gridCellSize * 0.5f;

            if (pieceColliders.Count > 0)
            {
                float minLocalX = float.MaxValue, maxLocalX = float.MinValue;
                float minLocalAxis = float.MaxValue, maxLocalAxis = float.MinValue;

                foreach (var block in pieceColliders)
                {
                    float localX = block.unscaledLocalOffset.x;
                    float localAxis = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? block.unscaledLocalOffset.z : block.unscaledLocalOffset.y;

                    if (localX < minLocalX) minLocalX = localX;
                    if (localX > maxLocalX) maxLocalX = localX;
                    if (localAxis < minLocalAxis) minLocalAxis = localAxis;
                    if (localAxis > maxLocalAxis) maxLocalAxis = localAxis;
                }

                extLeft = -(minLocalX - halfCell);
                extRight = (maxLocalX + halfCell);
                extBottom = -(minLocalAxis - halfCell);
                extTop = (maxLocalAxis + halfCell);
            }
            else
            {
                Renderer[] renderers = currentlyDraggedParent.GetComponentsInChildren<Renderer>();
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

            bool jumpedAny = false;
            List<Transform> availableTiles = new List<Transform>();

            // Robust check: Collect any valid piece blocks (ignoring arrows, borders, etc.)
            foreach (Transform child in activeTray)
            {
                if (child.name == "JumpingTile") continue;
                if (child.GetComponentInChildren<TextMeshPro>(true) != null)
                {
                    availableTiles.Add(child);
                }
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
                            bool foundMatch = false;

                            foreach (Transform tile in availableTiles)
                            {
                                Transform nestedTile = tile.Find("Tile letter");
                                Transform text0 = tile.Find("Text 0");

                                // Check if it's the Double Letter block (by structural hierarchy)
                                if (nestedTile != null || text0 != null)
                                {
                                    if (nestedTile != null && nestedTile.parent == tile)
                                    {
                                        // Top layer is still attached
                                        var tm1 = nestedTile.GetComponentInChildren<TextMeshPro>(true);
                                        if (tm1 != null && tm1.text == neededLetter)
                                        {
                                            bestTileToJump = nestedTile; // Jump the inner tile layer
                                            if (text0 != null) text0.gameObject.SetActive(true); // Enable bottom text
                                            foundMatch = true;
                                        }
                                    }
                                    else if (text0 != null && text0.gameObject.activeSelf)
                                    {
                                        // Inner tile is gone, check the base block
                                        var tm0 = text0.GetComponent<TextMeshPro>();
                                        if (tm0 != null && tm0.text == neededLetter)
                                        {
                                            bestTileToJump = tile; // Jump the remaining double letter base block
                                            foundMatch = true;
                                        }
                                    }
                                }
                                else // Standard Tile letter behavior
                                {
                                    var textMesh = tile.GetComponentInChildren<TextMeshPro>(true);
                                    if (textMesh != null && textMesh.text == neededLetter)
                                    {
                                        bestTileToJump = tile;
                                        foundMatch = true;
                                    }
                                }

                                if (foundMatch)
                                {
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

                // Check if we are jumping a full tray base block or just extracting a nested top letter
                bool isTrayLevelChild = bestTileToJump.parent == activeTray;

                Vector3 jumpedLocalPos = isTrayLevelChild ? bestTileToJump.localPosition : bestTileToJump.parent.localPosition;
                Vector3 jumpedLocalScale = isTrayLevelChild ? bestTileToJump.localScale : bestTileToJump.parent.localScale;

                WordChecker.instance.AnimateTrayBlockToGrid(bestTileToJump, bestSlotTransform, bestMatchedKey);

                // Only cascade and shift blocks if a root-level base block left the tray completely
                if (isTrayLevelChild)
                {
                    ShiftTilesUp(activeTray, jumpedLocalPos, jumpedLocalScale);
                }

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

    /// <summary>
    /// Scans the tray for remaining tiles in the same column as the jumping tile and pushes them forward/up
    /// into the exact local position and scale of the tile that sat before them.
    /// </summary>
    private void ShiftTilesUp(Transform tray, Vector3 jumpedTileLocalPos, Vector3 jumpedTileLocalScale)
    {
        float colTolerance = gridCellSize * 0.4f; // More forgiving tolerance for columns offsets
        float rowTolerance = 0.05f; // Strict tolerance for row height
        float shiftDuration = Mathf.Max(0.05f, staggeredJumpDelay - 0.02f);

        List<Transform> tilesToShift = new List<Transform>();

        foreach (Transform child in tray)
        {
            if (child.name == "JumpingTile") continue;

            // Only push blocks that actually have Text (ignores tray walls/arrows)
            if (child.GetComponentInChildren<TextMeshPro>(true) == null) continue;

            Vector3 currentLocalPos = child.localPosition;

            // Ensure they are strictly in the same column
            if (Mathf.Abs(currentLocalPos.x - jumpedTileLocalPos.x) < colTolerance)
            {
                bool isBelow = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                    ? (currentLocalPos.z < jumpedTileLocalPos.z - rowTolerance)
                    : (currentLocalPos.y < jumpedTileLocalPos.y - rowTolerance);

                if (isBelow)
                {
                    tilesToShift.Add(child);
                }
            }
        }

        // Sort tiles from top (closest to the jumped tile) to bottom
        tilesToShift.Sort((a, b) =>
        {
            float posA = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? a.localPosition.z : a.localPosition.y;
            float posB = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? b.localPosition.z : b.localPosition.y;
            return posB.CompareTo(posA);
        });

        Vector3 nextTargetPos = jumpedTileLocalPos;
        Vector3 nextTargetScale = jumpedTileLocalScale;

        foreach (Transform child in tilesToShift)
        {
            Vector3 previousChildPos = child.localPosition;
            Vector3 previousChildScale = child.localScale;

            child.DOKill();

            // Slide smoothly to the exact local position and scale of the tile that sat before it
            child.DOLocalMove(nextTargetPos, shiftDuration).SetEase(Ease.OutQuad);
            child.DOScale(nextTargetScale, shiftDuration).SetEase(Ease.OutQuad);

            // Cascade target position and scale for subsequent tiles in the same column
            nextTargetPos = previousChildPos;
            nextTargetScale = previousChildScale;
        }
    }

    private bool CheckAndDestroyEmptyTray(Transform tray)
    {
        if (tray == null) return true;

        int totalRemaining = 0;
        foreach (Transform child in tray)
        {
            if (child.name == "JumpingTile") continue;

            if (child.GetComponentInChildren<TextMeshPro>(true) != null)
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

            // Immediately disable the collider so the user can't grab it while it's waiting to collapse
            Collider col = tray.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            tray.DOKill();

            // Fetch the jump duration from WordChecker to perfectly time the delay
            float waitToCollapseTime = 0f;
            if (WordChecker.instance != null)
            {
                waitToCollapseTime = WordChecker.instance.trayJumpDuration;
            }

            // Add SetDelay() so the tray waits for the final cube to land before vanishing
            tray.DOScale(Vector3.zero, snapBackDuration).SetDelay(waitToCollapseTime).SetEase(Ease.InBack).OnComplete(() =>
            {
                if (tray != null)
                {
                    // ==========================================
                    // Instantiate effect at the center
                    // ==========================================
                    if (effect != null)
                    {
                        ParticleSystem spawnedEffect = Instantiate(effect, tray.position, Quaternion.identity);
                        spawnedEffect.Play();

                        // Destroy the particle system after it finishes playing
                        Destroy(spawnedEffect.gameObject, spawnedEffect.main.duration + spawnedEffect.main.startLifetime.constantMax);
                    }
                    // ==========================================

                    Destroy(tray.gameObject);
                }
            });

            return true;
        }

        return false;
    }
    #endregion

    #region Release & Snapback

    private void ReleaseAndSnapBack()
    {
        if (currentlyDraggedParent == null) return;

        // ==== AUDIO PLAYBACK ====
        if (deselect != null)
        {
            AudioSource.PlayClipAtPoint(deselect, currentlyDraggedParent.position);
        }
        // ========================

        if (isReadyToJump)
        {
            Transform trayToJump = currentlyDraggedParent;

            float perfectX = GetNearestGridColumnX(trayToJump.position.x);
            Vector3 targetSnapPos = trayToJump.position;
            targetSnapPos.x = perfectX;

            if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
            {
                targetSnapPos.y = originalPosition.y;
                targetSnapPos.z = lockedSnapValue;
            }
            else
            {
                targetSnapPos.z = originalPosition.z;
                targetSnapPos.y = lockedSnapValue;
            }

            currentlyDraggedParent = null;
            isReadyToJump = false;

            trayToJump.DOMove(targetSnapPos, snapBackDuration).SetEase(snapBackEase);
            trayToJump.DOScale(originalScale, snapBackDuration).SetEase(snapBackEase);
            UpdateTrayLayers(trayToJump, false);

            TriggerJumpLogic(trayToJump);
            return;
        }

        if (CheckAndDestroyEmptyTray(currentlyDraggedParent)) return;

        Vector3 finalTargetPos = originalPosition;

        if (BottomGridManager.Instance != null && pieceColliders.Count > 0)
        {
            currentlyDraggedParent.localScale = originalScale;
            finalTargetPos = GetForgivingSnapPosition();
            currentlyDraggedParent.localScale = new Vector3(originalScale.x, originalScale.y * dragScaleMultiplier, originalScale.z);
        }

        if (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) finalTargetPos.y = originalPosition.y;
        else finalTargetPos.z = originalPosition.z;

        // ==============================================================
        // UPDATE TRAY SPLITTER GRID COORDINATES
        // ==============================================================
        TraySpliter spliter = currentlyDraggedParent.GetComponent<TraySpliter>();
        if (spliter != null && gridCellSize > 0.001f)
        {
            int rowSign = 1;
            if (BottomGridManager.Instance != null && BottomGridManager.Instance.width > 0)
            {
                int w = BottomGridManager.Instance.width;
                Transform gridT = BottomGridManager.Instance.transform;
                if (gridT.childCount > w)
                {
                    float z0 = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? gridT.GetChild(0).position.z : gridT.GetChild(0).position.y;
                    float z1 = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? gridT.GetChild(w).position.z : gridT.GetChild(w).position.y;
                    rowSign = (z1 > z0) ? 1 : -1;
                }
            }

            float startAxis = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? originalPosition.z : originalPosition.y;
            float finalAxis = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D) ? finalTargetPos.z : finalTargetPos.y;

            int deltaCol = Mathf.RoundToInt((finalTargetPos.x - originalPosition.x) / gridCellSize);
            int deltaRow = Mathf.RoundToInt((finalAxis - startAxis) / gridCellSize) * rowSign;

          //  spliter.UpdateGridPosition(deltaRow, deltaCol);
        }
        // ==============================================================

        currentlyDraggedParent.DOMove(finalTargetPos, snapBackDuration).SetEase(snapBackEase);
        currentlyDraggedParent.DOScale(originalScale, snapBackDuration).SetEase(snapBackEase);

        UpdateTrayLayers(currentlyDraggedParent, false);
        currentlyDraggedParent = null;
    }

    private float GetNearestGridColumnX(float currentX)
    {
        if (BottomGridManager.Instance == null || currentlyDraggedParent == null || pieceColliders.Count == 0)
            return currentX;

        Transform gridParent = BottomGridManager.Instance.transform;
        if (gridParent.childCount == 0) return currentX;

        // FIX: Use the true mathematical center from our physics scan instead of visually shifted letter child.
        float anchorOffsetX = pieceColliders[0].unscaledLocalOffset.x;

        float nearestX = currentX;
        float minDiff = float.MaxValue;

        int totalTrueSlots = BottomGridManager.Instance.width * BottomGridManager.Instance.height;
        int slotCount = Mathf.Min(totalTrueSlots, gridParent.childCount);

        for (int i = 0; i < slotCount; i++)
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

    private float GetNearestGridRowAxis(float currentAxis)
    {
        if (BottomGridManager.Instance == null || currentlyDraggedParent == null || pieceColliders.Count == 0)
            return currentAxis;

        Transform gridParent = BottomGridManager.Instance.transform;
        if (gridParent.childCount == 0) return currentAxis;

        // FIX: Use the true mathematical center from our physics scan instead of visually shifted letter child.
        float anchorOffsetAxis = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
            ? pieceColliders[0].unscaledLocalOffset.z
            : pieceColliders[0].unscaledLocalOffset.y;

        float nearestAxis = currentAxis;
        float minDiff = float.MaxValue;

        int totalTrueSlots = BottomGridManager.Instance.width * BottomGridManager.Instance.height;
        int slotCount = Mathf.Min(totalTrueSlots, gridParent.childCount);

        for (int i = 0; i < slotCount; i++)
        {
            float slotAxis = (planeMode == PlaneAxisMode.XZ_GroundPlane_3D)
                ? gridParent.GetChild(i).position.z
                : gridParent.GetChild(i).position.y;

            float proposedAxis = slotAxis - anchorOffsetAxis;
            float diff = Mathf.Abs(proposedAxis - currentAxis);

            if (diff < minDiff)
            {
                minDiff = diff;
                nearestAxis = proposedAxis;
            }
        }

        return nearestAxis;
    }

    private Vector3 GetForgivingSnapPosition()
    {
        Transform gridParent = BottomGridManager.Instance.transform;

        // FIX: Replaced the visual anchor with the exact mathematical offset calculated in CalculateDynamicBoundaries
        Vector3 anchorLocalOffset = pieceColliders[0].unscaledLocalOffset;
        Vector3 anchorWorldPos = currentlyDraggedParent.position + anchorLocalOffset;

        List<Transform> validSlots = new List<Transform>();

        int totalTrueSlots = BottomGridManager.Instance.width * BottomGridManager.Instance.height;
        for (int i = 0; i < totalTrueSlots; i++)
        {
            if (i < gridParent.childCount) validSlots.Add(gridParent.GetChild(i));
        }

        validSlots.Sort((a, b) =>
        {
            float distA = GetDistanceToSlot(anchorWorldPos, a.position);
            float distB = GetDistanceToSlot(anchorWorldPos, b.position);
            return distA.CompareTo(distB);
        });

        int maxSlotsToTest = Mathf.Min(4, validSlots.Count);

        for (int i = 0; i < maxSlotsToTest; i++)
        {
            Transform slot = validSlots[i];
            Vector3 testSnapPos = slot.position - anchorLocalOffset;

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

        parent.gameObject.layer = targetLayer;
    }

    #endregion
}