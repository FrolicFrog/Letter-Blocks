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
    private RectTransform gridRect;

    [Header("Intro Animation Settings")]
    public bool playIntroFirst = true;
    [Tooltip("How long each cell takes to pop in from 0 scale.")]
    public float introDuration = 0.4f;
    [Tooltip("Delay before the next cell starts popping in.")]
    public float introStagger = 0.1f;
    [Tooltip("Pause between the intro finishing and the shrink/move animation starting.")]
    public float pauseAfterIntro = 0.2f;
    public Ease introEase = Ease.OutBack;

    [Header("Main Animation Settings")]
    public float duration = 1f;
    [Tooltip("How long the cells scale together BEFORE moving to the top.")]
    public float startDelay = 0.5f;
    [Tooltip("The delay between each cell starting its movement to the top.")]
    public float staggerDelay = 0.1f;
    public Ease easeType = Ease.OutQuad;

    [Header("Child Scale Animation")]
    public bool animateChildScale = true;
    [Tooltip("Target scale while shrinking together (during Start Delay).")]
    public Vector3 targetChildScale = new Vector3(0.8f, 0.8f, 1f);
    public Ease childScaleEase = Ease.OutQuad;

    [Header("Cell Size")]
    public bool animateCellSize = true;
    public Vector2 targetCellSize = new Vector2(150f, 150f);

    [Header("Spacing")]
    public bool animateSpacing = true;
    public Vector2 targetSpacing = new Vector2(15f, 15f);

    [Header("Constraint Count")]
    public bool animateConstraintCount = true;
    public int targetConstraintCount = 4;

    [Header("Padding")]
    public bool animatePadding = true;
    public RectOffset targetPadding;

    [Header("Alpha Settings")]
    public bool animateAlpha = true;
    [Range(0f, 1f)]
    public float targetAlpha = 1f;
    public float alphaDuration = 0.3f;

    [Header("Max Font Size Settings")]
    public bool animateMaxFontSize = true;
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
    private Dictionary<Transform, Vector3> originalChildScales = new Dictionary<Transform, Vector3>();
    private bool hasCachedValues = false;

    private Coroutine activeAnimationRoutine;

    private void Awake()
    {
        instance = this;
        EnsureComponentsExist();

        if (targetPadding == null) targetPadding = new RectOffset();
        if (animateAlpha && backgroundImage == null)
            Debug.LogWarning("Animate Alpha is enabled, but no Image component was found!", this);
    }

    private void Start()
    {
        if (!hasCachedValues) CacheOriginalValues();
    }

    private void EnsureComponentsExist()
    {
        if (gridLayout == null) gridLayout = GetComponent<GridLayoutGroup>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (gridRect == null && gridLayout != null) gridRect = gridLayout.GetComponent<RectTransform>();
    }

    private void CacheOriginalValues()
    {
        if (hasCachedValues) return;
        EnsureComponentsExist();

        Canvas.ForceUpdateCanvases();
        if (gridLayout != null && gridRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);

            originalCellSize = gridLayout.cellSize;
            originalSpacing = gridLayout.spacing;
            originalConstraintCount = gridLayout.constraintCount;

            if (gridLayout.padding != null)
            {
                originalPadding = new RectOffset(
                    gridLayout.padding.left, gridLayout.padding.right,
                    gridLayout.padding.top, gridLayout.padding.bottom
                );
            }

            originalChildScales.Clear();
            originalFontSizes.Clear();

            for (int i = 0; i < gridLayout.transform.childCount; i++)
            {
                Transform gridChild = gridLayout.transform.GetChild(i);

                // Bulletproof cache: Don't cache a 0 scale if it's already invisible
                if (gridChild.localScale != Vector3.zero)
                {
                    originalChildScales[gridChild] = gridChild.localScale;
                }
                else
                {
                    originalChildScales[gridChild] = Vector3.one;
                }

                if (gridChild.childCount > 0)
                {
                    AutoAdjustTMP textAdjuster = gridChild.GetChild(0).GetComponent<AutoAdjustTMP>();
                    if (textAdjuster != null) originalFontSizes[textAdjuster] = textAdjuster.maxFontSize;
                }
            }
        }

        if (backgroundImage != null) originalAlpha = backgroundImage.color.a;
        hasCachedValues = true;
    }

    public void ResetToOriginal()
    {
        if (!hasCachedValues) return;

        DOTween.Kill(gridLayout, complete: false);
        StopAllCoroutines();

        gridLayout.enabled = true;

        if (animateCellSize) gridLayout.cellSize = originalCellSize;
        if (animateSpacing) gridLayout.spacing = originalSpacing;
        if (animateConstraintCount) gridLayout.constraintCount = originalConstraintCount;

        if (animatePadding && originalPadding != null)
        {
            RectOffset pad = gridLayout.padding ?? new RectOffset();
            pad.left = originalPadding.left;
            pad.right = originalPadding.right;
            pad.top = originalPadding.top;
            pad.bottom = originalPadding.bottom;
            gridLayout.padding = pad;
        }

        if (animateAlpha && backgroundImage != null)
        {
            Color c = backgroundImage.color;
            c.a = originalAlpha;
            backgroundImage.color = c;
            backgroundImage.canvasRenderer.SetAlpha(originalAlpha);
        }

        for (int i = 0; i < gridLayout.transform.childCount; i++)
        {
            Transform child = gridLayout.transform.GetChild(i);
            DOTween.Kill(child, complete: false);

            if (originalChildScales.ContainsKey(child))
                child.localScale = originalChildScales[child];

            if (enableSecondChildOnComplete && child.childCount > 1)
                child.GetChild(1).gameObject.SetActive(false);
        }

        if (animateMaxFontSize)
        {
            foreach (var kvp in originalFontSizes)
            {
                if (kvp.Key != null)
                {
                    DOTween.Kill(kvp.Key, complete: false);
                    kvp.Key.maxFontSize = kvp.Value;
                }
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);
    }

    public void AnimateGrid(bool instant = false)
    {
        EnsureComponentsExist();
        if (gridLayout == null) return;

        // 1. Instantly cache the values before doing anything
        CacheOriginalValues();

        // 2. Hide the items instantly to prevent the frame-0 visual pop/flash
        if (playIntroFirst && !instant)
        {
            for (int i = 0; i < gridLayout.transform.childCount; i++)
            {
                Transform child = gridLayout.transform.GetChild(i);
                DOTween.Kill(child, complete: false); // Kill any active scale tweens
                child.localScale = Vector3.zero;
            }
        }

        if (activeAnimationRoutine != null) StopCoroutine(activeAnimationRoutine);
        activeAnimationRoutine = StartCoroutine(AnimateGridRoutine(instant));
    }

    private IEnumerator AnimateGridRoutine(bool instant)
    {
        // Yield 1 frame to ensure UI Canvas layout calculates correctly on start
        if (Time.frameCount < 2)
        {
            yield return new WaitForEndOfFrame();
        }

        int childCount = gridLayout.transform.childCount;

        if (instant)
        {
            ResetToOriginal();
            if (animateCellSize) gridLayout.cellSize = targetCellSize;
            if (animateSpacing) gridLayout.spacing = targetSpacing;
            if (animateConstraintCount) gridLayout.constraintCount = targetConstraintCount;

            if (animatePadding)
            {
                RectOffset pad = gridLayout.padding ?? new RectOffset();
                pad.left = targetPadding.left; pad.right = targetPadding.right;
                pad.top = targetPadding.top; pad.bottom = targetPadding.bottom;
                gridLayout.padding = pad;
            }

            if (animateChildScale)
            {
                for (int i = 0; i < childCount; i++)
                    gridLayout.transform.GetChild(i).localScale = Vector3.one;
            }

            if (animateAlpha && backgroundImage != null)
            {
                Color c = backgroundImage.color; c.a = targetAlpha;
                backgroundImage.color = c;
                backgroundImage.canvasRenderer.SetAlpha(targetAlpha);
            }

            if (animateMaxFontSize)
            {
                foreach (var kvp in originalFontSizes)
                    if (kvp.Key != null) kvp.Key.maxFontSize = targetMaxFontSize;
            }

            if (enableSecondChildOnComplete) EnableSecondChildren();
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);
            yield break;
        }

        // ==========================================
        // PHASE 1: INTRO ANIMATION (0 to Default)
        // ==========================================
        if (playIntroFirst)
        {
            Tween lastTween = null;

            for (int i = 0; i < childCount; i++)
            {
                Transform child = gridLayout.transform.GetChild(i);

                // Fetch valid target scale
                Vector3 targetScale = originalChildScales.ContainsKey(child) ? originalChildScales[child] : Vector3.one;
                if (targetScale == Vector3.zero) targetScale = Vector3.one; // Ultimate fail-safe

                // Force 0 one last time right before starting the tween
                child.localScale = Vector3.zero;

                lastTween = child.DOScale(targetScale, introDuration)
                    .SetDelay(i * introStagger)
                    .SetEase(introEase)
                    .SetTarget(child);
            }

            // Explicitly sync with DOTween so we wait exactly until the last cell lerps perfectly
            if (lastTween != null)
            {
                yield return lastTween.WaitForCompletion();
            }

            if (pauseAfterIntro > 0)
            {
                yield return new WaitForSeconds(pauseAfterIntro);
            }
        }

        // ==========================================
        // PHASE 2 & 3: CALCULATE AND STAGGERED MOVE
        // ==========================================

        if (animateCellSize) gridLayout.cellSize = targetCellSize;
        if (animateSpacing) gridLayout.spacing = targetSpacing;
        if (animateConstraintCount) gridLayout.constraintCount = targetConstraintCount;
        if (animatePadding)
        {
            RectOffset pad = gridLayout.padding ?? new RectOffset();
            pad.left = targetPadding.left; pad.right = targetPadding.right;
            pad.top = targetPadding.top; pad.bottom = targetPadding.bottom;
            gridLayout.padding = pad;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);

        Vector2[] targetPositions = new Vector2[childCount];
        for (int i = 0; i < childCount; i++)
        {
            targetPositions[i] = (gridLayout.transform.GetChild(i) as RectTransform).anchoredPosition;
        }

        ResetToOriginal();
        gridLayout.enabled = false;

        for (int i = 0; i < childCount; i++)
        {
            RectTransform child = gridLayout.transform.GetChild(i) as RectTransform;
            float childMoveDelay = startDelay + (i * staggerDelay);

            // Phase 2: Shrink together
            if (animateChildScale && startDelay > 0f)
            {
                child.DOScale(targetChildScale, startDelay)
                    .SetEase(childScaleEase)
                    .SetTarget(child);
            }

            // Phase 3: Move to top
            child.DOAnchorPos(targetPositions[i], duration)
                .SetDelay(childMoveDelay)
                .SetEase(easeType)
                .SetTarget(child);

            // Phase 3: Scale back to 1
            if (animateChildScale)
            {
                child.DOScale(Vector3.one, duration)
                    .SetDelay(childMoveDelay)
                    .SetEase(easeType)
                    .SetTarget(child);
            }

            if (animateCellSize)
            {
                child.DOSizeDelta(targetCellSize, duration)
                    .SetDelay(childMoveDelay)
                    .SetEase(easeType)
                    .SetTarget(child);
            }

            if (animateMaxFontSize && child.childCount > 0)
            {
                AutoAdjustTMP textAdjuster = child.GetChild(0).GetComponent<AutoAdjustTMP>();
                if (textAdjuster != null)
                {
                    DOTween.To(() => textAdjuster.maxFontSize, x => textAdjuster.maxFontSize = x, targetMaxFontSize, duration)
                        .SetDelay(childMoveDelay)
                        .SetEase(easeType)
                        .SetTarget(textAdjuster);
                }
            }
        }

        float maxTotalTime = startDelay + (Mathf.Max(0, childCount - 1) * staggerDelay) + duration;

        if (animateAlpha && backgroundImage != null)
        {
            float clampedAlphaDur = Mathf.Clamp(alphaDuration, 0f, duration);
            float totalDurationBeforeLastMove = startDelay + (Mathf.Max(0, childCount - 1) * staggerDelay);
            float alphaDel = totalDurationBeforeLastMove + (duration - clampedAlphaDur);

            DOTween.To(() => backgroundImage.color.a, x =>
            {
                Color c = backgroundImage.color;
                c.a = x;
                backgroundImage.color = c;
            }, targetAlpha, clampedAlphaDur)
            .SetDelay(alphaDel)
            .SetEase(easeType)
            .SetTarget(gridLayout);
        }

        DOVirtual.DelayedCall(maxTotalTime, () => {
            gridLayout.enabled = true;

            if (animateCellSize) gridLayout.cellSize = targetCellSize;
            if (animateSpacing) gridLayout.spacing = targetSpacing;
            if (animateConstraintCount) gridLayout.constraintCount = targetConstraintCount;
            if (animatePadding)
            {
                RectOffset pad = gridLayout.padding ?? new RectOffset();
                pad.left = targetPadding.left; pad.right = targetPadding.right;
                pad.top = targetPadding.top; pad.bottom = targetPadding.bottom;
                gridLayout.padding = pad;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);

            if (enableSecondChildOnComplete) EnableSecondChildren();
        }, ignoreTimeScale: false).SetTarget(gridLayout);
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
}