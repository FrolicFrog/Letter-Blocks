using UnityEngine;
using DG.Tweening;

public class ScaleCycleDOTween : MonoBehaviour
{
    [Header("Parent Scale Settings")]
    public Vector3 firstTargetScale = new Vector3(0.5f, 0.5f, 0.5f);
    public Vector3 secondTargetScale = new Vector3(1.5f, 1.5f, 1.5f);
    public float scaleDuration = 1.0f;
    public float waitAtSecondScale = 1.0f;

    [Header("Child Shader Animation Settings")]
    [Tooltip("Starting inner radius (e.g., 0.0)")]
    public float startInnerRadius = 0.0f;

    [Tooltip("Starting outer radius (e.g., 0.1)")]
    public float startOuterRadius = 0.1f;

    [Tooltip("The maximum size the outer ring will expand to (e.g., 0.48)")]
    public float maxOuterRadius = 0.48f;

    [Tooltip("Time it takes for the ring to expand (Phase 1)")]
    public float phase1Duration = 0.5f;

    [Tooltip("Time it takes for the inner radius to catch up and vanish (Phase 2)")]
    public float phase2Duration = 0.5f;

    [Header("Animation Settings")]
    public Ease animationEase = Ease.InOutQuad;

    public Sprite sprite;

    private Tween currentTween;
    private Transform childTransform;
    private Material childMaterial;

    public void StartScalingCycle()
    {
        if (currentTween != null && currentTween.IsActive()) return;

        childTransform = transform.GetChild(0);
        childTransform.gameObject.SetActive(true);

        SpriteRenderer childRenderer = childTransform.GetComponent<SpriteRenderer>();
        if (childRenderer != null)
        {
            childMaterial = childRenderer.material;
        }

        GetComponent<SpriteRenderer>().sprite = sprite;

        // 1. Parent scales down to Vector1
        currentTween = transform.DOScale(firstTargetScale, scaleDuration)
            .SetEase(animationEase)
            .OnComplete(StartLoopingSequence);
    }

    private void StartLoopingSequence()
    {
        Sequence loopSequence = DOTween.Sequence();

        if (childMaterial != null && childMaterial.HasProperty("_InnerRadius") && childMaterial.HasProperty("_OuterRadius"))
        {
            Sequence childAnimSeq = DOTween.Sequence();

            // Calculate the thickness to maintain during Phase 1
            float ringThickness = startOuterRadius - startInnerRadius;
            float midInnerRadius = maxOuterRadius - ringThickness;

            // Reset shader values at the start of every loop cycle
            childAnimSeq.AppendCallback(() =>
            {
                childMaterial.SetFloat("_InnerRadius", startInnerRadius);
                childMaterial.SetFloat("_OuterRadius", startOuterRadius);
            });

            // PHASE 1: Outer and Inner both increase simultaneously, maintaining the difference
            childAnimSeq.Append(childMaterial.DOFloat(maxOuterRadius, "_OuterRadius", phase1Duration).SetEase(animationEase));
            childAnimSeq.Join(childMaterial.DOFloat(midInnerRadius, "_InnerRadius", phase1Duration).SetEase(animationEase));

            // PHASE 2: Outer stops. Inner increases to match Outer.
            childAnimSeq.Append(childMaterial.DOFloat(maxOuterRadius, "_InnerRadius", phase2Duration).SetEase(animationEase));

            loopSequence.Append(childAnimSeq);
        }
        else
        {
            loopSequence.AppendInterval(phase1Duration + phase2Duration);
        }

        // 3. Parent scales up to Vector2
        loopSequence.Append(transform.DOScale(secondTargetScale, scaleDuration).SetEase(animationEase));

        // 4. Delay at Vector2
        loopSequence.AppendInterval(waitAtSecondScale);

        // 5. Parent scales back to Vector1 to restart the loop
        loopSequence.Append(transform.DOScale(firstTargetScale, scaleDuration).SetEase(animationEase));

        loopSequence.SetLoops(-1, LoopType.Restart);
        currentTween = loopSequence;
    }

    public void StopScalingCycle()
    {
        if (currentTween != null)
        {
            currentTween.Kill();
            currentTween = null;
        }
    }

    private void OnDestroy()
    {
        if (currentTween != null) currentTween.Kill();
    }
}