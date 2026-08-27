using UnityEngine;
using DG.Tweening;

public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance { get; private set; }

    [Header("References")]
    public GameObject hammerPrefab;

    [Header("Animation Settings")]
    public float flyDuration = 0.4f;
    public float smashDuration = 0.15f;
    public float returnDuration = 0.3f;

    [Tooltip("How high above the block the hammer hovers.")]
    public float hoverHeight = 2.0f;

    [Tooltip("Matches flightElevationOffset from WordChecker to pull it closer to the camera.")]
    public float depthOffset = 5.0f;

    public float scaleMultiplier = 1.0f;

    [Header("Rotations")]
    public Vector3 defaultRotation = new Vector3(0, 0, 0);
    public Vector3 windUpRotation = new Vector3(-20, 0, 0);
    public Vector3 smashRotation = new Vector3(90, 0, 0);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void HammerSmash(GameObject uiButton, GameObject trayGameObject)
    {
        if (hammerPrefab == null)
        {
            Debug.LogError("Hammer Prefab is missing!");
            return;
        }

        // --- MATCHING WORDCHECKER'S UI-TO-WORLD MATH ---

        // 1. Get Screen Pos of the UI Button using the Canvas Camera
        Canvas canvas = uiButton.GetComponentInParent<Canvas>();
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        Vector2 buttonScreenPos = RectTransformUtility.WorldToScreenPoint(uiCam, uiButton.transform.position);

        Camera mainCam = Camera.main;
        Vector3 targetPos = trayGameObject.transform.position;

        // 2. Determine appropriate 3D depth based on the Tray's depth (just like WordChecker)
        float trayDepth = mainCam.WorldToScreenPoint(targetPos).z;
        float startDepth = Mathf.Max(0.5f, trayDepth - depthOffset);

        // 3. Project 2D Screen Pos into 3D World Space for the perfect starting location
        Vector3 startWorldPos = mainCam.ScreenToWorldPoint(new Vector3(buttonScreenPos.x, buttonScreenPos.y, startDepth));

        // 4. Setup Hover Position
        Vector3 hoverPos = targetPos + (Vector3.up * hoverHeight);

        // --- ANIMATION ---

        GameObject activeHammer = Instantiate(hammerPrefab, startWorldPos, Quaternion.Euler(defaultRotation));
        Vector3 targetScale = activeHammer.transform.localScale * scaleMultiplier;
        activeHammer.transform.localScale = Vector3.zero;

        Sequence smashSequence = DOTween.Sequence();

        // Fly up & wind up
        smashSequence.Append(activeHammer.transform.DOMove(hoverPos, flyDuration).SetEase(Ease.OutQuad));
        smashSequence.Join(activeHammer.transform.DOScale(targetScale, flyDuration).SetEase(Ease.OutBack));
        smashSequence.Join(activeHammer.transform.DORotate(windUpRotation, flyDuration).SetEase(Ease.OutQuad));

        // Smash down
        smashSequence.Append(activeHammer.transform.DOMove(targetPos, smashDuration).SetEase(Ease.InCubic));
        smashSequence.Join(activeHammer.transform.DORotate(smashRotation, smashDuration).SetEase(Ease.InCubic));

        // Impact callback
        smashSequence.AppendCallback(() =>
        {
            Debug.Log("<color=yellow>Smash Impact!</color>");
            // ADD YOUR SPLIT LOGIC HERE
        });

        smashSequence.AppendInterval(0.15f);

        // Return to the calculated 3D UI position and shrink
        smashSequence.Append(activeHammer.transform.DOMove(startWorldPos, returnDuration).SetEase(Ease.InQuad));
        smashSequence.Join(activeHammer.transform.DORotate(defaultRotation, returnDuration).SetEase(Ease.InQuad));
        smashSequence.Join(activeHammer.transform.DOScale(Vector3.zero, returnDuration).SetEase(Ease.InQuad));

        smashSequence.OnComplete(() =>
        {
            Destroy(activeHammer);
        });
    }
}