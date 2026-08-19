using UnityEngine;
using DG.Tweening;

public class ScaleCycleDOTween : MonoBehaviour
{
    [Header("Scale Settings")]
    [Tooltip("The first size the object will scale to.")]
    public Vector3 firstTargetScale = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("The second size the object will scale to.")]
    public Vector3 secondTargetScale = new Vector3(1.5f, 1.5f, 1.5f);

    [Tooltip("How long (in seconds) the scaling animation takes.")]
    public float scaleDuration = 1.0f;

    [Header("Delay Settings")]
    [Tooltip("How long to wait after reaching the FIRST scale.")]
    public float waitAtFirstScale = 1.0f;

    [Tooltip("How long to wait after reaching the SECOND scale.")]
    public float waitAtSecondScale = 1.0f;

    [Header("Animation Settings")]
    [Tooltip("The easing curve for the scale animations.")]
    public Ease animationEase = Ease.InOutSine;


    private Tween currentTween;

    public void StartScalingCycle()
    {

        if (currentTween != null && currentTween.IsActive())
        {
            return;
        }

        currentTween = transform.DOScale(firstTargetScale, scaleDuration)
            .SetEase(animationEase)
            .OnComplete(StartLoopingSequence);
    }

    private void StartLoopingSequence()
    {

        Sequence loopSequence = DOTween.Sequence();

        loopSequence.AppendInterval(waitAtFirstScale);

      
        loopSequence.Append(transform.DOScale(secondTargetScale, scaleDuration).SetEase(animationEase));

       
        loopSequence.AppendInterval(waitAtSecondScale);

     
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
        if (currentTween != null)
        {
            currentTween.Kill();
        }
    }
}