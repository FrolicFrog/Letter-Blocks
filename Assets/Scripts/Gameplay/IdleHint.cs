using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Linq;

public class IdleHintManager : MonoBehaviour
{
    [Header("Idle Settings")]
    [Tooltip("Time in seconds before the hint animation plays.")]
    public float idleTimeThreshold = 8f;

    [Header("Shine Settings")]
    [Tooltip("Time in seconds before the material shine activates.")]
    public float shineThresholdTime = 4f;
    [Tooltip("The exact variable name in your shader to toggle (e.g., _UseSweep).")]
    public string shinePropertyName = "_UseSweep";

    [Header("Animation Settings")]
    public float jumpHeight = 0.5f;
    public float moveUpDuration = 0.2f;
    public float stayUpDuration = 0.15f;
    public float moveDownDuration = 0.2f;
    public float staggerDelay = 0.1f;

    [Header("Vibration Settings")]
    public float vibrateStrength = 8f;
    public int vibrateVibrato = 15;

    private float idleTimer = 0f;
    private bool isAnimating = false;
    private bool isShining = false;

    private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Quaternion> originalRotations = new Dictionary<Transform, Quaternion>();
    private List<Sequence> activeSequences = new List<Sequence>();

    private List<Transform> shiningTiles = new List<Transform>();
    private MaterialPropertyBlock propBlock;

    // Specific tracking to avoid killing the wrong coroutines
    private Coroutine animationCoroutine;
    private Coroutine gracefulCancelCoroutine;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        // If the user touches the screen or drags a tray
        if (Input.GetMouseButton(0) || Input.touchCount > 0 || (GlobalTrayDragger.Instance != null && GlobalTrayDragger.Instance.IsDragging))
        {
            ResetAllIdleHints();
            return;
        }

        // Only tick the timer if no grid processing is happening
        if (WordChecker.instance == null || !WordChecker.instance.isProcessing)
        {
            idleTimer += Time.deltaTime;

            // Trigger Shine Phase
            if (idleTimer >= shineThresholdTime && !isShining)
            {
                TriggerShineHint();
            }

            // Trigger Animation Phase
            if (idleTimer >= idleTimeThreshold && !isAnimating)
            {
                TriggerIdleHint();
                idleTimer = 0f;
            }
        }
    }

    private void ResetAllIdleHints()
    {
        if (isAnimating) CancelHintAnimation();

        // Only trigger graceful cancel if it's shining and not already cancelling
        if (isShining && gracefulCancelCoroutine == null)
        {
            gracefulCancelCoroutine = StartCoroutine(CancelShineGracefullyRoutine());
        }

        idleTimer = 0f;
    }

    // --- Core Logic: Find the right tiles to hint ---
    private List<Transform> GetTargetTiles()
    {
        var grid = TopGridManager.instance;
        var lvl = LevelManager.Instance;

        if (grid == null || lvl == null) return new List<Transform>();

        Dictionary<string, int> neededLettersCount = new Dictionary<string, int>();
        int startRow = Mathf.Max(0, grid.rows - 3);

        for (int r = startRow; r < grid.rows; r++)
        {
            for (int c = 0; c < grid.columns; c++)
            {
                Vector2Int key = new Vector2Int(r, c);

                if (lvl.excludedChar.Contains(key) && lvl.cellTexts.ContainsKey(key))
                {
                    string letter = lvl.cellTexts[key].Trim().ToUpper().Replace("\u200B", "");
                    if (!neededLettersCount.ContainsKey(letter)) neededLettersCount[letter] = 0;
                    neededLettersCount[letter]++;
                }
            }
        }

        if (neededLettersCount.Count == 0) return new List<Transform>();

        List<Transform> candidateTiles = new List<Transform>();
        if (transform.name.Contains("Procedural_Tray")) GatherTilesFromTray(transform, candidateTiles, neededLettersCount);
        else
        {
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Procedural_Tray")) GatherTilesFromTray(child, candidateTiles, neededLettersCount);
            }
        }

        if (candidateTiles.Count == 0) return new List<Transform>();

        candidateTiles.Sort((a, b) =>
        {
            float zDiff = b.position.z - a.position.z;
            if (Mathf.Abs(zDiff) > 0.05f) return zDiff.CompareTo(0f);
            float xDiff = a.position.x - b.position.x;
            return xDiff.CompareTo(0f);
        });

        List<Transform> finalTiles = new List<Transform>();
        foreach (Transform tile in candidateTiles)
        {
            string letter = tile.GetComponentInChildren<TextMeshPro>(true).text.Trim().ToUpper().Replace("\u200B", "");
            if (neededLettersCount.ContainsKey(letter) && neededLettersCount[letter] > 0)
            {
                finalTiles.Add(tile);
                neededLettersCount[letter]--;
            }
        }

        return finalTiles;
    }

    private void GatherTilesFromTray(Transform tray, List<Transform> candidateTiles, Dictionary<string, int> neededLettersCount)
    {
        foreach (Transform tile in tray)
        {
            if (DOTween.IsTweening(tile) || tile.name == "JumpingTile") continue;

            TextMeshPro tmp = tile.GetComponentInChildren<TextMeshPro>(true);
            if (tmp != null)
            {
                string letter = tmp.text.Trim().ToUpper().Replace("\u200B", "");
                if (neededLettersCount.ContainsKey(letter) && neededLettersCount[letter] > 0)
                {
                    candidateTiles.Add(tile);
                }
            }
        }
    }

    // --- Shine Logic ---
    private void TriggerShineHint()
    {
        List<Transform> tilesToShine = GetTargetTiles();
        if (tilesToShine.Count == 0) return;

        isShining = true;
        shiningTiles.Clear();

        // Stop graceful cancel if a new shine starts
        if (gracefulCancelCoroutine != null)
        {
            StopCoroutine(gracefulCancelCoroutine);
            gracefulCancelCoroutine = null;
        }

        foreach (Transform tile in tilesToShine)
        {
            Renderer r = tile.GetComponent<Renderer>();
            if (r != null)
            {
                r.GetPropertyBlock(propBlock);
                propBlock.SetFloat(shinePropertyName, 1f);
                r.SetPropertyBlock(propBlock);
                shiningTiles.Add(tile);
            }
        }
    }

    private IEnumerator CancelShineGracefullyRoutine()
    {
        // 1. Find the first valid shining tile to read the material properties
        Transform validTile = shiningTiles.FirstOrDefault(t => t != null);

        if (validTile != null)
        {
            Renderer r = validTile.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null)
            {
                // Replicate the math done inside the shader
                float sweepSpeed = r.sharedMaterial.HasProperty("_SweepSpeed") ? r.sharedMaterial.GetFloat("_SweepSpeed") : 1.5f;
                float sweepDelay = r.sharedMaterial.HasProperty("_SweepDelay") ? r.sharedMaterial.GetFloat("_SweepDelay") : 0f;

                float activeDuration = 1.0f / Mathf.Max(sweepSpeed, 0.0001f);
                float totalDuration = activeDuration + sweepDelay;

                // _Time.y in Unity corresponds exactly to Time.time
                float currentCycleTime = Time.time % totalDuration;

                // If we are currently in the active sweeping portion, wait until it finishes
                if (currentCycleTime < activeDuration)
                {
                    float remainingTime = activeDuration - currentCycleTime;
                    yield return new WaitForSeconds(remainingTime);
                }
            }
        }

        // 2. Shut off the shine immediately after the sweep finishes
        CancelShine();
    }

    private void CancelShine()
    {
        foreach (Transform tile in shiningTiles)
        {
            if (tile != null) // Null check in case the tile was destroyed while waiting
            {
                Renderer r = tile.GetComponent<Renderer>();
                if (r != null)
                {
                    r.GetPropertyBlock(propBlock);
                    propBlock.SetFloat(shinePropertyName, 0f);
                    r.SetPropertyBlock(propBlock);
                }
            }
        }
        shiningTiles.Clear();
        isShining = false;
        gracefulCancelCoroutine = null;
    }

    // --- Animation Logic ---
    private void TriggerIdleHint()
    {
        List<Transform> finalTilesToAnimate = GetTargetTiles();

        if (finalTilesToAnimate.Count > 0)
        {
            if (animationCoroutine != null) StopCoroutine(animationCoroutine);
            animationCoroutine = StartCoroutine(AnimateTilesRoutine(finalTilesToAnimate));
        }
    }

    private IEnumerator AnimateTilesRoutine(List<Transform> tiles)
    {
        isAnimating = true;
        float totalAnimTime = 0f;

        originalPositions.Clear();
        originalRotations.Clear();
        activeSequences.Clear();

        for (int i = 0; i < tiles.Count; i++)
        {
            Transform tile = tiles[i];
            if (tile == null) continue;

            originalPositions[tile] = tile.localPosition;
            originalRotations[tile] = tile.localRotation;

            float delay = i * staggerDelay;
            float animDuration = moveUpDuration + stayUpDuration + moveDownDuration;

            Sequence seq = DOTween.Sequence().SetLink(tile.gameObject).SetTarget(tile);
            activeSequences.Add(seq);

            seq.AppendInterval(delay);
            seq.Append(tile.DOLocalMoveY(originalPositions[tile].y + jumpHeight, moveUpDuration).SetEase(Ease.OutQuad));
            seq.Append(tile.DOShakeRotation(stayUpDuration, new Vector3(0, 0, vibrateStrength), vibrateVibrato, 90, false));
            seq.Append(tile.DOLocalRotateQuaternion(originalRotations[tile], 0f));
            seq.Append(tile.DOLocalMoveY(originalPositions[tile].y, moveDownDuration).SetEase(Ease.InQuad));

            float timeToFinish = delay + animDuration;
            if (timeToFinish > totalAnimTime) totalAnimTime = timeToFinish;
        }

        yield return new WaitForSeconds(totalAnimTime);

        if (isAnimating)
        {
            originalPositions.Clear();
            originalRotations.Clear();
            activeSequences.Clear();
            isAnimating = false;
        }
    }

    private void CancelHintAnimation()
    {
        // Only stop the specific animation coroutine to avoid killing the graceful shader cancel
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        foreach (var seq in activeSequences)
        {
            if (seq != null && seq.IsActive())
            {
                seq.Kill();
            }
        }
        activeSequences.Clear();

        foreach (var tile in originalPositions.Keys)
        {
            if (tile != null)
            {
                tile.DOKill();
                tile.localPosition = originalPositions[tile];
                tile.localRotation = originalRotations[tile];
            }
        }

        originalPositions.Clear();
        originalRotations.Clear();
        isAnimating = false;
    }
}