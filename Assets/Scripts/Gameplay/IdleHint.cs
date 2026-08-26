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

    [Header("Animation Settings")]
    public float jumpHeight = 0.5f;
    [Tooltip("How long it takes to travel up.")]
    public float moveUpDuration = 0.2f;
    [Tooltip("How long it stays hovering in the up position.")]
    public float stayUpDuration = 0.15f;
    [Tooltip("How long it takes to fall back down.")]
    public float moveDownDuration = 0.2f;
    public float staggerDelay = 0.1f;

    [Header("Vibration Settings")]
    [Tooltip("How much the letter wiggles/vibrates while up (Z-axis rotation).")]
    public float vibrateStrength = 8f;
    [Tooltip("How fast the vibration shakes.")]
    public int vibrateVibrato = 15;

    private float idleTimer = 0f;
    private bool isAnimating = false;

    // Track state to instantly reset if the user interrupts
    private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Quaternion> originalRotations = new Dictionary<Transform, Quaternion>();

    // Explicitly track active sequences to forcefully kill them
    private List<Sequence> activeSequences = new List<Sequence>();

    private void Update()
    {
        // If the user touches the screen or drags a tray
        if (Input.GetMouseButton(0) || Input.touchCount > 0 || (GlobalTrayDragger.Instance != null && GlobalTrayDragger.Instance.IsDragging))
        {
            // Instantly abort the animation and sit the letters down if it was running
            if (isAnimating)
            {
                CancelHintAnimation();
            }

            idleTimer = 0f;
            return;
        }

        // Only tick the timer if we aren't currently animating and no grid processing is happening
        if (!isAnimating && (WordChecker.instance == null || !WordChecker.instance.isProcessing))
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= idleTimeThreshold)
            {
                TriggerIdleHint();
                idleTimer = 0f; // Reset so it waits before repeating
            }
        }
    }

    private void TriggerIdleHint()
    {
        var grid = TopGridManager.instance;
        var lvl = LevelManager.Instance;

        if (grid == null || lvl == null) return;

        // 1. Find missing letters in the last 3 rows of the TOP grid
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

                    if (!neededLettersCount.ContainsKey(letter))
                        neededLettersCount[letter] = 0;

                    neededLettersCount[letter]++;
                }
            }
        }

        if (neededLettersCount.Count == 0) return;

        // 2. Gather all valid candidate tiles across ALL Procedural_Trays
        List<Transform> candidateTiles = new List<Transform>();

        if (transform.name.Contains("Procedural_Tray"))
        {
            GatherTilesFromTray(transform, candidateTiles, neededLettersCount);
        }
        else
        {
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Procedural_Tray"))
                {
                    GatherTilesFromTray(child, candidateTiles, neededLettersCount);
                }
            }
        }

        if (candidateTiles.Count == 0) return;

        // 3. Sort tiles: Highest Z first, then lowest X (Left to Right)
        candidateTiles.Sort((a, b) =>
        {
            float zDiff = b.position.z - a.position.z;
            if (Mathf.Abs(zDiff) > 0.05f)
            {
                return zDiff.CompareTo(0f);
            }

            float xDiff = a.position.x - b.position.x;
            return xDiff.CompareTo(0f);
        });

        // 4. Select exactly the required amount of tiles based on the sorted list
        List<Transform> finalTilesToAnimate = new List<Transform>();
        foreach (Transform tile in candidateTiles)
        {
            string letter = tile.GetComponentInChildren<TextMeshPro>(true).text.Trim().ToUpper().Replace("\u200B", "");

            if (neededLettersCount.ContainsKey(letter) && neededLettersCount[letter] > 0)
            {
                finalTilesToAnimate.Add(tile);
                neededLettersCount[letter]--;
            }
        }

        // 5. Play staggered DOTween animation
        if (finalTilesToAnimate.Count > 0)
        {
            StartCoroutine(AnimateTilesRoutine(finalTilesToAnimate));
        }
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

            // Save state so we can restore it if interrupted
            originalPositions[tile] = tile.localPosition;
            originalRotations[tile] = tile.localRotation;

            float delay = i * staggerDelay;
            float animDuration = moveUpDuration + stayUpDuration + moveDownDuration;

            // Link the sequence AND set the target explicitly
            Sequence seq = DOTween.Sequence().SetLink(tile.gameObject).SetTarget(tile);
            activeSequences.Add(seq); // Track it so we can slaughter it later

            seq.AppendInterval(delay);

            // Move up
            seq.Append(tile.DOLocalMoveY(originalPositions[tile].y + jumpHeight, moveUpDuration).SetEase(Ease.OutQuad));

            // Hover and Vibrate
            seq.Append(tile.DOShakeRotation(stayUpDuration, new Vector3(0, 0, vibrateStrength), vibrateVibrato, 90, false));

            // Ensure rotation is perfectly flat before coming down
            seq.Append(tile.DOLocalRotateQuaternion(originalRotations[tile], 0f));

            // Move back down
            seq.Append(tile.DOLocalMoveY(originalPositions[tile].y, moveDownDuration).SetEase(Ease.InQuad));

            float timeToFinish = delay + animDuration;
            if (timeToFinish > totalAnimTime) totalAnimTime = timeToFinish;
        }

        // Wait for all animations to finish before resetting
        yield return new WaitForSeconds(totalAnimTime);

        // Clear dictionaries cleanly if animation finishes uninterrupted
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
        // 1. Stop the coroutine from firing off any more logic
        StopAllCoroutines();

        // 2. Forcefully kill all active sequences
        foreach (var seq in activeSequences)
        {
            if (seq != null && seq.IsActive())
            {
                seq.Kill();
            }
        }
        activeSequences.Clear();

        // 3. Instantly snap tiles back to their saved origin
        foreach (var tile in originalPositions.Keys)
        {
            if (tile != null)
            {
                tile.DOKill(); // Catch any stray tweens on the transform itself
                tile.localPosition = originalPositions[tile];
                tile.localRotation = originalRotations[tile];
            }
        }

        originalPositions.Clear();
        originalRotations.Clear();
        isAnimating = false;
    }
}