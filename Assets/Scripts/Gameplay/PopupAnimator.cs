using UnityEngine;
using DG.Tweening;

public class PopupAnimator : MonoBehaviour
{
    [Header("Animation Timings")]
    [Tooltip("Time it takes to scale from 0 to the default scale.")]
    public float introDuration = 0.4f;

    [Tooltip("How long to wait before scaling back down.")]
    public float waitDuration = 1.5f;

    [Tooltip("Time it takes to scale from the default scale back to 0.")]
    public float outroDuration = 0.3f;

    private Vector3 defaultScale;

    private void Awake()
    {
        // Save the original scale exactly as it is set in the Inspector
        defaultScale = transform.localScale;
    }

    private void OnEnable()
    {
        // 1. Reset scale to 0 immediately when enabled
        transform.localScale = Vector3.zero;

        // 2. Kill any currently running tweens on this object to prevent glitches
        transform.DOKill();

        // 3. Create a Sequence to chain the animations
        Sequence sequence = DOTween.Sequence();

        // Step A: Scale up (Intro)
        // Using Ease.OutBack gives it a slight overshoot/bounce for a juicy UI feel
        sequence.Append(transform.DOScale(defaultScale, introDuration).SetEase(Ease.OutBack));

        // Step B: Wait
        sequence.AppendInterval(waitDuration);

        // Step C: Scale down (Outro)
        // Using Ease.InBack makes it shrink slightly inward before popping away
        sequence.Append(transform.DOScale(Vector3.zero, outroDuration).SetEase(Ease.InBack));

        // Step D: Disable when finished
        sequence.OnComplete(() =>
        {
            transform.parent.gameObject.SetActive(false);
            gameObject.SetActive(false);
        });
    }

    private void OnDisable()
    {
        // Safety net: Ensure tweens are killed if the object is turned off manually
        transform.DOKill();
    }
}