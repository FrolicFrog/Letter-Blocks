using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance { get; private set; }

    [Header("<color=#90EE90>--- General & UI Settings ---</color>")]
    [Tooltip("Matches flightElevationOffset from WordChecker to pull the hammer closer to the camera in front of gameplay objects.")]
    [SerializeField] private float flightElevationOffset = 5.0f;

    [Tooltip("Target scale to punch the UI button when the hammer spawns and when it lands back.")]
    [SerializeField] private Vector3 uiPopScale = new Vector3(1.15f, 1.15f, 1f);

    [Tooltip("Duration of the UI button bounce/pop animation.")]
    [SerializeField] private float uiPopDuration = 0.15f;


    [Header("<color=#FFA500>--- Hammer References & Settings ---</color>")]
    [Tooltip("Prefab of the 3D hammer to spawn.")]
    [SerializeField] private GameObject hammerPrefab;

    [SerializeField] private float flyInDuration = 0.35f;
    [SerializeField] private Ease flyEase = Ease.OutQuad;
    [SerializeField] private float windUpDuration = 0.16f;
    [SerializeField] private Ease windUpEase = Ease.OutBack;
    [SerializeField] private float smashDuration = 0.09f;
    [SerializeField] private Ease smashEase = Ease.InCubic;
    [SerializeField] private float impactPauseDuration = 0.08f;
    [SerializeField] private float returnDuration = 0.35f;
    [SerializeField] private Ease returnEase = Ease.InQuad;

    [Space]
    [SerializeField] private Vector3 hoverOffset = new Vector3(0.5f, 1.2f, -0.4f);
    [SerializeField] private Vector3 windUpOffset = new Vector3(0.8f, 2.0f, -0.8f);
    [SerializeField] private Vector3 hitOffset = new Vector3(0f, 0.1f, -0.2f);

    [Space]
    [SerializeField] private Vector3 spawnRotation = new Vector3(-10f, 30f, -15f);
    [SerializeField] private Vector3 hoverRotation = new Vector3(-15f, 35f, -10f);
    [SerializeField] private Vector3 windUpRotation = new Vector3(-60f, 45f, -25f);
    [SerializeField] private Vector3 smashRotation = new Vector3(65f, 15f, 0f);

    [Space]
    [SerializeField] private float scaleMultiplier = 1.0f;
    [SerializeField] private Vector3 targetPunchScale = new Vector3(0.2f, -0.2f, 0.2f);
    [SerializeField] private float targetPunchDuration = 0.18f;

    [Header("<color=#FF1493>--- Exaggerated Smash Scale Settings ---</color>")]
    [Tooltip("The overall base size multiplier of the hammer.")]
    [SerializeField] private float baseHammerSize = 1.0f;

    [Tooltip("How massive the hammer gets during the hang-time/wind-up phase.")]
    [SerializeField] private float windUpSizeMultiplier = 3.5f;

    [Tooltip("How flat the hammer squashes on the Y-axis upon impact.")]
    [SerializeField] private float squashYMultiplier = 0.4f;

    [Tooltip("How wide the hammer stretches on the X and Z axes upon impact.")]
    [SerializeField] private float stretchXZMultiplier = 4.5f;

    [Header("<color=#00FFFF>--- Cleaner References & Settings ---</color>")]
    [Tooltip("Prefab of the Vacuum Cleaner to spawn.")]
    [SerializeField] private GameObject cleanerPrefab;

    [Tooltip("Position offset relative to the tray when spawning the cleaner.")]
    [SerializeField] private Vector3 cleanerSpawnOffset = new Vector3(0f, 2.5f, -0.5f);

    [Tooltip("Initial rotation (Euler) of the cleaner when it spawns.")]
    [SerializeField] private Vector3 cleanerSpawnRotation = new Vector3(0f, 0f, 0f);

    [Tooltip("How many full 360-degree spins the cleaner does when spawning and leaving.")]
    [SerializeField] private int cleanerSpinCount = 1;

    [Tooltip("How high the items jump into the air before getting sucked in.")]
    [SerializeField] private float suckJumpPower = 1.5f;

    [SerializeField] private float cleanerSpawnDuration = 0.6f;
    [SerializeField] private float vacuumSuckDuration = 0.5f;
    [SerializeField] private float staggerDelayPerChild = 0.08f;

    public LayerMask frozenLayer;

    // --- Active State Flags ---
    private bool isHammerActive = false;
    private bool isCleanerActive = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (!ResultManager.levelFailed && Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, frozenLayer))
            {
                if (hit.transform != null)
                {
                    GlobalTrayDragger.Instance.hammerOptions.gameObject.SetActive(false);
                    var currentTray = hit.transform;
                    GlobalTrayDragger.Instance.pointer.SetActive(false);
                    if (GlobalTrayDragger.Instance.pHammer.isOn)
                    {
                       HammerSmash(GlobalTrayDragger.Instance.pHammer.transform, currentTray.gameObject, () => { PowerUpLockManager.Instance.UpdatePowerUpQuantity(7, -1);
                           FreezeManager.DecreaseFreezeCount(currentTray.GetComponent<FreezeManager>());
                          
                       });
                    }
                }
                
            }
        }
    }
    public void HammerSmash(Transform uiTransform, GameObject trayGameObject, Action onSmashHit = null)
    {
        // 1. Check if hammer is already active to prevent duplicates
        if (isHammerActive) return;

        if (hammerPrefab == null || trayGameObject == null || uiTransform == null)
        {
            Debug.LogError("[PowerUpManager] Missing reference!");
            return;
        }

        // 2. Lock the hammer state
        isHammerActive = true;

        Vector3 targetPos = trayGameObject.transform.position;
        Vector3 uiWorldPos = GetWorldPosFromUI(uiTransform, targetPos);

        // EXAGGERATED OFFSETS: Pull it way higher and further back for a huge swing
        Vector3 hoverPos = targetPos + hoverOffset;
        Vector3 windUpPos = targetPos + windUpOffset + new Vector3(0, 3.5f, -1.5f);
        Vector3 strikePos = targetPos + hitOffset;

        GameObject activeHammer = Instantiate(hammerPrefab, uiWorldPos, Quaternion.Euler(spawnRotation));

        // Apply your new base size control
        Vector3 baseScale = activeHammer.transform.localScale * scaleMultiplier * baseHammerSize;
        activeHammer.transform.localScale = Vector3.zero;

        PopUIElement(uiTransform);

        Sequence smashSeq = DOTween.Sequence();

        // --- PHASE 1: FLY IN (With a crazy 360 spin and elastic pop) ---
        smashSeq.Append(activeHammer.transform.DOMove(hoverPos, flyInDuration).SetEase(Ease.OutBack, 1.5f));
        smashSeq.Join(activeHammer.transform.DOScale(baseScale * 1.5f, flyInDuration).SetEase(Ease.OutElastic, 1.2f));
        smashSeq.Join(activeHammer.transform.DORotate(hoverRotation + new Vector3(0, 360f, 0), flyInDuration, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));

        // --- PHASE 2: WIND UP & HANG TIME (Anticipation) ---
        // Grow based on your windUpSizeMultiplier and tilt way back
        smashSeq.Append(activeHammer.transform.DOMove(windUpPos, windUpDuration * 1.5f).SetEase(Ease.OutSine));
        smashSeq.Join(activeHammer.transform.DORotate(windUpRotation + new Vector3(-45f, 0, 0), windUpDuration * 1.5f).SetEase(Ease.OutBack));
        smashSeq.Join(activeHammer.transform.DOScale(baseScale * windUpSizeMultiplier, windUpDuration * 1.5f).SetEase(Ease.OutCubic));

        // Add a tiny dramatic pause at the absolute peak of the swing
        smashSeq.AppendInterval(0.12f);

        // --- PHASE 3: THE SMASH (Violent speed + Squash and Stretch) ---
        float hyperSmashSpeed = smashDuration * 0.7f; // Faster than normal
        smashSeq.Append(activeHammer.transform.DOMove(strikePos, hyperSmashSpeed).SetEase(Ease.InExpo));
        smashSeq.Join(activeHammer.transform.DORotate(smashRotation + new Vector3(25f, 0, 0), hyperSmashSpeed).SetEase(Ease.InExpo));

        // Squash flat and stretch wide using your custom multipliers
        Vector3 squashStretchScale = new Vector3(
            baseScale.x * stretchXZMultiplier,
            baseScale.y * squashYMultiplier,
            baseScale.z * stretchXZMultiplier
        );
        smashSeq.Join(activeHammer.transform.DOScale(squashStretchScale, hyperSmashSpeed).SetEase(Ease.InExpo));

        // --- PHASE 4: THE IMPACT JUICE ---
        smashSeq.AppendCallback(() =>
        {
            // Massive tray punch
            if (trayGameObject != null)
                trayGameObject.transform.DOPunchScale(targetPunchScale * 2.5f, targetPunchDuration * 1.5f, 15, 1f);

            // Snap the hammer back from its squashed state into a wobble (slightly bigger than base scale for emphasis)
            if (activeHammer != null)
            {
                activeHammer.transform.DOScale(baseScale * 1.2f, 0.2f).SetEase(Ease.OutElastic, 2f);
                activeHammer.transform.DOPunchPosition(new Vector3(0, 0.6f, 0), 0.2f, 20, 1f);
            }

            // EXAGGERATION: Add Camera Shake! (Requires Camera.main to be tagged "MainCamera")
            if (Camera.main != null)
            {
                Camera.main.transform.DOShakePosition(0.25f, new Vector3(0.3f, 0.3f, 0f), 25, 90f, false, true);
            }
        });

        // 4. WAIT for the tray punch animation to complete
        smashSeq.AppendInterval(targetPunchDuration);

        // 5. NOW invoke the hit logic (which will destroy the tray)
        smashSeq.AppendCallback(() =>
        {
            onSmashHit?.Invoke();
        });

        smashSeq.AppendInterval(impactPauseDuration);

        // --- PHASE 5: RETURN (Shrink away quickly) ---
        smashSeq.Append(activeHammer.transform.DOMove(uiWorldPos, returnDuration).SetEase(Ease.InBack));
        smashSeq.Join(activeHammer.transform.DORotate(spawnRotation, returnDuration).SetEase(Ease.InBack));
        smashSeq.Join(activeHammer.transform.DOScale(Vector3.zero, returnDuration).SetEase(Ease.InBack, 1.5f));

        smashSeq.OnComplete(() =>
        {
            PopUIElement(uiTransform);
            Destroy(activeHammer);

            Toggle toggle = uiTransform.GetComponent<Toggle>();
            if (toggle != null) toggle.isOn = false;

            // 6. Unlock the hammer state
            isHammerActive = false;
        });
    }
    public void SuckTrays(GameObject tray, Action onComplete = null)
    {
        // 1. Check if cleaner is already active to prevent duplicates
        if (isCleanerActive) return;

        if (cleanerPrefab == null || tray == null)
        {
            Debug.LogError("[PowerUpManager] Cleaner Prefab or Tray is null!");
            return;
        }

        // 2. Lock the cleaner state
        isCleanerActive = true;

        Vector3 spawnPos = tray.transform.position + cleanerSpawnOffset;
        GameObject activeCleaner = Instantiate(cleanerPrefab, spawnPos, Quaternion.Euler(cleanerSpawnRotation));

        Vector3 cleanerTargetScale = activeCleaner.transform.localScale * scaleMultiplier;

        Transform nozzle = activeCleaner.transform.childCount > 0 ? activeCleaner.transform.GetChild(0) : activeCleaner.transform;
        Vector3 nozzleTargetPos = nozzle.position;

        // --- NEW: Find the grandchild suction effect and disable it initially ---
        GameObject suctionEffect = null;
        if (activeCleaner.transform.childCount > 0 && activeCleaner.transform.GetChild(0).childCount > 0)
        {
            suctionEffect = activeCleaner.transform.GetChild(0).GetChild(0).gameObject;
            suctionEffect.SetActive(false);
        }

        activeCleaner.transform.localScale = Vector3.zero;

        Sequence vacuumSeq = DOTween.Sequence();

        // 1. Cleaner Entrance Animation
        vacuumSeq.Append(activeCleaner.transform.DOScale(cleanerTargetScale, cleanerSpawnDuration).SetEase(Ease.OutBack));
        vacuumSeq.Join(activeCleaner.transform.DORotate(new Vector3(0, 360f * cleanerSpinCount, 0), cleanerSpawnDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.OutQuad)
            .SetRelative(true));

        // --- NEW: Enable the suction effect exactly when the entrance animation finishes ---
        if (suctionEffect != null)
        {
            vacuumSeq.InsertCallback(cleanerSpawnDuration, () => suctionEffect.SetActive(true));
            activeCleaner.GetComponent<AudioSource>().Play();
        }

        // 2. Cache & Extract Children: Splits Double Letters apart so they are vacuumed independently!
        List<Transform> trayChildren = new List<Transform>();
        for (int i = tray.transform.childCount - 1; i >= 0; i--)
        {
            Transform item = tray.transform.GetChild(i);
            trayChildren.Add(item);

            for (int j = item.childCount - 1; j >= 0; j--)
            {
                Transform subChild = item.GetChild(j);

                if (subChild.GetComponent<TMPro.TextMeshPro>() == null && subChild.GetComponentInChildren<TMPro.TextMeshPro>(true) != null)
                {
                    subChild.SetParent(tray.transform, true);
                    trayChildren.Add(subChild);
                }
            }
        }

        float totalVacuumTime = cleanerSpawnDuration;

        // 3. Animate each independent block flying into the nozzle
        for (int i = 0; i < trayChildren.Count; i++)
        {
            Transform item = trayChildren[i];
            float delay = cleanerSpawnDuration + (i * staggerDelayPerChild);
            float pieceReachTime = delay + vacuumSuckDuration;

            Vector3 randomSpin = new Vector3(
                UnityEngine.Random.Range(-360, 360),
                UnityEngine.Random.Range(-360, 360),
                UnityEngine.Random.Range(-360, 360)
            );

            vacuumSeq.Insert(delay, item.DOJump(nozzleTargetPos, suckJumpPower, 1, vacuumSuckDuration).SetEase(Ease.InCubic));
            vacuumSeq.Insert(delay, item.DORotate(randomSpin, vacuumSuckDuration, RotateMode.FastBeyond360).SetEase(Ease.InQuad));
            vacuumSeq.Insert(delay, item.DOScale(Vector3.zero, vacuumSuckDuration).SetEase(Ease.InCirc));

            // Evaluate placement exactly when it hits the nozzle
            vacuumSeq.InsertCallback(pieceReachTime, () =>
            {
                if (item != null)
                {
                    // Spits the key out of the nozzle to the lock (if it exists)
                    ExtractAndFlyKeyFromNozzle(item, nozzleTargetPos);

                    bool placed = false;
                    if (WordChecker.instance != null)
                    {
                        placed = WordChecker.instance.TryPlaceVacuumedPiece(item);

                    }

                    if (!placed) Destroy(item.gameObject);
                }
            });

            totalVacuumTime = Mathf.Max(totalVacuumTime, pieceReachTime);
        }

        // 4. Animate the Tray base itself getting sucked up
        float traySuckDelay = totalVacuumTime;
        Vector3 traySpin = new Vector3(180, 360, 90);

        vacuumSeq.Insert(traySuckDelay, tray.transform.DOJump(nozzleTargetPos, suckJumpPower * 0.75f, 1, vacuumSuckDuration).SetEase(Ease.InCubic));
        vacuumSeq.Insert(traySuckDelay, tray.transform.DORotate(traySpin, vacuumSuckDuration, RotateMode.FastBeyond360).SetEase(Ease.InQuad));
        vacuumSeq.Insert(traySuckDelay, tray.transform.DOScale(Vector3.zero, vacuumSuckDuration).SetEase(Ease.InCirc));

        totalVacuumTime += vacuumSuckDuration;

        vacuumSeq.InsertCallback(totalVacuumTime, () =>
        {
            if (tray != null) Destroy(tray);
        });

        // 5. Cleaner Inverse Exit Animation
        float exitTime = totalVacuumTime + 0.15f;

        // --- NEW: Disable the suction effect exactly as the exit animation starts ---
        if (suctionEffect != null)
        {
            vacuumSeq.InsertCallback(exitTime, () => suctionEffect.SetActive(false));
        }

        vacuumSeq.Insert(exitTime, activeCleaner.transform.DOScale(Vector3.zero, cleanerSpawnDuration).SetEase(Ease.InBack));
        vacuumSeq.Insert(exitTime, activeCleaner.transform.DORotate(new Vector3(0, -360f * cleanerSpinCount, 0), cleanerSpawnDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.InQuad)
            .SetRelative(true));

        vacuumSeq.OnComplete(() =>
        {
            Destroy(activeCleaner);
            onComplete?.Invoke();

            // 3. Unlock the cleaner state so it can be used again
            isCleanerActive = false;
        });
    }
    

    /// <summary>
    /// Looks for a Key child, unparents it exactly at the nozzle, and flies it to the lock using WordChecker's timing.
    /// </summary>
    private void ExtractAndFlyKeyFromNozzle(Transform block, Vector3 nozzlePos)
    {
        if (block == null || block.childCount < 1) return;

        Transform keyChild = null;
        for (int i = 0; i < block.childCount; i++)
        {
            if (block.GetChild(i).name.Contains("Key"))
            {
                keyChild = block.GetChild(i);
                break;
            }
        }

        if (keyChild == null) return;

        GameObject[] locks = GameObject.FindGameObjectsWithTag("Lock");
        if (locks != null && locks.Length > 0)
        {
            GameObject targetLock = null;
            float maxZ = float.MinValue;

            foreach (GameObject l in locks)
            {
                if (l.transform.position.z > maxZ)
                {
                    maxZ = l.transform.position.z;
                    targetLock = l;
                }
            }

            if (targetLock != null)
            {
                targetLock.tag = "Untagged";

                // Detach from the letter block so WordChecker.TryPlaceVacuumedPiece doesn't touch it
                keyChild.SetParent(null);
                keyChild.position = nozzlePos;
                keyChild.localScale = Vector3.zero; // Start invisible inside the nozzle

                // Pull settings from WordChecker dynamically
                var wc = WordChecker.instance;
                float keySpeed = wc != null ? wc.keySpeed : 15f;
                Vector3 keyOffset = wc != null ? wc.keyOffset : Vector3.zero;
                float keyJumpPower = wc != null ? wc.keyJumpPower : 2.0f;
                float keyTargetScale = wc != null ? wc.keyTargetScale : 0.6f;
                float keyTurnDuration = wc != null ? wc.keyTurnDuration : 0.3f;
                float lockDestroyDuration = wc != null ? wc.lockDestroyDuration : 0.3f;

                Vector3 destination = targetLock.transform.position + keyOffset;
                float distance = Vector3.Distance(keyChild.position, destination);
                float duration = keySpeed > 0 ? distance / keySpeed : 0.5f;

                Sequence flightSeq = DOTween.Sequence();

                // Scale up instantly from 0 (making it look like the vacuum spat it out)
                flightSeq.Append(keyChild.DOScale(Vector3.one * 1.3f, 0.15f).SetEase(Ease.OutBack));
                flightSeq.Append(keyChild.DOJump(destination, keyJumpPower, 1, duration).SetEase(Ease.InOutSine));
                flightSeq.Join(keyChild.DORotate(new Vector3(0, 0, 90), duration).SetEase(Ease.InOutSine));
                flightSeq.Join(keyChild.DOScale(Vector3.one * keyTargetScale, duration).SetEase(Ease.InCubic));

                flightSeq.AppendCallback(() =>
                {
                    if (keyChild != null)
                    {
                        keyChild.SetParent(targetLock.transform);
                        keyChild.localPosition = keyOffset;
                        keyChild.localEulerAngles = new Vector3(0, 0, 90);
                    }
                    if (targetLock != null)
                    {
                        targetLock.transform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 10, 1f);
                    }
                });

                flightSeq.AppendInterval(0.25f);
                flightSeq.Append(keyChild.DOLocalRotate(new Vector3(0, 0, 180), keyTurnDuration).SetEase(Ease.InOutQuad));
                flightSeq.Append(targetLock.transform.DOScale(Vector3.zero, lockDestroyDuration).SetEase(Ease.InBack));

                flightSeq.OnComplete(() =>
                {
                    if (targetLock != null) Destroy(targetLock);
                });
            }
        }
    }

    private Vector3 GetWorldPosFromUI(Transform uiTransform, Vector3 referenceTargetPos)
    {
        Canvas canvas = uiTransform.GetComponentInParent<Canvas>();
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        Vector2 targetScreenPos = RectTransformUtility.WorldToScreenPoint(uiCam, uiTransform.position);

        Camera cam = Camera.main;
        float distanceToCamera = Mathf.Max(0.5f, cam.WorldToScreenPoint(referenceTargetPos).z - flightElevationOffset);
        return cam.ScreenToWorldPoint(new Vector3(targetScreenPos.x, targetScreenPos.y, distanceToCamera));
    }

    private void PopUIElement(Transform uiTransform)
    {
        if (uiTransform == null) return;
        uiTransform.DOKill(true);
        Vector3 punchStrength = uiPopScale - Vector3.one;
        uiTransform.DOPunchScale(punchStrength, uiPopDuration, 5, 0.3f).SetLink(uiTransform.gameObject);
    }
}