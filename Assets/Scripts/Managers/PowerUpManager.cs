using System;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Prefab of the 3D hammer to spawn.")]
    [SerializeField] private GameObject hammerPrefab;

    [Header("WordChecker UI Coordinate Settings")]
    [Tooltip("Matches flightElevationOffset from WordChecker to pull the hammer closer to the camera in front of gameplay objects.")]
    [SerializeField] private float flightElevationOffset = 5.0f;

    [Tooltip("Target scale to punch the UI button when the hammer spawns and when it lands back.")]
    [SerializeField] private Vector3 uiPopScale = new Vector3(1.15f, 1.15f, 1f);

    [Tooltip("Duration of the UI button bounce/pop animation.")]
    [SerializeField] private float uiPopDuration = 0.15f;

    [Header("Flight Timing & Easing")]
    [Tooltip("Flight time from UI button to hover position near the target.")]
    [SerializeField] private float flyInDuration = 0.35f;

    [Tooltip("Easing when flying out from the UI button.")]
    [SerializeField] private Ease flyEase = Ease.OutQuad;

    [Tooltip("Time to pull back and build momentum before smashing.")]
    [SerializeField] private float windUpDuration = 0.16f;

    [Tooltip("Easing for the wind-up pull back.")]
    [SerializeField] private Ease windUpEase = Ease.OutBack;

    [Tooltip("Time taken for the hammer to strike down on the target.")]
    [SerializeField] private float smashDuration = 0.09f;

    [Tooltip("Easing for the downward smash acceleration.")]
    [SerializeField] private Ease smashEase = Ease.InCubic;

    [Tooltip("Brief pause on the target after impact.")]
    [SerializeField] private float impactPauseDuration = 0.08f;

    [Tooltip("Flight time from the target back to the UI button.")]
    [SerializeField] private float returnDuration = 0.35f;

    [Tooltip("Easing when returning and shrinking into the UI button.")]
    [SerializeField] private Ease returnEase = Ease.InQuad;

    [Header("Offsets Relative to Target")]
    [Tooltip("Hover position beside the target tray before winding up.")]
    [SerializeField] private Vector3 hoverOffset = new Vector3(0.5f, 1.2f, -0.4f);

    [Tooltip("Pull-back position where hammer builds swing momentum.")]
    [SerializeField] private Vector3 windUpOffset = new Vector3(0.8f, 2.0f, -0.8f);

    [Tooltip("Hit point on the target tray.")]
    [SerializeField] private Vector3 hitOffset = new Vector3(0f, 0.1f, -0.2f);

    [Header("Rotations (Euler)")]
    [Tooltip("Rotation when originating from / returning to the UI button.")]
    [SerializeField] private Vector3 spawnRotation = new Vector3(-10f, 30f, -15f);

    [Tooltip("Rotation when hovering near the target.")]
    [SerializeField] private Vector3 hoverRotation = new Vector3(-15f, 35f, -10f);

    [Tooltip("Rotation at the peak of the wind-up momentum build.")]
    [SerializeField] private Vector3 windUpRotation = new Vector3(-60f, 45f, -25f);

    [Tooltip("Rotation at the moment of impact.")]
    [SerializeField] private Vector3 smashRotation = new Vector3(65f, 15f, 0f);

    [Header("Impact Feedback / Juice")]
    [Tooltip("Scale multiplier for the active hammer.")]
    [SerializeField] private float scaleMultiplier = 1.0f;

    [Tooltip("Punch scale applied to the target tray on hit.")]
    [SerializeField] private Vector3 targetPunchScale = new Vector3(0.2f, -0.2f, 0.2f);

    [Tooltip("Duration of the target tray squash/stretch.")]
    [SerializeField] private float targetPunchDuration = 0.18f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Launches hammer from UI button, smashes target tray, and flies back into the UI button.
    /// </summary>
    /// <param name="uiTransform">Transform/RectTransform of the UI Button toggle.</param>
    /// <param name="trayGameObject">Target tray or pit GameObject to smash.</param>
    /// <param name="onSmashHit">Callback invoked at the exact moment of impact.</param>
    public void HammerSmash(Transform uiTransform, GameObject trayGameObject, Action onSmashHit = null)
    {
        if (hammerPrefab == null || trayGameObject == null || uiTransform == null)
        {
            Debug.LogError("[PowerUpManager] Missing reference!");
            return;
        }

        // 1. Calculate UI Origin using WordChecker projection logic
        Vector3 targetPos = trayGameObject.transform.position;
        Vector3 uiWorldPos = GetWorldPosFromUI(uiTransform, targetPos);

        // 2. Setup trajectory positions
        Vector3 hoverPos = targetPos + hoverOffset;
        Vector3 windUpPos = targetPos + windUpOffset;
        Vector3 strikePos = targetPos + hitOffset;

        // 3. Spawn hammer at UI button world point with zero scale
        GameObject activeHammer = Instantiate(hammerPrefab, uiWorldPos, Quaternion.Euler(spawnRotation));
        Vector3 targetScale = activeHammer.transform.localScale * scaleMultiplier;
        activeHammer.transform.localScale = Vector3.zero;

        // Trigger UI pop upon takeoff
        PopUIElement(uiTransform);

        // 4. Build Sequence
        Sequence smashSeq = DOTween.Sequence();

        // PHASE 1: Fly out from UI button -> Hover near target
        smashSeq.Append(activeHammer.transform.DOMove(hoverPos, flyInDuration).SetEase(flyEase));
        smashSeq.Join(activeHammer.transform.DOScale(targetScale, flyInDuration).SetEase(Ease.OutBack));
        smashSeq.Join(activeHammer.transform.DORotate(hoverRotation, flyInDuration).SetEase(flyEase));

        // PHASE 2: Wind-Up / Build Momentum
        smashSeq.Append(activeHammer.transform.DOMove(windUpPos, windUpDuration).SetEase(windUpEase));
        smashSeq.Join(activeHammer.transform.DORotate(windUpRotation, windUpDuration).SetEase(windUpEase));

        // PHASE 3: Smash Down
        smashSeq.Append(activeHammer.transform.DOMove(strikePos, smashDuration).SetEase(smashEase));
        smashSeq.Join(activeHammer.transform.DORotate(smashRotation, smashDuration).SetEase(smashEase));

        // PHASE 4: Impact Feedback & Split Callback
        smashSeq.AppendCallback(() =>
        {
            trayGameObject.transform.DOPunchScale(targetPunchScale, targetPunchDuration, 10, 1f);
            activeHammer.transform.DOPunchScale(new Vector3(-0.1f, 0.12f, -0.1f), 0.12f, 6, 0.8f);
            onSmashHit?.Invoke();
        });

        smashSeq.AppendInterval(impactPauseDuration);

        // PHASE 5: Return flight directly into the UI button & shrink to zero
        smashSeq.Append(activeHammer.transform.DOMove(uiWorldPos, returnDuration).SetEase(returnEase));
        smashSeq.Join(activeHammer.transform.DORotate(spawnRotation, returnDuration).SetEase(returnEase));
        smashSeq.Join(activeHammer.transform.DOScale(Vector3.zero, returnDuration).SetEase(Ease.InBack, 1.3f));

        // PHASE 6: UI Impact Pop & Destroy
        smashSeq.OnComplete(() =>
        {
            PopUIElement(uiTransform);
            Destroy(activeHammer);
            uiTransform.GetComponent<Toggle>().isOn = false;
        });
    }

    /// <summary>
    /// Projects a 2D Canvas UI position into 3D World Space using WordChecker's camera projection formula.
    /// </summary>
    private Vector3 GetWorldPosFromUI(Transform uiTransform, Vector3 referenceTargetPos)
    {
        Canvas canvas = uiTransform.GetComponentInParent<Canvas>();
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        Vector2 targetScreenPos = RectTransformUtility.WorldToScreenPoint(uiCam, uiTransform.position);

        Camera cam = Camera.main;
        float distanceToCamera = Mathf.Max(0.5f, cam.WorldToScreenPoint(referenceTargetPos).z - flightElevationOffset);
        return cam.ScreenToWorldPoint(new Vector3(targetScreenPos.x, targetScreenPos.y, distanceToCamera));
    }

    /// <summary>
    /// Applies WordChecker's punch scale feedback to the UI button.
    /// </summary>
    private void PopUIElement(Transform uiTransform)
    {
        if (uiTransform == null) return;
        uiTransform.DOKill(true);
        Vector3 punchStrength = uiPopScale - Vector3.one;
        uiTransform.DOPunchScale(punchStrength, uiPopDuration, 5, 0.3f).SetLink(uiTransform.gameObject);
    }
}