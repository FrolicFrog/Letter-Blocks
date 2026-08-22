using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(GridLayoutGroup))]
public class GridLayoutTweener : MonoBehaviour
{
    [Header("Grid Reference")]
    [Tooltip("The GridLayoutGroup you want to animate.")]
    [SerializeField] private GridLayoutGroup gridLayout;
    private Image backgroundImage;

    [Header("Animation Settings")]
    public float duration = 1f;
    public float startDelay = 0.5f;
    public Ease easeType = Ease.OutQuad;

    [Header("Cell Size")]
    public bool animateCellSize = true;
    public Vector2 targetCellSize = new Vector2(150f, 150f);

    [Header("Spacing")]
    public bool animateSpacing = true;
    public Vector2 targetSpacing = new Vector2(15f, 15f);

    [Header("Constraint Count")]
    public bool animateConstraintCount = true;
    [Tooltip("Requires Constraint to be set to Fixed Column/Row Count in the GridLayoutGroup.")]
    public int targetConstraintCount = 4;

    [Header("Padding")]
    public bool animatePadding = true;
    [Tooltip("Target values for Left, Right, Top, Bottom.")]
    public RectOffset targetPadding;

    [Header("Alpha Settings")]
    public bool animateAlpha = true;
    [Range(0f, 1f)]
    public float targetAlpha = 1f;
    [Tooltip("Duration of the alpha fade. It will automatically start near the end of the grid animation.")]
    public float alphaDuration = 0.3f;

    [Header("Max Font Size Settings")]
    public bool animateMaxFontSize = true;
    [Tooltip("Target maxFontSize for AutoAdjustTMP on the 1st child of each grid element.")]
    public float targetMaxFontSize = 35f;

    [Header("Completion Events")]
    public bool enableSecondChildOnComplete = true;

    public static GridLayoutTweener instance;

    // --- Cached Original Values ---
    private Vector2 originalCellSize;
    private Vector2 originalSpacing;
    private int originalConstraintCount;
    private RectOffset originalPadding;
    private float originalAlpha = 1f;
    private Dictionary<AutoAdjustTMP, float> originalFontSizes = new Dictionary<AutoAdjustTMP, float>();
    private bool hasCachedValues = false;

    private void Awake()
    {
        EnsureComponentsExist();
        instance = this;

        if (targetPadding == null)
            targetPadding = new RectOffset();

        if (animateAlpha && backgroundImage == null)
            Debug.LogWarning("Animate Alpha is enabled, but no Image component was found!", this);

        CacheOriginalValues();

       
    }

    private void EnsureComponentsExist()
    {
        if (gridLayout == null) gridLayout = GetComponent<GridLayoutGroup>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
    }

    private void CacheOriginalValues()
    {
        if (hasCachedValues) return;
        EnsureComponentsExist();

        if (gridLayout != null)
        {
            originalCellSize = gridLayout.cellSize;
            originalSpacing = gridLayout.spacing;
            originalConstraintCount = gridLayout.constraintCount;

            if (gridLayout.padding != null)
            {
                originalPadding = new RectOffset(
                    gridLayout.padding.left,
                    gridLayout.padding.right,
                    gridLayout.padding.top,
                    gridLayout.padding.bottom
                );
            }

            // Cache font sizes
            for (int i = 0; i < gridLayout.transform.childCount; i++)
            {
                Transform gridChild = gridLayout.transform.GetChild(i);
                if (gridChild.childCount > 0)
                {
                    AutoAdjustTMP textAdjuster = gridChild.GetChild(0).GetComponent<AutoAdjustTMP>();
                    if (textAdjuster != null)
                    {
                        originalFontSizes[textAdjuster] = textAdjuster.maxFontSize;
                    }
                }
            }
        }

        if (backgroundImage != null)
        {
            originalAlpha = backgroundImage.color.a;
        }

        hasCachedValues = true;
    }

    /// <summary>
    /// Forcibly stops all animations and snaps everything back to its starting state.
    /// </summary>
    public void ResetToOriginal()
    {
        if (!hasCachedValues) return;

        // 1. FULLY KILL TWEENS AND COROUTINES
        DOTween.Kill(gridLayout, complete: false);
        StopAllCoroutines();

        // 2. RESET GRID
        if (animateCellSize) gridLayout.cellSize = originalCellSize;
        if (animateSpacing) gridLayout.spacing = originalSpacing;
        if (animateConstraintCount) gridLayout.constraintCount = originalConstraintCount;

        if (animatePadding && originalPadding != null)
        {
            gridLayout.padding = new RectOffset(
                originalPadding.left,
                originalPadding.right,
                originalPadding.top,
                originalPadding.bottom
            );
        }

        // 3. RESET ALPHA
        if (animateAlpha && backgroundImage != null)
        {
            Color c = backgroundImage.color;
            c.a = originalAlpha;
            backgroundImage.color = c;
            backgroundImage.canvasRenderer.SetAlpha(originalAlpha); // Force visual update
        }

        // 4. RESET FONTS
        if (animateMaxFontSize)
        {
            foreach (var kvp in originalFontSizes)
            {
                if (kvp.Key != null) kvp.Key.maxFontSize = kvp.Value;
            }
        }

        // 5. RESET 2ND CHILDREN
        if (enableSecondChildOnComplete)
        {
            for (int i = 0; i < gridLayout.transform.childCount; i++)
            {
                Transform gridChild = gridLayout.transform.GetChild(i);
                if (gridChild.childCount > 1)
                {
                    gridChild.GetChild(1).gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Animates the grid. If instant is true, no tweens are used and values are snapped immediately.
    /// </summary>
    public void AnimateGrid(bool instant = false)
    {
        EnsureComponentsExist();

        if (gridLayout == null)
        {
            Debug.LogWarning("GridLayoutGroup reference is missing!", this);
            return;
        }

        // Guarantee we know the original state before doing anything
        CacheOriginalValues();

        // Put everything back to the start line and destroy any active tweens
        ResetToOriginal();

        // ==========================================
        // INSTANT BEHAVIOR (NO ANIMATION)
        // ==========================================
        if (instant)
        {
            if (animateCellSize) gridLayout.cellSize = targetCellSize;
            if (animateSpacing) gridLayout.spacing = targetSpacing;
            if (animateConstraintCount) gridLayout.constraintCount = targetConstraintCount;

            if (animatePadding)
            {
                RectOffset pad = gridLayout.padding ?? new RectOffset();
                pad.left = targetPadding.left;
                pad.right = targetPadding.right;
                pad.top = targetPadding.top;
                pad.bottom = targetPadding.bottom;
                gridLayout.padding = pad;
            }

            if (animateAlpha && backgroundImage != null)
            {
                Color c = backgroundImage.color;
                c.a = targetAlpha;
                backgroundImage.color = c;
                backgroundImage.canvasRenderer.SetAlpha(targetAlpha);
            }

            if (animateMaxFontSize)
            {
                for (int i = 0; i < gridLayout.transform.childCount; i++)
                {
                    Transform gridChild = gridLayout.transform.GetChild(i);
                    if (gridChild.childCount > 0)
                    {
                        AutoAdjustTMP textAdjuster = gridChild.GetChild(0).GetComponent<AutoAdjustTMP>();
                        if (textAdjuster != null)
                        {
                            textAdjuster.maxFontSize = targetMaxFontSize;
                        }
                    }
                }
            }

            if (enableSecondChildOnComplete) EnableSecondChildren();

            // Force the layout to rebuild instantly so you don't see one frame of delay
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridLayout.transform as RectTransform);

            return; // EXIT FUNCTION, NO TWEENS RUN
        }

        // ==========================================
        // DOTWEEN BEHAVIOR (SMOOTH ANIMATION)
        // ==========================================
        if (animateCellSize)
        {
            DOTween.To(() => gridLayout.cellSize, x => gridLayout.cellSize = x, targetCellSize, duration)
            .SetDelay(startDelay).SetEase(easeType).SetTarget(gridLayout);
        }

        if (animateSpacing)
        {
            DOTween.To(() => gridLayout.spacing, x => gridLayout.spacing = x, targetSpacing, duration)
            .SetDelay(startDelay).SetEase(easeType).SetTarget(gridLayout);
        }

        if (animateConstraintCount)
        {
            DOTween.To(() => gridLayout.constraintCount, x => gridLayout.constraintCount = x, targetConstraintCount, duration)
            .SetDelay(startDelay).SetEase(easeType).SetTarget(gridLayout);
        }

        if (animatePadding)
        {
            RectOffset currentPad = gridLayout.padding ?? new RectOffset();
            Vector4 startPad = new Vector4(currentPad.left, currentPad.right, currentPad.top, currentPad.bottom);
            Vector4 endPad = new Vector4(targetPadding.left, targetPadding.right, targetPadding.top, targetPadding.bottom);

            DOTween.To(() => startPad, x =>
            {
                startPad = x;
                currentPad.left = Mathf.RoundToInt(x.x);
                currentPad.right = Mathf.RoundToInt(x.y);
                currentPad.top = Mathf.RoundToInt(x.z);
                currentPad.bottom = Mathf.RoundToInt(x.w);
                gridLayout.padding = currentPad;
            }, endPad, duration)
            .SetDelay(startDelay).SetEase(easeType).SetTarget(gridLayout);
        }

        if (animateAlpha && backgroundImage != null)
        {
            float clampedAlphaDur = Mathf.Clamp(alphaDuration, 0f, duration);
            float alphaDel = startDelay + (duration - clampedAlphaDur);

            DOTween.To(
                () => backgroundImage.color.a,
                x =>
                {
                    Color c = backgroundImage.color;
                    c.a = x;
                    backgroundImage.color = c;
                },
                targetAlpha,
                clampedAlphaDur
            )
            .SetDelay(alphaDel)
            .SetEase(easeType)
            .SetTarget(gridLayout);
        }

        if (animateMaxFontSize)
        {
            StartCoroutine(MaxTestFontSize());
        }

        if (enableSecondChildOnComplete)
        {
            float totalAnimationTime = startDelay + duration;
            DOVirtual.DelayedCall(totalAnimationTime, EnableSecondChildren, ignoreTimeScale: false)
                .SetTarget(gridLayout);
        }
    }

    private void EnableSecondChildren()
    {
        for (int i = 0; i < gridLayout.transform.childCount; i++)
        {
            Transform gridChild = gridLayout.transform.GetChild(i);
            if (gridChild.childCount > 1)
            {
                gridChild.GetChild(1).gameObject.SetActive(true);
            }
        }
    }

    IEnumerator MaxTestFontSize()
    {
        yield return new WaitForEndOfFrame();

        for (int i = 0; i < gridLayout.transform.childCount; i++)
        {
            Transform gridChild = gridLayout.transform.GetChild(i);
            if (gridChild.childCount > 0)
            {
                Transform firstSubChild = gridChild.GetChild(0);
                AutoAdjustTMP textAdjuster = firstSubChild.GetComponent<AutoAdjustTMP>();

                if (textAdjuster != null)
                {
                    DOTween.To(
                        () => textAdjuster.maxFontSize,
                        x => textAdjuster.maxFontSize = x,
                        targetMaxFontSize,
                        duration
                    )
                    .SetDelay(startDelay)
                    .SetEase(easeType)
                    .SetTarget(gridLayout);
                }
            }
        }
    }
}