using UnityEngine;
using DG.Tweening; // Required for DOTween

public class ImageScaler : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("The size the image will scale up to.")]
    public Vector3 targetScale = new Vector3(1.1f, 1.1f, 1.1f);

    [Tooltip("How long it takes to scale up (or down).")]
    public float duration = 0.6f;

    [Tooltip("The easing curve for the animation.")]
    public Ease easeType = Ease.InOutSine;

    private Vector3 originalScale;

    private void Awake()
    {
        // Remember the starting scale
        originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        // Reset scale in case the object was disabled mid-animation previously
        transform.localScale = originalScale;

        // --- DOTween Looping Animation ---
        // SetLoops(-1, LoopType.Yoyo) means loop infinitely (-1) and ping-pong back and forth (Yoyo)
        transform.DOScale(targetScale, duration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(easeType)
            .SetUpdate(true); // Set to true if you want it to animate even when Time.timeScale = 0 (game paused)
    }

    private void OnDisable()
    {
        // It's very important to kill the tween when the object is disabled or destroyed 
        // to prevent DOTween from trying to animate a missing object
        transform.DOKill();
    }
}