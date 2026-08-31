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
    [Tooltip("Requires Constraint to be set to Fixed Column/Row Count in the GridLayoutGroup.")]
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
    public GameObject toggles;
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

        // Safely capture original alpha on Frame 0 before any tweens or scripts can alter it
        if (backgroundImage != null)
            originalAlpha = backgroundImage.color.a;

        if (targetPadding == null) targetPadding = new RectOffset();
        if (animateAlpha && backgroundImage == null)
            Debug.LogWarning("Animate Alpha is enabled, but no Image component was found!", this);
    }

    private IEnumerator Start()
    {
        // Give the Canvas a split second to set up on scene load before centering
        if (Time.frameCount < 5)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
        }

        if (!hasCachedValues) CacheOriginalValues();

        // Instantly force the starting layout to be centered upon starting the game (if no anim is running)
        if (activeAnimationRoutine == null)
        {
            ResetToOriginal(true);
        }
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

        hasCachedValues = true;
    }

    private Vector2[] GetCenteredPositions(Vector2 cellSz, Vector2 spc, int constraint, RectOffset pad)
    {
        int childCount = gridLayout.transform.childCount;
        Vector2[] positions = new Vector2[childCount];

        gridLayout.enabled = true;
        gridLayout.cellSize = cellSz;
        gridLayout.spacing = spc;
        gridLayout.constraintCount = constraint;

        if (pad != null)
        {
            RectOffset safePad = gridLayout.padding ?? new RectOffset();
            safePad.left = pad.left; safePad.right = pad.right;
            safePad.top = pad.top; safePad.bottom = pad.bottom;
            gridLayout.padding = safePad;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);

        for (int i = 0; i < childCount; i++)
        {
            positions[i] = (gridLayout.transform.GetChild(i) as RectTransform).anchoredPosition;
        }

        // Prevent division by zero if constraint happens to be 0
        int actualColumns = Mathf.Max(1, Mathf.Min(childCount, constraint));

        float fullRowWidth = (actualColumns * cellSz.x) + Mathf.Max(0, actualColumns - 1) * spc.x;

        for (int i = 0; i < childCount; i++)
        {
            int row = i / actualColumns;
            int itemsInThisRow = Mathf.Min(actualColumns, childCount - (row * actualColumns));

            if (itemsInThisRow < actualColumns)
            {
                float currentRowWidth = (itemsInThisRow * cellSz.x) + Mathf.Max(0, itemsInThisRow - 1) * spc.x;
                float rightwardOffset = (fullRowWidth - currentRowWidth) / 2f;
                positions[i].x += rightwardOffset;
            }
        }

        return positions;
    }

    public void ResetToOriginal(bool killTweens = true, bool resetScale = true)
    {
        if (!hasCachedValues) return;

        if (killTweens)
        {
            DOTween.Kill(gridLayout, complete: false);
            if (backgroundImage != null) DOTween.Kill(backgroundImage, complete: false);
            if (activeAnimationRoutine != null) StopCoroutine(activeAnimationRoutine);
        }

        Vector2[] startPositions = GetCenteredPositions(originalCellSize, originalSpacing, originalConstraintCount, originalPadding);

        gridLayout.enabled = false;

        if (animateAlpha && backgroundImage != null)
        {
            Color c = backgroundImage.color;
            c.a = originalAlpha;
            backgroundImage.color = c;
        }

        for (int i = 0; i < gridLayout.transform.childCount; i++)
        {
            RectTransform child = gridLayout.transform.GetChild(i) as RectTransform;

            if (killTweens) DOTween.Kill(child, complete: false);

            child.anchoredPosition = startPositions[i];

            if (animateCellSize) child.sizeDelta = originalCellSize;

            if (resetScale && originalChildScales.ContainsKey(child))
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
                    if (killTweens) DOTween.Kill(kvp.Key, complete: false);
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

        // Force instant alpha immediately so it doesn't wait for the Coroutine layout delay
        if (instant && animateAlpha && backgroundImage != null)
        {
            DOTween.Kill(backgroundImage, complete: false);
            Color c = backgroundImage.color;
            c.a = targetAlpha;
            backgroundImage.color = c;
        }

        // Hide items visually on Frame 0 BEFORE the layout yields to prevent flashes
        if (playIntroFirst && !instant)
        {
            for (int i = 0; i < gridLayout.transform.childCount; i++)
            {
                Transform child = gridLayout.transform.GetChild(i);
                DOTween.Kill(child, complete: false);
                child.localScale = Vector3.zero;
            }
        }
        else if (instant)
        {
            // If calling instantly on frame 0, hide objects so they don't sit in the wrong place while layout builds
            for (int i = 0; i < gridLayout.transform.childCount; i++)
            {
                Transform child = gridLayout.transform.GetChild(i);
                DOTween.Kill(child, complete: false);
                child.localScale = Vector3.zero;
            }
        }

        if (activeAnimationRoutine != null) StopCoroutine(activeAnimationRoutine);
        activeAnimationRoutine = StartCoroutine(AnimateGridRoutine(instant));
    }

    private IEnumerator AnimateGridRoutine(bool instant)
    {
        // Wait for Unity to completely build the UI Layout if called during scene load
        if (Time.frameCount < 5)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
        }

        CacheOriginalValues();
        int childCount = gridLayout.transform.childCount;

        if (instant)
        {
            ResetToOriginal(true, true);

            Vector2[] instantPositions = GetCenteredPositions(targetCellSize, targetSpacing, targetConstraintCount, targetPadding);

            gridLayout.enabled = false;

            for (int i = 0; i < childCount; i++)
            {
                RectTransform child = gridLayout.transform.GetChild(i) as RectTransform;
                child.anchoredPosition = instantPositions[i];
                if (animateCellSize) child.sizeDelta = targetCellSize;
                if (animateChildScale) child.localScale = Vector3.one;
            }

            if (animateAlpha && backgroundImage != null)
            {
                Color c = backgroundImage.color; c.a = targetAlpha;
                backgroundImage.color = c;
            }

            if (animateMaxFontSize)
            {
                for (int i = 0; i < childCount; i++)
                {
                    Transform gridChild = gridLayout.transform.GetChild(i);
                    if (gridChild.childCount > 0)
                    {
                        AutoAdjustTMP textAdjuster = gridChild.GetChild(0).GetComponent<AutoAdjustTMP>();
                        if (textAdjuster != null) textAdjuster.maxFontSize = targetMaxFontSize;
                    }
                }
            }

            if (enableSecondChildOnComplete) EnableSecondChildren();
            yield break;
        }

        // Snap items safely without killing this active coroutine and without restoring scale
        bool shouldRestoreScale = !playIntroFirst;
        ResetToOriginal(false, shouldRestoreScale);

        // ==========================================
        // PHASE 1: INTRO ANIMATION (0 to Default)
        // ==========================================
        if (playIntroFirst)
        {
            Tween lastTween = null;

            for (int i = 0; i < childCount; i++)
            {
                Transform child = gridLayout.transform.GetChild(i);

                Vector3 targetScale = originalChildScales.ContainsKey(child) ? originalChildScales[child] : Vector3.one;
                if (targetScale == Vector3.zero) targetScale = Vector3.one;

                lastTween = child.DOScale(targetScale, introDuration)
                    .SetDelay(i * introStagger)
                    .SetEase(introEase)
                    .SetTarget(child);
            }

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

        Vector2[] targetPositions = GetCenteredPositions(targetCellSize, targetSpacing, targetConstraintCount, targetPadding);

        ResetToOriginal(false, false);

        gridLayout.enabled = false;

        for (int i = 0; i < childCount; i++)
        {
            RectTransform child = gridLayout.transform.GetChild(i) as RectTransform;
            float childMoveDelay = startDelay + (i * staggerDelay);

            if (animateChildScale && startDelay > 0f)
            {
                child.DOScale(targetChildScale, startDelay)
                    .SetEase(childScaleEase)
                    .SetTarget(child);
            }

            child.DOAnchorPos(targetPositions[i], duration)
                .SetDelay(childMoveDelay)
                .SetEase(easeType)
                .SetTarget(child);

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
            .SetTarget(backgroundImage);
        }

        DOVirtual.DelayedCall(maxTotalTime, () => {
            for (int i = 0; i < childCount; i++)
            {
                RectTransform child = gridLayout.transform.GetChild(i) as RectTransform;
                child.anchoredPosition = targetPositions[i];
                if (animateCellSize) child.sizeDelta = targetCellSize;
                if (animateChildScale) child.localScale = Vector3.one;
            }

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
        toggles.SetActive(true);
        Taptic.Vibrate();
    }

}